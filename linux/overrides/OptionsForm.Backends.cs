using Mash.GUI.MNXArchiver.Backends;

namespace Mash.GUI.MNXArchiver;

public sealed partial class OptionsForm {
    partial void ConfigurePlatformOptions() {
        // The portable toolkit's theme tabs have no page frame by default.
        // Keep a visible foreground-colored outline around the tab control.
        optionsTabs.Style.Border.Width = 1;
        optionsTabs.Style.Border.Color = Theme.ForegroundColor;
        // Linux uses the desktop keyring rather than Windows DPAPI.
        label4.Text = "WARNING: PRIVATE/PERSONAL COMPUTER USE ONLY. Never enable on shared machines. The saved password is protected using your desktop keyring.";
        BackendAvailability? available = BackendRuntime.Availability;
        if (available is null) {
            return;
            }
        GroupBox group = new GroupBox();
        group.Text = "Interface";
        group.Name = "interfaceGroupBox";
        group.AutoSize = true;
        group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        group.Padding = new Padding(12, 24, 12, 12);
        FlowLayoutPanel body = new FlowLayoutPanel();
        body.AutoSize = true;
        body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        body.FlowDirection = FlowDirection.TopDown;
        body.WrapContents = false;
        body.Location = new Point(12, 24);
        ComboBox choice = new ComboBox();
        choice.Name = "interfaceBackendComboBox";
        choice.DropDownStyle = ComboBoxStyle.DropDownList;
        choice.Width = 300;
        choice.Items.Add(BackendSelection.Label(GuiBackend.Automatic));
        choice.Items.Add(BackendSelection.Label(GuiBackend.MWin));
        choice.Items.Add(BackendSelection.Label(GuiBackend.Gtk3));
        GuiBackend preference = BackendSelection.ParsePreference(_settings.Get("Interface", "Backend", "auto"));
        choice.SelectedIndex = (int)preference;
        choice.Enabled = available.MWinAvailable && available.Gtk3Available;
        body.Controls.Add(choice);
        Label current = new Label();
        current.AutoSize = true;
        current.Text = "Currently using: " + BackendSelection.Label(BackendRuntime.Current);
        body.Controls.Add(current);
        CheckBox useColors = new CheckBox();
        useColors.Name = "useDesktopColorsCheckBox";
        useColors.Text = "Use desktop colors (editable INI palette)";
        useColors.AutoSize = true;
        useColors.Checked = _settings.Flag("Interface", "UseDesktopColors", true);
        body.Controls.Add(useColors);
        _readControls.Add(() => _settings.Set("Interface", "UseDesktopColors", useColors.Checked));
        Label restart = new Label();
        restart.AutoSize = true;
        restart.Text = "Changes apply after restarting MNX. Automatic prefers MWin.";
        body.Controls.Add(restart);
        if (!choice.Enabled) {
            Label unavailable = new Label();
            unavailable.AutoSize = true;
            unavailable.Text = "Only one interface is available; install the other toolkit to enable selection.";
            body.Controls.Add(unavailable);
            }
        group.Controls.Add(body);
        optionsBodyPanel.Controls.Add(group);
        optionsBodyPanel.Controls.SetChildIndex(group, 0);
        _readControls.Add(() => {
            if (choice.Enabled) {
                _settings.Set("Interface", "Backend", BackendSelection.Key((GuiBackend)choice.SelectedIndex));
                }
            });
        }
    }
