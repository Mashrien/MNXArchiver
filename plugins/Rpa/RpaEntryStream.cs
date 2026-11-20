using Microsoft.Win32.SafeHandles;

namespace Mash.GUI.MNXArchiver.Plugins.Rpa;

internal sealed class RpaEntryStream : IDisposable {
    private readonly FileStream _archive;
    private readonly SafeFileHandle _fileHandle;
    private readonly RpaEntry _entry;
    private readonly long[] _segmentEnds;

    internal RpaEntryStream(FileStream archive, RpaEntry entry, RpaCancellation cancellation) {
        _archive = archive;
        _fileHandle = archive.SafeFileHandle;
        _entry = entry;
        _segmentEnds = new long[entry.Segments.Length];

        long end = 0;
        for (int index = 0; index < entry.Segments.Length; index++) {
            cancellation.Check();
            RpaSegment segment = entry.Segments[index];
            end += segment.Prefix.Length + segment.Length;
            _segmentEnds[index] = end;
            }
        }

    internal int ReadAt(long position, Span<byte> destination, RpaCancellation cancellation) {
        cancellation.Check();
        if (position >= _entry.Size || destination.IsEmpty) {
            return 0;
            }

        int count = (int)Math.Min(destination.Length, _entry.Size - position);
        Span<byte> remaining = destination[..count];
        int index = FindSegment(position);

        while (!remaining.IsEmpty) {
            RpaSegment segment = _entry.Segments[index];
            long segmentStart = index == 0 ? 0 : _segmentEnds[index - 1];
            long segmentOffset = position - segmentStart;
            if (segmentOffset < segment.Prefix.Length) {
                int prefixCount = (int)Math.Min(Math.Min(remaining.Length, RpaIndex.BufferSize),
                    segment.Prefix.Length - segmentOffset);
                segment.Prefix.AsSpan((int)segmentOffset, prefixCount).CopyTo(remaining);
                remaining = remaining[prefixCount..];
                position += prefixCount;
                }
            else if (position < _segmentEnds[index]) {
                int fileCount = (int)Math.Min(Math.Min(remaining.Length, RpaIndex.BufferSize),
                    _segmentEnds[index] - position);
                long fileOffset = segment.Offset + (segmentOffset - segment.Prefix.Length);
                int bytesRead = RandomAccess.Read(_fileHandle, remaining[..fileCount], fileOffset);
                if (bytesRead == 0) {
                    throw new EndOfStreamException("The RPA archive ended within a streamed entry.");
                    }
                remaining = remaining[bytesRead..];
                position += bytesRead;
                }

            if (position == _segmentEnds[index]) {
                index++;
                }
            cancellation.Check();
            }

        return count;
        }

    private int FindSegment(long position) {
        // Empty segments can share an end offset. Find the first end strictly past position.
        int first = 0;
        int last = _segmentEnds.Length;
        while (first < last) {
            int middle = first + (last - first) / 2;
            if (_segmentEnds[middle] <= position) {
                first = middle + 1;
                }
            else {
                last = middle;
                }
            }
        return first;
        }

    public void Dispose() {
        _archive.Dispose();
        }
    }
