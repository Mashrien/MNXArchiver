using System.Globalization;
using System.Security.Cryptography;
using Gtk;
using Mash.UI.MNXFile.GUI.Icons;
using Mash.UI.MNXFile.GUI.Plugins;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public enum ArchiveAction {
    New    ,
    Open   ,
    Add    ,
    Extract,
    Test   ,
    Delete ,
    Compact,
    Info   ,
    }

public sealed class ArchiveActionEventArgs(ArchiveAction action, Guid folderId, Guid[] selectedIds) : EventArgs {
    public ArchiveAction Action { get; } = action;
    public Guid FolderId { get; } = folderId;
    public IReadOnlyList<Guid> SelectedIds { get; } = selectedIds;
    }

/// <summary>GTK shell: layout in .ui, behavior and event hookups in C#.</summary>
public sealed partial class MainWindow : IDisposable {
    private readonly Builder _builder = new();
    private readonly Window _window;
    private readonly Paned _split;
    private readonly TreeView _tree, _list;
    private readonly TreeStore _folders = new(typeof(Gdk.Pixbuf), typeof(string), typeof(string));
    private readonly ListStore _files = new(
        typeof(Gdk.Pixbuf),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(bool),
        typeof(ulong),
        typeof(long),
        typeof(string), // File type (normalized extension).
        typeof(string), // Human-readable chunk count.
        typeof(uint)    // Actual chunk count, used for sorting.
        );
    private readonly Entry _path;
    private readonly Label _status, _summary;
    private readonly Dictionary<Guid, TreeIter> _folderRows = new();
    private readonly HashSet<Guid> _populated = [];
    private ArchiveFile? _archive;
    private ArchiveBrowser? _browser;
    private Guid _current;
    private bool _syncingTree, _initialSplitSet, _disposed;
    /// <summary>Remaining toolbar operations can be connected by the caller.</summary>
    public event EventHandler<ArchiveActionEventArgs>? ActionRequested;
    public MainWindow() : this(null) {
        }
    internal MainWindow(Settings.AppSettings? initialSettings) {
        _builder.AddFromString(ReadAsset("MainWindow.ui"));
        _window = Widget<Window>("main_window");
        using (Stream? stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Mash.UI.MNXFile.GUI.archive.png") ?? throw new InvalidDataException("Missing embedded archive icon.")) {
            using Gdk.Pixbuf icon = new Gdk.Pixbuf(stream);
            Window.DefaultIcon = icon;
            _window.Icon = icon;
            }

        _split = Widget<Paned>("browser_split");
        _tree = Widget<TreeView>("folder_tree");
        _list = Widget<TreeView>("file_list");
        _path = Widget<Entry>("current_path");
        _status = Widget<Label>("status_message");
        _summary = Widget<Label>("selection_summary");
        CssProvider css = new CssProvider();
        css.LoadFromData(ReadAsset("style.css"));
        StyleContext.AddProviderForScreen(Gdk.Screen.Default, css, 600);
        css.Dispose();
        ConfigureOwnedIcons();
        ConfigureViews();
        ConfigureOptions(initialSettings);
        ConfigurePreviews();
        ConfigureDragAndDrop();
        ConfigureExtractionMenu();
        ConfigurePluginMenu();
        // Set the initial 30/70 split after GTK measures the actual client width.
        // Both panes grow on resize; moving the divider remains under the user's control.
        _split.SizeAllocated += (_, args) => {
            if (!_initialSplitSet && args.Allocation.Width > 500) {
                _initialSplitSet = true;
                _split.Position = (int)(args.Allocation.Width * 0.30);
                }
            };
        _window.DeleteEvent += (_, args) => {
            args.RetVal = true;
            RequestQuit();
            };
        _window.KeyPressEvent += OnKeyPress;
        _path.Activated += (_, _) => NavigatePath();
        Widget<Button>("navigate_up").Clicked += (_, _) => NavigateUp();
        Widget<Button>("navigate_root").Clicked += (_, _) => {
            if (_browser is not null) {
                Navigate(_browser.Root);
                }
            };
        Widget<MenuItem>("menu_close").Activated += (_, _) => CloseArchive();
        Widget<MenuItem>("menu_resume").Activated += (_, _) => ChooseResumeQueue();
        Widget<MenuItem>("menu_quit").Activated += (_, _) => {
            RequestQuit();
            };
        Widget<MenuItem>("menu_about").Activated += (_, _) => {
            string version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "Unknown";
            Message("About MNX", "MNX Archive Manager — " + version + "\nGTK3 / C#\n\n" + PluginCatalog.Installed.AboutSummary());
            };
        Widget<MenuItem>("menu_open_plugins_dir").Activated += (_, _) => {
            try {
                PluginCatalog.Installed.OpenDirectory();
                }
            catch (Exception error) {
                Message("Could not open plugins directory", error.Message, MessageType.Error);
                }
            };
        foreach (ArchiveAction action in Enum.GetValues<ArchiveAction>()) {
            ArchiveAction captured = action;
            string name = action.ToString().ToLowerInvariant();
            ToolButton? tool = Widget<ToolButton>("action_" + name);
            tool.Clicked += (_, _) => Dispatch(captured);
            Widget<MenuItem>("menu_" + name).Activated += (_, _) => Dispatch(captured);
            }

        UpdateActions();
        }

