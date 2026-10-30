using Mash.IO.Format.MNX;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class InputSelectionForm : MnxDialog {
    internal string[] Paths => inputPathsListBox.Items.Cast<string>().ToArray();

    public InputSelectionForm() {
        InitializeComponent();
        addFilesButton.Click += (_, _) => {
            using OpenFileDialog chooser = new OpenFileDialog { Multiselect = true, Title = "Select input files" };
            if (chooser.ShowDialog(this) == DialogResult.OK) {
                AddPaths(chooser.FileNames);
                }
            };
        addFolderButton.Click += (_, _) => {
            using FolderBrowserDialog chooser = new FolderBrowserDialog { Description = "Select an input folder", UseDescriptionForTitle = true };
            if (chooser.ShowDialog(this) == DialogResult.OK) {
                AddPaths(new string[] { chooser.SelectedPath });
                }
            };
        removeButton.Click += (_, _) => {
            foreach (string selected in inputPathsListBox.SelectedItems.Cast<string>().ToArray()) {
                inputPathsListBox.Items.Remove(selected);
                }
            };
        continueButton.Click += (_, _) => {
            if (inputPathsListBox.Items.Count > 0) {
                DialogResult = DialogResult.OK;
                }
            };
        DragEnter += (_, args) => args.Effect = args.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, args) => {
            if (args.Data?.GetData(DataFormats.FileDrop) is string[] paths) {
                AddPaths(paths);
                }
            };
        }

    internal InputSelectionForm(string title) : this() {
        Text = title;
        }

    private void AddPaths(string[] paths) {
        foreach (string path in paths) {
            if (!inputPathsListBox.Items.Contains(path)) {
                inputPathsListBox.Items.Add(path);
                }
            }
        }
    }

internal sealed record CreationSettings(string Path, ArchiveCompression Compression, int Level, string? Password, bool Overwrite);

