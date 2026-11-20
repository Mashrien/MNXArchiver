using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mash.GUI.MNXArchiver.Plugins;

internal sealed class InvalidVideoException(string message) : IOException(message);

internal enum VideoProviderStatus {
    Success = 0,
    EndOfStream = 1,
    InvalidMedia = 2,
    MissingDependency = 3,
    Unsupported = 4,
    Cancelled = 5,
    Failed = 6,
    }

[StructLayout(LayoutKind.Sequential)]
internal struct VideoInfo {
    internal int Width;
    internal int Height;
    internal int FpsNumerator;
    internal int FpsDenominator;
    internal long DurationMs;
    }

internal sealed record VideoProviderDescription(string Id, string Name, string Version, string MinHostVersion,
    string MaxHostVersion, string[] Extensions);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VideoProviderDescription))]
internal partial class VideoProviderJson : JsonSerializerContext { }

internal sealed unsafe class NativeVideoProvider {
    [StructLayout(LayoutKind.Sequential)]
    private struct InputCallbacks {
        internal nint State;
        internal delegate* unmanaged[Cdecl]<nint, byte*, int, int> Read;
        internal delegate* unmanaged[Cdecl]<nint, long, int, long> Seek;
        internal delegate* unmanaged[Cdecl]<nint, int> Cancelled;
        }

    private const int ReadEof = -541478725;
    private const int ReadCancelled = -1414092869;
    private const int IoError = -5;
    private const int InvalidSeek = -22;
    private const int SizeQuery = 0x10000;
    private const int ForceSeek = 0x20000;
    private const int MaxDimension = 8192;

    private readonly delegate* unmanaged[Cdecl]<InputCallbacks*, uint, nint*, VideoProviderStatus> _open;
    private readonly delegate* unmanaged[Cdecl]<nint, VideoInfo*, VideoProviderStatus> _getInfo;
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, long, int, long*, VideoProviderStatus> _decode;
    private readonly delegate* unmanaged[Cdecl]<nint, long, VideoProviderStatus> _seek;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _close;

    internal string LibraryName { get; }
    internal VideoProviderDescription Description { get; }

    internal NativeVideoProvider(string path, PluginLibrary library, nint versionExport) {
        LibraryName = Path.GetFileName(path);
        delegate* unmanaged[Cdecl]<uint> getVersion = (delegate* unmanaged[Cdecl]<uint>)versionExport;
        uint version = getVersion();
        if (version != 1) {
            throw new NotSupportedException($"Video provider ABI {version} is unsupported.");
            }

        Description = ReadDescription(library);
        PluginDescriptorValidation.ValidateReleaseRanges(Description.Version, Description.MinHostVersion, Description.MaxHostVersion);
        PluginDescriptorValidation.ValidateIdentity(Description.Id, Description.Name);
        PluginDescriptorValidation.ValidateExtensions(Description.Extensions);

        _open = (delegate* unmanaged[Cdecl]<InputCallbacks*, uint, nint*, VideoProviderStatus>)
            library.GetExport("mnx_video_provider_open_stream");
        _getInfo = (delegate* unmanaged[Cdecl]<nint, VideoInfo*, VideoProviderStatus>)
            library.GetExport("mnx_video_provider_get_info");
        _decode = (delegate* unmanaged[Cdecl]<nint, byte*, long, int, long*, VideoProviderStatus>)
            library.GetExport("mnx_video_provider_decode_next_frame");
        _seek = (delegate* unmanaged[Cdecl]<nint, long, VideoProviderStatus>)
            library.GetExport("mnx_video_provider_seek_ms");
        _close = (delegate* unmanaged[Cdecl]<nint, void>)
            library.GetExport("mnx_video_provider_close");
        }

    private static VideoProviderDescription ReadDescription(PluginLibrary library) {
        delegate* unmanaged[Cdecl]<byte*, int, int> describe =
            (delegate* unmanaged[Cdecl]<byte*, int, int>)library.GetExport("mnx_video_provider_describe");
        int length = describe(null, 0);
        if (length is < 1 or > 16 * 1024) {
            throw new InvalidDataException("The video provider descriptor has an invalid length.");
            }

        byte[] bytes = new byte[length];
        fixed (byte* output = bytes) {
            if (describe(output, length) != length) {
                throw new InvalidDataException("The video provider returned an incomplete descriptor.");
                }
            }
        return JsonSerializer.Deserialize(bytes, VideoProviderJson.Default.VideoProviderDescription)
            ?? throw new InvalidDataException("The video provider returned no descriptor.");
        }

    /// <summary>Takes ownership of the source on success or failure.</summary>
    internal NativeVideoStream Open(Stream source, bool halfResolution, CancellationToken cancellation) {
        VideoInput input = new VideoInput(source, cancellation);
        GCHandle state = default;
        nint handle = 0;
        try {
            cancellation.ThrowIfCancellationRequested();
            if (!source.CanRead || !source.CanSeek) {
                throw new IOException("Video previews require a seekable archive entry.");
                }
            state = GCHandle.Alloc(input);
            InputCallbacks callbacks = new InputCallbacks {
                State = GCHandle.ToIntPtr(state),
                Read = &ReadInput,
                Seek = &SeekInput,
                Cancelled = &IsCancelled
                };
            CheckStatus(_open(&callbacks, halfResolution ? 1u : 0u, &handle), input);
            if (handle == 0) {
                throw new IOException("The video provider returned no stream handle.");
                }

            VideoInfo info = default;
            CheckStatus(_getInfo(handle, &info), input);
            if (info.Width is < 1 or > MaxDimension || info.Height is < 1 or > MaxDimension ||
                info.DurationMs < -1 || info.FpsNumerator < 0 || info.FpsDenominator <= 0) {
                throw new IOException("The video provider returned invalid dimensions or timing.");
                }
            return new NativeVideoStream(this, handle, state, input, info);
            }
        catch {
            Close(handle, state, input);
            throw;
            }
        }

