using Mash.IO.Format.MNX;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class ProgressForm : MnxDialog {
    private readonly TransferTracker? _tracker;
    private readonly TransferCancellation? _cancellation;
    private readonly CancellationTokenSource? _pluginCancellation;
    private readonly bool _extraction;
    private readonly bool _existingArchive;
    private readonly bool _encrypted;
    private readonly string _title = "MNX Archive Progress";
    internal bool AllowClose;
    internal TransferSnapshot? Snapshot => _tracker?.Snapshot;


    public ProgressForm() {
        InitializeComponent();
        AllowClose = true;
        }

    internal ProgressForm(string title, string archivePath, string codec, bool encrypted, TransferTracker? tracker, TransferCancellation? cancellation, bool extraction, bool existingArchive,
        CancellationTokenSource? pluginCancellation = null, string? pluginFormat = null) : this() {
        _title = title;
        _tracker = tracker;
        _cancellation = cancellation;
        _pluginCancellation = pluginCancellation;
        _extraction = extraction;
        _existingArchive = existingArchive;
        _encrypted = encrypted;
        Text = title;
        archivePathLabel.Text = archivePath;
        archiveSettingsLabel.Text = pluginFormat is null
            ? "Compression: " + codec + "    -    Encrypted: " + (encrypted ? "Yes" : "No")
            : "Format: " + pluginFormat;
        AllowClose = false;
        cancelButton.Enabled = cancellation is not null || pluginCancellation is not null;
        cancelButton.Click += (_, _) => ChooseCancellation();
        FormClosing += (_, args) => {
            if (!AllowClose) {
                args.Cancel = true;
                ChooseCancellation();
                }
            };
        }

    internal void UpdateProgress(OperationProgressArgs? progress) {
        if (_pluginCancellation is not null) {
            currentFileLabel.Text = _pluginCancellation.IsCancellationRequested ? "Stopping…" : "Working…";
            filePiecesLabel.Text = "";
            archiveProgressLabel.Text = "";
            SetFileProgress(0, 0, true);
            totalProgressBar.UpdateValue(0, 0, true);
            return;
            }
        TransferSnapshot? snapshot = _tracker?.Snapshot;
        currentFileLabel.Text = snapshot?.File ?? progress?.FileName ?? "Working…";
        if (snapshot is not null && !snapshot.Scanning) {
            int percentage = snapshot.Total > 0 ? (int)Math.Clamp((double)snapshot.Bytes / snapshot.Total * 100, 0, 100) : 0;
            Text = _title + " [" + percentage + "%]";
            filePiecesLabel.Text = "File Pieces: " + TransferTracker.PieceCounts(_tracker!.Pieces(snapshot.FileBytes), _tracker.Pieces(snapshot.FileTotal));
            archiveProgressLabel.Text = "Archive progress: " + TransferTracker.PieceCounts(snapshot.Pieces, snapshot.TotalPieces)
                + $"    Files: {snapshot.Files:N0} / {snapshot.TotalFiles:N0}\n"
                + TransferTracker.ByteCounts(snapshot.Bytes, snapshot.Total);
            SetFileProgress(snapshot.FileBytes, snapshot.FileTotal, false);
            totalProgressBar.UpdateValue(snapshot.Bytes, snapshot.Total, false);
            }
        else {
            Text = _title + " [preparing]";
            filePiecesLabel.Text = "File Pieces: preparing…";
            archiveProgressLabel.Text = progress is null ? "Scanning input files…" : TransferTracker.ByteCounts(progress.BytesComplete, progress.BytesMax);
            SetFileProgress(progress?.BytesComplete ?? 0, progress?.BytesMax ?? 0, progress is null);
            totalProgressBar.UpdateValue(progress?.BytesComplete ?? 0, progress?.BytesMax ?? 0, progress is null);
            }
        }

    private void SetFileProgress(long done, long total, bool scanning) {
        fileProgressBar.Style = scanning ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        fileProgressBar.Value = total > 0 ? (int)Math.Clamp((double)done / total * 100, 0, 100) : 0;
        }

    private void ChooseCancellation() {
        if (_pluginCancellation is not null && !AllowClose && cancelButton.Enabled) {
            cancelButton.Enabled = false;
            _pluginCancellation.Cancel();
            return;
            }
        if (_cancellation is null || !cancelButton.Enabled || AllowClose) {
            return;
            }
        cancelButton.Enabled = false;
        _cancellation.BeginChoice();
        string explanation = _extraction
            ? "Keep: finish the current file, then stop and keep completed output.\nDelete: stop immediately and delete the unfinished output file. Completed files are kept."
            : "Keep: finish the current file, then save completed files and a checksummed .mnq remaining queue.\nDelete: stop immediately and delete the partial archive.";
        if (_existingArchive && !_extraction) {
            explanation += "\n\nDelete removes this entire existing archive, including earlier files.";
            }
        using CancellationForm choice = new CancellationForm(explanation, _encrypted && !_extraction);
        DialogResult result = choice.ShowDialog(this);
        _cancellation.Choose(result == DialogResult.Yes, choice.ExportKey);
        currentFileLabel.Text = result == DialogResult.Yes ? "Finishing the current file…" : "Stopping immediately…";
        }
    }
