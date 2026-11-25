using System.Collections.Concurrent;
using System.Diagnostics;
using Mash.GUI.MNXArchiver.About;

if (args.Length == 1 && args[0].StartsWith("--about-restart=", StringComparison.Ordinal)) {
    string[] restored = AboutRestart.RestoreArguments(args);
    return restored.Length == 1 && File.Exists(restored[0]) && AboutRestart.Restored?.PreviousPlugins.Count == 1 ? 42 : 1;
    }

string temporary = Path.Combine(Path.GetTempPath(), "mnx-about-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
string? previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temporary, "new-profile-data"));
try {
    string plugin = Path.Combine(temporary, "format.mnxp");
    await File.WriteAllTextAsync(plugin, "original");
    Dictionary<string, string> before = PluginSnapshot.Capture(temporary);
    Require(before.Count == 1, "Snapshot must include the actual .mnxp file.");
    await File.WriteAllTextAsync(Path.Combine(temporary, "ignored.txt"), "ignored");
    Require(!PluginSnapshot.Differs(before, PluginSnapshot.Capture(temporary)), "Non-plugin changes must be ignored.");
    ConcurrentQueue<Action> dispatched = new ConcurrentQueue<Action>();
    int notifications = 0;
    using (PluginDirectoryWatch watch = new PluginDirectoryWatch(temporary, action => dispatched.Enqueue(action), () => notifications++)) {
        await watch.StartAsync();
        string pending = Path.Combine(temporary, "incoming.tmp");
        await File.WriteAllTextAsync(pending, "updated");
        File.Move(pending, plugin, true);
        await Until(() => !dispatched.IsEmpty);
        Require(notifications == 0, "Watcher callbacks must go through the supplied UI dispatcher.");
        Drain(dispatched);
        Require(notifications > 0, "Atomic replacement must offer Reload.");
        Dictionary<string, string> after = PluginSnapshot.Capture(temporary);
        Require(PluginSnapshot.IsUpdated("format.mnxp", before, after), "Content updates must be highlighted even with the same filename.");
        Require(!PluginSnapshot.IsUpdated("format.mnxp", after, after), "Unchanged plugins must keep their normal color.");
        Require(!PluginSnapshot.IsUpdated("missing.mnxp", before, after), "Removed plugins must not be highlighted.");
        watch.Dispose();
        int previous = notifications;
        await File.WriteAllTextAsync(plugin, "after-close");
        await Task.Delay(450);
        Drain(dispatched);
        Require(notifications == previous, "Closing About must stop all watcher callbacks.");
        }
    using (PluginDirectoryWatch expiring = new PluginDirectoryWatch(temporary, action => dispatched.Enqueue(action), () => notifications++)) {
        await expiring.StartAsync(TimeSpan.FromMilliseconds(90));
        await Task.Delay(300);
        Drain(dispatched);
        int previous = notifications;
        await File.WriteAllTextAsync(Path.Combine(temporary, "new.mnxp"), "new");
        await Task.Delay(350);
        Drain(dispatched);
        Require(notifications == previous, "Expired watchers must not offer Reload.");
        }
    Dictionary<string, string> current = PluginSnapshot.Capture(temporary);
    Require(PluginSnapshot.IsUpdated("new.mnxp", before, current), "Newly installed plugins must be highlighted.");
    string archive = Path.Combine(temporary, "archive with spaces.mnx");
    await File.WriteAllTextAsync(archive, "archive");
    string descriptor = AboutRestart.WriteDescriptor(archive, before);
    if (!OperatingSystem.IsWindows()) {
        UnixFileMode permissions = File.GetUnixFileMode(descriptor);
        Require((permissions & (UnixFileMode.GroupRead | UnixFileMode.OtherRead | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0,
            "Restart state must be private to the current user.");
        }
    ProcessStartInfo start = AboutRestart.CreateStartInfo(descriptor);
    start.RedirectStandardError = true;
    start.RedirectStandardOutput = true;
    using (Process child = Process.Start(start) ?? throw new Exception("Restart child failed to launch.")) {
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Require(child.ExitCode == 42, "A real restarted process must restore the existing archive and plugin hashes: " + await child.StandardError.ReadToEndAsync());
        }
    Require(!File.Exists(descriptor), "Restart descriptors must be consumed exactly once.");
    string missing = AboutRestart.WriteDescriptor(Path.Combine(temporary, "missing.mnx"), before);
    AboutRestart missingState = AboutRestart.ReadDescriptor(missing);
    Require(missingState.ArchivePath is null, "Deleted archives must not be reopened.");
    string expired = AboutRestart.WriteDescriptor(archive, before);
    File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddHours(-1));
    bool rejected = false;
    try {
        AboutRestart.ReadDescriptor(expired);
        }
    catch (IOException) {
        rejected = true;
        }
    Require(rejected && !File.Exists(expired), "Expired restart state must be rejected and removed.");
    DebugLogChecks.Run();
    Console.WriteLine("PASS: real plugin hashing, atomic rename, UI dispatch, disposal, timeout, new/updated highlighting, private bounded restart state and real child-process archive restore.");
    return 0;
    }
finally {
    Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
    Directory.Delete(temporary, true);
    }

static void Require(bool condition, string message) {
    if (!condition) {
        throw new Exception(message);
        }
    }

static void Drain(ConcurrentQueue<Action> queue) {
    while (queue.TryDequeue(out Action? callback)) {
        callback();
        }
    }

static async Task Until(Func<bool> condition) {
    Stopwatch timeout = Stopwatch.StartNew();
    while (!condition()) {
        if (timeout.Elapsed > TimeSpan.FromSeconds(5)) {
            throw new Exception("Timed out waiting for the real filesystem watcher.");
            }
        await Task.Delay(25);
        }
    }
