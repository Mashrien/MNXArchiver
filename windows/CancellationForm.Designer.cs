#nullable enable
namespace Mash.GUI.MNXArchiver;

partial class CancellationForm {
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            components?.Dispose();
            }
        base.Dispose(disposing);
        }

    #region Windows Form Designer generated code

    private void InitializeComponent() {
        components = new System.ComponentModel.Container();
        explanationLabel = new System.Windows.Forms.Label();
        exportKeyCheckBox = new System.Windows.Forms.CheckBox();
        keepButton = new System.Windows.Forms.Button();
        deleteButton = new System.Windows.Forms.Button();
        buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
        SuspendLayout();
        Text = "Cancel operation";
        ClientSize = new System.Drawing.Size(524, 285);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        ControlBox = false;
        explanationLabel.Text = "Finish the current file and keep progress, or stop now and delete partial output.";
        explanationLabel.Dock = System.Windows.Forms.DockStyle.Top;
        explanationLabel.Height = 135;
        explanationLabel.Padding = new System.Windows.Forms.Padding(15);
        exportKeyCheckBox.Text = "Export an optional password key beside the remaining queue";
        exportKeyCheckBox.Dock = System.Windows.Forms.DockStyle.Top;
        exportKeyCheckBox.Height = 36;
        exportKeyCheckBox.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
        keepButton.Text = "Keep";
        keepButton.Name = "keepCancellation";
        keepButton.DialogResult = System.Windows.Forms.DialogResult.Yes;
        keepButton.Width = 100;
        deleteButton.Text = "Delete";
        deleteButton.Name = "deleteCancellation";
        deleteButton.DialogResult = System.Windows.Forms.DialogResult.No;
        deleteButton.Width = 100;
        buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        buttonPanel.Height = 48;
        buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        buttonPanel.Padding = new System.Windows.Forms.Padding(8);
        buttonPanel.Controls.Add(keepButton);
        buttonPanel.Controls.Add(deleteButton);
        Controls.Add(exportKeyCheckBox);
        Controls.Add(explanationLabel);
        Controls.Add(buttonPanel);
        AcceptButton = keepButton;
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Name = "CancellationForm";
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.Label explanationLabel = null!;
    private System.Windows.Forms.CheckBox exportKeyCheckBox = null!;
    private System.Windows.Forms.Button keepButton = null!;
    private System.Windows.Forms.Button deleteButton = null!;
    private System.Windows.Forms.FlowLayoutPanel buttonPanel = null!;
    }
