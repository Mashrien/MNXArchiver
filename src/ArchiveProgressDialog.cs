using Mash.GUI.MNXArchiver.Icons;
using Gtk;
using MNX;

namespace Mash.GUI.MNXArchiver;
/// <summary>A modal, parent-owned transfer window. No archive getters run on the UI thread.</summary>
internal sealed class ArchiveProgressDialog : IDisposable {
    internal Dialog Window { get; }

    private readonly Label _file = new();
    private readonly Label _pieces = new();
    private readonly Label _archivePieces = new();
    private readonly Label _bytes = new();
    private readonly ProgressBar _fileProgress = new() {
        ShowText = true
        };
    private readonly ProgressBar _overallProgress = new() {
        ShowText = true
        };
    private readonly TransferTracker? _tracker;
    private readonly bool _encrypted;
    private readonly bool _extracting;
    private TransferSnapshot? _lastSnapshot;
    private OperationProgressArgs? _lastProgress;
    internal CheckButton? ExportKeyFileChoice { get; private set; }
    internal TransferSnapshot? Snapshot => _tracker?.Snapshot;
    internal Button CancelButton { get; } = new("Cancel");

    internal event System.Action? CancelRequested;
    internal ArchiveProgressDialog(
        Gtk.Window parent,
        string archive,
        string compression,
        bool encrypted,
        TransferTracker? tracker,
        bool extracting = false,
        bool canCancel = false
        ) {
        _tracker = tracker;
        _encrypted = encrypted;
        _extracting = extracting;
        string title = extracting ? "MNX Extraction Progress" : "MNX Archive Progress";
        Window = new Dialog(title, parent, DialogFlags.Modal) {
            DestroyWithParent = true,
            Resizable = true
            };
        GtkWindowAppearance.Apply(Window);
        Window.SetDefaultSize(620, 400);
        Window.DeleteEvent += (_, args) => {
            args.RetVal = true;
            RequestCancel();
            };
        Box? box = Window.ContentArea;
        box.BorderWidth = 18;
        box.Spacing = 10;
        box.PackStart(Text(archive), false, false, 0);
        box.PackStart(Text($"Compression: {compression} - Encrypted: {(encrypted ? "Yes" : "No")}"), false, false, 0);
        box.PackStart(new Label(""), false, false, 0);
        box.PackStart(Text("Current File:"), false, false, 0);
        _file.Xalign = 0;
        _file.Ellipsize = Pango.EllipsizeMode.Middle;
        _file.MaxWidthChars = 75;
        box.PackStart(_file, false, false, 0);
        _pieces.Xalign = 0;
        _pieces.TooltipText = "Input pieces use the archive's chunk size. Compressed physical output counts are known only after compression finishes.";
        box.PackStart(_pieces, false, false, 0);
        box.PackStart(_fileProgress, false, false, 0);
        box.PackStart(new Label(""), false, false, 0);
        _archivePieces.Xalign = 0;
        box.PackStart(_archivePieces, false, false, 0);
        _bytes.Xalign = 0;
        box.PackStart(_bytes, false, false, 0);
        box.PackStart(_overallProgress, false, false, 0);
        CancelButton.Clicked += (_, _) => RequestCancel();
        CancelButton.Sensitive = canCancel;
        box.PackEnd(CancelButton, false, false, 0);
        Window.ShowAll();
        }

    private void RequestCancel() {
        if (CancelButton.Sensitive) {
            CancelButton.Sensitive = false;
            CancelRequested?.Invoke();
            }
        }

    internal void ShowCancellationChoice(TransferCancellation cancellation, bool addingToExisting) {
        cancellation.BeginChoice();
        string explanation = addingToExisting ? "Keep current progress finishes the current file and keeps this archive. Delete partial archive stops now and deletes the entire archive, including files already present before this add." : "Keep current progress finishes the current file and saves the completed files. Delete partial archive stops now and deletes the new partial archive.";
        using MessageDialog question = OwnedIcons.Dialog(Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, explanation);
        question.Title = "Cancel archiving";
        question.AddButton("Keep current progress", ResponseType.Accept);
        question.AddButton("Delete partial archive", ResponseType.Reject);
        question.DefaultResponse = ResponseType.Accept;
        CheckButton exportKey = new CheckButton("Export key-file") {
            Active = false,
            Sensitive = _encrypted,
            TooltipText = "Save a temporary password key beside the remaining queue when keeping progress."
            };
        ExportKeyFileChoice = exportKey;
        question.ContentArea.PackStart(exportKey, false, false, 8);
        exportKey.Show();
        bool keep = true;
        try {
            keep = (ResponseType)question.Run() != ResponseType.Reject;
            }
        finally {
            question.Hide();
            cancellation.Choose(keep, _encrypted && exportKey.Active);
            ExportKeyFileChoice = null;
            }

        CancelButton.Label = keep ? "Finishing current file…" : "Stopping and deleting…";
        }

