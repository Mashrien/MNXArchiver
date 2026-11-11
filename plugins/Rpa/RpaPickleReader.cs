using System.Buffers.Binary;
using System.Text;

namespace Mash.UI.MNXFile.GUI.Plugins.Rpa;

internal sealed class RpaPickleReader(byte[] data, int length, RpaCancellation cancellation) {
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly object Mark = new();
    private static readonly object None = new();
    private const int MaxValues = 4_000_000;

    private enum BytesConstructor {
        EmptyBytes,
        Latin1,
        }

    private readonly List<object> stack = [];
    private readonly List<int> marks = [];
    private readonly Dictionary<int, object> memo = [];
    private int position;
    private int frameEnd;
    private int remainingLegacyPrefixBytes = RpaIndex.MaxPrefixBytes;
    internal byte Protocol { get; private set; }

    internal Dictionary<string, object> Read() {
        if (ReadByte() != 0x80)
            throw InvalidPickle("missing protocol marker");
        Protocol = ReadByte();
        if (Protocol < 2 || Protocol > 5)
            throw InvalidPickle("unsupported pickle protocol");

        for (int instruction = 0; instruction < MaxValues; instruction++) {
            if (instruction % 1024 == 0)
                cancellation.Check();

            if (position == frameEnd)
                frameEnd = 0;

            byte opcode = ReadByte();
            switch (opcode) {
                case 0x95: // FRAME
                    ReadFrame();
                    break;
                case (byte)'.': // STOP
                    if (position != length || marks.Count != 0 || stack.Count != 1
                        || stack[0] is not Dictionary<string, object> index)
                        throw InvalidPickle("expected a single archive dictionary");
                    return index;
                case (byte)'(':
                    marks.Add(stack.Count);
                    stack.Add(Mark);
                    break;
                case (byte)'}':
                    stack.Add(new Dictionary<string, object>(StringComparer.Ordinal));
                    break;
                case (byte)']':
                    stack.Add(new List<object>());
                    break;
                case (byte)')':
                    stack.Add(Array.Empty<object>());
                    break;
                case (byte)'N': // NONE
                    stack.Add(None);
                    break;
                case (byte)'d': // DICT
                    ReadDictionary();
                    break;
                case (byte)'l': // LIST
                    stack.Add(new List<object>(ReadMarkedValues()));
                    break;
                case (byte)'t': // TUPLE
                    stack.Add(ReadMarkedValues());
                    break;
                case 0x85: // TUPLE1
                case 0x86: // TUPLE2
                case 0x87: // TUPLE3
                    ReadTuple(opcode - 0x84);
                    break;
                case (byte)'s': // SETITEM
                    ReadSetItem();
                    break;
                case (byte)'u': // SETITEMS
                    object[] pairs = ReadMarkedValues();
                    SetItems(Peek<Dictionary<string, object>>(), pairs);
                    break;
                case (byte)'a': // APPEND
                    object value = Pop();
                    Peek<List<object>>().Add(value);
                    break;
                case (byte)'e': // APPENDS
                    object[] items = ReadMarkedValues();
                    Peek<List<object>>().AddRange(items);
                    break;
                case (byte)'q': // BINPUT
                    Memoize(ReadByte());
                    break;
                case (byte)'r': // LONG_BINPUT
                    Memoize(ReadInt32());
                    break;
                case 0x94: // MEMOIZE
                    Memoize(memo.Count);
                    break;
                case (byte)'h': // BINGET
                    GetMemo(ReadByte());
                    break;
                case (byte)'j': // LONG_BINGET
                    GetMemo(ReadInt32());
                    break;
                case (byte)'K': // BININT1
                    stack.Add((long)ReadByte());
                    break;
                case (byte)'M': // BININT2
                    stack.Add((long)BinaryPrimitives.ReadUInt16LittleEndian(Take(2)));
                    break;
                case (byte)'J': // BININT
                    stack.Add((long)ReadInt32());
                    break;
                case 0x8a: // LONG1
                    stack.Add(ReadLong(ReadByte()));
                    break;
                case 0x8b: // LONG4
                    stack.Add(ReadLong(ReadInt32()));
                    break;
                case (byte)'X': // BINUNICODE
                    stack.Add(Utf8.GetString(Take(ReadInt32())));
                    break;
                case 0x8c: // SHORT_BINUNICODE
                    stack.Add(Utf8.GetString(Take(ReadByte())));
                    break;
                case 0x8d: // BINUNICODE8
                    stack.Add(Utf8.GetString(Take(ReadLength64())));
                    break;
                case (byte)'U': // SHORT_BINSTRING (Python 2)
                case (byte)'C': // SHORT_BINBYTES
                    stack.Add(Take(ReadByte()).ToArray());
                    break;
                case (byte)'T': // BINSTRING (Python 2)
                case (byte)'B': // BINBYTES
                    stack.Add(Take(ReadInt32()).ToArray());
                    break;
                case 0x8e: // BINBYTES8
                    stack.Add(Take(ReadLength64()).ToArray());
                    break;
                case (byte)'c' when Protocol == 2: // GLOBAL
                    stack.Add(ReadProtocolTwoBytesConstructor());
                    break;
                case (byte)'R' when Protocol == 2: // REDUCE
                    ReadProtocolTwoBytes();
                    break;
                default:
                    throw InvalidPickle($"unsupported opcode 0x{opcode:x2}");
                }
            }

        throw InvalidPickle("too many values");
        }

