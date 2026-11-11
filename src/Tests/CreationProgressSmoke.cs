using System.Diagnostics;
using Gtk;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

internal static class CreationProgressSmoke {
    internal static void Run(MainWindow host, string work, string screenshots) {
        Console.WriteLine("CHECK: configuration, validation, overwrite and progress ownership");
        string source = Path.Combine(work, "setup.input.with.dots.txt");
        DiskFile.WriteAllBytes(source, Enumerable.Range(0, 2_000_000).Select(i => (byte)(i * 37)).ToArray());
        string output = Path.Combine(work, "configured.mnx");
        Window? parent = host.TestWidget<Window>("main_window");
        bool progressSeen = false;
        string? error = null;
        bool firstPassword = true;
        bool screenshot = false;
        Stopwatch? timer = Stopwatch.StartNew();
        uint monitor = GLib.Timeout.Add(10, () => {
            MessageDialog[]? messages = Window.ListToplevels().OfType<MessageDialog>().Where(window => window.Visible).ToArray();
            foreach (MessageDialog message in messages) {
                if (message.Title == "Cannot create archive" && firstPassword) {
                    firstPassword = false;
                    Check(!DiskFile.Exists(output), "Password mismatch writes no archive");
                    message.Respond(ResponseType.Ok);
                    }
                else {
                    error = "Unexpected message: " + message.Title;
                    message.Respond(ResponseType.Ok);
                    }
                }

            if (messages.Length > 0) {
                return true;
                }

            ArchiveCreationDialog? setup = host.TestCreationDialog;
            if (setup is not null && setup.Window.Visible) {
                Check(setup.Window.TransientFor.Handle == parent.Handle, "Setup belongs to main window");
                if (!screenshot) {
                    Check(
                        setup.Output.Text.EndsWith("setup.input.with.dots.mnx"),
                        "Suggested output retains dotted basename"
                        );
                    }

                setup.Output.Text = output;
                setup.Codec.ActiveId = "Brotli";
                setup.Level.Value = 2;
                setup.Encrypt.Active = true;
                setup.Password.Text = "matching-passphrase";
                setup.Confirmation.Text = firstPassword ? "mismatched" : "matching-passphrase";
                if (!screenshot) {
                    Screenshot(setup.Window, Path.Combine(screenshots, "mnx-create-options.png"));
                    screenshot = true;
                    }

                setup.Window.Respond(ResponseType.Accept);
                return true;
                }

            foreach (Window window in Window.ListToplevels().Where(window => window.Visible && (window.Title?.StartsWith("MNX Archive Progress") ?? false))) {
                Check(
                    !parent.Visible && window.TransientFor.Handle == parent.Handle,
                    "Progress hides and belongs to its parent"
                    );
                progressSeen = true;
                }

            if (timer.ElapsedMilliseconds > 15_000) {
                error = "Timed out testing creation configuration.";
                host.TestCreationDialog?.Window.Respond(ResponseType.Cancel);
                return false;
                }

            return true;
            });
        try {
            host.OpenInputs([source]);
            }
        finally {
            GLib.Source.Remove(monitor);
            }

        Check(
            error is null && !firstPassword && parent.Visible,
            error ?? "Parent returns after successful transfer"
            );
        using (Archive archive = new Archive().Open(output, "matching-passphrase")) {
            Check(
                archive.Compression == ArchiveCompression.Brotli && archive.CompressionLevel == 2 && archive.IsEncrypted,
                "Compression level and encryption match setup"
                );
            }

        // A source failure after overwrite confirmation must retain the old file.
        byte[] before = DiskFile.ReadAllBytes(output);
        RespondOverwrite(host, [source], output, false);
        Check(DiskFile.ReadAllBytes(output).SequenceEqual(before), "Overwrite No preserves old archive");
        RespondOverwrite(host, [source], output, true);
        using (Archive archive = new Archive().Open(output)) {
            Check(
                archive.Compression == ArchiveCompression.None && !archive.IsEncrypted,
                "Disable compression and encryption"
                );
            }

        before = DiskFile.ReadAllBytes(output);
        string other = Path.Combine(work, "same.input");
        Directory.CreateDirectory(other);
        string duplicate = Path.Combine(other, Path.GetFileName(source));
        DiskFile.WriteAllText(duplicate, "This distinct input has a conflicting root basename.");
        RespondOverwrite(host, [source, duplicate], output, true, expectFailure: true);
        Check(
            DiskFile.ReadAllBytes(output).SequenceEqual(before),
            "Batch name conflict cannot overwrite the existing archive"
            );
        Check(!Directory.EnumerateFiles(work, ".mnx-pending-*").Any(), "Failed staging archive is removed");
        bool invalid = false;
        try {
            ArchiveCreationDialog.ValidateDestination(Path.Combine(work, "missing", "out.mnx"), [source]);
            }
        catch (DirectoryNotFoundException) {
            invalid = true;
            }

        Check(invalid, "Invalid output directory is rejected before transfer");
        Check(
            TransferTracker.PieceCounts(1300, 12_948_000) == "1.3 / 12,948 K Ch.",
            "Piece counters use common K scale with commas"
            );
        using (Archive archive = new Archive()) {
            archive.Queue_AddFile(new FileInfo(source));
            TransferTracker tracker = new TransferTracker(32768);
            FileQueueItem[]? pending = archive.FileQueue.ToArray();
            tracker.Configure(pending);
            tracker.Start(pending[0], work);
            tracker.Update(new OperationProgressArgs { BytesComplete = 750_000 });
            using ArchiveProgressDialog progress = new ArchiveProgressDialog(parent, output, "Brotli, Level 2", true, tracker);
            parent.Hide();
            progress.Update(null);
            Pump();
            Check(
                progress.Window.TransientFor.Handle == parent.Handle && !parent.Visible,
                "Progress is owned while parent is hidden"
                );
            Screenshot(progress.Window, Path.Combine(screenshots, "mnx-archive-progress.png"));
            progress.Window.Hide();
            parent.ShowAll();
            }

        // Actual transfers can finish between two timer ticks on an NVMe drive.
        Console.WriteLine("PASS: setup errors, encryption, overwrite Yes/No, conflict preflight, hidden parent and progress layout; live progress observed=" + progressSeen);
        }

