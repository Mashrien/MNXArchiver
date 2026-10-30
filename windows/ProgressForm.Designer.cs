#nullable enable
namespace Mash.UI.MNXFile.GUI;

partial class ProgressForm {
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
        progressLayout = new System.Windows.Forms.TableLayoutPanel();
        archivePathLabel = new System.Windows.Forms.Label();
        archiveSettingsLabel = new System.Windows.Forms.Label();
        currentFileCaption = new System.Windows.Forms.Label();
        currentFileLabel = new System.Windows.Forms.Label();
        filePiecesLabel = new System.Windows.Forms.Label();
        fileProgressBar = new System.Windows.Forms.ProgressBar();
        archiveProgressLabel = new System.Windows.Forms.Label();
        totalProgressBar = new Mash.UI.MNXFile.GUI.PercentageProgressBar();
        buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
        cancelButton = new System.Windows.Forms.Button();
        SuspendLayout();
        progressLayout.SuspendLayout();
        buttonPanel.SuspendLayout();
        // ProgressForm
        Dock = System.Windows.Forms.DockStyle.None;
        Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        AutoSize = false;
        MinimumSize = new System.Drawing.Size(0, 0);
        MaximumSize = new System.Drawing.Size(0, 0);
        TabIndex = 0;
        TabStop = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        AutoScroll = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        ControlBox = true;
        KeyPreview = false;
        DialogResult = System.Windows.Forms.DialogResult.None;
        ClientSize = new System.Drawing.Size(720, 399);
        Name = "ProgressForm";
        Text = "MNX Archive Progress";
        Controls.Add(progressLayout);
        // progressLayout
        progressLayout.Location = new System.Drawing.Point(0, 0);
        progressLayout.Size = new System.Drawing.Size(720, 365);
        progressLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        progressLayout.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        progressLayout.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        progressLayout.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
        progressLayout.AutoSize = false;
        progressLayout.MinimumSize = new System.Drawing.Size(0, 0);
        progressLayout.MaximumSize = new System.Drawing.Size(0, 0);
        progressLayout.TabIndex = 1;
        progressLayout.TabStop = false;
        progressLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        progressLayout.AutoScroll = false;
        progressLayout.BorderStyle = System.Windows.Forms.BorderStyle.None;
        progressLayout.ColumnCount = 1;
        progressLayout.RowCount = 10;
        progressLayout.Name = "progressLayout";
        progressLayout.Text = "";
        progressLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
        progressLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
        progressLayout.Controls.Add(archivePathLabel, 0, 0);
        progressLayout.Controls.Add(archiveSettingsLabel, 0, 1);
        progressLayout.Controls.Add(currentFileCaption, 0, 2);
        progressLayout.Controls.Add(currentFileLabel, 0, 3);
        progressLayout.Controls.Add(filePiecesLabel, 0, 4);
        progressLayout.Controls.Add(fileProgressBar, 0, 5);
        progressLayout.Controls.Add(archiveProgressLabel, 0, 7);
        progressLayout.Controls.Add(totalProgressBar, 0, 8);
        progressLayout.Controls.Add(buttonPanel, 0, 9);
        // archivePathLabel
        archivePathLabel.Location = new System.Drawing.Point(19, 16);
        archivePathLabel.Size = new System.Drawing.Size(682, 48);
        archivePathLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        archivePathLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        archivePathLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        archivePathLabel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        archivePathLabel.AutoSize = false;
        archivePathLabel.MinimumSize = new System.Drawing.Size(0, 0);
        archivePathLabel.MaximumSize = new System.Drawing.Size(0, 0);
        archivePathLabel.TabIndex = 2;
        archivePathLabel.TabStop = false;
        archivePathLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        archivePathLabel.AutoEllipsis = true;
        archivePathLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        archivePathLabel.Name = "archivePathLabel";
        archivePathLabel.Text = "Example.mnx";
        // archiveSettingsLabel
        archiveSettingsLabel.Location = new System.Drawing.Point(19, 64);
        archiveSettingsLabel.Size = new System.Drawing.Size(682, 28);
        archiveSettingsLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        archiveSettingsLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        archiveSettingsLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        archiveSettingsLabel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        archiveSettingsLabel.AutoSize = false;
        archiveSettingsLabel.MinimumSize = new System.Drawing.Size(0, 0);
        archiveSettingsLabel.MaximumSize = new System.Drawing.Size(0, 0);
        archiveSettingsLabel.TabIndex = 3;
        archiveSettingsLabel.TabStop = false;
        archiveSettingsLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        archiveSettingsLabel.AutoEllipsis = false;
        archiveSettingsLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        archiveSettingsLabel.Name = "archiveSettingsLabel";
        archiveSettingsLabel.Text = "Compression: Snappy    -    Encrypted: No";
        // currentFileCaption
        currentFileCaption.Location = new System.Drawing.Point(19, 92);
        currentFileCaption.Size = new System.Drawing.Size(682, 24);
        currentFileCaption.Dock = System.Windows.Forms.DockStyle.Fill;
        currentFileCaption.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        currentFileCaption.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        currentFileCaption.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        currentFileCaption.AutoSize = false;
        currentFileCaption.MinimumSize = new System.Drawing.Size(0, 0);
        currentFileCaption.MaximumSize = new System.Drawing.Size(0, 0);
        currentFileCaption.TabIndex = 4;
        currentFileCaption.TabStop = false;
        currentFileCaption.BorderStyle = System.Windows.Forms.BorderStyle.None;
        currentFileCaption.AutoEllipsis = false;
        currentFileCaption.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        currentFileCaption.Name = "currentFileCaption";
        currentFileCaption.Text = "Current File:";
        // currentFileLabel
        currentFileLabel.Location = new System.Drawing.Point(19, 116);
        currentFileLabel.Size = new System.Drawing.Size(682, 40);
        currentFileLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        currentFileLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        currentFileLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        currentFileLabel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        currentFileLabel.AutoSize = false;
        currentFileLabel.MinimumSize = new System.Drawing.Size(0, 0);
        currentFileLabel.MaximumSize = new System.Drawing.Size(0, 0);
        currentFileLabel.TabIndex = 5;
        currentFileLabel.TabStop = false;
        currentFileLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        currentFileLabel.AutoEllipsis = true;
        currentFileLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        currentFileLabel.Name = "currentFileLabel";
        currentFileLabel.Text = "";
        // filePiecesLabel
        filePiecesLabel.Location = new System.Drawing.Point(19, 156);
        filePiecesLabel.Size = new System.Drawing.Size(682, 24);
        filePiecesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        filePiecesLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        filePiecesLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        filePiecesLabel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        filePiecesLabel.AutoSize = false;
        filePiecesLabel.MinimumSize = new System.Drawing.Size(0, 0);
        filePiecesLabel.MaximumSize = new System.Drawing.Size(0, 0);
        filePiecesLabel.TabIndex = 6;
        filePiecesLabel.TabStop = false;
        filePiecesLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        filePiecesLabel.AutoEllipsis = false;
        filePiecesLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        filePiecesLabel.Name = "filePiecesLabel";
        filePiecesLabel.Text = "";
        // fileProgressBar
        fileProgressBar.Location = new System.Drawing.Point(19, 183);
        fileProgressBar.Size = new System.Drawing.Size(682, 18);
        fileProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
        fileProgressBar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        fileProgressBar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        fileProgressBar.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        fileProgressBar.AutoSize = false;
        fileProgressBar.MinimumSize = new System.Drawing.Size(0, 0);
        fileProgressBar.MaximumSize = new System.Drawing.Size(0, 0);
        fileProgressBar.TabIndex = 7;
        fileProgressBar.TabStop = true;
        fileProgressBar.Minimum = 0;
        fileProgressBar.Maximum = 100;
        fileProgressBar.Name = "fileProgressBar";
        fileProgressBar.Text = "";
        fileProgressBar.Value = 0;
        // archiveProgressLabel
        archiveProgressLabel.Location = new System.Drawing.Point(19, 222);
        archiveProgressLabel.Size = new System.Drawing.Size(682, 45);
        archiveProgressLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        archiveProgressLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        archiveProgressLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
        archiveProgressLabel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        archiveProgressLabel.AutoSize = false;
        archiveProgressLabel.MinimumSize = new System.Drawing.Size(0, 0);
        archiveProgressLabel.MaximumSize = new System.Drawing.Size(0, 0);
        archiveProgressLabel.TabIndex = 8;
        archiveProgressLabel.TabStop = false;
        archiveProgressLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        archiveProgressLabel.AutoEllipsis = false;
        archiveProgressLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        archiveProgressLabel.Name = "archiveProgressLabel";
        archiveProgressLabel.Text = "";
        // totalProgressBar
        totalProgressBar.Location = new System.Drawing.Point(19, 270);
        totalProgressBar.Size = new System.Drawing.Size(682, 20);
        totalProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
        totalProgressBar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        totalProgressBar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        totalProgressBar.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        totalProgressBar.AutoSize = false;
        totalProgressBar.MinimumSize = new System.Drawing.Size(0, 0);
        totalProgressBar.MaximumSize = new System.Drawing.Size(0, 0);
        totalProgressBar.TabIndex = 9;
        totalProgressBar.TabStop = true;
        totalProgressBar.Name = "totalProgressBar";
        totalProgressBar.Text = "";
        // buttonPanel
        buttonPanel.Location = new System.Drawing.Point(19, 296);
        buttonPanel.Size = new System.Drawing.Size(682, 50);
        buttonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        buttonPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        buttonPanel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        buttonPanel.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
        buttonPanel.AutoSize = false;
        buttonPanel.MinimumSize = new System.Drawing.Size(0, 0);
        buttonPanel.MaximumSize = new System.Drawing.Size(0, 0);
        buttonPanel.TabIndex = 10;
        buttonPanel.TabStop = false;
        buttonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        buttonPanel.WrapContents = true;
        buttonPanel.AutoScroll = false;
        buttonPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Text = "";
        buttonPanel.Controls.Add(cancelButton);
        // cancelButton
        cancelButton.Location = new System.Drawing.Point(579, 13);
        cancelButton.Size = new System.Drawing.Size(100, 23);
        cancelButton.Dock = System.Windows.Forms.DockStyle.None;
        cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        cancelButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        cancelButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        cancelButton.AutoSize = false;
        cancelButton.MinimumSize = new System.Drawing.Size(0, 0);
        cancelButton.MaximumSize = new System.Drawing.Size(0, 0);
        cancelButton.TabIndex = 11;
        cancelButton.TabStop = true;
        cancelButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        cancelButton.AutoEllipsis = false;
        cancelButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        cancelButton.DialogResult = System.Windows.Forms.DialogResult.None;
        cancelButton.Name = "cancelTransfer";
        cancelButton.Text = "Cancel\u2026";
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        progressLayout.ResumeLayout(false);
        progressLayout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.TableLayoutPanel progressLayout = null!;
    private System.Windows.Forms.Label archivePathLabel = null!;
    private System.Windows.Forms.Label archiveSettingsLabel = null!;
    private System.Windows.Forms.Label currentFileCaption = null!;
    private System.Windows.Forms.Label currentFileLabel = null!;
    private System.Windows.Forms.Label filePiecesLabel = null!;
    private System.Windows.Forms.ProgressBar fileProgressBar = null!;
    private System.Windows.Forms.Label archiveProgressLabel = null!;
    private Mash.UI.MNXFile.GUI.PercentageProgressBar totalProgressBar = null!;
    private System.Windows.Forms.FlowLayoutPanel buttonPanel = null!;
    private System.Windows.Forms.Button cancelButton = null!;
    }
