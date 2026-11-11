using Mash.GUI.MNXArchiver.Previews;
using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Settings;
using MNX;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
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
        if (_busy || !_settings.Previews || _browser is null) {
            return false;
            }
        if (_archive is null && _pluginArchive?.Supports(PluginActions.ReadEntry) != true) {
            return false;
            }

        ArchiveBrowser.Entry entry = _browser.Get(id);
        if (entry.IsDirectory || entry.Bytes > (ulong)_settings.PreviewBufferBytes) {
            return false;
            }
        return _previews.CanPreview(entry.Name);
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
        Archive? archive = _archive;
        OpenedModuleArchive? moduleArchive = _pluginArchive;
        int bufferLimit = _settings.PreviewBufferBytes;
        ListViewItem? row = fileList.Items.Cast<ListViewItem>().FirstOrDefault(item => item.Tag is Guid rowId && rowId == id);
        Point position = hover ? _hoverPosition : fileList.PointToScreen(row?.Bounds.Location ?? Point.Empty);
        long generation = _hoverGeneration;
        _previewReading = true;
        _busy = true;
        UpdateActions();
        PreviewData? data = null;
        try {
            data = await Task.Run(() => {
                if (moduleArchive is not null) {
                    return PreviewData.Read(moduleArchive, id, kind, CancellationToken.None, bufferLimit);
                    }
                return PreviewData.Read(archive!, id, kind, CancellationToken.None, bufferLimit);
                });
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

    }
