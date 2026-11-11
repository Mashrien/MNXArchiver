using Gtk;

namespace Mash.GUI.MNXArchiver;

/// <summary>One opacity preference for application windows; errors remain opaque.</summary>
internal static class GtkWindowAppearance {
    private static double _opacity = 1.0;

    internal static void Configure(int opacityPercent) {
        _opacity = Math.Clamp(opacityPercent, 10, 100) / 100.0;
        foreach (Gtk.Window window in Gtk.Window.ListToplevels()) {
            Apply(window);
            }
        }

    internal static void Apply(Gtk.Window window) {
        bool error = window is MessageDialog message && message.MessageType == MessageType.Error;
        window.Opacity = error ? 1.0 : _opacity;
        }
    }
