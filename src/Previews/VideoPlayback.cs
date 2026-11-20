using System.Diagnostics;
using System.Security.Cryptography;
using Mash.GUI.MNXArchiver.Plugins;

namespace Mash.GUI.MNXArchiver.Previews;

/// <summary>Decodes on one worker and publishes frames on their presentation clock.</summary>
internal sealed class VideoPlayback {
    private readonly object _gate = new object();
    private readonly NativeVideoStream _decoder;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _worker;
    private readonly int _stride;
    private byte[] _front;
    private byte[] _back;
    private long _positionMs;
    private long _clockOriginMs;
    private long _clockStart;
    private long? _requestedSeek;
    private long _seekRevision;
    private long _frameNumber;
    private long _displayedFrame;
    private bool _playing = true;
    private bool _ended;
    private Exception? _error;
    private Task? _stopTask;

    internal VideoInfo Info { get; }
    internal long PositionMs { get { lock (_gate) { return _positionMs; } } }
    internal bool IsPlaying { get { lock (_gate) { return _playing; } } }
    internal bool HasEnded { get { lock (_gate) { return _ended; } } }
    internal Exception? Error { get { lock (_gate) { return _error; } } }

    private VideoPlayback(NativeVideoStream decoder, CancellationTokenSource cancellation, long memoryLimit) {
        _decoder = decoder;
        _cancellation = cancellation;
        Info = decoder.Info;
        _stride = checked(Info.Width * 4);
        int bytes = checked(_stride * Info.Height);
        // Two playback buffers plus one display surface reserved by the frontend.
        if (3L * bytes > memoryLimit) {
            throw new IOException("The video frame buffers exceed the configured preview memory limit.");
            }

        _front = new byte[bytes];
        _back = new byte[bytes];
        _clockStart = Stopwatch.GetTimestamp();
        _worker = Task.Run(Run);
        }

    internal static async Task<VideoPlayback> OpenAsync(NativeVideoProvider provider,
        Func<CancellationToken, Stream> openSource, bool halfResolution, long memoryLimit,
        CancellationToken cancellation = default) {
        CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        try {
            return await Task.Run(() => Open(provider, openSource, halfResolution, memoryLimit, lifetime)).ConfigureAwait(false);
            }
        catch {
            lifetime.Dispose();
            throw;
            }
        }

    private static VideoPlayback Open(NativeVideoProvider provider, Func<CancellationToken, Stream> openSource,
        bool halfResolution, long memoryLimit, CancellationTokenSource lifetime) {
        lifetime.Token.ThrowIfCancellationRequested();
        NativeVideoStream decoder = provider.Open(openSource(lifetime.Token), halfResolution, lifetime.Token);
        try {
            return new VideoPlayback(decoder, lifetime, memoryLimit);
            }
        catch {
            decoder.Dispose();
            throw;
            }
        }

    internal void Play() {
        lock (_gate) {
            if (_stopTask is not null || _error is not null || _playing) {
                return;
                }
            if (_ended) {
                RequestSeek(0);
                }
            _playing = true;
            ResetClock(_positionMs);
            Monitor.PulseAll(_gate);
            }
        }

    internal void Pause() {
        lock (_gate) {
            _playing = false;
            Monitor.PulseAll(_gate);
            }
        }

    internal void Seek(long targetMs) {
        lock (_gate) {
            if (_stopTask is not null || _error is not null) {
                return;
                }
            long limit = Info.DurationMs >= 0 ? Info.DurationMs : long.MaxValue;
            RequestSeek(Math.Clamp(targetMs, 0, limit));
            Monitor.PulseAll(_gate);
            }
        }

    private void RequestSeek(long positionMs) {
        _requestedSeek = positionMs;
        _seekRevision++;
        _ended = false;
        }

    private void ResetClock(long positionMs) {
        _clockOriginMs = positionMs;
        _clockStart = Stopwatch.GetTimestamp();
        }

    internal bool TryCopyFrame(Span<byte> destination, int stride) {
        if (stride < _stride || (long)stride * Info.Height > destination.Length) {
            throw new ArgumentException("The video display surface is too small.", nameof(destination));
            }

        lock (_gate) {
            if (_stopTask is not null || _error is not null || _displayedFrame == _frameNumber) {
                return false;
                }
            if (stride == _stride) {
                _front.AsSpan().CopyTo(destination);
                }
            else {
                for (int row = 0; row < Info.Height; row++) {
                    _front.AsSpan(row * _stride, _stride).CopyTo(destination.Slice(row * stride, _stride));
                    }
                }
            _displayedFrame = _frameNumber;
            return true;
            }
        }

    internal Task StopAsync() {
        lock (_gate) {
            return _stopTask ??= StopWorkerAsync();
            }
        }

    private async Task StopWorkerAsync() {
        _cancellation.Cancel();
        Monitor.PulseAll(_gate);
        await _worker.ConfigureAwait(false);
        _cancellation.Dispose();
        }

    private void Run() {
        try {
            using CancellationTokenRegistration wake = _cancellation.Token.Register(Wake);
            DecodeFrames();
            }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) {
            }
        catch (Exception error) {
            RecordError(error);
            }
        finally {
            try {
                _decoder.Dispose();
                }
            catch (Exception error) {
                RecordError(error);
                }
            lock (_gate) {
                _playing = false;
                CryptographicOperations.ZeroMemory(_front);
                CryptographicOperations.ZeroMemory(_back);
                }
            }
        }

    private void Wake() {
        lock (_gate) {
            Monitor.PulseAll(_gate);
            }
        }

    private void RecordError(Exception error) {
        lock (_gate) {
            _error ??= error;
            _playing = false;
            }
        }

    private void DecodeFrames() {
        CancellationToken cancellation = _cancellation.Token;
        while (!cancellation.IsCancellationRequested) {
            long? seek;
            long revision;
            lock (_gate) {
                while (!_playing && _requestedSeek is null && !cancellation.IsCancellationRequested) {
                    Monitor.Wait(_gate);
                    }
                cancellation.ThrowIfCancellationRequested();
                seek = _requestedSeek;
                _requestedSeek = null;
                revision = _seekRevision;
                }

            if (seek.HasValue) {
                _decoder.Seek(seek.Value);
                }
            bool decoded = _decoder.DecodeNextFrame(_back, _stride, out long pts);
            lock (_gate) {
                if (_seekRevision != revision) {
                    continue;
                    }
                if (!decoded) {
                    _playing = false;
                    _ended = true;
                    continue;
                    }
                if (seek.HasValue || _frameNumber == 0) {
                    ResetClock(pts);
                    }
                PublishFrame(pts, revision, seek.HasValue, cancellation);
                }
            }
        }

    private void PublishFrame(long pts, long revision, bool seeking, CancellationToken cancellation) {
        while (!seeking && !cancellation.IsCancellationRequested && revision == _seekRevision) {
            if (!_playing) {
                Monitor.Wait(_gate);
                continue;
                }
            double delay = pts - _clockOriginMs - Stopwatch.GetElapsedTime(_clockStart).TotalMilliseconds;
            if (delay <= 0) {
                break;
                }
            Monitor.Wait(_gate, (int)Math.Min(1000, Math.Ceiling(delay)));
            }

        if (cancellation.IsCancellationRequested || revision != _seekRevision) {
            return;
            }
        (_front, _back) = (_back, _front);
        _positionMs = Math.Max(0, pts);
        _frameNumber++;
        }
    }
