using MashWin.Settings;

namespace Mash.GUI.MNXArchiver;

/// <summary>Shared MWin chrome defaults for owned MNX dialogs; content stays designer-editable.</summary>
public partial class MnxDialog : MashWin.MWindow {
    internal static double ConfiguredOpacity { get; set; } = 1.0;
    public MnxDialog() {
        MoveOwnerWithTitleBar = true;
        Opacity = ConfiguredOpacity;
        Settings.StatusBar.Visible = false;
        Settings.MenuSeparator.Enabled = false;
        Settings.Frame.Resizable = false;
        Settings.Frame.ShowSizeGrip = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        }

    protected virtual bool PreserveOwnFrameStyle => false;

    protected override void OnLoad(EventArgs arguments) {
        if (Owner is MashWin.MWindow owner) {
            Opacity = owner.Opacity;
            }
        UserColors.Targets targets = new UserColors.Targets {
            InheritOwnerAppearance = true,
            PreserveFrameGeometry = PreserveOwnFrameStyle,
            };
        UserColors.Apply(MWinDrawing.Current, this, targets);
        base.OnLoad(arguments);
        }
    }