    private T Widget<T>(string id)
        where T : GLib.Object => _builder.GetObject(id) as T ?? throw new InvalidDataException($"Missing GTK object: {id}");
    private static string ReadAsset(string name) {
        string sidecar = Path.Combine(AppContext.BaseDirectory, name);
        if (DiskFile.Exists(sidecar)) {
            return DiskFile.ReadAllText(sidecar);
            }

        using Stream stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Mash.UI.MNXFile.GUI." + name) ?? throw new InvalidDataException($"Missing embedded UI asset: {name}");
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
        }

    private void ConfigureViews() {
        _tree.Model = _folders;
        _tree.RowExpanded += (_, args) => PopulateFolders(args.Iter);
        _tree.Selection.Changed += (_, _) => {
            if (_syncingTree || !_tree.Selection.GetSelected( out TreeIter row)) {
                return;
                }

            if (Guid.TryParse((string)_folders.GetValue(row, 2), out Guid id)) {
                Navigate(id);
                }
            };
        _list.Model = _files;
        ConfigureColumnSorting();
        _list.Selection.Mode = SelectionMode.Multiple;
        _list.RowActivated += (_, args) => {
            if (_files.GetIter(out TreeIter row, args.Path)) {
                Guid id = Guid.Parse((string)_files.GetValue(row, 6));
                if ((bool)_files.GetValue(row, 7)) {
                    Navigate(id);
                    }
                else if (_pluginArchive is null && _settings.Trigger == "Double-Click") {
                    PreviewEntry(id);
                    }
                }
            };
        _list.Selection.Changed += (_, _) => {
            Guid[]? ids = SelectedIds();
            ulong bytes = 0;
            if (_browser is not null) {
                foreach (Guid id in ids) {
                    bytes += _browser.Get(id).Bytes;
                    }
                }

            _summary.Text = $"{ids.Length:N0} selected" + (ids.Length > 0 ? $" · {ArchiveBrowser.HumanSize(bytes)}" : "");
            UpdateActions();
            };
        }

    public void Show() => _window.ShowAll();
    public void LoadPreview() {
        if (_operationActive) {
            return;
            }

        ClosePreviews();
        _archive?.Dispose();
        _archive = null;
        _pluginArchive = null;
        LoadBrowser(ArchiveBrowser.Preview());
        }

    /// <summary>Open a supported archive. Failures leave the previous view intact.</summary>
    public bool OpenArchive(string fileName, string? password = null, bool showErrors = true) {
        if (_operationActive) {
            return false;
            }

        fileName = FileTransfer.LocalPath(fileName);
        try {
            PluginMatch? match = PluginCatalog.Installed.Match(fileName);
            if (match is not null) {
                PluginArchive archive = RunPluginOperation("Opening archive", token => PluginArchive.Open(match, fileName, token));
                LoadPluginArchive(archive);
                return true;
                }
            }
        catch (OperationCanceledException) {
            _status.Text = "Opening archive cancelled.";
            return false;
            }
        catch (Exception error) when (showErrors) {
            Message("Could not open plugin archive", fileName + "\n\n" + error.Message, MessageType.Error);
            return false;
            }

        return OpenMnxArchive(fileName, password, showErrors);
        }

