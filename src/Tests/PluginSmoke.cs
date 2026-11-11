using System.Diagnostics;
using Gtk;
using Mash.UI.MNXFile.GUI.Plugins;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

internal static class PluginSmoke {
    internal static void Run(MainWindow host, string fixture) {
        host.TestWidget<Window>("main_window").Move(0, 0);
        string work = Directory.CreateTempSubdirectory("mnx-gtk-plugin-").FullName;
        try {
            const string original = "hello from Ren'Py";
            const string replacement = "replacement contents";
            string archive = Path.Combine(work, "fixture.rpa");
            DiskFile.Copy(fixture, archive);
            Choose(() => host.TestWidget<MenuItem>("menu_open").Activate(), archive);
            Check(host.TestBrowser?.Title == "fixture.rpa" && host.TestArchive is null, "Open the plugin archive in the regular browser");

            string renamed = Path.Combine(work, "archive without extension");
            DiskFile.Copy(archive, renamed);
            host.OpenArchive(renamed, showErrors: false);
            Check(host.TestBrowser?.Title == Path.GetFileName(renamed), "Plugin archive opens by magic with no suffix");
            host.OpenInputs([archive]);
            Check(host.TestBrowser?.Title == "fixture.rpa", "OpenInputs routes the plugin archive to the browser");

            string malformed = Path.Combine(work, "malformed.rpa");
            DiskFile.WriteAllText(malformed, "invalid archive");
            ExpectOpenError(() => host.OpenInputs([malformed]));
            Check(host.TestBrowser?.Title == "fixture.rpa", "Failed plugin open preserves the previous view");

            Entry location = host.TestWidget<Entry>("current_path");
            location.Text = "/folder";
            location.Activate();
            Check(host.TestCurrentPath == "/folder" && host.TestListCount == 1, "Plugin folders use normal path navigation");
            TreeView list = host.TestWidget<TreeView>("file_list");
            using (TreePath row = new TreePath("0")) {
                TransferSmoke.RightClick(list, row);
                }
            Check(host.TestEntryMenu.Visible && host.TestExtractEntry.Sensitive, "Plugin files have a normal Extract context action");
            MenuItem replace = FindItem(host.TestEntryMenu, "Replace");
            Check(replace.Sensitive, "Plugin files have an enabled Replace context action");
            host.TestEntryMenu.Popdown();
            host.TestSettings.Set("Previews", "Enabled", true);
            using (TreePath row = new TreePath("0")) {
                TransferSmoke.RightClick(list, row);
                }
            MenuItem preview = FindItem(host.TestEntryMenu, "Preview");
            Check(preview.Sensitive, "Plugin text preview is enabled");
            host.TestEntryMenu.Popdown();
            preview.Activate();
            CheckTextPreview(host);
            host.TestSettings.Set("Previews", "Enabled", false);
            Check(FindItem(host.TestEntryMenu, "Delete").Sensitive, "Plugin files have an enabled Delete context action");
            host.TestEntryMenu.Popdown();
            foreach (string action in new[] { "test", "compact" }) {
                Check(!host.TestWidget<ToolButton>("action_" + action).Sensitive, action + " is disabled for the plugin");
                }
            Check(!host.TestWidget<MenuItem>("menu_resume").Sensitive, "Resume is disabled for the plugin");
            Check(Drag.SourceGetTargetList(list) is null, "Drag-out is disabled for the plugin");

            string extracted = Path.Combine(work, "extracted.txt");
            Choose(() => host.TestExtractEntry.Activate(), extracted);
            Check(DiskFile.ReadAllText(extracted) == original, "Selected file extracts through the context menu");

            string added = Path.Combine(work, "added.txt");
            DiskFile.WriteAllText(added, "added contents");
            Choose(() => host.TestWidget<MenuItem>("menu_add").Activate(), added);
            Check(host.TestCurrentPath == "/folder" && host.TestListCount == 2, "Add refreshes the current plugin folder");
            string dropped = Path.Combine(work, "dropped.txt");
            DiskFile.WriteAllText(dropped, "dragged contents");
            TransferSmoke.DropFile(list, dropped);
            Check(host.TestCurrentPath == "/folder" && host.TestListCount == 3, "File-manager drag-in adds to the current plugin folder");

            string sourceFolder = Directory.CreateDirectory(Path.Combine(work, "source")).FullName;
            DiskFile.WriteAllText(Path.Combine(sourceFolder, "child.txt"), "nested contents");
            Choose(() => host.TestWidget<MenuItem>("menu_add").Activate(), sourceFolder);
            Check(host.TestCurrentPath == "/folder" && host.TestListCount == 4, "Add accepts source folders through the file chooser");

            string source = Path.Combine(work, "replacement.txt");
            DiskFile.WriteAllText(source, replacement);
            using (TreePath row = FindRow(list, "hello.txt")) {
                TransferSmoke.RightClick(list, row);
                }
            host.TestEntryMenu.Popdown();
            Choose(() => replace.Activate(), source);
            host.ExtractArchive(archive, extractHere: true);
            Check(DiskFile.ReadAllText(Path.Combine(work, "folder", "hello.txt")) == replacement, "Replace updates the selected entry without renaming it");
            Check(DiskFile.ReadAllText(Path.Combine(work, "folder", "added.txt")) == "added contents", "Add preserves the target folder and exact bytes");
            Check(DiskFile.ReadAllText(Path.Combine(work, "folder", "dropped.txt")) == "dragged contents", "Drag-in preserves exact bytes");
            Check(DiskFile.ReadAllText(Path.Combine(work, "folder", "source", "child.txt")) == "nested contents", "Add recursively preserves folder contents");

            MenuItem create = FindItem(host.TestWidget<Menu>("file_submenu"), "New Ren'Py RPA");
            string created = Path.Combine(work, "created.rpa");
            Choose(() => create.Activate(), added, created);
            Check(host.TestBrowser?.Title == "created.rpa" && host.TestListCount == 1, "New plugin archive opens in the same browser");
            string output = Directory.CreateDirectory(Path.Combine(work, "output")).FullName;
            Choose(() => host.TestWidget<MenuItem>("menu_extract").Activate(), output);
            Check(DiskFile.ReadAllText(Path.Combine(output, "added.txt")) == "added contents", "Extract without a selection uses the entire archive");

            host.TestWidget<MenuItem>("menu_close").Activate();
            TransferSmoke.DropFile(list, archive);
            Check(host.TestBrowser?.Title == "fixture.rpa", "Dropping a plugin archive on the empty browser opens it");
            location.Text = "/folder/source";
            location.Activate();
            using (TreePath row = FindRow(list, "child.txt")) {
                TransferSmoke.RightClick(list, row);
                }
            MenuItem delete = FindItem(host.TestEntryMenu, "Delete");
            Check(delete.Sensitive && host.TestWidget<ToolButton>("action_delete").Sensitive, "Plugin Delete is available for the selection");
            host.TestEntryMenu.Popdown();
            ConfirmDelete(() => delete.Activate(), ResponseType.No);
            Check(host.TestCurrentPath == "/folder/source" && host.TestListCount == 1, "Declining Delete preserves the selection and folder");
            ConfirmDelete(() => host.TestWidget<MenuItem>("menu_delete").Activate(), ResponseType.Yes);
            Check(host.TestCurrentPath == "/folder" && host.TestListCount == 3, "Deleting the last file navigates to the closest remaining folder");
            host.OpenArchive(archive, showErrors: false);
            ArchiveBrowser browser = host.TestBrowser!;
            Check(!browser.Children(browser.Resolve("/folder")).Any(entry => entry.Name == "source"), "Confirmed Delete persists after reopening");

            CheckWebpPreview(host, work);

            string mnx = Path.Combine(work, "mnx with plugin extension.rpa");
            using (ArchiveFile native = new ArchiveFile()) {
                native.Create(new FileInfo(mnx));
                }
            Check(host.OpenArchive(mnx, showErrors: false) && host.TestArchive is not null, "MNX magic takes precedence over plugin extension");
            Check(host.TestWidget<ToolButton>("action_test").Sensitive && host.TestWidget<MenuItem>("menu_resume").Sensitive, "Switching back restores MNX controls");
            Check(Drag.SourceGetTargetList(list) is not null, "Switching back restores MNX drag-out");
            }
        finally {
            host.TestWidget<MenuItem>("menu_close").Activate();
            Directory.Delete(work, recursive: true);
            }
        Console.WriteLine("PASS: GTK plugin open, extract, add, replace, create, delete, capability controls and MNX switching");
        }

