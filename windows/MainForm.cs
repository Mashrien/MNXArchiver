using static MNX.Archive;
using Mash.GUI.MNXArchiver.Plugins;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Mash.GUI.MNXArchiver.Previews;
using Mash.GUI.MNXArchiver.Settings;
using MNX;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm : MashWin.MWindow {
    private Archive? _archive;
    private ArchiveBrowser? _browser;
    private Guid _current;
    private AppSettings _settings = null!;
    private AppSettings? _initialSettings;
    private bool _runtimeInitialized;
    private PreviewManager _previews = null!;
    private bool _busy;
    private bool _previewReading;
    private bool _syncingTree;
    private bool _closing;
    private int _sortColumn = -1;
    private bool _sortDescending;
    private readonly Dictionary<string, List<ToolStripItem>> _actions = new Dictionary<string, List<ToolStripItem>>();
    private readonly Dictionary<Guid, TreeNode> _folderNodes = new Dictionary<Guid, TreeNode>();
    private readonly List<Image> _ownedIcons = new List<Image>();
    private readonly List<DragFiles> _dragLeases = new List<DragFiles>();
    private Guid _hovered;
    private long _hoverGeneration;
    private Point _hoverPosition;
    private Guid[] _contextSelection = Array.Empty<Guid>();
    private bool _contextFromMouse;
    private Point _dragOrigin;
    private Guid[] _dragSelection = Array.Empty<Guid>();
    private bool _dragArmed;

    public MainForm() {
        InitializeComponent();
        // Keep the strip as ordinary designer-created content in MWin's panel.
        SetStatusBar(statusBar, statusMessage);
        // Designer resources load before runtime initialization. Keep ownership of
        // their images so closing the form releases them along with dynamic icons.
        TrackDesignerIcons(mainMenu);
        TrackDesignerIcons(mainToolbar);
        TrackDesignerIcons(entryContextMenu);
        }

    internal MainForm(AppSettings settings) : this() {
        _initialSettings = settings;
        }

    protected override void OnLoad(EventArgs args) {
        if (!_runtimeInitialized && !DesignMode && System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime) {
            _runtimeInitialized = true;
            InitializeRuntime(_initialSettings);
            }
        base.OnLoad(args);
        }

    protected override void OnShown(EventArgs args) {
        if (_runtimeInitialized) {
            InitializeFolderPaneWidth();
            }
        base.OnShown(args);
        }

    private void InitializeRuntime(AppSettings? settings) {
        rootButton.Click += (_, _) => NavigateRoot();
        upButton.Click += (_, _) => NavigateUp();
        _settings = settings ?? LoadSettings();
        ApplyWindowAppearance();
        _previews = new PreviewManager(this, _settings);
        using Stream? iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mash.GUI.MNXArchiver.archive.ico");
        if (iconStream is not null) {
            using Icon applicationIcon = new Icon(iconStream);
            SetIcon(applicationIcon);
            }
        entryImages.ImageSize = new Size(16, 16);
        entryImages.ColorDepth = ColorDepth.Depth32Bit;
        entryImages.Images.Add("folder", ReadIcon("folder", 16));
        entryImages.Images.Add("file", ReadIcon("file", 16));
        ConfigureActions();
        ConfigureDragAndDrop();
        ConfigureFileListLayout();
        folderTree.AfterSelect += (_, args) => {
            if (!_syncingTree && args.Node?.Tag is Guid id) {
                Navigate(id);
                }
            };
        folderTree.BeforeExpand += (_, args) => PopulateFolders(args.Node!);
        fileList.ItemActivate += async (_, _) => {
            Guid[] ids = SelectedIds();
            if (ids.Length == 1 && _browser is not null) {
                if (_browser.Get(ids[0]).IsDirectory) {
                    Navigate(ids[0]);
                    }
                else if (_settings.Trigger == "Double-Click") {
                    await PreviewEntry(ids[0], false);
                    }
                }
            };
        fileList.SelectedIndexChanged += (_, _) => UpdateSelection();
        fileList.ColumnClick += (_, args) => {
            if (_browser is null) {
                return;
                }
            _sortDescending = _sortColumn == args.Column && !_sortDescending;
            _sortColumn = args.Column;
            fileList.Sorting = SortOrder.None;
            fileList.ListViewItemSorter = new EntryComparer(this, _sortColumn, _sortDescending);
            fileList.Sort();
            };
        currentPath.KeyDown += (_, args) => {
            if (args.KeyCode == Keys.Enter && _browser is not null) {
                args.SuppressKeyPress = true;
                try {
                    Navigate(_browser.Resolve(currentPath.Text));
                    }
                catch (Exception error) {
                    statusMessage.Text = error.Message;
                    }
                }
            };
        searchText.TextChanged += (_, _) => PopulateList();
        fileList.MouseMove += OnListHover;
        fileList.MouseLeave += (_, _) => CancelHover();
        fileList.MouseDown += (_, _) => CancelHover();
        Deactivate += (_, _) => CancelHover();
        hoverTimer.Tick += async (_, _) => {
            hoverTimer.Stop();
            if (_hovered != Guid.Empty && !_busy) {
                await PreviewEntry(_hovered, true);
                }
            };
        FormClosing += (_, args) => {
            if (_busy) {
                args.Cancel = true;
                statusMessage.Text = "Wait for the current operation or use Cancel in its progress window.";
                }
            else {
                _closing = true;
                }
            };
        UpdateActions();
        }

    partial void ApplyPlatformAppearance();

    private void ApplyWindowAppearance() {
        ApplyPlatformAppearance();
        double opacity = _settings.OpacityPercent / 100.0;
        MnxDialog.ConfiguredOpacity = opacity;
        Opacity = opacity;
        foreach (Form window in Application.OpenForms) {
            if (window is MnxDialog) {
                window.Opacity = opacity;
                }
            }
        }

    private static AppSettings LoadSettings() {
        try {
            return AppSettings.Load();
            }
        catch (Exception error) {
            MessageBox.Show(error.Message, "Could not load options", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new AppSettings();
            }
        }

    private void TrackDesignerIcons(ToolStrip strip) {
        foreach (ToolStripItem item in strip.Items) {
            Image? image = item.Image;
            if (image is not null && !_ownedIcons.Contains(image)) {
                _ownedIcons.Add(image);
                }
            if (item is ToolStripDropDownItem submenu && submenu.HasDropDownItems) {
                TrackDesignerIcons(submenu.DropDown);
                }
            }
        }

    private Image ReadIcon(string name, int size = 32) {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Mash.GUI.MNXArchiver.Icons.{size}.{name}.png") ?? throw new InvalidDataException("Missing icon: " + name);
        using Image source = Image.FromStream(stream);
        Image image = new Bitmap(source);
        _ownedIcons.Add(image);
        return image;
        }

    private void ConfigureActions() {
        BindAction(newArchiveMenuItem, newArchiveButton, "New", async () => await ChooseInputs(false));
        BindAction(openArchiveMenuItem, openArchiveButton, "Open", ChooseArchive);
        BindAction(addFilesMenuItem, addFilesButton, "Add", async () => await ChooseInputs(true));
        BindAction(extractMenuItem, extractButton, "Extract", async () => await ChooseExtraction(ActionSelection()));
        BindAction(deleteMenuItem, deleteButton, "Delete", async () => await DeleteEntries(SelectedIds()));
        BindAction(verifyMenuItem, verifyButton, "Verify", async () => await VerifyArchive());
        BindAction(compactMenuItem, compactButton, "Compact", async () => await CompactArchive());
        BindAction(propertiesMenuItem, propertiesButton, "Properties", () => {
            ShowProperties(SelectedIds());
            return Task.CompletedTask;
            });
        resumeMenuItem.Click += async (_, _) => await Guard(ChooseResume);
        TrackAction("Resume", resumeMenuItem);
        optionsMenuItem.Click += (_, _) => ShowOptions();
        TrackAction("Options", optionsMenuItem);
        closeArchiveMenuItem.Click += (_, _) => CloseArchive();
        TrackAction("Close", closeArchiveMenuItem);
        exitMenuItem.Click += (_, _) => Close();
        aboutMenuItem.Click += (_, _) => MessageBox.Show(this,
            "MNX Archive Manager " + Assembly.GetExecutingAssembly().GetName().Version
            + "\nWindows Forms / C#\n\n" + BuiltInArchiveFormat() + "\n\n" + PluginCatalog.Installed.AboutSummary(), "About MNX");
        openPluginsDirectoryMenuItem.Click += async (_, _) => await Guard(() => {
            PluginCatalog.Installed.OpenDirectory();
            return Task.CompletedTask;
            });
        ConfigurePluginActions();
        ContextMenuStrip menu = entryContextMenu;
        extractContextMenuItem.Click += async (_, _) => await Guard(() => ChooseExtraction(_contextSelection));
        previewContextMenuItem.Click += async (_, _) => {
            if (_contextSelection.Length == 1) {
                await PreviewEntry(_contextSelection[0], false);
                }
            };
        deleteContextMenuItem.Click += async (_, _) => await Guard(() => DeleteEntries(_contextSelection));
        propertiesContextMenuItem.Click += (_, _) => ShowProperties(_contextSelection);
        menu.Opening += (_, args) => {
            bool fromMouse = _contextFromMouse;
            _contextFromMouse = false;
            if (!fromMouse) {
                if (menu.SourceControl == fileList) {
                    _contextSelection = SelectedIds();
                    }
                else {
                    _contextSelection = folderTree.SelectedNode?.Tag is Guid selected && selected != _browser?.Root ? new Guid[] { selected } : Array.Empty<Guid>();
                    }
                }
            args.Cancel = _busy || (_archive is null && _pluginArchive is null) || _contextSelection.Length == 0;
            if (!args.Cancel) {
                previewContextMenuItem.Enabled = _contextSelection.Length == 1 && CanPreview(_contextSelection[0]);
                deleteContextMenuItem.Enabled = _archive is not null || _pluginArchive?.Supports(PluginActions.Delete) == true;
                extractContextMenuItem.Enabled = _pluginArchive is null || _pluginArchive.Supports(PluginActions.Extract);
                _replacePluginEntry.Visible = _pluginArchive is not null;
                _replacePluginEntry.Enabled = CanReplacePluginEntry(_contextSelection);
                }
            };
        fileList.ContextMenuStrip = menu;
        folderTree.ContextMenuStrip = menu;
        fileList.MouseDown += (_, args) => {
            if (args.Button == MouseButtons.Right) {
                _contextFromMouse = true;
                ListViewItem? item = fileList.GetItemAt(args.X, args.Y);
                if (item is null) {
                    _contextSelection = Array.Empty<Guid>();
                    return;
                    }
                if (!item.Selected) {
                    fileList.SelectedItems.Clear();
                    item.Selected = true;
                    }
                _contextSelection = SelectedIds();
                }
            };
        folderTree.MouseDown += (_, args) => {
            if (args.Button == MouseButtons.Right) {
                _contextFromMouse = true;
                TreeViewHitTestInfo hit = folderTree.HitTest(args.Location);
                TreeNode? node = (hit.Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.Image)) != 0 ? hit.Node : null;
                if (node is not null) {
                    folderTree.SelectedNode = node;
                    }
                _contextSelection = node?.Tag is Guid id && id != _browser?.Root ? new Guid[] { id } : Array.Empty<Guid>();
                }
            };
        }

    private void BindAction(ToolStripMenuItem menu, ToolStripButton button, string key, Func<Task> action) {
        menu.Click += async (_, _) => await Guard(action);
        button.Click += async (_, _) => await Guard(action);
        TrackAction(key, menu);
        TrackAction(key, button);
        }

    private void TrackAction(string key, ToolStripItem item) {
        if (!_actions.TryGetValue(key, out List<ToolStripItem>? controls)) {
            controls = new List<ToolStripItem>();
            _actions.Add(key, controls);
            }
        controls.Add(item);
        }

    private async Task Guard(Func<Task> action) {
        if (_busy) {
            return;
            }
        try {
            await action();
            }
        catch (OperationCanceledException) {
            statusMessage.Text = "Operation cancelled.";
            }
        catch (Exception error) {
            MessageBox.Show(this, error.Message, "MNX Archive Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    private void UpdateActions() {
        foreach (KeyValuePair<string, List<ToolStripItem>> action in _actions) {
            bool enabled = !_busy && (action.Key is "New" or "Open" or "Options" or "Resume" || _archive is not null);
            if (_pluginArchive is not null) {
                enabled = !_busy && (action.Key switch {
                    "New" or "Open" or "Options" or "Close" or "Properties" => true,
                    "Add" => _pluginArchive.Supports(PluginActions.Add),
                    "Extract" => _pluginArchive.Supports(PluginActions.Extract),
                    "Delete" => _pluginArchive.Supports(PluginActions.Delete),
                    _ => false,
                    });
                }
            if (action.Key == "Delete") {
                enabled = enabled && SelectedIds().Length > 0;
                }
            foreach (ToolStripItem control in action.Value) {
                control.Enabled = enabled;
                }
            }
        folderTree.Enabled = (!_busy || _previewReading) && _browser is not null;
        fileList.Enabled = (!_busy || _previewReading) && _browser is not null;
        currentPath.Enabled = !_busy && _browser is not null;
        searchText.Enabled = !_busy && _browser is not null;
        }

    internal void LoadPreview() {
        CloseArchive();
        LoadBrowser(ArchiveBrowser.Preview());
        }

    internal async Task<bool> OpenArchive(string path, string? password = null, bool showErrors = true) {
        if (_busy) {
            return false;
            }
        path = FileTransfer.LocalPath(path);
        try {
            PluginMatch? match = PluginCatalog.Installed.Match(path);
            if (match is not null) {
                OpenedModuleArchive result = await RunPluginOperation("Opening archive", path, match.Module.Format.Name,
                    token => OpenedModuleArchive.Open(match, path, token));
                CloseArchive();
                SetPluginArchive(result);
                return true;
                }
            }
        catch (OperationCanceledException) when (showErrors) {
            statusMessage.Text = "Opening cancelled.";
            return false;
            }
        catch (Exception error) when (showErrors) {
            MessageBox.Show(this, error.Message, "Could not open plugin archive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
            }
        Archive next = new Archive();
        next.Options.ProgressReportFrequency = 50;
        try {
            await RunOperation("Opening archive", () => next.Open(FileTransfer.LocalPath(path), password), next, showProgress: false);
            ArchiveBrowser browser = ArchiveBrowser.FromArchive(next);
            CloseArchive();
            _archive = next;
            LoadBrowser(browser);
            return true;
            }
        catch (CryptographicException) when (showErrors) {
            next.Dispose();
            string? entered = TextPrompt.Ask(this, "Archive password", "Enter the archive password:", true);
            return entered is not null && await OpenArchive(path, entered, showErrors);
            }
        catch (Exception error) {
            ReturnCode code = next.ErrorStatus;
            next.Dispose();
            if (!showErrors) {
                throw;
                }
            MessageBox.Show(this, ArchiveOpenError(path, error, code), "Invalid or unreadable MNX archive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
            }
        }

    internal static string ArchiveOpenError(string path, Exception error, ReturnCode code) {
        return path + "\n\n" + error.Message + "\n\nError code: " + code + " (" + ((ulong)code).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

    private void CloseArchive() {
        if (_busy) {
            return;
            }
        ResetPreviews();
        _archive?.Dispose();
        _archive = null;
        _pluginArchive = null;
        _browser = null;
        _folderNodes.Clear();
        folderTree.Nodes.Clear();
        fileList.Items.Clear();
        currentPath.Clear();
        Text = "MNX Archive Manager";
        UpdateActions();
        }

    private void LoadBrowser(ArchiveBrowser browser, Guid? location = null) {
        CancelHover();
        _browser = browser;
        _folderNodes.Clear();
        folderTree.BeginUpdate();
        folderTree.Nodes.Clear();
        TreeNode root = new TreeNode(browser.Title) { Tag = browser.Root, ImageKey = "folder", SelectedImageKey = "folder" };
        folderTree.Nodes.Add(root);
        _folderNodes.Add(browser.Root, root);
        root.Nodes.Add(new TreeNode());
        folderTree.EndUpdate();
        Text = browser.Title + " — MNX Archive Manager";
        Guid destination = location ?? browser.Root;
        try {
            Navigate(destination);
            }
        catch (KeyNotFoundException) {
            Navigate(browser.Root);
            }
        }

    private void PopulateFolders(TreeNode node) {
        if (_browser is null || node.Tag is not Guid id || (node.Nodes.Count > 0 && node.Nodes[0].Tag is Guid)) {
            return;
            }
        node.Nodes.Clear();
        foreach (ArchiveBrowser.Entry entry in _browser.Children(id)) {
            if (!entry.IsDirectory) {
                continue;
                }
            TreeNode child = new TreeNode(entry.Name) { Tag = entry.Id, ImageKey = "folder", SelectedImageKey = "folder" };
            node.Nodes.Add(child);
            _folderNodes[entry.Id] = child;
            if (_browser.HasFolders(entry.Id)) {
                child.Nodes.Add(new TreeNode());
                }
            }
        }

    private void Navigate(Guid folder) {
        if (_browser is null || _busy) {
            return;
            }
        CancelHover();
        _current = folder;
        currentPath.Text = _browser.PathOf(folder);
        searchText.Text = "";
        PopulateList();
        _syncingTree = true;
        try {
            foreach (Guid ancestor in _browser.Ancestors(folder)) {
                TreeNode node = _folderNodes[ancestor];
                PopulateFolders(node);
                node.Expand();
                }
            folderTree.SelectedNode = _folderNodes[folder];
            _folderNodes[folder].EnsureVisible();
            }
        finally {
            _syncingTree = false;
            }
        UpdateActions();
        }

    private void PopulateList() {
        if (_browser is null || _busy) {
            return;
            }
        string search = searchText.Text.Trim();
        IEnumerable<ArchiveBrowser.Entry> entries = search.Length == 0
            ? _browser.Children(_current)
            : Descendants(_current).Where(entry => entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        fileList.BeginUpdate();
        fileList.Items.Clear();
        foreach (ArchiveBrowser.Entry entry in entries) {
            ListViewItem item = new ListViewItem(entry.Name, entry.IsDirectory ? "folder" : "file") { Tag = entry.Id };
            item.SubItems.Add(ArchiveBrowser.FileType(entry));
            item.SubItems.Add(entry.IsDirectory ? "" : ArchiveBrowser.HumanSize(entry.Bytes));
            item.SubItems.Add(entry.IsDirectory ? "" : (_browser.Compressed ? "C" : "") + (_browser.Encrypted ? "E" : ""));
            item.SubItems.Add(entry.IsDirectory ? "" : entry.Bytes.ToString("N0", CultureInfo.InvariantCulture));
            item.SubItems.Add(entry.IsDirectory || _pluginArchive is not null ? "" : ArchiveBrowser.HumanChunks(entry.Chunks));
            item.SubItems.Add(entry.CreatedUtc == DateTime.MinValue ? "" : entry.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (search.Length > 0) {
                item.ToolTipText = _browser.PathOf(entry.Id);
                }
            fileList.Items.Add(item);
            }
        fileList.EndUpdate();
        statusMessage.Text = $"{fileList.Items.Count:N0} item(s)" + (search.Length > 0 ? " matching " + search : " in " + currentPath.Text) + (_archive is null ? " · Layout preview" : "");
        UpdateSelection();
        }

    private IEnumerable<ArchiveBrowser.Entry> Descendants(Guid parent) {
        Stack<Guid> pending = new Stack<Guid>();
        pending.Push(parent);
        while (pending.Count > 0) {
            foreach (ArchiveBrowser.Entry entry in _browser!.Children(pending.Pop())) {
                yield return entry;
                if (entry.IsDirectory) {
                    pending.Push(entry.Id);
                    }
                }
            }
        }

    private Guid[] SelectedIds() {
        return fileList.SelectedItems.Cast<ListViewItem>().Where(item => item.Tag is Guid).Select(item => (Guid)item.Tag!).ToArray();
        }

    private Guid[] ActionSelection() {
        Guid[] selected = SelectedIds();
        return selected.Length > 0 || _browser is null ? selected : _browser.Children(_current).Select(entry => entry.Id).ToArray();
        }

    private void UpdateSelection() {
        Guid[] selected = SelectedIds();
        ulong bytes = 0;
        foreach (Guid id in selected) {
            bytes += _browser!.Get(id).Bytes;
            }
        selectionSummary.Text = $"{selected.Length:N0} selected" + (selected.Length > 0 ? " · " + ArchiveBrowser.HumanSize(bytes) : "");
        UpdateActions();
        }

    private void NavigateRoot() {
        if (_browser is not null) {
            Navigate(_browser.Root);
            }
        }

    private void NavigateUp() {
        if (_browser is not null) {
            Navigate(_browser.Parent(_current));
            }
        }

    private void RefreshArchive(Guid location) {
        if (_archive is not null) {
            LoadBrowser(ArchiveBrowser.FromArchive(_archive), location);
            }
        }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData) {
        if (!_busy && fileList.Focused && keyData == (Keys.Control | Keys.A)) {
            foreach (ListViewItem item in fileList.Items) {
                item.Selected = true;
                }
            return true;
            }
        if (!_busy && keyData == (Keys.Control | Keys.L)) {
            currentPath.Focus();
            currentPath.SelectAll();
            return true;
            }
        if (!_busy && keyData == (Keys.Alt | Keys.Up)) {
            NavigateUp();
            return true;
            }
        return base.ProcessCmdKey(ref message, keyData);
        }

    protected override void Dispose(bool disposing) {
        if (disposing && !_busy) {
            _previews?.Dispose();
            _archive?.Dispose();
            foreach (DragFiles lease in _dragLeases) {
                lease.Dispose();
                }
            components?.Dispose();
            foreach (Image icon in _ownedIcons) {
                icon.Dispose();
                }
            }
        base.Dispose(disposing);
        }

    private sealed class EntryComparer : System.Collections.IComparer {
        private readonly MainForm owner;
        private readonly int column;
        private readonly bool descending;

        internal EntryComparer(MainForm owner, int column, bool descending) {
            this.owner = owner;
            this.column = column;
            this.descending = descending;
            }

        public int Compare(object? first, object? second) {
            if (owner._browser is null) {
                return 0;
                }
            ListViewItem left = (ListViewItem)first!;
            ListViewItem right = (ListViewItem)second!;
            ArchiveBrowser.Entry leftEntry = owner._browser.Get((Guid)left.Tag!);
            ArchiveBrowser.Entry rightEntry = owner._browser.Get((Guid)right.Tag!);
            if (leftEntry.IsDirectory != rightEntry.IsDirectory) {
                return leftEntry.IsDirectory ? -1 : 1;
                }
            int result;
            switch (column) {
                case 1:
                    result = StringComparer.OrdinalIgnoreCase.Compare(ArchiveBrowser.FileType(leftEntry), ArchiveBrowser.FileType(rightEntry));
                    break;
                case 2:
                case 4:
                    result = leftEntry.Bytes.CompareTo(rightEntry.Bytes);
                    break;
                case 5:
                    result = leftEntry.Chunks.CompareTo(rightEntry.Chunks);
                    break;
                case 6:
                    result = leftEntry.CreatedUtc.CompareTo(rightEntry.CreatedUtc);
                    break;
                default:
                    result = StringComparer.OrdinalIgnoreCase.Compare(left.SubItems[column].Text, right.SubItems[column].Text);
                    break;
                }
            if (result == 0) {
                result = StringComparer.OrdinalIgnoreCase.Compare(leftEntry.Name, rightEntry.Name);
                }
            return descending ? -result : result;
            }
        }
    }
