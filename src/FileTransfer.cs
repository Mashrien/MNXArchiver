using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive.Resume;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX.Tools;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;
/// <summary>Translate file-manager URIs and perform disk transfers through MNX.</summary>
internal static class FileTransfer {
    internal static bool IsArchiveDrop(string[] paths) {
        return paths.Length == 1 && DiskFile.Exists(paths[0]) &&
            (Path.GetExtension(paths[0]).Equals(".mnx", StringComparison.OrdinalIgnoreCase) || IsArchive(paths[0]));
        }

    public static string LocalPath(string value) {
        if (Path.IsPathFullyQualified(value)) {
            return Path.GetFullPath(value);
            }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) {
            if (!uri.IsFile) {
                throw new ArgumentException("Drop local files or mounted network files, not web URLs.");
                }

            if (!OperatingSystem.IsWindows() && uri.Host.Length > 0 && uri.Host != "localhost") {
                throw new ArgumentException("Mount the network folder before dropping its files.");
                }

            return Path.GetFullPath(uri.LocalPath);
            }

        return Path.GetFullPath(value);
        }

    public static string[] LocalPaths(IEnumerable<string> uris) {
        StringComparer? comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        List<string> paths = new List<string>();
        foreach (string value in uris) {
            if (string.IsNullOrEmpty(value)) {
                continue;
                }

            string path = LocalPath(value);
            if (FileSystemTools.IsLink(new FileInfo(path))) {
                continue;
                }

            if (!DiskFile.Exists(path) && !Directory.Exists(path)) {
                throw new FileNotFoundException("Dropped path does not exist.", path);
                }

            if (!paths.Contains(path, comparer)) {
                paths.Add(path);
                }
            }

        // A selected parent folder already includes its selected descendants.
        return paths.Where(path => !paths.Any(parent => parent != path && Directory.Exists(parent) && path.StartsWith(
            Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
            ))).ToArray();
        }

    public static bool IsArchive(string path) {
        return ArchiveFile.IsArchive(new FileInfo(path));
        }

    internal static FileQueueItem[] PrepareImport(ArchiveFile archive, IEnumerable<string> paths, Guid parent) {
        HashSet<Guid>? before = archive.FileQueue.Select(item => item.ID).ToHashSet();
        try {
            foreach (string path in paths) {
                archive.Options.CancellationToken.ThrowIfCancellationRequested();
                ArchiveFile.ReturnCode result = Directory.Exists(path) ? archive.Queue_AddDirectory(new DirectoryInfo(path), parent) : archive.Queue_AddFile(new FileInfo(path), parent);
                if (result != ArchiveFile.ReturnCode.Success) {
                    throw new IOException($"Could not queue {path}: {result}");
                    }
                }

            FileQueueItem[]? pending = archive.FileQueue.Where(item => !before.Contains(item.ID)).ToArray();
            HashSet<(Guid OwnerID, string Name)>? names = archive.Files.Select(item => (item.OwnerID, item.Name)).ToHashSet();
            foreach (FileQueueItem item in pending) {
                archive.Options.CancellationToken.ThrowIfCancellationRequested();
                if (!names.Add((item.Parent, item.Name))) {
                    throw new IOException($"More than one input has the archive name {item.Name} in the same folder, or that name already exists. No files were added.");
                    }

                if (!item.IsDirectory) {
                    // Check access before committing any entries. Never rewrite names.
                    using FileStream input = DiskFile.Open(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    _ = input.Length;
                    }
                }

            return pending;
            }
        catch {
            ClearImportQueue(archive, before);
            throw;
            }
        }

    public static void Import(
        ArchiveFile archive,
        IEnumerable<string> paths,
        Guid parent,
        TransferTracker? tracker = null,
        TransferCancellation? cancellation = null
        ) {
        string[] inputs = paths.ToArray();
        HashSet<Guid>? before = archive.FileQueue.Select(item => item.ID).ToHashSet();
        bool overwrite = archive.Options.OverrideByDefault;
        archive.Options.OverrideByDefault = false;
        try {
            FileQueueItem[] pending = PrepareImport(archive, inputs, parent);
            tracker?.Configure(pending);
            ProcessPreparedImport(
                archive,
                pending,
                Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(inputs[0]))!,
                tracker,
                cancellation
                );
            }
        catch (OperationCanceledException) when (cancellation?.Keep == true) {
            archive.SaveRemainingQueue(cancellation.ExportKeyFile);
            throw;
            }
        finally {
            archive.Options.OverrideByDefault = overwrite;
            ClearImportQueue(archive, before);
            }
        }

    internal static void ProcessPreparedImport(
        ArchiveFile archive,
        FileQueueItem[] pending,
        string baseDirectory,
        TransferTracker? tracker,
        TransferCancellation? cancellation = null
        ) {
        int index = 0;
        // The queue contains exactly this batch; never silently process unrelated work.
        if (archive.FileQueue.Count != pending.Length) {
            throw new InvalidOperationException("Finish the existing archive queue before importing another batch.");
            }

        ArchiveFile.ReturnCode result = archive.ProcessAddQueue((_, _, completed, _) => {
            if (completed == 0) {
                cancellation?.CheckBoundary();
                tracker?.Start(pending[index], baseDirectory);
                }
            else {
                tracker?.Finish(pending[index]);
                index++;
                cancellation?.CheckBoundary();
                }

            return false;
            });
        cancellation?.CheckBoundary();
        if (result != ArchiveFile.ReturnCode.Success) {
            throw new IOException("Archive import failed: " + result);
            }
        }

    private static void ClearImportQueue(ArchiveFile archive, HashSet<Guid> keep) {
        foreach (FileQueueItem item in archive.FileQueue) {
            if (!keep.Contains(item.ID)) {
                archive.Queue_RemoveFromQueue(item.ID);
                }
            }
        }

    public static string[] Export(ArchiveFile archive, IReadOnlyCollection<Guid> ids, string directory) {
        ExtractionPlan? plan = ExtractionPlan.Create(archive, ids, directory);
        string[] existing = plan.ExistingFiles();
        if (existing.Length > 0) {
            throw new IOException("Extraction destination already exists: " + existing[0]);
            }

        TransferTracker tracker = new TransferTracker(archive.ChunkSize);
        using TransferCancellation cancellation = new TransferCancellation();
        return plan.Extract(archive, tracker, cancellation);
        }
    }

