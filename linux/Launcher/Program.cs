using System.Runtime.CompilerServices;
using Mash.GUI.MNXArchiver.Backends;

namespace Mash.GUI.MNXArchiver.Linux;

internal static class Program {
    [STAThread]
    private static int Main(string[] args) {
        try {
            // These paths must not initialize either UI binding, including when a
            // toolkit is absent. Probes run before reading private application settings.
            if (args.Length == 2 && args[0] == "--probe-backend") {
                return BackendProbe.RunChild(args[1]);
                }
            BackendCommandLine options = BackendCommandLine.Parse(args);
            if (options.Arguments.Length == 1 && (options.Arguments[0] == "--help" || options.Arguments[0] == "-h")) {
                Console.WriteLine("MNX Archive Manager — GTK3 / MWin (GTK4)");
                Console.WriteLine("mnx-gui [--backend=auto|mwin|gtk3] [ARCHIVE | FILES/FOLDERS...]");
                Console.WriteLine("mnx-gui [--backend=...] --extract-here|--extract-to [--] ARCHIVE");
                Console.WriteLine("mnx-gui --list-backends");
                Console.WriteLine("Interface changes in Options apply on the next launch. Automatic prefers MWin.");
                return 0;
                }
            BackendAvailability available = BackendProbe.Detect();
            if (options.Arguments.Length == 1 && options.Arguments[0] == "--list-backends") {
                PrintAvailability("MWin (GTK4)", available.MWinAvailable, available.MWinError);
                PrintAvailability("Native GTK3", available.Gtk3Available, available.Gtk3Error);
                return available.MWinAvailable || available.Gtk3Available ? 0 : 2;
                }
            GuiBackend preference = options.Preference;
            if (!options.HasOverride) {
                try {
                    preference = BackendRuntime.LoadPreference();
                    }
                catch (Exception error) {
                    Console.Error.WriteLine("Could not read interface preference; using Automatic. " + error.Message);
                    }
                }
            BackendSelection selected = BackendSelection.Select(preference, options.HasOverride, available);
            BackendRuntime.Initialize(available, selected);
            if (selected.Warning.Length > 0) {
                Console.Error.WriteLine(selected.Warning);
                }
            if (selected.Backend == GuiBackend.MWin) {
                return RunMWin(options.Arguments);
                }
            return RunGtk3(options.Arguments);
            }
        catch (Exception error) {
            Console.Error.WriteLine("MNX GUI: " + error.Message);
            return 2;
            }
        }

    private static void PrintAvailability(string label, bool available, string error) {
        Console.WriteLine(label + ": " + (available ? "available" : "unavailable — " + error));
        }

    // Keep references to the UI assemblies out of the selection/probing path.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunMWin(string[] args) {
        return EntryPoints.MWinFrontend.Run(args);
        }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunGtk3(string[] args) {
        return EntryPoints.Gtk3Frontend.Run(args);
        }
    }
