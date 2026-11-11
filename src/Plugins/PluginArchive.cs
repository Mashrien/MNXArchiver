using System.Security.Cryptography;
using System.Text;

namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>One metadata snapshot. The module owns every archive read and write.</summary>
internal sealed class PluginArchive {
    private readonly ArchivePlugin _module;
    private readonly PluginFormatVersion _version;
    internal string Path { get; }
    internal PluginFormat Format => _module.Format;
    internal string FormatVersion => _version.Version;
    internal ArchiveBrowser Browser { get; }

    private PluginArchive(ArchivePlugin module, PluginFormatVersion version, string path, PluginResponse result) {
        _module = module;
        _version = version;
        Path = path;
        PluginEntry[] files = result.Entries ?? throw new InvalidDataException("The module did not return an archive listing.");
        if (files.Length > PluginProtocol.MaxEntries) {
            throw new InvalidDataException("The archive listing has too many entries.");
            }
        Dictionary<string, ArchiveBrowser.Entry> entries = new(StringComparer.Ordinal);
        foreach (PluginEntry file in files) {
            if (file is null || file.Size < 0) {
                throw new InvalidDataException("The module returned an invalid entry.");
                }
            ValidateEntryPath(file.Path);
            string parent = ParentPath(file.Path);
            ArchiveBrowser.Entry entry = new(EntryId(file.Path), EntryId(parent), System.IO.Path.GetFileName(file.Path),
                false, (ulong)file.Size, DateTime.MinValue);
            if (!entries.TryAdd(file.Path, entry)) {
                throw new InvalidDataException("Duplicate file or folder in archive: " + file.Path);
                }
            while (parent.Length > 0) {
                if (entries.TryGetValue(parent, out ArchiveBrowser.Entry? existing)) {
                    if (!existing.IsDirectory) {
                        throw new InvalidDataException("An archive file also appears as a folder: " + parent);
                        }
                    break;
                    }
                if (entries.Count >= PluginProtocol.MaxEntries) {
                    throw new InvalidDataException("The archive tree has too many entries.");
                    }
                string next = ParentPath(parent);
                entries.Add(parent, new ArchiveBrowser.Entry(EntryId(parent), EntryId(next), System.IO.Path.GetFileName(parent),
                    true, 0, DateTime.MinValue));
                parent = next;
                }
            if (entries.Count > PluginProtocol.MaxEntries) {
                throw new InvalidDataException("The archive tree has too many entries.");
                }
            }
        Browser = ArchiveBrowser.FromEntries(Guid.Empty, System.IO.Path.GetFileName(path), entries.Values);
        }

    private static string ParentPath(string path) {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "" : path[..separator];
        }

    private static Guid EntryId(string path) {
        return path.Length == 0 ? Guid.Empty : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(path)).AsSpan(0, 16));
        }

    private static void ValidateEntryPath(string path) {
        if (string.IsNullOrEmpty(path) || path.Length > PluginProtocol.MaxPathCharacters || path.Any(character => char.IsControl(character) || character is '\\' or ':')) {
            throw new InvalidDataException("Invalid archive entry path.");
            }
        string[] components = path.Split('/');
        if (components.Length > PluginProtocol.MaxPathDepth || components.Any(component => component.Length is 0 or > PluginProtocol.MaxPathComponent || component is "." or "..")) {
            throw new InvalidDataException("Invalid archive entry path: " + path);
            }
        }

    internal bool Supports(PluginActions action) => (_version.Actions & action) == action;

    internal static PluginArchive Open(PluginMatch match, string path, CancellationToken cancellation = default) {
        path = System.IO.Path.GetFullPath(path);
        PluginResponse result = match.Module.Invoke(new PluginRequest { Operation = "open", ArchivePath = path }, cancellation);
        return new PluginArchive(match.Module, match.Version, path, result);
        }

    internal static PluginArchive Create(ArchivePlugin plugin, string path, string[] sources, bool overwrite, CancellationToken cancellation = default) {
        if ((plugin.Current.Actions & PluginActions.Create) == 0) {
            throw new NotSupportedException("This module cannot create archives.");
            }
        path = System.IO.Path.GetFullPath(path);
        PluginResponse result = plugin.Invoke(new PluginRequest {
            Operation = "create", ArchivePath = path, Sources = sources, Overwrite = overwrite,
            }, cancellation);
        return new PluginArchive(plugin, plugin.Current, path, result);
        }

    internal PluginArchive Add(string[] sources, string folder, CancellationToken cancellation = default) {
        RequireAction(PluginActions.Add);
        PluginResponse result = _module.Invoke(new PluginRequest {
            Operation = "add", ArchivePath = Path, Sources = sources, Folder = folder.TrimStart('/'),
            }, cancellation);
        return new PluginArchive(_module, _version, Path, result);
        }

    internal PluginArchive Replace(string source, Guid id, CancellationToken cancellation = default) {
        RequireAction(PluginActions.Replace);
        if (Browser.Get(id).IsDirectory) {
            throw new InvalidOperationException("Select an archived file to replace.");
            }
        PluginResponse result = _module.Invoke(new PluginRequest {
            Operation = "replace", ArchivePath = Path, EntryPath = Browser.PathOf(id).TrimStart('/'), Sources = [source],
            }, cancellation);
        return new PluginArchive(_module, _version, Path, result);
        }

    internal PluginArchive Delete(Guid[] ids, CancellationToken cancellation = default) {
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
        return new PluginArchive(_module, _version, Path, result);
        }

    private void RequireAction(PluginActions action) {
        if (!Supports(action)) {
            throw new NotSupportedException($"The archive module does not support {action} for format version {FormatVersion}.");
            }
        }

    internal PluginExtraction PlanExtraction(Guid[] ids, string destination, bool singleFile) {
        RequireAction(PluginActions.Extract);
        return new PluginExtraction(_module, this, ids, destination, singleFile);
        }
    }
