using Mash.UI.MNXFile.GUI.Previews;
using SkiaSharp;

string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
byte[] bytes = File.ReadAllBytes(Path.Combine(fixtures, "disposal.gif"));
foreach (bool half in new bool[] { false, true }) {
    int width = half ? 128 : 256;
    int height = half ? 64 : 128;
    using CachedImageDecoder cache = new CachedImageDecoder(bytes, width, height, 16 * 1024 * 1024);
    Require(cache.Width == width && cache.Height == height && cache.FrameCount == 5, "The GIF cache has incorrect dimensions or frame count.");
    Require(cache.TotalPlays == 2 && cache.Delay(2) == 110, "GIF repetition or frame delays changed.");
    Require(cache.PixelBytes == (long)width * height * 4 * 5, "The pixel cache is not bounded by the selected resolution.");
    for (int loop = 0; loop < 20; loop++) {
        for (int frame = 0; frame < cache.FrameCount; frame++) {
            using SKBitmap expected = SKBitmap.Decode(Path.Combine(fixtures, "frame-" + frame + ".png"));
            SKBitmap actual = cache.Frame(frame);
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    SKColor wanted = expected.GetPixel(half ? x * 2 + 1 : x, half ? y * 2 + 1 : y);
                    SKColor got = actual.GetPixel(x, y);
                    Require(wanted.Alpha == 0 ? got.Alpha == 0 : wanted == got,
                        $"GIF compositing/disposal mismatch: half={half}, frame={frame}, pixel={x},{y}, expected={wanted}, got={got}.");
                    }
                }
            }
        }
    Require(cache.DecodeCount == 5, "Loop playback decoded the GIF again instead of reading cached pixels.");
    Console.WriteLine($"PASS: {(half ? "half" : "full")}-resolution cached GIF, transparency/local palettes/disposal, 20 loops with only 5 decodes.");
    cache.Dispose();
    Require(cache.PixelBytes == 0, "Disposed frames still occupy a cache slot.");
    bool released = false;
    try {
        cache.Frame(0);
        }
    catch (ObjectDisposedException) {
        released = true;
        }
    Require(released, "A disposed pixel cache remained accessible.");
    }
byte[] png = File.ReadAllBytes(Path.Combine(fixtures, "static.png"));
using (CachedImageDecoder cache = new CachedImageDecoder(png, 128, 64, 16 * 1024 * 1024)) {
    Require(cache.FrameCount == 1 && cache.Width == 128 && cache.Height == 64 && cache.Frame(0).GetPixel(10, 20) == SKColors.Red,
        "A codec without native reduced decoding did not produce a reduced static image.");
    }
bool rejected = false;
try {
    using CachedImageDecoder cache = new CachedImageDecoder(bytes, 256, 128, 1024);
    }
catch (IOException) {
    rejected = true;
    }
Require(rejected, "The cache ignored its decoded-memory budget.");
Console.WriteLine("PASS: reduced static image, memory limit and disposal.");

string? sample = Environment.GetEnvironmentVariable("MNX_GUI_GIF_SAMPLE");
if (!string.IsNullOrEmpty(sample)) {
    string path = sample;
    byte[] source = File.ReadAllBytes(path);
    using SKCodec header = SKCodec.Create(new SKMemoryStream(source));
    foreach (bool half in new bool[] { false, true }) {
        int width = half ? (header.Info.Width + 1) / 2 : header.Info.Width;
        int height = half ? (header.Info.Height + 1) / 2 : header.Info.Height;
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        using CachedImageDecoder cache = new CachedImageDecoder(source, width, height, 5120L * 1024 * 1024);
        long elapsed = clock.ElapsedMilliseconds;
        int decoded = cache.DecodeCount;
        for (int loop = 0; loop < 100; loop++) {
            for (int frame = 0; frame < cache.FrameCount; frame++) {
                cache.Frame(frame);
                }
            }
        Require(cache.DecodeCount == decoded, "A real GIF was decoded again while looping.");
        Console.WriteLine($"PASS: real {source.Length:N0}-byte GIF, {width}x{height}, {cache.FrameCount} frames; cache {cache.PixelBytes:N0} bytes, initial decode {elapsed:N0} ms; 100 loops added zero decodes.");
        }
    }

static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
        }
    }
