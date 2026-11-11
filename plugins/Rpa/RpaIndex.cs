using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Mash.UI.MNXFile.GUI.Plugins.Rpa;

internal sealed record RpaSegment(long Offset, long Length, byte[] Prefix);
internal sealed record RpaEntry(RpaSegment[] Segments, long Size);

internal sealed class RpaIndex(uint key, byte pickleProtocol = 2) {
    internal const int HeaderLength = 34;
    internal const int BufferSize = 128 * 1024;
    internal const uint DefaultKey = 0x42424242;
    private const int MaxIndexBytes = 64 * 1024 * 1024;
    private const int MaxCompressedIndexBytes = MaxIndexBytes + BufferSize;
    // An escaped filename uses at most six bytes per UTF-16 code unit.
    // This budget leaves more than 48 bytes per entry plus the response envelope.
    private const int MaxTotalPathCharacters = 8 * 1024 * 1024;
    private const int MaxSegments = 1_000_000;
    internal const int MaxPrefixBytes = 64 * 1024 * 1024;
    private const int PickleFrameTargetBytes = 64 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal uint Key { get; } = key;
    internal byte PickleProtocol { get; } = pickleProtocol;
    internal Dictionary<string, RpaEntry> Entries { get; } = new(StringComparer.Ordinal);

    internal static RpaIndex Read(FileStream archive, RpaCancellation cancellation) {
        Span<byte> header = stackalloc byte[HeaderLength];
        archive.ReadExactly(header);
        if (!header.StartsWith("RPA-3.0 "u8) || header[24] != (byte)' ')
            throw new InvalidDataException("The file does not have an RPA 3.0 header.");

        int headerLength = HeaderLength;
        if (header[33] == (byte)'\r' && archive.ReadByte() == (byte)'\n')
            headerLength++;
        else if (header[33] != (byte)'\n')
            throw new InvalidDataException("The RPA 3.0 header has an invalid line ending.");

        if (!long.TryParse(header[8..24], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                out long indexOffset)
            || !uint.TryParse(header[25..33], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                out uint key)
            || indexOffset < headerLength || indexOffset > archive.Length - 8)
            throw new InvalidDataException("The RPA 3.0 header has an invalid index offset or key.");

        if (archive.Length - indexOffset > MaxCompressedIndexBytes)
            throw new InvalidDataException("The compressed RPA index exceeds the size limit.");

        Span<byte> trailer = stackalloc byte[sizeof(uint)];
        archive.Position = archive.Length - trailer.Length;
        archive.ReadExactly(trailer);
        uint expectedChecksum = BinaryPrimitives.ReadUInt32BigEndian(trailer);
        archive.Position = indexOffset;

        using MemoryStream decoded = new();
        uint checksum = 1;
        using (ZLibStream compressed = new(archive, CompressionMode.Decompress, leaveOpen: true)) {
            byte[] buffer = new byte[BufferSize];
            while (true) {
                cancellation.Check();
                int count = compressed.Read(buffer);
                if (count == 0)
                    break;
                if (count > MaxIndexBytes - decoded.Length)
                    throw new InvalidDataException("The RPA index exceeds the decompression limit.");
                decoded.Write(buffer, 0, count);
                checksum = UpdateAdler32(checksum, buffer.AsSpan(0, count));
                }
            }

        // ZLibStream can return the decoded bytes even if the checksum trailer was truncated.
        if (checksum != expectedChecksum)
            throw new InvalidDataException("The RPA index has a missing or invalid zlib checksum.");

        RpaPickleReader pickle = new(decoded.GetBuffer(), (int)decoded.Length, cancellation);
        return Decode(pickle.Read(), key, pickle.Protocol, headerLength, indexOffset, cancellation);
        }

    private static uint UpdateAdler32(uint checksum, ReadOnlySpan<byte> bytes) {
        const uint modulus = 65521;
        uint first = checksum & 0xffff;
        uint second = checksum >> 16;

        while (!bytes.IsEmpty) {
            // Reducing every 4096 bytes keeps both sums in the uint range.
            int count = Math.Min(bytes.Length, 4096);
            foreach (byte value in bytes[..count]) {
                first += value;
                second += first;
                }
            first %= modulus;
            second %= modulus;
            bytes = bytes[count..];
            }

        return (second << 16) | first;
        }