    private bool OpenMnxArchive(string fileName, string? password, bool showErrors) {
        ArchiveFile next = new ArchiveFile();
        try {
            next.Open(fileName, password);
            ArchiveBrowser? browser = ArchiveBrowser.FromArchive(next);
            ClosePreviews();
            _archive?.Dispose();
            _archive = next;
            _pluginArchive = null;
            LoadBrowser(browser);
            return true;
            }
        catch (CryptographicException) when (password is null && showErrors) {
            next.Dispose();
            string? entered = AskPassword();
            return entered is not null && OpenMnxArchive(fileName, entered, showErrors);
            }
        catch (Exception ex) {
            ArchiveFile.ReturnCode errorCode = next.ErrorStatus;
            next.Dispose();
            if (!showErrors) {
                throw;
                }

            Message("Invalid or unreadable MNX archive", ArchiveOpenError(fileName, ex, errorCode), MessageType.Error);
            return false;
            }
        }

    internal static string ArchiveOpenError(string path, Exception error, ArchiveFile.ReturnCode code) {
        string description = path + "\n\n" + error.Message;
        if (code != 0 && code != ArchiveFile.ReturnCode.None && code != ArchiveFile.ReturnCode.Success) {
            description += "\n\nError code: " + code;
            }
        return description;
        }

    private void LoadBrowser(ArchiveBrowser browser, Guid? folder = null) {
        // Clear selection while its IDs still belong to the previous browser.
        // GTK emits change signals during model clearing.
        _list.Selection.UnselectAll();
        _files.Clear();
        _contextSelection = [];
        _syncingTree = true;
        _folders.Clear();
        _folderRows.Clear();
        _populated.Clear();
        _browser = browser;
        TreeIter rootRow = _folders.AppendValues(OwnedIcons.Get("archive", 16), browser.Title, browser.Root.ToString());
        _folderRows[browser.Root] = rootRow;
        PopulateFolders(rootRow);
        _syncingTree = false;
        _window.Title = browser.Title + " — MNX Archive Manager" + (IsLayoutPreview ? " (layout preview)" : "");
        UpdateDragSources();
        Navigate(folder ?? browser.Root);
        }

    private bool IsLayoutPreview => _archive is null && _pluginArchive is null;

    private void PopulateFolders(TreeIter parent) {
        if (_browser is null || !Guid.TryParse((string)_folders.GetValue(parent, 2), out Guid id) || !_populated.Add(id)) {
            return;
            }

        if (_folders.IterChildren( out TreeIter placeholder, parent)) {
            _folders.Remove(ref placeholder);
            }

        foreach (ArchiveBrowser.Entry entry in _browser.Children(id).Where(e => e.IsDirectory)) {
            TreeIter row = _folders.AppendValues(parent, OwnedIcons.Get("folder", 16), entry.Name, entry.Id.ToString());
            _folderRows[entry.Id] = row;
            if (_browser.HasFolders(entry.Id)) {
                _folders.AppendValues(row, null, "", "");
                }
            }
        }

    private void Navigate(Guid folder) {
        if (_browser is null) {
            return;
            }

        CancelHover(closePopups: true);
        _current = folder;
        _files.Clear();
        _path.Text = _browser.PathOf(folder);
        bool plugin = _pluginArchive is not null;
        foreach (ArchiveBrowser.Entry entry in _browser.Children(folder)) {
            string flags = entry.IsDirectory ? "[   ]" : $"[{(_browser.Compressed ? 'C' : ' ')}{(_browser.Encrypted ? 'E' : ' ')} ]";
            _files.AppendValues(
                OwnedIcons.Get(entry.IsDirectory ? "folder" : "file", 16),
                entry.Name,
                entry.IsDirectory ? "" : ArchiveBrowser.HumanSize(entry.Bytes),
                plugin ? "" : flags,
                entry.IsDirectory ? "" : entry.Bytes.ToString("N0", CultureInfo.InvariantCulture),
                plugin && entry.CreatedUtc == default ? "" : entry.CreatedUtc.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                entry.Id.ToString(),
                entry.IsDirectory,
                entry.Bytes,
                entry.CreatedUtc.Ticks,
                ArchiveBrowser.FileType(entry),
                entry.IsDirectory || plugin ? "" : ArchiveBrowser.HumanChunks(entry.Chunks),
                entry.Chunks
                );
            }

        _syncingTree = true;
        try {
            foreach (Guid id in _browser.Ancestors(folder)) {
                PopulateFolders(_folderRows[id]);
                }

            using TreePath treePath = _folders.GetPath(_folderRows[folder]);
            _tree.ExpandToPath(treePath);
            _tree.ExpandRow(treePath, false);
            _tree.Selection.SelectPath(treePath);
            _tree.ScrollToCell(treePath, null, false, 0, 0);
            }
        finally {
            _syncingTree = false;
            }

        int count = _browser.Children(folder).Count;
        _status.Text = $"{count:N0} items in {_path.Text}" + (IsLayoutPreview ? " · Layout preview" : "");
        _summary.Text = "0 selected";
        UpdateActions();
        }

