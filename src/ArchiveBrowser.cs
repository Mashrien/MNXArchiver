using MNX;

namespace Mash.GUI.MNXArchiver;
/// <summary>Metadata only. Browsing never opens file payload streams.</summary>
internal sealed class ArchiveBrowser {
    public sealed record Entry(Guid Id, Guid Parent, string Name, bool IsDirectory, ulong Bytes, DateTime CreatedUtc, uint Chunks = 0);
    private readonly Dictionary<Guid, Entry>? _entries;
    private readonly Dictionary<Guid, Entry[]>? _children;
    private readonly ArchiveNavigationCache? _navigation;
    private readonly Func<Guid, bool>? _hasFolders;
    public Guid Root { get; }
    public string Title { get; }
    public bool Compressed { get; }
    public bool Encrypted { get; }

    private ArchiveBrowser(Guid root, string title, IEnumerable<Entry> entries, bool compressed, bool encrypted) {
        Root = root;
        Title = title;
        Compressed = compressed;
        Encrypted = encrypted;
        _entries = entries.ToDictionary(e => e.Id);
        _children = _entries.Values.GroupBy(e => e.Parent).ToDictionary(
            g => g.Key,
            g => g.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray()
            );
        }

    private ArchiveBrowser(Archive archive) {
        Root = archive.RootDirectoryId;
        Title = Path.GetFileName(archive.ArchivePath!);
        Compressed = archive.Compression != ArchiveCompression.None;
        Encrypted = archive.IsEncrypted;
        _navigation = new ArchiveNavigationCache(Root,
            id => ReadEntry(archive, id),
            id => ReadChildren(archive, id)
            );
        _hasFolders = archive.HasSubdirectories;
        }

    public static ArchiveBrowser FromArchive(Archive archive) {
        // Do not access archive.Files: that compatibility property intentionally
        // creates a complete snapshot and would defeat the paged native index.
        return new ArchiveBrowser(archive);
        }

    private static Entry FromFile(VFile file) {
        return new Entry(file.FileID, file.OwnerID, file.Name, file.IsDirectory,
            file.SizeOriginal, file.CreationDateUtc, file.ChunkCount);
        }

    private static Entry ReadEntry(Archive archive, Guid id) {
        VFile? file = archive.GetEntry(id);
        if (file is null) {
            throw new KeyNotFoundException("Archive entry not found: " + id);
            }
        return FromFile(file);
        }

    private static IReadOnlyList<Entry> ReadChildren(Archive archive, Guid parent) {
        return archive.DirectoryList(parent)
            .OrderByDescending(file => file.IsDirectory)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .Select(FromFile)
            .ToArray();
        }
    internal static ArchiveBrowser FromEntries(Guid root, string title, IEnumerable<Entry> entries) =>
        new ArchiveBrowser(root, title, entries, compressed: false, encrypted: false);

    public IReadOnlyList<Entry> Children(Guid id) {
        if (_navigation is not null) {
            return _navigation.Children(id);
            }
        return _children!.GetValueOrDefault(id) ?? Array.Empty<Entry>();
        }

    public Entry Get(Guid id) {
        if (_navigation is not null) {
            return _navigation.Get(id);
            }
        return _entries![id];
        }

    public void WarmNavigation(Guid current) {
        _navigation?.WarmNavigation(current);
        }
    public Guid Parent(Guid id) => id == Root ? Root : Get(id).Parent;
    public bool HasFolders(Guid id) {
        if (_hasFolders is not null) {
            return _hasFolders(id);
            }
        return Children(id).Any(entry => entry.IsDirectory);
        }
    public IEnumerable<Guid> Ancestors(Guid id) {
        Stack<Guid> chain = new Stack<Guid>();
        HashSet<Guid> visited = new HashSet<Guid>();
        while (id != Root) {
            if (!visited.Add(id)) {
                throw new InvalidDataException("The archive contains a directory parent cycle.");
                }
            chain.Push(id);
            id = Parent(id);
            }

        chain.Push(Root);
        return chain;
        }

    public string PathOf(Guid id) => id == Root ? "/" : "/" + string.Join("/", Ancestors(id).Skip(1).Select(g => Get(g).Name));
    public Guid Resolve(string path) {
        if (!path.StartsWith('/')) {
            throw new ArgumentException("Archive paths start with /.");
            }

        Guid current = Root;
        foreach (string component in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (component == ".") {
                continue;
                }

            if (component == "..") {
                current = Parent(current);
                continue;
                }

            Entry[]? matches = Children(current).Where(e => e.IsDirectory && string.Equals(e.Name, component, StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0) {
                matches = Children(current).Where(e => e.IsDirectory && string.Equals(e.Name, component, StringComparison.OrdinalIgnoreCase)).ToArray();
                }

            if (matches.Length != 1) {
                throw new ArgumentException($"Folder not found or ambiguous: {component}");
                }

            current = matches[0].Id;
            }

        return current;
        }

    public static ArchiveBrowser Preview() {
        Guid root = Guid.NewGuid();
        List<Entry> entries = new List<Entry>();
        DateTime created = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        Guid Add(Guid parent, string name, bool folder, ulong size = 0) {
            Guid id = Guid.NewGuid();
            entries.Add(new Entry(id, parent, name, folder, size, created));
            return id;
            }

        Guid docs = Add(root, "Documents", true);
        Guid projects = Add(root, "Projects", true);
        Guid photos = Add(root, "Photos", true);
        Guid source = Add(projects, "MNXFile", true);
        Guid holiday = Add(photos, "Holiday", true);
        Add(docs, "Notes.txt", false, 2674);
        Add(docs, "Archive design.pdf", false, 284016);
        Add(source, "MainWindow.ui", false, 14592);
        Add(source, "MainWindow.cs", false, 12618);
        Add(holiday, "Mountain.jpg", false, 6480172);
        Add(root, "README.txt", false, 1846);
        Add(root, "Model.safetensors", false, 6843023360);
        Add(root, "Backup manifest.json", false, 128017);
        return new(root, "Preview.mnx", entries, true, false);
        }

    public static string FileType(Entry entry) {
        if (entry.IsDirectory) {
            return "Folder";
            }
        string extension = Path.GetExtension(entry.Name);
        return extension.Length > 1 ? extension.Substring(1).ToUpperInvariant() : "File";
        }

    public static string HumanChunks(uint chunks) {
        if (chunks < 1000) {
            return chunks.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            }
        string[] units = { "k", "M", "G" };
        double count = chunks;
        int unit = -1;
        while (count >= 1000 && unit < units.Length - 1) {
            count /= 1000;
            unit++;
            }
        count = Math.Round(count, 1);
        if (count >= 1000 && unit < units.Length - 1) {
            count /= 1000;
            unit++;
            }
        return count.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " " + units[unit];
        }

    public static string HumanSize(ulong bytes) {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) {
            value /= 1024;
            unit++;
            }

        return $"{value:0.##} {units[unit]}";
        }
    }
