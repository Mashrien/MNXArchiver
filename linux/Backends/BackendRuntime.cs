using Mash.UI.MNXFile.GUI.Settings;

namespace Mash.UI.MNXFile.GUI.Backends;

/// <summary>Toolkit-free startup state shared with both options windows.</summary>
public static class BackendRuntime {
    public static BackendAvailability? Availability { get; private set; }
    public static GuiBackend Current { get; private set; }
    public static string StartupWarning { get; private set; } = "";

    public static void Initialize(BackendAvailability availability, BackendSelection selected) {
        Availability = availability;
        Current = selected.Backend;
        StartupWarning = selected.Warning;
        }

    public static GuiBackend LoadPreference(string? directory = null) {
        AppSettings settings = AppSettings.Load(directory);
        return BackendSelection.ParsePreference(settings.Get("Interface", "Backend", "auto"));
        }
    }
