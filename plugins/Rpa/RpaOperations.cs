namespace Mash.UI.MNXFile.GUI.Plugins.Rpa;

internal static class RpaOperations {
    private static readonly object OperationGate = new();

    internal static PluginResponse Run(PluginRequest request, RpaCancellation cancellation) {
        while (!Monitor.TryEnter(OperationGate, millisecondsTimeout: 50))
            cancellation.Check();

        try {
            cancellation.Check();
            string archivePath = RpaPaths.FullFilePath(request.ArchivePath, "archive path");
            return request.Operation switch {
                "open" => Open(archivePath, cancellation),
                "extract" => Extract(archivePath, request, cancellation),
                "create" or "add" or "replace" or "delete" => Edit(archivePath, request, cancellation),
                _ => throw new InvalidDataException($"Unknown RPA operation '{request.Operation}'."),
                };
            }
        finally {
            Monitor.Exit(OperationGate);
            }
        }

    private static PluginResponse Open(string archivePath, RpaCancellation cancellation) {
        using FileStream archive = OpenArchive(archivePath);
        RpaIndex index = RpaIndex.Read(archive, cancellation);
        return new PluginResponse { Entries = index.List(cancellation) };
        }

    private static FileStream OpenArchive(string path, bool editing = false) {
        RpaPaths.RequireFile(path);
        // Linux permits an atomic rename while FileShare.None holds an exclusive advisory lock.
        FileShare sharing = editing && OperatingSystem.IsLinux()
            ? FileShare.None
            : FileShare.Read | FileShare.Delete;
        FileAccess access = editing && OperatingSystem.IsWindows() ? FileAccess.ReadWrite : FileAccess.Read;
        return new FileStream(path, FileMode.Open, access, sharing);
        }

    private static PluginResponse Edit(string archivePath, PluginRequest request, RpaCancellation cancellation) {
        bool creating = request.Operation == "create";
        bool hasOriginal = !creating || RpaPaths.CheckOutput(archivePath, request.Overwrite);

        using FileStream? archive = hasOriginal ? OpenArchive(archivePath, editing: true) : null;
        DateTime originalWriteTime = archive is null ? default : File.GetLastWriteTimeUtc(archive.SafeFileHandle);
        long originalLength = archive?.Length ?? 0;
        RpaIndex original = creating ? new RpaIndex(RpaIndex.DefaultKey) : RpaIndex.Read(archive!, cancellation);
        Dictionary<string, string?> files = PlanEdit(original, archivePath, request, cancellation);

        string parent = Path.GetDirectoryName(archivePath)!;
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"The archive directory '{parent}' does not exist.");

        using StagedFile stage = new(archivePath);
        RpaIndex updated = new(original.Key, original.PickleProtocol);
        using (FileStream output = stage.Open()) {
            output.Position = RpaIndex.HeaderLength;
            byte[] buffer = new byte[RpaIndex.BufferSize];
            foreach ((string path, string? sourcePath) in files) {
                cancellation.Check();
                long offset = output.Position;
                if (sourcePath is not null)
                    CopySource(sourcePath, output, buffer, cancellation);
                else
                    CopyEntry(original.Entries[path], archive!, output, buffer, cancellation);

                long length = output.Position - offset;
                RpaSegment segment = new(offset, length, Array.Empty<byte>());
                updated.Entries.Add(path, new RpaEntry([segment], length));
                }

            updated.Write(output, cancellation);
            if (archive is not null && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(output.SafeFileHandle, File.GetUnixFileMode(archive.SafeFileHandle));
            output.Flush(flushToDisk: true);
            }

        PluginEntry[] entries = updated.List(cancellation);
        cancellation.Check();
        if (archive is not null) {
            RpaPaths.RequireFile(archivePath);
            FileInfo current = new(archivePath);
            if (current.Length != originalLength || current.LastWriteTimeUtc != originalWriteTime)
                throw new IOException("The RPA archive changed while it was being edited.");
            if (OperatingSystem.IsWindows())
                archive.Dispose();
            }

        stage.Commit(overwrite: hasOriginal);
        return new PluginResponse { Entries = entries };
        }

