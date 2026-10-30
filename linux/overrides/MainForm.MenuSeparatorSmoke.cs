namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private void CheckMenuSeparatorDeviceBounds() {
        // A 2x device rectangle must be painted to its far edge even when the
        // managed strip reports half that size. This catches logical/device mixing.
        using MenuStrip strip = new MenuStrip();
        strip.Size = new Size(200, 24);
        using Bitmap image = new Bitmap(400, 48);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.Clear(Color.White);
        // Attach through MWindow so the renderer receives its separator settings.
        using MashWin.MWindow owner = new MashWin.MWindow();
        owner.Settings.MenuSeparator.Enabled = true;
        owner.Settings.MenuSeparator.Color = Color.Red;
        owner.Controls.Add(strip);
        ToolStripRenderer activeRenderer = strip.Renderer;
        activeRenderer.DrawToolStripBorder(new ToolStripRenderEventArgs(graphics, strip,
            new Rectangle(0, 0, image.Width, image.Height), Color.White));
        Require(image.GetPixel(399, 47).ToArgb() == Color.Red.ToArgb(),
            "The menu divider did not reach the device-pixel bottom/right edge.");
        Require(image.GetPixel(100, 23).ToArgb() == Color.White.ToArgb(),
            "The menu divider was drawn at the logical edge instead of the device edge.");
        }

    private void CheckMenuSeparator(string directory) {
        SaveScreenshot(Path.Combine(directory, "00-before-toolbar-spacing.png"));
        CheckMenuSeparatorDeviceBounds();
        Padding previousPadding = mainToolbar.Padding;
        int previousHeight = mainToolbar.Height;
        int previousButtonTop = newArchiveButton.Bounds.Top;
        ToolStripRenderer previousRenderer = mainMenu.Renderer;
        try {
            // Reproduce the requested six-pixel experiment without editing the Windows designer.
            mainToolbar.Padding = new Padding(previousPadding.Left, previousPadding.Top + 6,
                previousPadding.Right, previousPadding.Bottom);
            mainToolbar.Height = previousHeight + 6;
            PerformLayout();
            mainToolbar.PerformLayout();
            SaveScreenshot(Path.Combine(directory, "00-toolbar-six-pixels.png"));
            bool moved = newArchiveButton.Bounds.Top >= previousButtonTop + 6;
            // Suppress the menu-border hook to isolate spacing from the rendering fix.
            // The legacy parent-drawn line is still enabled throughout this check.
            mainMenu.Renderer = new ToolStripSystemRenderer();
            mainMenu.Invalidate();
            SaveScreenshot(Path.Combine(directory, "00-six-pixels-without-menu-border.png"));
            using (Bitmap originalLine = new Bitmap(Width, Height)) {
                this.DrawToBitmap(originalLine, ClientRectangle);
                Color coveredLine = originalLine.GetPixel(Width / 2, 52 + Settings.Frame.BorderThickness);
                Require(coveredLine.ToArgb() != Settings.MenuSeparator.Color.ToArgb(),
                    "The spacing experiment unexpectedly exposed the legacy parent-drawn separator.");
                }
            mainMenu.Renderer = previousRenderer;
            mainMenu.Invalidate();
            using Bitmap menu = new Bitmap(mainMenu.Width, mainMenu.Height);
            mainMenu.DrawToBitmap(menu, mainMenu.ClientRectangle);
            Color border = menu.GetPixel(mainMenu.Width / 2, mainMenu.Height - 1);
            bool dividerDrawn = border.ToArgb() == Settings.MenuSeparator.Color.ToArgb();
            // DrawToBitmap clears the control's dirty flag while painting into the
            // supplied bitmap, so refresh its screen cache before composing the form.
            mainMenu.Invalidate();
            using (Bitmap composed = new Bitmap(Width, Height)) {
                this.DrawToBitmap(composed, ClientRectangle);
                Color visibleBorder = composed.GetPixel(mainMenu.Left + mainMenu.Width / 2,
                    mainMenu.Bottom - 1);
                Require(visibleBorder.ToArgb() == Settings.MenuSeparator.Color.ToArgb(),
                    "The composed form covered the menu's own divider.");
                }
            System.IO.File.WriteAllText(Path.Combine(directory, "menu-separator-result.txt"),
                "Buttons moved six pixels: " + moved + "; previous top: " + previousButtonTop + "; current top: " + newArchiveButton.Bounds.Top + "; divider drawn: " + dividerDrawn + "; observed border: " + border);
            Require(moved && dividerDrawn,
                "Toolbar/menu rendering failed. Buttons moved: " + moved + "; divider drawn: " + dividerDrawn + "; observed " + border);
            }
        finally {
            mainMenu.Renderer = previousRenderer;
            mainMenu.Invalidate();
            mainToolbar.Padding = previousPadding;
            mainToolbar.Height = previousHeight;
            PerformLayout();
            }
        }
    }
