namespace Mash.UI.MNXFile.GUI;

/// <summary>Shared MWin chrome defaults for owned MNX dialogs; content stays designer-editable.</summary>
public partial class MnxDialog : MashWin.MWindow {
    internal static double ConfiguredOpacity { get; set; } = 1.0;
    public MnxDialog() {
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

    partial void ApplyPlatformAppearance();
    protected virtual bool PreserveOwnFrameStyle => false;

    protected override void OnLoad(EventArgs arguments) {
        if (Owner is MashWin.MWindow owner) {
            Opacity = owner.Opacity;
            // Copy appearance, while preserving this dialog's own close/resize policy.
            Settings.Frame.BorderColor = owner.Settings.Frame.BorderColor;
            if (!PreserveOwnFrameStyle) {
                Settings.Frame.BorderThickness = owner.Settings.Frame.BorderThickness;
                Settings.Frame.CornerRadius = owner.Settings.Frame.CornerRadius;
                }
            Settings.Title.Font = owner.Settings.Title.Font;
            Settings.Title.Alignment = owner.Settings.Title.Alignment;
            Settings.ToolStrips.BackgroundColor = owner.Settings.ToolStrips.BackgroundColor;
            Settings.ToolStrips.TextColor = owner.Settings.ToolStrips.TextColor;
            }
        ApplyPlatformAppearance();
        base.OnLoad(arguments);
        }
    }
