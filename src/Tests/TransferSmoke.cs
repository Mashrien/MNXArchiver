using System.Runtime.InteropServices;
using Gtk;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;
/// <summary>Exercise the real URI-list drag protocol on a disposable Xvfb desktop.</summary>
internal static class TransferSmoke {
    public static void Run(MainWindow host, string directory, string archivePath) {
        string source = Path.Combine(directory, "drop source");
        Directory.CreateDirectory(source);
        string file = Path.Combine(source, "20261004-014252 # 100% ü.png");
        byte[] contents = new byte[100_003];
        new Random(42).NextBytes(contents);
        DiskFile.WriteAllBytes(file, contents);
        string folder = Path.Combine(source, "dropped folder");
        Directory.CreateDirectory(Path.Combine(folder, "empty"));
        DiskFile.WriteAllText(Path.Combine(folder, "nested.txt"), "A distinct nested file.");
        string linkedFile = Path.Combine(source, "linked-file");
        string linkedDirectory = Path.Combine(folder, "linked-directory");
        string danglingLink = Path.Combine(folder, "dangling-link");
        DiskFile.CreateSymbolicLink(linkedFile, file);
        Directory.CreateSymbolicLink(linkedDirectory, folder);
        DiskFile.CreateSymbolicLink(danglingLink, Path.Combine(directory, "missing-target"));
        Check(
            FileTransfer.LocalPaths([linkedFile, linkedDirectory, danglingLink, file]).SequenceEqual([file]),
            "GUI silently excludes file, directory and dangling links"
            );
        host.OpenInputs([danglingLink]);
        Check(
            FileTransfer.LocalPath(new Uri(file).AbsoluteUri) == file,
            "File URI preserves spaces, percent, hash and Unicode"
            );
        Check(
            FileTransfer.LocalPaths([new Uri(folder).AbsoluteUri, new Uri(Path.Combine(folder, "nested.txt")).AbsoluteUri]).Length == 1,
            "Parent/child drops avoid duplicate ingestion"
            );
        Window peer = new Window("Disposable file-manager drag peer");
        Box box = new Box(Orientation.Vertical, 4);
        EventBox dragSource = new EventBox {
            HeightRequest = 80
            };
        dragSource.Add(new Label("Drag real input files into MNX"));
        EventBox dragDestination = new EventBox {
            HeightRequest = 80
            };
        dragDestination.Add(new Label("Drop archived entries here"));
        box.PackStart(dragSource, true, true, 0);
        box.PackStart(dragDestination, true, true, 0);
        peer.Add(box);
        peer.SetDefaultSize(320, 220);
        peer.Move(1500, 50);
        peer.ShowAll();
        TargetEntry[]? targets = new[] {
            new TargetEntry("text/uri-list", 0, 1)
            };
        string[] incoming = [new Uri(linkedFile).AbsoluteUri, new Uri(danglingLink).AbsoluteUri, new Uri(file).AbsoluteUri, new Uri(folder).AbsoluteUri];
        Drag.SourceSet(dragSource, Gdk.ModifierType.Button1Mask, targets, Gdk.DragAction.Copy);
        dragSource.DragDataGet += (_, args) => {
            args.SelectionData.SetUris(incoming);
            };
        string[]? exported = null;
        Drag.DestSet(dragDestination, DestDefaults.All, targets, Gdk.DragAction.Copy);
        dragDestination.DragDataReceived += (_, args) => {
            exported = args.SelectionData.Uris;
            Drag.Finish(args.Context, exported is not null && exported.Length > 0, false, args.Time);
            };
        Entry? pathEntry = host.TestWidget<Entry>("current_path");
        pathEntry.Text = "/source";
        pathEntry.Activate();
        Pump();
        TreeView? list = host.TestWidget<TreeView>("file_list");
        // An empty area must never become a drag source, even with an old selection.
        list.Selection.UnselectAll();
        DragGesture(Point(list, 220, 180), Point(list, 260, 220));
        Check(host.TestListCount == 1, "Blank list drag without selection stays inside the control");
        using (TreePath existing = new TreePath("0")) {
            list.Selection.SelectPath(existing);
            }
        DragGesture(Point(list, 220, 180), Point(list, 260, 220));
        Check(host.TestListCount == 1, "Blank list drag with selection does not export files");
        DragGesture(Center(dragSource), Point(list, 220, 180));
        Check(host.TestListCount == 3, "Real file-manager drop imports file and folder into current path");
        Guid fileId = host.TestBrowser!.Children(host.TestBrowser.Resolve("/source")).Single(item => item.Name == Path.GetFileName(file)).Id;
        Guid folderId = host.TestBrowser.Children(host.TestBrowser.Resolve("/source")).Single(item => item.Name == "dropped folder").Id;
        string listFile = Path.Combine(source, "list-target.txt");
        DiskFile.WriteAllText(listFile, "Target a folder through the file list.");
        incoming = [new Uri(listFile).AbsoluteUri];
        using (TreePath row = new TreePath("0")) {
            Gdk.Rectangle area = list.GetCellArea(row, list.Columns[0]);
            list.BinWindow.GetOrigin( out int x, out int y);
            DragGesture(Center(dragSource), (x + area.X + 100, y + area.Y + area.Height / 2));
            }

        Check(
            host.TestBrowser.Children(folderId).Any(item => item.Name == "list-target.txt"),
            "List drop targets the folder row"
            );
        pathEntry.Text = "/source";
        pathEntry.Activate();
        Pump();
        // Dropping onto a tree folder targets that folder, not the current listing.
        string treeFile = Path.Combine(source, "tree-only.txt");
        DiskFile.WriteAllText(treeFile, "Target a folder through the tree.");
        incoming = [new Uri(treeFile).AbsoluteUri];
        TreeView? tree = host.TestWidget<TreeView>("folder_tree");
        using (TreePath treePath = new TreePath("0:0:0")) {
            Gdk.Rectangle area = tree.GetCellArea(treePath, tree.Columns[0]);
            tree.BinWindow.GetOrigin( out int x, out int y);
            DragGesture(Center(dragSource), (x + area.X + 60, y + area.Y + area.Height / 2));
            }

        Check(
            host.TestBrowser.Children(folderId).Any(item => item.Name == "tree-only.txt"),
            "Tree drop targets the folder row"
            );
        pathEntry.Text = "/source";
        pathEntry.Activate();
        Pump();
        // The list is sorted: folder first, timestamp PNG next, hello.txt last.
        list.Selection.UnselectAll();
        using (TreePath row = new TreePath("1")) {
            list.Selection.SelectPath(row);
            Gdk.Rectangle area = list.GetCellArea(row, list.Columns[0]);
            list.BinWindow.GetOrigin( out int x, out int y);
            DragGesture((x + area.X + 100, y + area.Y + area.Height / 2), Center(dragDestination));
            }

        Check(
            exported is not null && exported.Length == 1,
            "List exports file URI through actual GTK drag signals"
            );
        Check(
            DiskFile.ReadAllBytes(FileTransfer.LocalPath(exported![0])).SequenceEqual(contents),
            "Exported bytes match the unique source"
            );
        Check(DiskFile.ReadAllBytes(file).SequenceEqual(contents), "Import preserves the source file");
        exported = null;
        using (TreePath first = new TreePath("1")) {
            using TreePath second = new TreePath("2");
            list.Selection.SelectPath(first);
            list.Selection.SelectPath(second);
            Gdk.Rectangle area = list.GetCellArea(first, list.Columns[0]);
            list.BinWindow.GetOrigin( out int x, out int y);
            DragGesture((x + area.X + 100, y + area.Y + area.Height / 2), Center(dragDestination));
            }

        Check(
            exported is not null && exported.Length == 2,
            "A multi-selection drag exports both selected files"
            );
        exported = null;
        using (TreePath row = new TreePath("0:0:0")) {
            tree.Selection.SelectPath(row);
            Gdk.Rectangle area = tree.GetCellArea(row, tree.Columns[0]);
            tree.BinWindow.GetOrigin( out int x, out int y);
            DragGesture((x + area.X + 60, y + area.Y + area.Height / 2), Center(dragDestination));
            }

        Check(
            exported is not null && exported.Length == 1,
            "Folder tree exports folder URI through actual GTK drag signals"
            );
        string output = FileTransfer.LocalPath(exported![0]);
        Check(
            DiskFile.ReadAllText(Path.Combine(output, "nested.txt")) == "A distinct nested file." && Directory.Exists(Path.Combine(output, "empty")),
            "Folder drag preserves descendants and empty directories"
            );
        using (ArchiveFile archive = new ArchiveFile()) {
            archive.Open(archivePath, "test-passphrase");
            bool rejected = false;
            try {
                FileTransfer.Import(archive, [file], host.TestBrowser.Resolve("/source"));
                }
            catch (IOException) {
                rejected = true;
                }

            Check(rejected, "Duplicate import fails rather than replacing existing data");
            using Stream stream = archive.OpenRead(fileId);
            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Check(
                buffer.ToArray().SequenceEqual(contents),
                "Drag export and duplicate import preserve archive bytes"
                );
            }

        peer.Dispose();
    // Deliberately leave exported files available across DragEnd; the host
    // lease protects them until disposal and stale cleanup handles them later.
        }

