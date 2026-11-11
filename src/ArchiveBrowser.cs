using MNX;

namespace Mash.GUI.MNXArchiver;
/// <summary>Metadata only. Browsing never opens file payload streams.</summary>
internal sealed class ArchiveBrowser {
    public sealed record Entry(Guid Id, Guid Parent, string Name, bool IsDirectory, ulong Bytes, DateTime CreatedUtc, uint Chunks = 0);
    private readonly Dictionary<Guid, Entry> _entries;
    private readonly Dictionary<Guid, Entry[]> _children;
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

    public static ArchiveBrowser FromArchive(Archive archive) => new(
        archive.RootDirectoryId,
        Path.GetFileName(archive.ArchivePath!),
        archive.Files.Select(f => new Entry(f.FileID, f.OwnerID, f.Name, f.IsDirectory, f.SizeOriginal, f.CreationDateUtc, f.ChunkCount)),
        archive.Compression != ArchiveCompression.None,
        archive.IsEncrypted
        );
    internal static ArchiveBrowser FromEntries(Guid root, string title, IEnumerable<Entry> entries) =>
        new ArchiveBrowser(root, title, entries, compressed: false, encrypted: false);

    public IReadOnlyList<Entry> Children(Guid id) => _children.GetValueOrDefault(id) ?? [];
    public Entry Get(Guid id) => _entries[id];
    public Guid Parent(Guid id) => id == Root ? Root : Get(id).Parent;
    public bool HasFolders(Guid id) => Children(id).Any(e => e.IsDirectory);
    public IEnumerable<Guid> Ancestors(Guid id) {
        Stack<Guid> chain = new Stack<Guid>();
        while (id != Root) {
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
