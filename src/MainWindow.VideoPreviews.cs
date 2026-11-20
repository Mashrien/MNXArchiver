using Gtk;
using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Previews;
using Mash.GUI.MNXArchiver.Settings;
using MNX;
using Action = System.Action;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainWindow {
    private readonly List<GtkVideoPreviewWindow> _videoPreviews = [];
    private Task<VideoPlayback>? _openingVideo;
    private CancellationTokenSource? _videoCancellation;
    private uint _videoPoll;
    internal GtkVideoPreviewWindow? TestVideoPreview => _videoPreviews.LastOrDefault();
    internal VideoPlayback? TestVideoPlayback => TestVideoPreview?.Playback;

    private long PreviewMemoryUsed() {
        return _previews.Sum(preview => preview.MemoryCost) + _videoPreviews.Sum(preview => preview.MemoryCost);
        }

    private int PreviewWindowCount() {
        return _previews.Count + _videoPreviews.Count;
        }

    private NativeVideoProvider? VideoProviderForEntry(ArchiveBrowser.Entry entry) {
        if (entry.IsDirectory) {
            return null;
            }
        if (_archive is null && _pluginArchive?.Supports(PluginActions.StreamEntry) != true) {
            return null;
            }
        return PluginCatalog.Installed.VideoProviderFor(entry.Name);
        }

    private bool IsVideoEntry(Guid id) {
        return _browser is not null && VideoProviderForEntry(_browser.Get(id)) is not null;
        }

    private void OpenVideoPreview(Guid id, ArchiveBrowser.Entry entry, NativeVideoProvider provider, Action? fallback,
        Gdk.Rectangle anchor, int cursorX, int cursorY) {
        AppSettings settings = _settings.Copy();
        PreviewPresentation presentation = new PreviewPresentation(settings);
        CloseOwnedPreviews(presentation.Independent);
        if (PreviewWindowCount() >= MaxPreviewWindows) {
            _status.Text = "Close a preview first: at most 10 preview windows may be open.";
            return;
            }

        long memoryLimit = Math.Min(settings.DecodedPreviewBytes, settings.TotalPreviewBytes - PreviewMemoryUsed());
        Archive? archive = _archive;
        OpenedModuleArchive? module = _pluginArchive;
        _videoCancellation = new CancellationTokenSource();
        _openingVideo = VideoPlayback.OpenAsync(provider, cancellation =>
            module is null ? archive!.OpenRead(id) : module.OpenEntryStream(id, cancellation),
            settings.HalfResolutionPreviews, memoryLimit, _videoCancellation.Token);
        _status.Text = "Opening video: " + entry.Name;
        _videoPoll = GLib.Timeout.Add(25, () => CompleteVideoPreview(entry.Name, fallback, presentation, anchor, cursorX, cursorY));
        }

    private bool CompleteVideoPreview(string name, Action? fallback, PreviewPresentation presentation,
        Gdk.Rectangle anchor, int cursorX, int cursorY) {
        if (_openingVideo is null || !_openingVideo.IsCompleted) {
            return _openingVideo is not null;
            }

        Task<VideoPlayback> opened = _openingVideo;
        _openingVideo = null;
        _videoPoll = 0;
        _videoCancellation?.Dispose();
        _videoCancellation = null;
        VideoPlayback? playback = null;
        try {
            playback = opened.GetAwaiter().GetResult();
            GtkVideoPreviewWindow preview = new GtkVideoPreviewWindow(_window, name, playback, presentation,
                anchor, cursorX, cursorY, error => ShowVideoError(name, error));
            preview.Closed += closed => _videoPreviews.Remove(closed);
            _videoPreviews.Add(preview);
            playback = null;
            preview.Show();
            _status.Text = "Preview: " + name;
            }
        catch (OperationCanceledException) {
            }
        catch (InvalidVideoException) when (fallback is not null) {
            fallback();
            }
        catch (Exception error) {
            ShowVideoError(name, error);
            }
        finally {
            playback?.StopAsync().GetAwaiter().GetResult();
            }
        return false;
        }

    private void ShowVideoError(string name, Exception error) {
        _status.Text = "Could not preview " + name + ": " + error.Message;
        }

    private void StopVideoPreview() {
        foreach (GtkVideoPreviewWindow preview in _videoPreviews.ToArray()) {
            preview.Dispose();
            }
        if (_videoPoll != 0) {
            GLib.Source.Remove(_videoPoll);
            _videoPoll = 0;
            }
        if (_openingVideo is Task<VideoPlayback> opening) {
            _openingVideo = null;
            _videoCancellation?.Cancel();
            try {
                opening.GetAwaiter().GetResult().StopAsync().GetAwaiter().GetResult();
                }
            catch (Exception) {
                // Closing the archive also cancels a preview that has not opened yet.
                }
            }
        _videoCancellation?.Dispose();
        _videoCancellation = null;
        }

    private void CloseOwnedPreviews(bool independent) {
        if (independent) {
            return;
            }
        foreach (PreviewWindow preview in _previews.ToArray()) {
            if (!preview.Independent) {
                preview.Dispose();
                }
            }
        foreach (GtkVideoPreviewWindow preview in _videoPreviews.ToArray()) {
            if (!preview.Independent) {
                preview.Dispose();
                }
            }
        }
    }
