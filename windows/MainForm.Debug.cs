using Mash.GUI.MNXArchiver.Diagnostics;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private DebugForm? _debugWindow;

    private void ConfigureDebugWindow() {
        ToolStripMenuItem debugMenu = new ToolStripMenuItem("Debug Window");
        debugMenu.Click += (_, _) => ShowDebugWindow();
        helpMenu.DropDownItems.Insert(0, debugMenu);
        FormClosed += (_, _) => {
            _debugWindow?.Shutdown();
            _debugWindow = null;
            DebugLog.Buffer.Dispose();
            };
        DebugLog.Write("Archive manager interface initialized.", LogType.Init | LogType.GUI);
        }

    internal void ShowDebugWindow() {
        if (_debugWindow is null || _debugWindow.IsDisposed) {
            _debugWindow = new DebugForm();
            }
        _debugWindow.ShowLog(this);
        DebugLog.Write("Debug window opened.", LogType.GUI);
        }
    }
