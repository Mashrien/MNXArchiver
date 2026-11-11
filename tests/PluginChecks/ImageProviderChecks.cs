using System.Runtime.InteropServices;
using Mash.GUI.MNXArchiver.Plugins;

internal static class ImageProviderChecks {
    internal static void Run(PluginCatalog catalog) {
        NativeImageProvider provider = catalog.ImageProviderFor("test.WEBP")
            ?? throw new InvalidDataException("The WebP provider did not load.");
        byte[] encoded = Convert.FromBase64String("UklGRh4AAABXRUJQVlA4TBEAAAAvAoAAEAdQsiJXpoCBiOh/AAA=");
        ImageProviderImageInfo info = provider.Inspect(encoded);
        if (info.Width != 3 || info.Height != 3 || info.FrameCount != 1 || info.TotalPlays != 1) {
            throw new InvalidDataException("WebP header dimensions did not match the fixture.");
            }
        CheckPixels(provider, encoded, 3, tolerance: 0);
        CheckPixels(provider, encoded, 2, tolerance: 1);
        ExpectInvalidImage(() => provider.Inspect(encoded[..^1]));

        byte[] animated = Convert.FromBase64String(
            "UklGRsAAAABXRUJQVlA4WAoAAAACAAAAAQAAAQAAQU5JTQYAAAD/////AABBTk1GSAAAAAAAAAAAAAEAAAEAAGQAAAJWUDggMAAAANABAJ0BKgIAAgACADQloAJ0ugH4AAOwAP7wxAv/ILlhdcjX/yA/5Af8gP/48gAAAEFOTUZEAAAAAAAAAAAAAQAAAQAAZAAAAFZQOCAsAAAAlAEAnQEqAgACAAAANCWgAnS6AAOYAP75k2//kB//kB//kB//ID/iF3sgMAA="
            );
        ExpectInvalidImage(() => provider.Inspect(animated));
        Console.WriteLine("PASS: WebP discovery, direct BGRA pixels and alpha at full and half resolution, corrupt/animated rejection.");
        }

    private static void CheckPixels(NativeImageProvider provider, byte[] encoded, int dimension, int tolerance) {
        int stride = dimension * 4 + 4;
        int length = stride * dimension;
        nint buffer = Marshal.AllocHGlobal(length);
        try {
            provider.Decode(encoded, dimension, dimension, [buffer], stride, new int[1]);
            byte[] expected = [25, 50, 100, 128];
            for (int row = 0; row < dimension; row++) {
                for (int column = 0; column < dimension; column++) {
                    int offset = row * stride + column * 4;
                    for (int component = 0; component < expected.Length; component++) {
                        int actual = Marshal.ReadByte(buffer, offset + component);
                        if (Math.Abs(actual - expected[component]) > tolerance) {
                            throw new InvalidDataException("The provider returned incorrect premultiplied BGRA pixels.");
                            }
                        }
                    }
                }
            ExpectInvalidImage(() => provider.Decode(encoded, dimension, dimension, [buffer], 1, new int[1]));
            }
        finally {
            Marshal.FreeHGlobal(buffer);
            }
        }

    private static void ExpectInvalidImage(Action action) {
        try {
            action();
            }
        catch (IOException) {
            return;
            }
        throw new InvalidDataException("The provider accepted an invalid image request.");
        }
    }