    private Guid[] SelectedIds() {
        List<Guid> selected = new List<Guid>();
        foreach (TreePath path in _list.Selection.GetSelectedRows()) {
            using (path) {
                if (_files.GetIter(out TreeIter row, path) && Guid.TryParse((string)_files.GetValue(row, 6), out Guid id)) {
                    selected.Add(id);
                    }
                }
            }
        return selected.ToArray();
        }
    private void NavigateUp() {
        if (_browser is not null) {
            Navigate(_browser.Parent(_current));
            }
        }

    private void NavigatePath() {
        if (_browser is null) {
            return;
            }

        try {
            Navigate(_browser.Resolve(_path.Text));
            }
        catch (ArgumentException ex) {
            _status.Text = ex.Message;
            }
        }

    private void UpdateActions() {
        bool loaded = _browser is not null;
        foreach (ArchiveAction action in Enum.GetValues<ArchiveAction>()) {
            bool enabled = action is ArchiveAction.New or ArchiveAction.Open || loaded;
            if (action == ArchiveAction.Delete) {
                enabled = loaded && _list.Selection.CountSelectedRows() > 0;
                }

            if (_pluginArchive is not null) {
                enabled = action switch {
                    ArchiveAction.New or ArchiveAction.Open or ArchiveAction.Info => true,
                    ArchiveAction.Add => _pluginArchive.Supports(PluginActions.Add),
                    ArchiveAction.Extract => _pluginArchive.Supports(PluginActions.Extract),
                    ArchiveAction.Delete => _pluginArchive.Supports(PluginActions.Delete) && _list.Selection.CountSelectedRows() > 0,
                    _ => false,
                    };
                }

            string suffix = action.ToString().ToLowerInvariant();
            enabled = enabled && !_operationActive;
            Widget<ToolButton>("action_" + suffix).Sensitive = enabled;
            Widget<MenuItem>("menu_" + suffix).Sensitive = enabled;
            }

        Widget<MenuItem>("menu_close").Sensitive = loaded && !_operationActive;
        Widget<MenuItem>("menu_resume").Sensitive = !_operationActive && _pluginArchive is null;
        foreach (MenuItem create in _pluginCreateItems) {
            create.Sensitive = !_operationActive;
            }
        Widget<MenuItem>("menu_options").Sensitive = !_operationActive;
        Widget<Button>("navigate_up").Sensitive = loaded && _current != _browser!.Root;
        Widget<Button>("navigate_root").Sensitive = loaded;
        _path.Sensitive = loaded;
        }

    private void Dispatch(ArchiveAction action) {
        if (_operationActive) {
            return;
            }

        if (action == ArchiveAction.Open) {
            ChooseArchive();
            return;
            }

        if (_pluginArchive is not null && action != ArchiveAction.New) {
            DispatchPluginAction(action);
            return;
            }

        if (action == ArchiveAction.Extract && _archive is not null) {
            ExtractEntries(SelectedIds());
            return;
            }

        if (action == ArchiveAction.Delete && _archive is not null) {
            DeleteEntries(SelectedIds());
            return;
            }

        if (action == ArchiveAction.Info && _browser is not null) {
            Message(
                "Archive information",
                _archive is null ? "Layout preview — sample names and sizes only." : $"{_archive.ArchivePath}\n\nCompression: {_archive.Compression}\nEncrypted: {_archive.IsEncrypted}\nChunk size: {_archive.ChunkSize:N0} bytes\nFront index: {_archive.IndexChunkCount:N0} chunks\nEntries: {_archive.Files.Count:N0}"
                );
            return;
            }

        ArchiveActionEventArgs args = new ArchiveActionEventArgs(action, _current, SelectedIds());
        if (ActionRequested is not null) {
            ActionRequested(this, args);
            }
        else {
            _status.Text = $"{action}: event hook ready; archive operation is not connected yet.";
            }
        }

