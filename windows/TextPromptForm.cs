namespace Mash.GUI.MNXArchiver;

public partial class TextPromptForm : MnxDialog {
    public TextPromptForm() {
        InitializeComponent();
        }

    internal TextPromptForm(string title, string instruction, bool password) : this() {
        Text = title;
        instructionLabel.Text = instruction;
        inputTextBox.UseSystemPasswordChar = password;
        }

    internal string Input => inputTextBox.Text;
    }
