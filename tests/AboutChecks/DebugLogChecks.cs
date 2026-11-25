using Mash.GUI.MNXArchiver.Diagnostics;

internal static class DebugLogChecks {
    internal static void Run() {
        using DebugLogBuffer buffer = new DebugLogBuffer(3, 32);
        buffer.Write(LogType.Init, "Test->Open", "one");
        buffer.Write(LogType.Info, "Test->Open", "two");
        buffer.Write(LogType.Error | LogType.IO, "Test->Open", new string('x', 80));
        buffer.Write(LogType.Warning, "Test->Open", "four");
        DebugLogBatch first = buffer.ReadBatch(0, 2);
        Require(first.Records.Count == 2 && first.Records[0].Message == "two", "The ring must retain only its newest capacity records.");
        Require(first.Records[1].Message.Length == 32, "Individual messages must be bounded.");
        DebugLogBatch second = buffer.ReadBatch(first.Cursor, 2);
        Require(second.Records.Count == 1 && second.Records[0].Message == "four", "Batches must continue without duplication.");
        DebugLogBatch filtered = buffer.ReadBatch(0, 20, LogType.Error);
        Require(filtered.Records.Count == 1 && filtered.Records[0].Type.HasFlag(LogType.IO), "Severity filtering must preserve combined flag categories.");
        Require(buffer.ReadBatch(filtered.Cursor, 20, LogType.Error).Records.Count == 0, "Filtered cursors must advance past all examined records.");
        using DebugLogBuffer concurrent = new DebugLogBuffer(4000, 128);
        Parallel.For(0, 2000, number => concurrent.Write(LogType.Info, "Thread", number.ToString()));
        DebugLogBatch all = concurrent.ReadBatch(0, 4000);
        Require(all.Records.Count == 2000 && all.Records.Select(record => record.Sequence).Distinct().Count() == 2000,
            "Concurrent producers must preserve every unique sequence without duplicates.");
        Require(all.Records.Zip(all.Records.Skip(1)).All(pair => pair.First.Sequence < pair.Second.Sequence), "Concurrent batches must remain ordered.");
        concurrent.Dispose();
        concurrent.Write(LogType.Info, "Thread", "after disposal");
        Require(concurrent.ReadBatch(0, 10).Records.Count == 0, "Disposed log buffers must release and reject retained messages.");
        int[] flags = Enum.GetValues<LogType>().Where(value => value != LogType.All && value != LogType.None).Select(value => (int)value).ToArray();
        Require(flags.Distinct().Count() == 10 && flags.All(value => (value & (value - 1)) == 0), "Every category must use one explicit independent flag bit.");
        DebugLog.Write("Caller metadata probe.", LogType.GUI);
        DebugLogRecord caller = DebugLog.Buffer.ReadBatch(0, 4000).Records.Last();
        Require(caller.Caller == "DebugLogChecks->Run" && !caller.Caller.Contains('/'), "Caller metadata must identify source and member without retaining full paths.");
        DebugLog.Exception("Opening archive", new InvalidDataException("Header checksum failed; password=firstSecret token: secondSecret api_key='thirdSecret' Authorization: Bearer fourthSecret"));
        DebugLogRecord failure = DebugLog.Buffer.ReadBatch(caller.Sequence, 20).Records.Single();
        Require(failure.Message.Contains("Header checksum failed") && failure.Message.Contains("Opening archive"), "Diagnostic exceptions must retain useful operation and failure reasons.");
        foreach (string secret in new string[] { "firstSecret", "secondSecret", "thirdSecret", "fourthSecret" }) {
            Require(!failure.Message.Contains(secret), "Credential assignments must be redacted from exception messages.");
            }
        DebugLog.Exception("Long failure", new IOException(new string('x', 20000)));
        DebugLogRecord boundedFailure = DebugLog.Buffer.ReadBatch(failure.Sequence, 20).Records.Single();
        Require(boundedFailure.Message.Length <= 2048, "Exception details must respect the same bounded message limit.");
        Console.WriteLine("PASS: bounded debug ring, message limits, cursor batches, severity flags/filter, concurrent writes, disposal, compile-time caller metadata and bounded/redacted exception details.");
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new Exception(message);
            }
        }
    }
