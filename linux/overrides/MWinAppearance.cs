using System.Runtime.InteropServices;
using Mash.GUI.MNXArchiver.Settings;
using SkiaSharp;

namespace Mash.GUI.MNXArchiver;

/// <summary>Linux-only desktop import and MWin palette application; no theme package dependency.</summary>
internal static class MWinAppearance {
    private static MWinColors? _colors;
    private static bool _initialized;

    internal static void Configure(AppSettings settings) {
        // Palette edits and the off switch apply on restart; opacity may change live.
        if (_initialized) {
            return;
            }
        _initialized = true;
        _colors = settings.Flag("Interface", "UseDesktopColors", true)
            ? MWinColors.LoadOrCreate(settings.DirectoryPath, ReadDesktop)
            : null;
        Majorsilence.Forms.Drawing.SystemColorPalette.SetOverrides(SystemPalette());
        Theme.SetBuiltInTheme(_colors is not null && Brightness(_colors["WindowBackground"]) < 128 ? BuiltInTheme.Dark : BuiltInTheme.Light);
        if (_colors is null) {
            return;
            }
        Theme.BeginUpdate();
        try {
            Theme.BackgroundColor = Skia("WindowBackground");
            Theme.ForegroundColor = Skia("WindowText");
            Theme.ForegroundDisabledColor = Skia("DisabledText");
            Theme.ForegroundColorOnAccent = Skia("SelectionText");
            Theme.AccentColor = Skia("Accent");
            Theme.AccentColor2 = Skia("SelectionBackground");
            Theme.TextSelectionBackgroundColor = Skia("SelectionBackground");
            Theme.ControlLowColor = Skia("ViewBackground");
            Theme.ControlMidColor = Skia("ButtonBackground");
            Theme.BorderLowColor = Skia("Border");
            Theme.BorderMidColor = Skia("Border");
            }
        finally {
            Theme.EndUpdate();
            }
        }

    private static int Brightness(uint color) {
        return (int)(((color >> 16 & 255) * 299 + (color >> 8 & 255) * 587 + (color & 255) * 114) / 1000);
        }

    private static SKColor Skia(string name) {
        return new SKColor(_colors![name]);
        }

    private static Color Drawing(string name) {
        return Color.FromArgb(unchecked((int)_colors![name]));
        }

    private static Dictionary<string, Color> SystemPalette() {
        Dictionary<string, Color> palette = new Dictionary<string, Color>(StringComparer.Ordinal);
        if (_colors is null) {
            return palette;
            }
        foreach (string name in new string[] { "Control", "Menu", "MenuBar", "Info", "AppWorkspace" }) {
            palette[name] = Drawing("WindowBackground");
            }
        foreach (string name in new string[] { "ControlText", "MenuText", "InfoText", "CaptionText" }) {
            palette[name] = Drawing("WindowText");
            }
        palette["Window"] = Drawing("ViewBackground");
        palette["WindowText"] = Drawing("ViewText");
        palette["ButtonFace"] = Drawing("ButtonBackground");
        palette["GrayText"] = Drawing("DisabledText");
        palette["Highlight"] = Drawing("SelectionBackground");
        palette["HighlightText"] = Drawing("SelectionText");
        palette["HotTrack"] = Drawing("Accent");
        palette["ActiveCaption"] = Drawing("TitleBackground");
        palette["ActiveCaptionText"] = Drawing("TitleText");
        foreach (string name in new string[] { "ControlDark", "ControlDarkDark", "WindowFrame", "ActiveBorder", "InactiveBorder", "ButtonShadow" }) {
            palette[name] = Drawing("Border");
            }
        foreach (string name in new string[] { "ControlLight", "ControlLightLight", "ButtonHighlight", "ScrollBar", "InactiveCaption" }) {
            palette[name] = Drawing("ButtonBackground");
            }
        return palette;
        }

    internal static void Apply(MashWin.MWindow window) {
        if (_colors is null) {
            return;
            }
        window.BackColor = Drawing("WindowBackground");
        window.ForeColor = Drawing("WindowText");
        window.Settings.Title.TextColor = Drawing("TitleText");
        window.Settings.Title.BackgroundColor = Drawing("TitleBackground");
        window.Settings.Frame.BorderColor = Drawing("Border");
        window.Settings.MenuSeparator.Color = Drawing("Border");
        MashWin.ToolStripSettings strips = window.Settings.ToolStrips;
        strips.BackgroundColor = Drawing("MenuBarBackground");
        strips.MenuBackgroundColor = Drawing("MenuBackground");
        strips.TextColor = Drawing("MenuText");
        strips.DisabledTextColor = Drawing("MenuDisabledText");
        strips.BorderColor = Drawing("MenuBorder");
        strips.HoverColor = Drawing("MenuHoverBackground");
        strips.PressedColor = Drawing("MenuPressedBackground");
        strips.PressedTextColor = Drawing("MenuPressedText");
        strips.CheckedColor = Drawing("MenuCheckedBackground");
        strips.HoverTextColor = Drawing("MenuHoverText");
        strips.CheckedTextColor = Drawing("MenuCheckedText");
        strips.ImageMarginColor = Drawing("MenuImageMargin");
        strips.SeparatorColor = Drawing("MenuSeparator");
        ApplyControls(window.Controls.Owner);
        window.Invalidate(true);
        }

