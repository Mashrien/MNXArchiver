using System.Diagnostics;
using Gtk;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

internal static class ExtractionSmoke {
    internal static void Run(MainWindow host, string work, string screenshots) {
        string archivePath = Path.Combine(work, "extraction.progress.mnx");
        byte[] data = new byte[32 * 1024 * 1024];
        new Random(273).NextBytes(data);
        using (ArchiveFile archive = new ArchiveFile()) {
            archive.Create(new FileInfo(archivePath));
            archive.AddStreamNow(new MemoryStream(data), "first.dots.é.bin", data.Length);
            archive.AddStreamNow(new MemoryStream(data), "second.bin", data.Length);
            }

        host.OpenArchive(archivePath);
        TreeView? list = host.TestWidget<TreeView>("file_list");
        list.Selection.SelectAll();
        Window? parent = host.TestWidget<Window>("main_window");
        string output = Path.Combine(work, "extraction-output");
        Directory.CreateDirectory(output);
        bool seen = false;
        bool attempted = false;
        Exception? failure = null;
        Stopwatch? timer = Stopwatch.StartNew();
        long chosenAt = 0;
        uint monitor = GLib.Timeout.Add(5, () => {
            try {
                FileChooserDialog? chooser = Window.ListToplevels().OfType<FileChooserDialog>().FirstOrDefault(dialog => dialog.Visible);
                if (chooser is not null) {
                    if (chosenAt == 0) {
                        chooser.SetFilename(output);
                        chosenAt = timer.ElapsedMilliseconds;
                        }
                    else if (timer.ElapsedMilliseconds - chosenAt > 500) {
                        chooser.Respond(ResponseType.Accept);
                        }
                    }

                ArchiveProgressDialog? dialog = host.TestProgressDialog;
                if (dialog is not null && dialog.Window.Visible) {
                    seen = true;
                    Check(
                        !parent.Visible && !parent.Sensitive && dialog.Window.TransientFor.Handle == parent.Handle,
                        "Extraction owns a hidden, disabled parent"
                        );
                    Check(
                        dialog.Snapshot is { TotalFiles: 2 } snapshot && snapshot.Total == data.Length * 2L,
                        "Extraction tracks the whole selection instead of just the current file"
                        );
                    Check(
                        dialog.Window.Title.StartsWith("MNX Extraction Progress"),
                        "Extraction has its own progress title"
                        );
                    if (!attempted) {
                        attempted = true;
                        Check(
                            !host.OpenArchive(archivePath, showErrors: false),
                            "Another archive operation is refused during extraction"
                            );
                        host.OpenInputs([archivePath]);
                        host.TestWidget<MenuItem>("menu_extract").Activate();
                        CreationProgressSmoke.Screenshot(dialog.Window, Path.Combine(screenshots, "mnx-extraction-progress.png"));
                        }
                    }

                if (timer.ElapsedMilliseconds > 30_000) {
                    throw new InvalidOperationException("Extraction smoke timed out.");
                    }
                }
            catch (Exception error) {
                failure = error;
                return false;
                }

            return true;
            });
        try {
            host.TestWidget<MenuItem>("menu_extract").Activate();
            }
        finally {
            if (failure is null) {
                GLib.Source.Remove(monitor);
                }
            }

        if (failure is not null) {
            throw failure;
            }

        Check(
            seen && attempted && parent.Visible && parent.Sensitive,
            "Extraction completes and restores the parent"
            );
        Check(
            DiskFile.ReadAllBytes(Path.Combine(output, "first.dots.é.bin")).SequenceEqual(data) && DiskFile.ReadAllBytes(Path.Combine(output, "second.bin")).SequenceEqual(data),
            "Every selected file extracts exactly"
            );
        Console.WriteLine("PASS: extraction totals, owned progress, disabled parent, overlapping operation rejection and exact output");
        DriveCancellation(host, archivePath, Path.Combine(work, "extract-keep"), keep: true, screenshots);
        DriveCancellation(host, archivePath, Path.Combine(work, "extract-stop"), keep: false, screenshots);
        Check(
            DiskFile.ReadAllBytes(Path.Combine(work, "extract-keep", "first.dots.é.bin")).SequenceEqual(data),
            "Stop after current file keeps its complete output"
            );
        Check(
            !DiskFile.Exists(Path.Combine(work, "extract-keep", "second.bin")),
            "Stop after current file never starts the next file"
            );
        Check(
            DiskFile.ReadAllBytes(Path.Combine(work, "extract-stop", "first.dots.é.bin")).SequenceEqual(data),
            "Stop now retains the earlier completed file"
            );
        Check(
            !DiskFile.Exists(Path.Combine(work, "extract-stop", "second.bin")),
            "Stop now deletes the unfinished direct output"
            );
        using (ArchiveFile archive = new ArchiveFile().Open(archivePath)) {
            Check(
                archive.VerifyArchive() == ArchiveFile.ReturnCode.Success,
                "Extraction cancellation leaves the archive intact"
                );
            }

        host.ExtractArchive(archivePath, extractHere: true);
        Check(
            DiskFile.ReadAllBytes(Path.Combine(work, "first.dots.é.bin")).SequenceEqual(data) && DiskFile.ReadAllBytes(Path.Combine(work, "second.bin")).SequenceEqual(data),
            "Extract Here writes root contents beside the archive"
            );
        string chosenOutput = Path.Combine(work, "extract-to-action");
        Directory.CreateDirectory(chosenOutput);
        DriveFolderAction(host, archivePath, chosenOutput);
        Check(
            DiskFile.ReadAllBytes(Path.Combine(chosenOutput, "second.bin")).SequenceEqual(data),
            "Extract To uses the selected folder"
            );
        DriveReplacement(host, archivePath, chosenOutput, replace: false);
        Check(
            DiskFile.ReadAllText(Path.Combine(chosenOutput, "second.bin")) == "keep this existing target",
            "Declining replacement preserves existing files"
            );
        DriveReplacement(host, archivePath, chosenOutput, replace: true);
        Check(
            DiskFile.ReadAllBytes(Path.Combine(chosenOutput, "second.bin")).SequenceEqual(data),
            "Confirmed replacement extracts exact bytes"
            );
        Console.WriteLine("PASS: real extraction cancel choices, direct output removal, Extract Here/To and overwrite confirmation");
        }

