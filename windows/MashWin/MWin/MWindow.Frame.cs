using System;
using System.ComponentModel;
using System.Drawing;
#if MWIN_PORTABLE
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing;
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

        // On native Wayland the compositor tracks the complete gesture, including
        // release. Managed Location changes must never replay the same movement.
        protected virtual bool CompositorMovesOwner => false;

        private void BeginTitleBarDrag() {
            OnTitleBarDragStarted(EventArgs.Empty);
            titleBarDragLocation = Location;
            titleBarDragActive = !CompositorMovesOwner;
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

        private void PaintStatusDivider(Graphics graphics) {
            int top = StatusBarTop;
            int start = Math.Max(TitleContentTop, top - StatusSeparatorHeight);
            if (start >= top) {
                return;
                }
            GraphicsState state = graphics.Save();
            graphics.SetClip(WindowGP, CombineMode.Intersect);
            using (SolidBrush divider = new SolidBrush(Settings.MenuSeparator.Color)) {
                graphics.FillRectangle(divider, 0, start, ClientSize.Width, top - start);
                }
#if MWIN_PORTABLE
            // The inset StatusStrip paints its own top edge. Continue that same
            // row through the two gutters, leaving the hosted strip and grip alone.
            if (statusBarStrip != null && statusBarStrip.Visible) {
                BorderSideStyle topEdge = statusBarStrip.Style.Border.Top;
                int rows = Math.Min(topEdge.GetWidth(), statusBarControls.Height);
                if (rows > 0) {
                    SkiaSharp.SKColor ink = topEdge.GetColor();
                    using (SolidBrush edge = new SolidBrush(Color.FromArgb(ink.Alpha, ink.Red, ink.Green, ink.Blue))) {
                        graphics.FillRectangle(edge, 0, top, statusBarControls.Left, rows);
                        graphics.FillRectangle(edge, statusBarControls.Right, top, ClientSize.Width - statusBarControls.Right, rows);
                        }
                    }
                }
#endif
            graphics.Restore(state);
            }

        // Preserve the established main-window silhouette. Fixed dialogs use the
        // inset outline below so docked controls cannot cover their bottom corners.
        private GraphicsPath CreateStatusWindowOutline(int radius) {
            int stroke = Math.Max(0, Settings.Frame.BorderThickness);
            int inset = stroke / 2;
            int diameter = radius * 2;
            int right = ClientSize.Width - diameter - stroke - 2 * inset - 1;
            int bottom = ClientSize.Height - diameter - stroke - 2 * inset - 1;
            int outerArc = diameter + stroke + 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(inset, inset, diameter + inset, diameter + inset, 180, 90);
            path.AddArc(right, inset, outerArc, outerArc, 270, 90);
            path.AddArc(right, bottom, outerArc, outerArc, 0, 90);
            path.AddArc(inset, bottom, outerArc, outerArc, 90, 90);
            path.CloseFigure();
            return path;
            }

        private GraphicsPath CreateFramePath(int cornerRadius) {
            if (Settings.StatusBar.Visible && cornerRadius > 0 && cornerRadius * 2 < Math.Min(ClientSize.Width, ClientSize.Height)) {
                return CreateStatusWindowOutline(cornerRadius);
                }
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
