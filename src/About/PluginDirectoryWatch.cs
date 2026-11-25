namespace Mash.GUI.MNXArchiver.About;

/// <summary>Watches only after opening Plugins Folder. All hashing happens on a worker.</summary>
internal sealed class PluginDirectoryWatch : IDisposable {
    private readonly string _directory;
    private readonly Action<Action> _dispatch;
    private readonly Action _changed;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _expiry;
    private int _scanRequested;
    private int _scanRunning;
    private Dictionary<string, string>? _baseline;
    private int _disposed;

    internal bool IsStopped => Volatile.Read(ref _disposed) != 0;
    internal IReadOnlyDictionary<string, string> Baseline => _baseline ?? throw new InvalidOperationException("The watcher has not started.");

    internal PluginDirectoryWatch(string directory, Action<Action> dispatch, Action changed) {
        _directory = directory;
        _dispatch = dispatch;
        _changed = changed;
        }

    internal async Task StartAsync(TimeSpan? duration = null) {
        if (_watcher is not null || Volatile.Read(ref _disposed) != 0) {
            return;
            }
        Directory.CreateDirectory(_directory);
        _baseline = await Task.Run(() => PluginSnapshot.Capture(_directory, _lifetime.Token));
        _lifetime.Token.ThrowIfCancellationRequested();
        _watcher = new FileSystemWatcher(_directory) {
            Filter = "*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += (_, _) => QueueScan();
        _watcher.EnableRaisingEvents = true;
        TimeSpan requested = duration ?? TimeSpan.FromMinutes(15);
        TimeSpan bounded = requested > TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : requested;
        _expiry = new System.Threading.Timer(_ => Dispose(), null, bounded, Timeout.InfiniteTimeSpan);
        QueueScan(); // Close the snapshot-to-watch race.
        }

    private void OnChanged(object sender, FileSystemEventArgs args) {
        if (IsPlugin(args.FullPath)) {
            QueueScan();
            }
        }

    private void OnRenamed(object sender, RenamedEventArgs args) {
        if (IsPlugin(args.FullPath) || IsPlugin(args.OldFullPath)) {
            QueueScan();
            }
        }

    private static bool IsPlugin(string path) {
        return Path.GetExtension(path).Equals(".mnxp", StringComparison.OrdinalIgnoreCase);
        }

    private void QueueScan() {
        if (IsStopped) {
            return;
            }
        Interlocked.Exchange(ref _scanRequested, 1);
        if (Interlocked.CompareExchange(ref _scanRunning, 1, 0) == 0) {
            _ = ScanAsync();
            }
        }

    private async Task ScanAsync() {
        try {
            while (!IsStopped) {
                // Coalesce a write/rename burst into one worker scan, including large file copies.
                await Task.Delay(180, _lifetime.Token);
                Interlocked.Exchange(ref _scanRequested, 0);
                try {
                    Dictionary<string, string> current = await Task.Run(() => PluginSnapshot.Capture(_directory, _lifetime.Token));
                    if (_baseline is not null && PluginSnapshot.Differs(_baseline, current) && !IsStopped) {
                        NotifyChanged();
                        }
                    }
                catch (IOException) {
                    // Atomic replacements can temporarily lock a file. Retry without blocking the UI.
                    Interlocked.Exchange(ref _scanRequested, 1);
                    }
                catch (UnauthorizedAccessException) {
                    // A subsequent filesystem event can retry after permissions change.
                    }
                if (Volatile.Read(ref _scanRequested) == 0) {
                    break;
                    }
                }
            }
        catch (OperationCanceledException) {
            }
        finally {
            Volatile.Write(ref _scanRunning, 0);
            if (Volatile.Read(ref _scanRequested) != 0 && !IsStopped) {
                QueueScan();
                }
            }
        }

    private void NotifyChanged() {
        try {
            _dispatch(() => {
                if (!IsStopped) {
                    _changed();
                    }
                });
            }
        catch (InvalidOperationException) {
            // A form can lose its native handle between the liveness check and BeginInvoke.
            }
        }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
            }
        _lifetime.Cancel();
        _watcher?.Dispose();
        _expiry?.Dispose();
        }
    }
