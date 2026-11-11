namespace Mash.GUI.MNXArchiver;

public partial class TextViewerForm : MnxDialog {
    public TextViewerForm() {
        InitializeComponent();
        }

    internal TextViewerForm(string title, string text) : this() {
        Text = title;
        contentTextBox.Text = text.Replace("\n", Environment.NewLine);
        }
    }
