using System.Runtime.InteropServices;
using SkiaSharp;

namespace Mash.GUI.MNXArchiver.Previews;

/// <summary>Decodes once into bounded pixel buffers. Playback never reopens a codec.</summary>
internal sealed class CachedImageDecoder : IDisposable {
    private readonly List<SKBitmap> _frames = new List<SKBitmap>();
    private readonly List<int> _delays = new List<int>();
    private bool _disposed;
    internal int Width { get; }
    internal int Height { get; }
    internal int FrameCount => _frames.Count;
    internal int TotalPlays { get; }
    internal long PixelBytes => (long)Width * Height * 4 * _frames.Count;
    internal int DecodeCount { get; private set; }

    internal static bool IsGif(byte[] bytes) {
        return bytes.AsSpan().StartsWith("GIF89a"u8) || bytes.AsSpan().StartsWith("GIF87a"u8);
        }

    internal CachedImageDecoder(int width, int height, int frameCount, int totalPlays,
        Action<nint[], int, int[]> decode) {
        Width = width;
        Height = height;
        TotalPlays = totalPlays;
        SKImageInfo output = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        nint[] pixels = new nint[frameCount];
        int[] delays = new int[frameCount];
        try {
            for (int index = 0; index < frameCount; index++) {
                SKBitmap frame = new SKBitmap(output);
                _frames.Add(frame);
                pixels[index] = frame.GetPixels();
                if (pixels[index] == 0) {
                    throw new IOException("Could not allocate an image provider frame.");
                    }
                }

            decode(pixels, _frames[0].RowBytes, delays);
            foreach (int delay in delays) {
                _delays.Add(delay <= 0 ? 40 : Math.Max(10, delay));
                }
            DecodeCount = frameCount;
            }
        catch {
            Dispose();
            throw;
            }
        }

    internal CachedImageDecoder(byte[] bytes, int width, int height, long decodedLimit) {
        Width = width;
        Height = height;
        GCHandle sourcePin = default;
        try {
            // Borrow the original bounded snapshot without another encoded-data copy.
            // The codec and stream are gone before the pin is released; only pixels remain.
            sourcePin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            using SKData source = SKData.Create(sourcePin.AddrOfPinnedObject(), bytes.Length);
            using SKCodec codec = SKCodec.Create(source) ?? throw new NotSupportedException("This image format requires the platform decoder.");
            if (codec.Info.Width < 1 || codec.Info.Height < 1 || codec.Info.Width > 16384 || codec.Info.Height > 16384
                || width < 1 || height < 1 || width > codec.Info.Width || height > codec.Info.Height) {
                throw new IOException("The decoded image dimensions disagree with its validated header.");
                }
            bool gif = IsGif(bytes);
            int count = gif ? Math.Max(1, codec.FrameCount) : 1;
            if (count > 1024) {
                throw new IOException("The animation exceeds the safe preview frame count.");
                }
            long workingBytes = checked((long)width * height * 4 * (count + 2L));
            // Some static formats cannot decode directly at the requested resolution.
            // Their full-size temporary raster must fit too, even in half-size mode.
            if (!gif && (width != codec.Info.Width || height != codec.Info.Height)) {
                workingBytes += (long)codec.Info.Width * codec.Info.Height * 8;
                }
            if (workingBytes > decodedLimit) {
                throw new IOException("The image's decoded frame cache exceeds its configured memory limit.");
                }
            TotalPlays = codec.RepetitionCount < 0 ? 0 : checked(codec.RepetitionCount + 1);
            SKCodecFrameInfo[] frameInfo = codec.FrameInfo;
            SKImageInfo target = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            for (int frameIndex = 0; frameIndex < count; frameIndex++) {
                SKBitmap frame = new SKBitmap(target);
                bool cached = false;
                try {
                    int requiredFrame = frameInfo.Length > frameIndex ? frameInfo[frameIndex].RequiredFrame : -1;
                    if (requiredFrame >= 0) {
                        if (requiredFrame >= _frames.Count) {
                            throw new IOException("The GIF refers to an unavailable preceding frame.");
                            }
                        // Skia identifies the compositing base after GIF disposal rules.
                        // Reuse those cached pixels instead of decoding preceding frames again.
                        using SKPixmap previous = _frames[requiredFrame].PeekPixels();
                        if (!previous.ReadPixels(target, frame.GetPixels(), frame.RowBytes)) {
                            throw new IOException("Could not initialize the animation's compositing canvas.");
                            }
                        }
                    SKCodecResult result = codec.GetPixels(target, frame.GetPixels(), frame.RowBytes,
                        new SKCodecOptions(frameIndex, requiredFrame));
                    if (result == SKCodecResult.InvalidScale && !gif) {
                        using SKBitmap fullSize = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                        result = codec.GetPixels(fullSize.Info, fullSize.GetPixels());
                        if (result == SKCodecResult.Success && !fullSize.ScalePixels(frame, new SKSamplingOptions(SKFilterMode.Linear))) {
                            throw new IOException("Could not reduce the decoded image.");
                            }
                        }
                    if (result != SKCodecResult.Success) {
                        throw new IOException("Image decoding failed: " + result + ".");
                        }
                    DecodeCount++;
                    _frames.Add(frame);
                    cached = true; // Ownership passes to the cache after a successful decode.
                    int delay = frameInfo.Length > frameIndex ? frameInfo[frameIndex].Duration : 0;
                    _delays.Add(delay <= 0 ? 40 : Math.Max(10, delay));
                    }
                finally {
                    if (!cached) {
                        frame.Dispose();
                        }
                    }
                }
            }
        catch {
            Dispose();
            throw;
            }
        finally {
            if (sourcePin.IsAllocated) {
                sourcePin.Free();
                }
            }
        }

    internal SKBitmap Frame(int index) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _frames[index];
        }

    internal int Delay(int index) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _delays[index];
        }

    internal void CopyRgbaFrame(int index, IntPtr destination, int rowBytes) {
        using SKPixmap pixels = Frame(index).PeekPixels();
        SKImageInfo target = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (!pixels.ReadPixels(target, destination, rowBytes)) {
            throw new IOException("Could not display the cached image pixels.");
            }
        }

    public void Dispose() {
        if (_disposed) {
            return;
            }
        _disposed = true;
        foreach (SKBitmap frame in _frames) {
            frame.Erase(SKColors.Transparent);
            frame.Dispose();
            }
        _frames.Clear();
        _delays.Clear();
        }
    }
