using static MNX.Archive;
using System.Security.Cryptography;
using System.Text;
using Gtk;
using Mash.GUI.MNXArchiver.Settings;
using Mash.GUI.MNXArchiver.Previews;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

internal static class OptionsPreviewSmoke {
    private static T? FindSetting<T>(Widget widget, string name) where T : Widget {
        if (widget is T match && widget.Name == name) {
            return match;
            }
        if (widget is Container container) {
            foreach (Widget child in container.Children) {
                T? nested = FindSetting<T>(child, name);
                if (nested is not null) {
                    return nested;
                    }
                }
            }
        return null;
        }

    private static void CheckPreviewOverrides(MainWindow host, string directory) {
        AppSettings settings = new AppSettings(Path.Combine(directory, "override-options"));
        settings.Set("Previews", "Enabled", true);
        settings.Set("MNX.Previews.OVERRIDE", "BufferMiB", 512);
        settings.Set("MNX.Previews.OVERRIDE", "DecodedMiB", 5120);
        settings.Set("MNX.Previews.OVERRIDE", "TotalMiB", 8192);
        settings.Set("MNX.Previews.OVERRIDE", "DebugFlags", 1);
        settings.Save();
        using (OptionsDialog options = new OptionsDialog(host.TestWidget<Window>("main_window"), settings)) {
            string[] keys = { "BufferMiB", "DecodedMiB", "TotalMiB" };
            double[] maxima = { 128, 1024, 2048 };
            for (int index = 0; index < keys.Length; index++) {
                SpinButton numeric = FindSetting<SpinButton>(options.Window.ContentArea, "previews" + keys[index] + "NumericUpDown")!;
                Check(!numeric.Sensitive && numeric.Adjustment.Upper == maxima[index], "GTK3 override leaves ordinary range disabled and unchanged");
                }
            GLib.Idle.Add(() => { options.Window.Respond(ResponseType.Accept); return false; });
            Check(options.Run()!.TotalPreviewBytes == 8192L * 1024 * 1024, "GTK3 Save preserves large overrides");
            }
        using (OptionsDialog options = new OptionsDialog(host.TestWidget<Window>("main_window"), AppSettings.Load(settings.DirectoryPath))) {
            GLib.Idle.Add(() => { FindSetting<Button>(options.Window.ContentArea, "revertPreviewOverridesButton")!.Click(); return false; });
            Check(options.Run() is null && options.ExitRequested && !AppSettings.Load(settings.DirectoryPath).HasPreviewOverrides,
                "GTK3 Revert removes the whole override category and requests application exit");
            }
        }
    private static ComboBoxText? FindBackendChoice(Widget widget) {
        if (widget is ComboBoxText choice && choice.Name == "interfaceBackendComboBox") {
            return choice;
            }
        if (widget is Container container) {
            foreach (Widget child in container.Children) {
                ComboBoxText? nested = FindBackendChoice(child);
                if (nested is not null) {
                    return nested;
                    }
                }
            }
        return null;
        }

    private static SpinButton? FindOpacityControl(Widget widget) {
        if (widget is SpinButton opacity && opacity.Name == "interfaceOpacityNumericUpDown") {
            return opacity;
            }
        if (widget is Container container) {
            foreach (Widget child in container.Children) {
                SpinButton? nested = FindOpacityControl(child);
                if (nested is not null) {
                    return nested;
                    }
                }
            }
        return null;
        }

