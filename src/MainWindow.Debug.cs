using Gtk;
using Mash.GUI.MNXArchiver.Diagnostics;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainWindow {
    private DebugWindow? _debugWindow;

    private void ConfigureDebugWindow() {
        MenuItem item = new MenuItem("Debug Window");
        item.Activated += (_, _) => {
            _debugWindow ??= new DebugWindow(_window);
            _debugWindow.Show();
            DebugLog.Write("Debug window opened.", LogType.GUI);
            };
        Menu menu = (Menu)Widget<MenuItem>("menu_about").Parent;
        menu.Insert(item, 0);
        item.Show();
        DebugLog.Write("GTK interface initialized.", LogType.Init | LogType.GUI);
        }

    private void DisposeDebugWindow() {
        _debugWindow?.Dispose();
        _debugWindow = null;
        DebugLog.Buffer.Dispose();
        }
    }
