using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive.Resume;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    internal async Task RunSmokeTest(string directory) {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string work = Path.Combine(directory, "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        List<string> evidence = new List<string>();
        RunDesignerConstructionChecks(directory);
        evidence.Add("PASS: all ten parameterless designer forms instantiate/dispose; MWin caption visibility, defaults and standalone RoundButton painting");
        LoadPreview();
        await Task.Delay(200);
        Require(folderTree.Nodes.Count == 1 && fileList.Items.Count > 0, "Preview layout was not populated.");
        SaveScreenshot(Path.Combine(directory, "01-layout.png"));
        Require(browserSplit.SplitterDistance == 192, "The folder pane must start at 192 pixels.");
        Require(ReferenceEquals(statusBar.Parent, StatusBarControls) && ReferenceEquals(statusMessage, StatusText),
            "Status items are not hosted inside MWin's owned status region.");
        Require(StatusBarStrip.SizingGrip == false && !StatusBarControls.Bounds.Contains(ClientSize.Width - 5, ClientSize.Height - 5),
            "Status controls cover the native resize grip.");
        Require(currentPath.Dock == DockStyle.Fill && searchText.Dock == DockStyle.Fill,
            "Path and search fields are not standard dockable text controls.");
        Require(pathLabel.Width >= TextRenderer.MeasureText(pathLabel.Text, pathLabel.Font).Width
            && searchLabel.Width >= TextRenderer.MeasureText(searchLabel.Text, searchLabel.Font).Width,
            "Path and Search labels must fit their complete text.");
        int pathWidth = currentPath.Width;
        int previousNameWidth = nameColumn.Width;
        int searchWidth = searchText.Width;
        Size previous = Size;
        Size = new Size(1380, 860);
        await Task.Delay(80);
        Require(currentPath.Width > pathWidth && searchText.Width == searchWidth && browserSplit.SplitterDistance == 192,
            "Resizing must grow the path field and preserve the search and folder pane widths.");
        Require(nameColumn.Width > previousNameWidth, "The Name column must absorb spare width when the file list grows.");
        int expandedNameWidth = nameColumn.Width;
        nameColumn.Width = previousNameWidth;
        Require(nameColumn.Width == expandedNameWidth,
            "Manually narrowing Name must still fill the available space.");
        int previousTypeWidth = typeColumn.Width;
        typeColumn.Width += 20;
        Require(nameColumn.Width == expandedNameWidth - 20,
            "Widening a metadata column must reduce the spare width assigned to Name.");
        typeColumn.Width = previousTypeWidth;
        Require(browserSplit.Width > 1000 && fileList.Width > 500 && folderTree.Width > 180, "Docking did not resize the browser.");
        SaveScreenshot(Path.Combine(directory, "02-resized.png"));
        Size = previous;
        evidence.Add("PASS: populated designer layout, 192-pixel folder pane, expanding path field, stable search width, protected MWin status container, embedded icons");
        string inputs = Path.Combine(work, "Unicode 文件 λ");
        Directory.CreateDirectory(inputs);
        string first = Path.Combine(inputs, "Read me λ.txt");
        string second = Path.Combine(inputs, "second.txt");
        byte[] payload = Encoding.UTF8.GetBytes("MNX WinForms Unicode smoke\r\n文件 λ\r\n");
        DiskFile.WriteAllBytes(first, payload);
        DiskFile.WriteAllText(second, "Second payload", Encoding.UTF8);
        string folder = Path.Combine(inputs, "Nested");
        Directory.CreateDirectory(folder);
        DiskFile.WriteAllText(Path.Combine(folder, "child.txt"), "nested child");
        string archivePath = Path.Combine(work, "Windows 文件.mnx");
        await CreateArchive(new string[] { inputs }, new CreationSettings(archivePath, ArchiveCompression.Snappy, 0, null, false));
        Require(_archive is not null && _archive.Files.Count == 5, "Creation did not produce expected indexed entries.");
        Guid inputId = _archive!.Files.Single(entry => entry.Name == Path.GetFileName(inputs)).FileID;
        Navigate(inputId);
        Require(currentPath.Text.Contains("文件", StringComparison.Ordinal) && fileList.Items.Count == 3, "Folder navigation failed.");
        Require(fileList.Columns.Count == 7 && fileList.Items.Cast<ListViewItem>().All(item => item.SubItems.Count == 7),
            "The user-added file type and chunk columns were lost.");
        ListViewItem textItem = fileList.Items.Cast<ListViewItem>().Single(item => item.Text == "Read me λ.txt");
        Require(textItem.SubItems[1].Text == "TXT" && textItem.SubItems[5].Text.Length > 0,
            "File type and chunk columns were not populated.");
        EntryComparer sizeSort = new EntryComparer(this, 2, false);
        ListViewItem smallerItem = fileList.Items.Cast<ListViewItem>().Single(item => item.Text == "second.txt");
        Require(sizeSort.Compare(smallerItem, textItem) < 0 && new EntryComparer(this, 2, true).Compare(smallerItem, textItem) > 0,
            "Size sorting is not numeric or does not reverse correctly.");
        searchText.Text = "child";
        Require(fileList.Items.Count == 1 && fileList.Items[0].Text == "child.txt", "Recursive search failed.");
        searchText.Text = "";
        Guid firstId = _archive.Files.Single(entry => entry.Name == "Read me λ.txt").FileID;
        fileList.Items.Cast<ListViewItem>().Single(item => (Guid)item.Tag! == firstId).Selected = true;
        Require(SelectedIds().SequenceEqual(new Guid[] { firstId }), "List selection failed.");
        Require(DragSelectionAt(fileList, new Point(fileList.ClientSize.Width - 4, fileList.ClientSize.Height - 4)).Length == 0, "Empty list area started a drag using stale selection.");
        Require(DragSelectionAt(folderTree, new Point(8, folderTree.ClientSize.Height - 4)).Length == 0, "Empty tree area started a drag using stale selection.");
        ListViewItem selectedItem = fileList.Items.Cast<ListViewItem>().Single(item => (Guid)item.Tag! == firstId);
        Require(DragSelectionAt(fileList, new Point(selectedItem.Bounds.Left + 20, selectedItem.Bounds.Top + 5)).SequenceEqual(new Guid[] { firstId }), "File drag selection failed.");
        SaveScreenshot(Path.Combine(directory, "03-archive-navigation.png"));
        evidence.Add("PASS: native-engine creation, Unicode metadata navigation, recursive search, selection and safe empty-area drag");
        string added = Path.Combine(work, "added.txt");
        DiskFile.WriteAllText(added, "added through UI worker");
        await ImportFiles(new string[] { added }, inputId);
        Require(_archive.Files.Any(entry => entry.Name == "added.txt" && entry.OwnerID == inputId), "Add did not use the selected archive folder.");
        await VerifyArchive();
        string output = Path.Combine(work, "Extracted 文件");
        await ExtractSelection(new Guid[] { inputId }, output);
        Require(DiskFile.ReadAllBytes(Path.Combine(output, Path.GetFileName(inputs), "Read me λ.txt")).SequenceEqual(payload), "Extraction bytes did not match.");
        Guid addedId = _archive.Files.Single(entry => entry.Name == "added.txt").FileID;
        await DeleteEntries(new Guid[] { addedId }, true);
        Require(_archive.GetFile(addedId) is null, "Delete did not remove the indexed entry.");
        await CompactArchive();
        await VerifyArchive();
        await CheckArchiveDrops(archivePath, work);
        evidence.Add("PASS: archive drop Open/Add/Cancel and ordinary creation form when no archive is open");
        evidence.Add("PASS: add to selected folder, extraction bytes, delete, verify, compact through serialized UI workers");
        string current = Path.Combine(work, "current-large.bin");
        byte[] currentBytes = new byte[32 * 1024 * 1024];
        new Random(719).NextBytes(currentBytes);
        DiskFile.WriteAllBytes(current, currentBytes);
        string keepArchive = Path.Combine(work, "keep.mnx");
        await DriveCancellation(new string[] { current, second }, new CreationSettings(keepArchive, ArchiveCompression.Brotli, 4, "smoke-password", false), true, directory);
        string queuePath = Path.ChangeExtension(keepArchive, ".mnq");
        Require(DiskFile.Exists(keepArchive) && DiskFile.Exists(queuePath), "Keep cancellation did not preserve archive and MNQ.");
        QueuePayload queue = RemainingQueue.Read(new FileInfo(queuePath));
        Require(queue.Entries.Length == 1, "Keep cancellation did not stop at the completed file boundary.");
        Require(_archive!.Files.Count == 1 && _archive.Files.Single().Name == Path.GetFileName(current), "Keep cancellation did not retain exactly the completed current file.");
        Require(_archive.ReadFileRange(_archive.Files.Single().FileID, 0, currentBytes.Length).SequenceEqual(currentBytes), "The completed current file was truncated by keep cancellation.");
        CloseArchive();
        await ResumeFromQueue(queuePath);
        Require(_archive is not null && _archive.Files.Count == 2, "Encrypted resume did not restore remaining files.");
        await VerifyArchive();
        string abortArchive = Path.Combine(work, "abort.mnx");
        bool aborted = false;
        try {
            await DriveCancellation(new string[] { current, second }, new CreationSettings(abortArchive, ArchiveCompression.Brotli, 4, null, false), false, directory);
            }
        catch (OperationCanceledException) {
            aborted = true;
            }
        Require(aborted && !DiskFile.Exists(abortArchive) && !Directory.EnumerateFiles(work, ".mnx-pending-*").Any(), "Abort cancellation left partial output.");
        evidence.Add("PASS: owned progress and cancellation dialogs with hidden main form, finish-current keep, checksummed MNQ and exported key, encrypted resume, immediate abort removes staging output");
        foreach (ArchiveCompression codec in new ArchiveCompression[] { ArchiveCompression.None, ArchiveCompression.LZ4 }) {
            string codecPath = Path.Combine(work, codec + ".mnx");
            await CreateArchive(new string[] { first }, new CreationSettings(codecPath, codec, 0, null, false));
            await VerifyArchive();
            Require(_archive!.Compression == codec, "The selected creation codec was not retained.");
            }
        evidence.Add("PASS: WinForms creation choices None, Snappy, LZ4 and Brotli, encryption");
        string bad = Path.Combine(work, "corrupt.mnx");
        DiskFile.WriteAllText(bad, "not an archive");
        bool rejected = false;
        try {
            await OpenArchive(bad, showErrors: false);
            }
        catch {
            rejected = true;
            }
        Require(rejected && _archive is not null, "Corrupt archive input was not rejected while preserving the previous view.");
        string diagnostic = ArchiveOpenError(bad, new InvalidDataException("Invalid MNX archive format"), ArchiveFile.ReturnCode.NotValidMNXFile);
        Require(diagnostic.Contains("Error code:", StringComparison.Ordinal), "Archive diagnostics omitted the error code.");
        await CheckInvalidCommandLine(bad);
        evidence.Add("PASS: corrupt .mnx preserves current archive interactively; command-line invalid archive shows format/error-code dialog and exits with code 1");
        PreviewSmokeTests.Run(this, directory);
        evidence.Add("PASS: options and preview smoke assertions");
        SaveScreenshot(Path.Combine(directory, "04-complete.png"));
        DiskFile.WriteAllLines(Path.Combine(directory, "PASS.txt"), evidence);
        }

    private static async Task CheckInvalidCommandLine(string path) {
        ProcessStartInfo start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running GUI executable.")) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "mnx-gui.dll"));
            }
        start.ArgumentList.Add(path);
        using Process child = Process.Start(start) ?? throw new IOException("Could not start the invalid-archive smoke process.");
        bool observed = false;
        string dialogText = "";
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!child.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(20)) {
            NativeSmoke.EnumWindows((window, _) => {
                NativeSmoke.GetWindowThreadProcessId(window, out uint processId);
                if (processId != child.Id || NativeSmoke.WindowText(window) != "Invalid or unreadable MNX archive") {
                    return true;
                    }
                observed = true;
                IntPtr okayButton = IntPtr.Zero;
                NativeSmoke.EnumChildWindows(window, (control, _) => {
                    string text = NativeSmoke.ControlText(control);
                    dialogText += text + "\n";
                    if (text.Replace("&", "", StringComparison.Ordinal) == "OK") {
                        okayButton = control;
                        }
                    return true;
                    }, IntPtr.Zero);
                // EnumWindows can observe the message box before Windows has
                // populated its static text. Read the diagnostic before closing it.
                if (okayButton != IntPtr.Zero && dialogText.Contains("Error code:", StringComparison.Ordinal)) {
                    NativeSmoke.PostMessage(okayButton, 0x00f5, IntPtr.Zero, IntPtr.Zero);
                    }
                return true;
                }, IntPtr.Zero);
            await Task.Delay(40);
            }
        if (!child.HasExited) {
            child.Kill(true);
            await child.WaitForExitAsync();
            throw new InvalidOperationException("Command-line invalid archive did not exit after its error dialog was dismissed.");
            }
        Require(observed && dialogText.Contains("Error code:", StringComparison.Ordinal) && child.ExitCode == 1,
            "Command-line invalid archive failed its diagnostic check. Observed: " + observed + "; exit code: " + child.ExitCode + "; dialog text: " + dialogText);
        }

    private static class NativeSmoke {
        internal delegate bool WindowCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr first, IntPtr second);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr first, StringBuilder second, uint flags, uint timeout, out UIntPtr result);
        internal static string ControlText(IntPtr window) {
            StringBuilder text = new StringBuilder(4096);
            SendMessageTimeout(window, 0x000d, new UIntPtr((uint)text.Capacity), text, 2, 1000, out UIntPtr result);
            return text.ToString();
            }
        internal static string WindowText(IntPtr window) {
            StringBuilder text = new StringBuilder(4096);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
            }
        }

    private async Task DriveCancellation(string[] paths, CreationSettings settings, bool keep, string screenshots) {
        bool progressSeen = false;
        bool clickedCancel = false;
        bool choiceSeen = false;
        int filePaused = 0;
        using ManualResetEventSlim cancellationChosen = new ManualResetEventSlim(false);
        string? monitorError = null;
        using System.Windows.Forms.Timer monitor = new System.Windows.Forms.Timer { Interval = 10 };
        using System.Windows.Forms.Timer choose = new System.Windows.Forms.Timer { Interval = 10 };
        choose.Tick += (_, _) => {
            Form? choice = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Text == "Cancel operation");
            if (choice is null) {
                return;
                }
            choiceSeen = true;
            if (choice.Owner is not ProgressForm) {
                monitorError = "Cancellation choice was not owned by the progress window.";
                }
            using (Bitmap image = new Bitmap(choice.Width, choice.Height)) {
                choice.DrawToBitmap(image, new Rectangle(Point.Empty, choice.Size));
                image.Save(Path.Combine(screenshots, keep ? "06-keep-choice.png" : "08-delete-choice.png"), ImageFormat.Png);
                }
            if (keep) {
                foreach (CheckBox export in choice.Controls.OfType<CheckBox>()) {
                    export.Checked = true;
                    }
                }
            Button button = (Button)choice.Controls.Find(keep ? "keepCancellation" : "deleteCancellation", true).Single();
            choose.Stop();
            button.PerformClick();
            cancellationChosen.Set();
            };
        monitor.Tick += (_, _) => {
            ProgressForm? progress = Application.OpenForms.OfType<ProgressForm>().FirstOrDefault(form => form.Text.StartsWith("Creating archive", StringComparison.Ordinal));
            if (progress is null) {
                return;
                }
            progressSeen = true;
            if (Visible || progress.Owner != this) {
                monitorError = "The progress window did not own a hidden main window.";
                }
            TransferSnapshot? snapshot = progress.Snapshot;
            if (!clickedCancel && snapshot is not null && snapshot.FileBytes > 0 && snapshot.FileBytes < snapshot.FileTotal) {
                clickedCancel = true;
                progress.UpdateProgress(null);
                Require(progress.Text.Contains('%'), "The progress title omitted its percentage.");
                using (Bitmap image = new Bitmap(progress.Width, progress.Height)) {
                    progress.DrawToBitmap(image, new Rectangle(Point.Empty, progress.Size));
                    image.Save(Path.Combine(screenshots, keep ? "05-progress-keep.png" : "07-progress-delete.png"), ImageFormat.Png);
                    }
                monitor.Stop();
                Button button = (Button)progress.Controls.Find("cancelTransfer", true).Single();
                button.PerformClick();
                }
            };
        monitor.Start();
        choose.Start();
        try {
            await CreateArchive(paths, settings, (_, archive) => {
                // Hold the worker after a real partial-file callback so the UI
                // can click Cancel even when the VM stops scheduling its timer.
                archive.ProgressReport += (_, progress) => {
                    if (progress.BytesComplete > 0 && progress.BytesComplete < progress.BytesMax
                        && Interlocked.CompareExchange(ref filePaused, 1, 0) == 0) {
                        if (!cancellationChosen.Wait(TimeSpan.FromSeconds(15))) {
                            throw new TimeoutException("The cancellation test did not answer its dialog while the file was paused.");
                            }
                        }
                    };
                });
            }
        finally {
            monitor.Stop();
            choose.Stop();
            Require(monitorError is null, monitorError ?? "Progress ownership failed.");
            cancellationChosen.Set();
            Require(progressSeen && clickedCancel && choiceSeen && Visible,
                "Progress/cancellation UI check failed. Progress: " + progressSeen + "; clicked: " + clickedCancel + "; choice: " + choiceSeen + "; restored main: " + Visible);
            }
        }

    private void SaveScreenshot(string path) {
        using Bitmap image = new Bitmap(Width, Height);
        DrawToBitmap(image, new Rectangle(Point.Empty, Size));
        image.Save(path, ImageFormat.Png);
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
            }
        }
    }
