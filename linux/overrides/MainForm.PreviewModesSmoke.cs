using System.Text;
using Mash.UI.MNXFile.GUI.Settings;
using Mash.UI.MNXFile.GUI.Previews;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private void CheckPreviewModes(string directory) {
        AppSettings settings = new AppSettings(Path.Combine(directory, "preview-modes"));
        settings.Set("Previews", "Enabled", true);
        settings.Set("Previews", "Trigger", "Single-Click");
        Require(settings.Trigger == "Single-Click", "Single-Click must be accepted as a preview trigger.");
        settings.Set("Presentation", "Customize", true);
        settings.Set("Presentation", "WindowType", "Dialog");
        settings.Set("Presentation", "Origin", "Right of Main Window");
        using (PreviewManager previews = new PreviewManager(this, settings)) {
            previews.Show(new PreviewData(Encoding.UTF8.GetBytes("Preview dialog"), PreviewKind.Text, false), "dialog.txt", Location, false);
            PreviewForm dialog = (PreviewForm)previews.Windows[0];
            Require(!dialog.Settings.Frame.Resizable && !dialog.Settings.Frame.ShowSizeGrip && dialog.Settings.Frame.BorderThickness == 0 && !dialog.MinimizeBox && !dialog.MaximizeBox,
                "Dialog previews must have fixed minimal chrome.");
            Require(dialog.Controls.OfType<Button>().All(button => button.Text != "Close" || !button.Visible), "Dialog must not show a footer Close button.");
            Require(dialog.Bounds.Left == Bounds.Right && dialog.Bounds.Top == Bounds.Top, "Right-side preview must touch the main window's right edge.");
            dialog.Close();
            }
        settings.Set("Presentation", "WindowType", "Window");
        using (PreviewManager previews = new PreviewManager(this, settings)) {
            previews.Show(new PreviewData(Encoding.UTF8.GetBytes("Preview window"), PreviewKind.Text, false), "window.txt", Location, false);
            PreviewForm window = (PreviewForm)previews.Windows[0];
            Require(window.Settings.Frame.Resizable && window.Settings.Frame.ShowSizeGrip && window.MinimizeBox && window.MaximizeBox,
                "Window previews must retain their existing full chrome.");
            window.Close();
            }
        settings.Set("Presentation", "Mode", "Tooltip");
        settings.Set("Presentation", "Anchor", "File");
        using (PreviewManager previews = new PreviewManager(this, settings)) {
            previews.Show(new PreviewData(Encoding.UTF8.GetBytes("Tooltip"), PreviewKind.Text, false), "tooltip.txt", new Point(Bounds.Right - 1, Bounds.Bottom - 1), false);
            PreviewForm tooltip = (PreviewForm)previews.Windows[0];
            Require(Bounds.Contains(tooltip.Bounds), "Tooltips must remain entirely inside the main window.");
            tooltip.Close();
            }
        }
    }
