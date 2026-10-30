using Gtk;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Exercise popup visibility and real header clicks against disposable archive entries.</summary>
internal static class BrowserColumnsSmoke {
    internal static void Run(MainWindow host, string screenshots) {
        Gtk.Window fixtureWindow = host.TestWidget<Gtk.Window>("main_window");
        if (Mash.UI.MNXFile.GUI.Previews.PreviewWindow.IsWaylandDisplay(fixtureWindow.Display)) {
            // The preceding resize can extend below the work area. Wayland then
            // slides popups away from the requested corner. Keep this exact-anchor
            // fixture inside the work area instead of testing compositor placement.
            fixtureWindow.Maximize();
            Pump(50);
            }
        string directory = Path.Combine(screenshots, "browser-fixtures");
        Directory.CreateDirectory(directory);
        string archivePath = Path.Combine(directory, "browser.mnx");
        string imagePath = Path.Combine(directory, "picture.PNG");
        using (Gdk.Pixbuf image = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, false, 8, 100, 50)) {
            image.Fill(0x268beeff);
            image.Save(imagePath, "png");
            }
        DiskFile.WriteAllText(Path.Combine(directory, "z.TXT"), "A short note");
        DiskFile.WriteAllText(Path.Combine(directory, "a.txt"), "Another short note");
        using (FileStream large = DiskFile.Create(Path.Combine(directory, "large.bin"))) {
            large.SetLength(42_000_000);
            }
        using (ArchiveFile archive = new ArchiveFile()) {
            archive.Create(new FileInfo(archivePath));
            archive.AddDirectoryNow(Directory.CreateDirectory(Path.Combine(directory, "Folder")));
            archive.AddFileNow(new FileInfo(imagePath));
            archive.AddFileNow(new FileInfo(Path.Combine(directory, "z.TXT")));
            archive.AddFileNow(new FileInfo(Path.Combine(directory, "a.txt")));
            archive.AddFileNow(new FileInfo(Path.Combine(directory, "large.bin")));
            }
        host.OpenArchive(archivePath, showErrors: false);
        AppSettings settings = host.TestSettings;
        settings.Set("Previews", "Enabled", true);
        settings.Set("Previews", "Trigger", "Double-Click");
        settings.Set("Images", "Static", true);
        settings.Set("Presentation", "Customize", true);
        settings.Set("Presentation", "Mode", "Tooltip");
        settings.Set("Presentation", "Anchor", "Bottom-Right");
        TreeView list = host.TestWidget<TreeView>("file_list");
        using (TreePath imageRow = new TreePath("3")) {
            list.Selection.SelectPath(imageRow);
            if (!Mash.UI.MNXFile.GUI.Previews.PreviewWindow.IsWaylandDisplay(list.Display)) {
                Gdk.Rectangle cell = list.GetCellArea(imageRow, list.Columns[0]);
                list.ConvertBinWindowToWidgetCoords(cell.X + 200, cell.Y + cell.Height / 2, out int pointerX, out int pointerY);
                TransferSmoke.MoveMouse(list, pointerX, pointerY);
                }
            list.ActivateRow(imageRow, list.Columns[0]);
            }
        WaitFor(() => host.TestPreviewCount == 1, "Image tooltip opens after row activation");
        Pump(50);
        Check(host.TestPreviewCount == 1, "Bottom-right image tooltip survives display/focus events");
        if (!Mash.UI.MNXFile.GUI.Previews.PreviewWindow.IsWaylandDisplay(list.Display)) {
            TransferSmoke.MoveMouse(host.TestWidget<Gtk.Window>("main_window"), 20, 20);
            Check(host.TestPreviewCount == 1, "Click-triggered tooltip survives the pointer leaving the file list");
            }
        Gtk.Window popup = host.TestPreviews[0].Window;
        Gtk.Window parent = host.TestWidget<Gtk.Window>("main_window");
        Check(popup.Visible && popup.IsMapped, "Image tooltip maps on the desktop");
        popup.Window.GetOrigin(out int x, out int y);
        popup.GetSize(out int width, out int height);
        parent.Window.GetOrigin(out int parentX, out int parentY);
        parent.GetSize(out int parentWidth, out int parentHeight);
        parent.GetPosition(out int frameX, out int frameY);
        Console.WriteLine($"CHECK: image tooltip client={parentX},{parentY},{parentWidth},{parentHeight}; frame={frameX},{frameY}; popup={x},{y},{width},{height}");
        Capture(popup, Path.Combine(screenshots, "mnx-bottom-right-image.png"));
        Capture(parent, Path.Combine(screenshots, "mnx-bottom-right-composite.png"));
        Check(Math.Abs((width - 16) - 2 * (height - 16)) <= 2,
            "Wide image preview trims unused canvas while retaining its 2:1 aspect ratio");
        Console.WriteLine("CHECK: popup GDK type=" + popup.Window.WindowType);
        if (host.TestPreviews[0].UsesWayland) {
            popup.Window.GetPosition(out x, out y);
            Gdk.Rectangle clientArea = parent.Child.Allocation;
            parentX = clientArea.X;
            parentY = clientArea.Y;
            parentWidth = clientArea.Width;
            parentHeight = clientArea.Height;
            Console.WriteLine($"CHECK: Wayland client allocation={clientArea}, popup={x},{y},{width},{height}");
            }
        Check(Math.Abs(x + width - (parentX + parentWidth - 16)) < 6
            && Math.Abs(y + height - (parentY + parentHeight - 16)) < 6,
            "Image tooltip is anchored to the parent's bottom-right client corner");
        Capture(popup, Path.Combine(screenshots, "mnx-bottom-right-image.png"));
        if (host.TestPreviews[0].UsesWayland) {
            host.TestPreviews[0].Dispose();
            }
        else {
            TransferSmoke.MoveMouse(popup, width / 2, height / 2, click: true);
            }
        WaitFor(() => host.TestPreviewCount == 0, "Image tooltip click releases its window");

