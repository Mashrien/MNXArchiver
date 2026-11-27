using System.Globalization;
using System.Text.Json;
using Mash.GUI.MNXArchiver.Diagnostics;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Gtk4;

namespace Mash.GUI.MNXArchiver;

internal sealed class KWinDialogMovement : IDisposable {
    private const string Scripting = "org.kde.kwin.Scripting";
    private readonly string scriptName = "mnx-dialog-" + Guid.NewGuid().ToString("N");
    private bool loaded;

    internal static bool IsAvailable() {
        if (Platform.ConfiguredBackend is not Gtk4PlatformBackend || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) {
            return false;
            }
        string desktops = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
        return desktops.Split(':').Contains("KDE", StringComparer.OrdinalIgnoreCase);
        }

    internal void Attach(string title) {
        // KWin reads scripts from a path. Keep the source private and remove it
        // once run() has returned. The compositor owns movement during the drag.
        string directory = Directory.CreateTempSubdirectory("mnx-kwin-").FullName;
        string script = Path.Combine(directory, "move-owner.js");
        try {
            using Stream resource = typeof(KWinDialogMovement).Assembly.GetManifestResourceStream("MNX.KWin.MoveDialogOwner")
                ?? throw new InvalidOperationException("The embedded KWin movement script is missing.");
            using StreamReader reader = new StreamReader(resource);
            string pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            string quotedTitle = JsonEncodedText.Encode(title).ToString();
            string identity = $"const mnxPid = {pid};\nconst mnxTitle = \"{quotedTitle}\";\nconst mnxScriptName = '{scriptName}';\n";
            File.WriteAllText(script, identity + reader.ReadToEnd());
            using Gio.DBusConnection bus = Gio.Functions.BusGetSync(Gio.BusType.Session, null);
            using GLib.Variant path = GLib.Variant.NewString(script);
            using GLib.Variant name = GLib.Variant.NewString(scriptName);
            using GLib.Variant parameters = GLib.Variant.NewTuple([path, name]);
            using GLib.Variant result = Call(bus, "/Scripting", Scripting, "loadScript", parameters);
            using GLib.Variant value = result.GetChildValue(0);
            int id = value.GetInt32();
            if (id < 0) {
                throw new InvalidOperationException("KWin declined to load the dialog movement script.");
                }
            loaded = true;
            using GLib.Variant response = Call(bus, "/Scripting/Script" + id.ToString(CultureInfo.InvariantCulture), "org.kde.kwin.Script", "run", null);
            }
        finally {
            File.Delete(script);
            Directory.Delete(directory);
            }
        }

    private static GLib.Variant Call(Gio.DBusConnection bus, string path, string contract, string method, GLib.Variant? arguments) {
        return bus.CallSync("org.kde.KWin", path, contract, method, arguments, null, Gio.DBusCallFlags.NoAutoStart, 750, null);
        }

    public void Dispose() {
        if (!loaded) {
            return;
            }
        loaded = false;
        try {
            using Gio.DBusConnection bus = Gio.Functions.BusGetSync(Gio.BusType.Session, null);
            using GLib.Variant name = GLib.Variant.NewString(scriptName);
            using GLib.Variant parameters = GLib.Variant.NewTuple([name]);
            using GLib.Variant result = Call(bus, "/Scripting", Scripting, "unloadScript", parameters);
            }
        catch (Exception error) {
            DebugLog.Write("Could not release KWin dialog movement: " + error.Message, LogType.GUI | LogType.Warning);
            }
        }
    }
