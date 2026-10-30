using Mash.UI.MNXFile.GUI.Previews;
using Mash.UI.MNXFile.GUI.Settings;

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
        if (FileTransfer.IsArchiveDrop(paths)) {
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
        if (_archive is null) {
            // Explicit Add must not run through OpenInputs' archive auto-detection.
            await CreateFromInputs(paths);
            }
        else {
            await ImportFiles(paths, target);
            }
        }

    private void ConfigureDragAndDrop() {
        foreach (Control control in new Control[] { this, folderTree, fileList }) {
            control.DragEnter += (_, args) => args.Effect = !_busy && !_draggingOut && args.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
            control.DragOver += (_, args) => args.Effect = !_busy && !_draggingOut && args.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
            control.DragDrop += async (_, args) => {
                if (_busy || _draggingOut || args.Data?.GetData(DataFormats.FileDrop) is not string[] dropped) {
                    return;
                    }
                await Guard(async () => {
                    string[] paths = FileTransfer.LocalPaths(dropped);
                    Guid target = _current;
                    Point point = control.PointToClient(new Point(args.X, args.Y));
                    if (control == folderTree && folderTree.GetNodeAt(point)?.Tag is Guid folder) {
                        target = folder;
                        }
                    if (control == fileList && fileList.GetItemAt(point.X, point.Y)?.Tag is Guid item && _browser!.Get(item).IsDirectory) {
                        target = item;
                        }
                    await HandleDroppedFiles(paths, target);
                    });
                };
            }
        foreach (Control source in new Control[] { folderTree, fileList }) {
            source.MouseDown += (_, args) => {
                if (args.Button != MouseButtons.Left || _busy || _archive is null) {
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
                if (!_dragArmed || args.Button != MouseButtons.Left || _busy || _archive is null) {
                    return;
                    }
                Size threshold = SystemInformation.DragSize;
                Rectangle origin = new Rectangle(_dragOrigin.X - threshold.Width / 2, _dragOrigin.Y - threshold.Height / 2, threshold.Width, threshold.Height);
                if (origin.Contains(args.Location)) {
                    return;
                    }
                _dragArmed = false;
                Guid[] selection = _dragSelection;
                CancelHover();
                await Guard(async () => {
                    DragFiles lease = new DragFiles(_settings.TempPath);
                    _dragLeases.Add(lease);
                    string destination = lease.CreateDirectory();
                    ExtractionPlan plan = ExtractionPlan.Create(_archive!, selection, destination);
                    if (!await RunExtraction(plan)) {
                        return;
                        }
                    string[] paths = plan.RootPaths.ToArray();
                    if (paths.Length == 0) {
                        return;
                        }
                    DataObject data = new DataObject(DataFormats.FileDrop, paths);
                    _draggingOut = true;
                    try {
                        source.DoDragDrop(data, DragDropEffects.Copy);
                        }
                    finally {
                        _draggingOut = false;
                        }
                    });
                };
            }
        }

    internal Guid[] DragSelectionAt(Control source, Point point) {
        if (_browser is null || _archive is null || _busy) {
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
