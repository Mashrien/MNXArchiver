using System.Runtime.InteropServices;
using Mash.GUI.MNXArchiver.Settings;

namespace Mash.GUI.MNXArchiver;

internal static partial class MWinDrawing {
    static partial void ReadPortableColors(MWinColors colors) {
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
