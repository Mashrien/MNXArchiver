using System.Security.Cryptography;
using System.Text;

namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>An open archive path and its browser listing. The module performs the archive I/O.</summary>
internal sealed class OpenedModuleArchive {
    private readonly NativeArchiveModule _module;
    private readonly PluginFormatVersion _version;
    internal string Path { get; }
    internal PluginFormat Format => _module.Format;
    internal string FormatVersion => _version.Version;
    internal ArchiveBrowser Browser { get; }

    private OpenedModuleArchive(NativeArchiveModule module, PluginFormatVersion version, string path, PluginResponse result) {
        _module = module;
        _version = version;
        Path = path;
        PluginEntry[] files = result.Entries ?? throw new InvalidDataException("The module did not return an archive listing.");
        Browser = BuildBrowser(path, files);
        }

    private static ArchiveBrowser BuildBrowser(string archivePath, PluginEntry[] files) {
        if (files.Length > PluginProtocol.MaxEntries) {
            throw new InvalidDataException("The archive listing has too many entries.");
            }

        Dictionary<string, ArchiveBrowser.Entry> entries = new Dictionary<string, ArchiveBrowser.Entry>(StringComparer.Ordinal);
        foreach (PluginEntry file in files) {
            if (file is null || file.Size < 0) {
                throw new InvalidDataException("The module returned an invalid entry.");
                }
            ValidateEntryPath(file.Path);
            string parent = ParentPath(file.Path);
            ArchiveBrowser.Entry entry = new ArchiveBrowser.Entry(EntryId(file.Path), EntryId(parent),
                System.IO.Path.GetFileName(file.Path), false, (ulong)file.Size, DateTime.MinValue);
            AddEntry(entries, file.Path, entry);
            AddFolderAncestors(entries, parent);
            }

        return ArchiveBrowser.FromEntries(Guid.Empty, System.IO.Path.GetFileName(archivePath), entries.Values);
        }

    private static void AddEntry(Dictionary<string, ArchiveBrowser.Entry> entries, string path, ArchiveBrowser.Entry entry) {
        if (entries.Count >= PluginProtocol.MaxEntries) {
            throw new InvalidDataException("The archive tree has too many entries.");
            }
        if (!entries.TryAdd(path, entry)) {
            throw new InvalidDataException("Duplicate file or folder in archive: " + path);
            }
        }

    private static void AddFolderAncestors(Dictionary<string, ArchiveBrowser.Entry> entries, string path) {
        while (path.Length > 0) {
            if (entries.TryGetValue(path, out ArchiveBrowser.Entry? existing)) {
                if (!existing.IsDirectory) {
                    throw new InvalidDataException("An archive file also appears as a folder: " + path);
                    }
                return;
                }

            string parent = ParentPath(path);
            ArchiveBrowser.Entry folder = new ArchiveBrowser.Entry(EntryId(path), EntryId(parent),
                System.IO.Path.GetFileName(path), true, 0, DateTime.MinValue);
            AddEntry(entries, path, folder);
            path = parent;
            }
        }

    private static string ParentPath(string path) {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "" : path[..separator];
        }

