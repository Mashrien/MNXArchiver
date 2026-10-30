using System.Diagnostics;
using Gtk;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;
using VirtualFile = Mash.IO.Format.MNX.VFile.VFile;

namespace Mash.UI.MNXFile.GUI;
/// <summary>Exercise the real save/folder dialogs and context menu on disposable data.</summary>
internal static class FileActionsSmoke {
    public static void Run(MainWindow host, string directory) {
        Console.WriteLine("CHECK: creation/extraction dialogs");
        string source = Path.Combine(directory, "context input");
        Directory.CreateDirectory(source);
        string ordinary = Path.Combine(source, "ordinary.mnx");
        byte[] bytes = new byte[79_019];
        new Random(109).NextBytes(bytes);
        DiskFile.WriteAllBytes(ordinary, bytes);
        string folder = Path.Combine(source, "documents");
        Directory.CreateDirectory(Path.Combine(folder, "empty"));
        DiskFile.WriteAllText(Path.Combine(folder, "distinct ü.txt"), "A unique file in a nested folder.");
        string destination = Path.Combine(directory, "created # 100% ü.mnx");
        string previous = host.TestBrowser!.Title;
        Choose(host, () => host.OpenInputs([ordinary, folder]), null);
        Check(
            host.TestBrowser.Title == previous && !DiskFile.Exists(destination),
            "Cancelled creation preserves the open archive"
            );
        Choose(host, () => host.OpenInputs([new Uri(ordinary).AbsoluteUri, folder]), destination);
        Check(
            host.TestBrowser!.Title == Path.GetFileName(destination) && host.TestListCount == 2,
            "Multiple startup inputs create a new archive through the configuration dialog"
            );
        using (ArchiveFile archive = new ArchiveFile()) {
            archive.Open(destination);
            Check(archive.Compression == ArchiveCompression.Snappy, "New context archive uses Snappy");
            VirtualFile? file = archive.Files.Single(item => item.Name == "ordinary.mnx");
            using Stream input = archive.OpenRead(file.FileID);
            using MemoryStream buffer = new MemoryStream();
            input.CopyTo(buffer);
            Check(buffer.ToArray().SequenceEqual(bytes), "A misleading MNX extension is stored as ordinary input");
            }

        // A real archive needs no .mnx suffix to open normally.
        string renamed = Path.Combine(directory, "archive without suffix");
        DiskFile.Copy(destination, renamed);
        host.OpenInputs([renamed]);
        Check(
            host.TestBrowser!.Title == Path.GetFileName(renamed),
            "Startup identifies archives by magic rather than extension"
            );
        TreeView? list = host.TestWidget<TreeView>("file_list");
        using (TreePath row = new TreePath("1")) {
            TransferSmoke.RightClick(list, row);
            }

        Check(
            host.TestEntryMenu.Visible && host.TestExtractEntry.Sensitive,
            "Right click displays an enabled Extract menu"
            );
        host.TestEntryMenu.Popdown();
        string extraction = Path.Combine(directory, "renamed output # ü.bin");
        Choose(host, () => host.TestExtractEntry.Activate(), null);
        Check(!DiskFile.Exists(extraction), "Cancelled extraction writes nothing");
        Choose(host, () => host.TestExtractEntry.Activate(), extraction);
        Check(
            DiskFile.ReadAllBytes(extraction).SequenceEqual(bytes),
            "Context menu saves the file to the chosen name with exact bytes"
            );
        using (TreePath row = new TreePath("0")) {
            list.Selection.UnselectAll();
            list.Selection.SelectPath(row);
            }

        string outputFolder = Path.Combine(directory, "folder extraction");
        Directory.CreateDirectory(outputFolder);
        Choose(host, () => host.TestWidget<MenuItem>("menu_extract").Activate(), outputFolder);
        Check(
            DiskFile.ReadAllText(Path.Combine(outputFolder, "documents", "distinct ü.txt")) == "A unique file in a nested folder." && Directory.Exists(Path.Combine(outputFolder, "documents", "empty")),
            "Folder chooser preserves directory contents and empty folders"
            );
        using (TreePath row = new TreePath("1")) {
            list.Selection.SelectPath(row);
            }

        string multi = Path.Combine(directory, "multiple extraction");
        Directory.CreateDirectory(multi);
        Choose(host, () => host.TestWidget<MenuItem>("menu_extract").Activate(), multi);
        Check(
            DiskFile.ReadAllBytes(Path.Combine(multi, "ordinary.mnx")).SequenceEqual(bytes) && DiskFile.Exists(Path.Combine(multi, "documents", "distinct ü.txt")),
            "Multiple selection extracts through the folder dialog"
            );
        Check(DiskFile.ReadAllBytes(ordinary).SequenceEqual(bytes), "Creation preserves original source bytes");
        }

    private static void Choose(MainWindow host, System.Action action, string? destination) {
        Exception? error = null;
        FileChooserDialog? current = null;
        Stopwatch? timer = Stopwatch.StartNew();
        long readyAt = 0;
        bool responded = false;
        GLib.Timeout.Add(50, () => {
            try {
                Window[]? dialogs = Window.ListToplevels();
                MessageDialog? message = dialogs.OfType<MessageDialog>().FirstOrDefault(window => window.Visible);
                if (message is not null) {
                    error = new InvalidOperationException("Unexpected error dialog: " + message.Title);
                    message.Respond(ResponseType.Ok);
                    responded = true;
                    return false;
                    }

                ArchiveCreationDialog? setup = host.TestCreationDialog;
                if (setup is not null && setup.Window.Visible) {
                    if (destination is not null) {
                        setup.Output.Text = destination;
                        }

                    setup.Window.Respond(destination is null ? ResponseType.Cancel : ResponseType.Accept);
                    responded = true;
                    return false;
                    }

                current ??= dialogs.OfType<FileChooserDialog>().FirstOrDefault(window => window.Visible);
                if (current is null) {
                    if (timer.ElapsedMilliseconds > 10_000) {
                        error = new InvalidOperationException("Expected a file chooser dialog.");
                        return false;
                        }

                    return true;
                    }

                if (destination is null) {
                    current.Respond(ResponseType.Cancel);
                    responded = true;
                    return false;
                    }

                if (readyAt == 0) {
                    if (current.Action == FileChooserAction.SelectFolder) {
                        current.SetFilename(destination);
                        }
                    else {
                        current.SetCurrentFolder(Path.GetDirectoryName(destination)!);
                        current.CurrentName = Path.GetFileName(destination);
                        }

                    readyAt = timer.ElapsedMilliseconds + 500;
                    return true;
                    }

                if (timer.ElapsedMilliseconds < readyAt) {
                    return true;
                    }

                Console.WriteLine("CHECK: accepting " + current.Filename + " action=" + current.Action);
                current.Respond(ResponseType.Accept);
                responded = true;
                return false;
                }
            catch (Exception ex) {
                error = ex;
                current?.Respond(ResponseType.Cancel);
                responded = true;
                return false;
                }
            });
        Console.WriteLine("CHECK: opening chooser " + (destination ?? "cancel"));
        action();
        Console.WriteLine("CHECK: chooser completed, response=" + responded);
        if (error is not null) {
            throw error;
            }

        Check(responded, "Dialog responded to the test input");
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    }
