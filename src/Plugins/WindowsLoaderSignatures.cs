using System.Buffers.Binary;

namespace Mash.GUI.MNXArchiver.Plugins;

internal readonly record struct WindowsTlsSymbols(int HandleTlsRva, int ReleaseTlsRva, int TlsLockRva);

/// <summary>Locates the static TLS routines and checks their shared lock against exported SRW calls.</summary>
internal static class WindowsLoaderSignatures {
    private static readonly Caller[] _callers = [
        new("48 8B 4B 30 66 39 79 6E 75 ?? E8 ?? ?? ?? ?? 44 8B C0 85 C0 78 ?? 41 8B C0", 10, 8, 22),
        new("48 8B 4F 38 66 39 71 6E 75 ?? E8 ?? ?? ?? ?? 8B D8 85 C0 78 ?? E8 ?? ?? ?? ??", 10, 8, 21),
        new("66 39 6E 6E 75 ?? 48 8B CE E8 ?? ?? ?? ?? 8B D8 85 C0 78 ?? E8 ?? ?? ?? ??", 9, 4, 20),
        new("66 83 7E 6E 00 75 ?? 48 8B CE E8 ?? ?? ?? ?? 8B D8 85 C0 78 ?? E8 ?? ?? ?? ??", 10, 5, 21),
        new("66 83 7E 6E 00 75 ?? 48 8B CE E8 ?? ?? ?? ?? 8B D8 85 C0 79 ?? EB ??", 10, 5, null, 19)
        ];
    private static readonly byte[] _releasePrefix = Convert.FromHexString("48895C2408574883EC20488BFA488BD94885D2750C");

    internal static WindowsTlsSymbols Resolve(ReadOnlySpan<byte> code, int codeRva, int dataRva, int dataLength,
        ReadOnlySpan<byte> exceptionTable, int acquireRva, int releaseRva) {
        SortedList<int, int> functions = ReadFunctions(exceptionTable, codeRva, code.Length);
        HashSet<int> callerTargets = FindCallerTargets(code, codeRva, functions);
        if (callerTargets.Count != 1) {
            throw new NotSupportedException("The Windows TLS registration signature is missing or ambiguous.");
            }

        int handleTls = callerTargets.Single();
        int lockRva = FindTlsLock(code, codeRva, dataRva, dataLength,
            handleTls, functions[handleTls], acquireRva, releaseRva);
        if (lockRva == 0) {
            throw new NotSupportedException("The Windows TLS registration routine did not reference a valid lock.");
            }

        int releaseTls = 0;
        foreach ((int begin, int end) in functions) {
            if (!code.Slice(begin - codeRva, end - begin).StartsWith(_releasePrefix)) {
                continue;
                }
            if (FindTlsLock(code, codeRva, dataRva, dataLength, begin, end, acquireRva, releaseRva) != lockRva) {
                continue;
                }
            if (releaseTls != 0) {
                throw new NotSupportedException("The Windows TLS release signature is ambiguous.");
                }
            releaseTls = begin;
            }
        if (releaseTls == 0) {
            throw new NotSupportedException("The Windows TLS routines do not share a verified lock.");
            }
        return new WindowsTlsSymbols(handleTls, releaseTls, lockRva);
        }

    private static SortedList<int, int> ReadFunctions(ReadOnlySpan<byte> table, int codeRva, int codeLength) {
        const int runtimeFunctionSize = 12;
        if (table.IsEmpty || table.Length % runtimeFunctionSize != 0) {
            throw new BadImageFormatException("The Windows unwind table has an invalid length.");
            }
        int endRva = checked(codeRva + codeLength);
        SortedList<int, int> functions = new SortedList<int, int>();
        for (int offset = 0; offset < table.Length; offset += runtimeFunctionSize) {
            int start = BinaryPrimitives.ReadInt32LittleEndian(table[offset..]);
            int end = BinaryPrimitives.ReadInt32LittleEndian(table[(offset + 4)..]);
            if (start >= codeRva && end <= endRva && end > start) {
                functions.TryAdd(start, end);
                }
            }
        return functions;
        }

