namespace Mash.GUI.MNXArchiver.EntryPoints;

/// <summary>Entry point used by the single-binary Linux launcher.</summary>
public static class Gtk3Frontend {
    public static int Run(string[] args) {
        return Program.Run(args);
        }
    }