    internal void ShowExtractionCancellationChoice(TransferCancellation cancellation) {
        cancellation.BeginChoice();
        string explanation = "Stop after current file finishes the file being extracted and keeps completed files. " + "Stop now deletes the unfinished file being written. Completed files and the archive are kept.";
        using MessageDialog question = OwnedIcons.Dialog(Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, explanation);
        question.Title = "Cancel extraction";
        question.AddButton("Stop after current file", ResponseType.Accept);
        question.AddButton("Stop now", ResponseType.Reject);
        question.DefaultResponse = ResponseType.Accept;
        bool finishCurrent = true;
        try {
            finishCurrent = (ResponseType)question.Run() != ResponseType.Reject;
            }
        finally {
            question.Hide();
            cancellation.Choose(finishCurrent);
            }

        CancelButton.Label = finishCurrent ? "Finishing current file…" : "Stopping extraction…";
        }

    private static Label Text(string text) {
        return new Label(text) {
            Xalign = 0,
            Ellipsize = Pango.EllipsizeMode.Middle,
            TooltipText = text
            };
        }

    internal void Update(OperationProgressArgs? progress) {
        TransferSnapshot? current = _tracker?.Snapshot;
        if (ReferenceEquals(current, _lastSnapshot) && ReferenceEquals(progress, _lastProgress) && current?.Scanning != true) {
            return;
            }

        _lastSnapshot = current;
        _lastProgress = progress;
        if (current is not null) {
            _file.Text = current.File;
            _file.TooltipText = current.File;
            if (current.Scanning) {
                _pieces.Text = _extracting ? "Preparing extraction…" : "Scanning names, sizes and read permissions…";
                _overallProgress.Pulse();
                return;
                }

            _pieces.Text = "File Pieces: " + TransferTracker.PieceCounts(_tracker!.Pieces(current.FileBytes), _tracker.Pieces(current.FileTotal));
            _fileProgress.Fraction = Fraction(current.FileBytes, current.FileTotal);
            _fileProgress.Text = $"{_fileProgress.Fraction * 100:0.0}%";
            string overallLabel = _extracting ? "Extraction progress: " : "Archive progress: ";
            _archivePieces.Text = overallLabel + TransferTracker.PieceCounts(current.Pieces, current.TotalPieces) + $"    Files: {current.Files:N0} / {current.TotalFiles:N0}";
            _bytes.Text = TransferTracker.ByteCounts(current.Bytes, current.Total);
            _overallProgress.Fraction = Fraction(current.Bytes, current.Total);
            _overallProgress.Text = $"{_overallProgress.Fraction * 100:0.0}%";
            }
        else if (progress is not null) {
            _file.Text = progress.FileName;
            _fileProgress.Fraction = Fraction(progress.BytesComplete, progress.BytesMax);
            _overallProgress.Fraction = _fileProgress.Fraction;
            _overallProgress.Text = $"{_overallProgress.Fraction * 100:0.0}%";
            _bytes.Text = TransferTracker.ByteCounts(progress.BytesComplete, progress.BytesMax);
            }
        else {
            _overallProgress.Pulse();
            }

        string operation = _extracting ? "Extraction" : "Archive";
        Window.Title = $"MNX {operation} Progress [{_overallProgress.Fraction * 100:0.0}%]";
        }

    private static double Fraction(long done, long total) {
        return total == 0 ? 1 : Math.Clamp((double)done / total, 0, 1);
        }

    public void Dispose() {
        Window.Hide();
        Window.Dispose();
        }
    }
