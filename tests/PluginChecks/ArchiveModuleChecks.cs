using Mash.GUI.MNXArchiver;
using Mash.GUI.MNXArchiver.Plugins;

internal static class ArchiveModuleChecks {
    internal static void Run(string directory, string[] paths) {
        PluginCatalog catalog = PluginCatalog.Discover(directory);
        if (catalog.Issues.Count > 0) {
            throw new InvalidOperationException(catalog.AboutSummary());
            }
        Task worker = Task.Factory.StartNew(() => {
            foreach (string path in paths) {
                Verify(catalog, path);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        worker.GetAwaiter().GetResult();
        }

    private static void Verify(PluginCatalog catalog, string path) {
        PluginMatch match = catalog.Match(path) ?? throw new InvalidDataException("No module matched " + path);
        OpenedModuleArchive archive = OpenedModuleArchive.Open(match, path);
        Guid entry = FindFile(archive.Browser, Guid.Empty);
        ArchiveBrowser.Entry file = archive.Browser.Get(entry);
        byte[] preview = new byte[(int)Math.Min(file.Bytes, 1024)];
        archive.ReadEntry(entry, preview, CancellationToken.None);
        DirectoryInfo output = Directory.CreateTempSubdirectory("mnx-provider-check-");
        try {
            string destination = Path.Combine(output.FullName, "output.bin");
            archive.PlanExtraction([entry], destination, singleFile: true).Extract(overwrite: false);
            using FileStream extracted = File.OpenRead(destination);
            byte[] actual = new byte[preview.Length];
            extracted.ReadExactly(actual);
            if ((ulong)extracted.Length != file.Bytes || !actual.AsSpan().SequenceEqual(preview)) {
                throw new InvalidDataException("Preview and extracted entry disagree for " + path);
                }
            Console.WriteLine($"PASS: {archive.Format.Id} {archive.FormatVersion}, {file.Name}, {file.Bytes} bytes.");
            }
        finally {
            output.Delete(recursive: true);
            }
        }

    private static Guid FindFile(ArchiveBrowser browser, Guid folder) {
        foreach (ArchiveBrowser.Entry entry in browser.Children(folder)) {
            Guid found = entry.IsDirectory ? FindFile(browser, entry.Id) : entry.Id;
            if (found != Guid.Empty) {
                return found;
                }
            }
        if (folder == Guid.Empty) {
            throw new InvalidDataException("Expected a file in the test archive.");
            }
        return Guid.Empty;
        }
    }
