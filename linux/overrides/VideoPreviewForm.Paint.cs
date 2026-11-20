using SkiaSharp;

namespace Mash.GUI.MNXArchiver;

internal sealed partial class VideoPreviewForm {
    partial void PaintPlatformFrame(PaintEventArgs args, RectangleF bounds, IntPtr pixels, ref bool painted) {
        // Draw from the reusable display buffer. MWin's Graphics.DrawImage snapshots its source.
        SKImageInfo info = new SKImageInfo(_frameSize.Width, _frameSize.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using SKPixmap pixmap = new SKPixmap(info, pixels, _stride);
        using SKImage image = SKImage.FromPixels(pixmap);
        SKRect destination = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        args.Canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear));
        painted = true;
        }
    }
