using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>A native image provider that writes pixels into buffers owned by the preview.</summary>
internal sealed unsafe class NativeImageProvider {
    private readonly delegate* unmanaged[Cdecl]<byte*, int, ImageProviderImageInfo*, ImageProviderStatus> _inspect;
    private readonly delegate* unmanaged[Cdecl]<byte*, int, nint*, long, int, int, int, int, int*, ImageProviderStatus> _decode;

    internal string LibraryName { get; }
    internal ImageProviderDescription Description { get; }

    internal NativeImageProvider(string path, nint library, nint versionExport) {
        LibraryName = Path.GetFileName(path);
        delegate* unmanaged[Cdecl]<uint> getVersion = (delegate* unmanaged[Cdecl]<uint>)versionExport;
        uint version = getVersion();
        if (version != ImageProviderProtocol.AbiVersion) {
            throw new NotSupportedException($"Image provider ABI {version} is unsupported.");
            }

        Description = ReadDescription(library);
        PluginDescriptorValidation.ValidateReleaseRanges(Description.Version, Description.MinHostVersion, Description.MaxHostVersion);
        PluginDescriptorValidation.ValidateIdentity(Description.Id, Description.Name);
        PluginDescriptorValidation.ValidateExtensions(Description.Extensions);

        _inspect = (delegate* unmanaged[Cdecl]<byte*, int, ImageProviderImageInfo*, ImageProviderStatus>)
            NativeLibrary.GetExport(library, "mnx_image_provider_inspect");
        _decode = (delegate* unmanaged[Cdecl]<byte*, int, nint*, long, int, int, int, int, int*, ImageProviderStatus>)
            NativeLibrary.GetExport(library, "mnx_image_provider_decode");
        }

    private static ImageProviderDescription ReadDescription(nint library) {
        delegate* unmanaged[Cdecl]<byte*, int, int> describe =
            (delegate* unmanaged[Cdecl]<byte*, int, int>)NativeLibrary.GetExport(library, "mnx_image_provider_describe");
        int length = describe(null, 0);
        if (length <= 0 || length > ImageProviderProtocol.MaxDescriptionBytes) {
            throw new InvalidDataException("The image provider descriptor has an invalid length.");
            }

        byte[] json = new byte[length];
        fixed (byte* destination = json) {
            if (describe(destination, length) != length) {
                throw new InvalidDataException("The image provider returned an incomplete descriptor.");
                }
            }

        return JsonSerializer.Deserialize(json, ImageProviderJson.Default.ImageProviderDescription)
            ?? throw new InvalidDataException("The image provider returned no descriptor.");
        }

    internal ImageProviderImageInfo Inspect(byte[] encoded) {
        ImageProviderImageInfo info = default;
        fixed (byte* source = encoded) {
            CheckStatus(_inspect(source, encoded.Length, &info));
            }

        if (info.Width is < 1 or > ImageProviderProtocol.MaxDimension ||
            info.Height is < 1 or > ImageProviderProtocol.MaxDimension) {
            throw new IOException("The image provider reported dimensions outside the preview limit.");
            }
        if (info.FrameCount is < 1 or > ImageProviderProtocol.MaxFrames || info.TotalPlays < 0) {
            throw new IOException("The image provider reported invalid frame or playback metadata.");
            }
        return info;
        }

    internal void Decode(byte[] encoded, int width, int height, nint[] frames, int stride, int[] delays) {
        if (frames.Length != delays.Length) {
            throw new ArgumentException("Every output frame must have a delay.");
            }

        long length = checked((long)stride * height);
        fixed (byte* source = encoded)
        fixed (nint* pixels = frames)
        fixed (int* frameDelays = delays) {
            CheckStatus(_decode(source, encoded.Length, pixels, length, width, height, stride, frames.Length, frameDelays));
            }
        }

    private void CheckStatus(ImageProviderStatus status) {
        if (status == ImageProviderStatus.Success) {
            return;
            }

        string explanation = status switch {
            ImageProviderStatus.InvalidImage => "the image header or data is invalid.",
            ImageProviderStatus.AnimatedImage => "this provider supports static images only.",
            ImageProviderStatus.InvalidDestination => "the requested pixel layout is invalid.",
            _ => "the decoder could not read the image."
            };
        throw new IOException($"{Description.Name}: {explanation}");
        }
    }
