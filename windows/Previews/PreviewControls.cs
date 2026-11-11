namespace Mash.GUI.MNXArchiver;

public sealed class ImageSurface : Control {
    internal bool PassThrough;
    public ImageSurface() {
        DoubleBuffered = true;
        ResizeRedraw = true;
        }
    protected override void WndProc(ref Message message) {
        if (PassThrough && message.Msg == 0x0084) {
            message.Result = new IntPtr(-1);
            return;
            }
        base.WndProc(ref message);
        }
    }
public sealed class PreviewTextBox : RichTextBox {
    internal bool PassThrough;
    protected override void WndProc(ref Message message) {
        if (PassThrough && message.Msg == 0x0084) {
            message.Result = new IntPtr(-1);
            return;
            }
        base.WndProc(ref message);
        }
    }
