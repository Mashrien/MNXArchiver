using System.Globalization;
using Mash.UI.MNXFile.GUI.Settings;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Edits a settings copy; all editable controls live in OptionsForm.Designer.cs.</summary>
public sealed partial class OptionsForm : MnxDialog {
    private AppSettings _settings = null!;
    private DefaultKeyStore _keys = null!;
    private readonly List<Action> _readControls = new List<Action>();
    internal AppSettings SelectedSettings => _settings;

    public OptionsForm() {
        InitializeComponent();
        }

    internal OptionsForm(AppSettings settings) : this() {
        _settings = settings.Copy();
        _keys = new DefaultKeyStore(_settings);
        ConfigurePlatformOptions();
        opacityNumericUpDown.Value = _settings.OpacityPercent;
        _readControls.Add(() => _settings.Set("Interface", "OpacityPercent", (int)opacityNumericUpDown.Value));
        LoadCheckBox(overrideTempCheckBox, "General", "OverrideTemp", false);
        LoadCheckBox(defaultsCompressCheckBox, "Defaults", "Compress", true);
        LoadChoice(defaultsCompressorComboBox, "Defaults", "Compressor", "Snappy");
        LoadNumber(defaultsLevelNumericUpDown, "Defaults", "Level", (int)4m);
        LoadCheckBox(defaultEncryptionCheckBox, "Defaults", "Encrypt", false);
        LoadCheckBox(previewsEnabledCheckBox, "Previews", "Enabled", false);
        LoadNumber(previewsBufferMiBNumericUpDown, "Previews", "BufferMiB", (int)32m);
        LoadNumber(previewsDecodedMiBNumericUpDown, "Previews", "DecodedMiB", (int)512m);
        LoadNumber(previewsTotalMiBNumericUpDown, "Previews", "TotalMiB", (int)1024m);
        LoadChoice(previewsTriggerComboBox, "Previews", "Trigger", "Double-Click");
        LoadNumber(previewsDelayMsNumericUpDown, "Previews", "DelayMs", (int)750m);
        LoadCheckBox(textPlainCheckBox, "Text", "Plain", true);
        LoadExtensions(textPlainTypesTextBox, "Text", "PlainTypes");
        LoadCheckBox(textSourceCheckBox, "Text", "Source", true);
        LoadExtensions(textSourceTypesTextBox, "Text", "SourceTypes");
        LoadCheckBox(imagesStaticCheckBox, "Images", "Static", true);
        LoadExtensions(imagesStaticTypesTextBox, "Images", "StaticTypes");
        LoadCheckBox(imagesAnimatedCheckBox, "Images", "Animated", false);
        LoadCheckBox(presentationCustomizeCheckBox, "Presentation", "Customize", false);
        LoadChoice(presentationModeComboBox, "Presentation", "Mode", "Window");
        LoadChoice(presentationAnchorComboBox, "Presentation", "Anchor", "File");
        LoadChoice(presentationWindowTypeComboBox, "Presentation", "WindowType", "Dialog");
        LoadChoice(presentationOriginComboBox, "Presentation", "Origin", "Center");
        LoadChoice(presentationRelativeToComboBox, "Presentation", "RelativeTo", "Program Window");
        LoadCheckBox(presentationLimitSizeCheckBox, "Presentation", "LimitSize", false);
        LoadChoice(presentationSizeModeComboBox, "Presentation", "SizeMode", "Ratio");
        LoadChoice(presentationMaxXComboBox, "Presentation", "MaxX", "800");
        LoadChoice(presentationMaxYComboBox, "Presentation", "MaxY", "640");
        LoadChoice(presentationPercentComboBox, "Presentation", "Percent", "66");
        LoadChoice(presentationSizeRelativeToComboBox, "Presentation", "SizeRelativeTo", "Main Window");
        LoadChoice(presentationScaleModeComboBox, "Presentation", "ScaleMode", "Scale");
        tempPathTextBox.Text = _settings.Get("General", "TempPath", Path.GetTempPath());
        _readControls.Add(() => _settings.Set("General", "TempPath", tempPathTextBox.Text));
        previewsDelayMsNumericUpDown.Value = _settings.HoverDelay;
        _readControls.Add(() => _settings.Set("Previews", "DelayMs", (int)previewsDelayMsNumericUpDown.Value / 250 * 250));
        saveButton.Click += (_, _) => SaveSettings();
        browseTempButton.Click += (_, _) => ChooseTemp();
        passwordTextBox.TextChanged += (_, _) => UpdatePasswordState();
        confirmationTextBox.TextChanged += (_, _) => UpdatePasswordState();
        storeDefaultButton.Click += async (_, _) => await StoreKey();
        clearDefaultButton.Click += async (_, _) => await ClearKey();
        Shown += (_, _) => UpdateLayoutChoices();
        optionsTabs.SelectedIndexChanged += (_, _) => UpdateLayoutChoices();
        optionsTabs.SizeChanged += (_, _) => UpdateLayoutChoices();
        optionsScrollPanel.ClientSizeChanged += (_, _) => UpdateLayoutChoices();
        previewsEnabledCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        overrideTempCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        defaultsCompressCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        defaultEncryptionCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        textPlainCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        textSourceCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        imagesStaticCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        presentationCustomizeCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        presentationLimitSizeCheckBox.CheckedChanged += (_, _) => UpdateLayoutChoices();
        defaultsCompressorComboBox.SelectedIndexChanged += (_, _) => UpdateLayoutChoices();
        previewsTriggerComboBox.SelectedIndexChanged += (_, _) => UpdateLayoutChoices();
        presentationModeComboBox.SelectedIndexChanged += (_, _) => UpdateLayoutChoices();
        presentationSizeModeComboBox.SelectedIndexChanged += (_, _) => UpdateLayoutChoices();
        UpdateLayoutChoices();
        UpdatePasswordState();
        }

