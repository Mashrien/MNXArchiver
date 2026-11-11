#nullable enable
namespace Mash.GUI.MNXArchiver;

partial class ArchiveDropForm {
    private System.ComponentModel.IContainer? components;
    private Panel bodyPanel = null!;
    private Label archiveNameLabel = null!;
    private Button openArchiveButton = null!;
    private Button addArchiveButton = null!;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            components?.Dispose();
            }
        base.Dispose(disposing);
        }

    private void InitializeComponent() {
        components = new System.ComponentModel.Container();
        bodyPanel = new Panel();
        archiveNameLabel = new Label();
        openArchiveButton = new Button();
        addArchiveButton = new Button();
        SuspendLayout();
        Text = "Dropped archive";
        Name = "ArchiveDropForm";
        ClientSize = new Size(430, 170);
        StartPosition = FormStartPosition.CenterParent;
        bodyPanel.Dock = DockStyle.Fill;
        bodyPanel.Padding = new Padding(14);
        archiveNameLabel.Dock = DockStyle.Top;
        archiveNameLabel.Height = 54;
        archiveNameLabel.Text = "Open this archive, or add it as a file?";
        openArchiveButton.Text = "Open Archive";
        openArchiveButton.Name = "openArchiveButton";
        openArchiveButton.DialogResult = DialogResult.Yes;
        openArchiveButton.SetBounds(14, 76, 190, 32);
        addArchiveButton.Text = "Add to Archive";
        addArchiveButton.Name = "addArchiveButton";
        addArchiveButton.DialogResult = DialogResult.No;
        addArchiveButton.SetBounds(212, 76, 190, 32);
        bodyPanel.Controls.Add(archiveNameLabel);
        bodyPanel.Controls.Add(openArchiveButton);
        bodyPanel.Controls.Add(addArchiveButton);
        Controls.Add(bodyPanel);
        AcceptButton = openArchiveButton;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(false);
        }
    }
