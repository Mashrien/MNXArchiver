namespace Mash.GUI.MNXArchiver.Backends;

public enum GuiBackend {
    Automatic,
    MWin,
    Gtk3
    }

public sealed class BackendAvailability {
    public bool MWinAvailable { get; }
    public bool Gtk3Available { get; }
    public string MWinError { get; }
    public string Gtk3Error { get; }
    public BackendAvailability(bool mwin, bool gtk3, string mwinError = "", string gtk3Error = "") {
        MWinAvailable = mwin;
        Gtk3Available = gtk3;
        MWinError = mwinError;
        Gtk3Error = gtk3Error;
        }
    }

public sealed class BackendSelection {
    public GuiBackend Backend { get; }
    public string Warning { get; }
    public BackendSelection(GuiBackend backend, string warning = "") {
        Backend = backend;
        Warning = warning;
        }
    public static BackendSelection Select(GuiBackend preference, bool explicitChoice, BackendAvailability available) {
        if (preference == GuiBackend.MWin && available.MWinAvailable) {
            return new BackendSelection(GuiBackend.MWin);
            }
        if (preference == GuiBackend.Gtk3 && available.Gtk3Available) {
            return new BackendSelection(GuiBackend.Gtk3);
            }
        if (explicitChoice && preference != GuiBackend.Automatic) {
            string detail = preference == GuiBackend.MWin ? available.MWinError : available.Gtk3Error;
            throw new InvalidOperationException(Label(preference) + " is unavailable. " + detail);
            }
        GuiBackend fallback;
        if (available.MWinAvailable) {
            fallback = GuiBackend.MWin;
            }
        else if (available.Gtk3Available) {
            fallback = GuiBackend.Gtk3;
            }
        else {
            throw new InvalidOperationException("No usable GUI backend was found. Install GTK4 for MWin or GTK3 for the native interface.\nMWin: "
                + available.MWinError + "\nGTK3: " + available.Gtk3Error);
            }
        string warning = "";
        if (preference != GuiBackend.Automatic) {
            warning = "Saved interface " + Label(preference) + " is unavailable; using " + Label(fallback)
                + " for this launch. The saved preference has been retained.";
            }
        return new BackendSelection(fallback, warning);
        }
    public static GuiBackend ParsePreference(string value) {
        if (value.Equals("mwin", StringComparison.OrdinalIgnoreCase) || value.Equals("gtk4", StringComparison.OrdinalIgnoreCase)) {
            return GuiBackend.MWin;
            }
        if (value.Equals("gtk3", StringComparison.OrdinalIgnoreCase)) {
            return GuiBackend.Gtk3;
            }
        return GuiBackend.Automatic;
        }

    public static string Key(GuiBackend backend) {
        if (backend == GuiBackend.MWin) {
            return "mwin";
            }
        if (backend == GuiBackend.Gtk3) {
            return "gtk3";
            }
        return "auto";
        }

    public static string Label(GuiBackend backend) {
        if (backend == GuiBackend.MWin) {
            return "MWin (GTK4)";
            }
        if (backend == GuiBackend.Gtk3) {
            return "Native GTK3";
            }
        return "Automatic (prefer MWin)";
        }
    }