    private static void DriveCancellation(MainWindow host, string archivePath, string output, bool keep, string screenshots) {
        Directory.CreateDirectory(output);
        host.OpenArchive(archivePath);
        host.TestWidget<TreeView>("file_list").Selection.SelectAll();
        Window? parent = host.TestWidget<Window>("main_window");
        ArchiveFile? archive = host.TestArchive!;
        // Hold one partial file until the GUI has applied the cancellation choice.
        // Notification cadence is intentionally throttled across the native ABI;
        // sleeping per notification would no longer make this test deterministic.
        using ManualResetEventSlim releaseWorker = new ManualResetEventSlim(false);
        int held = 0;
        int reports = 0;
        archive.Options.ProgressReportFrequency = 1;
        string heldFile = keep ? "first.dots.é.bin" : "second.bin";
        void SlowProgress(object? sender, OperationProgressArgs progress) {
            if (progress.FileName == heldFile && Interlocked.Increment(ref reports) == 1) {
                // Let the normal progress subscriber receive this first report.
                // The next callback can then block while the UI sees real progress.
                Thread.Sleep(5);
                return;
                }
            if (progress.FileName == heldFile && progress.BytesComplete > 0 &&
                progress.BytesComplete < progress.BytesMax && Interlocked.CompareExchange(ref held, 1, 0) == 0) {
                if (!releaseWorker.Wait(TimeSpan.FromSeconds(25))) {
                    throw new TimeoutException("The GUI did not apply its cancellation choice.");
                    }
                }
            }

        archive.ProgressReport += SlowProgress;
        bool clicked = false;
        bool answered = false;
        string? error = null;
        Stopwatch? timer = Stopwatch.StartNew();
        long chosenAt = -1;
        uint monitor = GLib.Timeout.Add(5, () => {
            foreach (MessageDialog question in Window.ListToplevels().OfType<MessageDialog>().Where(dialog => dialog.Visible)) {
                if (question.Title == "Cancel extraction") {
                    Check(
                        clicked && !parent.Visible && !parent.Sensitive && question.TransientFor.Handle == host.TestProgressDialog!.Window.Handle,
                        "Extraction cancel question is owned by its progress dialog"
                        );
                    Check(
                        host.TestProgressDialog!.ExportKeyFileChoice is null,
                        "Extraction cancellation does not export archive keys"
                        );
                    CreationProgressSmoke.Screenshot(question, Path.Combine(screenshots, keep ? "mnx-extract-keep.png" : "mnx-extract-stop.png"));
                    answered = true;
                    question.Respond(keep ? ResponseType.Accept : ResponseType.Reject);
                    GLib.Idle.Add(() => {
                        releaseWorker.Set();
                        return false;
                        });
                    }
                else {
                    error = "Unexpected extraction question: " + question.Title + ": " + question.Text;
                    question.Respond(ResponseType.Ok);
                    }

                return true;
                }

            FileChooserDialog? chooser = Window.ListToplevels().OfType<FileChooserDialog>().FirstOrDefault(dialog => dialog.Visible);
            if (chooser is not null) {
                if (chosenAt < 0) {
                    chooser.SetFilename(output);
                    chosenAt = timer.ElapsedMilliseconds;
                    }
                else if (timer.ElapsedMilliseconds - chosenAt > 500) {
                    chooser.Respond(ResponseType.Accept);
                    }

                return true;
                }

            ArchiveProgressDialog? progress = host.TestProgressDialog;
            int completedBeforeClick = keep ? 0 : 1;
            if (!clicked && progress?.Snapshot is { FileBytes: > 0 } snapshot && snapshot.Files == completedBeforeClick) {
                clicked = true;
                GLib.Idle.Add(() => {
                    progress.CancelButton.Click();
                    return false;
                    });
                }

            if (timer.ElapsedMilliseconds > 30_000) {
                error = "Extraction cancellation timed out.";
                return false;
                }

            return true;
            });
        try {
            host.TestWidget<MenuItem>("menu_extract").Activate();
            }
        finally {
            releaseWorker.Set();
            archive.ProgressReport -= SlowProgress;
            if (timer.ElapsedMilliseconds <= 30_000) {
                GLib.Source.Remove(monitor);
                }
            }

        Check(
            clicked && answered && error is null && parent.Visible && parent.Sensitive,
            error ?? "Extraction cancellation returns control to the main window"
            );
        Check(
            !Directory.EnumerateFiles(output, "*.mnq").Any() && !Directory.EnumerateFiles(output, ".mnx-extract-*").Any(),
            "Extraction uses no resume queue or temporary output files"
            );
        }