    private void ReadFrame() {
        if (Protocol < 4 || frameEnd != 0)
            throw InvalidPickle("unexpected frame");

        int frameLength = ReadLength64();
        if (frameLength > length - position)
            throw InvalidPickle("frame extends beyond the index");
        frameEnd = position + frameLength;
        }

    private ReadOnlySpan<byte> Take(int count) {
        int limit = frameEnd == 0 ? length : frameEnd;
        if (count < 0 || count > limit - position)
            throw InvalidPickle("unexpected end of data");

        ReadOnlySpan<byte> value = data.AsSpan(position, count);
        position += count;
        return value;
        }

    private byte ReadByte() {
        return Take(1)[0];
        }

    private int ReadInt32() {
        return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        }

    private int ReadLength64() {
        ulong size = BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
        if (size > int.MaxValue)
            throw InvalidPickle("length exceeds the index limit");
        return (int)size;
        }

    private long ReadLong(int count) {
        if (count > sizeof(long))
            throw InvalidPickle("integer exceeds the supported range");
        ReadOnlySpan<byte> bytes = Take(count);
        if (bytes.IsEmpty)
            return 0;

        long value = (bytes[^1] & 0x80) == 0 ? 0 : -1;
        for (int index = bytes.Length - 1; index >= 0; index--)
            value = (value << 8) | bytes[index];
        return value;
        }

    private void ReadDictionary() {
        object[] pairs = ReadMarkedValues();
        Dictionary<string, object> dictionary = new(StringComparer.Ordinal);
        SetItems(dictionary, pairs);
        stack.Add(dictionary);
        }

    private void ReadSetItem() {
        object value = Pop();
        object key = Pop();
        SetItem(Peek<Dictionary<string, object>>(), key, value);
        }

    private void SetItems(Dictionary<string, object> dictionary, object[] pairs) {
        if (pairs.Length % 2 != 0)
            throw InvalidPickle("unpaired dictionary item");
        for (int index = 0; index < pairs.Length; index += 2) {
            if (index % 1024 == 0)
                cancellation.Check();
            SetItem(dictionary, pairs[index], pairs[index + 1]);
            }
        }

    private static void SetItem(Dictionary<string, object> dictionary, object key, object value) {
        string name = key switch {
            string text => text,
            byte[] bytes => Utf8.GetString(bytes),
            _ => throw InvalidPickle("an archive filename is not text"),
            };
        if (name.Length > PluginProtocol.MaxPathCharacters)
            throw InvalidPickle("an archive filename exceeds the filename limit");
        if (dictionary.Count >= PluginProtocol.MaxEntries)
            throw InvalidPickle("too many archive entries");
        if (!dictionary.TryAdd(name, value))
            throw InvalidPickle("duplicate archive filename");
        }

