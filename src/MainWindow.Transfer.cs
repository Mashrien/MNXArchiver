using System.Diagnostics;
using Gtk;
using MNX;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainWindow {
    private T RunTransfer<T>(
        string description,
        Func<T> action,
        TransferTracker? tracker = null,
        Archive? targetArchive = null,
        string? displayPath = null,
        bool creatingEncrypted = false,
        TransferCancellation? cancellation = null,
        bool extracting = false
        ) {
        if (_operationActive) {
            throw new InvalidOperationException("An archive operation is already running.");
            }

        _operationActive = true;
        StopPreviewRead();
        _transferProgress = null;
        UpdateActions();
        ProgressBar? progress = Widget<ProgressBar>("operation_progress");
        progress.Show();
        progress.ShowText = true;
        progress.Text = description;
        _status.Text = description;
        void OnProgress(object? sender, OperationProgressArgs args) {
            tracker?.Update(args);
            Volatile.Write(ref _transferProgress, args);
            }

        Archive? archive = targetArchive ?? _archive!;
        string compression = (archive.IsOpen ? archive.Compression : archive.Options.Compression).ToString();
        int level = archive.IsOpen ? archive.CompressionLevel : archive.Options.CompressionLevel;
        if (compression == "Brotli") {
            compression += $", Level {level}";
            }

        using ArchiveProgressDialog dialog = new ArchiveProgressDialog(
            _window,
            displayPath ?? archive.ArchivePath ?? "MNX archive",
            compression,
            archive.IsOpen ? archive.IsEncrypted : creatingEncrypted,
            tracker,
            extracting,
            cancellation is not null
            );
        _activeProgressDialog = dialog;
        CancellationToken previousCancellation = archive.Options.CancellationToken;
        if (cancellation is not null) {
            archive.Options.CancellationToken = cancellation.Token;
            dialog.CancelRequested += () => {
                if (extracting) {
                    dialog.ShowExtractionCancellationChoice(cancellation);
                    }
                else {
                    dialog.ShowCancellationChoice(cancellation, targetArchive is null);
                    }
                };
            }

        bool previousSensitivity = _window.Sensitive;
        _window.Sensitive = false;
        _window.Hide();
        archive.ProgressReport += OnProgress;
        try {
            Task<T>? task = Task.Run(action);
            T result = WaitForTransfer(task, () => {
                OperationProgressArgs? current = Volatile.Read(ref _transferProgress);
                dialog.Update(current);
                if (current is not null) {
                    progress.Fraction = current.BytesMax > 0 ? Math.Clamp((double)current.BytesComplete / current.BytesMax, 0, 1) : 0;
                    progress.Text = $"{current.BytesComplete:N0} / {current.BytesMax:N0} bytes";
                    _status.Text = description + ": " + current.FileName;
                    }
                else {
                    progress.Pulse();
                    }
                });
            dialog.Update(Volatile.Read(ref _transferProgress));
            return result;
            }
        finally {
            _activeProgressDialog = null;
            archive.ProgressReport -= OnProgress;
            archive.Options.CancellationToken = previousCancellation;
            dialog.Window.Hide();
            _window.Sensitive = previousSensitivity;
            _window.ShowAll();
            progress.Hide();
            _operationActive = false;
            UpdateActions();
            if (_quitRequested) {
                RequestQuit();
                }
            }
        }

    private static T WaitForTransfer<T>(Task<T> task, System.Action updateProgress) {
        Stopwatch refresh = Stopwatch.StartNew();
        while (!task.IsCompleted) {
            for (int i = 0; i < 32 && Application.EventsPending(); i++) {
                Application.RunIteration(false);
                }

            // Keep pumping input for cancellation and redraw on a fixed cadence.
            // Workers never wait for widget updates.
            if (refresh.ElapsedMilliseconds >= 100) {
                updateProgress();
                refresh.Restart();
                }

            Thread.Sleep(10);
            }

        return task.GetAwaiter().GetResult();
        }
    }
