using Mash.GUI.MNXArchiver.Settings;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private static T FindOption<T>(Control parent, string name) where T : Control {
        return FindOptionOrNull<T>(parent, name) ?? throw new KeyNotFoundException(name);
        }

    private static T? FindOptionOrNull<T>(Control parent, string name) where T : Control {
        foreach (Control child in parent.Controls) {
            if (child.Name == name && child is T match) {
                return match;
                }
            T? nested = FindOptionOrNull<T>(child, name);
            if (nested is not null) {
                return nested;
                }
            }
        return null;
        }

    private void CheckPreviewOverrideOptions(string directory) {
        AppSettings settings = new AppSettings(Path.Combine(directory, "override-options"));
        settings.Set("Previews", "Enabled", true);
        settings.Set("Previews", "BufferMiB", 32);
        settings.Set("Previews", "DecodedMiB", 512);
        settings.Set("Previews", "TotalMiB", 1024);
        settings.Set("MNX.Previews.OVERRIDE", "BufferMiB", 512);
        settings.Set("MNX.Previews.OVERRIDE", "DecodedMiB", 5120);
        settings.Set("MNX.Previews.OVERRIDE", "TotalMiB", 8192);
        settings.Set("MNX.Previews.OVERRIDE", "DebugFlags", 1);
        settings.Save();
        using OptionsForm options = new OptionsForm(settings);
        TabControl tabs = FindOption<TabControl>(options.Controls.Owner, "optionsTabs");
        tabs.SelectedIndex = 2;
        options.Show();
        Application.DoEvents();
        string[] numbers = { "previewsBufferMiBNumericUpDown", "previewsDecodedMiBNumericUpDown", "previewsTotalMiBNumericUpDown" };
        string[] labels = { "previewBufferOverrideLabel", "previewDecodedOverrideLabel", "previewTotalOverrideLabel" };
        decimal[] maxima = { 128, 1024, 2048 };
        string[] values = { "512 MiB", "5,120 MiB", "8,192 MiB" };
        for (int index = 0; index < numbers.Length; index++) {
            NumericUpDown numeric = FindOption<NumericUpDown>(options.Controls.Owner, numbers[index]);
            Label label = FindOption<Label>(options.Controls.Owner, labels[index]);
            Require(!numeric.Enabled && numeric.Maximum == maxima[index], "Override changed or enabled an ordinary numeric range.");
            Require(label.Visible && label.Text == values[index] && label.ForeColor.ToArgb() == Color.Red.ToArgb(),
                "Override value was not shown in red beside its disabled control.");
            Control group = label.Parent!;
            while (group is not GroupBox && group.Parent is not null) {
                group = group.Parent;
                }
            int right = group.PointToScreen(new Point(group.ClientRectangle.Right - group.Padding.Right, 0)).X;
            Require(label.PointToScreen(new Point(label.Width, 0)).X <= right,
                "An override indicator is clipped outside its memory group.");
            }
        Require(FindOption<Label>(options.Controls.Owner, "previewOverridesWarningLabel").Text == "WARNING: EXPERIMENTAL OVERRIDES IN USER CONF!",
            "Override warning is missing.");
        Require(tabs.Style.Border.Width == 1 && tabs.Style.Border.Color == Theme.ForegroundColor, "Tab outline does not follow the foreground color.");
        using (Bitmap screenshot = new Bitmap(options.Width, options.Height)) {
            options.DrawToBitmap(screenshot, options.ClientRectangle);
            screenshot.Save(Path.Combine(directory, "options-overrides.png"));
            }
        options.SaveSettings();
        AppSettings saved = AppSettings.Load(settings.DirectoryPath);
        Require(saved.PreviewBufferBytes == 512 * 1024 * 1024 && saved.TotalPreviewBytes == 8192L * 1024 * 1024 && saved.HalfResolutionPreviews,
            "Saving Options clamped the overrides back to ordinary limits.");
        }

    partial void FinishPlatformSmoke(string directory) {
        AppSettings settings = AppSettings.Load(Path.Combine(directory, "override-options"));
        using OptionsForm options = new OptionsForm(settings);
        FindOption<TabControl>(options.Controls.Owner, "optionsTabs").SelectedIndex = 2;
        options.Show();
        bool closed = false;
        FormClosed += (_, _) => closed = true;
        FindOption<Button>(options.Controls.Owner, "revertPreviewOverridesButton").PerformClick();
        Require(!AppSettings.Load(settings.DirectoryPath).HasPreviewOverrides, "Revert left its saved override category behind.");
        Require(closed && !Visible, "Revert did not close the main application window.");
        System.IO.File.WriteAllText(Path.Combine(directory, "override-revert-exit-PASS.txt"), "PASS: real revert click removed the saved category and closed the application.");
        }
    }
