using Mash.GUI.MNXArchiver.Diagnostics;
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

    private NativeVideoProvider? VideoProviderFor(string name) {
        if (_archive is null && _pluginArchive?.Supports(PluginActions.StreamEntry) != true) {
            return null;
            }
        return PluginCatalog.Installed.VideoProviderFor(name);
        }

    private bool CanPreview(Guid id, bool hover = false) {
        if (_busy || !_settings.Previews || _browser is null) {
            return false;
            }
        ArchiveBrowser.Entry entry = _browser.Get(id);
        if (entry.IsDirectory) {
            return false;
            }
        if (!hover && VideoProviderFor(entry.Name) is not null) {
            return true;
            }
        return entry.Bytes <= (ulong)_settings.PreviewBufferBytes
            && (_archive is not null || _pluginArchive?.Supports(PluginActions.ReadEntry) == true)
            && _previews.CanPreview(entry.Name);
        }

    private void OnListHover(object? sender, MouseEventArgs args) {
        if (_busy || !_settings.Previews || _settings.Trigger != "Mouse-Over With Delay" || args.Button != MouseButtons.None) {
            return;
            }
        ListViewItem? item = fileList.GetItemAt(args.X, args.Y);
        Guid id = item?.Tag is Guid found && CanPreview(found, hover: true) ? found : Guid.Empty;
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
        if (!CanPreview(id, hover)) {
            if (!hover) {
                DebugLog.Write("Preview request rejected by type, size or settings.", LogType.GUI | LogType.Warning);
                statusMessage.Text = "Preview unavailable. Enable a supported type in Options; files must fit the preview buffer.";
                }
            return;
            }
        if (!hover) {
            DebugLog.Write("Preview request accepted.", LogType.GUI);
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
            NativeVideoProvider? videoProvider = hover ? null : VideoProviderFor(entry.Name);
            if (videoProvider is not null) {
                statusMessage.Text = "Opening preview: " + entry.Name;
                try {
                    await _previews.ShowVideoAsync(videoProvider, token => moduleArchive is not null
                        ? moduleArchive.OpenEntryStream(id, token)
                        : archive!.OpenRead(id), entry.Name, position);
                    statusMessage.Text = "Preview: " + entry.Name;
                    return;
                    }
                catch (InvalidVideoException) when (kind != PreviewKind.Unsupported &&
                    entry.Bytes <= (ulong)bufferLimit &&
                    (archive is not null || moduleArchive?.Supports(PluginActions.ReadEntry) == true)) {
                    // A text or image preview can share a video extension.
                    }
                }

            data = await Task.Run(() => {
                if (moduleArchive is not null) {
                    return PreviewData.Read(moduleArchive, id, kind, CancellationToken.None, bufferLimit);
                    }
                return PreviewData.Read(archive!, id, kind, CancellationToken.None, bufferLimit);
                });
            if (data.IsMpegTransportDisguisedAsText(entry.Name)) {
                statusMessage.Text = "Open MPEG transport stream previews explicitly.";
                return;
                }
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
