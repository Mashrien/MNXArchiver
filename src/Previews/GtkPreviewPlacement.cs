using System.Drawing;
using System.Runtime.InteropServices;
using Gtk;
using Action = System.Action;

namespace Mash.GUI.MNXArchiver.Previews;

internal sealed class GtkPreviewPlacement {
    private readonly Gtk.Window _parent;
    private readonly PreviewPresentation _presentation;
    private readonly Rectangle _parentBounds;
    private readonly Rectangle _screen;
    private readonly bool _relativeDialog;

    internal Size SizeLimit => _presentation.SizeLimit(_parentBounds, _screen);

    internal GtkPreviewPlacement(Gtk.Window parent, PreviewPresentation presentation) {
        _parent = parent;
        _presentation = presentation;
        _relativeDialog = presentation.Dialog && presentation.BesideParent;
        Gdk.Rectangle monitor = parent.Display.GetMonitorAtWindow(parent.Window).Workarea;
        _screen = new Rectangle(monitor.X, monitor.Y, monitor.Width, monitor.Height);
        // Use the client origin. The window manager's frame is outside the preview anchors.
        parent.Window.GetOrigin(out int x, out int y);
        parent.GetSize(out int width, out int height);
        _parentBounds = new Rectangle(x, y, width, height);
        }

    internal Gtk.Window CreateWindow(string title) {
        bool fullChrome = !_presentation.Tooltip && !_presentation.Dialog;
        Gtk.Window window = new Gtk.Window(_presentation.Tooltip || _relativeDialog ? WindowType.Popup : WindowType.Toplevel) {
            Title = "Preview — " + title,
            Decorated = fullChrome,
            Resizable = fullChrome,
            SkipTaskbarHint = !_presentation.Independent,
            TypeHint = _presentation.Tooltip ? Gdk.WindowTypeHint.Tooltip
                : _presentation.Dialog ? Gdk.WindowTypeHint.Dialog : Gdk.WindowTypeHint.Normal,
            AcceptFocus = !_presentation.Tooltip,
            DestroyWithParent = !_presentation.Independent,
            };
        GtkWindowAppearance.Apply(window);
        if (!_presentation.Independent) {
            window.TransientFor = _parent;
            }
        return window;
        }

    internal static Box CreateTitleBar(string title, Action close) {
        Box bar = new Box(Orientation.Horizontal, 0);
        bar.StyleContext.AddClass("titlebar");
        Label text = new Label(title) { Xalign = 0, Ellipsize = Pango.EllipsizeMode.End, Hexpand = true, MarginStart = 8 };
        Button button = new Button("×") { TooltipText = "Close preview", WidthRequest = 28, HeightRequest = 28 };
        button.Clicked += (_, _) => close();
        bar.PackStart(text, true, true, 0);
        bar.PackEnd(button, false, false, 0);
        return bar;
        }

    internal void Place(Gtk.Window window, Size size, Gdk.Rectangle anchor, int cursorX, int cursorY) {
        window.SetDefaultSize(size.Width, size.Height);
        Point location = _presentation.Position(size, _parentBounds, _screen,
            new Point(anchor.X, anchor.Y), new Point(cursorX, cursorY));
        bool popup = _presentation.Tooltip || _relativeDialog;
        if (popup) {
            window.Realize();
            }
        if (popup && PreviewWindow.IsWaylandDisplay(window.Display)) {
            int localX = location.X - _parentBounds.X;
            int localY = location.Y - _parentBounds.Y;
            if (_presentation.Tooltip && _presentation.Anchor == "Bottom-Right") {
                // Wayland anchors use the content box, excluding client-side decoration.
                Gdk.Rectangle area = _parent.Child.Allocation;
                localX = Math.Max(area.X, area.X + area.Width - size.Width - 16);
                localY = Math.Max(area.Y, area.Y + area.Height - size.Height - 16);
                }
            Gdk.Rectangle local = new Gdk.Rectangle(localX, localY, 1, 1);
            window.Window.TransientFor = _parent.Window;
            const int slideBothAxes = (1 << 2) | (1 << 3);
            gdk_window_move_to_rect(window.Window.Handle, ref local,
                Gdk.Gravity.NorthWest, Gdk.Gravity.NorthWest, slideBothAxes, 0, 0
                );
            return;
            }
        window.Move(location.X, location.Y);
        }

    // GtkSharp 3.24 does not bind the parent-relative GDK placement API.
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gdk_window_move_to_rect(IntPtr window, ref Gdk.Rectangle anchor,
        Gdk.Gravity rectangleAnchor, Gdk.Gravity windowAnchor, int hints, int offsetX, int offsetY
        );
    }
