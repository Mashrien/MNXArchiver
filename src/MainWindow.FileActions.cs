using Gtk;
using Mash.GUI.MNXArchiver.Icons;
using Mash.GUI.MNXArchiver.Plugins;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainWindow {
    private readonly Menu _entryMenu = new();
    private readonly MenuItem _extractEntry = new MenuItem("Extract…");
    private Guid[] _contextSelection = [];
    /// <summary>Open a signature-identified archive, or offer to archive ordinary input paths.</summary>
    public void OpenInputs(IEnumerable<string> inputs) {
        if (_operationActive) {
            return;
            }

        try {
            string[] paths = FileTransfer.LocalPaths(inputs.Select(FileTransfer.LocalPath));
            if (paths.Length == 0) {
                return;
                }

            if (paths.Length == 1 && DiskFile.Exists(paths[0]) &&
                (PluginCatalog.Installed.LooksLikeArchive(paths[0]) || FileTransfer.IsArchive(paths[0]))) {
                OpenArchive(paths[0]);
                return;
                }

            if (paths.Length == 1 && DiskFile.Exists(paths[0]) && RemainingQueue.IsQueue(new FileInfo(paths[0]))) {
                ResumeFromQueue(paths[0]);
                return;
                }

            CreateFromInputs(paths);
            }
        catch (OperationCanceledException) {
            _status.Text = "Archive creation cancelled. The output was not replaced.";
            }
        catch (Exception ex) {
            Message("Could not use supplied files", ex.Message, MessageType.Error);
            }
        }

    private ArchiveCreationDialog? _creationDialog;
    internal ArchiveCreationDialog? TestCreationDialog => _creationDialog;

    private void CreateFromInputs(string[] paths) {
        using ArchiveCreationDialog setup = new ArchiveCreationDialog(_window, paths, _settings, DefaultPassword());
        _creationDialog = setup;
        ArchiveCreationSettings? settings;
        try {
            settings = setup.Run();
            }
        finally {
            _creationDialog = null;
            }

        if (settings is null) {
            return;
            }

        string staging = Path.Combine(Path.GetDirectoryName(settings.Path)!, ".mnx-pending-" + Guid.NewGuid().ToString("N"));
        using Archive next = new Archive();
        next.Options.Compression = settings.Compression;
        next.Options.CompressionLevel = settings.Level;
        next.Options.OverrideByDefault = false;
        using TransferCancellation cancellation = new TransferCancellation();
        TransferTracker tracker = new TransferTracker(next.Options.ChunkSize);
        try {
            RunTransfer("Creating archive", () => {
                FileQueueItem[]? pending = FileTransfer.PrepareImport(next, paths, next.RootDirectoryId);
                tracker.Configure(pending);
                next.Create(new FileInfo(staging), settings.Password);
                bool complete = true;
                try {
                    FileTransfer.ProcessPreparedImport(
                        next,
                        pending,
                        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(paths[0]))!,
                        tracker,
                        cancellation
                        );
                    cancellation.CheckBoundary();
                    }
                catch (OperationCanceledException) when (cancellation.Keep) {
                    complete = false;
                    }

                next.Close();
                DiskFile.Move(staging, settings.Path, settings.Overwrite);
                if (!complete) {
                    RemainingQueue.Save(settings.Path, next.FileQueue, cancellation.ExportKeyFile ? settings.Password : null);
                    }

                return complete;
                }, tracker, next, settings.Path, settings.Password is not null, cancellation);
            OpenArchive(settings.Path, settings.Password);
            _status.Text = cancellation.Keep ? "Cancelled after the current file. Completed files kept; matching .mnq saved alongside the archive." : $"Created archive from {paths.Length:N0} input item(s).";
            }
        finally {
            next.Close();
            if (DiskFile.Exists(staging)) {
                DiskFile.Delete(staging);
                }
            }
        }

    private static void AddArchiveFilter(FileChooserDialog chooser) {
        FileFilter filter = new FileFilter {
            Name = "MNX archives (*.mnx)"
            };
        filter.AddPattern("*.mnx");
        filter.AddPattern("*.MNX");
        chooser.AddFilter(filter);
        }

    private void ConfigureExtractionMenu() {
        OwnedIcons.SetMenuImage(_extractEntry, "extract");
        _entryMenu.Append(_extractEntry);
        _extractEntry.Activated += (_, _) => ExtractEntries(_contextSelection);
        _list.ButtonPressEvent += OnEntryMenu;
        _tree.ButtonPressEvent += OnEntryMenu;
        AddEntryActions();
        _entryMenu.ShowAll();
        }

    [GLib.ConnectBefore]
    private void OnEntryMenu(object sender, ButtonPressEventArgs args) {
        if (args.Event.Button != 3 || _operationActive || IsLayoutPreview || _browser is null) {
            return;
            }

        TreeView? view = (TreeView)sender;
        int x = (int)args.Event.X;
        int y = (int)args.Event.Y;
        // Button events usually originate in TreeView's bin window. Drag-drop
        // coordinates, in contrast, are widget-relative; do not shift clicks twice.
        if (args.Event.Window.Handle != view.BinWindow.Handle) {
            view.ConvertWidgetToBinWindowCoords(x, y, out x, out y);
            }

        if (!view.GetPathAtPos(x, y, out TreePath path)) {
            return;
            }

        using (path) {
            if (view == _list) {
                if (!view.Selection.PathIsSelected(path)) {
                    view.Selection.UnselectAll();
                    view.Selection.SelectPath(path);
                    }

                _contextSelection = SelectedIds();
                }
            else {
                view.Selection.SelectPath(path);
                _folders.GetIter( out TreeIter row, path);
                Guid id = Guid.Parse((string)_folders.GetValue(row, 2));
                _contextSelection = id == _browser.Root ? [] : [id];
                }
            }

        _extractEntry.Sensitive = _contextSelection.Length > 0 && (_pluginArchive?.Supports(PluginActions.Extract) ?? true);
        _previewEntry.Sensitive = _contextSelection.Length == 1 && CanPreview(_contextSelection[0]);
        _deleteEntry.Sensitive = _contextSelection.Length > 0
            && (_archive is not null || _pluginArchive?.Supports(PluginActions.Delete) == true);
        _replaceEntry.Visible = _pluginArchive?.Supports(PluginActions.Replace) ?? false;
        _replaceEntry.Sensitive = CanReplacePluginFile(_contextSelection);
        _infoEntry.Sensitive = _contextSelection.Length > 0;
        _entryMenu.PopupAtPointer(args.Event);
        args.RetVal = true;
        }

    private void ExtractEntries(Guid[] ids, bool entireArchive = false) {
        if (_operationActive || IsLayoutPreview || _browser is null || _pluginArchive?.Supports(PluginActions.Extract) == false) {
            return;
            }

        if (ids.Length == 0) {
            if (entireArchive) {
                _status.Text = "The archive has no entries to extract.";
                }
            return;
            }

        bool singleFile = !entireArchive && ids.Length == 1 && !_browser.Get(ids[0]).IsDirectory;
        FileChooserDialog chooser = new FileChooserDialog(
            singleFile ? "Extract file" : "Extract to folder",
            _window,
            singleFile ? FileChooserAction.Save : FileChooserAction.SelectFolder,
            "Cancel",
            ResponseType.Cancel,
            "Extract",
            ResponseType.Accept
            );
        GtkWindowAppearance.Apply(chooser);
        if (singleFile) {
            chooser.CurrentName = _browser.Get(ids[0]).Name;
            chooser.DoOverwriteConfirmation = _pluginArchive is null;
            }

        string? destination = null;
        if ((ResponseType)chooser.Run() == ResponseType.Accept) {
            destination = chooser.Filename;
            }

        chooser.Hide();
        chooser.Dispose();
        if (destination is null) {
            return;
            }

        try {
            ExtractSelection(ids, destination, singleFile, replacementConfirmed: singleFile && _pluginArchive is null);
            }
        catch (Exception ex) {
            Message("Could not extract selection", ex.Message, MessageType.Error);
            }
        }

    internal Menu TestEntryMenu => _entryMenu;
    internal MenuItem TestExtractEntry => _extractEntry;
    }
