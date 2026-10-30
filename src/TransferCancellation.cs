using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;

namespace Mash.UI.MNXFile.GUI;
/// <summary>Pause between files while the user chooses a cancellation outcome.</summary>
internal sealed class TransferCancellation : IDisposable {
    private enum Choice {
        Continue,
        Pending ,
        Keep    ,
        Delete  ,
        }

    private int _choice;
    private readonly ManualResetEventSlim _decision = new(true);
    private readonly CancellationTokenSource _force = new();
    internal CancellationToken Token => _force.Token;
    internal bool Keep => Volatile.Read(ref _choice) == (int)Choice.Keep;
    internal bool Delete => Volatile.Read(ref _choice) == (int)Choice.Delete;
    internal bool ExportKeyFile { get; private set; }

    internal void BeginChoice() {
        _decision.Reset();
        Volatile.Write(ref _choice, (int)Choice.Pending);
        }

    internal void Choose(bool keep, bool exportKeyFile = false) {
        ExportKeyFile = keep && exportKeyFile;
        Volatile.Write(ref _choice, (int)(keep ? Choice.Keep : Choice.Delete));
        if (!keep) {
            _force.Cancel();
            }

        _decision.Set();
        }

    internal void CheckBoundary() {
        // The current file continues during the question. Starting another file
        // or publishing the archive waits until the user has made a choice.
        if (Volatile.Read(ref _choice) == (int)Choice.Pending) {
            _decision.Wait();
            }

        Token.ThrowIfCancellationRequested();
        if (Keep) {
            throw new OperationCanceledException("Stopped after the completed file.");
            }
        }

    public void Dispose() {
        _force.Dispose();
        _decision.Dispose();
        }
    }
