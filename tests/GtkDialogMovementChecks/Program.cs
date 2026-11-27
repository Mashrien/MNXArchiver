using System.Drawing;
using System.Reflection;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Gtk4;
using MashWin;
using Mash.GUI.MNXArchiver;

internal static class Program {
    private static int Main() {
        Gtk4Application.Use();
        Application.SetDefaultFont(new Font("Noto Sans", 9));
        using MWindow owner = new MWindow();
        using MnxDialog child = new MnxDialog();
        owner.Size = new Size(600, 400);
        child.Size = new Size(360, 240);
        child.Owner = owner;
        owner.Show();
        child.Show();
        Application.DoEvents();
        Gtk4HostInterop.RestoreX11Position(owner, new Point(80, 100));
        Gtk4HostInterop.RestoreX11Position(child, new Point(140, 160));
        Point ownerStart = ReadPosition(owner);
        Point childStart = ReadPosition(child);
        Console.WriteLine("Initial native owner: " + ownerStart + "; child: " + childStart);
        PressChrome(child, 150, 14);
        Point movedChild = new Point(childStart.X + 45, childStart.Y - 19);
        Gtk4HostInterop.RestoreX11Position(child, movedChild);
        // The compositor changes a GTK surface without updating Form.Location.
        // Exercise the same polling callback without moving the desktop pointer.
        MethodInfo tick = typeof(MnxDialog).GetMethod("TrackNativeTitleDrag", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The native caption bridge is missing.");
        tick.Invoke(child, new object?[] { null, EventArgs.Empty });
        Point expectedOwner = new Point(ownerStart.X + 45, ownerStart.Y - 19);
        Require(ReadPosition(owner) == expectedOwner, "The native owner did not follow the child delta.");
        Require(ReadPosition(child) == movedChild, "Owner movement changed the child's native offset.");
        Console.WriteLine("PASS: native X11 title movement preserved the offset by +45,-19.");
        Gtk4HostInterop.RestoreX11Position(child, new Point(movedChild.X + 12, movedChild.Y + 8));
        Application.DoEvents();
        Require(ReadPosition(owner) == expectedOwner, "Movement after pointer release moved the owner.");
        child.Settings.Frame.Resizable = true;
        PressChrome(child, child.Width - 2, child.Height - 2);
        Gtk4HostInterop.RestoreX11Position(child, new Point(movedChild.X + 25, movedChild.Y + 15));
        Application.DoEvents();
        Require(ReadPosition(owner) == expectedOwner, "A resize gesture moved the owner.");
        Console.WriteLine("PASS: native movement outside a caption gesture and resizing left the owner fixed.");
        return 0;
        }

    private static Point ReadPosition(Form form) {
        Require(Gtk4HostInterop.TryReadX11Position(form, out Point position), "An X11 display is required for native movement checks.");
        return position;
        }

    private static void PressChrome(MWindow window, int x, int y) {
        MethodInfo method = typeof(Form).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The portable mouse dispatcher is missing.");
        method.Invoke(window, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
            }
        }
    }
