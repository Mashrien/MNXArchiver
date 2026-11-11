using Gtk;
using Mash.UI.MNXFile.GUI.Plugins;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    // GtkFileChooserDialog reserves Accept, OK, Yes and Apply for the current selection.
    internal const ResponseType ChooseFoldersResponse = (ResponseType)1;
    private readonly List<MenuItem> _pluginCreateItems = [];
    private PluginArchive? _pluginArchive;

    private void ConfigurePluginMenu() {
        Menu menu = Widget<Menu>("file_submenu");
        foreach (ArchivePlugin module in PluginCatalog.Installed.Modules) {
            if ((module.Current.Actions & PluginActions.Create) == 0) {
                continue;
                }

            MenuItem create = new MenuItem($"New {module.Format.Name} archive…");
            create.Activated += (_, _) => CreatePluginArchive(module);
            menu.Insert(create, 1 + _pluginCreateItems.Count);
            _pluginCreateItems.Add(create);
            create.Show();
            }
        }

    private static void AddPluginFilter(FileChooserDialog chooser, ArchivePlugin plugin) {
        string[] extensions = plugin.Format.Extensions;
        FileFilter filter = new FileFilter {
            Name = $"{plugin.Format.Name} archives ({string.Join(", ", extensions.Select(extension => "*" + extension))})"
            };
        foreach (string extension in extensions) {
            filter.AddPattern("*" + extension.ToLowerInvariant());
            filter.AddPattern("*" + extension.ToUpperInvariant());
            }
        chooser.AddFilter(filter);
        }

    private void LoadPluginArchive(PluginArchive archive, string folder = "/") {
        Guid location = archive.Browser.Root;
        while (folder != "/") {
            try {
                location = archive.Browser.Resolve(folder);
                break;
                }
            catch (ArgumentException) {
                // Removing the last file also removes its virtual parent folder.
                int separator = folder.LastIndexOf('/');
                folder = separator > 0 ? folder[..separator] : "/";
                }
            }

        ClosePreviews();
        _archive?.Dispose();
        _archive = null;
        _pluginArchive = archive;
        LoadBrowser(archive.Browser, location);
        }

    private void DispatchPluginAction(ArchiveAction action) {
        PluginArchive archive = _pluginArchive!;
        switch (action) {
            case ArchiveAction.Add when archive.Supports(PluginActions.Add):
                string[] sources = ChoosePluginSources("Add to archive");
                if (sources.Length > 0) {
                    ImportPluginFiles(archive, sources, _current);
                    }
                break;
            case ArchiveAction.Extract when archive.Supports(PluginActions.Extract):
                Guid[] selection = SelectedIds();
                bool entireArchive = selection.Length == 0;
                if (entireArchive) {
                    selection = archive.Browser.Children(archive.Browser.Root).Select(entry => entry.Id).ToArray();
                    }
                ExtractEntries(selection, entireArchive);
                break;
            case ArchiveAction.Delete when archive.Supports(PluginActions.Delete):
                DeleteEntries(SelectedIds());
                break;
            case ArchiveAction.Info:
                Message("Archive information", $"{archive.Path}\n\nFormat: {archive.Format.Name} {archive.FormatVersion}");
                break;
            }
        }

    private string[] ChoosePluginSources(string title) {
        using (FileChooserDialog files = new FileChooserDialog(title, _window, FileChooserAction.Open,
            "Cancel", ResponseType.Cancel, "Choose Folders…", ChooseFoldersResponse, "Select Files", ResponseType.Accept)) {
            GtkWindowAppearance.Apply(files);
            files.SelectMultiple = true;
            ResponseType response = (ResponseType)files.Run();
            if (response != ChooseFoldersResponse) {
                string[] paths = response == ResponseType.Accept ? files.Filenames : [];
                files.Hide();
                return paths;
                }
            files.Hide();
            }

        using FileChooserDialog folders = new FileChooserDialog(title, _window, FileChooserAction.SelectFolder,
            "Cancel", ResponseType.Cancel, "Select Folders", ResponseType.Accept);
        GtkWindowAppearance.Apply(folders);
        folders.CreateFolders = false;
        folders.SelectMultiple = true;
        string[] selected = (ResponseType)folders.Run() == ResponseType.Accept ? folders.Filenames : [];
        folders.Hide();
        return selected;
        }

    private void CreatePluginArchive(ArchivePlugin module) {
        if (_operationActive) {
            return;
            }

        try {
            string[] sources = FileTransfer.LocalPaths(ChoosePluginSources("Choose archive contents"));
            if (sources.Length == 0) {
                return;
                }

            string extension = module.Format.Extensions[0];
            using FileChooserDialog chooser = new FileChooserDialog($"New {module.Format.Name} archive", _window,
                FileChooserAction.Save, "Cancel", ResponseType.Cancel, "Create", ResponseType.Accept);
            GtkWindowAppearance.Apply(chooser);
            chooser.CurrentName = "Archive" + extension;
            AddPluginFilter(chooser, module);
            string? destination = (ResponseType)chooser.Run() == ResponseType.Accept ? chooser.Filename : null;
            chooser.Hide();
            if (destination is null) {
                return;
                }

            if (!module.Format.Extensions.Contains(Path.GetExtension(destination), StringComparer.OrdinalIgnoreCase)) {
                destination += extension;
                }

            bool overwrite = DiskFile.Exists(destination);
            if (overwrite && !ConfirmOverwrite("Replace existing archive?", $"{destination}\n\nReplace this file?")) {
                return;
                }

            PluginArchive archive = RunPluginOperation("Creating archive", token => PluginArchive.Create(module, destination, sources, overwrite, token));
            LoadPluginArchive(archive);
            _status.Text = $"Created archive from {sources.Length:N0} input item(s).";
            }
        catch (OperationCanceledException) {
            _status.Text = "Archive creation cancelled.";
            }
        catch (Exception error) {
            Message("Could not create archive", error.Message, MessageType.Error);
            }
        }

    private void ImportPluginFiles(PluginArchive archive, string[] sources, Guid folder) {
        if (_operationActive || !archive.Supports(PluginActions.Add)) {
            return;
            }

        try {
            sources = FileTransfer.LocalPaths(sources);
            if (sources.Length == 0) {
                return;
                }

            string destination = archive.Browser.PathOf(folder);
            PluginArchive updated = RunPluginOperation("Adding files", token => archive.Add(sources, destination, token));
            LoadPluginArchive(updated, destination);
            _status.Text = $"Added {sources.Length:N0} item(s).";
            }
        catch (OperationCanceledException) {
            _status.Text = "Add cancelled.";
            }
        catch (Exception error) {
            Message("Could not add files", error.Message, MessageType.Error);
            }
        }

    private bool CanReplacePluginFile(Guid[] ids) {
        return _pluginArchive is { } archive && archive.Supports(PluginActions.Replace)
            && ids.Length == 1 && !archive.Browser.Get(ids[0]).IsDirectory;
        }

    private void ReplacePluginFile(Guid[] ids) {
        if (_operationActive || !CanReplacePluginFile(ids)) {
            return;
            }

        PluginArchive archive = _pluginArchive!;
        ArchiveBrowser.Entry entry = archive.Browser.Get(ids[0]);
        string folder = archive.Browser.PathOf(_current);
        using FileChooserDialog chooser = new FileChooserDialog($"Replace {entry.Name}", _window,
            FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Replace", ResponseType.Accept);
        GtkWindowAppearance.Apply(chooser);
        string? source = (ResponseType)chooser.Run() == ResponseType.Accept ? chooser.Filename : null;
        chooser.Hide();
        if (source is null) {
            return;
            }

        try {
            PluginArchive updated = RunPluginOperation("Replacing file", token => archive.Replace(source, entry.Id, token));
            LoadPluginArchive(updated, folder);
            _status.Text = $"Replaced {entry.Name}.";
            }
        catch (OperationCanceledException) {
            _status.Text = "Replacement cancelled.";
            }
        catch (Exception error) {
            Message("Could not replace file", error.Message, MessageType.Error);
            }
        }

    private void DeletePluginEntries(PluginArchive archive, Guid[] ids, string folder) {
        try {
            PluginArchive updated = RunPluginOperation("Deleting archive entries", token => archive.Delete(ids, token));
            LoadPluginArchive(updated, folder);
            _status.Text = "Selected archive entries deleted.";
            }
        catch (OperationCanceledException) {
            _status.Text = "Delete cancelled.";
            }
        catch (Exception error) {
            Message("Could not delete archive entries", error.Message, MessageType.Error);
            }
        }

    private T RunPluginOperation<T>(string description, Func<CancellationToken, T> action) {
        if (_operationActive) {
            throw new InvalidOperationException("An archive operation is already running.");
            }

        using CancellationTokenSource cancellation = new CancellationTokenSource();
        using Dialog dialog = new Dialog(description, _window, DialogFlags.Modal | DialogFlags.DestroyWithParent,
            "Cancel", ResponseType.Cancel);
        GtkWindowAppearance.Apply(dialog);
        dialog.ContentArea.BorderWidth = 16;
        dialog.ContentArea.Spacing = 8;
        Label label = new Label(description + "…") { Xalign = 0 };
        ProgressBar progress = new ProgressBar { WidthRequest = 280 };
        dialog.ContentArea.PackStart(label, false, false, 0);
        dialog.ContentArea.PackStart(progress, false, false, 0);
        void RequestCancellation() {
            cancellation.Cancel();
            label.Text = "Cancelling…";
            dialog.SetResponseSensitive(ResponseType.Cancel, false);
            }
        dialog.Response += (_, _) => RequestCancellation();
        dialog.DeleteEvent += (_, args) => {
            RequestCancellation();
            args.RetVal = true;
            };

        string previousStatus = _status.Text;
        bool previousSensitivity = _window.Sensitive;
        _operationActive = true;
        try {
            StopPreviewRead();
            UpdateActions();
            _status.Text = description;
            _window.Sensitive = false;
            dialog.ShowAll();
            Task<T> task = Task.Run(() => action(cancellation.Token));
            return WaitForTransfer(task, progress.Pulse);
            }
        finally {
            dialog.Hide();
            _status.Text = previousStatus;
            _window.Sensitive = previousSensitivity;
            _operationActive = false;
            UpdateActions();
            if (_quitRequested) {
                RequestQuit();
                }
            }
        }
    }