    private static TreePath FindRow(TreeView view, string name) {
        TreePath? result = null;
        view.Model.Foreach((model, path, row) => {
            if ((string)model.GetValue(row, 1) != name) {
                return false;
                }
            result = new TreePath(path.ToString());
            return true;
            });
        return result ?? throw new InvalidOperationException("Missing GTK row " + name);
        }

    private static MenuItem FindItem(Menu menu, string label) {
        return menu.Children.OfType<MenuItem>().SingleOrDefault(item => ItemLabel(item).StartsWith(label, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Missing GTK menu item " + label);
        }

    private static string ItemLabel(MenuItem item) {
        if (item.Child is Box content) {
            return content.Children.OfType<Label>().FirstOrDefault()?.Text ?? "";
            }
        return item.Label ?? "";
        }

    private static void Choose(System.Action action, params string[] paths) {
        Stopwatch time = Stopwatch.StartNew();
        FileChooserDialog? current = null;
        Button? chooseFolders = null;
        Exception? failure = null;
        int next = 0;
        long ready = 0;
        uint monitor = GLib.Timeout.Add(50, () => {
            Dialog? active = Window.ListToplevels().OfType<Dialog>().FirstOrDefault(dialog => dialog.Visible && dialog is MessageDialog or FileChooserDialog);
            if (active is null) {
                return true;
                }
            if (active is not FileChooserDialog chooser || time.ElapsedMilliseconds > 10_000) {
                failure = new InvalidOperationException("Unexpected or stalled GTK dialog: " + active.Title + " " + (active as MessageDialog)?.Text);
                active.Respond(ResponseType.Cancel);
                return true;
                }
            if (chooser != current) {
                current = chooser;
                if (next == paths.Length) {
                    failure = new InvalidOperationException("Unexpected GTK chooser: " + chooser.Title);
                    chooser.Respond(ResponseType.Cancel);
                    return true;
                    }
                string path = paths[next];
                if (chooser.Action == FileChooserAction.Open && Directory.Exists(path)) {
                    chooseFolders = chooser.GetWidgetForResponse((int)MainWindow.ChooseFoldersResponse) as Button;
                    if (chooseFolders is null) {
                        failure = new InvalidOperationException("Missing the source-folder picker button.");
                        chooser.Respond(ResponseType.Cancel);
                        return true;
                        }
                    }
                else {
                    chooseFolders = null;
                    next++;
                    if (chooser.Action == FileChooserAction.Save) {
                        chooser.SetCurrentFolder(Path.GetDirectoryName(path)!);
                        chooser.CurrentName = Path.GetFileName(path);
                        }
                    else {
                        chooser.SetFilename(path);
                        }
                    }
                ready = time.ElapsedMilliseconds + 500;
                }
            else if (time.ElapsedMilliseconds >= ready) {
                if (chooseFolders is not null) {
                    chooseFolders.Click();
                    }
                else {
                    chooser.Respond(ResponseType.Accept);
                    }
                }
            return true;
            });
        try {
            action();
            }
        finally {
            GLib.Source.Remove(monitor);
            }
        if (failure is not null) {
            throw failure;
            }
        Check(next == paths.Length, "All expected GTK file choosers appeared");
        }

    private static void ExpectOpenError(System.Action open) {
        bool seen = false;
        uint monitor = GLib.Timeout.Add(50, () => {
            foreach (Dialog dialog in Window.ListToplevels().OfType<Dialog>().Where(dialog => dialog.Visible)) {
                if (dialog is MessageDialog message && message.MessageType == MessageType.Error) {
                    seen = true;
                    }
                dialog.Respond(ResponseType.Cancel);
                }
            return true;
            });
        try {
            open();
            }
        finally {
            GLib.Source.Remove(monitor);
            }
        Check(seen, "Malformed plugin archive reports an error");
        }

    private static void ConfirmDelete(System.Action delete, ResponseType answer) {
        MessageType? dialogType = null;
        string? error = null;
        uint monitor = GLib.Timeout.Add(50, () => {
            MessageDialog? dialog = Window.ListToplevels().OfType<MessageDialog>().FirstOrDefault(window => window.Visible);
            if (dialog is null) {
                return true;
                }
            if (dialogType is null) {
                dialogType = dialog.MessageType;
                dialog.Respond(answer);
                }
            else {
                error = dialog.Text;
                dialog.Respond(ResponseType.Cancel);
                }
            return true;
            });
        try {
            delete();
            }
        finally {
            GLib.Source.Remove(monitor);
            }
        Check(dialogType == MessageType.Warning && error is null, "Delete requests confirmation and succeeds: " + error);
        }

    private static void CheckWebpPreview(MainWindow host, string work) {
        if (PluginCatalog.Installed.ImageProviderFor("image.webp") is null) {
            return;
            }

        string input = Path.Combine(Directory.CreateDirectory(Path.Combine(work, "webp-source")).FullName, "preview.webp");
        byte[] encoded = Convert.FromBase64String("UklGRh4AAABXRUJQVlA4TBEAAAAvAoAAEAdQsiJXpoCBiOh/AAA=");
        DiskFile.WriteAllBytes(input, encoded);
        string archive = Path.Combine(work, "preview.rpa");
        NativeArchiveModule module = PluginCatalog.Installed.Modules.Single(candidate => candidate.Format.Id == "renpy.rpa");
        OpenedModuleArchive.Create(module, archive, [input], false);
        Check(host.OpenArchive(archive, showErrors: false), "Open RPA containing a provider-backed image");
        ArchiveBrowser browser = host.TestBrowser!;
        Guid image = browser.Children(browser.Root).Single().Id;
        host.TestSettings.Set("Previews", "Enabled", true);
        host.PreviewEntry(image);
        WaitForPreview(host);
        Check(host.TestPreviews[0].ImageSize == (3, 3), "The WebP provider displayed a decoded frame");
        host.TestPreviews[0].Dispose();
        host.TestSettings.Set("Previews", "Enabled", false);

        host.ExtractArchive(archive, extractHere: true);
        string extracted = Path.Combine(work, "preview.webp");
        Check(DiskFile.ReadAllBytes(extracted).AsSpan().SequenceEqual(encoded), "Extraction returns the original WebP bytes");
        Console.WriteLine("PASS: GTK WebP provider preview and byte-for-byte RPA extraction");
        }

    private static void CheckTextPreview(MainWindow host) {
        WaitForPreview(host);
        host.TestPreviews[0].Dispose();
        }

    private static void WaitForPreview(MainWindow host) {
        Stopwatch wait = Stopwatch.StartNew();
        while (host.TestPreviewCount == 0 && wait.ElapsedMilliseconds < 5000) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }
            Thread.Sleep(10);
            }
        Check(host.TestPreviewCount == 1, "The RPA preview opened");
        }

    private static void Check(bool condition, string description) {
        if (!condition) {
            throw new InvalidOperationException("FAILED: " + description);
            }
        }
    }
