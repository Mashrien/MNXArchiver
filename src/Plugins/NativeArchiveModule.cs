using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mash.GUI.MNXArchiver.Plugins;

/// <summary>A loaded native library and its declared archive formats and capabilities.</summary>
internal sealed unsafe class NativeArchiveModule {
    private readonly delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte**, int*, int> _request;
    private readonly delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte*, int, byte**, int*, int> _readEntry;
    private readonly delegate* unmanaged[Cdecl]<byte*, int, nint, nint, nint*, byte**, int*, int> _openEntryStream;
    private readonly delegate* unmanaged[Cdecl]<nint, long, byte*, int, nint, nint, int*, int> _readEntryAt;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _closeEntryStream;
    private readonly delegate* unmanaged[Cdecl]<byte*, void> _release;
    private readonly (PluginFormatVersion Version, byte[] Magic)[] _signatures;
    internal string LibraryName { get; }
    internal PluginFormat Format { get; }
    internal PluginFormatVersion Current { get; }
    internal int SignatureLength { get; }

    internal NativeArchiveModule(string path, PluginLibrary library) {
        LibraryName = Path.GetFileName(path);
        delegate* unmanaged[Cdecl]<uint> abi = (delegate* unmanaged[Cdecl]<uint>)library.GetExport("mnx_plugin_abi_version");
        uint version = abi();
        if (version != PluginProtocol.AbiVersion) {
            throw new NotSupportedException($"Plugin ABI {version} is unsupported (expected {PluginProtocol.AbiVersion}).");
            }

        _request = (delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte**, int*, int>)library.GetExport("mnx_plugin_request");
        _release = (delegate* unmanaged[Cdecl]<byte*, void>)library.GetExport("mnx_plugin_release");
        if (library.TryGetExport("mnx_plugin_read_entry", out nint readEntry)) {
            _readEntry = (delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte*, int, byte**, int*, int>)readEntry;
            }
        if (library.TryGetExport("mnx_plugin_open_entry_stream", out nint openEntryStream)
            && library.TryGetExport("mnx_plugin_read_entry_at", out nint readEntryAt)
            && library.TryGetExport("mnx_plugin_close_entry_stream", out nint closeEntryStream)) {
            _openEntryStream = (delegate* unmanaged[Cdecl]<byte*, int, nint, nint, nint*, byte**, int*, int>)openEntryStream;
            _readEntryAt = (delegate* unmanaged[Cdecl]<nint, long, byte*, int, nint, nint, int*, int>)readEntryAt;
            _closeEntryStream = (delegate* unmanaged[Cdecl]<nint, void>)closeEntryStream;
            }

        Format = Invoke(new PluginRequest { Operation = "describe" }).Format
            ?? throw new InvalidDataException("The plugin returned no format descriptor.");
        PluginDescriptorValidation.Validate(Format);
        if (_readEntry == null && Format.Versions.Any(entry => entry.Actions.HasFlag(PluginActions.ReadEntry))) {
            throw new InvalidDataException("The module advertises entry reads but does not export mnx_plugin_read_entry.");
            }
        if (_openEntryStream == null && Format.Versions.Any(entry => entry.Actions.HasFlag(PluginActions.StreamEntry))) {
            throw new InvalidDataException("The module advertises entry streams but is missing an entry stream export.");
            }
        Current = Format.Versions.Single(entry => entry.Version == Format.CurrentVersion);
        _signatures = Format.Versions.Select(entry => (entry, Convert.FromHexString(entry.MagicHex))).ToArray();
        SignatureLength = _signatures.Max(entry => entry.Magic.Length);

        // The module independently validates both release ranges. Refusing
        // negotiate leaves it unavailable even if its descriptor looked valid.
        Invoke(new PluginRequest { Operation = "negotiate" });
        }

    internal PluginMatch? Match(ReadOnlySpan<byte> header) {
        PluginMatch? result = null;
        foreach ((PluginFormatVersion version, byte[] magic) in _signatures) {
            if (!header.StartsWith(magic)) {
                continue;
                }
            if (result is null || magic.Length > result.SignatureLength) {
                result = new PluginMatch(this, version, magic.Length);
                }
            }
        return result;
        }