    private static Dictionary<string, string?> PlanEdit(RpaIndex original, string archivePath,
        PluginRequest request, RpaCancellation cancellation) {
        Dictionary<string, string?> files = new(StringComparer.Ordinal);
        foreach (string path in original.Entries.Keys) {
            cancellation.Check();
            files.Add(path, null);
            }

        if (request.Operation == "delete") {
            string[] paths = request.EntryPaths ?? [];
            if (paths.Length == 0 || paths.Length > files.Count)
                throw new InvalidDataException("The number of selected RPA entries to delete is invalid.");

            foreach (string path in paths) {
                cancellation.Check();
                RpaPaths.ValidateEntryPath(path);
                if (!files.Remove(path))
                    throw new InvalidDataException($"The RPA entry '{path}' is missing or already selected.");
                }
            return files;
            }

        string[] sources = request.Sources ?? [];
        if (request.Operation == "replace") {
            RpaPaths.ValidateEntryPath(request.EntryPath);
            string entryPath = request.EntryPath!;
            if (!files.ContainsKey(entryPath))
                throw new InvalidDataException($"The RPA entry '{entryPath}' does not exist.");
            if (sources.Length != 1)
                throw new InvalidDataException("Replacing an RPA entry requires exactly one source file.");
            string source = RpaPaths.FullFilePath(sources[0], "replacement source");
            CheckSourcePath(source, archivePath);
            RpaPaths.RequireFile(source);
            files[entryPath] = source;
            return files;
            }

        string folder = request.Folder ?? "";
        if (folder.Length > 0)
            RpaPaths.ValidateEntryPath(folder);
        int originalCount = files.Count;

        foreach (string sourcePath in sources) {
            cancellation.Check();
            string source = RpaPaths.FullFilePath(sourcePath, "archive source");
            string name = Path.GetFileName(source);
            string entryPath = folder.Length == 0 ? name : folder + "/" + name;
            AddSource(files, source, entryPath, archivePath, cancellation);
            }

        if (request.Operation == "add" && files.Count == originalCount)
            throw new InvalidDataException("Adding to an RPA archive requires at least one source file.");

        RpaIndex.ValidatePaths(files, cancellation);
        return files;
        }

    private static void AddSource(Dictionary<string, string?> files, string source, string entryPath,
        string archivePath, RpaCancellation cancellation) {
        Stack<(string Source, string EntryPath)> pending = new();
        pending.Push((source, entryPath));
        int visited = 0;

        while (pending.TryPop(out (string Source, string EntryPath) item)) {
            cancellation.Check();
            if (++visited > PluginProtocol.MaxEntries * 2)
                throw new InvalidDataException("The RPA source directory contains too many files or folders.");

            CheckSourcePath(item.Source, archivePath);
            RpaPaths.ValidateEntryPath(item.EntryPath);
            FileAttributes attributes = RpaPaths.Inspect(item.Source)
                ?? throw new FileNotFoundException($"The source '{item.Source}' does not exist.");
            if ((attributes & FileAttributes.Directory) != 0) {
                foreach (string child in Directory.EnumerateFileSystemEntries(item.Source)) {
                    cancellation.Check();
                    if (pending.Count >= PluginProtocol.MaxEntries)
                        throw new InvalidDataException("The RPA source directory contains too many files or folders.");
                    pending.Push((child, item.EntryPath + "/" + Path.GetFileName(child)));
                    }
                continue;
                }

            if (files.Count >= PluginProtocol.MaxEntries)
                throw new InvalidDataException("The RPA archive contains too many entries.");
            if (!files.TryAdd(item.EntryPath, item.Source))
                throw new InvalidDataException($"The RPA entry '{item.EntryPath}' already exists.");
            }
        }

    private static void CheckSourcePath(string source, string archivePath) {
        if (RpaPaths.FileSystemComparer.Equals(source, archivePath))
            throw new InvalidDataException("An RPA archive cannot be used as its own source file.");
        }

    private static void CopySource(string sourcePath, FileStream destination, byte[] buffer,
        RpaCancellation cancellation) {
        RpaPaths.RequireFile(sourcePath);
        using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        CopyBytes(source, destination, source.Length, buffer, cancellation);
        if (source.ReadByte() != -1)
            throw new IOException($"The source '{sourcePath}' changed while it was being copied.");
        }

