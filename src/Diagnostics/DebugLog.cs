using System.Runtime.CompilerServices;
using System.Drawing;
using System.Text.RegularExpressions;

namespace Mash.GUI.MNXArchiver.Diagnostics;

[Flags]
internal enum LogType {
    None = 0,
    Init = 1 << 0,
    Parse = 1 << 1,
    Info = 1 << 2,
    Warning = 1 << 3,
    Error = 1 << 4,
    Critical = 1 << 5,
    IO = 1 << 6,
    FTable = 1 << 7,
    GUI = 1 << 8,
    Exception = 1 << 9,
    All = Init | Parse | Info | Warning | Error | Critical | IO | FTable | GUI | Exception,
    }

internal sealed record DebugLogRecord(long Sequence, DateTimeOffset Time, LogType Type, string Caller, string Message);
internal sealed record DebugLogBatch(IReadOnlyList<DebugLogRecord> Records, long Cursor);

/// <summary>A bounded in-memory history. Producers never call a UI dispatcher or perform file I/O.</summary>
internal sealed class DebugLogBuffer : IDisposable {
    internal const int DefaultCapacity = 4000;
    private readonly object _gate = new object();
    private readonly DebugLogRecord?[] _records;
    private readonly int _maximumMessageCharacters;
    private long _sequence;
    private int _count;
    private int _next;
    private bool _disposed;
    internal int Capacity => _records.Length;

    internal DebugLogBuffer(int capacity = DefaultCapacity, int maximumMessageCharacters = 2048) {
        if (capacity < 1 || capacity > 10000 || maximumMessageCharacters < 1 || maximumMessageCharacters > 16384) {
            throw new ArgumentOutOfRangeException(nameof(capacity));
            }
        _records = new DebugLogRecord[capacity];
        _maximumMessageCharacters = maximumMessageCharacters;
        }

    internal void Write(LogType type, string caller, string message) {
        string boundedCaller = Limit(caller, 160);
        string boundedMessage = Limit(message, _maximumMessageCharacters);
        lock (_gate) {
            if (_disposed) {
                return;
                }
            _sequence++;
            _records[_next] = new DebugLogRecord(_sequence, DateTimeOffset.Now, type, boundedCaller, boundedMessage);
            _next = (_next + 1) % _records.Length;
            _count = Math.Min(_records.Length, _count + 1);
            }
        }

    private static string Limit(string text, int maximum) {
        if (text.Length <= maximum) {
            return text;
            }
        return text[..maximum];
        }

    internal DebugLogBatch ReadBatch(long afterSequence, int maximum = 250, LogType filter = LogType.All) {
        if (maximum < 1 || maximum > 10000) {
            throw new ArgumentOutOfRangeException(nameof(maximum));
            }
        List<DebugLogRecord> batch = new List<DebugLogRecord>(Math.Min(maximum, _records.Length));
        long cursor = afterSequence;
        lock (_gate) {
            if (_disposed || _count == 0 || _sequence <= afterSequence) {
                return new DebugLogBatch(batch.AsReadOnly(), cursor);
                }
            int oldest = (_next - _count + _records.Length) % _records.Length;
            int scanned = 0;
            for (int index = 0; index < _count; index++) {
                DebugLogRecord record = _records[(oldest + index) % _records.Length]!;
                if (record.Sequence <= afterSequence) {
                    continue;
                    }
                cursor = record.Sequence;
                if ((record.Type & filter) != 0) {
                    batch.Add(record);
                    }
                scanned++;
                if (scanned >= maximum) {
                    break;
                    }
                }
            }
        return new DebugLogBatch(batch.AsReadOnly(), cursor);
        }

    public void Dispose() {
        lock (_gate) {
            _disposed = true;
            Array.Clear(_records);
            _count = 0;
            }
        }
    }

internal static class DebugLog {
    private static readonly Regex SecretAssignments = new Regex(
        "(?<label>\\b(?:password|passwd|passphrase|token|api[_-]?key|key|secret|authorization)\\b[\"']?\\s*[:=]\\s*)(?:Bearer\\s+[^\\s,;]+|\"[^\"]*\"|'[^']*'|[^\\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(50));

    internal static DebugLogBuffer Buffer { get; } = new DebugLogBuffer();

    internal static void Write(string message, LogType type = LogType.Info,
        [CallerMemberName] string member = "", [CallerFilePath] string source = "") {
        string caller = Path.GetFileNameWithoutExtension(source) + "->" + member;
        Buffer.Write(type, caller, message);
        }

    internal static void Exception(string context, System.Exception error, LogType type = LogType.Error | LogType.Exception,
        [CallerMemberName] string member = "", [CallerFilePath] string source = "") {
        string details = context + ": " + error.GetType().Name + ": " + error.Message;
        if (details.Length > 8192) {
            details = details[..8192];
            }
        try {
            details = SecretAssignments.Replace(details, "${label}[redacted]");
            }
        catch (RegexMatchTimeoutException) {
            details = "[Details omitted because credential sanitization exceeded its time limit.]";
            }
        Write(details, type, member, source);
        }

    internal static Color ColorFor(LogType type) {
        if ((type & LogType.Critical) != 0) {
            return Color.Red;
            }
        if ((type & LogType.Exception) != 0) {
            return Color.FromArgb(250, 20, 150);
            }
        if ((type & LogType.Error) != 0) {
            return Color.MediumVioletRed;
            }
        if ((type & LogType.Warning) != 0) {
            return Color.IndianRed;
            }
        if ((type & LogType.Init) != 0) {
            return Color.Green;
            }
        if ((type & LogType.Parse) != 0) {
            return Color.GreenYellow;
            }
        if ((type & LogType.IO) != 0) {
            return Color.BlueViolet;
            }
        if ((type & LogType.FTable) != 0) {
            return Color.DarkGreen;
            }
        if ((type & LogType.GUI) != 0) {
            return Color.FromArgb(0, 112, 166);
            }
        return Color.Cyan;
        }
    }
