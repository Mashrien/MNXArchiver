using System.Runtime.InteropServices;

namespace Mash.GUI.MNXArchiver.Plugins;

internal sealed unsafe class NativeModuleEntryStream : Stream {
    private readonly object _gate = new object();
    private readonly EntryStreamHandle _handle;
    private readonly delegate* unmanaged[Cdecl]<nint, long, byte*, int, nint, nint, int*, int> _readEntryAt;
    private readonly CancellationToken _cancellation;
    private readonly long _length;
    private long _position;

    internal NativeModuleEntryStream(nint handle,
        delegate* unmanaged[Cdecl]<nint, long, byte*, int, nint, nint, int*, int> readEntryAt,
        delegate* unmanaged[Cdecl]<nint, void> closeEntryStream, long length, CancellationToken cancellation) {
        _readEntryAt = readEntryAt;
        _cancellation = cancellation;
        _length = length;
        _handle = new EntryStreamHandle(handle, closeEntryStream, cancellation);
        }

    public override bool CanRead => !_handle.IsClosed;
    public override bool CanSeek => !_handle.IsClosed;
    public override bool CanWrite => false;

    public override long Length {
        get {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _length;
            }
        }

    public override long Position {
        get {
            lock (_gate) {
                ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
                return _position;
                }
            }
        set => Seek(value, SeekOrigin.Begin);
        }

    public override int Read(byte[] buffer, int offset, int count) {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
        }

    public override int Read(Span<byte> buffer) {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            _cancellation.ThrowIfCancellationRequested();
            if (buffer.IsEmpty || _position >= _length) {
                return 0;
                }

            int count = (int)Math.Min(buffer.Length, _length - _position);
            int bytesRead = 0;
            int status;
            fixed (byte* destination = buffer) {
                status = _readEntryAt(_handle.DangerousGetHandle(), _position, destination, count,
                    _handle.CancelCallback, _handle.CancelState, &bytesRead);
                GC.KeepAlive(_handle);
                }
            if (status == PluginProtocol.EntryStreamReadCancelled) {
                throw new OperationCanceledException(_cancellation);
                }
            if (status != 0) {
                throw new IOException($"The archive module could not read the entry stream (status {status}).");
                }
            if (bytesRead != count) {
                throw new IOException("The archive module returned an unexpected entry stream read length.");
                }
            _position += bytesRead;
            return bytesRead;
            }
        }

    public override long Seek(long offset, SeekOrigin origin) {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            _cancellation.ThrowIfCancellationRequested();
            long start = origin switch {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => _position,
                SeekOrigin.End => _length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };
            long position;
            try {
                position = checked(start + offset);
                }
            catch (OverflowException error) {
                throw new IOException("The entry stream position is out of range.", error);
                }
            if (position < 0) {
                throw new IOException("Cannot seek before the start of an archive entry.");
                }
            _position = position;
            return position;
            }
        }

    public override void Flush() {
        ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
        }

    public override void SetLength(long value) => throw new NotSupportedException("The entry stream is read-only.");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("The entry stream is read-only.");

    protected override void Dispose(bool disposing) {
        if (disposing) {
            lock (_gate) {
                _handle.Dispose();
                }
            }
        base.Dispose(disposing);
        }

    private sealed class EntryStreamHandle : SafeHandle {
        private readonly delegate* unmanaged[Cdecl]<nint, void> _closeEntryStream;
        private GCHandle _cancelState;

        internal EntryStreamHandle(nint stream, delegate* unmanaged[Cdecl]<nint, void> closeEntryStream,
            CancellationToken cancellation) : base(IntPtr.Zero, ownsHandle: true) {
            _closeEntryStream = closeEntryStream;
            if (cancellation.CanBeCanceled) {
                _cancelState = GCHandle.Alloc(cancellation);
                }
            SetHandle(stream);
            }

        public override bool IsInvalid => handle == 0;
        internal nint CancelCallback => _cancelState.IsAllocated
            ? (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NativeArchiveModule.IsCancelled : 0;
        internal nint CancelState => _cancelState.IsAllocated ? GCHandle.ToIntPtr(_cancelState) : 0;

        protected override bool ReleaseHandle() {
            _closeEntryStream(handle);
            if (_cancelState.IsAllocated) {
                _cancelState.Free();
                }
            return true;
            }
        }
    }
