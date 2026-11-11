using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Settings;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private async Task RunPluginSmoke(string work, List<string> evidence) {
        string? fixture = Environment.GetEnvironmentVariable("MNX_GUI_PLUGIN_SMOKE_FIXTURE");
        if (string.IsNullOrEmpty(fixture)) {
            return;
            }

        string[] openFilters = PluginOpenFilter().Split('|');
        Require(openFilters[0] == "All supported archives"
            && openFilters[1].Split(';').Contains("*.mnx")
            && openFilters[1].Split(';').Contains("*.rpa"),
            "The default archive picker filter must include built-in and plugin formats.");
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
        Require(CanReplacePluginEntry([hello]) && !CanReplacePluginEntry([folder]),
            "Replace was enabled for the wrong entry or format.");
        await CheckPluginPreviews(work, folder, hello);

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

        NativeArchiveModule module = PluginCatalog.Installed.Modules.Single(plugin => plugin.Format.Id == "renpy.rpa");
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
        evidence.Add("PASS: native RPA discovery, magic routing, text/image previews, GUI extraction/add/replace/delete/create, feature gating and failed-open preservation");
        }
    private async Task CheckPluginPreviews(string work, Guid folder, Guid hello) {
        AppSettings original = _settings;
        try {
            _settings = original.Copy();
            _settings.Set("Previews", "Enabled", true);
            ResetPreviews();
            Require(CanPreview(hello) && !CanPreview(folder), "RPA text preview eligibility is incorrect.");
            await PreviewEntry(hello, false);
            using (Form textWindow = _previews.Windows.Single()) {
                RichTextBox text = textWindow.Controls.OfType<RichTextBox>().Single();
                Require(text.ReadOnly && text.Text == "hello from Ren'Py", "RPA text preview changed the entry bytes.");
                textWindow.Close();
                }

            string imagePath = Path.Combine(work, "plugin-preview.png");
            DiskFile.WriteAllBytes(imagePath, PreviewSmokeTests.Png());
            await ImportFiles([imagePath], folder);
            Guid imageId = _browser!.Children(folder).Single(entry => entry.Name == "plugin-preview.png").Id;
            await PreviewEntry(imageId, false);
            using (PreviewForm image = (PreviewForm)_previews.Windows.Single()) {
                Require(image.ImageSize == new Size(4, 3), "RPA image preview did not decode the archived PNG.");
                image.Close();
                }
            await DeleteEntries([imageId], confirmed: true);
            }
        finally {
            _settings = original;
            ResetPreviews();
            }
        }
    }
