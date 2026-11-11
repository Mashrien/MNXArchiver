using static MNX.Archive;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Gtk;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

internal static class CancellationSmoke {
    internal static void Run(MainWindow host, string work, string screenshots) {
        string current = Path.Combine(work, "cancel.current.with.dots.bin");
        string later = Path.Combine(work, "cancel.later.txt");
        byte[] contents = new byte[16 * 1024 * 1024];
        new Random(97).NextBytes(contents);
        DiskFile.WriteAllBytes(current, contents);
        DiskFile.WriteAllText(later, "This queued file must never start after cancellation.");
        string waiting = Path.Combine(work, "waiting.directory");
        Directory.CreateDirectory(waiting);
        DiskFile.WriteAllText(Path.Combine(waiting, new string ('é', 100) + ".notes"), "Exact queued Unicode name.");
        string output = Path.Combine(work, "cancel.keep.mnx");
        DriveChoice(host, [current, later, waiting], output, keep: true, creating: true, screenshots);
        Check(DiskFile.Exists(output), "Keep publishes the partial archive");
        using (Archive archive = new Archive().Open(output)) {
            Check(
                archive.Files.Count == 1 && archive.Files.Single().Name == Path.GetFileName(current),
                "Keep finishes the current file and never starts the next one"
                );
            Check(
                archive.ReadFileRange(archive.Files.Single().FileID, 0, contents.Length).SequenceEqual(contents) && archive.VerifyArchive() == ReturnCode.Success,
                "Kept archive has complete exact contents"
                );
            }

        CheckQueue(output, [later, waiting, Path.Combine(waiting, new string ('é', 100) + ".notes")]);
        string resumeQueue = Path.ChangeExtension(output, ".mnq");
        host.OpenInputs([resumeQueue]);
        Check(
            !DiskFile.Exists(resumeQueue) && Directory.EnumerateFiles(work, "cancel.keep.mnq.completed-*").Any(),
            "Opening a queue resumes work and marks it completed"
            );
        using (Archive archive = new Archive().Open(output)) {
            Check(
                archive.Files.Count == 4 && archive.Files.Any(file => file.Name == Path.GetFileName(later)) && archive.Files.Any(file => file.Name == new string ('é', 100) + ".notes") && archive.VerifyArchive() == ReturnCode.Success,
                "GUI resume adds the entire remaining queue with long Unicode names"
                );
            }

        string existing = Path.Combine(work, "cancel.overwrite.mnx");
        CreateExisting(existing);
        byte[] before = DiskFile.ReadAllBytes(existing);
        DriveChoice(host, [current, later], existing, keep: false, creating: true, screenshots);
        Check(
            DiskFile.ReadAllBytes(existing).SequenceEqual(before),
            "Delete during creation preserves the old overwrite target"
            );
        Check(
            !Directory.EnumerateFiles(work, ".mnx-pending-*").Any(),
            "Force cancellation removes staging files"
            );
        host.OpenArchive(existing);
        DriveChoice(host, [current, later, waiting], existing, keep: true, creating: false, screenshots);
        using (Archive archive = new Archive().Open(existing)) {
            Check(
                archive.Files.Count == 2 && archive.Files.Any(file => file.Name == "original.txt") && archive.Files.Any(file => file.Name == Path.GetFileName(current)) && archive.VerifyArchive() == ReturnCode.Success,
                "Keep during add preserves original and current complete files"
                );
            }

        CheckQueue(existing, [later, waiting, Path.Combine(waiting, new string ('é', 100) + ".notes")]);
        byte[] savedQueue = DiskFile.ReadAllBytes(Path.ChangeExtension(existing, ".mnq"));
        using (Archive archive = new Archive().Open(existing)) {
            archive.Queue_AddFile(new FileInfo(later));
            RemainingQueue.Save(existing, archive.FileQueue);
            }

        Check(
            Directory.EnumerateFiles(work, "cancel.overwrite.previous-*.mnq").Any(),
            "Previous queue sidecar is retained before replacement"
            );
        DiskFile.WriteAllBytes(Path.ChangeExtension(existing, ".mnq"), savedQueue);
        string deleteTarget = Path.Combine(work, "cancel.delete.mnx");
        CreateExisting(deleteTarget);
        host.OpenArchive(deleteTarget);
        DriveChoice(host, [current, later], deleteTarget, keep: false, creating: false, screenshots);
        Check(
            !DiskFile.Exists(deleteTarget) && host.TestBrowser is null,
            "Delete during add closes and removes the entire selected archive"
            );
        string encrypted = Path.Combine(work, "cancel.encrypted.mnx");
        string second = Path.Combine(work, "cancel.second.large.bin");
        DiskFile.WriteAllBytes(second, contents);
        const string password = "GUI temporary é🐈 key password";
        DriveChoice(
            host,
            [current, second, later],
            encrypted,
            keep: true,
            creating: true,
            screenshots,
            password: password,
            exportKey: true
            );
        string encryptedQueue = Path.ChangeExtension(encrypted, ".mnq");
        string encryptedKey = Path.ChangeExtension(encrypted, ".mnx-key");
        Check(
            ResumePassword.Read(new FileInfo(encryptedQueue), encrypted) == password,
            "Checked export on encrypted creation saves a usable temporary key"
            );
        byte[] firstKey = DiskFile.ReadAllBytes(encryptedKey);
        host.TestWidget<MenuItem>("menu_close").Activate();
        DriveChoice(
            host,
            [encryptedQueue],
            encrypted,
            keep: true,
            creating: false,
            screenshots,
            password: password,
            exportKey: true,
            resuming: true
            );
        Check(
            DiskFile.Exists(encryptedKey) && !DiskFile.ReadAllBytes(encryptedKey).SequenceEqual(firstKey),
            "Resume consumes the old key and another cancellation exports a fresh one"
            );
        host.TestWidget<MenuItem>("menu_close").Activate();
        host.OpenInputs([encryptedQueue]);
        Check(
            !DiskFile.Exists(encryptedKey) && !DiskFile.Exists(encryptedQueue),
            "GUI resume opens via temporary key and consumes it"
            );
        using (Archive archive = new Archive().Open(encrypted, password)) {
            Check(
                archive.Files.Count == 3 && archive.IsEncrypted && archive.Compression == ArchiveCompression.Brotli && archive.VerifyArchive() == ReturnCode.Success,
                "Encrypted GUI resume preserves archive contents and settings"
                );
            }

        string noExport = Path.Combine(work, "cancel.encrypted.no-key.mnx");
        DriveChoice(host, [current, later], noExport, keep: true, creating: true, screenshots, password: password);
        Check(
            DiskFile.Exists(Path.ChangeExtension(noExport, ".mnq")) && !DiskFile.Exists(Path.ChangeExtension(noExport, ".mnx-key")),
            "Encrypted keep does not export a key unless checked"
            );
        DriveChoice(
            host,
            [second, later],
            noExport,
            keep: true,
            creating: false,
            screenshots,
            password: password,
            exportKey: true
            );
        Check(
            ResumePassword.Read(new FileInfo(Path.ChangeExtension(noExport, ".mnq")), noExport) == password,
            "Checked export while adding to an existing encrypted archive also works"
            );
        DriveChoice(
            host,
            [current, later],
            encrypted,
            keep: false,
            creating: true,
            screenshots,
            password: password,
            exportKey: true
            );
        Check(!DiskFile.Exists(encryptedKey), "Delete never exports a new key even if the checkbox was checked");
        Console.WriteLine("PASS: real Cancel button choices keep the current complete file or delete immediately; overwrite targets and parent ownership preserved");
        Console.WriteLine("PASS: unchecked export choice, encrypted creation/add, automatic resume, repeated interruption and key consumption");
        }

