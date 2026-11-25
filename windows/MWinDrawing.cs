using Mash.GUI.MNXArchiver.Settings;
using MashWin.Settings;

namespace Mash.GUI.MNXArchiver;

internal static partial class MWinDrawing {
    private static UserColors.Theme? _theme;

    // The configuration and its desktop import are read once per launch.
    internal static UserColors.Theme Load(AppSettings settings) {
        _theme ??= settings.Flag("Interface", "UseDesktopColors", true)
            ? ReadTheme(MWinColors.LoadOrCreate(settings.DirectoryPath, ReadDesktopColors))
            : default(UserColors.Theme);
        return _theme.Value;
        }

    internal static UserColors.Theme Current => _theme.GetValueOrDefault();

    private static UserColors.Theme ReadTheme(MWinColors colors) {
        const string window = MWinColors.WindowSection;
        const string buttons = MWinColors.ButtonsSection;
        const string text = MWinColors.TextSection;
        const string files = MWinColors.FilesSection;
        Color textForeground = Drawing(colors.GetColor(text, "Color", colors["MenuText"]));
        return new UserColors.Theme {
            ConfigureSystemColors = true,
            Window = new UserColors.WindowTheme {
                Border = Drawing(colors["Border"]),
                Background = Drawing(colors["WindowBackground"]),
                Divider = Drawing(colors.GetColor(window, "Color.wmDivs", colors["Border"])),
                Foreground = Drawing(colors["WindowText"]),
                TitleBackground = Drawing(colors["TitleBackground"]),
                TitleForeground = Drawing(colors["TitleText"]),
                Font = ReadFont(colors, window, "Color.wmFont", "Color.wmfSize"),
                TitleFont = new UserColors.FontSpec(colors.GetString(window, "Color.wtFont", "Segoe UI"), colors.CaptionFontPoints),
                DividerMargin = colors.GetInt(window, "dividerMargin", 6, 0, 48),
                TitleHeight = colors.CaptionHeight,
                StatusHeight = colors.GetInt(window, "wmStatusHeight", 24, 18, 256),
                MinMax = ReadCaption(colors, "controlBox.MinMax.Colors.", colors["Accent"], colors["SelectionBackground"]),
                Exit = ReadCaption(colors, "controlBox.Exit.Colors.", 0xFFDB7093, 0xFF8B0000, 0xFF000000),
                },
            Buttons = new UserColors.ToolbarTheme {
                Font = ReadFont(colors, buttons, "Font", "Size"),
                Background = Drawing(colors["ButtonBackground"]),
                Foreground = Drawing(colors["ButtonText"]),
                Border = Drawing(colors.GetColor(buttons, "Border.Color", colors["Border"])),
                BorderThickness = colors.GetInt(buttons, "Border.Thickness", 2, 0, 8),
                CornerRadius = colors.GetInt(buttons, "Border.CornerRadius", 5, 0, 64),
                RoundedCorners = ReadRoundedCorners(colors.GetString(buttons, "Border.RoundedCorners", "All")),
                },
            Text = new UserColors.TextTheme {
                Font = ReadFont(colors, text, "Font", "Size"),
                Foreground = textForeground,
                },
            Files = new UserColors.BrowserTheme {
                Font = ReadFont(colors, files, "Font", "Size"),
                HeaderFont = ReadFont(colors, files, "ColumnHeaders.Font", "ColumnHeaders.Size"),
                Background = Drawing(colors["ViewBackground"]),
                Foreground = Drawing(colors["ViewText"]),
                HeaderBackground = Drawing(colors.GetColor(files, "ColumnHeaders.BackColor", colors["ButtonBackground"])),
                HeaderHighlight = Drawing(colors.GetColor(files, "ColumnHeaders.HighlightColor", colors["Accent"])),
                HeaderForeground = Drawing(colors.GetColor(files, "ColumnHeaders.Color", colors["ButtonText"])),
                },
            Menus = new UserColors.MenuTheme {
                BarBackground = Drawing(colors["MenuBarBackground"]),
                Background = Drawing(colors["MenuBackground"]),
                Foreground = textForeground,
                DisabledForeground = Drawing(colors["MenuDisabledText"]),
                Highlight = Drawing(colors["MenuHoverBackground"]),
                HighlightForeground = Drawing(colors["MenuHoverText"]),
                Pressed = Drawing(colors["MenuPressedBackground"]),
                PressedForeground = Drawing(colors["MenuPressedText"]),
                Checked = Drawing(colors["MenuCheckedBackground"]),
                CheckedForeground = Drawing(colors["MenuCheckedText"]),
                ImageMargin = Drawing(colors["MenuImageMargin"]),
                Border = Drawing(colors["MenuBorder"]),
                Separator = Drawing(colors["MenuSeparator"]),
                },
            Accent = Drawing(colors["Accent"]),
            SelectionBackground = Drawing(colors["SelectionBackground"]),
            SelectionForeground = Drawing(colors["SelectionText"]),
            DisabledForeground = Drawing(colors["DisabledText"]),
            };
        }