    private static RpaIndex Decode(Dictionary<string, object> values, uint key, byte protocol, int headerLength,
        long indexOffset, RpaCancellation cancellation) {
        RpaIndex index = new(key, protocol);
        int remainingSegments = MaxSegments;
        int remainingPrefixBytes = MaxPrefixBytes;

        foreach ((string path, object rawSegments) in values) {
            cancellation.Check();
            if (rawSegments is not List<object> segments || segments.Count == 0)
                throw new InvalidDataException($"The RPA entry '{path}' has an invalid segment list.");
            if (segments.Count > remainingSegments)
                throw new InvalidDataException("The RPA index contains too many segments.");

            remainingSegments -= segments.Count;
            RpaSegment[] decoded = new RpaSegment[segments.Count];
            int tupleLength = 0;
            long size = 0;

            for (int number = 0; number < decoded.Length; number++) {
                if (number % 1024 == 0)
                    cancellation.Check();
                if (segments[number] is not object[] fields || fields.Length < 2 || fields.Length > 3
                    || (tupleLength != 0 && fields.Length != tupleLength)
                    || fields[0] is not long encodedOffset || fields[1] is not long encodedLength)
                    throw new InvalidDataException($"The RPA entry '{path}' has an invalid segment.");

                tupleLength = fields.Length;
                long offset = encodedOffset ^ key;
                long length = encodedLength ^ key;
                if (offset < headerLength || offset > indexOffset || length < 0 || length > indexOffset - offset)
                    throw new InvalidDataException($"The RPA entry '{path}' points outside the archive data.");

                byte[] prefix = Array.Empty<byte>();
                if (fields.Length == 3 && !RpaPickleReader.IsNone(fields[2])) {
                    object rawPrefix = fields[2];
                    int prefixLength = rawPrefix switch {
                        byte[] bytes => bytes.Length,
                        string text => text.Length,
                        _ => throw new InvalidDataException($"The RPA entry '{path}' has an invalid prefix."),
                        };
                    if (prefixLength > remainingPrefixBytes)
                        throw new InvalidDataException("The RPA index contains too many prefix bytes.");
                    remainingPrefixBytes -= prefixLength;
                    prefix = rawPrefix is byte[] bytesPrefix ? bytesPrefix
                        : RpaPickleReader.ToLatin1((string)rawPrefix, cancellation);
                    }

                size = checked(size + length + prefix.Length);
                decoded[number] = new RpaSegment(offset, length, prefix);
                }

            index.Entries.Add(path, new RpaEntry(decoded, size));
            }

        index.ValidatePaths(cancellation);
        return index;
        }

    internal void ValidatePaths(RpaCancellation cancellation) {
        ValidatePaths(Entries, cancellation);
        }

    internal static void ValidatePaths<T>(Dictionary<string, T> entries, RpaCancellation cancellation) {
        if (entries.Count > PluginProtocol.MaxEntries)
            throw new InvalidDataException("The RPA index contains too many entries.");

        int remainingCharacters = MaxTotalPathCharacters;
        Dictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> lookup =
            entries.GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (string path in entries.Keys) {
            cancellation.Check();
            RpaPaths.ValidateEntryPath(path);
            if (path.Length > remainingCharacters)
                throw new InvalidDataException("The RPA index contains too many filename characters.");
            remainingCharacters -= path.Length;
            }

        // A folder's descendants are adjacent in ordinal order. Count each folder once
        // without allocating each folder name.
        string[] paths = new string[entries.Count];
        entries.Keys.CopyTo(paths, 0);
        Array.Sort(paths, StringComparer.Ordinal);
        string previous = "";
        int treeEntries = entries.Count;

        foreach (string path in paths) {
            cancellation.Check();
            int sharedPrefix = path.AsSpan().CommonPrefixLength(previous.AsSpan());

            int separator = path.IndexOf('/', sharedPrefix);
            while (separator >= 0) {
                if (lookup.ContainsKey(path.AsSpan(0, separator)))
                    throw new InvalidDataException($"The RPA entry '{path}' conflicts with a file in a parent folder.");
                if (++treeEntries > PluginProtocol.MaxEntries)
                    throw new InvalidDataException("The RPA archive tree contains too many entries.");
                separator = path.IndexOf('/', separator + 1);
                }
            previous = path;
            }
        }

