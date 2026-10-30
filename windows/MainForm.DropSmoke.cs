namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private async Task CheckArchiveDrops(string currentPath, string directory) {
        string incoming = Path.Combine(directory, "dropped.mnx");
        System.IO.File.Copy(currentPath, incoming);
        object? previous = _archive;
        using System.Windows.Forms.Timer responder = new System.Windows.Forms.Timer();
        responder.Interval = 20;
        DialogResult choice = DialogResult.Cancel;
        bool creationShown = false;
        responder.Tick += (_, _) => {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) {
                if (form is ArchiveDropForm) {
                    form.DialogResult = choice;
                    form.Close();
                    }
                else if (form is CreationForm) {
                    creationShown = true;
                    form.DialogResult = DialogResult.Cancel;
                    form.Close();
                    }
                }
            };
        responder.Start();
        await HandleDroppedFiles(new string[] { incoming }, _archive!.RootDirectoryId);
        Require(ReferenceEquals(previous, _archive), "Cancelling an archive drop replaced the active archive.");
        choice = DialogResult.No;
        await HandleDroppedFiles(new string[] { incoming }, _archive!.RootDirectoryId);
        Require(_archive.Files.Any(entry => entry.Name == "dropped.mnx"), "Add to Archive did not store the dropped archive as a file.");
        choice = DialogResult.Yes;
        await HandleDroppedFiles(new string[] { incoming }, _archive.RootDirectoryId);
        Require(!ReferenceEquals(previous, _archive) && Text.Contains("dropped.mnx", StringComparison.Ordinal), "Open Archive did not replace the active view.");
        CloseArchive();
        choice = DialogResult.No;
        await HandleDroppedFiles(new string[] { incoming }, Guid.Empty);
        Require(creationShown && _archive is null, "Add without an open archive must show the ordinary creation form and honor Cancel.");
        responder.Stop();
        await OpenArchive(currentPath);
        }
    }
