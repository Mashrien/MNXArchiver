using System.Drawing;
using System.Reflection;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Headless;
using MashWin;

internal static class Program {
    private static string evidence = "";
    private static int failures;

    private static int Main(string[] arguments) {
        HeadlessRenderer.Use();
        Application.SetDefaultFont(new Font("Noto Sans", 9));
        evidence = arguments.Length > 0 ? arguments[0] : Path.Combine(Path.GetTempPath(), "mnx-frame-checks");
        Directory.CreateDirectory(evidence);
        Check("Status divider spans the frame at its configured width", CheckStatusDivider);
        Check("Docked dialog content preserves rounded bottom corners", CheckDialogCorners);
        Check("Only title drags move an owned dialog's parent", CheckOwnedMovement);
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
        }

    private static void Check(string name, Action action) {
        try {
            action();
            Console.WriteLine("PASS: " + name);
            }
        catch (Exception error) {
            failures++;
            Console.WriteLine("FAIL: " + name + ": " + error.Message);
            }
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
            }
        }

    private static MWindow CreateWindow() {
        MWindow window = new MWindow();
        window.Size = new Size(480, 300);
        window.Settings.Frame.BorderThickness = 5;
        window.Settings.Frame.BorderColor = Color.Lime;
        window.Settings.Frame.CornerRadius = 28;
        window.Settings.MenuSeparator.Enabled = false;
        window.Settings.MenuSeparator.Color = Color.Lime;
        window.BackColor = Color.Black;
        window.Show();
        Application.DoEvents();
        return window;
        }

    private static Bitmap Capture(MWindow window, string name) {
        Bitmap image = new Bitmap(window.Width, window.Height);
        window.DrawToBitmap(image, window.ClientRectangle);
        image.Save(Path.Combine(evidence, name + ".png"));
        return image;
        }

    private static void CheckStatusDivider() {
        using MWindow window = CreateWindow();
        window.Settings.StatusBar.Height = 36;
        window.Settings.MenuSeparator.Thickness = 2;
        window.Settings.MenuSeparator.Color = Color.Aqua;
        window.Settings.StatusBar.ShowSeparator = true;
        window.StatusText.Text = "Ready";
        window.StatusBarStrip.Style.Border.Top.Color = SkiaSharp.SKColors.Yellow;
        using Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Magenta };
        window.Controls.Add(content);
        window.PerformLayout();
        Application.DoEvents();
        using Bitmap image = Capture(window, "status-divider");
        int top = window.StatusBarControls.Top;
        int left = window.Frame.BorderThickness + 1;
        int right = window.Width - left - 1;
        for (int x = left; x <= right; x++) {
            Require(image.GetPixel(x, top - 2).ToArgb() == Color.Aqua.ToArgb(), "Upper divider gap at x=" + x);
            Require(image.GetPixel(x, top - 1).ToArgb() == Color.Aqua.ToArgb(), "Lower divider gap at x=" + x);
            Require(image.GetPixel(x, top).ToArgb() == Color.Yellow.ToArgb(), "Status strip top edge gap at x=" + x);
            }
        Require(content.Bottom == top - 2, "Status divider must keep its own configured thickness.");
        Require(image.GetPixel(window.Width / 2, top - 3).ToArgb() == Color.Magenta.ToArgb(), "Divider enlarged into content.");
        }

    private static void CheckDialogCorners() {
        foreach (int radius in new int[] { 0, 12, 28, 48 }) {
            using MWindow window = CreateWindow();
            window.Settings.Frame.Resizable = false;
            window.Settings.Frame.ShowSizeGrip = false;
            window.Settings.Frame.CornerRadius = radius;
            window.Settings.StatusBar.Visible = false;
            Application.DoEvents();
            using Bitmap before = Capture(window, "dialog-empty-" + radius);
            using Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Magenta };
            window.Controls.Add(content);
            window.PerformLayout();
            Application.DoEvents();
            using Bitmap after = Capture(window, "dialog-content-" + radius);
            int covered = 0;
            int corner = Math.Max(12, radius + 15);
            for (int y = window.Height - corner; y < window.Height; y++) {
                for (int x = 0; x < window.Width; x++) {
                    if (x >= corner && x < window.Width - corner) {
                        continue;
                        }
                    if (before.GetPixel(x, y).ToArgb() == Color.Lime.ToArgb()
                        && after.GetPixel(x, y).ToArgb() != Color.Lime.ToArgb()) {
                        covered++;
                        }
                    }
                }
            Require(covered == 0, "Radius " + radius + " lost " + covered + " border pixels under docked content " + content.Bounds + ".");
            }
        }

    private static void CheckOwnedMovement() {
        using MWindow owner = CreateWindow();
        using MWindow child = CreateWindow();
        PropertyInfo? option = typeof(MWindow).GetProperty("MoveOwnerWithTitleBar");
        Require(option != null, "MWin has no opt-in owner movement for title drags.");
        option!.SetValue(child, true);
        child.Owner = owner;
        owner.Location = new Point(100, 100);
        child.Location = new Point(150, 160);
        Require(owner.Location == new Point(100, 100), "Initial positioning moved the parent.");
        int initialVersion = owner.OwnedDialogMoveVersion;
        Point offset = new Point(child.Left - owner.Left, child.Top - owner.Top);
        owner.Move += delegate {
            child.Location = new Point(owner.Left + offset.X, owner.Top + offset.Y);
            };
        HeadlessRenderer.MouseDown(child, 180, 14);
        child.Location = new Point(child.Left + 31, child.Top - 17);
        Require(owner.Location == new Point(131, 83), "Caption drag did not move the owner by the child delta.");
        Require(child.Location == new Point(owner.Left + offset.X, owner.Top + offset.Y), "Caption drag changed the relative offset.");
        Require(owner.OwnedDialogMoveVersion != initialVersion, "Caption movement did not identify the deliberate owner drag.");
        child.Location = new Point(child.Left - 9, child.Top + 4);
        Require(owner.Location == new Point(122, 87), "Repeated movement or the owner's follow callback changed the drag delta.");
        HeadlessRenderer.MouseUp(child, 180, 14);
        Point afterDrag = owner.Location;
        int afterDragVersion = owner.OwnedDialogMoveVersion;
        child.Location = new Point(240, 220);
        Require(owner.Location == afterDrag, "Programmatic movement after the drag moved the owner.");
        Require(owner.OwnedDialogMoveVersion == afterDragVersion, "Programmatic movement advanced the drag version.");
        HeadlessRenderer.MouseDown(child, child.Width - 2, child.Height - 2);
        child.Bounds = new Rectangle(child.Left - 10, child.Top - 10, child.Width + 10, child.Height + 10);
        HeadlessRenderer.MouseUp(child, child.Width - 2, child.Height - 2);
        Require(owner.Location == afterDrag, "Resize moved the parent.");
        option.SetValue(child, false);
        HeadlessRenderer.MouseDown(child, 180, 14);
        child.Location = new Point(child.Left + 20, child.Top + 20);
        HeadlessRenderer.MouseUp(child, 180, 14);
        Require(owner.Location == afterDrag, "Ordinary MWin windows unexpectedly moved their owner.");
        }
    }