    partial void ConfigurePlatformOptions();

    private void UpdateLayoutChoices() {
        int bodyWidth = Math.Max(560, optionsTabs.ClientSize.Width - 52);
        foreach (FlowLayoutPanel body in new FlowLayoutPanel[] { optionsBodyPanel, compressionBodyPanel, previewBodyPanel, flowLayoutPanel16, displayBodyPanel }) {
            body.MinimumSize = new Size(bodyWidth, 0);
            body.MaximumSize = new Size(bodyWidth, 0);
            body.Width = bodyWidth;
            foreach (GroupBox group in body.Controls.OfType<GroupBox>()) {
                int groupWidth = bodyWidth - group.Margin.Horizontal;
                group.MinimumSize = new Size(groupWidth, 0);
                group.MaximumSize = new Size(groupWidth, 0);
                group.Width = groupWidth;
                }
            }
        groupBox4.Enabled = previewsEnabledCheckBox.Checked;
        int textWidth = Math.Max(500, compressionBodyPanel.Width - 64);
        label3.MaximumSize = new Size(textWidth, 0);
        label4.MaximumSize = new Size(textWidth, 0);
        label7.MaximumSize = new Size(textWidth, 0);
        flowLayoutPanel16.Visible = previewsEnabledCheckBox.Checked;
        flowLayoutPanel7.Visible = overrideTempCheckBox.Checked;
        flowLayoutPanel9.Visible = defaultsCompressCheckBox.Checked;
        flowLayoutPanel12.Visible = defaultEncryptionCheckBox.Checked;
        flowLayoutPanel23.Visible = textPlainCheckBox.Checked;
        flowLayoutPanel24.Visible = textSourceCheckBox.Checked;
        flowLayoutPanel27.Visible = imagesStaticCheckBox.Checked;
        flowLayoutPanel29.Visible = presentationCustomizeCheckBox.Checked;
        flowLayoutPanel35.Visible = presentationLimitSizeCheckBox.Checked;
        defaultsLevelNumericUpDown.Enabled = defaultsCompressorComboBox.Text == "Brotli";
        previewsDelayMsNumericUpDown.Enabled = previewsTriggerComboBox.Text == "Mouse-Over With Delay";
        tooltipOptionsPanel.Enabled = presentationModeComboBox.Text == "Tooltip";
        windowOptionsPanel.Enabled = presentationModeComboBox.Text != "Tooltip";
        pixelOptionsPanel.Enabled = presentationSizeModeComboBox.Text == "Pixels";
        ratioOptionsPanel.Enabled = presentationSizeModeComboBox.Text != "Pixels";
        }

