using System.Globalization;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;

namespace Mash.UI.MNXFile.GUI;

internal sealed class TransferSnapshot {
    internal string File { get; set; } = "Scanning input files…";
    internal long FileBytes { get; set; } = 0;
    internal long FileTotal { get; set; } = 0;
    internal long Bytes { get; set; } = 0;
    internal long Total { get; set; } = 0;
    internal long Pieces { get; set; } = 0;
    internal long TotalPieces { get; set; } = 0;
    internal int Files { get; set; } = 0;
    internal int TotalFiles { get; set; } = 0;
    internal bool Scanning { get; set; } = true;

    internal TransferSnapshot Copy() {
        return (TransferSnapshot)MemberwiseClone();
        }
    }

/// <summary>One worker publishes snapshots; GTK reads them without entering the archive lock.</summary>
internal sealed class TransferTracker {
    private readonly object _gate = new();
    private TransferSnapshot _snapshot = new TransferSnapshot();
    private long _finishedBytes;
    private long _finishedPieces;
    private readonly Dictionary<Guid, long> _sizes = new();
    private readonly int _chunkSize;
    internal TransferTracker(int chunkSize) {
        _chunkSize = chunkSize;
        }

    internal TransferSnapshot Snapshot {
        get {
            lock (_gate) {
                return _snapshot;
                }
            }
        }

    internal void Configure(IReadOnlyList<FileQueueItem> items) {
        long bytes = 0;
        long pieces = 0;
        int files = 0;
        foreach (FileQueueItem item in items) {
            _sizes[item.ID] = checked((long)item.Length);
            if (!item.IsDirectory) {
                bytes = checked(bytes + _sizes[item.ID]);
                pieces = checked(pieces + Pieces(_sizes[item.ID]));
                files++;
                }
            }

        lock (_gate) {
            TransferSnapshot nextSnapshot = _snapshot.Copy();
            nextSnapshot.Total = bytes;
            nextSnapshot.TotalPieces = pieces;
            nextSnapshot.TotalFiles = files;
            nextSnapshot.Scanning = false;
            _snapshot = nextSnapshot;
            }
        }

    internal void Configure(IReadOnlyList<ExtractionItem> items) {
        long bytes = 0;
        long pieces = 0;
        int files = 0;
        foreach (ExtractionItem item in items) {
            _sizes[item.Id] = item.Length;
            if (!item.IsDirectory) {
                bytes = checked(bytes + item.Length);
                pieces = checked(pieces + Pieces(item.Length));
                files++;
                }
            }

        lock (_gate) {
            TransferSnapshot nextSnapshot = _snapshot.Copy();
            nextSnapshot.File = "Preparing extraction…";
            nextSnapshot.Total = bytes;
            nextSnapshot.TotalPieces = pieces;
            nextSnapshot.TotalFiles = files;
            nextSnapshot.Scanning = false;
            _snapshot = nextSnapshot;
            }
        }

    internal void Start(FileQueueItem item, string baseDirectory) {
        Start(item.ID, Path.GetRelativePath(baseDirectory, item.SourcePath));
        }

    internal void Start(Guid id, string displayName) {
        lock (_gate) {
            TransferSnapshot nextSnapshot = _snapshot.Copy();
            nextSnapshot.File = displayName;
            nextSnapshot.FileBytes = 0;
            nextSnapshot.FileTotal = _sizes[id];
            _snapshot = nextSnapshot;
            }
        }

    internal void Update(OperationProgressArgs args) {
        lock (_gate) {
            long done = Math.Clamp(args.BytesComplete, 0, _snapshot.FileTotal);
            TransferSnapshot nextSnapshot = _snapshot.Copy();
            nextSnapshot.FileBytes = done;
            nextSnapshot.Bytes = _finishedBytes + done;
            nextSnapshot.Pieces = _finishedPieces + Pieces(done);
            _snapshot = nextSnapshot;
            }
        }

    internal void Finish(FileQueueItem item) {
        Finish(item.ID, item.IsDirectory);
        }

    internal void Finish(Guid id, bool directory) {
        lock (_gate) {
            long size = _sizes[id];
            if (!directory) {
                _finishedBytes = checked(_finishedBytes + size);
                _finishedPieces = checked(_finishedPieces + Pieces(size));
                }

            TransferSnapshot nextSnapshot = _snapshot.Copy();
            nextSnapshot.FileBytes = size;
            nextSnapshot.Bytes = _finishedBytes;
            nextSnapshot.Pieces = _finishedPieces;
            nextSnapshot.Files = _snapshot.Files + (directory ? 0 : 1);
            _snapshot = nextSnapshot;
            }
        }

    internal long Pieces(long bytes) {
        return bytes / _chunkSize + (bytes % _chunkSize == 0 ? 0 : 1);
        }

    internal static string PieceCounts(long done, long total) {
        double scale = total > 1000 ? 1000 : 1;
        string format = scale == 1 ? "N0" : "#,##0.###";
        return (done / scale).ToString(format, CultureInfo.InvariantCulture) + " / " + (total / scale).ToString(format, CultureInfo.InvariantCulture) + (scale == 1 ? " Ch." : " K Ch.");
        }

    internal static string ByteCounts(long done, long total) {
        double scale = 1;
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        int unit = 0;
        while (total / scale >= 1024 && unit < units.Length - 1) {
            scale *= 1024;
            unit++;
            }

        return (done / scale).ToString("#,##0.##", CultureInfo.InvariantCulture) + " / " + (total / scale).ToString("#,##0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