    private static UserColors.CaptionTheme ReadCaption(MWinColors colors, string prefix, uint highlight, uint pressed, uint? pressedOuter = null) {
        const string window = MWinColors.WindowSection;
        (uint highlightColor, uint? highlightEdge) = colors.GetColorPair(window, prefix + "Highlight", highlight);
        (uint pressedColor, uint? pressedEdge) = colors.GetColorPair(window, prefix + "Pressed", pressed, pressedOuter);
        return new UserColors.CaptionTheme {
            Background = Drawing(colors.GetColor(window, prefix + "Background", colors["ButtonBackground"])),
            Foreground = Drawing(colors.GetColor(window, prefix + "Foreground", colors["TitleText"])),
            Highlight = Drawing(highlightColor),
            Pressed = Drawing(pressedColor),
            HighlightOuter = Drawing(highlightEdge),
            PressedOuter = Drawing(pressedEdge),
            };
        }

    private static UserColors.FontSpec ReadFont(MWinColors colors, string section, string fontKey, string sizeKey) {
        string family = colors.GetString(section, fontKey, "");
        float size = colors.GetFloat(section, sizeKey, 0, 6, 48);
        return new UserColors.FontSpec(family, size);
        }

    private static MashWin.ExtendedButtonSettings.RoundedCorners ReadRoundedCorners(string value) {
        const MashWin.ExtendedButtonSettings.RoundedCorners all = MashWin.ExtendedButtonSettings.RoundedCorners.All;
        if (bool.TryParse(value, out bool rounded)) {
            return rounded ? all : MashWin.ExtendedButtonSettings.RoundedCorners.None;
            }
        if (Enum.TryParse(value, true, out MashWin.ExtendedButtonSettings.RoundedCorners corners) && (corners & ~all) == 0) {
            return corners;
            }
        return all;
        }

    private static Color Drawing(uint color) {
        return Color.FromArgb(unchecked((int)color));
        }

    private static Color Drawing(uint? color) {
        return color is uint argb ? Drawing(argb) : Color.Empty;
        }

    private static MWinColors ReadDesktopColors() {
        MWinColors colors = new MWinColors();
        if (OperatingSystem.IsWindows()) {
            colors["WindowBackground"] = Argb(SystemColors.Control);
            colors["WindowText"] = Argb(SystemColors.ControlText);
            colors["TitleBackground"] = Argb(SystemColors.ActiveCaption);
            colors["TitleText"] = Argb(SystemColors.ActiveCaptionText);
            colors["ViewBackground"] = Argb(SystemColors.Window);
            colors["ViewText"] = Argb(SystemColors.WindowText);
            colors["ButtonBackground"] = Argb(SystemColors.ButtonFace);
            colors["ButtonText"] = Argb(SystemColors.ControlText);
            colors["Border"] = Argb(SystemColors.ActiveBorder);
            colors["Accent"] = colors["SelectionBackground"] = Argb(SystemColors.Highlight);
            colors["SelectionText"] = Argb(SystemColors.HighlightText);
            colors["DisabledText"] = Argb(SystemColors.GrayText);
            }
        else {
            ReadPortableColors(colors);
            }
        return colors;
        }

    static partial void ReadPortableColors(MWinColors colors);

    private static uint Argb(Color color) {
        return unchecked((uint)color.ToArgb());
        }
    }
