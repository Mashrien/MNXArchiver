using System.Globalization;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI.Settings;

/// <summary>Small, editable INI file. Unknown keys survive a save.</summary>
internal sealed class AppSettings {
    internal const int MinimumHoverDelay = 500;
    internal const int DefaultDecodedMiB = 512;
    internal const int DefaultTotalPreviewMiB = 1024;
    internal static readonly int[] Resolutions = [64,128,256,384,512,640,720,800,896,1024,1280,1366,1440,1536,1664,1792,1920,2048,2304,2560,2816,3072];
    internal static readonly double[] Percentages = [10,12.5,15,20,25,33,50,66,75,80,85,87.5,90];
    private INI_Config _config = new INI_Config();
    internal string DirectoryPath { get; }
    internal string ConfigPath => Path.Combine(DirectoryPath, "mnxarchiver.conf");
    internal string KeyPath => Path.Combine(DirectoryPath, ".mnxdefaultkey");
    internal AppSettings(string? directory = null) {
        string basePath = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        DirectoryPath = directory ?? Path.Combine(basePath, "mnxarchiver");
        }

    internal string Get(string section, string key, string fallback) {
        return _config[SectionName(section, key)].GetString(key, fallback);
        }

    internal bool Flag(string section, string key, bool fallback = false) {
        return _config[SectionName(section, key)].GetBool(key, fallback);
        }

    internal void Set(string section, string key, object value) {
        _config[SectionName(section, key)].Set(key, value);
        }

    // Keep the UI-facing accessors stable while the editable INI uses categories.
    private static string SectionName(string section, string key) {
        return section switch {
            "General" => "MNX.Storage",
            "Interface" => key == "UseDesktopColors" ? "MNX.Display.MWin" : "MNX.Display",
            "Defaults" => key == "Encrypt" ? "MNX.Encryption" : "MNX.Compression",
            "Previews" => "MNX.Previews",
            "Text" => "MNX.Previews.Text",
            "Images" => "MNX.Previews.Images",
            "Presentation" => "MNX.Previews.Display",
            _ => section,
            };
        }

    internal int Number(string section, string key, int fallback, int min, int max) {
        if (int.TryParse(Get(section, key, ""), CultureInfo.InvariantCulture, out int value)) {
            return Math.Clamp(value, min, max);
            }
        return fallback;
        }

    internal string Choice(string section, string key, string fallback, params string[] choices) {
        string value = Get(section, key, fallback);
        return choices.Contains(value, StringComparer.Ordinal) ? value : fallback;
        }

    internal int PreviewBufferBytes => Number("Previews", "BufferMiB", 32, 16, 128) * 1024 * 1024;
    internal long DecodedPreviewBytes => Number("Previews", "DecodedMiB", DefaultDecodedMiB, 64, 1024) * 1024L * 1024;
    internal long TotalPreviewBytes => Number("Previews", "TotalMiB", DefaultTotalPreviewMiB, 256, 4096) * 1024L * 1024;
    internal bool Previews => Flag("Previews", "Enabled");
    internal string Trigger => Choice("Previews", "Trigger", "Double-Click", "Double-Click", "Single-Click", "Mouse-Over With Delay");
    internal int OpacityPercent => Number("Interface", "OpacityPercent", 100, 10, 100);
    internal int HoverDelay => Math.Clamp((Number("Previews", "DelayMs", 750, MinimumHoverDelay, 2000) / 250) * 250, MinimumHoverDelay, 2000);
    internal string TempPath => Flag("General", "OverrideTemp") ? Get("General", "TempPath", Path.GetTempPath()) : Path.GetTempPath();
    internal bool IndependentWindows => Flag("Presentation", "Customize") && Get("Presentation", "Mode", "Window") == "Window" && Get("Presentation", "WindowType", "Dialog") == "Window" && Get("Presentation", "RelativeTo", "Program Window") == "Current Screen" && Get("Presentation", "Origin", "Center") != "Right of Main Window";

    internal AppSettings Copy() {
        AppSettings copy = new AppSettings(DirectoryPath);
        copy._config = _config.Copy();
        return copy;
        }

    internal static AppSettings Load(string? directory = null) {
        AppSettings result = new AppSettings(directory);
        result._config = INI_Config.Load(result.ConfigPath);
        // Read older installations too, and migrate known keys on the next save.
        foreach (string section in result._config.SectionNames.ToArray()) {
            foreach (KeyValuePair<string, string> value in result._config[section].Values.ToArray()) {
                string destination = SectionName(section, value.Key);
                if (destination != section) {
                    if (!result._config[destination].Contains(value.Key)) {
                        result._config[destination].Set(value.Key, value.Value);
                        }
                    result._config[section].Remove(value.Key);
                    }
                }
            }
        return result;
        }

    internal void Save() {
        EnsurePrivateDirectory();
        _config.Save(ConfigPath, "MNX Archive Manager settings. Passwords are never stored here.");
        }

    internal void EnsurePrivateDirectory() {
        if (OperatingSystem.IsWindows()) {
            Directory.CreateDirectory(DirectoryPath);
            }
        else {
            Directory.CreateDirectory(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

    internal static void WritePrivate(string path, byte[] bytes) {
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try {
            FileStreamOptions options = new FileStreamOptions {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
                };
            if (!OperatingSystem.IsWindows()) {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }
            using (FileStream output = new FileStream(pending, options)) {
                output.Write(bytes);
                output.Flush(true);
                }
            DiskFile.Move(pending, path, true);
            }
        finally {
            DiskFile.Delete(pending);
            }
        }

    internal static string Extensions(string csv) {
        SortedSet<string> types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in csv.Split(',')) {
            string extension = item.Trim().TrimStart('.').ToLowerInvariant();
            if (extension.Length == 0) {
                continue;
                }
            if (extension.Length > 32 || extension.Any(character => !char.IsLetterOrDigit(character) && character != '_' && character != '-' && character != '+')) {
                throw new ArgumentException("Use comma-separated extensions, such as txt, ini, cs. Paths, wildcards and punctuation are not extensions.");
                }
            types.Add(extension);
            }
        return string.Join(',', types);
        }

    internal static void ValidateTemp(string path) {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) {
            throw new DirectoryNotFoundException("Select an existing, absolute temporary folder.");
            }
        string probe = Path.Combine(path, ".mnx-access-" + Guid.NewGuid().ToString("N"));
        using FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        stream.WriteByte(42);
        stream.Position = 0;
        if (stream.ReadByte() != 42) {
            throw new IOException("Could not read back a test file in the temporary folder.");
            }
        }
    }
