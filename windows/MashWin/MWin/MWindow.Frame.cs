using System;
using System.ComponentModel;
using System.Drawing;
#if MWIN_PORTABLE
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing.Drawing2D;
#else
using System.Windows.Forms;
using System.Drawing.Drawing2D;
#endif

namespace MashWin {
    public partial class MWindow {
        private bool titleBarDragActive;
        private bool movingOwner;
        private Point titleBarDragLocation;

        [Category("MWin Customization")]
        [DefaultValue(false)]
        [Description("Moves the owner by the same delta while this window is dragged by its title bar.")]
        public bool MoveOwnerWithTitleBar { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int OwnedDialogMoveVersion { get; private set; }

        private void BeginTitleBarDrag() {
            OnTitleBarDragStarted(EventArgs.Empty);
            titleBarDragLocation = Location;
            titleBarDragActive = true;
            }

        protected void EndTitleBarDrag() {
            if (!titleBarDragActive) {
                return;
                }
            try {
                OnTitleBarDragEnded(EventArgs.Empty);
                }
            finally {
                titleBarDragActive = false;
                }
            }

        protected virtual void OnTitleBarDragStarted(EventArgs arguments) { }

        protected virtual void OnTitleBarDragEnded(EventArgs arguments) { }

        protected override void OnResizeEnd(EventArgs arguments) {
            EndTitleBarDrag();
            base.OnResizeEnd(arguments);
            }

        protected override void OnMove(EventArgs arguments) {
            Point current = Location;
            Point delta = new Point(current.X - titleBarDragLocation.X, current.Y - titleBarDragLocation.Y);
            titleBarDragLocation = current;
            if (titleBarDragActive && MoveOwnerWithTitleBar && !movingOwner && Owner != null
                && !Owner.IsDisposed && (delta.X != 0 || delta.Y != 0)) {
                movingOwner = true;
                try {
                    // Only an active caption gesture propagates movement. Initial
                    // centering, layout, and edge resizing never enter this path.
                    if (Owner is MWindow frameOwner) {
                        unchecked {
                            frameOwner.OwnedDialogMoveVersion++;
                            }
                        }
                    Owner.Location = new Point(Owner.Left + delta.X, Owner.Top + delta.Y);
                    }
                finally {
                    movingOwner = false;
                    }
                }
            base.OnMove(arguments);
            }

        private GraphicsPath CreateFramePath(int cornerRadius) {
            // A centered stroke needs half its width on each side. The old
            // asymmetric bounds placed the right/bottom stroke inside content.
            float border = Math.Max(0, Settings.Frame.BorderThickness);
            float inset = border / 2;
            float width = Math.Max(1, ClientSize.Width - border);
            float height = Math.Max(1, ClientSize.Height - border);
            float radius = Math.Min(Math.Max(0, cornerRadius), Math.Min(width, height) / 2);
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0) {
                path.AddRectangle(new RectangleF(inset, inset, width, height));
                return path;
                }
            float diameter = radius * 2;
            float right = inset + width - diameter;
            float bottom = inset + height - diameter;
            path.AddArc(inset, inset, diameter, diameter, 180, 90);
            path.AddArc(right, inset, diameter, diameter, 270, 90);
            path.AddArc(right, bottom, diameter, diameter, 0, 90);
            path.AddArc(inset, bottom, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
            }
        }
    }
