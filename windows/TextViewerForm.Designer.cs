#nullable enable
namespace Mash.UI.MNXFile.GUI;

partial class TextViewerForm {
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
        contentTextBox = new System.Windows.Forms.TextBox();
        SuspendLayout();
        Frame.Resizable = true;
        Frame.ShowSizeGrip = true;
        Text = "MNX Information";
        ClientSize = new System.Drawing.Size(604, 495);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        contentTextBox.Multiline = true;
        contentTextBox.ReadOnly = true;
        contentTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        contentTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        Controls.Add(contentTextBox);
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Name = "TextViewerForm";
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.TextBox contentTextBox = null!;
    }