    private static void ApplyControls(Control parent) {
        foreach (Control control in parent.Controls) {
            // System colors resolve through the palette; preserve explicit warning
            // colors, image surfaces and transparency keys chosen by the designer.
            if (control is MashWin.RoundButton button) {
                button.ForeColor = Drawing("TitleText");
                button.ExtendedSettings.BackgroundColor = Drawing("ButtonBackground");
                button.ExtendedSettings.MouseOverBackColor = Drawing("Accent");
                button.ExtendedSettings.MouseDownBackColor = Drawing("SelectionBackground");
                }
            else if (control is ButtonBase) {
                control.ForeColor = Drawing("ButtonText");
                }
            if (control.BackColor == Color.WhiteSmoke || control.BackColor == Color.White) {
                control.BackColor = control is ListView || control is TreeView || control is TextBoxBase
                    ? Drawing("ViewBackground") : Drawing("WindowBackground");
                }
            ApplyControls(control);
            }
        }

    private static MWinColors ReadDesktop() {
        MWinColors colors = new MWinColors();
        ReadGtk(colors);
        string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        string desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
        if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase) || desktop.Length == 0) {
            try {
                colors.ImportKde(Path.Combine(config, "kdeglobals"));
                }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) {
                System.Diagnostics.Trace.WriteLine(error.Message);
                }
            }
        return colors;
        }

    private static void ReadGtk(MWinColors colors) {
        try {
            // Headless checks have no initialized GDK display. Never initialize a
            // second GUI backend just to import a color.
            if (gdk_display_get_default() == IntPtr.Zero) {
                return;
                }
            IntPtr label = gtk_label_new("");
            g_object_ref_sink(label);
            try {
                IntPtr context = gtk_widget_get_style_context(label);
                Dictionary<string, string[]> roles = new Dictionary<string, string[]> {
                    ["WindowBackground"] = new string[] { "window_bg_color", "theme_bg_color" },
                    ["WindowText"] = new string[] { "window_fg_color", "theme_fg_color" },
                    ["ViewBackground"] = new string[] { "view_bg_color", "theme_base_color" },
                    ["ViewText"] = new string[] { "view_fg_color", "theme_text_color" },
                    ["ButtonBackground"] = new string[] { "theme_bg_color" },
                    ["ButtonText"] = new string[] { "theme_fg_color" },
                    ["SelectionBackground"] = new string[] { "accent_bg_color", "theme_selected_bg_color" },
                    ["SelectionText"] = new string[] { "accent_fg_color", "theme_selected_fg_color" },
                    ["Accent"] = new string[] { "accent_color", "theme_selected_bg_color" },
                    ["Border"] = new string[] { "borders", "theme_unfocused_fg_color" },
                    ["DisabledText"] = new string[] { "insensitive_fg_color" },
                    ["TitleBackground"] = new string[] { "headerbar_bg_color", "theme_bg_color" },
                    ["TitleText"] = new string[] { "headerbar_fg_color", "theme_fg_color" },
                    };
                foreach (KeyValuePair<string, string[]> role in roles) {
                    foreach (string name in role.Value) {
                        if (gtk_style_context_lookup_color(context, name, out Rgba value) != 0) {
                            uint red = (uint)Math.Clamp((int)Math.Round(value.Red * 255), 0, 255);
                            uint green = (uint)Math.Clamp((int)Math.Round(value.Green * 255), 0, 255);
                            uint blue = (uint)Math.Clamp((int)Math.Round(value.Blue * 255), 0, 255);
                            colors[role.Key] = 0xFF000000 | red << 16 | green << 8 | blue;
                            break;
                            }
                        }
                    }
                }
            finally {
                g_object_unref(label);
                }
            }
        catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException) {
            System.Diagnostics.Trace.WriteLine(error.Message);
            }
        }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rgba {
        internal float Red;
        internal float Green;
        internal float Blue;
        internal float Alpha;
        }
    [DllImport("libgtk-4.so.1", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_display_get_default();
    [DllImport("libgtk-4.so.1", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gtk_label_new([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport("libgtk-4.so.1", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gtk_widget_get_style_context(IntPtr widget);
    [DllImport("libgtk-4.so.1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int gtk_style_context_lookup_color(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out Rgba value);
    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr g_object_ref_sink(IntPtr instance);
    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void g_object_unref(IntPtr instance);
    }

public sealed partial class MainForm {
    partial void ApplyPlatformAppearance() {
        MWinAppearance.Configure(_settings);
        MWinAppearance.Apply(this);
        }
    }

public partial class MnxDialog {
    partial void ApplyPlatformAppearance() {
        MWinAppearance.Apply(this);
        }
    }
