using System.Runtime.InteropServices;
using Gtk;
using Mash.GUI.MNXArchiver.Icons;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;
/// <summary>Optional integration probe: real GTK signals and a disposable tiny archive.</summary>
internal static class ScaffoldSmoke {
    public static void Run(MainWindow host, string screenshots) {
        Directory.CreateDirectory(screenshots);
        if (Environment.GetEnvironmentVariable("MNX_GUI_BROWSER_SMOKE_ONLY") == "1") {
            BrowserColumnsSmoke.Run(host, screenshots);
            return;
            }
        Window? window = host.TestWidget<Window>("main_window");
        Check(string.IsNullOrEmpty(host.TestWidget<ToolButton>("action_new").IconName), "Toolbar does not request desktop theme icons");
        Check(host.TestWidget<TreeView>("file_list").Model.GetColumnType(0) == Gdk.Pixbuf.GType, "File rows use embedded pixels instead of theme names");
        foreach (string name in OwnedIcons.Names) {
            foreach (int size in OwnedIcons.Sizes) {
                Gdk.Pixbuf pixels = OwnedIcons.Get(name, size);
                Check(pixels.Width == size && pixels.Height == size, "Embedded icon dimensions: " + name + "/" + size);
                Check(ReferenceEquals(pixels, OwnedIcons.Get(name, size)), "Icon cache reuses decoded pixels");
                }
            }
        Check(host.TestWidget<ToolButton>("action_new").IconWidget is Image, "Toolbar uses an embedded image");
        Check(host.TestWidget<Button>("navigate_up").Image is Image, "Navigation uses an embedded image");
        using (MessageDialog dialog = OwnedIcons.Dialog(window, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok, "Owned icon test")) {
#pragma warning disable CS0612
            Check(dialog.MessageType == MessageType.Warning, "Owned icons preserve message semantics");
            Check(dialog.Image is Image image && image.Pixbuf.Width == 48, "Dialogs use owned PNGs");
#pragma warning restore CS0612
            }
        Paned? split = host.TestWidget<Paned>("browser_split");
        TreeView? list = host.TestWidget<TreeView>("file_list");
        TreeView? tree = host.TestWidget<TreeView>("folder_tree");
        using (TargetList? targets = Gtk.Drag.DestGetTargetList(list)) {
            Check(
                targets is not null && targets.Find(Gdk.Atom.Intern("text/uri-list", false), out _),
                "File list accepts file-manager drops"
                );
            }

        using (TargetList? targets = Gtk.Drag.DestGetTargetList(tree)) {
            Check(
                targets is not null && targets.Find(Gdk.Atom.Intern("text/uri-list", false), out _),
                "Folder tree accepts file-manager drops"
                );
            }

        window.Move(0, 0);
        host.LoadPreview();
        Pump();
        Check(host.TestCurrentPath == "/" && host.TestListCount == 6, "Preview root contents");
        using (TreePath path = new TreePath("3")) {
            Check(list.Model.GetIter(out TreeIter row, path), "Preview manifest row exists");
            Check((string)list.Model.GetValue(row, 4) == "128,017", "Byte counts use consistent comma grouping");
            }
        Check(Math.Abs((double)split.Position / split.Allocation.Width - 0.30) < 0.03, "Initial 30/70 split");
        Screenshot(window, Path.Combine(screenshots, "mnx-main-window.png"));
        int initialHeight = list.Allocation.Height;
        int initialWidth = split.Allocation.Width;
        window.Resize(1400, 900);
        Pump();
        Check(
            split.Allocation.Width > initialWidth && list.Allocation.Height > initialHeight,
            "List grows on window resize"
            );
        split.Position = 470;
        Pump();
        Check(Math.Abs(split.Position - 470) < 5, "Divider can be moved");
        Screenshot(window, Path.Combine(screenshots, "mnx-main-window-resized.png"));
        // Actual list row activation opens a folder, then the Up button restores root.
        using (TreePath row = new TreePath("0")) {
            list.ActivateRow(row, list.Columns[0]);
            }

        Check(host.TestCurrentPath == "/Documents" && host.TestListCount == 2, "Folder double-click navigation");
        host.TestWidget<Button>("navigate_up").Click();
        Check(host.TestCurrentPath == "/", "Up navigation");
        // The preview tree begins with lazily populated child directories.
        using (TreePath path = new TreePath("0:1")) {
            tree.Selection.SelectPath(path);
            }

        Check(host.TestCurrentPath == "/Photos", "Tree selection navigation");
        Entry? entry = host.TestWidget<Entry>("current_path");
        entry.Text = "/Projects/MNXFile";
        entry.Activate();
        Check(host.TestListCount == 2 && host.TestCurrentPath == "/Projects/MNXFile", "Nested path navigation");
        using (TreePath row = new TreePath("0")) {
            list.Selection.SelectPath(row);
            }

        Check(host.TestWidget<ToolButton>("action_delete").Sensitive, "Selected row enables Delete hook");
        bool hookCalled = false;
        host.ActionRequested += (_, e) => hookCalled = e.Action == ArchiveAction.Delete && e.SelectedIds.Count == 1;
        // Menu activation exercises the same dispatch used by the toolbar button.
        host.TestWidget<MenuItem>("menu_delete").Activate();
        Check(hookCalled, "Action hook receives selection");
        string temp = Path.Combine(Path.GetTempPath(), "mnx-gui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            string source = Path.Combine(temp, "source");
            Directory.CreateDirectory(source);
            DiskFile.WriteAllText(Path.Combine(source, "hello.txt"), "Hello from the MNX GUI smoke test.");
            string archivePath = Path.Combine(temp, "test.mnx");
            using (Archive archive = new Archive()) {
                archive.Options.Compression = ArchiveCompression.Snappy;
                archive.Create(new FileInfo(archivePath), "test-passphrase");
                archive.AddDirectoryNow(new DirectoryInfo(source));
                }

            string renamedArchive = Path.Combine(temp, "archive without extension");
            DiskFile.Copy(archivePath, renamedArchive);
            Check(FileTransfer.IsArchive(renamedArchive), "Detect archive by signature without MNX extension");
            string fakeArchive = Path.Combine(temp, "ordinary.mnx");
            DiskFile.WriteAllText(fakeArchive, "This is ordinary input, not an MNX archive.");
            Check(!FileTransfer.IsArchive(fakeArchive), "Reject ordinary input despite MNX extension");
            Check(
                host.OpenArchive(archivePath, "test-passphrase", showErrors: false),
                "Open real encrypted archive"
                );
            Check(
                host.TestListCount == 1 && host.TestBrowser!.Compressed && host.TestBrowser.Encrypted,
                "Archive metadata and storage flags"
                );
            entry.Text = "/source";
            entry.Activate();
            Check(host.TestListCount == 1 && host.TestCurrentPath == "/source", "Real archive directory listing");
            using (TreePath row = new TreePath("0")) {
                list.Model.GetIter( out TreeIter item, row);
                Check((string)list.Model.GetValue(item, 1) == "hello.txt", "Real archive file name");
                Check((string)list.Model.GetValue(item, 3) == "[CE ]", "Compression/encryption indicators");
                }

            BrowserColumnsSmoke.Run(host, screenshots);
            OptionsPreviewSmoke.Run(host, temp, screenshots);
            host.OpenArchive(archivePath, "test-passphrase", showErrors: false);
            entry.Text = "/source";
            entry.Activate();
            Console.WriteLine("CHECK: drag/drop");
            TransferSmoke.Run(host, temp, archivePath);
            FileActionsSmoke.Run(host, temp);
            CreationProgressSmoke.Run(host, temp, screenshots);
            ExtractionSmoke.Run(host, temp, screenshots);
            CancellationSmoke.Run(host, temp, screenshots);
            host.OpenArchive(archivePath, "test-passphrase", showErrors: false);
            entry.Text = "/source";
            entry.Activate();
            bool failedOpen = false;
            try {
                host.OpenArchive(Path.Combine(temp, "missing.mnx"), showErrors: false);
                }
            catch (FileNotFoundException) {
                failedOpen = true;
                }

            Check(failedOpen && host.TestCurrentPath == "/source", "Failed open preserves current view");
            host.TestWidget<MenuItem>("menu_close").Activate();
            Check(
                host.TestListCount == 0 && !host.TestWidget<ToolButton>("action_add").Sensitive,
                "Close clears archive state"
                );
            }
        finally {
            Directory.Delete(temp, true);
            }
        }

    private static void Pump() {
        for (int i = 0; i < 60; i++) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }

            Thread.Sleep(5);
            }
        }

    // Capture the mapped window: Widget.Draw alone omits native child windows such
    // as TreeView column headers. This optional probe runs on Linux under Xvfb.
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_pixbuf_get_from_window(IntPtr window, int x, int y, int width, int height);
    private static void Screenshot(Window window, string path) {
        IntPtr raw = gdk_pixbuf_get_from_window(window.Window.Handle, 0, 0, window.Allocation.Width, window.Allocation.Height);
        if (raw == IntPtr.Zero) {
            throw new InvalidOperationException("Could not capture mapped GTK window.");
            }

        using Gdk.Pixbuf image = new Gdk.Pixbuf(raw);
        image.Save(path, "png");
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    }
