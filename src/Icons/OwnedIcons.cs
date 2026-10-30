using Gtk;

namespace Mash.UI.MNXFile.GUI.Icons;

/// <summary>Application-owned PNGs. No icon-theme lookup or runtime SVG loader.</summary>
internal static class OwnedIcons {
    private static readonly Dictionary<(string Name, int Size), Gdk.Pixbuf> Pixels = new();
    internal static readonly string[] Names = [
        "add", "alert", "archive", "compact", "delete", "extract", "extract-plain",
        "file", "folder", "home", "info", "new", "new-folder", "open",
        "options", "question", "recheck", "save", "search", "sliders", "test", "up", "warning"
        ];
    internal static readonly int[] Sizes = [16, 24, 32, 48];

    /// <summary>Borrow a cached image; the application owns its lifetime.</summary>
    internal static Gdk.Pixbuf Get(string name, int size) {
        if (!Names.Contains(name, StringComparer.Ordinal) || !Sizes.Contains(size)) {
            throw new ArgumentException("Unknown MNX icon or unsupported size: " + name + "/" + size);
            }
        if (Pixels.TryGetValue((name, size), out Gdk.Pixbuf? existing)) {
            return existing;
            }
        string resource = $"Mash.UI.MNXFile.GUI.Icons.{size}.{name}.png";
        if (name == "archive") {
            using Stream original = typeof(OwnedIcons).Assembly.GetManifestResourceStream("Mash.UI.MNXFile.GUI.archive.png")
                ?? throw new InvalidDataException("Missing embedded application icon.");
            using Gdk.Pixbuf archive = new Gdk.Pixbuf(original);
            Gdk.Pixbuf scaled = archive.ScaleSimple(size, size, Gdk.InterpType.Bilinear);
            Pixels.Add((name, size), scaled);
            return scaled;
            }
        using Stream stream = typeof(OwnedIcons).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException("Missing embedded MNX icon: " + resource);
        Gdk.Pixbuf image = new Gdk.Pixbuf(stream);
        Pixels.Add((name, size), image);
        return image;
        }

    internal static Image Image(string name, int size) {
        return new Image(Get(name, size)) {
            Visible = true
            };
        }

    internal static void SetMenuImage(MenuItem item, string name) {
        Widget label = item.Child;
        item.Remove(label);
        Box content = new Box(Orientation.Horizontal, 8);
        content.PackStart(Image(name, 16), false, false, 0);
        content.PackStart(label, true, true, 0);
        item.Add(content);
        content.ShowAll();
        }

    internal static MessageDialog Dialog(
        Gtk.Window parent,
        DialogFlags flags,
        MessageType type,
        ButtonsType buttons,
        bool markup,
        string message,
        params object[] arguments
        ) {
        MessageDialog dialog = new MessageDialog(parent, flags, type, buttons, markup, message, arguments);
        GtkWindowAppearance.Apply(dialog);
        string name = type switch {
            MessageType.Error => "delete",
            MessageType.Warning => "warning",
            MessageType.Question => "question",
            _ => "info"
            };
        // GTK3 supports replacing this image directly. Keep the actual
        // MessageDialog type so existing response handling stays unchanged.
#pragma warning disable CS0612
        dialog.Image = Image(name, 48);
#pragma warning restore CS0612
        // GTK resets MessageType to Other when replacing the image. Restore
        // the question/warning/error identity used by accessibility and callers.
        dialog.MessageType = type;
        return dialog;
        }

    internal static MessageDialog Dialog(
        Gtk.Window parent,
        DialogFlags flags,
        MessageType type,
        ButtonsType buttons,
        string message
        ) {
        return Dialog(parent, flags, type, buttons, false, "{0}", message);
        }

    internal static void Dispose() {
        foreach (Gdk.Pixbuf image in Pixels.Values) {
            image.Dispose();
            }
        Pixels.Clear();
        }
    }
