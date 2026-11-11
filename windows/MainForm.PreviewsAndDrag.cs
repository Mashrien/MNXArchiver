using Mash.UI.MNXFile.GUI.Previews;
using Mash.UI.MNXFile.GUI.Plugins;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private bool _draggingOut;

    private void ShowOptions() {
        if (_busy) {
            return;
            }
        CancelHover();
        _previews.Dispose();
        using OptionsForm dialog = new OptionsForm(_settings);
        if (dialog.ShowDialog(this) == DialogResult.OK) {
            _settings = dialog.SelectedSettings;
            }
        else {
            _settings = LoadSettings();
            }
        ApplyWindowAppearance();
        _previews = new PreviewManager(this, _settings);
        }

    private void ResetPreviews() {
        CancelHover();
        _previews.Dispose();
        _previews = new PreviewManager(this, _settings);
        }

    private bool CanPreview(Guid id) {
        if (_archive is null || _browser is null || _busy || !_settings.Previews) {
            return false;
            }
        ArchiveBrowser.Entry entry = _browser.Get(id);
        return !entry.IsDirectory && entry.Bytes <= (ulong)_settings.PreviewBufferBytes && _previews.CanPreview(entry.Name);
        }

    private void OnListHover(object? sender, MouseEventArgs args) {
        if (_busy || !_settings.Previews || _settings.Trigger != "Mouse-Over With Delay" || args.Button != MouseButtons.None) {
            return;
            }
        ListViewItem? item = fileList.GetItemAt(args.X, args.Y);
        Guid id = item?.Tag is Guid found && CanPreview(found) ? found : Guid.Empty;
        if (id == _hovered) {
            return;
            }
        CancelHover();
        _hovered = id;
        if (id != Guid.Empty) {
            _hoverPosition = fileList.PointToScreen(item!.Bounds.Location);
            hoverTimer.Interval = _settings.HoverDelay;
            hoverTimer.Start();
            }
        }

    private void CancelHover() {
        _hoverGeneration++;
        _hovered = Guid.Empty;
        hoverTimer.Stop();
        _previews?.HideHover();
        }

    private async Task PreviewEntry(Guid id, bool hover) {
        if (!CanPreview(id)) {
            if (!hover) {
                statusMessage.Text = "Preview unavailable. Enable a supported type in Options; files must fit the preview buffer.";
                }
            return;
            }
        ArchiveBrowser.Entry entry = _browser!.Get(id);
        PreviewKind kind = PreviewData.Classify(entry.Name, _settings);
        ListViewItem? row = fileList.Items.Cast<ListViewItem>().FirstOrDefault(item => item.Tag is Guid rowId && rowId == id);
        Point position = hover ? _hoverPosition : fileList.PointToScreen(row?.Bounds.Location ?? Point.Empty);
        long generation = _hoverGeneration;
        _previewReading = true;
        _busy = true;
        UpdateActions();
        PreviewData? data = null;
        try {
            data = await Task.Run(() => PreviewData.Read(_archive!, id, kind, CancellationToken.None, _settings.PreviewBufferBytes));
            Point currentPoint = fileList.PointToClient(Cursor.Position);
            Guid underCursor = fileList.GetItemAt(currentPoint.X, currentPoint.Y)?.Tag is Guid cursorId ? cursorId : Guid.Empty;
            if (hover && (generation != _hoverGeneration || underCursor != id)) {
                data.Dispose();
                data = null;
                return;
                }
            PreviewData owned = data;
            data = null;
            _previews.Show(owned, entry.Name, position, hover);
            statusMessage.Text = "Preview: " + entry.Name;
            }
        catch (Exception error) {
            statusMessage.Text = "Could not preview " + entry.Name + ": " + error.Message;
            }
        finally {
            data?.Dispose();
            _busy = false;
            _previewReading = false;
            UpdateActions();
            }
        }

    private async Task HandleDroppedFiles(string[] paths, Guid target) {
        if (paths.Length == 1 && System.IO.File.Exists(paths[0]) && ShouldOpenArchive(paths[0])) {
            if (_pluginArchive?.Supports(PluginActions.Add) == false) {
                await OpenArchive(paths[0]);
                return;
                }
            using ArchiveDropForm question = new ArchiveDropForm(paths[0]);
            DialogResult answer = question.ShowDialog(this);
            if (answer == DialogResult.Yes) {
                await OpenArchive(paths[0]);
                return;
                }
            if (answer != DialogResult.No) {
                return;
                }
            }
        if (_archive is null && _pluginArchive is null) {
            // Explicit Add must not run through OpenInputs' archive auto-detection.
            await CreateFromInputs(paths);
            }
        else {
            await ImportFiles(paths, target);
            }
        }

    private void ConfigureDragAndDrop() {
        DragEnter += SetFileDropEffect;
        DragOver += SetFileDropEffect;
        DragDrop += OnFileDrop;
        foreach (Control control in new Control[] { folderTree, fileList }) {
            control.DragEnter += SetFileDropEffect;
            control.DragOver += SetFileDropEffect;
            control.DragDrop += OnFileDrop;
            }
        foreach (Control source in new Control[] { folderTree, fileList }) {
            source.MouseDown += (_, args) => {
                if (args.Button != MouseButtons.Left || _busy) {
                    _dragArmed = false;
                    return;
                    }
                _dragOrigin = args.Location;
                _dragSelection = DragSelectionAt(source, args.Location);
                _dragArmed = _dragSelection.Length > 0;
                };
            source.MouseUp += async (_, arguments) => {
                bool clicked = _dragArmed;
                _dragArmed = false;
                if (source != fileList || !clicked || arguments.Button != MouseButtons.Left || _settings.Trigger != "Single-Click") {
                    return;
                    }
                Size threshold = SystemInformation.DragSize;
                if (Math.Abs(arguments.X - _dragOrigin.X) > threshold.Width || Math.Abs(arguments.Y - _dragOrigin.Y) > threshold.Height) {
                    return;
                    }
                ListViewItem? item = fileList.GetItemAt(arguments.X, arguments.Y);
                if (item?.Tag is Guid id && _dragSelection.Contains(id) && CanPreview(id)) {
                    await PreviewEntry(id, false);
                    }
                };
            source.MouseMove += async (_, args) => {
                if (!_dragArmed || args.Button != MouseButtons.Left || _busy) {
                    return;
                    }
                Size threshold = SystemInformation.DragSize;
                Rectangle origin = new Rectangle(_dragOrigin.X - threshold.Width / 2, _dragOrigin.Y - threshold.Height / 2, threshold.Width, threshold.Height);
                if (origin.Contains(args.Location)) {
                    return;
                    }
                _dragArmed = false;
                Guid[] selection = _dragSelection;
                await Guard(() => DragArchiveSelection(source, selection));
                };
            }
        }

    private void SetFileDropEffect(object? sender, DragEventArgs args) {
        args.Effect = !_busy && !_draggingOut
            && (args.AllowedEffect & DragDropEffects.Copy) != 0
            && args.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        }

    private async void OnFileDrop(object? sender, DragEventArgs args) {
        if (_busy || _draggingOut || args.Data?.GetData(DataFormats.FileDrop) is not string[] dropped) {
            args.Effect = DragDropEffects.None;
            return;
            }
        await Guard(async () => {
            string[] paths;
            try {
                paths = FileTransfer.LocalPaths(dropped);
                }
            catch {
                args.Effect = DragDropEffects.None;
                await Task.Yield();
                throw;
                }
            if (paths.Length == 0) {
                args.Effect = DragDropEffects.None;
                return;
                }
            Guid target = _current;
            Point location = new Point(args.X, args.Y);
            if (sender == folderTree && folderTree.GetNodeAt(folderTree.PointToClient(location))?.Tag is Guid folder) {
                target = folder;
                }
            if (sender == fileList) {
                Point point = fileList.PointToClient(location);
                if (fileList.GetItemAt(point.X, point.Y)?.Tag is Guid item && _browser?.Get(item).IsDirectory == true) {
                    target = item;
                    }
                }
            // Finish the desktop drag before opening an archive or showing a dialog.
            await Task.Yield();
            if (!_busy) {
                await HandleDroppedFiles(paths, target);
                }
            });
        }

    private async Task DragArchiveSelection(Control source, Guid[] selection) {
        DragFiles? lease = null;
        DragDropEffects effect = DragDropEffects.None;
        async Task<string[]> PrepareFiles(CancellationToken cancellation) {
            lease = new DragFiles(_settings.TempPath);
            string destination = lease.CreateDirectory();
            return await StageDraggedFiles(selection, destination, cancellation);
            }

        CancelHover();
        _draggingOut = true;
        _busy = true;
        UpdateActions();
        statusMessage.Text = "Drag to a folder to copy the selected entries.";
        try {
            effect = await StartFileDrag(source, PrepareFiles);
            statusMessage.Text = effect == DragDropEffects.Copy ? "File drop accepted." : "Drag cancelled.";
            }
        catch (OperationCanceledException) {
            statusMessage.Text = "Drag cancelled.";
            }
        finally {
            if (lease is not null) {
                if (effect == DragDropEffects.Copy) {
                    _dragLeases.Add(lease);
                    }
                else {
                    lease.Discard();
                    }
                }
            _busy = false;
            _draggingOut = false;
            UpdateActions();
            }
        }

    private static async Task<DragDropEffects> StartFileDrag(Control source, Func<CancellationToken, Task<string[]>> prepareFiles) {
        string[] paths = await prepareFiles(CancellationToken.None);
        DataObject data = new DataObject(DataFormats.FileDrop, paths);
        return source.DoDragDrop(data, DragDropEffects.Copy);
        }

    private Task<string[]> StageDraggedFiles(Guid[] selection, string destination, CancellationToken cancellation) {
        PluginArchive? plugin = _pluginArchive;
        ArchiveFile? archive = _archive;
        statusMessage.Text = "Preparing files to copy.";
        return Task.Run(() => {
            if (plugin is not null) {
                PluginExtraction plan = plugin.PlanExtraction(selection, destination, singleFile: false);
                plan.Extract(false, cancellation);
                return plan.RootPaths.ToArray();
                }
            if (archive is null) {
                throw new InvalidOperationException("Open an archive before dragging files out.");
                }
            ExtractionPlan planForMnx = ExtractionPlan.Create(archive, selection, destination);
            using TransferCancellation extraction = new TransferCancellation();
            using CancellationTokenRegistration registration = cancellation.Register(() => extraction.Choose(false));
            CancellationToken previous = archive.Options.CancellationToken;
            try {
                archive.Options.CancellationToken = extraction.Token;
                planForMnx.Extract(archive, new TransferTracker(archive.ChunkSize), extraction);
                return planForMnx.RootPaths.ToArray();
                }
            finally {
                archive.Options.CancellationToken = previous;
                }
            }, cancellation);
        }

    internal Guid[] DragSelectionAt(Control source, Point point) {
        if (_browser is null || _busy || (_archive is null && _pluginArchive?.Supports(PluginActions.Extract) != true)) {
            return Array.Empty<Guid>();
            }
        if (source == fileList) {
            ListViewItem? item = fileList.GetItemAt(point.X, point.Y);
            if (item is null || item.Tag is not Guid id) {
                return Array.Empty<Guid>();
                }
            return item.Selected ? SelectedIds() : new Guid[] { id };
            }
        if (source == folderTree) {
            TreeNode? node = folderTree.GetNodeAt(point);
            if (node?.Tag is Guid id && id != _browser.Root && (folderTree.HitTest(point).Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.Image)) != 0) {
                return new Guid[] { id };
                }
            }
        return Array.Empty<Guid>();
        }
    }
