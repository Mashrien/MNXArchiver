#nullable enable
namespace Mash.GUI.MNXArchiver;

partial class PreviewForm {
    private System.ComponentModel.IContainer? components;

    #region Windows Form Designer generated code

    private void InitializeComponent() {
        components = new System.ComponentModel.Container();
        textPreview = new PreviewTextBox();
        imageSurface = new ImageSurface();
        closePreviewButton = new System.Windows.Forms.Button();
        SuspendLayout();
        Frame.Resizable = true;
        Frame.ShowSizeGrip = true;
        Text = "Preview";
        ClientSize = new System.Drawing.Size(640, 514);
        StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        KeyPreview = true;
        BackColor = System.Drawing.SystemColors.Window;
        textPreview.Name = "textPreview";
        imageSurface.Name = "imageSurface";
        closePreviewButton.Name = "closePreviewButton";
        textPreview.ReadOnly = true;
        textPreview.DetectUrls = false;
        textPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
        textPreview.Dock = System.Windows.Forms.DockStyle.Fill;
        textPreview.WordWrap = true;
        textPreview.ShortcutsEnabled = true;
        textPreview.Text = "File text preview";
        imageSurface.Dock = System.Windows.Forms.DockStyle.Fill;
        imageSurface.BackColor = System.Drawing.Color.FromArgb(38, 38, 38);
        closePreviewButton.Text = "Close";
        closePreviewButton.Dock = System.Windows.Forms.DockStyle.Bottom;
        closePreviewButton.Height = 32;
        Controls.Add(textPreview);
        Controls.Add(imageSurface);
        Controls.Add(closePreviewButton);
        CancelButton = closePreviewButton;
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Name = "PreviewForm";
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private PreviewTextBox textPreview = null!;
    private ImageSurface imageSurface = null!;
    private System.Windows.Forms.Button closePreviewButton = null!;
    }