    private static void DriveFolderAction(MainWindow host, string archivePath, string output) {
        Stopwatch? timer = Stopwatch.StartNew();
        long chosenAt = -1;
        uint monitor = GLib.Timeout.Add(10, () => {
            FileChooserDialog? chooser = Window.ListToplevels().OfType<FileChooserDialog>().FirstOrDefault(dialog => dialog.Visible);
            if (chooser is not null) {
                if (chosenAt < 0) {
                    chooser.SetFilename(output);
                    chosenAt = timer.ElapsedMilliseconds;
                    }
                else if (timer.ElapsedMilliseconds - chosenAt > 500) {
                    chooser.Respond(ResponseType.Accept);
                    }
                }

            return true;
            });
        try {
            host.ExtractArchive(archivePath, extractHere: false);
            }
        finally {
            GLib.Source.Remove(monitor);
            }
        }

    private static void DriveReplacement(MainWindow host, string archivePath, string output, bool replace) {
        DiskFile.WriteAllText(Path.Combine(output, "second.bin"), "keep this existing target");
        bool answered = false;
        uint monitor = GLib.Timeout.Add(10, () => {
            MessageDialog? question = Window.ListToplevels().OfType<MessageDialog>().FirstOrDefault(dialog => dialog.Visible && dialog.Title == "Replace existing files?");
            if (question is not null) {
                answered = true;
                question.Respond(replace ? ResponseType.Yes : ResponseType.No);
                }

            return true;
            });
        try {
            DriveFolderAction(host, archivePath, output);
            }
        finally {
            GLib.Source.Remove(monitor);
            }

        Check(answered, "Existing output files require replacement confirmation");
        }

    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + message);
            }
        }
    }
