using Gtk;
using Mash.UI.MNXFile.GUI.Icons;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    private void ConfigureOwnedIcons() {
        Widget<Button>("navigate_up").Image = OwnedIcons.Image("up", 24);
        Widget<Button>("navigate_root").Image = OwnedIcons.Image("home", 24);
        foreach (ArchiveAction action in Enum.GetValues<ArchiveAction>()) {
            string name = action.ToString().ToLowerInvariant();
            Widget<ToolButton>("action_" + name).IconWidget = OwnedIcons.Image(name, 48);
            SetMenuIcon("menu_" + name, name);
            }
        SetMenuIcon("menu_resume", "recheck");
        SetMenuIcon("menu_close", "delete");
        SetMenuIcon("menu_options", "options");
        SetMenuIcon("menu_about", "info");
        }

    private void SetMenuIcon(string id, string name) {
        OwnedIcons.SetMenuImage(Widget<MenuItem>(id), name);
        }
    }
