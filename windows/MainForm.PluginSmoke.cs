using Mash.UI.MNXFile.GUI.Plugins;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private async Task RunPluginSmoke(string work, List<string> evidence) {
        string? fixture = Environment.GetEnvironmentVariable("MNX_GUI_PLUGIN_SMOKE_FIXTURE");
        if (string.IsNullOrEmpty(fixture)) {
            return;
            }

        string archivePath = Path.Combine(work, "renamed-rpa.dat");
        DiskFile.Copy(fixture, archivePath);
        Require(await OpenArchive(archivePath, showErrors: false), "The plugin archive did not open by magic.");
        Require(_archive is null && _pluginArchive?.Format.Id == "renpy.rpa", "The module did not own the active archive.");
        Require(addFilesButton.Enabled && extractButton.Enabled && !verifyButton.Enabled && !compactButton.Enabled
            && !resumeMenuItem.Enabled, "Archive capabilities did not gate the controls.");
        ArchiveBrowser browser = _browser!;
        Guid folder = browser.Resolve("/folder");
        Navigate(folder);
        Guid hello = browser.Children(folder).Single().Id;
        Require(!CanPreview(hello) && CanReplacePluginEntry([hello]) && !CanReplacePluginEntry([folder]),
            "Preview or Replace was enabled for the wrong entry or format.");

        string output = Path.Combine(work, "plugin-extracted.txt");
        await ExtractSelection([hello], output, singleFile: true);
        Require(DiskFile.ReadAllText(output) == "hello from Ren'Py", "GUI extraction did not preserve the fixture's bytes.");
        string source = Path.Combine(work, "new plugin entry.txt");
        DiskFile.WriteAllText(source, "new entry");
        await ImportFiles([source], folder);
        Require(_browser!.Children(folder).Count == 2 && _current == folder, "Plugin Add did not preserve and refresh the current folder.");
        await ReplacePluginFile(source, hello);
        await ExtractSelection([hello], output, singleFile: true, overwrite: true);
        Require(DiskFile.ReadAllText(output) == "new entry", "Plugin Replace did not change the selected entry.");

        await DeleteEntries([hello], confirmed: true);
        Require(_browser!.Children(folder).Single().Name == Path.GetFileName(source), "Plugin Delete removed the wrong entry.");
        await DeleteEntries([folder], confirmed: true);
        Require(_browser!.Children(_browser.Root).Count == 0 && _current == _browser.Root, "Deleting a virtual folder left entries or an invalid location.");

        ArchivePlugin module = PluginCatalog.Installed.Modules.Single(plugin => plugin.Format.Id == "renpy.rpa");
        string created = Path.Combine(work, "created.rpa");
        await CreatePluginArchive(module, created, [source], overwrite: false);
        Require(_pluginArchive!.Path == created && _browser!.Children(_browser.Root).Single().Name == Path.GetFileName(source),
            "Plugin Create did not open the resulting archive.");
        string unsupported = Path.Combine(work, "unsupported.rpa");
        DiskFile.WriteAllText(unsupported, "RPA-2.0 0000000000000000\n");
        bool refused = false;
        try {
            await OpenArchive(unsupported, showErrors: false);
            }
        catch (NotSupportedException) {
            refused = true;
            }
        Require(refused && _pluginArchive?.Path == created, "An unsupported RPA replaced the current archive view.");
        CloseArchive();
        evidence.Add("PASS: native RPA discovery, magic routing, GUI extraction/add/replace/delete/create, feature gating and failed-open preservation");
        }
    }
