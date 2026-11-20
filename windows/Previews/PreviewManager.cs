using System.Drawing.Drawing2D;
using System.Globalization;
using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Previews;
using Mash.GUI.MNXArchiver.Settings;

namespace Mash.GUI.MNXArchiver;

/// <summary>Owns preview windows and their bounded plaintext snapshots.</summary>
internal sealed class PreviewManager : IDisposable {
    internal const int MaximumWindows = 10;
    private readonly Form _owner;
    private readonly AppSettings _settings;
    private readonly List<PreviewForm> _windows = new List<PreviewForm>();
    private readonly List<VideoPreviewForm> _videoWindows = new List<VideoPreviewForm>();
    private readonly List<(Task Completion, long MemoryCost)> _stoppingVideos = new List<(Task, long)>();
    private bool _disposed;
    internal int Count => _windows.Count + _videoWindows.Count;
    internal IReadOnlyList<Form> Windows => _windows.Cast<Form>().Concat(_videoWindows).ToArray();

    internal PreviewManager(Form owner, AppSettings settings) {
        _owner = owner;
        _settings = settings.Copy();
        _owner.FormClosed += OwnerClosed;
        }
    internal bool CanPreview(string name) {
        if (_disposed || !_settings.Previews) {
            return false;
            }
        PreviewKind kind = PreviewData.Classify(name, _settings);
        if (kind == PreviewKind.Unsupported) {
            return false;
            }
        if (kind == PreviewKind.Image && PluginCatalog.Installed.ImageProviderFor(name) is not null) {
            return true;
            }
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (kind == PreviewKind.Image && new string[] { ".webp", ".pnm", ".ppm", ".pgm", ".pbm", ".tga" }.Contains(extension)) {
            return false;
            }
        return true;
        }
    /// <summary>Takes ownership of data, including when validation or display fails.</summary>
    internal void Show(PreviewData data, string name, Point itemScreenPosition, bool hover) {
        PreviewForm? preview = null;
        bool transferred = false;
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_settings.Previews || data.Kind == PreviewKind.Unsupported) {
                throw new IOException("Enable a supported preview type in Options first.");
                }
            if (data.Bytes.Length > _settings.PreviewBufferBytes || data.Kind == PreviewKind.Text && data.Bytes.Length > PreviewData.TextLimit) {
                throw new IOException("The preview exceeds its configured input buffer.");
                }
            PrepareWindow();
            ImagePreflight.ImageInfo? imageInfo = null;
            if (data.Kind == PreviewKind.Image) {
                imageInfo = InspectImage(data, name);
                }
            long memoryCost = data.Bytes.Length + (imageInfo.HasValue ? imageInfo.Value.MemoryCost : 4L * data.Bytes.Length + 4096);
            if (memoryCost > AvailableMemory()) {
                throw new IOException($"Close a preview first: the combined preview memory limit is {_settings.TotalPreviewBytes / (1024 * 1024):N0} MiB. You can change it in Options.");
                }
            preview = new PreviewForm(_owner, data, name, _settings, itemScreenPosition, hover, imageInfo, memoryCost);
            transferred = true;
            _windows.Add(preview);
            preview.Disposed += PreviewDisposed;
            if (preview.Independent) {
                preview.Show();
                }
            else {
                preview.Show(_owner);
                }
            }
        catch {
            if (preview is not null) {
                _windows.Remove(preview);
                }
            preview?.Dispose();
            throw;
            }
        finally {
            if (!transferred) {
                data.Dispose();
                }
            }
        }

    internal async Task ShowVideoAsync(NativeVideoProvider provider, Func<CancellationToken, Stream> openSource, string name, Point anchor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_settings.Previews) {
            throw new IOException("Enable previews in Options first.");
            }
        Point cursor = Cursor.Position;
        PrepareWindow();
        if (!_settings.IndependentWindows) {
            await WaitForVideosAsync();
            }
        long limit = Math.Min(_settings.DecodedPreviewBytes, AvailableMemory());
        VideoPlayback playback = await VideoPlayback.OpenAsync(provider, openSource, _settings.HalfResolutionPreviews, limit);
        VideoPreviewForm? preview = null;
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);
            preview = new VideoPreviewForm(_owner, playback, name, new PreviewPresentation(_settings), anchor, cursor);
            _videoWindows.Add(preview);
            preview.Disposed += VideoDisposed;
            if (preview.Independent) {
                preview.Show();
                }
            else {
                preview.Show(_owner);
                }
            }
        catch {
            if (preview is null) {
                await playback.StopAsync();
                }
            else {
                preview.Dispose();
                await preview.StopPlayback();
                }
            throw;
            }
        }

    private void PrepareWindow() {
        if (!_settings.IndependentWindows) {
            foreach (PreviewForm current in _windows.ToArray()) {
                if (!current.Independent) {
                    current.Close();
                    }
                }
            foreach (VideoPreviewForm current in _videoWindows.ToArray()) {
                if (!current.Independent) {
                    current.Close();
                    }
                }
            }
        if (Count >= MaximumWindows) {
            throw new IOException("Close a preview first: at most ten preview windows may be open.");
            }
        }

    private long AvailableMemory() {
        _stoppingVideos.RemoveAll(video => video.Completion.IsCompletedSuccessfully);
        return _settings.TotalPreviewBytes - _windows.Sum(window => window.MemoryCost)
            - _videoWindows.Sum(window => window.MemoryCost) - _stoppingVideos.Sum(video => video.MemoryCost);
        }

    private void VideoDisposed(object? sender, EventArgs args) {
        if (sender is VideoPreviewForm preview) {
            preview.Disposed -= VideoDisposed;
            _videoWindows.Remove(preview);
            Task stopped = preview.StopPlayback();
            if (!stopped.IsCompletedSuccessfully) {
                _stoppingVideos.Add((stopped, preview.MemoryCost));
                }
            }
        }

    private Task WaitForVideosAsync() {
        _stoppingVideos.RemoveAll(video => video.Completion.IsCompletedSuccessfully);
        return Task.WhenAll(_stoppingVideos.Select(video => video.Completion));
        }

    internal Task CloseVideosAsync() {
        foreach (VideoPreviewForm preview in _videoWindows.ToArray()) {
            preview.Close();
            preview.Dispose();
            }
        return WaitForVideosAsync();
        }

    private ImagePreflight.ImageInfo InspectImage(PreviewData data, string name) {
        NativeImageProvider? provider = PluginCatalog.Installed.ImageProviderFor(name);
        return ImagePreflight.Inspect(data.Bytes, _settings.Flag("Images", "Animated"),
            _settings.DecodedPreviewBytes, _settings.HalfResolutionPreviews, provider);
        }

    private void PreviewDisposed(object? sender, EventArgs args) {
        if (sender is PreviewForm preview) {
            preview.Disposed -= PreviewDisposed;
            _windows.Remove(preview);
            }
        }
    internal void HideHover() {
        foreach (PreviewForm preview in _windows.ToArray()) {
            if (preview.IsTooltip) {
                preview.Close();
                }
            }
        }
    private void OwnerClosed(object? sender, FormClosedEventArgs args) {
        Dispose();
        }
    public void Dispose() {
        if (_disposed) {
            return;
            }
        _disposed = true;
        _owner.FormClosed -= OwnerClosed;
        CloseVideosAsync().GetAwaiter().GetResult();
        _stoppingVideos.Clear();
        foreach (PreviewForm preview in _windows.ToArray()) {
            preview.Close();
            preview.Dispose();
            }
        _windows.Clear();
        }
    }

