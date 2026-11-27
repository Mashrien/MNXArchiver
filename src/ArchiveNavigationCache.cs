using Entry = Mash.GUI.MNXArchiver.ArchiveBrowser.Entry;

namespace Mash.GUI.MNXArchiver;

/// <summary>Keep the active folder and its parent's immediate listings warm.</summary>
internal sealed class ArchiveNavigationCache {
    private const int MaximumDirectories = 8;
    private const int MaximumHistoryEntries = 4096;
    private const ulong MaximumHistoryBytes = 4 * 1024 * 1024;
    private const int MaximumIndividualEntries = 512;
    private readonly Guid _root;
    private readonly Func<Guid, Entry> _readEntry;
    private readonly Func<Guid, IReadOnlyList<Entry>> _readChildren;
    private readonly Dictionary<Guid, CachedDirectory> _directories = new Dictionary<Guid, CachedDirectory>();
    private readonly Dictionary<Guid, Entry> _residentEntries = new Dictionary<Guid, Entry>();
    private readonly Dictionary<Guid, CachedEntry> _individualEntries = new Dictionary<Guid, CachedEntry>();
    private readonly object _cacheLock = new object();
    private Guid _current;
    private Guid _parent;
    private Guid? _previous;
    private ulong _lastUse;

    internal ArchiveNavigationCache(Guid root, Func<Guid, Entry> readEntry, Func<Guid, IReadOnlyList<Entry>> readChildren) {
        _root = root;
        _readEntry = readEntry;
        _readChildren = readChildren;
        _current = root;
        _parent = root;
        }

    internal int CachedDirectoryCount {
        get {
            lock (_cacheLock) {
                return _directories.Count;
                }
            }
        }

    internal void WarmNavigation(Guid current) {
        lock (_cacheLock) {
            Guid parent = current == _root ? _root : Get(current).Parent;
            if (current != _current) {
                _previous = _current;
                }
            _current = current;
            _parent = parent;
            Children(parent);
            if (current != parent) {
                Children(current);
                }
            TrimDirectoryHistory();
            }
        }

    internal Entry Get(Guid id) {
        lock (_cacheLock) {
            if (_residentEntries.TryGetValue(id, out Entry? resident)) {
                return resident;
                }
            if (_individualEntries.TryGetValue(id, out CachedEntry? cached)) {
                cached.LastUse = ++_lastUse;
                return cached.Entry;
                }
            Entry entry = _readEntry(id);
            _individualEntries[id] = new CachedEntry(entry, ++_lastUse);
            if (_individualEntries.Count > MaximumIndividualEntries) {
                Guid oldest = _individualEntries.OrderBy(item => item.Value.LastUse).First().Key;
                _individualEntries.Remove(oldest);
                }
            return entry;
            }
        }

    internal IReadOnlyList<Entry> Children(Guid parent) {
        lock (_cacheLock) {
            if (_directories.TryGetValue(parent, out CachedDirectory? cached)) {
                cached.LastUse = ++_lastUse;
                return cached.Children;
                }
            // The source performs a direct directory lookup. No archive-wide
            // enumeration or payload stream is involved in filling this window.
            IReadOnlyList<Entry> children = _readChildren(parent);
            _directories[parent] = new CachedDirectory(children, ++_lastUse);
            foreach (Entry entry in children) {
                _residentEntries[entry.Id] = entry;
                }
            TrimDirectoryHistory();
            return children;
            }
        }

    private void TrimDirectoryHistory() {
        while (true) {
            ulong historyEntries = 0;
            ulong historyBytes = 0;
            Guid? oldest = null;
            ulong oldestUse = ulong.MaxValue;
            foreach (KeyValuePair<Guid, CachedDirectory> item in _directories) {
                if (item.Key == _current || item.Key == _parent) {
                    continue;
                    }
                historyEntries += (ulong)item.Value.Children.Count;
                historyBytes += item.Value.EstimatedBytes;
                // Prefer the last-visited directory over older history, but do
                // not let it exceed the optional history memory budget.
                if (item.Key != _previous && item.Value.LastUse < oldestUse) {
                    oldest = item.Key;
                    oldestUse = item.Value.LastUse;
                    }
                }
            if (_directories.Count <= MaximumDirectories && historyEntries <= MaximumHistoryEntries && historyBytes <= MaximumHistoryBytes) {
                return;
                }
            if (oldest is null && _previous is Guid previous && previous != _current && previous != _parent && _directories.ContainsKey(previous)) {
                oldest = previous;
                }
            if (oldest is null) {
                return;
                }
            foreach (Entry entry in _directories[oldest.Value].Children) {
                _residentEntries.Remove(entry.Id);
                }
            _directories.Remove(oldest.Value);
            }
        }

    private sealed class CachedDirectory {
        internal IReadOnlyList<Entry> Children { get; }
        internal ulong LastUse { get; set; }
        internal ulong EstimatedBytes { get; }
        internal CachedDirectory(IReadOnlyList<Entry> children, ulong lastUse) {
            Children = children;
            LastUse = lastUse;
            ulong bytes = 0;
            foreach (Entry child in children) {
                // Include references, IDs, numeric metadata and UTF-16 names.
                bytes = checked(bytes + 128UL + (ulong)child.Name.Length * 2);
                }
            EstimatedBytes = bytes;
            }
        }

    private sealed class CachedEntry {
        internal Entry Entry { get; }
        internal ulong LastUse { get; set; }
        internal CachedEntry(Entry entry, ulong lastUse) {
            Entry = entry;
            LastUse = lastUse;
            }
        }
    }