/// <summary>Keep drag files alive after the drop; the receiving app copies asynchronously.</summary>
internal sealed class DragFiles : IDisposable {
    private readonly List<FileStream> _leases = [];
    private readonly string _root;
    public DragFiles(string? temporaryRoot = null) {
        _root = Path.Combine(
            temporaryRoot ?? Path.GetTempPath(),
            "mnx-gui",
            "drag-files"
            );
        if (OperatingSystem.IsWindows()) {
            Directory.CreateDirectory(_root);
            }
        else {
            Directory.CreateDirectory(_root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

        foreach (string directory in Directory.EnumerateDirectories(_root, "transfer-*")) {
            try {
                if (DiskFile.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint) || Directory.GetLastWriteTimeUtc(directory) > DateTime.UtcNow.AddDays(-1)) {
                    continue;
                    }

                string marker = Path.Combine(directory, ".mnx-drag-lease");
                if (!DiskFile.Exists(marker)) {
                    continue;
                    }

                using (DiskFile.Open(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                // An active GUI holds this lease, so only abandoned transfers are removed.
                    }

                Directory.Delete(directory, true);
                }
            catch (IOException) {
            // Another GUI or file manager is still using this transfer.
                }
            catch (UnauthorizedAccessException) {
            // Leave files alone if their ownership or permissions have changed.
                }
            }
        }

    public string CreateDirectory() {
        string directory = Path.Combine(_root, "transfer-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) {
            Directory.CreateDirectory(directory);
            }
        else {
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

        _leases.Add(DiskFile.Open(
            Path.Combine(directory, ".mnx-drag-lease"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None
            ));
        string payload = Path.Combine(directory, "files");
        Directory.CreateDirectory(payload);
        return payload;
        }

    public void Dispose() {
        foreach (FileStream lease in _leases) {
            lease.Dispose();
            }

        _leases.Clear();
    // Do not delete here: a receiving file manager may still be copying.
        }
    }
