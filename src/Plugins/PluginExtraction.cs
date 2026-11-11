namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>Map selected archive paths to explicit local outputs; the module copies the bytes.</summary>
internal sealed class PluginExtraction {
    private readonly ArchivePlugin _module;
    private readonly string _archivePath;
    private readonly PluginOutput[] _outputs;
    internal IReadOnlyList<string> RootPaths { get; }

    internal PluginExtraction(ArchivePlugin module, PluginArchive archive, Guid[] ids, string destination, bool singleFile) {
        _module = module;
        _archivePath = archive.Path;
        ArchiveBrowser browser = archive.Browser;
        HashSet<Guid> selected = ids.ToHashSet();
        if (singleFile && (selected.Count != 1 || browser.Get(selected.Single()).IsDirectory)) {
            throw new ArgumentException("A single output file requires exactly one archive file.");
            }

        destination = Path.GetFullPath(destination);
        StringComparer comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string> outputPaths = new(comparison);
        List<PluginOutput> outputs = new();
        List<string> rootPaths = new();
        foreach (Guid id in selected) {
            if (browser.Ancestors(id).SkipLast(1).Any(selected.Contains)) {
                continue;
                }
            ArchiveBrowser.Entry entry = browser.Get(id);
            ValidateDestinationName(singleFile ? Path.GetFileName(destination) : entry.Name);
            string rootPath = singleFile ? destination : Path.Combine(destination, entry.Name);
            rootPaths.Add(rootPath);
            Stack<(ArchiveBrowser.Entry Entry, string Output)> pending = new();
            pending.Push((entry, rootPath));
            while (pending.TryPop(out (ArchiveBrowser.Entry Entry, string Output) current)) {
                if (!outputPaths.Add(current.Output)) {
                    throw new IOException("Selected entries share an extraction destination: " + current.Output);
                    }
                if (comparison.Equals(current.Output, _archivePath)) {
                    throw new IOException("Extraction cannot overwrite the archive itself.");
                    }
                if (!current.Entry.IsDirectory) {
                    outputs.Add(new PluginOutput(browser.PathOf(current.Entry.Id).TrimStart('/'), current.Output));
                    continue;
                    }
                foreach (ArchiveBrowser.Entry child in browser.Children(current.Entry.Id)) {
                    ValidateDestinationName(child.Name);
                    pending.Push((child, Path.Combine(current.Output, child.Name)));
                    }
                }
            }
        _outputs = outputs.ToArray();
        RootPaths = rootPaths;
        }

    private static void ValidateDestinationName(string name) {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            throw new IOException("This operating system cannot extract the preserved filename: " + name);
            }
        if (OperatingSystem.IsWindows()) {
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            bool reserved = stem is "CON" or "PRN" or "AUX" or "NUL"
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                    && (stem[3] is >= '0' and <= '9' or '¹' or '²' or '³');
            if (reserved || name.EndsWith(' ') || name.EndsWith('.')) {
                throw new IOException("Windows cannot extract this name without changing it: " + name);
                }
            }
        }

    internal string[] ExistingFiles() {
        List<string> existing = new();
        foreach (PluginOutput output in _outputs) {
            FileAttributes attributes;
            try {
                attributes = File.GetAttributes(output.Destination);
                }
            catch (FileNotFoundException) {
                continue;
                }
            catch (DirectoryNotFoundException) {
                continue;
                }
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) {
                throw new IOException("A folder or link conflicts with this extraction destination: " + output.Destination);
                }
            existing.Add(output.Destination);
            }
        return existing.ToArray();
        }

    internal void Extract(bool overwrite, CancellationToken cancellation = default) {
        if (_outputs.Length > 0) {
            _module.Invoke(new PluginRequest { Operation = "extract", ArchivePath = _archivePath, Outputs = _outputs, Overwrite = overwrite }, cancellation);
            }
        }
    }
