#nullable enable
namespace Mash.UI.MNXFile.GUI;

partial class TextPromptForm {
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
        promptPanel = new System.Windows.Forms.Panel();
        promptPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        instructionLabel = new System.Windows.Forms.Label();
        inputTextBox = new System.Windows.Forms.TextBox();
        acceptButton = new System.Windows.Forms.Button();
        cancelButton = new System.Windows.Forms.Button();
        SuspendLayout();
        Text = "MNX";
        ClientSize = new System.Drawing.Size(444, 185);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        instructionLabel.Dock = System.Windows.Forms.DockStyle.Top;
        instructionLabel.Height = 40;
        instructionLabel.Padding = new System.Windows.Forms.Padding(12);
        instructionLabel.Text = "Enter a value:";
        inputTextBox.Location = new System.Drawing.Point(15, 48);
        inputTextBox.Width = 405;
        acceptButton.Text = "OK";
        acceptButton.DialogResult = System.Windows.Forms.DialogResult.OK;
        acceptButton.Location = new System.Drawing.Point(245, 95);
        acceptButton.Width = 80;
        cancelButton.Text = "Cancel";
        cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        cancelButton.Location = new System.Drawing.Point(335, 95);
        cancelButton.Width = 80;
        promptPanel.Controls.Add(instructionLabel);
        promptPanel.Controls.Add(inputTextBox);
        promptPanel.Controls.Add(acceptButton);
        promptPanel.Controls.Add(cancelButton);
        Controls.Add(promptPanel);
        AcceptButton = acceptButton;
        CancelButton = cancelButton;
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Name = "TextPromptForm";
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.Panel promptPanel = null!;
    private System.Windows.Forms.Label instructionLabel = null!;
    private System.Windows.Forms.TextBox inputTextBox = null!;
    private System.Windows.Forms.Button acceptButton = null!;
    private System.Windows.Forms.Button cancelButton = null!;
    }
