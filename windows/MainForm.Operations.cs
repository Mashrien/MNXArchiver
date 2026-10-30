using Mash.IO.Format.MNX;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive.Resume;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;
using VirtualFile = Mash.IO.Format.MNX.VFile.VFile;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private async Task<T> RunOperation<T>(string title, Func<T> operation, ArchiveFile? target = null, TransferTracker? tracker = null, TransferCancellation? cancellation = null, bool extraction = false, string? displayPath = null, bool showProgress = true, bool encrypted = false) {
        if (_busy) {
            throw new InvalidOperationException("An archive operation is already running.");
            }
        _busy = true;
        CancelHover();
        UpdateActions();
        ArchiveFile? archive = target ?? _archive;
        CancellationToken previous = archive?.Options.CancellationToken ?? CancellationToken.None;
        OperationProgressArgs? progress = null;
        void Report(object? sender, OperationProgressArgs args) {
            tracker?.Update(args);
            Volatile.Write(ref progress, args);
            }
        if (archive is not null) {
            archive.ProgressReport += Report;
            if (cancellation is not null) {
                archive.Options.CancellationToken = cancellation.Token;
                }
            }
        string codec = archive?.Compression.ToString() ?? "None";
        if (archive?.Compression == ArchiveCompression.Brotli) {
            codec += ", level " + archive.CompressionLevel;
            }
        using ProgressForm dialog = new ProgressForm(title, displayPath ?? archive?.ArchivePath ?? "MNX archive", codec, archive?.IsEncrypted == true || encrypted, tracker, cancellation, extraction, target is null);
        using System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) => {
            OperationProgressArgs? current = Volatile.Read(ref progress);
            dialog.UpdateProgress(current);
            operationProgress.Style = current is null ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            operationProgress.Value = current is null || current.BytesMax <= 0 ? 0 : (int)Math.Clamp((double)current.BytesComplete / current.BytesMax * 100, 0, 100);
            };
        statusMessage.Text = title;
        operationProgress.Visible = true;
        bool previouslyVisible = Visible;
        try {
            if (showProgress) {
                Hide();
                dialog.Show(this);
                }
            timer.Start();
            T result = await Task.Run(operation);
            dialog.UpdateProgress(Volatile.Read(ref progress));
            return result;
            }
        finally {
            timer.Stop();
            dialog.AllowClose = true;
            dialog.Close();
            if (archive is not null) {
                archive.ProgressReport -= Report;
                archive.Options.CancellationToken = previous;
                }
            _busy = false;
            operationProgress.Visible = false;
            if (previouslyVisible && !_closing) {
                Show();
                Activate();
                }
            UpdateActions();
            }
        }

    private async Task ChooseArchive() {
        using OpenFileDialog chooser = new OpenFileDialog { Title = "Open MNX archive", Filter = "MNX archives (*.mnx)|*.mnx|All files (*.*)|*.*" };
        if (chooser.ShowDialog(this) == DialogResult.OK) {
            await OpenArchive(chooser.FileName);
            }
        }

    internal async Task OpenInputs(IEnumerable<string> inputs) {
        string[] paths = FileTransfer.LocalPaths(inputs);
        if (paths.Length == 0 || _busy) {
            return;
            }
        if (paths.Length == 1 && (Path.GetExtension(paths[0]).Equals(".mnx", StringComparison.OrdinalIgnoreCase) || FileTransfer.IsArchive(paths[0]))) {
            await OpenArchive(paths[0]);
            }
        else if (paths.Length == 1 && DiskFile.Exists(paths[0]) && (Path.GetExtension(paths[0]).Equals(".mnq", StringComparison.OrdinalIgnoreCase) || RemainingQueue.IsQueue(new FileInfo(paths[0])))) {
            await ResumeFromQueue(paths[0]);
            }
        else {
            await CreateFromInputs(paths);
            }
        }

    private async Task ChooseInputs(bool add) {
        using InputSelectionForm selection = new InputSelectionForm(add ? "Add files and folders" : "Create archive from files and folders");
        if (selection.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        string[] paths = FileTransfer.LocalPaths(selection.Paths);
        if (paths.Length == 0) {
            return;
            }
        if (add && _archive is not null) {
            await ImportFiles(paths, _current);
            }
        else {
            await CreateFromInputs(paths);
            }
        }

    private async Task CreateFromInputs(string[] paths) {
        string? password = _settings.Flag("Defaults", "Encrypt") ? await Task.Run(() => new DefaultKeyStore(_settings).Read()) : null;
        using CreationForm dialog = new CreationForm(paths, _settings, password);
        if (dialog.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        await CreateArchive(paths, dialog.SelectedSettings!);
        }

    internal async Task CreateArchive(string[] paths, CreationSettings settings, Action<TransferCancellation, ArchiveFile>? onCancellationReady = null) {
        string staging = Path.Combine(Path.GetDirectoryName(settings.Path)!, ".mnx-pending-" + Guid.NewGuid().ToString("N"));
        using ArchiveFile next = new ArchiveFile();
        next.Options.ProgressReportFrequency = 50;
        next.Options.Compression = settings.Compression;
        next.Options.CompressionLevel = settings.Level;
        next.Options.OverrideByDefault = false;
        using TransferCancellation cancellation = new TransferCancellation();
        TransferTracker tracker = new TransferTracker(next.Options.ChunkSize);
        try {
            bool complete = await RunOperation("Creating archive", () => {
                onCancellationReady?.Invoke(cancellation, next);
                FileQueueItem[] pending = FileTransfer.PrepareImport(next, paths, next.RootDirectoryId);
                tracker.Configure(pending);
                next.Create(new FileInfo(staging), settings.Password);
                bool finished = true;
                try {
                    FileTransfer.ProcessPreparedImport(next, pending, Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(paths[0]))!, tracker, cancellation);
                    cancellation.CheckBoundary();
                    }
                catch (OperationCanceledException) when (cancellation.Keep) {
                    finished = false;
                    }
                next.Close();
                DiskFile.Move(staging, settings.Path, settings.Overwrite);
                if (!finished) {
                    RemainingQueue.Save(settings.Path, next.FileQueue, cancellation.ExportKeyFile ? settings.Password : null);
                    }
                return finished;
                }, next, tracker, cancellation, displayPath: settings.Path, encrypted: settings.Password is not null);
            await OpenArchive(settings.Path, settings.Password);
            statusMessage.Text = complete ? "Archive created." : "Stopped after the current file. Completed files kept; matching .mnq saved alongside the archive.";
            }
        finally {
            next.Close();
            DiskFile.Delete(staging);
            }
        }

    private async Task ImportFiles(string[] paths, Guid parent) {
        ArchiveFile archive = _archive!;
        string archivePath = archive.ArchivePath!;
        using TransferCancellation cancellation = new TransferCancellation();
        TransferTracker tracker = new TransferTracker(archive.ChunkSize);
        try {
            await RunOperation("Adding files", () => {
                FileTransfer.Import(archive, paths, parent, tracker, cancellation);
                return true;
                }, tracker: tracker, cancellation: cancellation);
            statusMessage.Text = $"Added {paths.Length:N0} input item(s).";
            }
        catch (OperationCanceledException) {
            if (cancellation.Delete) {
                DeleteCancelledArchive(archivePath);
                return;
                }
            statusMessage.Text = "Stopped after the current file. Completed files kept; matching .mnq saved alongside the archive.";
            }
        finally {
            if (_archive is not null) {
                RefreshArchive(parent);
                }
            }
        }

    internal async Task<bool> ExtractArchive(string path, bool extractHere) {
        if (!await OpenArchive(path)) {
            return false;
            }
        string destination = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (!extractHere) {
            using FolderBrowserDialog chooser = new FolderBrowserDialog { Description = "Extract archive to folder", InitialDirectory = destination, UseDescriptionForTitle = true };
            if (chooser.ShowDialog(this) != DialogResult.OK) {
                return false;
                }
            destination = chooser.SelectedPath;
            }
        await ExtractSelection(_browser!.Children(_browser.Root).Select(entry => entry.Id).ToArray(), destination);
        return true;
        }

    private async Task ChooseExtraction(Guid[] ids) {
        if (_archive is null || ids.Length == 0) {
            return;
            }
        bool singleFile = ids.Length == 1 && !_browser!.Get(ids[0]).IsDirectory;
        if (singleFile) {
            using SaveFileDialog chooser = new SaveFileDialog { Title = "Extract file", FileName = _browser!.Get(ids[0]).Name, Filter = "All files (*.*)|*.*", OverwritePrompt = true };
            if (chooser.ShowDialog(this) == DialogResult.OK) {
                await ExtractSelection(ids, chooser.FileName, true, true);
                }
            }
        else {
            using FolderBrowserDialog chooser = new FolderBrowserDialog { Description = "Extract to folder", UseDescriptionForTitle = true };
            if (chooser.ShowDialog(this) == DialogResult.OK) {
                await ExtractSelection(ids, chooser.SelectedPath);
                }
            }
        }

    private async Task ExtractSelection(Guid[] ids, string destination, bool singleFile = false, bool overwrite = false) {
        ExtractionPlan plan = ExtractionPlan.Create(_archive!, ids, singleFile ? Path.GetDirectoryName(destination)! : destination, singleFile ? destination : null);
        string[] existing = plan.ExistingFiles();
        if (existing.Length > 0 && !overwrite) {
            if (MessageBox.Show(this, $"Replace {existing.Length:N0} existing file(s)?", "Replace files", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                return;
                }
            overwrite = true;
            }
        await RunExtraction(plan, overwrite);
        }

    private async Task<bool> RunExtraction(ExtractionPlan plan, bool overwrite = false) {
        ArchiveFile archive = _archive!;
        TransferTracker tracker = new TransferTracker(archive.ChunkSize);
        using TransferCancellation cancellation = new TransferCancellation();
        try {
            await RunOperation("Extracting files", () => plan.Extract(archive, tracker, cancellation, overwrite), tracker: tracker, cancellation: cancellation, extraction: true);
            statusMessage.Text = "Extraction complete.";
            return true;
            }
        catch (OperationCanceledException) {
            statusMessage.Text = cancellation.Keep ? "Stopped after the current file. Completed files were kept." : "Stopped immediately. The unfinished output was deleted; completed files were kept.";
            return false;
            }
        }

    private async Task DeleteEntries(Guid[] ids, bool confirmed = false) {
        if (_archive is null || ids.Length == 0) {
            return;
            }
        if (!confirmed && MessageBox.Show(this, $"Delete {ids.Length:N0} selected archive entries and their descendants?\nThis cannot be undone. Source files on disk are unaffected.", "Delete archive entries", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
            return;
            }
        ArchiveFile archive = _archive;
        Guid location = _current;
        foreach (Guid ancestor in _browser!.Ancestors(_current)) {
            if (ids.Contains(ancestor)) {
                location = _browser.Parent(ancestor);
                break;
                }
            }
        try {
            await RunOperation("Deleting archive entries", () => {
                foreach (Guid id in ids) {
                    if (archive.GetFile(id) is not null) {
                        RequireSuccess(archive.DeleteFileFromArchive(id), "Deleting entry");
                        }
                    }
                return true;
                });
            }
        finally {
            RefreshArchive(location);
            }
        statusMessage.Text = "Entries deleted. Compact the archive to reclaim free space.";
        }

    private async Task VerifyArchive() {
        ArchiveFile archive = _archive!;
        await RunOperation("Verifying archive", () => { RequireSuccess(archive.VerifyArchive(), "Verification"); return true; });
        statusMessage.Text = "Archive verified successfully.";
        }

    private async Task CompactArchive() {
        ArchiveFile archive = _archive!;
        Guid location = _current;
        await RunOperation("Compacting archive", () => { RequireSuccess(archive.CompactArchive(), "Compaction"); return true; });
        RefreshArchive(location);
        statusMessage.Text = "Archive compacted.";
        }

    private static void RequireSuccess(ArchiveFile.ReturnCode result, string action) {
        if (result != ArchiveFile.ReturnCode.Success) {
            throw new IOException(action + " failed: " + result);
            }
        }

    private async Task ChooseResume() {
        using OpenFileDialog chooser = new OpenFileDialog { Title = "Resume remaining queue", Filter = "MNX remaining queue (*.mnq)|*.mnq|All files (*.*)|*.*" };
        if (chooser.ShowDialog(this) == DialogResult.OK) {
            await ResumeFromQueue(chooser.FileName);
            }
        }

    internal async Task ResumeFromQueue(string path) {
        FileInfo queueFile = new FileInfo(path);
        QueuePayload queue = await Task.Run(() => RemainingQueue.Read(queueFile));
        string archivePath = RemainingQueue.LocateArchive(queueFile, queue);
        bool alreadyOpen = _archive?.ArchiveGuid == queue.Archive.ArchiveId && string.Equals(_archive.ArchivePath, archivePath, StringComparison.OrdinalIgnoreCase);
        string? password = alreadyOpen ? null : await Task.Run(() => ResumePassword.Read(queueFile, archivePath));
        if (!alreadyOpen && !await OpenArchive(archivePath, password)) {
            return;
            }
        ArchiveFile archive = _archive!;
        bool previousOverwrite = archive.Options.OverrideByDefault;
        archive.Options.OverrideByDefault = false;
        using TransferCancellation cancellation = new TransferCancellation();
        TransferTracker tracker = new TransferTracker(archive.ChunkSize);
        try {
            bool complete = await RunOperation("Resuming archive", () => {
                RequireSuccess(archive.LoadRemainingQueue(queueFile), "Loading remaining queue");
                archive.ConsumeResumeKey(queueFile);
                FileQueueItem[] pending = archive.FileQueue.ToArray();
                tracker.Configure(pending);
                try {
                    FileTransfer.ProcessPreparedImport(archive, pending, queueFile.DirectoryName!, tracker, cancellation);
                    }
                catch (OperationCanceledException) when (cancellation.Keep) {
                    archive.SaveRemainingQueue(cancellation.ExportKeyFile);
                    return false;
                    }
                return true;
                }, tracker: tracker, cancellation: cancellation);
            if (complete) {
                RemainingQueue.MarkCompleted(queueFile);
                }
            statusMessage.Text = complete ? "Archive resume complete. Finished queue retained as a completed record." : "Stopped after the current file. Remaining queue updated.";
            }
        catch (OperationCanceledException) {
            if (cancellation.Delete) {
                DeleteCancelledArchive(archivePath, queueFile);
                }
            }
        finally {
            if (_archive == archive) {
                archive.Options.OverrideByDefault = previousOverwrite;
                foreach (FileQueueItem item in archive.FileQueue) {
                    archive.Queue_RemoveFromQueue(item.ID);
                    }
                RefreshArchive(archive.RootDirectoryId);
                }
            }
        }

    private void DeleteCancelledArchive(string path, FileInfo? queue = null) {
        CloseArchive();
        RemainingQueue.Discard(queue ?? new FileInfo(Path.ChangeExtension(path, ".mnq")));
        DiskFile.Delete(path);
        statusMessage.Text = "Cancelled. The partial archive was deleted.";
        }

    private void ShowProperties(Guid[] ids) {
        if (_archive is null || _busy) {
            return;
            }
        string text;
        if (ids.Length == 1) {
            VirtualFile entry = _archive.GetFile(ids[0])!;
            text = $"{_browser!.PathOf(entry.FileID)}\n\nType: {(entry.IsDirectory ? "Directory" : "File")}\nSize: {entry.SizeOriginal:N0} bytes\nCreated: {entry.CreationDateUtc.ToLocalTime():G}\nModified: {entry.ModificationDateUtc.ToLocalTime():G}\nAccessed: {entry.AccessDateUtc.ToLocalTime():G}\nAttributes: {entry.Attributes}\nID: {entry.FileID}\nChunks: {entry.ChunkCount:N0}\nPhysical extents: {entry.ExtentCount:N0}";
            }
        else {
            ArchiveInfo info = _archive.GetArchiveInfo();
            text = $"{info.Path}\n\nArchive ID: {info.ArchiveGuid}\nSize on disk: {info.SizeOnDisk:N0} bytes\nFiles: {info.FileCount:N0}\nDirectories: {info.DirectoryCount:N0}\nChunks: {info.ChunkCount:N0}\nFront index chunks: {info.IndexChunkCount:N0}\nFree chunks: {info.FreeChunkCount:N0}\nFree ranges: {info.FreeRangeCount:N0}";
            if (ids.Length > 1) {
                text += $"\nSelected entries: {ids.Length:N0}\nIndexed bytes: {ids.Aggregate(0UL, (total, id) => total + _browser!.Get(id).Bytes):N0}";
                }
            }
        text += $"\nCompression: {_archive.Compression}\nEncryption: {_archive.IsEncrypted}\nFormat: MNX{_archive.FormatVersion}";
        TextPrompt.ShowText(this, "Properties", text);
        }
    }
