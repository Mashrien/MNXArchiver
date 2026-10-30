using System;
using Majorsilence.Forms;
using Majorsilence.Forms.Backends;

namespace MashWin {
    public partial class MWindow {
        // The portable backend does not deliver WM_NCHITTEST. Hover and drag must
        // use the same client-space regions, including the larger drawn size grip.
        private void InitializePortableFrameInput() {
            MouseMove += delegate(object sender, MouseEventArgs arguments) {
                WindowEdge? edge = PortableResizeEdge(arguments.X, arguments.Y);
                if (edge == WindowEdge.SouthEast) {
                    Cursor.Current = Cursors.BottomRightCorner;
                    }
                else if (edge == WindowEdge.East) {
                    Cursor.Current = Cursors.SizeWE;
                    }
                else if (edge == WindowEdge.South) {
                    Cursor.Current = Cursors.SizeNS;
                    }
                };
            MouseDown += delegate(object sender, MouseEventArgs arguments) {
                if (arguments.Button != MouseButtons.Left || !Settings.Frame.ShowChrome) {
                    return;
                    }
                WindowEdge? edge = PortableResizeEdge(arguments.X, arguments.Y);
                if (edge.HasValue) {
                    BeginResizeDrag(edge.Value);
                    }
                else if (arguments.Y < 30) {
                    BeginMoveDrag();
                    }
                };
            }

        private WindowEdge? PortableResizeEdge(int x, int y) {
            if (!Settings.Frame.ShowChrome || !Settings.Frame.Resizable || WindowState != FormWindowState.Normal) {
                return null;
                }
            bool rightEdge = x >= ClientSize.Width - 20;
            bool bottomEdge = y >= ClientSize.Height - 20;
            if (rightEdge && bottomEdge) {
                return WindowEdge.SouthEast;
                }
            if (rightEdge) {
                return WindowEdge.East;
                }
            if (bottomEdge) {
                return WindowEdge.South;
                }
            return null;
            }
        }
    }