    private static void CheckInterfaceOptions(MainWindow host, string config) {
        Backends.BackendAvailability? available = Backends.BackendRuntime.Availability;
        if (available is null) {
            return;
            }
        AppSettings original = AppSettings.Load(config);
        original.Set("Interface", "Backend", "auto");
        original.Set("Interface", "OpacityPercent", 100);
        original.Save();
        using (OptionsDialog cancelled = new OptionsDialog(host.TestWidget<Window>("main_window"), original)) {
            ComboBoxText? choice = FindBackendChoice(cancelled.Window.ContentArea);
            Check(choice is not null, "GTK3 options include the interface selector");
            Check(choice!.Sensitive == (available.MWinAvailable && available.Gtk3Available), "Unavailable backend selection is disabled");
            choice.ActiveId = "mwin";
            FindOpacityControl(cancelled.Window.ContentArea)!.Value = 65;
            GLib.Idle.Add(() => { cancelled.Window.Respond(ResponseType.Cancel); return false; });
            Check(cancelled.Run() is null, "Options cancel returns no changes");
            }
        Check(Backends.BackendRuntime.LoadPreference(config) == Backends.GuiBackend.Automatic, "Cancelling options does not persist interface changes");
        Check(AppSettings.Load(config).OpacityPercent == 100, "Cancelling options does not persist opacity changes");
        if (available.MWinAvailable && available.Gtk3Available) {
            using OptionsDialog saved = new OptionsDialog(host.TestWidget<Window>("main_window"), original);
            FindBackendChoice(saved.Window.ContentArea)!.ActiveId = "mwin";
            FindOpacityControl(saved.Window.ContentArea)!.Value = 65;
            GLib.Idle.Add(() => { saved.Window.Respond(ResponseType.Accept); return false; });
            Check(saved.Run() is not null, "Options save accepts a backend choice");
            Check(AppSettings.Load(config).OpacityPercent == 65, "GTK3 options persist window opacity");
            Check(Backends.BackendRuntime.LoadPreference(config) == Backends.GuiBackend.MWin, "GTK3 options persist the selected backend");
            Check(AppSettings.Load(config).Get("Future", "PreserveMe", "") == "value=with#punctuation", "Backend save preserves unrelated INI values");
            }
        }

