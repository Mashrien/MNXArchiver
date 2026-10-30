#nullable enable
namespace Mash.UI.MNXFile.GUI;

partial class CreationForm {
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            components?.Dispose();
            }
        base.Dispose(disposing);
        }

    #region Windows Form Designer generated code

    private void InitializeComponent() {
        creationLayout = new TableLayoutPanel();
        label1 = new Label();
        outputPathTextBox = new TextBox();
        browseButton = new Button();
        label2 = new Label();
        compressionComboBox = new ComboBox();
        label3 = new Label();
        compressionLevelNumericUpDown = new NumericUpDown();
        encryptionCheckBox = new CheckBox();
        label4 = new Label();
        passwordTextBox = new TextBox();
        label5 = new Label();
        confirmationTextBox = new TextBox();
        buttonPanel = new FlowLayoutPanel();
        createButton = new Button();
        cancelButton = new Button();
        creationLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)compressionLevelNumericUpDown).BeginInit();
        buttonPanel.SuspendLayout();
        SuspendLayout();
        // 
        // statusBarControls
        // 
        statusBarControls.Location = new Point(5, 335);
        statusBarControls.Size = new Size(630, 24);
        // 
        // creationLayout
        // 
        creationLayout.ColumnCount = 3;
        creationLayout.ColumnStyles.Add(new ColumnStyle());
        creationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        creationLayout.ColumnStyles.Add(new ColumnStyle());
        creationLayout.Controls.Add(label1, 0, 0);
        creationLayout.Controls.Add(outputPathTextBox, 1, 0);
        creationLayout.Controls.Add(browseButton, 2, 0);
        creationLayout.Controls.Add(label2, 0, 1);
        creationLayout.Controls.Add(compressionComboBox, 1, 1);
        creationLayout.Controls.Add(label3, 0, 2);
        creationLayout.Controls.Add(compressionLevelNumericUpDown, 1, 2);
        creationLayout.Controls.Add(encryptionCheckBox, 1, 3);
        creationLayout.Controls.Add(label4, 0, 4);
        creationLayout.Controls.Add(passwordTextBox, 1, 4);
        creationLayout.Controls.Add(label5, 0, 5);
        creationLayout.Controls.Add(confirmationTextBox, 1, 5);
        creationLayout.Controls.Add(buttonPanel, 0, 6);
        creationLayout.Dock = DockStyle.Fill;
        creationLayout.Location = new Point(1, 29);
        creationLayout.Name = "creationLayout";
        creationLayout.Padding = new Padding(16);
        creationLayout.RowCount = 8;
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        creationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        creationLayout.Size = new Size(638, 330);
        creationLayout.TabIndex = 1;
        // 
        // label1
        // 
        label1.Anchor = AnchorStyles.Left;
        label1.AutoSize = true;
        label1.Location = new Point(19, 26);
        label1.Name = "label1";
        label1.Size = new Size(99, 15);
        label1.TabIndex = 2;
        label1.Text = "Archive filename:";
        // 
        // outputPathTextBox
        // 
        outputPathTextBox.Dock = DockStyle.Fill;
        outputPathTextBox.Location = new Point(124, 19);
        outputPathTextBox.Name = "outputPathTextBox";
        outputPathTextBox.Size = new Size(414, 23);
        outputPathTextBox.TabIndex = 3;
        // 
        // browseButton
        // 
        browseButton.AutoSize = true;
        browseButton.Location = new Point(544, 19);
        browseButton.Name = "browseButton";
        browseButton.Size = new Size(75, 25);
        browseButton.TabIndex = 4;
        browseButton.Text = "Browse…";
        // 
        // label2
        // 
        label2.Anchor = AnchorStyles.Left;
        label2.AutoSize = true;
        label2.Location = new Point(19, 62);
        label2.Name = "label2";
        label2.Size = new Size(80, 15);
        label2.TabIndex = 5;
        label2.Text = "Compression:";
        // 
        // compressionComboBox
        // 
        compressionComboBox.Dock = DockStyle.Fill;
        compressionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        compressionComboBox.Items.AddRange(new object[] { "None", "Snappy", "LZ4", "Brotli" });
        compressionComboBox.Location = new Point(124, 55);
        compressionComboBox.Name = "compressionComboBox";
        compressionComboBox.Size = new Size(414, 23);
        compressionComboBox.TabIndex = 6;
        // 
        // label3
        // 
        label3.Anchor = AnchorStyles.Left;
        label3.AutoSize = true;
        label3.Location = new Point(19, 98);
        label3.Name = "label3";
        label3.Size = new Size(65, 15);
        label3.TabIndex = 7;
        label3.Text = "Brotli level:";
        // 
        // compressionLevelNumericUpDown
        // 
        compressionLevelNumericUpDown.Dock = DockStyle.Fill;
        compressionLevelNumericUpDown.Location = new Point(124, 91);
        compressionLevelNumericUpDown.Maximum = new decimal(new int[] { 9, 0, 0, 0 });
        compressionLevelNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        compressionLevelNumericUpDown.Name = "compressionLevelNumericUpDown";
        compressionLevelNumericUpDown.Size = new Size(414, 23);
        compressionLevelNumericUpDown.TabIndex = 8;
        compressionLevelNumericUpDown.Value = new decimal(new int[] { 4, 0, 0, 0 });
        // 
        // encryptionCheckBox
        // 
        encryptionCheckBox.AutoSize = true;
        encryptionCheckBox.Location = new Point(124, 127);
        encryptionCheckBox.Name = "encryptionCheckBox";
        encryptionCheckBox.Size = new Size(121, 19);
        encryptionCheckBox.TabIndex = 9;
        encryptionCheckBox.Text = "Enable encryption";
        // 
        // label4
        // 
        label4.Anchor = AnchorStyles.Left;
        label4.AutoSize = true;
        label4.Location = new Point(19, 170);
        label4.Name = "label4";
        label4.Size = new Size(60, 15);
        label4.TabIndex = 10;
        label4.Text = "Password:";
        // 
        // passwordTextBox
        // 
        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.Location = new Point(124, 163);
        passwordTextBox.Name = "passwordTextBox";
        passwordTextBox.Size = new Size(414, 23);
        passwordTextBox.TabIndex = 11;
        passwordTextBox.UseSystemPasswordChar = true;
        // 
        // label5
        // 
        label5.Anchor = AnchorStyles.Left;
        label5.AutoSize = true;
        label5.Location = new Point(19, 206);
        label5.Name = "label5";
        label5.Size = new Size(99, 15);
        label5.TabIndex = 12;
        label5.Text = "Repeat password:";
        // 
        // confirmationTextBox
        // 
        confirmationTextBox.Dock = DockStyle.Fill;
        confirmationTextBox.Location = new Point(124, 199);
        confirmationTextBox.Name = "confirmationTextBox";
        confirmationTextBox.Size = new Size(414, 23);
        confirmationTextBox.TabIndex = 13;
        confirmationTextBox.UseSystemPasswordChar = true;
        // 
        // buttonPanel
        // 
        creationLayout.SetColumnSpan(buttonPanel, 3);
        buttonPanel.Controls.Add(createButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Location = new Point(19, 235);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Size = new Size(600, 30);
        buttonPanel.TabIndex = 14;
        // 
        // createButton
        // 
        createButton.AutoSize = true;
        createButton.Location = new Point(505, 3);
        createButton.Name = "createButton";
        createButton.Size = new Size(92, 25);
        createButton.TabIndex = 15;
        createButton.Text = "Create archive";
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(424, 3);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(75, 23);
        cancelButton.TabIndex = 16;
        cancelButton.Text = "Cancel";
        // 
        // CreationForm
        // 
        AcceptButton = createButton;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        CancelButton = cancelButton;
        ClientSize = new Size(640, 360);
        Controls.Add(creationLayout);
        Frame.Resizable = false;
        MenuSeparator.Enabled = false;
        MinimumSize = new Size(640, 360);
        Name = "CreationForm";
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterParent;
        StatusBar.Font = new Font("Segoe UI", 9F);
        StatusBar.Visible = false;
        Text = "Create MNX archive";
        Title.Font = new Font("Segoe UI", 9F);
        Controls.SetChildIndex(creationLayout, 0);
        Controls.SetChildIndex(statusBarControls, 0);
        creationLayout.ResumeLayout(false);
        creationLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)compressionLevelNumericUpDown).EndInit();
        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        ResumeLayout(false);
        }

    #endregion

    private System.Windows.Forms.TableLayoutPanel creationLayout = null!;
    private System.Windows.Forms.Label label1 = null!;
    private System.Windows.Forms.TextBox outputPathTextBox = null!;
    private System.Windows.Forms.Button browseButton = null!;
    private System.Windows.Forms.Label label2 = null!;
    private System.Windows.Forms.ComboBox compressionComboBox = null!;
    private System.Windows.Forms.Label label3 = null!;
    private System.Windows.Forms.NumericUpDown compressionLevelNumericUpDown = null!;
    private System.Windows.Forms.CheckBox encryptionCheckBox = null!;
    private System.Windows.Forms.Label label4 = null!;
    private System.Windows.Forms.TextBox passwordTextBox = null!;
    private System.Windows.Forms.Label label5 = null!;
    private System.Windows.Forms.TextBox confirmationTextBox = null!;
    private System.Windows.Forms.FlowLayoutPanel buttonPanel = null!;
    private System.Windows.Forms.Button createButton = null!;
    private System.Windows.Forms.Button cancelButton = null!;
    }