    private static HashSet<int> FindCallerTargets(ReadOnlySpan<byte> code, int codeRva,
        SortedList<int, int> functions) {
        HashSet<int> targets = new HashSet<int>();
        foreach (Caller caller in _callers) {
            int current = 0;
            int last = code.Length - caller.Pattern.Length;
            while (current <= last) {
                int relative = code[current..].IndexOf(caller.FirstByte);
                if (relative < 0 || current + relative > last) {
                    break;
                    }
                int candidate = current + relative;
                current = candidate + 1;
                ReadOnlySpan<byte> bytes = code.Slice(candidate, caller.Pattern.Length);
                if (!caller.Matches(bytes) || !caller.BranchesMatch(bytes)) {
                    continue;
                    }

                int address = codeRva + candidate;
                if (!InsideFunction(functions, address, address + bytes.Length)) {
                    continue;
                    }
                int callOffset = caller.CallOffset;
                int displacement = BinaryPrimitives.ReadInt32LittleEndian(bytes[(callOffset + 1)..]);
                long target = (long)address + callOffset + 5 + displacement;
                if (target >= codeRva && target < (long)codeRva + code.Length
                    && functions.ContainsKey((int)target)) {
                    targets.Add((int)target);
                    }
                }
            }
        return targets;
        }

    private static bool InsideFunction(SortedList<int, int> functions, int start, int end) {
        IList<int> starts = functions.Keys;
        int low = 0;
        int high = starts.Count;
        while (low < high) {
            int middle = low + (high - low) / 2;
            if (starts[middle] <= start) {
                low = middle + 1;
                }
            else {
                high = middle;
                }
            }
        return low > 0 && end <= functions.Values[low - 1];
        }

    private static int FindTlsLock(ReadOnlySpan<byte> code, int codeRva, int dataRva, int dataLength,
        int begin, int end, int acquireRva, int releaseRva) {
        HashSet<int> acquisitions = new HashSet<int>();
        HashSet<int> releases = new HashSet<int>();
        ReadOnlySpan<byte> body = code.Slice(begin - codeRva, end - begin);
        // LEA RCX,[RIP+disp32] puts the lock in the first argument to the following CALL.
        for (int offset = 0; offset <= body.Length - 12; offset++) {
            if (!body[offset..].StartsWith<byte>([0x48, 0x8d, 0x0d]) || body[offset + 7] != 0xe8) {
                continue;
                }

            long operand = (long)begin + offset + 7 + BinaryPrimitives.ReadInt32LittleEndian(body[(offset + 3)..]);
            long target = (long)begin + offset + 12 + BinaryPrimitives.ReadInt32LittleEndian(body[(offset + 8)..]);
            if (operand < dataRva || operand + 8 > (long)dataRva + dataLength || (operand & 7) != 0) {
                continue;
                }
            if (target == acquireRva) {
                acquisitions.Add((int)operand);
                }
            if (target == releaseRva) {
                releases.Add((int)operand);
                }
            }
        acquisitions.IntersectWith(releases);
        return acquisitions.Count == 1 ? acquisitions.Single() : 0;
        }

    private sealed class Caller {
        private readonly int _skipOffset;
        private readonly int? _skipDestination;
        private readonly int _successOffset;
        internal int CallOffset { get; }
        internal int[] Pattern { get; }
        internal byte FirstByte => (byte)Pattern[0];

        internal Caller(string pattern, int callOffset, int skipOffset, int? skipDestination, int successOffset = 0) {
            Pattern = pattern.Split(' ').Select(token => token == "??" ? -1 : Convert.ToInt32(token, 16)).ToArray();
            CallOffset = callOffset;
            _skipOffset = skipOffset;
            _skipDestination = skipDestination;
            _successOffset = successOffset;
            }

        internal bool Matches(ReadOnlySpan<byte> bytes) {
            for (int index = 0; index < Pattern.Length; index++) {
                if (Pattern[index] >= 0 && bytes[index] != Pattern[index]) {
                    return false;
                    }
                }
            return true;
            }

        internal bool BranchesMatch(ReadOnlySpan<byte> bytes) {
            int skip = _skipOffset + 2 + (sbyte)bytes[_skipOffset + 1];
            int expected = _skipDestination ?? (_successOffset + 2 + (sbyte)bytes[_successOffset + 1]);
            return skip == expected;
            }
        }
    }