    internal bool Decode(nint handle, VideoInput input, Span<byte> pixels, int stride, out long ptsMs) {
        input.Cancellation.ThrowIfCancellationRequested();
        long timestamp = 0;
        VideoProviderStatus status;
        fixed (byte* destination = pixels) {
            status = _decode(handle, destination, pixels.Length, stride, &timestamp);
            }
        CheckStatus(status, input, allowEnd: true);
        ptsMs = timestamp;
        return status != VideoProviderStatus.EndOfStream;
        }

    internal void Seek(nint handle, VideoInput input, long targetMs) {
        input.Cancellation.ThrowIfCancellationRequested();
        CheckStatus(_seek(handle, targetMs), input);
        }

    internal void Close(nint handle, GCHandle state, VideoInput input) {
        try {
            if (handle != 0) {
                _close(handle);
                }
            }
        finally {
            if (state.IsAllocated) {
                state.Free();
                }
            input.Stream.Dispose();
            }
        }

    private void CheckStatus(VideoProviderStatus status, VideoInput input, bool allowEnd = false) {
        input.Cancellation.ThrowIfCancellationRequested();
        if (input.Failure is Exception failure) {
            input.Failure = null;
            throw new IOException("The archive entry could not be read.", failure);
            }
        if (status == VideoProviderStatus.Success || (allowEnd && status == VideoProviderStatus.EndOfStream)) {
            return;
            }
        if (status == VideoProviderStatus.InvalidMedia) {
            throw new InvalidVideoException($"{Description.Name}: the file header or video data is invalid.");
            }
        string explanation = status switch {
            VideoProviderStatus.MissingDependency => "compatible FFmpeg shared libraries are unavailable.",
            VideoProviderStatus.Unsupported => "the container, codec, or operation is unsupported.",
            VideoProviderStatus.Cancelled => "the operation was cancelled.",
            _ => "the video could not be decoded."
            };
        throw new IOException($"{Description.Name}: {explanation}");
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReadInput(nint state, byte* destination, int capacity) {
        VideoInput input = VideoInput.FromState(state);
        try {
            if (input.Cancellation.IsCancellationRequested) {
                return ReadCancelled;
                }
            if (destination == null || capacity <= 0) {
                return IoError;
                }
            int count = input.Stream.Read(new Span<byte>(destination, capacity));
            return count == 0 ? ReadEof : count;
            }
        catch (Exception error) {
            input.Failure = error;
            return input.Cancellation.IsCancellationRequested ? ReadCancelled : IoError;
            }
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long SeekInput(nint state, long offset, int whence) {
        VideoInput input = VideoInput.FromState(state);
        try {
            if (input.Cancellation.IsCancellationRequested) {
                return ReadCancelled;
                }
            int origin = whence & ~ForceSeek;
            if (origin == SizeQuery) {
                return input.Stream.Length;
                }
            if (origin is < 0 or > 2) {
                return InvalidSeek;
                }
            return input.Stream.Seek(offset, (SeekOrigin)origin);
            }
        catch (ArgumentException) {
            return InvalidSeek;
            }
        catch (OverflowException) {
            return InvalidSeek;
            }
        catch (Exception error) {
            input.Failure = error;
            return input.Cancellation.IsCancellationRequested ? ReadCancelled : IoError;
            }
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsCancelled(nint state) {
        return VideoInput.FromState(state).Cancellation.IsCancellationRequested ? 1 : 0;
        }

    internal sealed class VideoInput(Stream stream, CancellationToken cancellation) {
        internal Stream Stream { get; } = stream;
        internal CancellationToken Cancellation { get; } = cancellation;
        internal Exception? Failure { get; set; }

        internal static VideoInput FromState(nint state) {
            return (VideoInput)GCHandle.FromIntPtr(state).Target!;
            }
        }
    }

/// <summary>Owns the decoder and archive entry. Calls must be serialized by the player.</summary>
internal sealed class NativeVideoStream(NativeVideoProvider provider, nint handle, GCHandle state,
    NativeVideoProvider.VideoInput input, VideoInfo info) : IDisposable {
    private nint _handle = handle;

    internal VideoInfo Info { get; } = info;

    internal bool DecodeNextFrame(Span<byte> bgra, int stride, out long ptsMs) {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        if (stride < Info.Width * 4 || (long)stride * Info.Height > bgra.Length) {
            throw new ArgumentException("The video frame destination is too small.", nameof(bgra));
            }
        return provider.Decode(_handle, input, bgra, stride, out ptsMs);
        }

    internal void Seek(long targetMs) {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        ArgumentOutOfRangeException.ThrowIfNegative(targetMs);
        provider.Seek(_handle, input, targetMs);
        }

    public void Dispose() {
        nint closed = _handle;
        if (closed == 0) {
            return;
            }
        _handle = 0;
        provider.Close(closed, state, input);
        }
    }
