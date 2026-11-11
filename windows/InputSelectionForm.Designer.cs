#nullable enable
namespace Mash.GUI.MNXArchiver;

partial class InputSelectionForm {
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
        inputPathsListBox = new System.Windows.Forms.ListBox();
        buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
        addFilesButton = new System.Windows.Forms.Button();
        addFolderButton = new System.Windows.Forms.Button();
        removeButton = new System.Windows.Forms.Button();
        continueButton = new System.Windows.Forms.Button();
        cancelButton = new System.Windows.Forms.Button();
        SuspendLayout();
        Frame.Resizable = true;
        Frame.ShowSizeGrip = true;
        buttonPanel.SuspendLayout();
        // InputSelectionForm
        Dock = System.Windows.Forms.DockStyle.None;
        Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        AutoSize = false;
        MinimumSize = new System.Drawing.Size(500, 280);
        MaximumSize = new System.Drawing.Size(0, 0);
        TabIndex = 0;
        TabStop = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        AutoScroll = false;
        MaximizeBox = true;
        MinimizeBox = true;
        ShowInTaskbar = true;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        ControlBox = true;
        KeyPreview = false;
        DialogResult = System.Windows.Forms.DialogResult.None;
        ClientSize = new System.Drawing.Size(664, 375);
        Name = "InputSelectionForm";
        Text = "Select input files";
        Controls.Add(inputPathsListBox);
        Controls.Add(buttonPanel);
        // inputPathsListBox
        inputPathsListBox.Location = new System.Drawing.Point(0, 0);
        inputPathsListBox.Size = new System.Drawing.Size(664, 293);
        inputPathsListBox.Dock = System.Windows.Forms.DockStyle.Fill;
        inputPathsListBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        inputPathsListBox.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        inputPathsListBox.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        inputPathsListBox.AutoSize = false;
        inputPathsListBox.MinimumSize = new System.Drawing.Size(0, 0);
        inputPathsListBox.MaximumSize = new System.Drawing.Size(0, 0);
        inputPathsListBox.TabIndex = 1;
        inputPathsListBox.TabStop = true;
        inputPathsListBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
        inputPathsListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
        inputPathsListBox.HorizontalScrollbar = true;
        inputPathsListBox.Name = "inputPathsListBox";
        inputPathsListBox.Text = "";
        // buttonPanel
        buttonPanel.Location = new System.Drawing.Point(0, 293);
        buttonPanel.Size = new System.Drawing.Size(664, 48);
        buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        buttonPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        buttonPanel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        buttonPanel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
        buttonPanel.AutoSize = false;
        buttonPanel.MinimumSize = new System.Drawing.Size(0, 0);
        buttonPanel.MaximumSize = new System.Drawing.Size(0, 0);
        buttonPanel.TabIndex = 2;
        buttonPanel.TabStop = false;
        buttonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        buttonPanel.WrapContents = true;
        buttonPanel.AutoScroll = false;
        buttonPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Text = "";
        buttonPanel.Controls.Add(addFilesButton);
        buttonPanel.Controls.Add(addFolderButton);
        buttonPanel.Controls.Add(removeButton);
        buttonPanel.Controls.Add(continueButton);
        buttonPanel.Controls.Add(cancelButton);
        // addFilesButton
        addFilesButton.Location = new System.Drawing.Point(11, 11);
        addFilesButton.Size = new System.Drawing.Size(75, 25);
        addFilesButton.Dock = System.Windows.Forms.DockStyle.None;
        addFilesButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        addFilesButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        addFilesButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        addFilesButton.AutoSize = true;
        addFilesButton.MinimumSize = new System.Drawing.Size(0, 0);
        addFilesButton.MaximumSize = new System.Drawing.Size(0, 0);
        addFilesButton.TabIndex = 3;
        addFilesButton.TabStop = true;
        addFilesButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        addFilesButton.AutoEllipsis = false;
        addFilesButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        addFilesButton.DialogResult = System.Windows.Forms.DialogResult.None;
        addFilesButton.Name = "addFilesButton";
        addFilesButton.Text = "Add files\u2026";
        // addFolderButton
        addFolderButton.Location = new System.Drawing.Point(92, 11);
        addFolderButton.Size = new System.Drawing.Size(82, 25);
        addFolderButton.Dock = System.Windows.Forms.DockStyle.None;
        addFolderButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        addFolderButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        addFolderButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        addFolderButton.AutoSize = true;
        addFolderButton.MinimumSize = new System.Drawing.Size(0, 0);
        addFolderButton.MaximumSize = new System.Drawing.Size(0, 0);
        addFolderButton.TabIndex = 4;
        addFolderButton.TabStop = true;
        addFolderButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        addFolderButton.AutoEllipsis = false;
        addFolderButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        addFolderButton.DialogResult = System.Windows.Forms.DialogResult.None;
        addFolderButton.Name = "addFolderButton";
        addFolderButton.Text = "Add folder\u2026";
        // removeButton
        removeButton.Location = new System.Drawing.Point(180, 11);
        removeButton.Size = new System.Drawing.Size(75, 25);
        removeButton.Dock = System.Windows.Forms.DockStyle.None;
        removeButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        removeButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        removeButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        removeButton.AutoSize = true;
        removeButton.MinimumSize = new System.Drawing.Size(0, 0);
        removeButton.MaximumSize = new System.Drawing.Size(0, 0);
        removeButton.TabIndex = 5;
        removeButton.TabStop = true;
        removeButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        removeButton.AutoEllipsis = false;
        removeButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        removeButton.DialogResult = System.Windows.Forms.DialogResult.None;
        removeButton.Name = "removeButton";
        removeButton.Text = "Remove";
        // continueButton
        continueButton.Location = new System.Drawing.Point(261, 11);
        continueButton.Size = new System.Drawing.Size(75, 25);
        continueButton.Dock = System.Windows.Forms.DockStyle.None;
        continueButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        continueButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        continueButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        continueButton.AutoSize = true;
        continueButton.MinimumSize = new System.Drawing.Size(0, 0);
        continueButton.MaximumSize = new System.Drawing.Size(0, 0);
        continueButton.TabIndex = 6;
        continueButton.TabStop = true;
        continueButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        continueButton.AutoEllipsis = false;
        continueButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        continueButton.DialogResult = System.Windows.Forms.DialogResult.None;
        continueButton.Name = "continueButton";
        continueButton.Text = "Continue";
        // cancelButton
        cancelButton.Location = new System.Drawing.Point(342, 11);
        cancelButton.Size = new System.Drawing.Size(75, 25);
        cancelButton.Dock = System.Windows.Forms.DockStyle.None;
        cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        cancelButton.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        cancelButton.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
        cancelButton.AutoSize = true;
        cancelButton.MinimumSize = new System.Drawing.Size(0, 0);
        cancelButton.MaximumSize = new System.Drawing.Size(0, 0);
        cancelButton.TabIndex = 7;
        cancelButton.TabStop = true;
        cancelButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
        cancelButton.AutoEllipsis = false;
        cancelButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        cancelButton.Name = "cancelButton";
        cancelButton.Text = "Cancel";
        AcceptButton = continueButton;
        CancelButton = cancelButton;
        AllowDrop = true;
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.ListBox inputPathsListBox = null!;
    private System.Windows.Forms.FlowLayoutPanel buttonPanel = null!;
    private System.Windows.Forms.Button addFilesButton = null!;
    private System.Windows.Forms.Button addFolderButton = null!;
    private System.Windows.Forms.Button removeButton = null!;
    private System.Windows.Forms.Button continueButton = null!;
    private System.Windows.Forms.Button cancelButton = null!;
    }
