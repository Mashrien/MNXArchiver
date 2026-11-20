using System.Globalization;
using System.Text;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver.Settings;

/// <summary>Editable MWin drawing settings and the desktop colors they were based on.</summary>
internal sealed class MWinColors {
    internal const string FileName = "gui.mwin.drawing.conf";
    internal const string WindowSection = "mnx.display.mwin.window";
    internal const string ButtonsSection = "mnx.display.mwin.buttons";
    internal const string TextSection = "mnx.display.mwin.text";
    internal const string FilesSection = "mnx.display.mwin.files";
    internal const string MenusSection = "mnx.display.mwin.menus";
    private const string LegacyFileName = "mnx.mwin-colors.conf";
    private const string LegacySection = "MNX.Display.MWin";
    private const string DefaultFont = "Segoe UI";
    private const int DefaultFontSize = 9;

    private static readonly Dictionary<string, (string Section, string Key)> ColorLocations = new(StringComparer.OrdinalIgnoreCase) {
        ["WindowBackground"] = (WindowSection, "Color.wmBack"),
        ["WindowText"] = (WindowSection, "Color.wmfColor"),
        ["ViewBackground"] = (FilesSection, "BackColor"),
        ["ViewText"] = (FilesSection, "ForeColor"),
        ["ButtonBackground"] = (ButtonsSection, "BackColor"),
        ["ButtonText"] = (ButtonsSection, "ForeColor"),
        ["Border"] = (WindowSection, "Color.wmBorder"),
        ["TitleBackground"] = (WindowSection, "Color.wtBack"),
        ["TitleText"] = (WindowSection, "Color.tfColor"),
        };

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
    private INI_Config _values = new INI_Config();
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
        string legacyPath = Path.Combine(directory, LegacyFileName);
        MWinColors colors = DiskFile.Exists(legacyPath) ? Read(legacyPath) : desktopColors();
        colors._values = colors.CreateDrawingConfig();
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try {
            Directory.CreateDirectory(directory);
            string text = colors._values.ToText("Edit and restart MNX. Colors are #RRGGBB.\nTwo caption state colors give a radial gradient, center first.");
            DiskFile.WriteAllText(pending, text, new UTF8Encoding(false));
            try {
                // No overwrite: another launch may have created or edited it meanwhile.
                DiskFile.Move(pending, path, false);
                }
            catch (IOException) when (DiskFile.Exists(path)) {
                return Read(path);
                }
            }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) {
            System.Diagnostics.Trace.WriteLine("Could not save MWin drawing settings: " + error.Message);
            }
        finally {
            try {
                DiskFile.Delete(pending);
                }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) {
                System.Diagnostics.Trace.WriteLine("Could not remove temporary MWin drawing settings: " + error.Message);
                }
            }
        return colors;
        }

    internal static MWinColors Read(string path) {
        MWinColors colors = new MWinColors();
        try {
            colors._values = INI_Config.Load(path, 65536);
            foreach (string key in colors._colors.Keys.ToArray()) {
                (string section, string drawingKey) = ColorLocations.GetValueOrDefault(key, (WindowSection, key));
                string value = colors.GetString(section, drawingKey, colors.GetString(LegacySection, key, ""));
                if (TryColor(value, out uint color)) {
                    colors[key] = color;
                    }
                }
            foreach (string key in MenuDefaults.Keys) {
                string value = colors.GetString(MenusSection, key, "");
                if (TryColor(value, out uint color)) {
                    colors["Menu" + key] = color;
                    }
                }
            }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) {
            System.Diagnostics.Trace.WriteLine("Could not read MWin drawing settings: " + error.Message);
            }
        return colors;
        }

    internal string GetString(string section, string key, string fallback) {
        string value = _values[section].GetString(key);
        int comment = value.IndexOf(';');
        if (comment >= 0) {
            value = value[..comment].TrimEnd();
            }
        return value.Length == 0 ? fallback : value;
        }

    internal int GetInt(string section, string key, int fallback, int min, int max) {
        if (int.TryParse(GetString(section, key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max) {
            return value;
            }
        return fallback;
        }

    internal float GetFloat(string section, string key, float fallback, float min, float max) {
        if (float.TryParse(GetString(section, key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= min && value <= max) {
            return value;
            }
        return fallback;
        }

    internal uint GetColor(string section, string key, uint fallback) {
        return TryColor(GetString(section, key, ""), out uint color) ? color : fallback;
        }

    internal (uint Inner, uint? Outer) GetColorPair(string section, string key, uint fallback, uint? outerFallback = null) {
        string value = GetString(section, key, "");
        if (TryColor(value, out uint inner)) {
            return (inner, null);
            }
        int separator = value.IndexOf(',');
        if (separator >= 0 && TryHexColor(value.AsSpan(0, separator).Trim(), out inner) &&
            TryHexColor(value.AsSpan(separator + 1).Trim(), out uint outer)) {
            return (inner, outer);
            }
        return (fallback, outerFallback);
        }

    internal static bool TryColor(string value, out uint color) {
        if (TryHexColor(value, out color)) {
            return true;
            }
        string[] channels = value.Split(',');
        if (channels.Length == 3 && byte.TryParse(channels[0].Trim(), out byte red) && byte.TryParse(channels[1].Trim(), out byte green) && byte.TryParse(channels[2].Trim(), out byte blue)) {
            color = 0xFF000000 | (uint)red << 16 | (uint)green << 8 | blue;
            return true;
            }
        return false;
        }

    private static bool TryHexColor(ReadOnlySpan<char> value, out uint color) {
        color = 0;
        if (value.Length == 7 && value[0] == '#' && uint.TryParse(value[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out color)) {
            color |= 0xFF000000;
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

    private INI_Config CreateDrawingConfig() {
        INI_Config config = new INI_Config();
        INI_Config.Section window = config[WindowSection];
        window.Set("Color.wmBorder", Hex(this["Border"]));
        window.Set("Color.wmBack", Hex(this["WindowBackground"]));
        window.Set("Color.wmDivs", "");
        window.Set("Color.wtFont", DefaultFont);
        window.Set("Color.tfSize", DefaultFontSize);
        window.Set("Color.tfColor", Hex(this["TitleText"]));
        window.Set("Color.wmFont", DefaultFont);
        window.Set("Color.wmfSize", DefaultFontSize);
        window.Set("Color.wmfColor", Hex(this["WindowText"]));
        window.Set("controlBox.MinMax.Colors.Background", Hex(this["ButtonBackground"]));
        window.Set("controlBox.MinMax.Colors.Foreground", Hex(this["TitleText"]));
        window.Set("controlBox.MinMax.Colors.Highlight", Hex(this["Accent"]));
        window.Set("controlBox.MinMax.Colors.Pressed", Hex(this["SelectionBackground"]));
        window.Set("controlBox.Exit.Colors.Background", Hex(this["ButtonBackground"]));
        window.Set("controlBox.Exit.Colors.Foreground", Hex(this["TitleText"]));
        window.Set("controlBox.Exit.Colors.Highlight", "#DB7093");
        window.Set("controlBox.Exit.Colors.Pressed", "#8B0000,#000000");
        window.Set("dividerMargin", 6);
        window.Set("wmTitleHeight", 26);
        window.Set("wmStatusHeight", 24);
        window.Set("Color.wtBack", Hex(this["TitleBackground"]));
        window.Set("SelectionBackground", Hex(this["SelectionBackground"]));
        window.Set("SelectionText", Hex(this["SelectionText"]));
        window.Set("Accent", Hex(this["Accent"]));
        window.Set("DisabledText", Hex(this["DisabledText"]));

        INI_Config.Section buttons = config[ButtonsSection];
        buttons.Set("Font", DefaultFont);
        buttons.Set("Size", DefaultFontSize);
        buttons.Set("Border.Color", Hex(this["Border"]));
        buttons.Set("Border.Thickness", 2);
        buttons.Set("Border.RoundedCorners", "All");
        buttons.Set("Border.CornerRadius", 5);
        buttons.Set("BackColor", Hex(this["ButtonBackground"]));
        buttons.Set("ForeColor", Hex(this["ButtonText"]));

        INI_Config.Section text = config[TextSection];
        text.Set("Font", DefaultFont);
        text.Set("Size", DefaultFontSize);
        text.Set("Color", Hex(this["MenuText"]));

        INI_Config.Section files = config[FilesSection];
        files.Set("BackColor", Hex(this["ViewBackground"]));
        files.Set("Font", DefaultFont);
        files.Set("Size", DefaultFontSize);
        files.Set("ForeColor", Hex(this["ViewText"]));
        files.Set("ColumnHeaders.BackColor", Hex(this["ButtonBackground"]));
        files.Set("ColumnHeaders.HighlightColor", Hex(this["Accent"]));
        files.Set("ColumnHeaders.Font", DefaultFont);
        files.Set("ColumnHeaders.Size", DefaultFontSize);
        files.Set("ColumnHeaders.Color", Hex(this["ButtonText"]));

        INI_Config.Section menus = config[MenusSection];
        foreach (string key in MenuDefaults.Keys) {
            menus.Set(key, Hex(this["Menu" + key]));
            }
        return config;
        }

    private static string Hex(uint color) {
        return "#" + (color & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);
        }
    }