    private static Guid EntryId(string path) {
        return path.Length == 0 ? Guid.Empty : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(path)).AsSpan(0, 16));
        }

    private static void ValidateEntryPath(string path) {
        if (string.IsNullOrEmpty(path) || path.Length > PluginProtocol.MaxPathCharacters) {
            throw new InvalidDataException("The archive entry path is empty or too long.");
            }
        if (path.Any(character => char.IsControl(character) || character is '\\' or ':')) {
            throw new InvalidDataException("The archive entry path contains an invalid character.");
            }

        string[] components = path.Split('/');
        if (components.Length > PluginProtocol.MaxPathDepth) {
            throw new InvalidDataException("The archive entry path has too many levels: " + path);
            }
        foreach (string component in components) {
            if (component.Length == 0 || component.Length > PluginProtocol.MaxPathComponent) {
                throw new InvalidDataException("The archive entry path has an empty or oversized component: " + path);
                }
            if (component is "." or "..") {
                throw new InvalidDataException("The archive entry path contains a relative component: " + path);
                }
            }
        }

    internal bool Supports(PluginActions action) => (_version.Actions & action) == action;

    internal static OpenedModuleArchive Open(PluginMatch match, string path, CancellationToken cancellation = default) {
        path = System.IO.Path.GetFullPath(path);
        PluginResponse result = match.Module.Invoke(new PluginRequest { Operation = "open", ArchivePath = path }, cancellation);
        return new OpenedModuleArchive(match.Module, match.Version, path, result);
        }

    internal static OpenedModuleArchive Create(NativeArchiveModule plugin, string path, string[] sources, bool overwrite, CancellationToken cancellation = default) {
        if ((plugin.Current.Actions & PluginActions.Create) == 0) {
            throw new NotSupportedException("This module cannot create archives.");
            }
        path = System.IO.Path.GetFullPath(path);
        PluginResponse result = plugin.Invoke(new PluginRequest {
            Operation = "create", ArchivePath = path, Sources = sources, Overwrite = overwrite,
            }, cancellation);
        return new OpenedModuleArchive(plugin, plugin.Current, path, result);
        }

    internal OpenedModuleArchive Add(string[] sources, string folder, CancellationToken cancellation = default) {
        RequireAction(PluginActions.Add);
        PluginResponse result = _module.Invoke(new PluginRequest {
            Operation = "add", ArchivePath = Path, Sources = sources, Folder = folder.TrimStart('/'),
            }, cancellation);
        return new OpenedModuleArchive(_module, _version, Path, result);
        }

    internal OpenedModuleArchive Replace(string source, Guid id, CancellationToken cancellation = default) {
        RequireAction(PluginActions.Replace);
        if (Browser.Get(id).IsDirectory) {
            throw new InvalidOperationException("Select an archived file to replace.");
            }
        PluginResponse result = _module.Invoke(new PluginRequest {
            Operation = "replace", ArchivePath = Path, EntryPath = Browser.PathOf(id).TrimStart('/'), Sources = [source],
            }, cancellation);
        return new OpenedModuleArchive(_module, _version, Path, result);
        }

    internal OpenedModuleArchive Delete(Guid[] ids, CancellationToken cancellation = default) {
        RequireAction(PluginActions.Delete);
        HashSet<Guid> visited = new();
        Stack<Guid> pending = new(ids);
        List<string> paths = new();
        while (pending.TryPop(out Guid id)) {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(id)) {
                continue;
                }
            ArchiveBrowser.Entry entry = Browser.Get(id);
            if (entry.IsDirectory) {
                foreach (ArchiveBrowser.Entry child in Browser.Children(id)) {
                    pending.Push(child.Id);
                    }
                }
            else {
                paths.Add(Browser.PathOf(id).TrimStart('/'));
                }
            }
        if (paths.Count == 0) {
            return this;
            }
        PluginResponse result = _module.Invoke(new PluginRequest {
            Operation = "delete", ArchivePath = Path, EntryPaths = paths.ToArray(),
            }, cancellation);
        return new OpenedModuleArchive(_module, _version, Path, result);
        }

    private void RequireAction(PluginActions action) {
        if (!Supports(action)) {
            throw new NotSupportedException($"The archive module does not support {action} for format version {FormatVersion}.");
            }
        }

    internal void ReadEntry(Guid id, byte[] destination, CancellationToken cancellation) {
        RequireAction(PluginActions.ReadEntry);
        ArchiveBrowser.Entry entry = Browser.Get(id);
        if (entry.IsDirectory) {
            throw new InvalidOperationException("Select an archived file to read.");
            }
        if ((ulong)destination.Length > entry.Bytes) {
            throw new EndOfStreamException("The requested read is longer than the archived entry.");
            }

        PluginRequest request = new PluginRequest {
            Operation = "read",
            ArchivePath = Path,
            EntryPath = Browser.PathOf(id).TrimStart('/'),
            EntrySize = checked((long)entry.Bytes)
            };
        _module.ReadEntry(request, destination, cancellation);
        }

    internal PluginExtraction PlanExtraction(Guid[] ids, string destination, bool singleFile) {
        RequireAction(PluginActions.Extract);
        return new PluginExtraction(_module, this, ids, destination, singleFile);
        }
    }