    private static void RespondOverwrite(MainWindow host, string[] paths, string output, bool yes, bool expectFailure = false) {
        bool asked = false;
        bool failed = false;
        string? error = null;
        uint monitor = GLib.Timeout.Add(10, () => {
            MessageDialog[]? messages = Window.ListToplevels().OfType<MessageDialog>().Where(window => window.Visible).ToArray();
            foreach (MessageDialog message in messages) {
                if (message.MessageType == MessageType.Question) {
                    asked = true;
                    message.Respond(yes ? ResponseType.Yes : ResponseType.No);
                    }
                else {
                    failed = true;
                    if (!expectFailure) {
                        error = "Unexpected message: " + message.Title;
                        }

                    message.Respond(ResponseType.Ok);
                    }
                }

            if (messages.Length > 0) {
                return true;
                }

            ArchiveCreationDialog? setup = host.TestCreationDialog;
            if (setup is not null && setup.Window.Visible) {
                if (asked && !yes) {
                    setup.Window.Respond(ResponseType.Cancel);
                    }
                else {
                    setup.Output.Text = output;
                    setup.Compress.Active = false;
                    setup.Window.Respond(ResponseType.Accept);
                    }
                }

            return true;
            });
        try {
            host.OpenInputs(paths);
            }
        finally {
            GLib.Source.Remove(monitor);
            }

        Check(asked && error is null && failed == expectFailure, error ?? "Expected overwrite result");
        }

    private static void Pump() {
        for (int i = 0; i < 20; i++) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }

            Thread.Sleep(5);
            }
        }

    internal static void Screenshot(Window window, string path) {
        Pump();
        IntPtr raw = gdk_pixbuf_get_from_window(window.Window.Handle, 0, 0, window.Allocation.Width, window.Allocation.Height);
        Check(raw != IntPtr.Zero, "Window screenshot available");
        using Gdk.Pixbuf image = new Gdk.Pixbuf(raw);
        image.Save(path, "png");
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }

    [System.Runtime.InteropServices.DllImport("libgdk-3.so.0", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern IntPtr gdk_pixbuf_get_from_window(IntPtr window, int x, int y, int width, int height);
    }
