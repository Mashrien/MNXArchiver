#nullable disable
// Keep resource casts in the syntax understood by the WinForms designer.
namespace Mash.UI.MNXFile.GUI;

partial class MainForm {
    private System.ComponentModel.IContainer components;

    #region Windows Form Designer generated code

    private void InitializeComponent() {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        mainMenu = new MenuStrip();
        archiveMenu = new ToolStripMenuItem();
        newArchiveMenuItem = new ToolStripMenuItem();
        openArchiveMenuItem = new ToolStripMenuItem();
        addFilesMenuItem = new ToolStripMenuItem();
        extractMenuItem = new ToolStripMenuItem();
        deleteMenuItem = new ToolStripMenuItem();
        closeArchiveMenuItem = new ToolStripMenuItem();
        archiveSeparator = new ToolStripSeparator();
        exitMenuItem = new ToolStripMenuItem();
        toolsMenu = new ToolStripMenuItem();
        verifyMenuItem = new ToolStripMenuItem();
        compactMenuItem = new ToolStripMenuItem();
        propertiesMenuItem = new ToolStripMenuItem();
        resumeMenuItem = new ToolStripMenuItem();
        optionsMenuItem = new ToolStripMenuItem();
        helpMenu = new ToolStripMenuItem();
        aboutMenuItem = new ToolStripMenuItem();
        mainToolbar = new ToolStrip();
        newArchiveButton = new ToolStripButton();
        openArchiveButton = new ToolStripButton();
        addFilesButton = new ToolStripButton();
        extractButton = new ToolStripButton();
        deleteButton = new ToolStripButton();
        verifyButton = new ToolStripButton();
        compactButton = new ToolStripButton();
        propertiesButton = new ToolStripButton();
        pathPanel = new TableLayoutPanel();
        rootButton = new MashWin.RoundButton();
        upButton = new MashWin.RoundButton();
        pathLabel = new Label();
        currentPath = new TextBox();
        searchLabel = new Label();
        searchText = new TextBox();
        browserSplit = new SplitContainer();
        folderTree = new TreeView();
        entryContextMenu = new ContextMenuStrip(components);
        extractContextMenuItem = new ToolStripMenuItem();
        previewContextMenuItem = new ToolStripMenuItem();
        deleteContextMenuItem = new ToolStripMenuItem();
        propertiesContextMenuItem = new ToolStripMenuItem();
        entryImages = new ImageList(components);
        fileList = new ListView();
        nameColumn = new ColumnHeader();
        typeColumn = new ColumnHeader();
        sizeColumn = new ColumnHeader();
        flagsColumn = new ColumnHeader();
        bytesColumn = new ColumnHeader();
        chunksColumn = new ColumnHeader();
        createdColumn = new ColumnHeader();
        statusBar = new StatusStrip();
        statusMessage = new ToolStripStatusLabel();
        selectionSummary = new ToolStripStatusLabel();
        operationProgress = new ToolStripProgressBar();
        hoverTimer = new System.Windows.Forms.Timer(components);
        statusBarControls.SuspendLayout();
        mainMenu.SuspendLayout();
        mainToolbar.SuspendLayout();
        pathPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)browserSplit).BeginInit();
        browserSplit.Panel1.SuspendLayout();
        browserSplit.Panel2.SuspendLayout();
        browserSplit.SuspendLayout();
        entryContextMenu.SuspendLayout();
        statusBar.SuspendLayout();
        SuspendLayout();
        // 
        // statusBarControls
        // 
        statusBarControls.Controls.Add(statusBar);
        statusBarControls.Location = new Point(14, 693);
        statusBarControls.Size = new Size(1061, 24);
        // 
        // mainMenu
        // 
        mainMenu.Items.AddRange(new ToolStripItem[] { archiveMenu, toolsMenu, helpMenu });
        mainMenu.Location = new Point(4, 33);
        mainMenu.Name = "mainMenu";
        mainMenu.Size = new Size(1092, 24);
        mainMenu.TabIndex = 7;
        // 
        // archiveMenu
        // 
        archiveMenu.DropDownItems.AddRange(new ToolStripItem[] { newArchiveMenuItem, openArchiveMenuItem, addFilesMenuItem, extractMenuItem, deleteMenuItem, closeArchiveMenuItem, archiveSeparator, exitMenuItem });
        archiveMenu.Name = "archiveMenu";
        archiveMenu.Size = new Size(59, 20);
        archiveMenu.Text = "&Archive";
        // 
        // newArchiveMenuItem
        // 
        newArchiveMenuItem.Image = (Image)resources.GetObject("newArchiveMenuItem.Image");
        newArchiveMenuItem.Name = "newArchiveMenuItem";
        newArchiveMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        newArchiveMenuItem.Size = new Size(179, 22);
        newArchiveMenuItem.Text = "&New…";
        // 
        // openArchiveMenuItem
        // 
        openArchiveMenuItem.Image = (Image)resources.GetObject("openArchiveMenuItem.Image");
        openArchiveMenuItem.Name = "openArchiveMenuItem";
        openArchiveMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        openArchiveMenuItem.Size = new Size(179, 22);
        openArchiveMenuItem.Text = "&Open…";
        // 
        // addFilesMenuItem
        // 
        addFilesMenuItem.Image = (Image)resources.GetObject("addFilesMenuItem.Image");
        addFilesMenuItem.Name = "addFilesMenuItem";
        addFilesMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.A;
        addFilesMenuItem.Size = new Size(179, 22);
        addFilesMenuItem.Text = "&Add…";
        // 
        // extractMenuItem
        // 
        extractMenuItem.Image = (Image)resources.GetObject("extractMenuItem.Image");
        extractMenuItem.Name = "extractMenuItem";
        extractMenuItem.ShortcutKeys = Keys.Control | Keys.E;
        extractMenuItem.Size = new Size(179, 22);
        extractMenuItem.Text = "&Extract…";
        // 
        // deleteMenuItem
        // 
        deleteMenuItem.Image = (Image)resources.GetObject("deleteMenuItem.Image");
        deleteMenuItem.Name = "deleteMenuItem";
        deleteMenuItem.ShortcutKeys = Keys.Delete;
        deleteMenuItem.Size = new Size(179, 22);
        deleteMenuItem.Text = "&Delete…";
        // 
        // closeArchiveMenuItem
        // 
        closeArchiveMenuItem.Name = "closeArchiveMenuItem";
        closeArchiveMenuItem.Size = new Size(179, 22);
        closeArchiveMenuItem.Text = "&Close archive";
        // 
        // archiveSeparator
        // 
        archiveSeparator.Name = "archiveSeparator";
        archiveSeparator.Size = new Size(176, 6);
        // 
        // exitMenuItem
        // 
        exitMenuItem.Name = "exitMenuItem";
        exitMenuItem.Size = new Size(179, 22);
        exitMenuItem.Text = "E&xit";
        // 
        // toolsMenu
        // 
        toolsMenu.DropDownItems.AddRange(new ToolStripItem[] { verifyMenuItem, compactMenuItem, propertiesMenuItem, resumeMenuItem, optionsMenuItem });
        toolsMenu.Name = "toolsMenu";
        toolsMenu.Size = new Size(46, 20);
        toolsMenu.Text = "&Tools";
        // 
        // verifyMenuItem
        // 
        verifyMenuItem.Image = (Image)resources.GetObject("verifyMenuItem.Image");
        verifyMenuItem.Name = "verifyMenuItem";
        verifyMenuItem.Size = new Size(190, 22);
        verifyMenuItem.Text = "&Verify";
        // 
        // compactMenuItem
        // 
        compactMenuItem.Image = (Image)resources.GetObject("compactMenuItem.Image");
        compactMenuItem.Name = "compactMenuItem";
        compactMenuItem.Size = new Size(190, 22);
        compactMenuItem.Text = "&Compact";
        // 
        // propertiesMenuItem
        // 
        propertiesMenuItem.Image = (Image)resources.GetObject("propertiesMenuItem.Image");
        propertiesMenuItem.Name = "propertiesMenuItem";
        propertiesMenuItem.Size = new Size(190, 22);
        propertiesMenuItem.Text = "&Properties";
        // 
        // resumeMenuItem
        // 
        resumeMenuItem.Name = "resumeMenuItem";
        resumeMenuItem.Size = new Size(190, 22);
        resumeMenuItem.Text = "&Resume from queue…";
        // 
        // optionsMenuItem
        // 
        optionsMenuItem.Name = "optionsMenuItem";
        optionsMenuItem.Size = new Size(190, 22);
        optionsMenuItem.Text = "&Options…";
        // 
        // helpMenu
        // 
        helpMenu.DropDownItems.AddRange(new ToolStripItem[] { aboutMenuItem });
        helpMenu.Name = "helpMenu";
        helpMenu.Size = new Size(44, 20);
        helpMenu.Text = "&Help";
        // 
        // aboutMenuItem
        // 
        aboutMenuItem.Name = "aboutMenuItem";
        aboutMenuItem.Size = new Size(107, 22);
        aboutMenuItem.Text = "&About";
        // 
        // mainToolbar
        // 
        mainToolbar.GripStyle = ToolStripGripStyle.Hidden;
        mainToolbar.ImageScalingSize = new Size(32, 32);
        mainToolbar.Items.AddRange(new ToolStripItem[] { newArchiveButton, openArchiveButton, addFilesButton, extractButton, deleteButton, verifyButton, compactButton, propertiesButton });
        mainToolbar.Location = new Point(4, 57);
        mainToolbar.Name = "mainToolbar";
        mainToolbar.Padding = new Padding(8, 4, 8, 4);
        mainToolbar.Size = new Size(1092, 66);
        mainToolbar.TabIndex = 6;
        // 
        // newArchiveButton
        // 
        newArchiveButton.Image = (Image)resources.GetObject("newArchiveButton.Image");
        newArchiveButton.Name = "newArchiveButton";
        newArchiveButton.Padding = new Padding(8, 2, 8, 2);
        newArchiveButton.Size = new Size(52, 55);
        newArchiveButton.Text = "New";
        newArchiveButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // openArchiveButton
        // 
        openArchiveButton.Image = (Image)resources.GetObject("openArchiveButton.Image");
        openArchiveButton.Name = "openArchiveButton";
        openArchiveButton.Padding = new Padding(8, 2, 8, 2);
        openArchiveButton.Size = new Size(56, 55);
        openArchiveButton.Text = "Open";
        openArchiveButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // addFilesButton
        // 
        addFilesButton.Image = (Image)resources.GetObject("addFilesButton.Image");
        addFilesButton.Name = "addFilesButton";
        addFilesButton.Padding = new Padding(8, 2, 8, 2);
        addFilesButton.Size = new Size(52, 55);
        addFilesButton.Text = "Add";
        addFilesButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // extractButton
        // 
        extractButton.Image = (Image)resources.GetObject("extractButton.Image");
        extractButton.Name = "extractButton";
        extractButton.Padding = new Padding(8, 2, 8, 2);
        extractButton.Size = new Size(63, 55);
        extractButton.Text = "Extract";
        extractButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // deleteButton
        // 
        deleteButton.Image = (Image)resources.GetObject("deleteButton.Image");
        deleteButton.Name = "deleteButton";
        deleteButton.Padding = new Padding(8, 2, 8, 2);
        deleteButton.Size = new Size(60, 55);
        deleteButton.Text = "Delete";
        deleteButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // verifyButton
        // 
        verifyButton.Image = (Image)resources.GetObject("verifyButton.Image");
        verifyButton.Name = "verifyButton";
        verifyButton.Padding = new Padding(8, 2, 8, 2);
        verifyButton.Size = new Size(56, 55);
        verifyButton.Text = "Verify";
        verifyButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // compactButton
        // 
        compactButton.Image = (Image)resources.GetObject("compactButton.Image");
        compactButton.Name = "compactButton";
        compactButton.Padding = new Padding(8, 2, 8, 2);
        compactButton.Size = new Size(76, 55);
        compactButton.Text = "Compact";
        compactButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // propertiesButton
        // 
        propertiesButton.Image = (Image)resources.GetObject("propertiesButton.Image");
        propertiesButton.Name = "propertiesButton";
        propertiesButton.Padding = new Padding(8, 2, 8, 2);
        propertiesButton.Size = new Size(80, 55);
        propertiesButton.Text = "Properties";
        propertiesButton.TextImageRelation = TextImageRelation.ImageAboveText;
        // 
        // pathPanel
        // 
        pathPanel.ColumnCount = 6;
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        pathPanel.Controls.Add(rootButton, 0, 0);
        pathPanel.Controls.Add(upButton, 1, 0);
        pathPanel.Controls.Add(pathLabel, 2, 0);
        pathPanel.Controls.Add(currentPath, 3, 0);
        pathPanel.Controls.Add(searchLabel, 4, 0);
        pathPanel.Controls.Add(searchText, 5, 0);
        pathPanel.Dock = DockStyle.Top;
        pathPanel.Location = new Point(4, 123);
        pathPanel.Name = "pathPanel";
        pathPanel.Padding = new Padding(0, 2, 0, 2);
        pathPanel.RowCount = 1;
        pathPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pathPanel.Size = new Size(1092, 32);
        pathPanel.TabIndex = 5;
        // 
        // rootButton
        // 
        rootButton.Dock = DockStyle.Fill;
        rootButton.Location = new Point(0, 2);
        rootButton.Margin = new Padding(0, 0, 2, 0);
        rootButton.Name = "rootButton";
        rootButton.Size = new Size(58, 28);
        rootButton.TabIndex = 0;
        rootButton.Text = "Home";
        rootButton.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // upButton
        // 
        upButton.Dock = DockStyle.Fill;
        upButton.Location = new Point(60, 2);
        upButton.Margin = new Padding(0, 0, 2, 0);
        upButton.Name = "upButton";
        upButton.Size = new Size(34, 28);
        upButton.TabIndex = 1;
        upButton.Text = "Up";
        upButton.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pathLabel
        // 
        pathLabel.Anchor = AnchorStyles.Left;
        pathLabel.AutoSize = false;
        pathLabel.Dock = DockStyle.Fill;
        pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        pathLabel.Location = new Point(99, 8);
        pathLabel.Name = "pathLabel";
        pathLabel.Size = new Size(34, 15);
        pathLabel.TabIndex = 2;
        pathLabel.Text = "Path:";
        // 
        // currentPath
        // 
        currentPath.BorderStyle = BorderStyle.FixedSingle;
        currentPath.Dock = DockStyle.Fill;
        currentPath.Location = new Point(138, 4);
        currentPath.Margin = new Padding(2);
        currentPath.Name = "currentPath";
        currentPath.Size = new Size(721, 23);
        currentPath.TabIndex = 3;
        // 
        // searchLabel
        // 
        searchLabel.Anchor = AnchorStyles.Left;
        searchLabel.AutoSize = false;
        searchLabel.Dock = DockStyle.Fill;
        searchLabel.TextAlign = ContentAlignment.MiddleLeft;
        searchLabel.Location = new Point(864, 8);
        searchLabel.Name = "searchLabel";
        searchLabel.Size = new Size(45, 15);
        searchLabel.TabIndex = 4;
        searchLabel.Text = "Search:";
        // 
        // searchText
        // 
        searchText.Dock = DockStyle.Fill;
        searchText.Location = new Point(914, 4);
        searchText.Margin = new Padding(2);
        searchText.Name = "searchText";
        searchText.Size = new Size(176, 23);
        searchText.TabIndex = 5;
        // 
        // browserSplit
        // 
        browserSplit.Dock = DockStyle.Fill;
        browserSplit.FixedPanel = FixedPanel.Panel1;
        browserSplit.Location = new Point(4, 155);
        browserSplit.Name = "browserSplit";
        // 
        // browserSplit.Panel1
        // 
        browserSplit.Panel1.Controls.Add(folderTree);
        browserSplit.Panel1MinSize = 160;
        // 
        // browserSplit.Panel2
        // 
        browserSplit.Panel2.Controls.Add(fileList);
        browserSplit.Panel2MinSize = 300;
        browserSplit.Size = new Size(1092, 537);
        browserSplit.SplitterDistance = 192;
        browserSplit.TabIndex = 4;
        // 
        // folderTree
        // 
        folderTree.AllowDrop = true;
        folderTree.ContextMenuStrip = entryContextMenu;
        folderTree.Dock = DockStyle.Fill;
        folderTree.HideSelection = false;
        folderTree.ImageIndex = 0;
        folderTree.ImageList = entryImages;
        folderTree.Location = new Point(0, 0);
        folderTree.Name = "folderTree";
        folderTree.SelectedImageIndex = 0;
        folderTree.Size = new Size(192, 537);
        folderTree.TabIndex = 0;
        // 
        // entryContextMenu
        // 
        entryContextMenu.Items.AddRange(new ToolStripItem[] { extractContextMenuItem, previewContextMenuItem, deleteContextMenuItem, propertiesContextMenuItem });
        entryContextMenu.Name = "entryContextMenu";
        entryContextMenu.Size = new Size(137, 92);
        // 
        // extractContextMenuItem
        // 
        extractContextMenuItem.Image = (Image)resources.GetObject("extractContextMenuItem.Image");
        extractContextMenuItem.Name = "extractContextMenuItem";
        extractContextMenuItem.Size = new Size(136, 22);
        extractContextMenuItem.Text = "Extract…";
        // 
        // previewContextMenuItem
        // 
        previewContextMenuItem.Image = (Image)resources.GetObject("previewContextMenuItem.Image");
        previewContextMenuItem.Name = "previewContextMenuItem";
        previewContextMenuItem.Size = new Size(136, 22);
        previewContextMenuItem.Text = "Preview";
        // 
        // deleteContextMenuItem
        // 
        deleteContextMenuItem.Image = (Image)resources.GetObject("deleteContextMenuItem.Image");
        deleteContextMenuItem.Name = "deleteContextMenuItem";
        deleteContextMenuItem.Size = new Size(136, 22);
        deleteContextMenuItem.Text = "Delete…";
        // 
        // propertiesContextMenuItem
        // 
        propertiesContextMenuItem.Image = (Image)resources.GetObject("propertiesContextMenuItem.Image");
        propertiesContextMenuItem.Name = "propertiesContextMenuItem";
        propertiesContextMenuItem.Size = new Size(136, 22);
        propertiesContextMenuItem.Text = "Properties…";
        // 
        // entryImages
        // 
        entryImages.ColorDepth = ColorDepth.Depth32Bit;
        entryImages.ImageSize = new Size(16, 16);
        entryImages.TransparentColor = Color.Transparent;
        // 
        // fileList
        // 
        fileList.AllowDrop = true;
        fileList.Columns.AddRange(new ColumnHeader[] { nameColumn, typeColumn, sizeColumn, flagsColumn, bytesColumn, chunksColumn, createdColumn });
        fileList.ContextMenuStrip = entryContextMenu;
        fileList.Dock = DockStyle.Fill;
        fileList.FullRowSelect = true;
        fileList.Location = new Point(0, 0);
        fileList.Name = "fileList";
        fileList.Size = new Size(896, 537);
        fileList.SmallImageList = entryImages;
        fileList.TabIndex = 0;
        fileList.UseCompatibleStateImageBehavior = false;
        fileList.View = View.Details;
        // 
        // nameColumn
        // 
        nameColumn.Text = "Name";
        nameColumn.Width = 310;
        // 
        // typeColumn
        // 
        typeColumn.Text = "Type";
        typeColumn.Width = 72;
        // 
        // sizeColumn
        // 
        sizeColumn.Text = "Size";
        sizeColumn.TextAlign = HorizontalAlignment.Right;
        sizeColumn.Width = 92;
        // 
        // flagsColumn
        // 
        flagsColumn.Text = "Flags";
        flagsColumn.TextAlign = HorizontalAlignment.Center;
        flagsColumn.Width = 65;
        // 
        // bytesColumn
        // 
        bytesColumn.Text = "Bytes";
        bytesColumn.TextAlign = HorizontalAlignment.Right;
        bytesColumn.Width = 110;
        // 
        // chunksColumn
        // 
        chunksColumn.Text = "Chunks";
        chunksColumn.TextAlign = HorizontalAlignment.Right;
        // 
        // createdColumn
        // 
        createdColumn.Text = "Created";
        createdColumn.Width = 125;
        // 
        // statusBar
        // 
        statusBar.AutoSize = false;
        statusBar.Dock = DockStyle.Fill;
        statusBar.Items.AddRange(new ToolStripItem[] { statusMessage, selectionSummary, operationProgress });
        statusBar.Location = new Point(0, 0);
        statusBar.Name = "statusBar";
        statusBar.Size = new Size(1061, 24);
        statusBar.SizingGrip = false;
        statusBar.TabIndex = 8;
        // 
        // statusMessage
        // 
        statusMessage.Name = "statusMessage";
        statusMessage.Size = new Size(987, 19);
        statusMessage.Spring = true;
        statusMessage.Text = "Open an archive or drop files to create one.";
        statusMessage.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // selectionSummary
        // 
        selectionSummary.Name = "selectionSummary";
        selectionSummary.Size = new Size(59, 19);
        selectionSummary.Text = "0 selected";
        // 
        // operationProgress
        // 
        operationProgress.Name = "operationProgress";
        operationProgress.Size = new Size(160, 18);
        operationProgress.Visible = false;
        // 
        // MainForm
        // 
        AllowDrop = true;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1100, 720);
        Controls.Add(browserSplit);
        Controls.Add(pathPanel);
        Controls.Add(mainToolbar);
        Controls.Add(mainMenu);
        Frame.BorderThickness = 3;
        Frame.CornerRadius = 14;
        Frame.ShowSizeGrip = true;
        KeyPreview = true;
        MainMenuStrip = mainMenu;
        MenuSeparator.Thickness = 2;
        MinimumSize = new Size(800, 480);
        Name = "MainForm";
        Padding = new Padding(1);
        StatusBar.Font = new Font("Segoe UI", 9F);
        Text = "MNX Archive Manager";
        Title.Alignment = MashWin.Alignment.Center;
        Title.Font = new Font("Segoe UI", 9F);
        ToolStrips.BorderThickness = 2;
        Controls.SetChildIndex(statusBarControls, 0);
        Controls.SetChildIndex(mainMenu, 0);
        Controls.SetChildIndex(mainToolbar, 0);
        Controls.SetChildIndex(pathPanel, 0);
        Controls.SetChildIndex(browserSplit, 0);
        statusBarControls.ResumeLayout(false);
        mainMenu.ResumeLayout(false);
        mainMenu.PerformLayout();
        mainToolbar.ResumeLayout(false);
        mainToolbar.PerformLayout();
        pathPanel.ResumeLayout(false);
        pathPanel.PerformLayout();
        browserSplit.Panel1.ResumeLayout(false);
        browserSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)browserSplit).EndInit();
        browserSplit.ResumeLayout(false);
        entryContextMenu.ResumeLayout(false);
        statusBar.ResumeLayout(false);
        statusBar.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
        }

    #endregion

    private System.Windows.Forms.MenuStrip mainMenu;
    private System.Windows.Forms.ToolStrip mainToolbar;
    private System.Windows.Forms.TableLayoutPanel pathPanel;
    private System.Windows.Forms.ToolStripMenuItem archiveMenu;
    private System.Windows.Forms.ToolStripMenuItem toolsMenu;
    private System.Windows.Forms.ToolStripMenuItem helpMenu;
    private System.Windows.Forms.ToolStripMenuItem newArchiveMenuItem;
    private System.Windows.Forms.ToolStripButton newArchiveButton;
    private System.Windows.Forms.ToolStripMenuItem openArchiveMenuItem;
    private System.Windows.Forms.ToolStripButton openArchiveButton;
    private System.Windows.Forms.ToolStripMenuItem addFilesMenuItem;
    private System.Windows.Forms.ToolStripButton addFilesButton;
    private System.Windows.Forms.ToolStripMenuItem extractMenuItem;
    private System.Windows.Forms.ToolStripButton extractButton;
    private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
    private System.Windows.Forms.ToolStripButton deleteButton;
    private System.Windows.Forms.ToolStripMenuItem verifyMenuItem;
    private System.Windows.Forms.ToolStripButton verifyButton;
    private System.Windows.Forms.ToolStripMenuItem compactMenuItem;
    private System.Windows.Forms.ToolStripButton compactButton;
    private System.Windows.Forms.ToolStripMenuItem propertiesMenuItem;
    private System.Windows.Forms.ToolStripButton propertiesButton;
    private System.Windows.Forms.ToolStripMenuItem resumeMenuItem;
    private System.Windows.Forms.ToolStripMenuItem optionsMenuItem;
    private System.Windows.Forms.ToolStripMenuItem closeArchiveMenuItem;
    private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
    private System.Windows.Forms.ToolStripMenuItem aboutMenuItem;
    private System.Windows.Forms.ToolStripSeparator archiveSeparator;
    private System.Windows.Forms.SplitContainer browserSplit;
    private System.Windows.Forms.TreeView folderTree;
    private System.Windows.Forms.ListView fileList;
    private System.Windows.Forms.TextBox currentPath;
    private MashWin.RoundButton rootButton;
    private MashWin.RoundButton upButton;
    private System.Windows.Forms.Label pathLabel;
    private System.Windows.Forms.Label searchLabel;
    private System.Windows.Forms.TextBox searchText;
    private System.Windows.Forms.StatusStrip statusBar;
    private System.Windows.Forms.ToolStripStatusLabel statusMessage;
    private System.Windows.Forms.ToolStripStatusLabel selectionSummary;
    private System.Windows.Forms.ToolStripProgressBar operationProgress;
    private System.Windows.Forms.ImageList entryImages;
    private System.Windows.Forms.Timer hoverTimer;
    private System.Windows.Forms.ContextMenuStrip entryContextMenu;
    private System.Windows.Forms.ToolStripMenuItem extractContextMenuItem;
    private System.Windows.Forms.ToolStripMenuItem previewContextMenuItem;
    private System.Windows.Forms.ToolStripMenuItem deleteContextMenuItem;
    private System.Windows.Forms.ToolStripMenuItem propertiesContextMenuItem;
    private System.Windows.Forms.ColumnHeader nameColumn;
    private System.Windows.Forms.ColumnHeader sizeColumn;
    private System.Windows.Forms.ColumnHeader flagsColumn;
    private System.Windows.Forms.ColumnHeader bytesColumn;
    private System.Windows.Forms.ColumnHeader createdColumn;
    private ColumnHeader typeColumn;
    private ColumnHeader chunksColumn;
    }
