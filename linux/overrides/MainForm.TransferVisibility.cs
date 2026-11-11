using Majorsilence.Forms.Gtk4;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    partial void SetPlatformTransferConcealment(bool concealed, ref bool handled) {
        handled = Gtk4HostInterop.TryConcealForTransfer(this, concealed);
        }

    partial void ReadPlatformWindowPosition(ref Point position) {
        if (Gtk4HostInterop.TryReadX11Position(this, out Point nativePosition)) {
            position = nativePosition;
            }
        }

    partial void RestorePlatformWindowPosition(Point position) {
        Gtk4HostInterop.RestoreX11Position(this, position);
        }
    }