    private static void CheckQueue(string archivePath, string[] pending) {
        string queuePath = Path.ChangeExtension(archivePath, ".mnq");
        using JsonDocument document = JsonDocument.Parse(DiskFile.ReadAllBytes(queuePath));
        JsonElement root = document.RootElement;
        JsonElement payload = root.GetProperty("Payload");
        Check(
            root.GetProperty("Format").GetString() == "MNQ1" && root.GetProperty("PayloadSha256").GetString() == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.GetRawText()))),
            "Queue payload checksum covers its exact JSON bytes"
            );
        JsonElement[]? entries = payload.GetProperty("Entries").EnumerateArray().ToArray();
        Check(
            entries.Select(entry => entry.GetProperty("SourcePath").GetString()).SequenceEqual(pending),
            "Sidecar contains every remaining file and directory, and no completed file"
            );
        using Archive archive = new Archive().Open(archivePath);
        JsonElement identity = payload.GetProperty("Archive");
        Check(
            identity.GetProperty("Name").GetString() == Path.GetFileName(archivePath) && identity.GetProperty("ArchiveId").GetGuid() == archive.ArchiveGuid && identity.GetProperty("RootDirectoryId").GetGuid() == archive.RootDirectoryId,
            "Queue identifies the exact kept archive"
            );
        Check(
            entries[0].GetProperty("ParentId").GetGuid() == archive.RootDirectoryId && entries[2].GetProperty("ParentId").GetGuid() == entries[1].GetProperty("EntryId").GetGuid(),
            "Remaining folder relationships are resumable"
            );
        Check(
            entries[0].GetProperty("SourceLength").GetInt64() == new FileInfo(pending[0]).Length && entries[0].GetProperty("SourceModifiedUtc").GetDateTime() == DiskFile.GetLastWriteTimeUtc(pending[0]),
            "Source size and modification time are recorded"
            );
        byte[] bytes = DiskFile.ReadAllBytes(archivePath);
        int chunkSize = archive.ChunkSize;
        int indexBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(60)));
        Check(
            identity.GetProperty("HeaderSha256").GetString() == Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, chunkSize))) && identity.GetProperty("IndexSha256").GetString() == Convert.ToHexString(SHA256.HashData(bytes.AsSpan(chunkSize, indexBytes))),
            "Queue fingerprints the kept archive header and front index"
            );
        if (!OperatingSystem.IsWindows()) {
            Check(
                DiskFile.GetUnixFileMode(queuePath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                "Queue permissions are user-only"
                );
            }
        }

    private static void CreateExisting(string path) {
        using Archive archive = new Archive();
        archive.Options.Compression = ArchiveCompression.Brotli;
        archive.Options.CompressionLevel = 9;
        archive.Create(new FileInfo(path));
        archive.AddStreamNow(new MemoryStream([3, 1, 4]), "original.txt", 3);
        }

    private static void DriveChoice(
        MainWindow host,
        string[] paths,
        string output,
        bool keep,
        bool creating,
        string screenshots,
        string? password = null,
        bool exportKey = false,
        bool resuming = false
        ) {
        Window? parent = host.TestWidget<Window>("main_window");
        bool clicked = false;
        bool answered = false;
        string? error = null;
        Stopwatch? timer = Stopwatch.StartNew();
        uint monitor = GLib.Timeout.Add(10, () => {
            foreach (MessageDialog message in Window.ListToplevels().OfType<MessageDialog>().Where(window => window.Visible)) {
                if (message.Title == "Cancel archiving") {
                    CheckButton? choice = host.TestProgressDialog!.ExportKeyFileChoice!;
                    Check(
                        !choice.Active && choice.Sensitive == (password is not null),
                        "Key export starts unchecked and is enabled only for encryption"
                        );
                    choice.Active = exportKey;
                    Check(
                        clicked && !parent.Visible && message.TransientFor.Handle == host.TestProgressDialog!.Window.Handle,
                        "Cancellation question belongs to the progress dialog with hidden parent"
                        );
                    if (keep && creating) {
                        CreationProgressSmoke.Screenshot(message, Path.Combine(screenshots, "mnx-cancel-choice.png"));
                        }

                    answered = true;
                    message.Respond(keep ? ResponseType.Accept : ResponseType.Reject);
                    }
                else if (message.MessageType == MessageType.Question) {
                    message.Respond(ResponseType.Yes);
                    }
                else {
                    error = "Unexpected cancellation error: " + message.Title;
                    message.Respond(ResponseType.Ok);
                    }

                return true;
                }

            ArchiveCreationDialog? setup = host.TestCreationDialog;
            if (setup is not null && setup.Window.Visible) {
                setup.Output.Text = output;
                setup.Codec.ActiveId = "Brotli";
                setup.Level.Value = 9;
                if (password is not null) {
                    setup.Encrypt.Active = true;
                    setup.Password.Text = password;
                    setup.Confirmation.Text = password;
                    }

                setup.Window.Respond(ResponseType.Accept);
                return true;
                }

            ArchiveProgressDialog? progress = host.TestProgressDialog;
            if (!clicked && progress?.Snapshot is { FileBytes: > 0, Scanning: false }) {
                clicked = true;
                // Opening a modal dialog from this timer would block the same
                // timer that answers it. Dispatch the click separately.
                GLib.Idle.Add(() => {
                    progress.CancelButton.Click();
                    return false;
                    });
                }

            if (timer.ElapsedMilliseconds > 30_000) {
                error = "Cancellation test timed out.";
                return false;
                }

            return true;
            });
        try {
            if (creating || resuming) {
                host.OpenInputs(paths);
                }
            else {
                host.TestImportFiles(paths);
                }
            }
        finally {
            GLib.Source.Remove(monitor);
            }

        Check(
            clicked && answered && error is null && parent.Visible,
            error ?? "Cancel was offered and the main window returned"
            );
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    }
