using System.Diagnostics;

namespace Mash.GUI.MNXArchiver.About;

/// <summary>Small, private, single-use restart state. Archive passwords never enter this protocol.</summary>
internal sealed class AboutRestart {
    private const string Argument = "--about-restart=";
    private const int MaximumBytes = 128 * 1024;
    private static string? _pending;
    internal static AboutRestart? Restored { get; private set; }
    internal string? ArchivePath { get; }
    internal IReadOnlyDictionary<string, string> PreviousPlugins { get; }

    private AboutRestart(string? archivePath, Dictionary<string, string> previousPlugins) {
        ArchivePath = archivePath;
        PreviousPlugins = previousPlugins;
        }

    internal static string[] RestoreArguments(string[] args) {
        string[] descriptors = args.Where(value => value.StartsWith(Argument, StringComparison.Ordinal)).ToArray();
        if (descriptors.Length == 0) {
            return args;
            }
        if (descriptors.Length != 1 || args.Length != 1) {
            throw new ArgumentException("Invalid About restart arguments.");
            }
        string path = descriptors[0][Argument.Length..];
        Restored = ReadDescriptor(path);
        return Restored.ArchivePath is null ? [] : [Restored.ArchivePath];
        }

    internal static AboutRestart ReadDescriptor(string path) {
        string root = RestartDirectory();
        string absolute = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(absolute), root, StringComparison.Ordinal)
            || !Path.GetFileName(absolute).StartsWith("restart-", StringComparison.Ordinal)) {
            throw new IOException("The restart descriptor is outside the private restart directory.");
            }
        try {
            FileInfo file = new FileInfo(absolute);
            if (file.Length > MaximumBytes || (file.Attributes & FileAttributes.ReparsePoint) != 0
                || file.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-15)) {
                throw new IOException("The restart descriptor is invalid or expired.");
                }
            using FileStream stream = File.OpenRead(absolute);
            using BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadInt32() != 1) {
                throw new IOException("Unsupported restart descriptor version.");
                }
            string archive = reader.ReadString();
            int count = reader.ReadInt32();
            if (count < 0 || count > PluginSnapshot.MaximumFiles) {
                throw new IOException("Invalid plugin restart snapshot.");
                }
            Dictionary<string, string> previous = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < count; index++) {
                string name = reader.ReadString();
                string hash = reader.ReadString();
                if (name != Path.GetFileName(name) || name.Length > 255 || hash.Length != 64) {
                    throw new IOException("Invalid plugin restart identity.");
                    }
                previous.Add(name, hash);
                }
            if (stream.Position != stream.Length) {
                throw new IOException("Unexpected restart descriptor data.");
                }
            return new AboutRestart(File.Exists(archive) ? archive : null, previous);
            }
        finally {
            File.Delete(absolute);
            }
        }

    internal static string WriteDescriptor(string? archivePath, IReadOnlyDictionary<string, string> previous) {
        if (previous.Count > PluginSnapshot.MaximumFiles || archivePath?.Length > 32768) {
            throw new IOException("The restart state exceeds its size limit.");
            }
        string root = RestartDirectory();
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        string path = Path.Combine(root, "restart-" + Guid.NewGuid().ToString("N") + ".bin");
        FileStreamOptions options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
        try {
            using FileStream stream = new FileStream(path, options);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(1);
            writer.Write(archivePath is not null && File.Exists(archivePath) ? Path.GetFullPath(archivePath) : "");
            writer.Write(previous.Count);
            foreach (KeyValuePair<string, string> plugin in previous) {
                writer.Write(plugin.Key);
                writer.Write(plugin.Value);
                }
            writer.Flush();
            if (stream.Length > MaximumBytes) {
                throw new IOException("The restart state exceeds its size limit.");
                }
            return path;
            }
        catch {
            File.Delete(path);
            throw;
            }
        }

    private static string RestartDirectory() {
        string? root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify)
            : Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(root) || !Path.IsPathFullyQualified(root)) {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Path.IsPathFullyQualified(profile)) {
                throw new IOException("No absolute per-user restart directory is available.");
                }
            root = Path.Combine(profile, ".local", "share");
            }
        return Path.GetFullPath(Path.Combine(root, "mnxarchiver", "restart"));
        }

    internal static void Prepare(string? archivePath, IReadOnlyDictionary<string, string> previous) {
        if (_pending is not null) {
            File.Delete(_pending);
            }
        _pending = WriteDescriptor(archivePath, previous);
        }

    internal static ProcessStartInfo CreateStartInfo(string descriptor) {
        string executable = Environment.ProcessPath ?? throw new IOException("The application executable could not be found.");
        ProcessStartInfo start = new ProcessStartInfo(executable) { UseShellExecute = false };
        string[] original = Environment.GetCommandLineArgs();
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
            start.ArgumentList.Add(original[0]);
            }
        // Preserve an explicit Linux backend selection when restarting the shared launcher.
        for (int index = 1; index < original.Length; index++) {
            string argument = original[index];
            if (argument == "--") {
                break;
                }
            if (argument.StartsWith("--backend=", StringComparison.Ordinal)) {
                start.ArgumentList.Add(argument);
                }
            else if (argument == "--backend" && index + 1 < original.Length) {
                start.ArgumentList.Add("--backend=" + original[index + 1]);
                index++;
                }
            }
        start.ArgumentList.Add(Argument + descriptor);
        return start;
        }

    internal static void LaunchPending() {
        if (_pending is null) {
            return;
            }
        string descriptor = _pending;
        _pending = null;
        try {
            using Process? child = Process.Start(CreateStartInfo(descriptor));
            if (child is null) {
                throw new IOException("The restarted application did not start.");
                }
            }
        catch (Exception error) {
            File.Delete(descriptor);
            Console.Error.WriteLine("Could not restart MNX Archiver: " + error.Message);
            }
        }
    }