    private void ReadTuple(int count) {
        int start = stack.Count - count;
        if (start < 0 || (marks.Count > 0 && start <= marks[^1]))
            throw InvalidPickle("tuple has too few values");
        object[] values = CopyStackValues(start, count);
        stack.Add(values);
        }

    private object[] ReadMarkedValues() {
        if (marks.Count == 0)
            throw InvalidPickle("missing collection marker");
        int mark = marks[^1];
        marks.RemoveAt(marks.Count - 1);

        int start = mark + 1;
        object[] values = CopyStackValues(start, stack.Count - start);
        stack.RemoveAt(mark);
        return values;
        }

    private object[] CopyStackValues(int start, int count) {
        object[] values = new object[count];
        stack.CopyTo(start, values, 0, count);
        stack.RemoveRange(start, count);
        return values;
        }


    private T Peek<T>() where T : class {
        if (stack.Count == 0 || stack[^1] is not T value || ReferenceEquals(value, Mark))
            throw InvalidPickle("unexpected collection or marker");
        return value;
        }

    private object Pop() {
        object value = Peek<object>();
        stack.RemoveAt(stack.Count - 1);
        return value;
        }

    private void Memoize(int index) {
        if (index < 0 || index >= MaxValues || !memo.TryAdd(index, Peek<object>()))
            throw InvalidPickle("invalid or duplicate memo index");
        }

    private void GetMemo(int index) {
        if (!memo.TryGetValue(index, out object? value))
            throw InvalidPickle("undefined memo index");
        stack.Add(value);
        }

    private BytesConstructor ReadProtocolTwoBytesConstructor() {
        ReadOnlySpan<byte> remaining = data.AsSpan(position, length - position);
        if (remaining.StartsWith("_codecs\nencode\n"u8)) {
            Take("_codecs\nencode\n"u8.Length);
            return BytesConstructor.Latin1;
            }

        if (remaining.StartsWith("__builtin__\nbytes\n"u8)) {
            Take("__builtin__\nbytes\n"u8.Length);
            return BytesConstructor.EmptyBytes;
            }

        throw InvalidPickle("unsupported global");
        }

    private void ReadProtocolTwoBytes() {
        // Python 3 uses these pickle expressions to represent bytes in protocol 2.
        // Interpret their literal arguments directly without resolving a callable.
        object arguments = Pop();
        object constructor = Pop();
        if (constructor is BytesConstructor.EmptyBytes && arguments is object[] { Length: 0 }) {
            stack.Add(Array.Empty<byte>());
            return;
            }

        if (constructor is BytesConstructor.Latin1
            && arguments is object[] { Length: 2 } parts
            && parts[0] is string text && parts[1] is string encoding && encoding == "latin1") {
            if (text.Length > remainingLegacyPrefixBytes)
                throw InvalidPickle("the legacy prefix conversion limit was exceeded");
            remainingLegacyPrefixBytes -= text.Length;
            stack.Add(ToLatin1(text, cancellation));
            return;
            }

        throw InvalidPickle("unsupported byte string expression");
        }

    internal static bool IsNone(object value) {
        return ReferenceEquals(value, None);
        }

    internal static byte[] ToLatin1(string text, RpaCancellation cancellation) {
        byte[] value = new byte[text.Length];
        for (int index = 0; index < text.Length; index++) {
            if (index % RpaIndex.BufferSize == 0)
                cancellation.Check();
            char character = text[index];
            if (character > byte.MaxValue)
                throw InvalidPickle("a byte prefix is outside Latin-1");
            value[index] = (byte)character;
            }
        return value;
        }

    private static InvalidDataException InvalidPickle(string problem) {
        return new InvalidDataException($"Invalid RPA pickle index ({problem}).");
        }
    }
