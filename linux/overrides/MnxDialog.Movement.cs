using System.Runtime.InteropServices;
using Majorsilence.Forms.Gtk4;

namespace Mash.GUI.MNXArchiver;

public partial class MnxDialog {
    private Majorsilence.Forms.Timer? ownerMovementTimer;
    private Point nativeDragOrigin;
    private Point nativeOwnerOrigin;
    private Point lastNativePosition;
    private IntPtr dragDisplay;

    protected override void OnTitleBarDragStarted(EventArgs arguments) {
        base.OnTitleBarDragStarted(arguments);
        // GTK4 Location is logical bookkeeping. On X11 read the real surfaces;
        // the compositor owns movement while GDK's native drag is active.
        if (!MoveOwnerWithTitleBar || Owner is null
            || !Gtk4HostInterop.TryReadX11Position(this, out nativeDragOrigin)
            || !Gtk4HostInterop.TryReadX11Position(Owner, out nativeOwnerOrigin)) {
            return;
            }
        Location = ToLogicalPosition(this, nativeDragOrigin);
        Owner.Location = ToLogicalPosition(Owner, nativeOwnerOrigin);
        lastNativePosition = nativeDragOrigin;
        global::Gtk.Window nativeWindow = Gtk4HostInterop.ToGtkWindow(this);
        IntPtr nativeDisplay = nativeWindow.GetDisplay().Handle.DangerousGetHandle();
        dragDisplay = ReadXDisplay(nativeDisplay);
        ownerMovementTimer ??= CreateOwnerMovementTimer();
        ownerMovementTimer.Start();
        }

    protected override void OnTitleBarDragEnded(EventArgs arguments) {
        ownerMovementTimer?.Stop();
        ApplyNativeOwnerMovement();
        dragDisplay = IntPtr.Zero;
        base.OnTitleBarDragEnded(arguments);
        }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            ownerMovementTimer?.Dispose();
            ownerMovementTimer = null;
            }
        base.Dispose(disposing);
        }

    private Majorsilence.Forms.Timer CreateOwnerMovementTimer() {
        Majorsilence.Forms.Timer timer = new Majorsilence.Forms.Timer();
        timer.Interval = 16;
        timer.Tick += TrackNativeTitleDrag;
        return timer;
        }

    private void TrackNativeTitleDrag(object? sender, EventArgs arguments) {
        ApplyNativeOwnerMovement();
        if (dragDisplay == IntPtr.Zero || IsDisposed || Owner is null || Owner.IsDisposed
            || QueryPointer(dragDisplay, DefaultRootWindow(dragDisplay), out _, out _, out _, out _, out _, out _, out uint mask) == 0
            || (mask & 256) == 0) { // Button1Mask: the compositor may consume the release.
            EndTitleBarDrag();
            }
        }

    private void ApplyNativeOwnerMovement() {
        if (dragDisplay == IntPtr.Zero || IsDisposed || Owner is null || Owner.IsDisposed
            || !Gtk4HostInterop.TryReadX11Position(this, out Point position) || position == lastNativePosition) {
            return;
            }
        lastNativePosition = position;
        Point parentPosition = new Point(nativeOwnerOrigin.X + position.X - nativeDragOrigin.X,
            nativeOwnerOrigin.Y + position.Y - nativeDragOrigin.Y);
        // Updating logical positions also raises normal MWin Move callbacks.
        Location = ToLogicalPosition(this, position);
        Owner.Location = ToLogicalPosition(Owner, parentPosition);
        Gtk4HostInterop.RestoreX11Position(Owner, parentPosition);
        }

    private static Point ToLogicalPosition(Majorsilence.Forms.Form form, Point position) {
        double scale = Math.Max(1, form.DesktopScaling);
        return new Point((int)Math.Round(position.X / scale), (int)Math.Round(position.Y / scale));
        }

    [DllImport("libgtk-4.so.1", EntryPoint = "gdk_x11_display_get_xdisplay")]
    private static extern IntPtr ReadXDisplay(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
    private static extern nuint DefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6", EntryPoint = "XQueryPointer")]
    private static extern int QueryPointer(IntPtr display, nuint window, out nuint root, out nuint child,
        out int rootX, out int rootY, out int windowX, out int windowY, out uint mask);
    }