    internal PluginEntry[] List(RpaCancellation cancellation) {
        PluginEntry[] entries = new PluginEntry[Entries.Count];
        int index = 0;
        foreach ((string path, RpaEntry entry) in Entries) {
            cancellation.Check();
            entries[index++] = new PluginEntry(path, entry.Size);
            }
        return entries;
        }

    internal void Write(FileStream archive, RpaCancellation cancellation) {
        long indexOffset = archive.Position;
        using (ZLibStream compressed = new(archive, CompressionLevel.Optimal, leaveOpen: true)) {
            compressed.WriteByte(0x80); // PROTO
            compressed.WriteByte(PickleProtocol);
            using MemoryStream? frame = PickleProtocol >= 4 ? new MemoryStream(PickleFrameTargetBytes) : null;
            Stream payload = frame is null ? compressed : frame;
            using BinaryWriter pickle = new(payload, Utf8, leaveOpen: true);

            // The index only needs Unicode keys and two-integer tuples.
            pickle.Write((byte)'}');
            foreach ((string name, RpaEntry entry) in Entries) {
                cancellation.Check();
                WriteEntry(pickle, name, entry.Segments[0]);
                if (frame is not null && frame.Length >= PickleFrameTargetBytes)
                    WriteFrame(compressed, frame);
                }
            pickle.Write((byte)'.');
            if (frame is not null)
                WriteFrame(compressed, frame);
            }

        archive.Position = 0;
        string header = string.Create(CultureInfo.InvariantCulture, $"RPA-3.0 {indexOffset:x16} {Key:x8}\n");
        archive.Write(Encoding.ASCII.GetBytes(header));
        }

    private void WriteEntry(BinaryWriter pickle, string name, RpaSegment segment) {
        byte[] path = Utf8.GetBytes(name);
        if (PickleProtocol >= 4 && path.Length <= byte.MaxValue) {
            pickle.Write((byte)0x8c); // SHORT_BINUNICODE
            pickle.Write((byte)path.Length);
            }
        else {
            pickle.Write((byte)'X'); // BINUNICODE
            pickle.Write(path.Length);
            }
        pickle.Write(path);
        pickle.Write((byte)']');
        WriteInteger(pickle, segment.Offset ^ Key);
        WriteInteger(pickle, segment.Length ^ Key);
        pickle.Write((byte)0x86); // TUPLE2
        pickle.Write((byte)'a');
        pickle.Write((byte)'s');
        }

    private static void WriteFrame(ZLibStream compressed, MemoryStream frame) {
        Span<byte> header = stackalloc byte[1 + sizeof(long)];
        header[0] = 0x95; // FRAME
        BinaryPrimitives.WriteInt64LittleEndian(header[1..], frame.Length);
        compressed.Write(header);
        compressed.Write(frame.GetBuffer().AsSpan(0, (int)frame.Length));
        frame.SetLength(0);
        }

    private static void WriteInteger(BinaryWriter pickle, long value) {
        if (value <= int.MaxValue) {
            pickle.Write((byte)'J');
            pickle.Write((int)value);
            return;
            }

        int length = 1;
        long remaining = value;
        while (remaining > sbyte.MaxValue) {
            length++;
            remaining >>= 8;
            }

        pickle.Write((byte)0x8a); // LONG1
        pickle.Write((byte)length);
        for (int number = 0; number < length; number++) {
            pickle.Write((byte)value);
            value >>= 8;
            }
        }
    }
