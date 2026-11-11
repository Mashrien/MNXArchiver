namespace Mash.GUI.MNXArchiver;

public partial class CancellationForm : MnxDialog {
    public CancellationForm() {
        InitializeComponent();
        }

    internal CancellationForm(string explanation, bool offerKey) : this() {
        explanationLabel.Text = explanation;
        exportKeyCheckBox.Visible = offerKey;
        Height = offerKey ? 290 : 245;
        }

    internal bool ExportKey => exportKeyCheckBox.Checked;
    }