    internal static void Run(MainWindow host, string directory, string screenshots) {
        CheckPreviewOverrides(host, directory);
        Console.WriteLine("CHECK: INI options, key envelopes and bounded previews");
        string invalidArchive = Path.Combine(directory, "invalid.MNX");
        DiskFile.WriteAllText(invalidArchive, "This is not an archive.");
        Check(Program.IsArchiveOpenRequest([invalidArchive]), "Invalid MNX startup input requests archive parsing rather than creation");
        Check(!Program.IsArchiveOpenRequest([invalidArchive, Path.Combine(directory, "ordinary.txt")]), "Multiple supplied inputs remain an archive-creation request");
        Reject(() => host.OpenArchive(invalidArchive, showErrors: false), "Invalid archive rejected by the actual parser");
        string parseMessage = MainWindow.ArchiveOpenError(invalidArchive, new InvalidDataException("Invalid header"), ReturnCode.Failed | ReturnCode.ParseFailed);
        Check(parseMessage.Contains("Failed, ParseFailed") && parseMessage.Contains("Invalid header"), "Archive error dialog includes named parser status flags");
        string config = Path.Combine(directory, "private settings");
        AppSettings settings = new AppSettings(config);
        settings.Set("Previews", "Enabled", true);
        settings.Set("Previews", "DelayMs", 20000);
        settings.Set("General", "TempPath", directory);
        settings.Set("Future", "PreserveMe", "value=with#punctuation");
        settings.Save();
        AppSettings loaded = AppSettings.Load(config);
        Check(loaded.PreviewBufferBytes == 32 * 1024 * 1024, "Preview buffer defaults to 32 MiB");
        Check(loaded.DecodedPreviewBytes == 512L * 1024 * 1024 && loaded.TotalPreviewBytes == 1024L * 1024 * 1024,
            "Decoded and combined memory defaults are independent of the encoded input buffer");
        loaded.Set("Previews", "BufferMiB", 1);
        Check(loaded.PreviewBufferBytes == 16 * 1024 * 1024, "Preview buffer clamps to 16 MiB minimum");
        loaded.Set("Previews", "BufferMiB", 999);
        Check(loaded.PreviewBufferBytes == 128 * 1024 * 1024, "Preview buffer clamps to 128 MiB maximum");
        loaded.Set("Previews", "BufferMiB", 32);
        Check(loaded.Previews && loaded.HoverDelay == 2000 && loaded.Get("Future", "PreserveMe", "") == "value=with#punctuation", "INI round trip, unknown keys and bounds");
        Check(AppSettings.Extensions(" .TXT,cs, txt, .c++ ") == "c++,cs,txt", "Extension normalization");
        Reject(() => AppSettings.Extensions("txt,../secret"), "Extension traversal rejected");
        AppSettings.ValidateTemp(directory);
        Reject(() => AppSettings.ValidateTemp(Path.Combine(directory, "missing")), "Missing temporary folder rejected");
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] plain = Encoding.UTF8.GetBytes("not in the INI ü password");
        byte[] encrypted = DefaultKeyStore.Encrypt(plain, key, []);
        Check(DefaultKeyStore.Decrypt(encrypted, key).SequenceEqual(plain), "AES-256 envelope round trip");
        encrypted[encrypted.Length - 1] ^= 1;
        Reject(() => DefaultKeyStore.Decrypt(encrypted, key), "Modified encrypted default rejected");
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plain);
        Check(!DiskFile.ReadAllText(settings.ConfigPath).Contains("password" + " ü"), "INI contains no password");
        Console.WriteLine("CHECK: options construction");
        using (OptionsDialog options = new OptionsDialog(host.TestWidget<Window>("main_window"), loaded)) {
            options.Window.ShowAll();
            Notebook? tabs = options.Window.ContentArea.Children.OfType<Notebook>().SingleOrDefault();
            Check(tabs is not null && tabs.NPages == 4, "Options have four separate category tabs");
            for (int page = 0; page < 4; page++) {
                tabs!.CurrentPage = page;
                Pump(10);
                Screenshot(options.Window, Path.Combine(screenshots, "mnx-options-tab-" + page + ".png"));
                }
            tabs!.CurrentPage = 0;
            Pump(10);
            Screenshot(options.Window, Path.Combine(screenshots, "mnx-options.png"));
            options.Window.Hide();
            }
        CheckInterfaceOptions(host, config);
        GtkWindowAppearance.Configure(65);
        using (OptionsDialog translucent = new OptionsDialog(host.TestWidget<Window>("main_window"), loaded)) {
            Check(Math.Abs(translucent.Window.Opacity - 0.65) < 0.001, "Owned options inherit configured opacity");
            }
        using (MessageDialog error = Icons.OwnedIcons.Dialog(host.TestWidget<Window>("main_window"), DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, "Opacity error fixture")) {
            Check(Math.Abs(error.Opacity - 1.0) < 0.001, "Errors remain fully opaque");
            }
        using (MessageDialog question = Icons.OwnedIcons.Dialog(host.TestWidget<Window>("main_window"), DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, "Opacity question fixture")) {
            Check(Math.Abs(question.Opacity - 0.65) < 0.001, "Confirmation dialogs inherit configured opacity");
            }
        GtkWindowAppearance.Configure(100);
        Console.WriteLine("CHECK: preview fixture creation");
        string archivePath = Path.Combine(directory, "previews.mnx");
        string textPath = Path.Combine(directory, "preview.txt");
        DiskFile.WriteAllText(textPath, "Private preview text — ü\nRead without extraction.");
        string imagePath = Path.Combine(directory, "preview.png");
        using (Gdk.Pixbuf image = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, true, 8, 100, 50)) {
            image.Fill(0x276fabff);
            image.Save(imagePath, "png");
            }
        string animationPath = Path.Combine(directory, "preview.gif");
        byte[] gif = Convert.FromBase64String("R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAFAAAACwAAAAABAAEAAAICQABCBxIsCCAgAAh+QQBFAABACwAAAAABAAEAIEAAP8AAAAAAAAAAAAICQABCBxIsCCAgAA7");
        DiskFile.WriteAllBytes(animationPath, gif);
        AppSettings reducedSettings = settings.Copy();
        reducedSettings.Set("Images", "Animated", true);
        reducedSettings.Set("MNX.Previews.OVERRIDE", "DebugFlags", 1);
        using (PreviewWindow reduced = new PreviewWindow(host.TestWidget<Window>("main_window"), "half.gif",
            new PreviewData(gif.ToArray(), PreviewKind.Image, false), reducedSettings, new Gdk.Rectangle(10, 10, 10, 10), 10, 10)) {
            Check(reduced.ImageSize == (2, 2) && reduced.IsAnimating, "GTK3 uses half-size cached GIF pixels and preserves animation");
            }
        using (PreviewWindow reduced = new PreviewWindow(host.TestWidget<Window>("main_window"), "half.png",
            new PreviewData(DiskFile.ReadAllBytes(imagePath), PreviewKind.Image, false), reducedSettings, new Gdk.Rectangle(10, 10, 10, 10), 10, 10)) {
            Check(reduced.ImageSize == (50, 25), "GTK3 static previews use reduced pixel buffers");
            }
        Check(PreviewData.CheckGif(gif, true) == 2, "GIF frames counted before native decode");
        Reject(() => PreviewData.CheckGif(gif, false), "Animation opt-in enforced");
        byte[] giantGif = gif.ToArray();
        giantGif[6] = 255;
        giantGif[7] = 255;
        giantGif[8] = 255;
        giantGif[9] = 255;
        Reject(() => PreviewData.CheckGif(giantGif, true), "Tiny GIF with huge decoded canvas rejected");
        string oversized = Path.Combine(directory, "oversized.txt");
        using (FileStream file = DiskFile.Create(oversized)) {
            file.SetLength(PreviewData.InputLimit + 1L);
            }
        using (Archive archive = new Archive()) {
            archive.Create(new FileInfo(archivePath));
            archive.AddFileNow(new FileInfo(textPath));
            archive.AddFileNow(new FileInfo(imagePath));
            archive.AddFileNow(new FileInfo(oversized));
            archive.AddFileNow(new FileInfo(animationPath));
            Guid textId = archive.Files.Single(file => file.Name == "preview.txt").FileID;
            using PreviewData data = PreviewData.Read(archive, textId, PreviewKind.Text, CancellationToken.None);
            Check(data.Text().Contains("Private preview text"), "Archive text read in memory");
            Guid largeId = archive.Files.Single(file => file.Name == "oversized.txt").FileID;
            Reject(() => PreviewData.Read(archive, largeId, PreviewKind.Text, CancellationToken.None), "Configured default 32 MiB hard input cap");
            using CancellationTokenSource cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Reject(() => PreviewData.Read(archive, textId, PreviewKind.Text, cancelled.Token), "Cancelled preview read stops");
            }
        Check(PreviewWindow.ImageBounds("Scale", 100, 50, 200, 200) == (0, 50, 200, 100), "Scale preserves aspect fit");
        Check(PreviewWindow.ImageBounds("Zoom", 100, 50, 200, 200) == (-100, 0, 400, 200), "Zoom preserves aspect fill");
        Check(PreviewWindow.ImageBounds("Stretch", 100, 50, 200, 200) == (0, 0, 200, 200), "Stretch fills both axes");
        Check(PreviewWindow.ImageBounds("Center", 100, 50, 200, 200) == (50, 75, 100, 50), "Center retains original pixels");
        Console.WriteLine("CHECK: preview window lifecycle");
        host.OpenArchive(archivePath, showErrors: false);
        AppSettings hostSettings = host.TestSettings;
        hostSettings.Set("Previews", "Enabled", true);
        hostSettings.Set("Presentation", "Customize", true);
        hostSettings.Set("Presentation", "Mode", "Window");
        hostSettings.Set("Presentation", "WindowType", "Window");
        hostSettings.Set("Presentation", "RelativeTo", "Current Screen");
        Guid textEntry = host.TestArchive!.Files.Single(file => file.Name == "preview.txt").FileID;
        Guid imageEntry = host.TestArchive.Files.Single(file => file.Name == "preview.png").FileID;
        string[] before = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        host.PreviewEntry(textEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Independent text preview opened");
        host.PreviewEntry(imageEntry);
        Pump(100);
        WaitFor(() => host.TestPreviewCount == 2, "Multiple independent previews coexist");
        Check(host.TestPreviews.All(preview => preview.Independent), "Screen-relative Window previews are independent");
        Screenshot(host.TestPreviews[1].Window, Path.Combine(screenshots, "mnx-image-preview.png"));
        Check(before.SequenceEqual(Directory.GetFiles(directory, "*", SearchOption.AllDirectories)), "Previews create no temporary files");
        host.OpenArchive(archivePath, showErrors: false);
        hostSettings.Set("Previews", "Trigger", "Single-Click");
        hostSettings.Set("Presentation", "WindowType", "Dialog");
        hostSettings.Set("Presentation", "Origin", "Right of Main Window");
        TreeView clickList = host.TestWidget<TreeView>("file_list");
        Pump(10);
        TreePath? textRow = null;
        clickList.Model.Foreach((model, path, row) => {
            if ((string)model.GetValue(row, 6) == textEntry.ToString()) {
                textRow = new TreePath(path.ToString());
                return true;
                }
            return false;
            });
        Check(textRow is not null, "Single-click test locates its text row by ID after sorting");
        using (TreePath clickedPath = textRow!) {
            Gdk.Rectangle cell = clickList.GetCellArea(clickedPath, clickList.Columns[0]);
            clickList.ConvertBinWindowToWidgetCoords(cell.X + 50, cell.Y + cell.Height / 2, out int clickX, out int clickY);
            TransferSmoke.MoveMouse(clickList, clickX, clickY, click: true);
            WaitFor(() => host.TestPreviewCount == 1, "A single pointer click opens one preview");
            Gtk.Window parent = host.TestWidget<Gtk.Window>("main_window");
            Gtk.Window preview = host.TestPreviews[0].Window;
            parent.Window.GetOrigin(out int parentX, out int parentY);
            parent.GetSize(out int parentWidth, out _);
            preview.Window.GetOrigin(out int previewX, out int previewY);
            Check(previewX == parentX + parentWidth && previewY == parentY,
                "Right-side dialog is mapped touching its parent's right edge");
            Check(!preview.Decorated && !preview.Resizable, "Single-click dialog uses fixed minimal chrome");
            }
        host.OpenArchive(archivePath, showErrors: false);
        hostSettings.Set("Previews", "Trigger", "Double-Click");
        hostSettings.Set("Presentation", "WindowType", "Window");
        hostSettings.Set("Presentation", "Origin", "Center");
        host.PreviewEntry(textEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Text preview reopens after single-click test");
        host.PreviewEntry(imageEntry);
        WaitFor(() => host.TestPreviewCount == 2, "Image preview reopens after single-click test");
        Console.WriteLine("CHECK: preview disposal");
        PreviewWindow imageWindow = host.TestPreviews[1];
        TransferSmoke.MoveMouse(imageWindow.Window, 40, 40, click: true);
        Check(host.TestPreviewCount == 1, "Closing an image releases only its own window");
        host.OpenArchive(archivePath, showErrors: false);
        Check(host.TestPreviewCount == 0, "Archive replacement releases all children");
        hostSettings.Set("Presentation", "WindowType", "Dialog");
        host.PreviewEntry(textEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Dialog preview opens");
        Check(!host.TestPreviews[0].Window.Decorated && !host.TestPreviews[0].Window.Resizable,
            "Dialog preview has no native frame and cannot resize");
        TransferSmoke.MoveMouse(host.TestPreviews[0].Window, 40, 40, click: true);
        Check(host.TestPreviewCount == 0, "A dialog closes when its text area is clicked");
        host.PreviewEntry(textEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Focus-loss dialog opens");
        host.TestPreviews[0].Window.Window.Focus(0);
        Pump(5);
        host.TestWidget<Window>("main_window").Window.Focus(0);
        WaitFor(() => host.TestPreviewCount == 0, "Dialog closes on focus loss");
        hostSettings.Set("Presentation", "WindowType", "Window");
        hostSettings.Set("Images", "Animated", true);
        Guid animationEntry = host.TestArchive.Files.Single(file => file.Name == "preview.gif").FileID;
        host.PreviewEntry(animationEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Animated GIF opens internally");
        Pump(50);
        host.TestPreviews[0].Dispose();
        for (int window = 0; window < MainWindow.MaxPreviewWindows; window++) {
            host.PreviewEntry(textEntry);
            int expected = window + 1;
            WaitFor(() => host.TestPreviewCount == expected, "Independent preview count " + expected);
            }
        host.PreviewEntry(textEntry);
        Pump(10);
        Check(host.TestPreviewCount == 10, "Ten-window hard limit enforced");
        host.OpenArchive(archivePath, showErrors: false);
        hostSettings.Set("Presentation", "Mode", "Tooltip");
        hostSettings.Set("Presentation", "Anchor", "Bottom-Right");
        hostSettings.Set("Previews", "Trigger", "Double-Click");
        host.PreviewEntry(textEntry);
        WaitFor(() => host.TestPreviewCount == 1, "Click-triggered tooltip opens");
        Check(!host.TestPreviews[0].Window.Decorated, "Tooltip is an undecorated form");
        CheckBottomRightPreview(host, screenshots);
        TransferSmoke.MoveMouse(host.TestPreviews[0].Window, 40, 40, click: true);
        Check(host.TestPreviewCount == 0, "Click-triggered tooltip closes on a click");
        hostSettings.Set("Previews", "Trigger", "Mouse-Over With Delay");
        hostSettings.Set("Previews", "DelayMs", 500);
        TreeView list = host.TestWidget<TreeView>("file_list");
        using (TreePath row = new TreePath("3")) {
            Gdk.Rectangle cell = list.GetCellArea(row, list.Columns[0]);
            list.ConvertBinWindowToWidgetCoords(cell.X + 20, cell.Y + cell.Height / 2, out int x, out int y);
            TransferSmoke.MoveMouse(list, x, y);
            WaitFor(() => host.TestPreviewCount == 1, "Delayed hover opens a tooltip");
            Pump(80);
            Check(host.TestPreviewCount == 1, "Bottom-right hover preview stays open over its row");
            CheckBottomRightPreview(host, screenshots);
            TransferSmoke.MoveMouse(list, 50, 250);
            WaitFor(() => host.TestPreviewCount == 0, "Leaving the item destroys its hover preview");
            }
        hostSettings.Set("Previews", "Enabled", false);
        using (TreePath imageRow = new TreePath("2")) {
            list.Selection.UnselectAll();
            list.Selection.SelectPath(imageRow);
            }
        DeleteWithAnswer(host, ResponseType.No);
        Check(host.TestArchive.Files.Count == 4, "Declining deletion preserves all entries");
        DeleteWithAnswer(host, ResponseType.Yes);
        Check(host.TestArchive.Files.Count == 3 && host.TestArchive.GetFile(imageEntry) is null, "Confirmed deletion removes only the selected entry");
        Check(host.TestArchive.VerifyArchive() == ReturnCode.Success, "Archive verifies after context deletion");
        CheckArchiveAnimations(host, screenshots);
        Console.WriteLine("PASS: editable settings, authenticated key envelope, input limits, image geometry, independent windows and cleanup");
        }

    private static void CheckBottomRightPreview(MainWindow host, string screenshots) {
        Pump(10);
        Gtk.Window parent = host.TestWidget<Gtk.Window>("main_window");
        Gtk.Window preview = host.TestPreviews[0].Window;
        Check(preview.Visible && preview.IsMapped, "Bottom-right tooltip is actually mapped");
        parent.Window.GetOrigin(out int parentX, out int parentY);
        parent.GetSize(out int parentWidth, out int parentHeight);
        preview.Window.GetOrigin(out int previewX, out int previewY);
        preview.GetSize(out int previewWidth, out int previewHeight);
        Console.WriteLine($"CHECK: tooltip parent={parentX},{parentY},{parentWidth},{parentHeight}; preview={previewX},{previewY},{previewWidth},{previewHeight}");
        Check(Math.Abs(previewX + previewWidth - (parentX + parentWidth - 16)) <= 4
            && Math.Abs(previewY + previewHeight - (parentY + parentHeight - 16)) <= 4,
            "Bottom-right tooltip sits inside the parent client area");
        Screenshot(preview, Path.Combine(screenshots, "mnx-tooltip-bottom-right.png"));
        }

    private static void CheckArchiveAnimations(MainWindow host, string screenshots) {
        string? archivePath = Environment.GetEnvironmentVariable("MNX_GUI_LARGE_GIF_ARCHIVE");
        if (string.IsNullOrEmpty(archivePath)) {
            return;
            }
        host.OpenArchive(archivePath, showErrors: false);
        AppSettings settings = host.TestSettings;
        settings.Set("Previews", "Enabled", true);
        settings.Set("Images", "Animated", true);
        settings.Set("Presentation", "Mode", "Window");
        settings.Set("Presentation", "WindowType", "Window");
        settings.Set("Presentation", "RelativeTo", "Current Screen");
        MNX.VFile[] samples = host.TestArchive!.Files.Where(file => file.Name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            && file.SizeOriginal >= 8_000_000 && file.SizeOriginal <= 20_000_000).ToArray();
        Check(samples.Length > 0, "The real GIF archive has matching animation samples");
        List<string> evidence = new List<string>();
        foreach (long target in new long[] { 9_000_000, 15_000_000 }) {
            MNX.VFile sample = samples.OrderBy(file => Math.Abs((long)file.SizeOriginal - target)).First();
            host.PreviewEntry(sample.FileID);
            WaitFor(() => host.TestPreviewCount == 1, "Actual archived large GIF opens with the new decoded budget");
            Check(host.TestPreviews[0].IsAnimating, "Actual archived large GIF starts animating");
            host.TestPreviews[0].Dispose();
            Check(host.TestPreviewCount == 0, "Closing the actual GIF frees its preview slot");
            evidence.Add($"PASS: archived GIF {sample.SizeOriginal:N0} bytes decoded and animated; close freed the preview slot");
            }
        DiskFile.WriteAllLines(Path.Combine(screenshots, "large-gif-results.txt"), evidence);
        }
    private static void DeleteWithAnswer(MainWindow host, ResponseType answer) {
        bool answered = false;
        uint timer = GLib.Timeout.Add(10, () => {
            MessageDialog? question = Window.ListToplevels().OfType<MessageDialog>().FirstOrDefault(window => window.Visible && window.Title == "Delete from archive");
            if (question is null) {
                return true;
                }
            answered = true;
            question.Respond(answer);
            return false;
            });
        host.TestWidget<MenuItem>("menu_delete").Activate();
        if (!answered) {
            GLib.Source.Remove(timer);
            }
        Check(answered, "Deletion asks before changing the archive");
        }

    private static void WaitFor(Func<bool> predicate, string description) {
        for (int attempt = 0; attempt < 500 && !predicate(); attempt++) {
            Pump(1);
            }
        Check(predicate(), description);
        }
    private static void Pump(int count) {
        for (int step = 0; step < count; step++) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }
            Thread.Sleep(10);
            }
        }
    private static void Reject(System.Action action, string description) {
        try {
            action();
            }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or CryptographicException or OperationCanceledException) {
            return;
            }
        throw new InvalidOperationException("FAILED: " + description);
        }
    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    [System.Runtime.InteropServices.DllImport("libgdk-3.so.0")]
    private static extern IntPtr gdk_pixbuf_get_from_window(IntPtr window, int x, int y, int width, int height);
    private static void Screenshot(Window window, string path) {
        Pump(10);
        using Gdk.Pixbuf image = new Gdk.Pixbuf(gdk_pixbuf_get_from_window(window.Window.Handle, 0, 0, window.Allocation.Width, window.Allocation.Height));
        image.Save(path, "png");
        }
    }
