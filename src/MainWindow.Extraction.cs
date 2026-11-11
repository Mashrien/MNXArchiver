using Mash.UI.MNXFile.GUI.Icons;
using Mash.UI.MNXFile.GUI.Plugins;
using Gtk;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    /// <summary>Open a signature-checked archive and extract its root contents from a file-manager action.</summary>
    public void ExtractArchive(string fileName, bool extractHere) {
        if (_operationActive) {
            return;
            }

        try {
            string archivePath = FileTransfer.LocalPath(fileName);
            if (!OpenArchive(archivePath)) {
                return;
                }
            if (_pluginArchive?.Supports(PluginActions.Extract) == false) {
                throw new NotSupportedException("This archive format version does not support extraction.");
                }

            string destination = Path.GetDirectoryName(archivePath)!;
            if (!extractHere) {
                string? chosen = ChooseExtractionFolder(destination);
                if (chosen is null) {
                    _status.Text = "Extraction cancelled.";
                    return;
                    }

                destination = chosen;
                }

            IReadOnlyList<ArchiveBrowser.Entry>? rootEntries = _browser!.Children(_browser.Root);
            Guid[] ids = rootEntries.Select(entry => entry.Id).ToArray();
            ExtractSelection(ids, destination);
            }
        catch (Exception error) {
            Message("Could not extract archive", error.Message, MessageType.Error);
            }
        }

    private string? ChooseExtractionFolder(string initialDirectory) {
        using FileChooserDialog chooser = new FileChooserDialog(
            "Extract To…",
            _window,
            FileChooserAction.SelectFolder,
            "Cancel",
            ResponseType.Cancel,
            "Extract",
            ResponseType.Accept
            );
        GtkWindowAppearance.Apply(chooser);
        chooser.CreateFolders = true;
        chooser.SetCurrentFolder(initialDirectory);
        string? destination = null;
        if ((ResponseType)chooser.Run() == ResponseType.Accept) {
            destination = chooser.Filename;
            }

        chooser.Hide();
        return destination;
        }

    private void ExtractSelection(Guid[] ids, string destination, bool singleFile = false, bool replacementConfirmed = false) {
        if (_pluginArchive is { } plugin) {
            ExtractPluginSelection(plugin, ids, destination, singleFile, replacementConfirmed);
            return;
            }

        ArchiveFile? archive = _archive!;
        string directory = singleFile ? Path.GetDirectoryName(destination)! : destination;
        ExtractionPlan? plan = ExtractionPlan.Create(archive, ids, directory, singleFile ? destination : null);
        string[] existing = plan.ExistingFiles();
        bool overwrite = replacementConfirmed;
        if (existing.Length > 0 && !overwrite) {
            if (!ConfirmExtractionOverwrite(existing.Length)) {
                _status.Text = "Extraction cancelled. Existing files were kept.";
                return;
                }

            overwrite = true;
            }

        if (RunExtraction(plan, overwrite)) {
            _status.Text = $"Extracted {ids.Length:N0} item(s) to {destination}.";
            }
        }

    private bool ConfirmExtractionOverwrite(int existing) {
        return ConfirmOverwrite("Replace existing files?", $"{existing:N0} file(s) already exist in the destination. Replace them?");
        }

    private bool ConfirmOverwrite(string title, string explanation) {
        using MessageDialog question = OwnedIcons.Dialog(_window, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, explanation);
        question.Title = title;
        question.DefaultResponse = ResponseType.No;
        bool replace = (ResponseType)question.Run() == ResponseType.Yes;
        question.Hide();
        return replace;
        }

    private void ExtractPluginSelection(OpenedModuleArchive archive, Guid[] ids, string destination, bool singleFile, bool replacementConfirmed) {
        if (ids.Length == 0) {
            _status.Text = "The archive has no entries to extract.";
            return;
            }

        PluginExtraction plan = archive.PlanExtraction(ids, destination, singleFile);
        string[] existing = plan.ExistingFiles();
        if (existing.Length > 0 && !replacementConfirmed && !ConfirmExtractionOverwrite(existing.Length)) {
            _status.Text = "Extraction cancelled. Existing files were kept.";
            return;
            }

        try {
            RunPluginOperation("Extracting files", token => {
                plan.Extract(replacementConfirmed || existing.Length > 0, token);
                return true;
                });
            _status.Text = $"Extracted {ids.Length:N0} item(s) to {destination}.";
            }
        catch (OperationCanceledException) {
            _status.Text = "Extraction cancelled.";
            }
        }

    private bool RunExtraction(ExtractionPlan plan, bool overwrite = false) {
        ArchiveFile? archive = _archive!;
        TransferTracker tracker = new TransferTracker(archive.ChunkSize);
        tracker.Configure(plan.Items);
        using TransferCancellation cancellation = new TransferCancellation();
        try {
            RunTransfer("Extracting files", () => {
                return plan.Extract(archive, tracker, cancellation, overwrite);
                }, tracker, cancellation: cancellation, extracting: true);
            return true;
            }
        catch (OperationCanceledException) {
            if (cancellation.Keep) {
                _status.Text = "Extraction stopped after the current file. Completed files were kept.";
                }
            else {
                _status.Text = "Extraction stopped. The unfinished output file was deleted; completed files were kept.";
                }

            return false;
            }
        }

    private string[] ExportDraggedFiles(Guid[] ids, string directory) {
        ExtractionPlan? plan = ExtractionPlan.Create(_archive!, ids, directory);
        string[] existing = plan.ExistingFiles();
        if (existing.Length > 0) {
            throw new IOException("Drag extraction destination already exists: " + existing[0]);
            }

        RunExtraction(plan);
        List<string> completed = new List<string>();
        foreach (string path in plan.RootPaths) {
            if (DiskFile.Exists(path) || Directory.Exists(path)) {
                completed.Add(new Uri(path).AbsoluteUri);
                }
            }

        return completed.ToArray();
        }
    }
