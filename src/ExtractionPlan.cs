using static MNX.Archive;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

internal sealed record ExtractionItem(Guid Id, string RelativePath, string Destination, bool IsDirectory, long Length);
/// <summary>Metadata for a single extraction batch; file bytes stay inside MNX.</summary>
internal sealed class ExtractionPlan {
    internal IReadOnlyList<ExtractionItem> Items { get; }
    internal IReadOnlyList<string> RootPaths { get; }

    private ExtractionPlan(List<ExtractionItem> items, List<string> rootPaths) {
        Items = items;
        RootPaths = rootPaths;
        }

    internal static ExtractionPlan Create(Archive archive, IEnumerable<Guid> selection, string directory, string? singleFileDestination = null) {
        directory = Path.GetFullPath(directory);
        HashSet<Guid> selected = selection.ToHashSet();
        List<ExtractionItem> items = new List<ExtractionItem>();
        List<string> rootPaths = new List<string>();
        StringComparer? comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string> outputPaths = new HashSet<string>(comparison);
        foreach (Guid id in selected) {
            VFile entry = ReadEntry(archive, id);
            Guid ancestor = entry.OwnerID;
            bool coveredByParent = false;
            HashSet<Guid> ancestors = new HashSet<Guid>();
            while (ancestor != archive.RootDirectoryId && ancestor != Guid.Empty) {
                if (!ancestors.Add(ancestor)) {
                    throw new InvalidDataException("The archive contains a directory parent cycle.");
                    }
                if (selected.Contains(ancestor)) {
                    coveredByParent = true;
                    break;
                    }

                ancestor = ReadEntry(archive, ancestor).OwnerID;
                }

            if (coveredByParent) {
                continue;
                }

            string rootPath = singleFileDestination ?? Path.Combine(directory, entry.Name);
            rootPaths.Add(Path.GetFullPath(rootPath));
            Stack<(VFile Entry, string Path, string Relative)> pending = new Stack<(VFile Entry, string Path, string Relative)>();
            HashSet<Guid> visited = new HashSet<Guid>();
            pending.Push((entry, rootPath, entry.Name));
            while (pending.Count > 0) {
                (VFile Entry, string Path, string Relative) current = pending.Pop();
                if (!visited.Add(current.Entry.FileID)) {
                    throw new InvalidDataException("The archive contains a repeated or cyclic directory entry.");
                    }
                string path = Path.GetFullPath(current.Path);
                Archive.ValidateDestinationName(Path.GetFileName(path));
                if (!outputPaths.Add(path)) {
                    throw new IOException("Selected entries share an extraction destination: " + path);
                    }

                if (comparison.Equals(path, archive.ArchivePath)) {
                    throw new IOException("An archived entry would overwrite the archive itself: " + path);
                    }

                items.Add(new ExtractionItem(
                    current.Entry.FileID,
                    current.Relative,
                    path,
                    current.Entry.IsDirectory,
                    checked((long)current.Entry.SizeOriginal)
                    ));
                if (current.Entry.IsDirectory) {
                    // Only the selected subtrees are needed to plan this transfer.
                    IReadOnlyList<VFile> descendants = archive.DirectoryList(current.Entry.FileID);
                    for (int index = descendants.Count - 1; index >= 0; index--) {
                        VFile child = descendants[index];
                        pending.Push((child, Path.Combine(path, child.Name), Path.Combine(current.Relative, child.Name)));
                        }
                    }
                }
            }

        return new ExtractionPlan(items, rootPaths);
        }

    private static VFile ReadEntry(Archive archive, Guid id) {
        VFile? entry = archive.GetEntry(id);
        if (entry is null) {
            throw new FileNotFoundException("Archive entry not found: " + id);
            }
        return entry;
        }

    internal string[] ExistingFiles() {
        List<string> existing = new List<string>();
        foreach (ExtractionItem item in Items) {
            if (DiskFile.Exists(item.Destination) || Directory.Exists(item.Destination)) {
                FileAttributes attributes = DiskFile.GetAttributes(item.Destination);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    throw new IOException("Extraction cannot replace or follow a destination link: " + item.Destination);
                    }

                bool directory = attributes.HasFlag(FileAttributes.Directory);
                if (directory != item.IsDirectory) {
                    throw new IOException("A file/folder conflicts with this extraction destination: " + item.Destination);
                    }

                if (!directory) {
                    existing.Add(item.Destination);
                    }
                }
            }

        return existing.ToArray();
        }

    internal string[] Extract(Archive archive, TransferTracker tracker, TransferCancellation cancellation, bool overwrite = false) {
        tracker.Configure(Items);
        foreach (ExtractionItem item in Items) {
            cancellation.CheckBoundary();
            tracker.Start(item.Id, item.RelativePath);
            if (item.IsDirectory) {
                Directory.CreateDirectory(item.Destination);
                }
            else {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
                ReturnCode result = archive.ExtractFile(item.Id, new FileInfo(item.Destination), overwrite);
                if (result != ReturnCode.Success) {
                    throw new IOException("Extraction failed: " + result);
                    }
                }

            tracker.Finish(item.Id, item.IsDirectory);
            cancellation.CheckBoundary();
            }

        List<string> uris = new List<string>();
        foreach (string path in RootPaths) {
            uris.Add(new Uri(path).AbsoluteUri);
            }

        return uris.ToArray();
        }
    }