    private void LoadCheckBox(CheckBox control, string section, string key, bool fallback) {
        control.Checked = _settings.Flag(section, key, fallback);
        _readControls.Add(() => _settings.Set(section, key, control.Checked));
        }

    private void LoadChoice(ComboBox control, string section, string key, string fallback) {
        string[] choices = control.Items.Cast<string>().ToArray();
        control.SelectedItem = _settings.Choice(section, key, fallback, choices);
        _readControls.Add(() => _settings.Set(section, key, control.SelectedItem?.ToString() ?? fallback));
        }

    private void LoadNumber(NumericUpDown control, string section, string key, int fallback) {
        control.Value = _settings.Number(section, key, fallback, (int)control.Minimum, (int)control.Maximum);
        _readControls.Add(() => _settings.Set(section, key, (int)control.Value));
        }

    private void LoadExtensions(TextBox control, string section, string key) {
        control.Text = _settings.Get(section, key, "");
        _readControls.Add(() => _settings.Set(section, key, AppSettings.Extensions(control.Text)));
        }
    private void ChooseTemp() {
        using FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Select a writable temporary folder", SelectedPath = Directory.Exists(tempPathTextBox.Text) ? tempPathTextBox.Text : Path.GetTempPath(), UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        try {
            AppSettings.ValidateTemp(dialog.SelectedPath);
            tempPathTextBox.Text = dialog.SelectedPath;
            }
        catch (Exception error) {
            Error(error);
            }
        }
    private void UpdatePasswordState() {
        bool stored = _keys.Exists;
        bool matches = passwordTextBox.Text.Length > 0 && passwordTextBox.Text == confirmationTextBox.Text;
        passwordMatchLabel.Text = stored ? "✓ Stored" : matches ? "● ✓ Match" : "● ✕ No match";
        passwordMatchLabel.ForeColor = stored || matches ? Color.ForestGreen : Color.Firebrick;
        passwordTextBox.Enabled = !stored;
        confirmationTextBox.Enabled = !stored;
        storeDefaultButton.Enabled = !stored && matches;
        }
    private async Task StoreKey() {
        string password = passwordTextBox.Text;
        Enabled = false;
        try {
            await Task.Run(() => _keys.Store(password));
            passwordTextBox.Clear();
            confirmationTextBox.Clear();
            }
        catch (Exception error) {
            Error(error);
            }
        finally {
            Enabled = true;
            UpdatePasswordState();
            }
        }
    private async Task ClearKey() {
        passwordTextBox.Clear();
        confirmationTextBox.Clear();
        defaultEncryptionCheckBox.Checked = false;
        Enabled = false;
        try {
            await Task.Run(() => {
                AppSettings current = AppSettings.Load(_settings.DirectoryPath);
                current.Set("Defaults", "Encrypt", false);
                current.Save();
                _keys.Clear();
                });
            }
        catch (Exception error) {
            Error(error);
            }
        finally {
            Enabled = true;
            UpdatePasswordState();
            }
        }
    internal void SaveSettings() {
        try {
            foreach (Action read in _readControls) {
                read();
                }
            if (overrideTempCheckBox.Checked) {
                AppSettings.ValidateTemp(tempPathTextBox.Text);
                }
            if (defaultEncryptionCheckBox.Checked && !_keys.Exists) {
                throw new ArgumentException("Store a matching default password first, or disable default encryption.");
                }
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
            }
        catch (Exception error) {
            Error(error);
            }
        }
    private void Error(Exception error) {
        MessageBox.Show(this, error.Message, "MNX Options", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    protected override void Dispose(bool disposing) {
        if (disposing) {
            passwordTextBox.Clear();
            confirmationTextBox.Clear();
            components?.Dispose();
            }
        base.Dispose(disposing);
        }
    }
