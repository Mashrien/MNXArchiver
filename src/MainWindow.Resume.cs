using Gtk;
using MNX;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainWindow {
    private void ChooseResumeQueue() {
        if (_operationActive || _pluginArchive is not null) {
            return;
            }

        using FileChooserDialog chooser = new FileChooserDialog(
            "Resume from remaining queue",
            _window,
            FileChooserAction.Open,
            "Cancel",
            ResponseType.Cancel,
            "Resume",
            ResponseType.Accept
            );

        GtkWindowAppearance.Apply(chooser);
        FileFilter filter = new FileFilter {
            Name = "MNX remaining queues (*.mnq)"
            };
        filter.AddPattern("*.mnq");
        chooser.AddFilter(filter);
        string? path = (ResponseType)chooser.Run() == ResponseType.Accept ? chooser.Filename : null;
        chooser.Hide();
        if (path is not null) {
            ResumeFromQueue(path);
            }
        }

    private void ResumeFromQueue(string path) {
        if (_operationActive) {
            return;
            }

        FileInfo queueFile = new FileInfo(path);
        Archive? archive = null;
        string? archivePath = null;
        bool overwrite = false;
        using TransferCancellation cancellation = new TransferCancellation();
        try {
            QueuePayload? queue = RemainingQueue.Read(queueFile);
            archivePath = RemainingQueue.LocateArchive(queueFile, queue);
            // The optional key is bound to the archive and queue. Without it,
            // OpenArchive asks for the original password as before.
            bool alreadyOpen = _archive?.ArchiveGuid == queue.Archive.ArchiveId && string.Equals(
                _archive.ArchivePath,
                archivePath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
                );
            string? password = alreadyOpen ? null : ResumePassword.Read(queueFile, archivePath);
            if (!alreadyOpen && !OpenArchive(archivePath, password)) {
                return;
                }

            archive = _archive!;
            overwrite = archive.Options.OverrideByDefault;
            archive.Options.OverrideByDefault = false;
            TransferTracker tracker = new TransferTracker(archive.ChunkSize);
            bool complete = RunTransfer("Resuming archive", () => {
                archive.LoadRemainingQueue(queueFile);
                archive.ConsumeResumeKey(queueFile);
                FileQueueItem[]? pending = archive.FileQueue.ToArray();
                tracker.Configure(pending);
                try {
                    FileTransfer.ProcessPreparedImport(archive, pending, queueFile.DirectoryName!, tracker, cancellation);
                    }
                catch (OperationCanceledException) when (cancellation.Keep) {
                    archive.SaveRemainingQueue(cancellation.ExportKeyFile);
                    return false;
                    }

                return true;
                }, tracker, cancellation: cancellation);
            if (complete) {
                RemainingQueue.MarkCompleted(queueFile);
                }

            RefreshArchive(archive.RootDirectoryId);
            _status.Text = complete ? "Archive resume complete. The finished queue has been retained as a completed record." : "Resume stopped after the current file. The remaining queue has been updated.";
            }
        catch (OperationCanceledException) {
            if (cancellation.Delete && archivePath is not null) {
                DeleteCancelledArchive(archivePath, queueFile);
                }
            else if (archive is not null) {
                RefreshArchive(archive.RootDirectoryId);
                _status.Text = "Resume stopped. Completed files have been retained.";
                }
            }
        catch (Exception ex) {
            if (archive is not null) {
                RefreshArchive(archive.RootDirectoryId);
                }

            Message("Could not resume archive", ex.Message, MessageType.Error);
            }
        finally {
            if (archive is not null) {
                archive.Options.OverrideByDefault = overwrite;
                foreach (FileQueueItem item in archive.FileQueue) {
                    archive.Queue_RemoveFromQueue(item.ID);
                    }
                }
            }
        }
    }
