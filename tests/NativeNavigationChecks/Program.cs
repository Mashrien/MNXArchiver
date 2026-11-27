using System.Reflection;
using System.Security.Cryptography;
using MNX;
using Mash.GUI.MNXArchiver;

string scratch = Path.Combine(Path.GetTempPath(), "mnx-navigation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
try {
    string input = Path.Combine(scratch, "ひらがな and dots.folder");
    string nested = Path.Combine(input, "Nested");
    Directory.CreateDirectory(nested);
    byte[] expected = RandomNumberGenerator.GetBytes(65539);
    File.WriteAllBytes(Path.Combine(nested, "image.data.bin"), expected);
    for (int index = 0; index < 300; index++) {
        File.WriteAllText(Path.Combine(input, "note-" + index + ".txt"), "Distinct item " + index);
        }
    foreach (bool encrypted in new bool[] { false, true }) {
        string archivePath = Path.Combine(scratch, encrypted ? "encrypted.mnx" : "plain.mnx");
        string? password = encrypted ? "navigation-test-only" : null;
        using (Archive writer = new Archive()) {
            writer.Options.Compression = ArchiveCompression.LZ4;
            FileQueueItem[] pending = FileTransfer.PrepareImport(writer, new string[] { input }, writer.RootDirectoryId);
            writer.Create(new FileInfo(archivePath), password);
            FileTransfer.ProcessPreparedImport(writer, pending, scratch, null);
            }
        using Archive archive = new Archive().Open(archivePath, password);
        ArchiveBrowser browser = ArchiveBrowser.FromArchive(archive);
        browser.WarmNavigation(browser.Root);
        ArchiveBrowser.Entry folder = browser.Children(browser.Root).Single();
        browser.WarmNavigation(folder.Id);
        Require(browser.Children(folder.Id).Count == 301, "Requested folder listing lost entries.");
        ArchiveBrowser.Entry subfolder = browser.Children(folder.Id).Single(entry => entry.IsDirectory);
        browser.WarmNavigation(subfolder.Id);
        ArchiveBrowser.Entry payload = browser.Children(subfolder.Id).Single();
        Require(browser.PathOf(payload.Id).EndsWith("Nested/image.data.bin", StringComparison.Ordinal), "Lazy ancestry lookup lost the path.");

        // Selecting both a folder and its child should extract each output once.
        string output = Path.Combine(scratch, encrypted ? "out-encrypted" : "out-plain");
        ExtractionPlan plan = ExtractionPlan.Create(archive, new Guid[] { subfolder.Id, payload.Id }, output);
        Require(plan.Items.Count == 2 && plan.RootPaths.Count == 1, "Selected descendants were duplicated in the extraction plan.");
        using (TransferCancellation cancellation = new TransferCancellation()) {
            plan.Extract(archive, new TransferTracker(archive.ChunkSize), cancellation);
            }
        Require(File.ReadAllBytes(Path.Combine(output, "Nested", "image.data.bin")).SequenceEqual(expected), "Lazy extraction changed payload bytes.");

        string extra = Path.Combine(scratch, "extra.λ.txt");
        File.WriteAllText(extra, "A new item in the selected destination.");
        FileTransfer.Import(archive, new string[] { extra }, subfolder.Id);
        ArchiveBrowser refreshed = ArchiveBrowser.FromArchive(archive);
        refreshed.WarmNavigation(subfolder.Id);
        Require(refreshed.Children(subfolder.Id).Count == 2, "A refreshed browser retained a stale directory listing.");
        bool collision = false;
        try {
            FileTransfer.PrepareImport(archive, new string[] { extra }, subfolder.Id);
            }
        catch (IOException) {
            collision = true;
            }
        Require(collision && archive.FileQueue.Count == 0, "Import collision preflight did not preserve an empty queue.");
        Require(archive.VerifyArchive() == Archive.ReturnCode.Success, "Archive verification failed after browse/import/extract.");

        // A compatibility snapshot intentionally retains every entry. None of
        // these GUI paths should ever request it, even as an incidental lookup.
        FieldInfo snapshot = typeof(Archive).GetField("_files", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new Exception("Update this test if the client snapshot field is renamed.");
        Require(snapshot.GetValue(archive) is null, "GUI navigation or transfers created an archive-wide metadata snapshot.");
        }
    Console.WriteLine("PASS: native paged browse, ancestry, selected-subtree extraction, import collisions and refresh; no full-index snapshots, plain/encrypted.");
    }
finally {
    Directory.Delete(scratch, true);
    }

static void Require(bool condition, string message) {
    if (!condition) {
        throw new Exception(message);
        }
    }
