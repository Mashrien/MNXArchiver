using Mash.IO.Format.MNX;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class CreationForm : MnxDialog {
    private readonly string[] _inputs = Array.Empty<string>();
    internal CreationSettings? SelectedSettings { get; private set; }

    public CreationForm() {
        InitializeComponent();
        browseButton.Click += (_, _) => ChooseDestination();
        createButton.Click += (_, _) => Accept();
        }

    internal CreationForm(string[] inputs, AppSettings defaults, string? defaultPassword) : this() {
        _inputs = inputs;
        compressionComboBox.SelectedItem = defaults.Flag("Defaults", "Compress", true) ? defaults.Choice("Defaults", "Compressor", "Snappy", "Snappy", "LZ4", "Brotli") : "None";
        compressionLevelNumericUpDown.Value = defaults.Number("Defaults", "Level", 4, 1, 9);
        encryptionCheckBox.Checked = defaultPassword is not null;
        passwordTextBox.Text = defaultPassword ?? "";
        confirmationTextBox.Text = defaultPassword ?? "";
        string first = Path.TrimEndingDirectorySeparator(inputs[0]);
        string suggested = Directory.Exists(first) ? Path.GetFileName(first) : Path.GetFileNameWithoutExtension(first);
        if (suggested.Length == 0) {
            suggested = "Archive";
            }
        while (suggested.Length + 4 > 255) {
            int remove = char.IsLowSurrogate(suggested[suggested.Length - 1]) ? 2 : 1;
            suggested = suggested.Substring(0, suggested.Length - remove);
            }
        outputPathTextBox.Text = Path.Combine(Path.GetDirectoryName(first) ?? first, suggested + ".mnx");
        if (inputs.Contains(outputPathTextBox.Text, StringComparer.OrdinalIgnoreCase)) {
            outputPathTextBox.Text = Path.Combine(Path.GetDirectoryName(first)!, "Archive.mnx");
            }
        compressionComboBox.SelectedIndexChanged += (_, _) => UpdateControls();
        encryptionCheckBox.CheckedChanged += (_, _) => UpdateControls();
        UpdateControls();
        }

    private void ChooseDestination() {
            using SaveFileDialog chooser = new SaveFileDialog { Filter = "MNX archives (*.mnx)|*.mnx", FileName = Path.GetFileName(outputPathTextBox.Text), InitialDirectory = Path.GetDirectoryName(outputPathTextBox.Text), OverwritePrompt = false };
            if (chooser.ShowDialog(this) == DialogResult.OK) {
                outputPathTextBox.Text = chooser.FileName;
                }
        }

    private void UpdateControls() {
        compressionLevelNumericUpDown.Enabled = (string?)compressionComboBox.SelectedItem == "Brotli";
        passwordTextBox.Enabled = encryptionCheckBox.Checked;
        confirmationTextBox.Enabled = encryptionCheckBox.Checked;
        }

    private void Accept() {
        try {
            string path = ValidateDestination(outputPathTextBox.Text, _inputs);
            if (encryptionCheckBox.Checked && (passwordTextBox.Text.Length == 0 || passwordTextBox.Text != confirmationTextBox.Text)) {
                throw new ArgumentException("Enter a nonempty password and repeat it exactly.");
                }
            bool overwrite = System.IO.File.Exists(path);
            if (overwrite && MessageBox.Show(this, "Replace the existing archive?\n" + path, "Replace archive", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                return;
                }
            ArchiveCompression compression = Enum.Parse<ArchiveCompression>((string)compressionComboBox.SelectedItem!);
            SelectedSettings = new CreationSettings(path, compression, compression == ArchiveCompression.Brotli ? (int)compressionLevelNumericUpDown.Value : 0, encryptionCheckBox.Checked ? passwordTextBox.Text : null, overwrite);
            DialogResult = DialogResult.OK;
            }
        catch (Exception error) {
            MessageBox.Show(this, error.Message, "Cannot create archive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    internal static string ValidateDestination(string value, string[] inputs) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("Choose an archive filename.");
            }
        string path = Path.GetFullPath(value);
        if (!path.EndsWith(".mnx", StringComparison.OrdinalIgnoreCase)) {
            path += ".mnx";
            }
        if (!Directory.Exists(Path.GetDirectoryName(path))) {
            throw new DirectoryNotFoundException("The destination folder does not exist.");
            }
        ArchiveFile.ValidateDestinationName(Path.GetFileName(path));
        if (Directory.Exists(path)) {
            throw new IOException("The archive destination is a directory.");
            }
        foreach (string input in inputs) {
            string source = Path.GetFullPath(input);
            if (path.Equals(source, StringComparison.OrdinalIgnoreCase) || Directory.Exists(source) && path.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new IOException("The output archive must be outside its input files and folders.");
                }
            }
        if (System.IO.File.Exists(path)) {
            using FileStream existing = System.IO.File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read);
            }
        else {
            using FileStream probe = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
        return path;
        }
    }

