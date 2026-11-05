using System.Drawing;
using System.Reflection;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using MashWin;

Majorsilence.Forms.Headless.HeadlessRenderer.Use();
Application.SetDefaultFont(new Font("Noto Sans", 9));
string evidence = args.Length == 1 ? args[0] : Path.Combine(Path.GetTempPath(), "mnx-menu-checks");
Directory.CreateDirectory(evidence);
using MWindow window = new MWindow();
window.Size = new Size(600, 400);
ToolStripSettings palette = window.Settings.ToolStrips;
palette.BackgroundColor = Color.FromArgb(32, 35, 38);
palette.MenuBackgroundColor = Color.FromArgb(23, 41, 53);
palette.TextColor = Color.FromArgb(250, 240, 220);
palette.HoverColor = Color.FromArgb(60, 90, 120);
palette.HoverTextColor = Color.Yellow;
palette.PressedTextColor = Color.Lime;
palette.DisabledTextColor = Color.FromArgb(123, 132, 145);
palette.CheckedTextColor = Color.Cyan;
palette.CheckedColor = Color.FromArgb(43, 65, 87);
palette.SeparatorColor = Color.FromArgb(144, 123, 109);
palette.BorderColor = Color.FromArgb(111, 122, 133);
palette.UseGradients = false;
using MenuStrip menu = new MenuStrip();
ToolStripMenuItem file = new ToolStripMenuItem("File");
ToolStripMenuItem nested = new ToolStripMenuItem("Nested");
nested.DropDownItems.Add(new ToolStripMenuItem("Child"));
file.DropDownItems.Add(new ToolStripMenuItem("Open"));
file.DropDownItems.Add(new ToolStripSeparator());
file.DropDownItems.Add(nested);
menu.Items.Add(file);
window.Controls.Add(menu);
using ContextMenuStrip context = new ContextMenuStrip();
context.Items.Add(new ToolStripMenuItem("Extract"));
window.ContextMenuStrip = context;
window.Show();
Application.DoEvents();
CheckMenuBar("normal-bar", palette.TextColor);
file.ShowDropDown();
Application.DoEvents();
CheckMenuBar("pressed-bar", palette.PressedTextColor);
MenuDropDown popup = OpenPopup(file);
Require(ReferenceEquals(popup.Renderer, menu.Renderer), "Generated popup did not inherit the MWin renderer.");
CheckBackground(popup, "file-menu", palette.MenuBackgroundColor);
nested.ShowDropDown();
Application.DoEvents();
MenuDropDown submenu = OpenPopup(nested);
Require(ReferenceEquals(submenu.Renderer, menu.Renderer), "Nested popup did not inherit the MWin renderer.");
CheckBackground(submenu, "nested-menu", palette.MenuBackgroundColor);
nested.HideDropDown();
file.HideDropDown();
context.Show(window.Controls.Owner, new Point(80, 80));
Application.DoEvents();
Require(ReferenceEquals(context.Renderer, menu.Renderer), "Context menu did not receive the MWin renderer.");
CheckBackground(context, "context-menu", palette.MenuBackgroundColor);
ToolStripMenuItem contextItem = (ToolStripMenuItem)context.Items[0];
SetHovered(contextItem, true);
CheckState(context, "hovered-menu", palette.HoverColor, palette.HoverTextColor);
SetHovered(contextItem, false);
contextItem.Checked = true;
CheckState(context, "checked-menu", palette.CheckedColor, palette.CheckedTextColor);
contextItem.Checked = false;
contextItem.Enabled = false;
CheckState(context, "disabled-menu", palette.MenuBackgroundColor, palette.DisabledTextColor);
contextItem.Enabled = true;
context.Hide();
palette.MenuBackgroundColor = Color.Ivory;
palette.TextColor = Color.Black;
context.Show(window.Controls.Owner, new Point(80, 80));
Application.DoEvents();
CheckBackground(context, "light-menu", palette.MenuBackgroundColor);
CheckState(context, "light-text", palette.MenuBackgroundColor, palette.TextColor);
context.Hide();
window.Close();
Console.WriteLine("PASS: real popup/nested/context menus; dark/light backgrounds, gutter, border and normal/hover/checked/disabled text.");

MenuDropDown OpenPopup(ToolStripMenuItem item) {
    PropertyInfo property = typeof(MenuItem).GetProperty("OpenDropDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
    return (MenuDropDown)(property.GetValue(item) ?? throw new InvalidOperationException("Menu did not open."));
    }

void CheckMenuBar(string name, Color text) {
    menu.Invalidate();
    using Bitmap image = new Bitmap(menu.Width, menu.Height);
    menu.DrawToBitmap(image, menu.ClientRectangle);
    image.Save(Path.Combine(evidence, name + ".png"));
    Require(CountPixels(image, palette.BackgroundColor) > 10, name + " lost the menu-bar background.");
    // Small glyphs can contain only antialiased pixels, with no fully opaque center.
    Require(CountPixels(image, text, 8) > 0, name + " ignored the configured menu-bar text color.");
    }

void CheckBackground(MenuDropDown popupControl, string name, Color expected) {
    using Bitmap image = new Bitmap(popupControl.Width, popupControl.Height);
    popupControl.DrawToBitmap(image, popupControl.ClientRectangle);
    image.Save(Path.Combine(evidence, name + ".png"));
    Color actual = image.GetPixel(image.Width - 5, image.Height - 5);
    Require(actual.ToArgb() == expected.ToArgb(), name + " background was " + actual + ", expected " + expected);
    Color gutter = image.GetPixel(5, image.Height - 5);
    Require(gutter.ToArgb() == expected.ToArgb(), name + " icon margin ignored the custom palette: " + gutter);
    // The toolkit reserves its nonclient frame outside the renderer's paint area.
    Require(image.GetPixel(popupControl.ClientRectangle.Right - 1, image.Height / 2).ToArgb() == palette.BorderColor.ToArgb(),
        name + " border ignored the configured palette.");
    }

void SetHovered(ToolStripMenuItem item, bool hovered) {
    // The real hover setter is internal to the toolkit, normally called by its
    // input dispatcher. Exercise the same state without moving the user's mouse.
    typeof(MenuItem).GetProperty("Hovered")!.SetValue(item, hovered);
    }

void CheckState(MenuDropDown popupControl, string name, Color background, Color text) {
    popupControl.Invalidate();
    using Bitmap image = new Bitmap(popupControl.Width, popupControl.Height);
    popupControl.DrawToBitmap(image, popupControl.ClientRectangle);
    image.Save(Path.Combine(evidence, name + ".png"));
    Require(CountPixels(image, background) > 10, name + " lost its background color.");
    Require(CountPixels(image, text) > 0, name + " lost its text color.");
    }

int CountPixels(Bitmap image, Color expected, int tolerance = 0) {
    int count = 0;
    for (int y = 0; y < image.Height; y++) {
        for (int x = 0; x < image.Width; x++) {
            Color actual = image.GetPixel(x, y);
            if (actual.A == expected.A && Math.Abs(actual.R - expected.R) <= tolerance
                && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance) {
                count++;
                }
            }
        }
    return count;
    }

void Require(bool success, string message) {
    if (!success) {
        throw new InvalidOperationException(message);
        }
    }