    private static PluginResponse Extract(string archivePath, PluginRequest request, RpaCancellation cancellation) {
        using FileStream archive = OpenArchive(archivePath);
        RpaIndex index = RpaIndex.Read(archive, cancellation);
        Dictionary<string, RpaEntry> outputs = PlanExtraction(archivePath, index, request, cancellation);
        List<StagedFile> staged = new(outputs.Count);
        byte[] buffer = new byte[RpaIndex.BufferSize];

        try {
            foreach ((string path, RpaEntry entry) in outputs) {
                cancellation.Check();
                CheckExtractionDestination(archivePath, path, request.Overwrite);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                CheckExtractionDestination(archivePath, path, request.Overwrite);

                StagedFile stage = new(path);
                staged.Add(stage);
                using FileStream output = stage.Open();
                CopyEntry(entry, archive, output, buffer, cancellation);
                output.Flush(flushToDisk: true);
                }

            foreach (StagedFile stage in staged) {
                cancellation.Check();
                CheckExtractionDestination(archivePath, stage.Destination, request.Overwrite);
                }

            cancellation.Check();
            foreach (StagedFile stage in staged) {
                CheckExtractionDestination(archivePath, stage.Destination, request.Overwrite);
                stage.Commit(request.Overwrite);
                }

            return new PluginResponse();
            }
        finally {
            foreach (StagedFile stage in staged)
                stage.Dispose();
            }
        }

    private static Dictionary<string, RpaEntry> PlanExtraction(string archivePath, RpaIndex index,
        PluginRequest request, RpaCancellation cancellation) {
        PluginOutput[] requested = request.Outputs ?? [];
        if (requested.Length == 0 || requested.Length > PluginProtocol.MaxEntries)
            throw new InvalidDataException("The number of requested RPA outputs is invalid.");

        Dictionary<string, RpaEntry> outputs = new(RpaPaths.FileSystemComparer);
        foreach (PluginOutput output in requested) {
            cancellation.Check();
            if (output is null)
                throw new InvalidDataException("An RPA extraction output is missing.");
            RpaPaths.ValidateEntryPath(output.EntryPath);
            if (!index.Entries.TryGetValue(output.EntryPath, out RpaEntry? entry))
                throw new InvalidDataException($"The RPA entry '{output.EntryPath}' does not exist.");

            string path = RpaPaths.FullFilePath(output.Destination, "extraction destination");
            if (!outputs.TryAdd(path, entry))
                throw new InvalidDataException($"The extraction destination '{path}' was requested more than once.");
            CheckExtractionDestination(archivePath, path, request.Overwrite);
            }

        foreach (string path in outputs.Keys) {
            cancellation.Check();
            string? ancestor = Path.GetDirectoryName(path);
            while (ancestor is not null) {
                if (outputs.ContainsKey(ancestor))
                    throw new InvalidDataException($"The extraction destination '{path}' conflicts with '{ancestor}'.");
                ancestor = Path.GetDirectoryName(ancestor);
                }
            }

        return outputs;
        }

    private static void CheckExtractionDestination(string archivePath, string destination, bool overwrite) {
        if (RpaPaths.FileSystemComparer.Equals(archivePath, destination)
            || (RpaPaths.CheckOutput(destination, overwrite) && RpaPaths.HasSameFileIdentity(archivePath, destination)))
            throw new InvalidDataException("An extraction destination cannot overwrite the RPA archive.");
        }

    private static void CopyEntry(RpaEntry entry, FileStream archive, FileStream destination,
        byte[] buffer, RpaCancellation cancellation) {
        foreach (RpaSegment segment in entry.Segments) {
            cancellation.Check();
            for (int offset = 0; offset < segment.Prefix.Length;) {
                cancellation.Check();
                int count = Math.Min(segment.Prefix.Length - offset, buffer.Length);
                destination.Write(segment.Prefix, offset, count);
                offset += count;
                }

            archive.Position = segment.Offset;
            CopyBytes(archive, destination, segment.Length, buffer, cancellation);
            }
        }

    private static void CopyBytes(FileStream source, FileStream destination, long remaining,
        byte[] buffer, RpaCancellation cancellation) {
        while (remaining > 0) {
            cancellation.Check();
            int count = (int)Math.Min(remaining, buffer.Length);
            source.ReadExactly(buffer.AsSpan(0, count));
            destination.Write(buffer, 0, count);
            remaining -= count;
            }
        }
    }
