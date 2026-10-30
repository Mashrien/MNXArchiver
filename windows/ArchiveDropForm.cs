namespace Mash.UI.MNXFile.GUI;

/// <summary>Ask whether a dropped archive should be opened or stored as an ordinary file.</summary>
public partial class ArchiveDropForm : MnxDialog {
    public ArchiveDropForm() {
        InitializeComponent();
        }

    internal ArchiveDropForm(string path) : this() {
        archiveNameLabel.Text = Path.GetFileName(path) + "\nOpen this archive, or add it as a file?";
        }
    }
