using Mash.GUI.MNXArchiver;
using Entry = Mash.GUI.MNXArchiver.ArchiveBrowser.Entry;

Guid root = Guid.NewGuid();
Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
Dictionary<Guid, Entry[]> folders = new Dictionary<Guid, Entry[]>();
Dictionary<Guid, int> reads = new Dictionary<Guid, int>();
List<Entry> rootChildren = new List<Entry>();
for (int index = 0; index < 40; index++) {
    Guid id = Guid.NewGuid();
    Entry directory = new Entry(id, root, "Folder " + index, true, 0, DateTime.UnixEpoch);
    Entry file = new Entry(Guid.NewGuid(), id, "file-" + index + ".txt", false, 5_000_000_000, DateTime.UnixEpoch);
    entries.Add(id, directory);
    entries.Add(file.Id, file);
    folders.Add(id, new Entry[] { file });
    rootChildren.Add(directory);
    }
folders.Add(root, rootChildren.ToArray());
ArchiveNavigationCache cache = new ArchiveNavigationCache(root,
    id => entries[id],
    id => {
        reads[id] = reads.GetValueOrDefault(id) + 1;
        return folders[id];
        }
    );
Require(reads.Count == 0, "Constructing a browser cache must not enumerate the archive.");
Guid first = rootChildren[0].Id;
Guid second = rootChildren[1].Id;
cache.WarmNavigation(first);
Require(reads.Count == 2 && reads[first] == 1 && reads[root] == 1, "Initial navigation must read only current and parent directories.");
Entry firstFile = cache.Children(first)[0];
Require(cache.Get(firstFile.Id).Bytes == 5_000_000_000, "Cached file byte lengths must retain values above 4 GiB.");
cache.WarmNavigation(root);
cache.WarmNavigation(first);
Require(reads[root] == 1 && reads[first] == 1, "Back/up and returning to the current directory must reuse the warm listings.");
cache.WarmNavigation(second);
Require(reads[second] == 1 && reads[root] == 1, "Sibling navigation must reuse the parent listing.");
cache.Children(first);
Require(reads[first] == 1, "The single last-visited directory should stay warm when the history budget permits.");
for (int index = 2; index < rootChildren.Count; index++) {
    cache.WarmNavigation(rootChildren[index].Id);
    }
Require(cache.CachedDirectoryCount <= 8, "Directory history must be bounded rather than retaining every visited folder.");
Require(reads[root] == 1, "The pinned parent listing must survive history eviction.");
cache.Children(first);
Require(reads[first] == 2, "Old directory contents should be evicted after navigation moves elsewhere.");
Guid last = rootChildren[rootChildren.Count - 1].Id;
int lastReads = reads[last];
cache.Children(last);
Require(reads[last] == lastReads, "The current directory must remain pinned while older history is reloaded.");
Guid largeDirectory = Guid.NewGuid();
entries.Add(largeDirectory, new Entry(largeDirectory, root, "Large", true, 0, DateTime.UnixEpoch));
List<Entry> longNames = new List<Entry>();
for (int index = 0; index < 3000; index++) {
    longNames.Add(new Entry(Guid.NewGuid(), largeDirectory, new string('x', 1024) + index, false, 1, DateTime.UnixEpoch));
    }
folders[largeDirectory] = longNames.ToArray();
cache.WarmNavigation(largeDirectory);
cache.WarmNavigation(last);
cache.Children(largeDirectory);
Require(reads[largeDirectory] == 2, "An oversized last-visited listing must respect the optional history byte budget.");
Require(reads[root] == 1 && reads[last] == lastReads, "History eviction must not discard the pinned parent/current listings.");
Console.WriteLine("PASS: current/parent/sibling navigation, lazy directory reads, 64-bit lengths and bounded history eviction.");

static void Require(bool condition, string message) {
    if (!condition) {
        throw new Exception(message);
        }
    }