    private void ChooseArchive() {
        FileChooserDialog chooser = new FileChooserDialog(
            "Open archive",
            _window,
            FileChooserAction.Open,
            "Cancel",
            ResponseType.Cancel,
            "Open",
            ResponseType.Accept
            );
        GtkWindowAppearance.Apply(chooser);
        AddArchiveFilter(chooser);
        IReadOnlyList<ArchivePlugin> plugins = PluginCatalog.Installed.Modules;
        foreach (ArchivePlugin plugin in plugins) {
            AddPluginFilter(chooser, plugin);
            }
        FileFilter all = new FileFilter {
            Name = "All files"
            };
        all.AddPattern("*");
        chooser.AddFilter(all);
        if (plugins.Count > 0) {
            chooser.Filter = all;
            }
        if ((ResponseType)chooser.Run() == ResponseType.Accept) {
            string path = chooser.Filename;
            chooser.Hide();
            OpenArchive(path);
            }

        chooser.Dispose();
        }

    private string? AskPassword() {
        Dialog dialog = new Dialog(
            "Encrypted archive",
            _window,
            DialogFlags.Modal,
            "Cancel",
            ResponseType.Cancel,
            "Open",
            ResponseType.Accept
            );
        GtkWindowAppearance.Apply(dialog);
        Entry input = new Entry {
            Visibility = false,
            ActivatesDefault = true,
            WidthChars = 30
            };
        dialog.ContentArea.BorderWidth = 16;
        dialog.ContentArea.Spacing = 8;
        dialog.ContentArea.PackStart(new Label("Archive passphrase:"), false, false, 0);
        dialog.ContentArea.PackStart(input, false, false, 0);
        dialog.DefaultResponse = ResponseType.Accept;
        dialog.ShowAll();
        input.GrabFocus();
        string? password = (ResponseType)dialog.Run() == ResponseType.Accept ? input.Text : null;
        input.Text = "";
        dialog.Dispose();
        return password;
        }

    private void Message(string title, string text, MessageType type = MessageType.Info) {
        MessageDialog dialog = OwnedIcons.Dialog(_window, DialogFlags.Modal, type, ButtonsType.Ok, false, "{0}", text);
        dialog.Title = title;
        dialog.Run();
        dialog.Dispose();
        }

    private void CloseArchive() {
        if (_operationActive) {
            return;
            }

        ClosePreviews();
        _archive?.Dispose();
        _archive = null;
        _pluginArchive = null;
        _browser = null;
        _syncingTree = true;
        _folders.Clear();
        _syncingTree = false;
        _files.Clear();
        _folderRows.Clear();
        _populated.Clear();
        _current = Guid.Empty;
        _path.Text = "/";
        _window.Title = "MNX Archive Manager";
        _status.Text = "Open an archive to begin.";
        _summary.Text = "0 selected";
        UpdateDragSources();
        UpdateActions();
        }

    private void OnKeyPress(object sender, KeyPressEventArgs args) {
        if (_operationActive) {
            return;
            }

        bool control = args.Event.State.HasFlag(Gdk.ModifierType.ControlMask);
        bool alt = args.Event.State.HasFlag(Gdk.ModifierType.Mod1Mask);
        if (control && args.Event.Key is Gdk.Key.o or Gdk.Key.O) {
            ChooseArchive();
            args.RetVal = true;
            }
        else if (control && args.Event.Key is Gdk.Key.l or Gdk.Key.L) {
            _path.GrabFocus();
            _path.SelectRegion(0, -1);
            args.RetVal = true;
            }
        else if (alt && args.Event.Key == Gdk.Key.Up) {
            NavigateUp();
            args.RetVal = true;
            }
        }

    public void Dispose() {
        if (_disposed) {
            return;
            }

        _disposed = true;
        _operationActive = true;
        ClosePreviews();
        _archive?.Dispose();
        _dragFiles?.Dispose();
        foreach (DragFiles files in _oldDragFiles) {
            files.Dispose();
            }
        _entryMenu.Destroy();
        _window.Destroy();
        _files.Dispose();
        _folders.Dispose();
        _builder.Dispose();
        }

    internal void RunSmokeTest(string screenshots) => ScaffoldSmoke.Run(this, screenshots);
    internal T TestWidget<T>(string id)
        where T : GLib.Object => Widget<T>(id);
    internal string TestCurrentPath => _path.Text;
    internal int TestListCount => _files.IterNChildren();
    internal ArchiveBrowser? TestBrowser => _browser;
    internal ArchiveFile? TestArchive => _archive;
    }