    internal void ReadEntry(PluginRequest request, byte[] destination, CancellationToken cancellation) {
        if (_readEntry == null) {
            throw new NotSupportedException("This module does not support in-memory entry reads.");
            }
        if (destination.Length > PluginProtocol.MaxPreviewBytes) {
            throw new IOException("The entry read exceeds the plugin buffer limit.");
            }

        PluginResponse response = Invoke(request, cancellation, destination);
        if (response.BytesRead != destination.Length) {
            throw new EndOfStreamException("The module did not fill the requested entry buffer.");
            }
        }

    internal PluginResponse Invoke(PluginRequest input, CancellationToken cancellation = default) {
        return Invoke(input, cancellation, null);
        }

    internal Stream OpenEntryStream(PluginRequest request, CancellationToken cancellation) {
        if (_openEntryStream == null) {
            throw new NotSupportedException("This module does not support entry streams.");
            }
        long length = request.EntrySize ?? throw new ArgumentException("An entry size is required.", nameof(request));

        nint handle = 0;
        try {
            Invoke(request, cancellation, null, &handle);
            cancellation.ThrowIfCancellationRequested();
            if (handle == 0) {
                throw new IOException("The archive module did not return an entry stream handle.");
                }

            Stream stream = new NativeModuleEntryStream(handle, _readEntryAt, _closeEntryStream, length, cancellation);
            handle = 0;
            return stream;
            }
        finally {
            if (handle != 0) {
                _closeEntryStream(handle);
                }
            }
        }

    private PluginResponse Invoke(PluginRequest input, CancellationToken cancellation, byte[]? entryBuffer,
        nint* entryStream = null) {
        cancellation.ThrowIfCancellationRequested();
        PluginRequest request = input with {
            HostVersion = PluginProtocol.HostVersion,
            MinPluginVersion = PluginProtocol.MinPluginVersion,
            MaxPluginVersion = PluginProtocol.MaxPluginVersion,
            };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(request, PluginJson.Default.PluginRequest);
        if (json.Length > PluginProtocol.MaxMessageBytes) {
            throw new IOException("The archive request is too large.");
            }

        byte* response = null;
        int length = 0;
        GCHandle state = default;
        try {
            nint cancel = 0;
            if (cancellation.CanBeCanceled) {
                state = GCHandle.Alloc(cancellation);
                cancel = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsCancelled;
                }

            nint cancelState = state.IsAllocated ? GCHandle.ToIntPtr(state) : 0;
            int status;
            fixed (byte* bytes = json, destination = entryBuffer) {
                if (entryStream != null) {
                    status = _openEntryStream(bytes, json.Length, cancel, cancelState, entryStream, &response, &length);
                    }
                else if (entryBuffer is null) {
                    status = _request(bytes, json.Length, cancel, cancelState, &response, &length);
                    }
                else {
                    status = _readEntry(bytes, json.Length, cancel, cancelState, destination, entryBuffer.Length,
                        &response, &length);
                    }
                }
            if (status != 0 || response == null) {
                throw new IOException($"The archive module returned transport status {status} with no usable response.");
                }
            if (length <= 0 || length > PluginProtocol.MaxMessageBytes) {
                throw new IOException($"Invalid archive module response length ({length}).");
                }
            PluginResponse result = JsonSerializer.Deserialize(new ReadOnlySpan<byte>(response, length), PluginJson.Default.PluginResponse)
                ?? throw new IOException("The archive plugin returned an empty response.");
            if (result.Cancelled) {
                throw new OperationCanceledException(result.Error ?? "Archive operation cancelled.", cancellation);
                }
            if (result.Error is not null) {
                throw new IOException(result.Error);
                }
            return result;
            }
        finally {
            if (response != null) {
                _release(response);
                }
            if (state.IsAllocated) {
                state.Free();
                }
            }
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int IsCancelled(nint state) {
        try {
            return GCHandle.FromIntPtr(state).Target is CancellationToken token && token.IsCancellationRequested ? 1 : 0;
            }
        catch {
            return 1;
            }
        }
    }

internal sealed record PluginMatch(NativeArchiveModule Module, PluginFormatVersion Version, int SignatureLength);
