namespace Mash.GUI.MNXArchiver;

public sealed class PercentageProgressBar : Control {
    private int _percentage;
    private bool _preparing;

    public PercentageProgressBar() {
        DoubleBuffered = true;
        Height = 24;
        Dock = DockStyle.Fill;
        }

    internal void UpdateValue(long completed, long total, bool preparing) {
        _preparing = preparing;
        _percentage = total > 0 ? (int)Math.Clamp((double)completed / total * 100, 0, 100) : 0;
        AccessibleName = preparing ? "Preparing" : _percentage + "%";
        Invalidate();
        }

    protected override void OnPaint(PaintEventArgs args) {
        base.OnPaint(args);
        Rectangle bounds = ClientRectangle;
        args.Graphics.Clear(SystemColors.ControlLight);
        int completedWidth = (int)((long)Math.Max(0, bounds.Width - 2) * _percentage / 100);
        if (completedWidth > 0) {
            args.Graphics.FillRectangle(SystemBrushes.Highlight, 1, 1, completedWidth, Math.Max(0, bounds.Height - 2));
            }
        ControlPaint.DrawBorder(args.Graphics, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
        string caption = _preparing ? "Preparing…" : _percentage + "%";
        TextFormatFlags alignment = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(args.Graphics, caption, Font, bounds, SystemColors.ControlText, alignment);
        if (completedWidth > 0) {
            System.Drawing.Drawing2D.GraphicsState state = args.Graphics.Save();
            args.Graphics.SetClip(new Rectangle(1, 1, completedWidth, Math.Max(0, bounds.Height - 2)));
            TextRenderer.DrawText(args.Graphics, caption, Font, bounds, SystemColors.HighlightText, alignment | TextFormatFlags.PreserveGraphicsClipping);
            args.Graphics.Restore(state);
            }
        }
    }

