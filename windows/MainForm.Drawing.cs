using MashWin.Settings;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private void ApplyDrawingAppearance() {
        UserColors.Theme theme = MWinDrawing.Load(_settings);
        UserColors.Targets targets = new UserColors.Targets {
            Toolbar = mainToolbar,
            Navigation = pathPanel,
            NavigationLabels = new Control[] { pathLabel, searchLabel },
            Menus = new ToolStrip[] { mainMenu, entryContextMenu },
            FileList = fileList,
            FolderTree = folderTree,
            StatusDivider = true,
            };
        UserColors.Apply(theme, this, targets);
        }
    }
