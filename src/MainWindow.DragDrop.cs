using Gtk;
using Mash.UI.MNXFile.GUI.Icons;
using Mash.UI.MNXFile.GUI.Plugins;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive.Resume;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    private static readonly TargetEntry[] FileTargets = [new("text/uri-list", 0, 1)];
    private DragFiles? _dragFiles;
    private Guid[] _dragSelection = [];
    private Guid[] _pressedSelection = [];
    private string[]? _dragUris;
    private bool _operationActive, _quitRequested;
    private OperationProgressArgs? _transferProgress;
    private ArchiveProgressDialog? _activeProgressDialog;
    internal ArchiveProgressDialog? TestProgressDialog => _activeProgressDialog;

    internal void TestImportFiles(string[] paths) => ImportFiles(paths, _archive!.RootDirectoryId);
    private void ConfigureDragAndDrop() {
        _list.ButtonPressEvent += CaptureDragSelection;
        foreach (Gtk.Widget widget in new Gtk.Widget[] {
            _window,
            _tree,
            _list
            }

            ) {
            // Handle drop completion ourselves, exactly once, rather than asking
            // GTK's default handler to report success before the data is checked.
            Drag.DestSet(widget, DestDefaults.Motion | DestDefaults.Highlight, FileTargets, Gdk.DragAction.Copy);
            widget.DragDrop += OnFileDrop;
            widget.DragDataReceived += OnFilesReceived;
            }

        foreach (TreeView view in new[] {
            _tree,
            _list
            }

            ) {
            view.DragBegin += OnArchiveDragBegin;
            view.DragDataGet += OnArchiveDragDataGet;
            view.DragEnd += (_, _) => {
                _dragSelection = [];
                _pressedSelection = [];
                _dragUris = null;
                };
            }
        UpdateDragSources();
        }

    private void UpdateDragSources() {
        foreach (TreeView view in new[] { _tree, _list }) {
            if (_pluginArchive is null) {
                view.EnableModelDragSource(Gdk.ModifierType.Button1Mask, FileTargets, Gdk.DragAction.Copy);
                }
            else {
                view.UnsetRowsDragSource();
                }
            }
        }

    // GtkSharp normally connects event handlers after GTK's class handler.
    // Capture before TreeView's mouse-down handling narrows the selection.
    [GLib.ConnectBefore]
    private void CaptureDragSelection(object sender, ButtonPressEventArgs args) {
        if (args.Event.Button == 1) {
            _pressedSelection = SelectedIds();
            }
        }

    private void OnFileDrop(object sender, DragDropArgs args) {
        Widget? source = Drag.GetSourceWidget(args.Context);
        if (_operationActive || source == _tree || source == _list) {
            Drag.Finish(args.Context, false, false, args.Time);
            args.RetVal = true;
            return;
            }

        Drag.GetData((Gtk.Widget)sender, args.Context, Gdk.Atom.Intern("text/uri-list", false), args.Time);
        args.RetVal = true;
        }

    private void OnFilesReceived(object sender, DragDataReceivedArgs args) {
        string[] paths;
        Guid folder = DropFolder((Gtk.Widget)sender, args.X, args.Y);
        try {
            paths = FileTransfer.LocalPaths(args.SelectionData.Uris ?? []);
            if (_operationActive || paths.Length == 0) {
                Drag.Finish(args.Context, false, false, args.Time);
                return;
                }

            bool pluginCandidate = paths.Length == 1 && DiskFile.Exists(paths[0]) && PluginCatalog.Installed.LooksLikeArchive(paths[0]);
            if (pluginCandidate || FileTransfer.IsArchiveDrop(paths)) {
                Drag.Finish(args.Context, true, false, args.Time);
                ResponseType answer = (pluginCandidate && IsLayoutPreview) || _pluginArchive?.Supports(PluginActions.Add) == false
                    ? ResponseType.Yes : AskArchiveDrop(paths[0]);
                if (answer == ResponseType.Yes) {
                    OpenArchive(paths[0]);
                    }
                else if (answer == ResponseType.No) {
                    if (IsLayoutPreview) {
                        CreateFromInputs(paths);
                        }
                    else {
                        ImportFiles(paths, folder);
                        }
                    }
                return;
                }

            if (IsLayoutPreview) {
                throw new InvalidOperationException("Open an archive before adding files. You can drop a supported archive here to open it.");
                }
            if (_pluginArchive?.Supports(PluginActions.Add) == false) {
                throw new NotSupportedException("This archive format version does not support adding files.");
                }
            }
        catch (Exception ex) {
            Drag.Finish(args.Context, false, false, args.Time);
            Message("Could not accept dropped files", ex.Message, MessageType.Error);
            return;
            }

        // Accept a COPY before doing long I/O. The source file manager is free
        // to finish its gesture; it must never delete the original files.
        Drag.Finish(args.Context, true, false, args.Time);
        ImportFiles(paths, folder);
        }

    private ResponseType AskArchiveDrop(string path) {
        using Dialog question = new Dialog("Dropped archive", _window, DialogFlags.Modal,
            "Open Archive", ResponseType.Yes, "Add to Archive", ResponseType.No
            );
        GtkWindowAppearance.Apply(question);
        question.ContentArea.BorderWidth = 16;
        question.ContentArea.PackStart(new Label(Path.GetFileName(path) + "\nOpen this archive, or add it as a file?") { Xalign = 0 }, true, true, 0);
        question.DefaultResponse = ResponseType.Yes;
        question.ShowAll();
        ResponseType answer = (ResponseType)question.Run();
        question.Hide();
        return answer;
        }

    private Guid DropFolder(Gtk.Widget widget, int x, int y) {
        if (_browser is null) {
            return Guid.Empty;
            }

        if (widget is TreeView view) {
            view.ConvertWidgetToBinWindowCoords(x, y, out int binX, out int binY);
            if (view.GetPathAtPos(binX, binY, out TreePath path)) {
                using (path) {
                    if (view == _tree && _folders.GetIter( out TreeIter treeRow, path) && Guid.TryParse((string)_folders.GetValue(treeRow, 2), out Guid treeId)) {
                        return treeId;
                        }

                    if (view == _list && _files.GetIter( out TreeIter listRow, path) && (bool)_files.GetValue(listRow, 7)) {
                        return Guid.Parse((string)_files.GetValue(listRow, 6));
                        }
                    }
                }
            }

        if (widget == _tree) {
            return _browser.Root;
            }

        return _current;
        }

    private void OnArchiveDragBegin(object sender, DragBeginArgs args) {
        _dragSelection = [];
        _dragUris = null;
        if (_archive is null || _browser is null || _operationActive) {
            return;
            }

        if (sender == _tree) {
            if (_tree.Selection.GetSelected( out TreeIter row) && Guid.TryParse((string)_folders.GetValue(row, 2), out Guid id) && id != _browser.Root) {
                _dragSelection = [id];
                }
            }
        else {
            _dragSelection = SelectedIds();
            if (_dragSelection.Length == 1 && _pressedSelection.Length > 1 && _pressedSelection.Contains(_dragSelection[0])) {
                _dragSelection = _pressedSelection.ToArray();
                if (_files.GetIterFirst( out TreeIter row)) {
                    do {
                        Guid id = Guid.Parse((string)_files.GetValue(row, 6));
                        if (_dragSelection.Contains(id)) {
                            _list.Selection.SelectIter(row);
                            }
                        }
                    while (_files.IterNext(ref row));
                    }
                }
            }

        if (_dragSelection.Length == 0) {
            return;
            }

        Drag.SetIconPixbuf(args.Context, OwnedIcons.Get("save", 32), 0, 0);
        }

    private void OnArchiveDragDataGet(object sender, DragDataGetArgs args) {
        if (_archive is null || _dragSelection.Length == 0 || _operationActive) {
            return;
            }

        try {
            // Some targets ask more than once. Export once for this gesture.
            if (_dragUris is null) {
                Guid[] selected = _dragSelection.ToArray();
                _dragFiles ??= new DragFiles(_settings.TempPath);
                string directory = _dragFiles.CreateDirectory();
                _dragUris = ExportDraggedFiles(selected, directory);
                if (_dragUris.Length == 0) {
                    Drag.Cancel(args.Context);
                    return;
                    }
                }

            if (!args.SelectionData.SetUris(_dragUris)) {
                throw new IOException("The destination did not accept the file URI list.");
                }

            _status.Text = "Dragged files are ready for the destination to copy.";
            }
        catch (Exception ex) {
            _status.Text = "Could not prepare dragged files: " + ex.Message;
            Console.Error.WriteLine(ex);
            }
        }

    private void ImportFiles(string[] paths, Guid folder) {
        if (_pluginArchive is { } plugin) {
            ImportPluginFiles(plugin, paths, folder);
            return;
            }

        ArchiveFile? archive = _archive!;
        string archivePath = archive.ArchivePath!;
        using TransferCancellation cancellation = new TransferCancellation();
        try {
            TransferTracker tracker = new TransferTracker(archive.ChunkSize);
            RunTransfer("Adding dropped files", () => {
                FileTransfer.Import(archive, paths, folder, tracker, cancellation);
                return true;
                }, tracker, cancellation: cancellation);
            RefreshArchive(folder);
            _status.Text = $"Added {paths.Length:N0} dropped item(s).";
            }
        catch (OperationCanceledException) {
            if (cancellation.Delete) {
                DeleteCancelledArchive(archivePath);
                }
            else {
                RefreshArchive(folder);
                _status.Text = "Cancelled after the current file. Completed files kept; matching .mnq saved alongside the archive.";
                }
            }
        catch (Exception ex) {
            // Earlier files may already have committed before a later one failed.
            RefreshArchive(folder);
            Message("Could not add all dropped files", ex.Message, MessageType.Error);
            }
        }

    private void DeleteCancelledArchive(string path, FileInfo? queueFile = null) {
        CloseArchive();
        try {
            RemainingQueue.Discard(queueFile ?? new FileInfo(Path.ChangeExtension(path, ".mnq")));
            DiskFile.Delete(path);
            _status.Text = "Cancelled. The partial archive was deleted.";
            }
        catch (Exception ex) {
            _status.Text = "Archiving stopped, but the partial archive could not be deleted.";
            Message("Could not delete partial archive", path + "\n\n" + ex.Message, MessageType.Error);
            }
        }

    private void RefreshArchive(Guid folder) {
        if (_archive is not null) {
            LoadBrowser(ArchiveBrowser.FromArchive(_archive));
            Navigate(folder);
            }
        }

    private void RequestQuit() {
        if (_operationActive) {
            _quitRequested = true;
            _status.Text = "Closing when the current transfer finishes.";
            return;
            }

        ClosePreviews();
        _window.Hide();
        Application.Quit();
        }
    }
