using ArchiveFile = Mash.IO.Format.MNX.MNX;
using Mash.UI.MNXFile.GUI.Plugins;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private PluginArchive? _pluginArchive;
    private readonly ToolStripMenuItem _replacePluginEntry = new ToolStripMenuItem("Replace file…");

    internal static bool ShouldOpenArchive(string input) {
        string path = FileTransfer.LocalPath(input);
        return !Directory.Exists(path) && (Path.GetExtension(path).Equals(".mnx", StringComparison.OrdinalIgnoreCase)
            || PluginCatalog.Installed.LooksLikeArchive(path)
            || (System.IO.File.Exists(path) && FileTransfer.IsArchive(path)));
        }

    private static string BuiltInArchiveFormat() {
        try {
            using ArchiveFile engine = new ArchiveFile();
            return $"Built-in archive format: MNX (.mnx), version {engine.FormatVersion}";
            }
        catch (Exception error) {
            return "Built-in archive format: MNX (.mnx), engine unavailable — " + error.Message;
            }
        }

    private static string PluginOpenFilter() {
        List<string> supportedPatterns = new List<string> { "*.mnx" };
        List<string> filters = new List<string> { "MNX archives (*.mnx)|*.mnx" };
        foreach (ArchivePlugin module in PluginCatalog.Installed.Modules) {
            string[] patterns = module.Format.Extensions.Select(extension => "*" + extension).ToArray();
            supportedPatterns.AddRange(patterns);
            filters.Add(module.Format.Name + " archives|" + string.Join(";", patterns));
            }
        filters.Insert(0, "All supported archives|" + string.Join(";", supportedPatterns.Distinct(StringComparer.OrdinalIgnoreCase)));
        filters.Add("All files (*.*)|*.*");
        return string.Join("|", filters);
        }

    private void ConfigurePluginActions() {
        int index = 1;
        foreach (ArchivePlugin module in PluginCatalog.Installed.Modules) {
            if ((module.Current.Actions & PluginActions.Create) == 0) {
                continue;
                }
            ToolStripMenuItem create = new ToolStripMenuItem("New " + module.Format.Name + " archive…");
            create.Click += async (_, _) => await Guard(() => ChoosePluginCreation(module));
            archiveMenu.DropDownItems.Insert(index++, create);
            TrackAction("New", create);
            }
        _replacePluginEntry.Visible = false;
        _replacePluginEntry.Click += async (_, _) => await Guard(ChoosePluginReplacement);
        entryContextMenu.Items.Add(_replacePluginEntry);
        }

    private async Task<T> RunPluginOperation<T>(string title, string path, string formatName, Func<CancellationToken, T> operation) {
        using CancellationTokenSource cancellation = new();
        return await RunOperation(title, () => operation(cancellation.Token), displayPath: path,
            pluginCancellation: cancellation, pluginFormat: formatName);
        }

    private void SetPluginArchive(PluginArchive archive, Guid? location = null) {
        _pluginArchive = archive;
        LoadBrowser(archive.Browser, location);
        }

    private async Task ChoosePluginCreation(ArchivePlugin module) {
        using InputSelectionForm input = new InputSelectionForm("Create " + module.Format.Name + " archive");
        if (input.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        string[] sources = FileTransfer.LocalPaths(input.Paths);
        if (sources.Length == 0) {
            return;
            }
        string extension = module.Format.Extensions[0];
        using SaveFileDialog chooser = new SaveFileDialog {
            Title = "Create " + module.Format.Name + " archive",
            DefaultExt = extension.TrimStart('.'),
            AddExtension = true,
            Filter = module.Format.Name + " archive|*" + extension,
            OverwritePrompt = false,
            };
        if (chooser.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        bool overwrite = System.IO.File.Exists(chooser.FileName);
        if (overwrite && MessageBox.Show(this, "Replace the existing archive at " + chooser.FileName + "?",
            "Replace archive", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
            return;
            }
        await CreatePluginArchive(module, chooser.FileName, sources, overwrite);
        }

    private async Task CreatePluginArchive(ArchivePlugin module, string path, string[] sources, bool overwrite) {
        PluginArchive result = await RunPluginOperation("Creating archive", path, module.Format.Name,
            token => PluginArchive.Create(module, path, sources, overwrite, token));
        CloseArchive();
        SetPluginArchive(result);
        statusMessage.Text = "Created " + module.Format.Name + " archive.";
        }

    private async Task AddPluginFiles(string[] sources, Guid folder) {
        PluginArchive archive = _pluginArchive!;
        string target = archive.Browser.PathOf(folder);
        PluginArchive result = await RunPluginOperation("Adding files", archive.Path, archive.Format.Name,
            token => archive.Add(sources, target, token));
        SetPluginArchive(result, folder);
        statusMessage.Text = "Files added.";
        }

    private bool CanReplacePluginEntry(Guid[] selection) {
        return _pluginArchive?.Supports(PluginActions.Replace) == true && selection.Length == 1
            && !_pluginArchive.Browser.Get(selection[0]).IsDirectory;
        }

    private async Task ChoosePluginReplacement() {
        if (!CanReplacePluginEntry(_contextSelection)) {
            return;
            }
        Guid id = _contextSelection[0];
        using OpenFileDialog chooser = new OpenFileDialog {
            Title = "Choose replacement for " + _browser!.Get(id).Name,
            Filter = "All files (*.*)|*.*",
            };
        if (chooser.ShowDialog(this) != DialogResult.OK) {
            return;
            }
        if (MessageBox.Show(this, "Replace " + _browser.PathOf(id) + " in this archive?", "Replace archived file",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) {
            await ReplacePluginFile(chooser.FileName, id);
            }
        }

    private async Task ReplacePluginFile(string source, Guid id) {
        PluginArchive archive = _pluginArchive!;
        Guid folder = _current;
        PluginArchive result = await RunPluginOperation("Replacing file", archive.Path, archive.Format.Name,
            token => archive.Replace(source, id, token));
        SetPluginArchive(result, folder);
        statusMessage.Text = "Archived file replaced.";
        }

    private async Task ExtractPluginEntries(Guid[] ids, string destination, bool singleFile, bool overwrite) {
        PluginArchive archive = _pluginArchive!;
        PluginExtraction plan = archive.PlanExtraction(ids, destination, singleFile);
        string[] existing = plan.ExistingFiles();
        if (existing.Length > 0 && !overwrite) {
            if (MessageBox.Show(this, $"Replace {existing.Length:N0} existing file(s)?", "Replace files",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                return;
                }
            overwrite = true;
            }
        await RunPluginOperation("Extracting files", archive.Path, archive.Format.Name, token => {
            plan.Extract(overwrite, token);
            return true;
            });
        statusMessage.Text = "Extraction complete.";
        }

    private async Task DeletePluginEntries(Guid[] ids, Guid location) {
        PluginArchive archive = _pluginArchive!;
        PluginArchive result = await RunPluginOperation("Deleting entries", archive.Path, archive.Format.Name,
            token => archive.Delete(ids, token));
        SetPluginArchive(result, location);
        statusMessage.Text = "Entries deleted.";
        }

    private void ShowPluginProperties(Guid[] ids) {
        PluginArchive archive = _pluginArchive!;
        string details = archive.Path + "\n\nFormat: " + archive.Format.Name + " " + archive.FormatVersion;
        if (ids.Length == 1) {
            ArchiveBrowser.Entry entry = archive.Browser.Get(ids[0]);
            details += $"\n\n{archive.Browser.PathOf(entry.Id)}\nType: {(entry.IsDirectory ? "Directory" : "File")}\nSize: {entry.Bytes:N0} bytes";
            }
        TextPrompt.ShowText(this, "Properties", details);
        }
    }