    private static (int X, int Y) Center(Gtk.Widget widget) {
        return Point(widget, widget.Allocation.Width / 2, widget.Allocation.Height / 2);
        }

    private static (int X, int Y) Point(Gtk.Widget widget, int x, int y) {
        widget.Toplevel.Window.GetOrigin( out int rootX, out int rootY);
        widget.TranslateCoordinates(widget.Toplevel, x, y, out int localX, out int localY);
        return (rootX + localX, rootY + localY);
        }

    private static void DragGesture((int X, int Y) start, (int X, int Y) end) {
        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) {
            throw new InvalidOperationException("Drag smoke requires an X11 virtual display.");
            }

        try {
            XTestFakeMotionEvent(display, -1, start.X, start.Y, 0);
            XFlush(display);
            Pump();
            XTestFakeButtonEvent(display, 1, 1, 0);
            XFlush(display);
            Pump();
            for (int step = 1; step <= 20; step++) {
                XTestFakeMotionEvent(display, -1, start.X + (end.X - start.X) * step / 20, start.Y + (end.Y - start.Y) * step / 20, 0);
                XFlush(display);
                Pump();
                }

            XTestFakeButtonEvent(display, 1, 0, 0);
            XFlush(display);
            Pump();
            }
        finally {
            XCloseDisplay(display);
            }
        }

    internal static void MoveMouse(Gtk.Widget widget, int x, int y, bool click = false) {
        (int rootX, int rootY) = Point(widget, x, y);
        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) {
            throw new InvalidOperationException("The GUI pointer smoke test requires X11.");
            }
        try {
            XTestFakeMotionEvent(display, -1, rootX, rootY, 0);
            if (click) {
                XTestFakeButtonEvent(display, 1, 1, 0);
                XTestFakeButtonEvent(display, 1, 0, 0);
                }
            XFlush(display);
            Pump();
            }
        finally {
            XCloseDisplay(display);
            }
        }

    internal static void RightClick(TreeView view, TreePath path) {
        Pump();
        Gdk.Rectangle area = view.GetCellArea(path, view.Columns[0]);
        view.BinWindow.GetOrigin( out int x, out int y);
        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) {
            throw new InvalidOperationException("Context-menu smoke requires an X11 virtual display.");
            }

        try {
            XTestFakeMotionEvent(display, -1, x + area.X + 100, y + area.Y + area.Height / 2, 0);
            XTestFakeButtonEvent(display, 3, 1, 0);
            XTestFakeButtonEvent(display, 3, 0, 0);
            XFlush(display);
            Pump();
            }
        finally {
            XCloseDisplay(display);
            }
        }

    internal static void DropFile(TreeView view, string path) {
        Window peer = new Window("Disposable file-manager drag peer");
        EventBox source = new EventBox();
        source.Add(new Label("Drag file"));
        peer.Add(source);
        peer.SetDefaultSize(200, 120);
        peer.Move(1500, 50);
        TargetEntry[] targets = [new TargetEntry("text/uri-list", 0, 1)];
        Drag.SourceSet(source, Gdk.ModifierType.Button1Mask, targets, Gdk.DragAction.Copy);
        source.DragDataGet += (_, args) => args.SelectionData.SetUris([new Uri(path).AbsoluteUri]);
        peer.ShowAll();
        try {
            Pump();
            DragGesture(Center(source), Point(view, 220, 180));
            }
        finally {
            peer.Destroy();
            }
        }

    private static void Pump() {
        for (int i = 0; i < 12; i++) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }

            Thread.Sleep(5);
            }
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);
    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeMotionEvent(IntPtr display, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeButtonEvent(IntPtr display, uint button, int pressed, nuint delay);
    }
