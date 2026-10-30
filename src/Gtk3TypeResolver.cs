using System.Reflection;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Keep GtkSharp's native-object wrappers separate from embedded GTK4 bindings.</summary>
internal static class Gtk3TypeResolver {
    private static Assembly[]? _bindings;

    internal static void Install() {
        if (_bindings is not null) {
            return;
            }
        _bindings = new Assembly[] {
            typeof(Gtk.Widget).Assembly,
            typeof(Gdk.Window).Assembly,
            typeof(GLib.Object).Assembly,
            typeof(GLib.Application).Assembly,
            typeof(Pango.Font).Assembly,
            typeof(Atk.Object).Assembly,
            typeof(Cairo.Context).Assembly
            };
        // GtkSharp normally scans every loaded assembly for names such as
        // Gtk.Window. NativeAOT also exposes the embedded gir.core assemblies,
        // whose identically named types use different native handle constructors.
        GLib.GType.ResolveType += Resolve;
        }

    private static Type? Resolve(GLib.GType nativeType, string nativeName) {
        GLib.GType current = nativeType;
        string name = nativeName;
        while (current != GLib.GType.None && current != GLib.GType.Invalid) {
            string? managedName = ManagedName(name);
            if (managedName is not null) {
                foreach (Assembly assembly in _bindings!) {
                    Type? wrapper = assembly.GetType(managedName);
                    if (wrapper is not null) {
                        return wrapper;
                        }
                    }
                }
            // Backend-specific native subclasses (for example GdkX11Window)
            // may only have a portable parent wrapper in GtkSharp.
            current = current.GetBaseType();
            name = current.ToString();
            }
        return null;
        }

    private static string? ManagedName(string nativeName) {
        for (int index = 1; index < nativeName.Length; index++) {
            if (char.IsUpper(nativeName[index])) {
                if (index == 1 && nativeName[0] == 'G') {
                    return "GLib." + nativeName.Substring(1);
                    }
                return nativeName.Insert(index, ".");
                }
            }
        return null;
        }
    }
