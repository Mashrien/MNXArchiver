using System.Globalization;
using System.Text;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI.Settings;

/// <summary>An editable INI snapshot. Existing user colors always win over desktop changes.</summary>
internal sealed class MWinColors {
    internal const string FileName = "mnx.mwin-colors.conf";
    // Older palettes contain only the window colors. Menu defaults resolve lazily
    // so those files inherit their existing dark/light colors without a migration.
    private static readonly Dictionary<string, string> MenuDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["BarBackground"] = "WindowBackground",
        ["Background"] = "WindowBackground",
        ["Text"] = "WindowText",
        ["DisabledText"] = "DisabledText",
        ["HoverBackground"] = "SelectionBackground",
        ["HoverText"] = "SelectionText",
        ["PressedBackground"] = "SelectionBackground",
        ["PressedText"] = "SelectionText",
        ["CheckedBackground"] = "SelectionBackground",
        ["CheckedText"] = "SelectionText",
        ["ImageMargin"] = "WindowBackground",
        ["Border"] = "Border",
        ["Separator"] = "Border",
        };
    private readonly Dictionary<string, uint> _menuColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint> _colors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase) {
        ["WindowBackground"] = 0xFFF0F0F0,
        ["WindowText"] = 0xFF202020,
        ["ViewBackground"] = 0xFFFFFFFF,
        ["ViewText"] = 0xFF202020,
        ["ButtonBackground"] = 0xFFF0F0F0,
        ["ButtonText"] = 0xFF202020,
        ["SelectionBackground"] = 0xFF0078D7,
        ["SelectionText"] = 0xFFFFFFFF,
        ["Accent"] = 0xFF0078D7,
        ["Border"] = 0xFF808080,
        ["DisabledText"] = 0xFF808080,
        ["TitleBackground"] = 0xFFF0F0F0,
        ["TitleText"] = 0xFF202020,
        };
    internal uint this[string name] {
        get {
            if (_colors.TryGetValue(name, out uint color)) {
                return color;
                }
            if (_menuColors.TryGetValue(name, out color)) {
                return color;
                }
            return _colors[MenuDefaults[name.Substring(4)]];
            }
        set {
            if (name.StartsWith("Menu", StringComparison.OrdinalIgnoreCase) && MenuDefaults.ContainsKey(name.Substring(4))) {
                _menuColors[name] = value | 0xFF000000;
                }
            else {
                _colors[name] = value | 0xFF000000;
                }
            }
        }

    internal static MWinColors LoadOrCreate(string directory, Func<MWinColors> desktopColors) {
        string path = Path.Combine(directory, FileName);
        if (DiskFile.Exists(path)) {
            return Read(path);
            }
        MWinColors colors = desktopColors();
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try {
            Directory.CreateDirectory(directory);
            DiskFile.WriteAllText(pending, colors.ToIni(), new UTF8Encoding(false));
            try {
                // No overwrite: another launch may have created or edited it meanwhile.
                DiskFile.Move(pending, path, false);
                }
            catch (IOException) when (DiskFile.Exists(path)) {
                return Read(path);
                }
            }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) {
            System.Diagnostics.Trace.WriteLine("Could not save MWin colors: " + error.Message);
            }
        finally {
            if (DiskFile.Exists(pending)) {
                DiskFile.Delete(pending);
                }
            }
        return colors;
        }

    internal static MWinColors Read(string path) {
        MWinColors colors = new MWinColors();
        try {
            if (new FileInfo(path).Length > 65536) {
                return colors;
                }
            INI_Config config = INI_Config.Load(path, 65536);
            foreach (string key in colors._colors.Keys.ToArray()) {
                string value = config["MNX.Display.MWin"].GetString(key).Split(';')[0].Trim();
                if (TryColor(value, out uint color)) {
                    colors[key] = color;
                    }
                }
            foreach (string key in MenuDefaults.Keys) {
                string value = config["MNX.Display.MWin.Menus"].GetString(key).Split(';')[0].Trim();
                if (TryColor(value, out uint color)) {
                    colors._menuColors["Menu" + key] = color;
                    }
                }
            }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) {
            System.Diagnostics.Trace.WriteLine("Could not read MWin colors: " + error.Message);
            }
        return colors;
        }

    internal static bool TryColor(string value, out uint color) {
        color = 0;
        if (value.Length == 7 && value[0] == '#' && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color)) {
            color |= 0xFF000000;
            return true;
            }
        string[] channels = value.Split(',');
        if (channels.Length == 3 && byte.TryParse(channels[0].Trim(), out byte red) && byte.TryParse(channels[1].Trim(), out byte green) && byte.TryParse(channels[2].Trim(), out byte blue)) {
            color = 0xFF000000 | (uint)red << 16 | (uint)green << 8 | blue;
            return true;
            }
        return false;
        }

    internal void ImportKde(string path) {
        if (!DiskFile.Exists(path) || new FileInfo(path).Length > 1024 * 1024) {
            return;
            }
        INI_Config config = INI_Config.Load(path);
        Dictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["Colors:Window/BackgroundNormal"] = "WindowBackground",
            ["Colors:Window/ForegroundNormal"] = "WindowText",
            ["Colors:Window/ForegroundInactive"] = "DisabledText",
            ["Colors:Window/DecorationFocus"] = "Accent",
            ["Colors:Window/DecorationHover"] = "Border",
            ["Colors:View/BackgroundNormal"] = "ViewBackground",
            ["Colors:View/ForegroundNormal"] = "ViewText",
            ["Colors:Button/BackgroundNormal"] = "ButtonBackground",
            ["Colors:Button/ForegroundNormal"] = "ButtonText",
            ["Colors:Selection/BackgroundNormal"] = "SelectionBackground",
            ["Colors:Selection/ForegroundNormal"] = "SelectionText",
            ["WM/activeBackground"] = "TitleBackground",
            ["WM/activeForeground"] = "TitleText",
            };
        foreach (KeyValuePair<string, string> mapping in mappings) {
            string[] source = mapping.Key.Split('/');
            if (TryColor(config[source[0]].GetString(source[1]), out uint color)) {
                this[mapping.Value] = color;
                }
            }
        }

    private string ToIni() {
        INI_Config config = new INI_Config();
        foreach (KeyValuePair<string, uint> color in _colors) {
            config["MNX.Display.MWin"].Set(color.Key, "#" + (color.Value & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture));
            }
        foreach (string key in MenuDefaults.Keys) {
            config["MNX.Display.MWin.Menus"].Set(key, "#" + (this["Menu" + key] & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture));
            }
        return config.ToText("MNX MWin colors: #RRGGBB or red,green,blue (0..255).\nEdit colors and restart MNX. Delete this file to recapture desktop colors.\nOptions > General > Interface can disable this palette.");
        }
    }