        Check(list.Columns.Any(column => column.Title == "Type"), "File type column exists");
        Check(list.Columns.Any(column => column.Title == "Chunks"), "Chunk count column exists");
        TreeViewColumn types = list.Columns.Single(column => column.Title == "Type");
        types.Click();
        Check(RowNames(list).SequenceEqual(new[] { "Folder", "large.bin", "picture.PNG", "a.txt", "z.TXT" }),
            "Type header groups mixed-case extensions and uses names as a stable tie-break");
        types.Click();
        Check(RowNames(list).SequenceEqual(new[] { "Folder", "z.TXT", "a.txt", "picture.PNG", "large.bin" }),
            "Type header reverses sorting while keeping directories first");
        TreeViewColumn chunks = list.Columns.Single(column => column.Title == "Chunks");
        chunks.Click();
        Check(RowNames(list)[4] == "large.bin", "Chunk sorting uses numbers rather than the abbreviated display text");
        chunks.Click();
        Check(RowNames(list)[1] == "large.bin", "Chunk header toggles descending");
        TreeViewColumn bytes = list.Columns.Single(column => column.Title == "Bytes");
        bytes.Click();
        Check(RowNames(list)[4] == "large.bin", "Byte sorting remains numeric");
        foreach (TreeViewColumn column in list.Columns) {
            Check(column.Clickable, "Clickable header: " + column.Title);
            }
        host.TestWidget<TreeViewColumn>("file_name_column").Click();
        Capture(parent, Path.Combine(screenshots, "mnx-browser-columns.png"));
        string? realArchive = Environment.GetEnvironmentVariable("MNX_GUI_PREVIEW_ARCHIVE");
        if (!string.IsNullOrEmpty(realArchive)) {
            host.OpenArchive(realArchive, showErrors: false);
            Mash.IO.Format.MNX.VFile.VFile file = host.TestArchive!.Files.First(entry => !entry.IsDirectory && entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            host.TestWidget<Entry>("current_path").Text = "/JOYREACTOR";
            host.TestWidget<Entry>("current_path").Activate();
            host.PreviewEntry(file.FileID);
            WaitFor(() => host.TestPreviewCount == 1, "Real archive bottom-right preview opens");
            Pump(30);
            Check(host.TestPreviews[0].Window.IsMapped, "Real archive image tooltip stays mapped");
            Capture(host.TestPreviews[0].Window, Path.Combine(screenshots, "mnx-real-archive-tooltip.png"));
            // Allow a compositor-level capture while the controlled preview remains open.
            if (Environment.GetEnvironmentVariable("MNX_GUI_PREVIEW_CAPTURE") == "1") {
                Pump(1500);
                }
            }
        host.TestWidget<MenuItem>("menu_close").Activate();
        Console.WriteLine("PASS: bottom-right image popup, case-insensitive type grouping, numeric chunk/size sorting and clickable headers");
        }

    private static void Capture(Gtk.Window window, string path) {
        // Native Wayland forbids reading arbitrary window pixels; use Spectacle
        // for compositor captures in that session. The Xvfb suite captures directly.
        if (!Mash.UI.MNXFile.GUI.Previews.PreviewWindow.IsWaylandDisplay(window.Display)) {
            CreationProgressSmoke.Screenshot(window, path);
            }
        }

    private static string[] RowNames(TreeView list) {
        List<string> names = new List<string>();
        if (list.Model.GetIterFirst(out TreeIter row)) {
            do {
                names.Add((string)list.Model.GetValue(row, 1));
                } while (list.Model.IterNext(ref row));
            }
        return names.ToArray();
        }

    private static void WaitFor(Func<bool> ready, string description) {
        for (int attempt = 0; attempt < 500 && !ready(); attempt++) {
            Pump(1);
            }
        Check(ready(), description);
        }

    private static void Pump(int count) {
        for (int step = 0; step < count; step++) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }
            Thread.Sleep(10);
            }
        }

    private static void Check(bool success, string description) {
        if (!success) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    }
