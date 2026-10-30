namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private static ComboBox? FindBackendChoice(Control control) {
        if (control is ComboBox choice && choice.Name == "interfaceBackendComboBox") {
            return choice;
            }
        foreach (Control child in control.Controls) {
            ComboBox? nested = FindBackendChoice(child);
            if (nested is not null) {
                return nested;
                }
            }
        return null;
        }

    private static NumericUpDown? FindOpacityControl(Control control) {
        if (control is NumericUpDown opacity && opacity.Name == "interfaceOpacityNumericUpDown") {
            return opacity;
            }
        foreach (Control child in control.Controls) {
            NumericUpDown? nested = FindOpacityControl(child);
            if (nested is not null) {
                return nested;
                }
            }
        return null;
        }

    private static CheckBox? FindColorsChoice(Control parent) {
        foreach (Control control in parent.Controls) {
            if (control is CheckBox choice && choice.Name == "useDesktopColorsCheckBox") {
                return choice;
                }
            CheckBox? nested = FindColorsChoice(control);
            if (nested is not null) {
                return nested;
                }
            }
        return null;
        }

    private void CheckSavedInterfaceOptions(string directory) {
        Backends.BackendAvailability? available = Backends.BackendRuntime.Availability;
        if (available is null) {
            return;
            }
        string config = Path.Combine(directory, "interface-preference-check");
        global::Mash.UI.MNXFile.GUI.Settings.AppSettings original = new global::Mash.UI.MNXFile.GUI.Settings.AppSettings(config);
        original.Set("Interface", "Backend", "auto");
        original.Set("Interface", "OpacityPercent", 100);
        original.Set("Future", "Preserve", "あいう");
        original.Save();
        using (OptionsForm cancelled = new OptionsForm(original)) {
            ComboBox? choice = FindBackendChoice(cancelled.Controls.Owner);
            Require(choice is not null, "MWin options omitted the interface choice.");
            Require(choice!.Enabled == (available.MWinAvailable && available.Gtk3Available),
                "Interface selection is enabled without both backends.");
            CheckBox? colors = FindColorsChoice(cancelled.Controls.Owner);
            Require(colors is not null && colors.Checked, "Desktop colors must be enabled by default.");
            colors!.Checked = false;
            choice.SelectedIndex = (int)Backends.GuiBackend.Gtk3;
            FindOpacityControl(cancelled.Controls.Owner)!.Value = 65;
            cancelled.Close();
            }
        Require(Backends.BackendRuntime.LoadPreference(config) == Backends.GuiBackend.Automatic,
            "Cancelling options saved the backend preference.");
        Require(global::Mash.UI.MNXFile.GUI.Settings.AppSettings.Load(config).OpacityPercent == 100,
            "Cancelling options persisted opacity changes.");
        Require(global::Mash.UI.MNXFile.GUI.Settings.AppSettings.Load(config).Flag("Interface", "UseDesktopColors", true),
            "Cancelling Options must preserve the colors switch.");
        if (available.MWinAvailable && available.Gtk3Available) {
            using OptionsForm saved = new OptionsForm(original);
            ComboBox choice = FindBackendChoice(saved.Controls.Owner)!;
            choice.SelectedIndex = (int)Backends.GuiBackend.Gtk3;
            FindOpacityControl(saved.Controls.Owner)!.Value = 65;
            FindColorsChoice(saved.Controls.Owner)!.Checked = false;
            saved.SaveSettings();
            Require(global::Mash.UI.MNXFile.GUI.Settings.AppSettings.Load(config).OpacityPercent == 65,
                "Options did not persist the chosen opacity.");
            Require(!global::Mash.UI.MNXFile.GUI.Settings.AppSettings.Load(config).Flag("Interface", "UseDesktopColors", true),
                "Options must save the colors off switch.");
            Require(saved.DialogResult == DialogResult.OK, "Saving interface options did not succeed.");
            Require(Backends.BackendRuntime.LoadPreference(config) == Backends.GuiBackend.Gtk3,
                "MWin options did not persist the selected backend.");
            Require(global::Mash.UI.MNXFile.GUI.Settings.AppSettings.Load(config).Get("Future", "Preserve", "") == "あいう",
                "Interface options discarded an unrelated INI value.");
            }
        }

    private static void CollectTopGroups(Control parent, List<GroupBox> groups) {
        foreach (Control child in parent.Controls) {
            if (child is GroupBox group) {
                groups.Add(group);
                }
            else {
                CollectTopGroups(child, groups);
                }
            }
        }

    private void CheckBackendOptions(string directory) {
        CheckDetailsHorizontalScrolling();
        CheckPreviewModes(directory);
        // Exercise the toolkit's lazy menu settings path before menu painting.
        // Fresh Linux config directories must never resolve beside the executable.
        string toolkitSettings = Application.UserAppDataPath;
        Require(Path.IsPathFullyQualified(toolkitSettings) && Directory.Exists(toolkitSettings),
            "Portable menu settings did not resolve to an absolute user directory.");
        global::Mash.UI.MNXFile.GUI.Settings.AppSettings expanded = new global::Mash.UI.MNXFile.GUI.Settings.AppSettings(Path.Combine(directory, "options-tab-check"));
        expanded.Set("Previews", "Enabled", true);
        expanded.Set("Presentation", "Customize", true);
        expanded.Set("Presentation", "LimitSize", true);
        expanded.Set("Defaults", "Encrypt", true);
        expanded.Set("General", "OverrideTemp", true);
        using OptionsForm options = new OptionsForm(expanded);
        TabControl? tabs = options.Controls.OfType<TabControl>().SingleOrDefault();
        Require(tabs is not null && tabs.TabPages.Count == 4,
            "Options must have four clearly separated category tabs.");
        string[] expected = { "General", "Compression & Encryption", "Previews", "Preview Display" };
        for (int index = 0; index < expected.Length; index++) {
            Require(tabs!.TabPages[index].Text == expected[index], "Unexpected options tab title.");
            tabs.SelectedIndex = index;
            options.Show();
            options.PerformLayout();
            using Bitmap image = new Bitmap(options.Width, options.Height);
            options.DrawToBitmap(image, options.ClientRectangle);
            image.Save(Path.Combine(directory, "options-tab-" + index + ".png"), Majorsilence.Forms.Drawing.Imaging.ImageFormat.Png);
            }
        List<GroupBox> groups = new List<GroupBox>();
        CollectTopGroups(options.Controls.Owner, groups);
        Require(groups.Count > 6 && groups.Select(group => group.Width).Distinct().Count() == 1,
            "Options top-level group boxes must have one uniform width across all tabs: " +
            string.Join(", ", groups.Select(group => group.Name + "=" + group.Width)));
        options.Close();
        CheckSavedInterfaceOptions(directory);
        double previous = MnxDialog.ConfiguredOpacity;
        MnxDialog.ConfiguredOpacity = 0.65;
        using (MnxDialog child = new MnxDialog()) {
            Require(Math.Abs(child.Opacity - 0.65) < 0.001,
                "Owned MWin dialogs ignored configured opacity.");
            }
        MnxDialog.ConfiguredOpacity = previous;
        }
    }
