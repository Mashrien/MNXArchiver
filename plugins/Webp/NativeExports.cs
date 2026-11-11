using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

namespace Mash.GUI.MNXArchiver.Plugins.Webp;

internal static unsafe class NativeExports {
    private static readonly ImageProviderDescription Description =
        new ImageProviderDescription("webp", "WebP", "1.0.1", "2.0.0", "2.1.999", [".webp"]);

    [UnmanagedCallersOnly(EntryPoint = "mnx_image_provider_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static uint GetAbiVersion() {
        return ImageProviderProtocol.AbiVersion;
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_image_provider_describe", CallConvs = [typeof(CallConvCdecl)])]
    public static int Describe(byte* destination, int capacity) {
        try {
            byte[] description = JsonSerializer.SerializeToUtf8Bytes(Description,
                ImageProviderJson.Default.ImageProviderDescription);
            if (destination != null && capacity >= description.Length) {
                description.AsSpan().CopyTo(new Span<byte>(destination, description.Length));
                }
            return description.Length;
            }
        catch (Exception) {
            return -1;
            }
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_image_provider_inspect", CallConvs = [typeof(CallConvCdecl)])]
    public static ImageProviderStatus Inspect(byte* source, int length, ImageProviderImageInfo* image) {
        if (image == null) {
            return ImageProviderStatus.InvalidDestination;
            }
        if (source == null || length <= 0) {
            return ImageProviderStatus.InvalidImage;
            }

        ImageProviderStatus status = WebpHeader.Inspect(new ReadOnlySpan<byte>(source, length),
            out int width, out int height);
        if (status == ImageProviderStatus.Success) {
            *image = new ImageProviderImageInfo {
                Width = width,
                Height = height,
                FrameCount = 1,
                TotalPlays = 1
                };
            }
        return status;
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_image_provider_decode", CallConvs = [typeof(CallConvCdecl)])]
    public static ImageProviderStatus Decode(byte* source, int length, nint* frames,
        long capacity, int width, int height, int stride, int frameCount, int* delays) {
        if (source == null || length <= 0) {
            return ImageProviderStatus.InvalidImage;
            }

        ImageProviderStatus inspected = WebpHeader.Inspect(new ReadOnlySpan<byte>(source, length),
            out int sourceWidth, out int sourceHeight);
        if (inspected != ImageProviderStatus.Success) {
            return inspected;
            }
        if (frameCount != 1 || frames == null || delays == null) {
            return ImageProviderStatus.InvalidDestination;
            }

        byte* destination = (byte*)frames[0];
        if (destination == null || !ValidDestination(capacity, stride, width, height, sourceWidth, sourceHeight)) {
            return ImageProviderStatus.InvalidDestination;
            }

        try {
            ImageProviderStatus status = DecodePixels(source, length, destination, width, height, stride, sourceWidth, sourceHeight);
            if (status == ImageProviderStatus.Success) {
                delays[0] = 0;
                }
            return status;
            }
        catch (Exception) {
            return ImageProviderStatus.DecodeFailed;
            }
        }

    private static bool ValidDestination(long capacity, int stride, int width, int height,
        int sourceWidth, int sourceHeight) {
        bool fullSize = width == sourceWidth && height == sourceHeight;
        bool halfSize = width == (sourceWidth + 1) / 2 && height == (sourceHeight + 1) / 2;
        if (!fullSize && !halfSize) {
            return false;
            }
        if (stride < width * 4) {
            return false;
            }
        return capacity >= (long)stride * height;
        }

    private static ImageProviderStatus DecodePixels(byte* source, int length, byte* destination,
        int width, int height, int stride, int sourceWidth, int sourceHeight) {
        using SKData data = SKData.Create((nint)source, length);
        using SKCodec? codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Webp) {
            return ImageProviderStatus.InvalidImage;
            }
        if (codec.Info.Width != sourceWidth || codec.Info.Height != sourceHeight) {
            return ImageProviderStatus.InvalidImage;
            }

        using SKColorSpace srgb = SKColorSpace.CreateSrgb();
        SKImageInfo output = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, srgb);
        SKCodecResult decoded = codec.GetPixels(output, (nint)destination, stride, new SKCodecOptions(0));
        if (decoded == SKCodecResult.InvalidScale) {
            return DecodeAndScale(codec, output, (nint)destination, stride, sourceWidth, sourceHeight);
            }
        return decoded == SKCodecResult.Success ? ImageProviderStatus.Success : ImageProviderStatus.DecodeFailed;
        }

    private static ImageProviderStatus DecodeAndScale(SKCodec codec, SKImageInfo output, nint destination,
        int stride, int sourceWidth, int sourceHeight) {
        SKImageInfo fullSize = new SKImageInfo(sourceWidth, sourceHeight, output.ColorType, output.AlphaType, output.ColorSpace);
        using SKBitmap original = new SKBitmap(fullSize);
        if (codec.GetPixels(fullSize, original.GetPixels()) != SKCodecResult.Success) {
            return ImageProviderStatus.DecodeFailed;
            }
        using SKPixmap target = new SKPixmap(output, destination, stride);
        if (!original.ScalePixels(target, new SKSamplingOptions(SKFilterMode.Linear))) {
            return ImageProviderStatus.DecodeFailed;
            }
        return ImageProviderStatus.Success;
        }
    }
