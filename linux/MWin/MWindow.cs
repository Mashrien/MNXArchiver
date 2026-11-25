using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using ContentAlignment = Majorsilence.Forms.ContentAlignment;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Text;
using System.Runtime.InteropServices;
using Majorsilence.Forms;
using static MashWin.Win32;

namespace MashWin {
    [DefaultProperty("Frame")]
    public partial class MWindow : Form {

        #region members

        private Image IconImage;
        private Icon ownedWindowIcon;
        private GraphicsPath WindowGP;
        private bool HasBeenResized;
        private Size LastSize;
        private Rectangle SrcRect, DestRect, _WindowRect, _TitleTextRect;

        private MWindowSettings settings = new MWindowSettings();

        private Pen AppPenBorder = Pens.DarkCyan;
        private Pen AppPenSeparator = Pens.DarkCyan;
        private Pen AppPenTitleDivider = Pens.DarkCyan;
        public Pen BorderPen {
            get {
                return AppPenBorder;
                }
            }
        private Pen AppPenSizeGrip = Pens.DarkCyan;
        private Brush AppBrushTitle = new SolidBrush(Color.FromArgb(190, Color.DarkCyan));
        private Brush AppBrushBackground;
        private StringFormat StatusTextAlignment = new StringFormat() { LineAlignment = StringAlignment.Center };
        private StringFormat TitleTextAlignment = new StringFormat() { LineAlignment = StringAlignment.Center };

        public bool UserDrawn = false;

        private bool firstDraw = true;
        private Color OldBackColor;

        public object OwnerObj;

        // import for icon
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        #endregion members

        #region constructor
        public MWindow() {
            //Settings = new MWindowSettings();
            AppBrushBackground = new SolidBrush(this.BackColor);
            AppPenBorder = new Pen(this.Settings.Frame.BorderColor);
            AppPenSeparator = new Pen(this.Settings.MenuSeparator.Color);
            AppPenTitleDivider = new Pen(this.Settings.Frame.BorderColor);
            AppPenSizeGrip = new Pen(this.Settings.Frame.BorderColor);

            List<Control> cs = new List<Control>();
            foreach (Control c in this.Controls) {
                if (c is RoundButton) {
                    ((RoundButton)c).ExtendedSettings.BorderThickness = this.Settings.Frame.BorderThickness;
                    ((RoundButton)c).BorderPen = this.BorderPen;
                    if (c.Name != "rbExit") {
                        cs.Add(c);
                        }
                    }
                }
            if (Settings.MenuSeparator.Enabled) {
                foreach (Control c in cs) {
                    this.Controls.Remove(c);
                    }
                }
            cs.Clear();

            LastSize = new Size(0, 0);
            if (this.IconImage != null) {
                SrcRect = new Rectangle(0, 0, this.Icon.Width, this.Icon.Height);
                } else {
                SrcRect = new Rectangle(0, 0, 1, 1);
                //                DestRect = new Rectangle(0, 0, 1, 1);
                }
            InitializeComponent();
            // Portable Forms defaults to a transparent background; match WinForms' opaque chrome.
            BackColor = Majorsilence.Forms.SystemColors.Control;
            Opacity = 1.0;

            base.Size = this.Size;
            base.ClientSize = this.ClientSize;

            AppPenSeparator.Width = Settings.MenuSeparator.Thickness;

            if (Settings.CaptionButtons.ExitBox) {

                // .NET port: assign caption controls to the fields used during resize/disposal.
                rbExit = new RoundButton();
                rbExit.Name = "rbExit";
                rbExit.Text = "X";
                // DrawContent now honors TextAlign; the legacy default was BottomRight.
                rbExit.TextAlign = ContentAlignment.MiddleCenter;
                rbExit.UseCompatibleTextRendering = false;
                rbExit.ExtendedSettings.VistaStyleGradientBackground = true;
                rbExit.Click += rbExit_Click;
                this.Controls.Add(rbExit);
                }
            if (Settings.CaptionButtons.MaximizeBox) {
                rbMaximize = new RoundButton();
                rbMaximize.Name = "rbMaximize";
                rbMaximize.Text = "□";
                rbMaximize.TextAlign = ContentAlignment.MiddleCenter;
                rbMaximize.ExtendedSettings.VistaStyleGradientBackground = true;
                rbMaximize.Click += rbMaximize_Click;
                this.Controls.Add(rbMaximize);
                }
            if (Settings.CaptionButtons.MinimizeBox) {
                rbMinimize = new RoundButton();
                rbMinimize.Name = "rbMinimize";
                rbMinimize.ExtendedSettings.VistaStyleGradientBackground = true;
                rbMinimize.Text = "__";
                rbMinimize.TextAlign = ContentAlignment.BottomCenter;
                rbMinimize.Click += rbMinimize_Click;
                this.Controls.Add(rbMinimize);
                }

            InitializeStatusBar();
            InitializePortableFrameInput();
            StartToolStripStyling();
            settings.PropertyChanged += SettingsChanged;
            ApplyCaptionColors();
            ApplySettings();
            }
        #endregion constructor

        #region wincontrols
        private void ApplyCaptionColors() {
            rbExit?.SetCaptionColors(settings.CaptionButtons.Exit);
            ApplyMinMaxColors();
            }

        private void ApplyMinMaxColors() {
            rbMaximize?.SetCaptionColors(settings.CaptionButtons.MinMax);
            rbMinimize?.SetCaptionColors(settings.CaptionButtons.MinMax);
            }

        private void MoveButtons() {
            if (rbExit == null || rbMaximize == null || rbMinimize == null) {
                return;
                }

            bool show = settings.Frame.ShowChrome && ControlBox;
            bool showExit = show && settings.CaptionButtons.ExitBox;
            bool showMaximize = show && MaximizeBox && settings.CaptionButtons.MaximizeBox;
            bool showMinimize = show && MinimizeBox && settings.CaptionButtons.MinimizeBox;
            rbExit.Visible = showExit;
            rbMaximize.Visible = showMaximize;
            rbMinimize.Visible = showMinimize;
            if (!showExit && !showMaximize && !showMinimize) {
                return;
                }

            double scale = settings.Title.Height / (double)TitleSettings.DefaultHeight;
            int height = (int)Math.Round(20 * scale);
            int border = Math.Max(0, settings.Frame.BorderThickness);
            int top = border - 1;
            int right = ClientSize.Width - Math.Max(0, settings.Frame.CornerRadius) - border;
            int count = (showExit ? 1 : 0) + (showMaximize ? 1 : 0) + (showMinimize ? 1 : 0);
            int availableWidth = Math.Max(1, (right - border + count - 1) / count);
            int width = Math.Min(availableWidth, (int)Math.Round(30 * scale));
            int step = Math.Max(1, width - 1);

            if (showExit) {
                SetCaptionBounds(rbExit, right, top, width, height, !showMaximize && !showMinimize, true);
                rbExit.SetCaptionFont(Font, (float)(Font.SizeInPoints * scale));
                right -= step;
                }
            if (showMaximize) {
                SetCaptionBounds(rbMaximize, right, top, width, height, !showMinimize, !showExit);
                // Balance the visible square within the font's larger line box.
                rbMaximize.TextDrawingOffset = new Point(0, (int)Math.Round(-3 * scale));
                rbMaximize.SetCaptionFont(Font, (float)(14 * scale));
                right -= step;
                }
            if (showMinimize) {
                SetCaptionBounds(rbMinimize, right, top, width, height, true, !showExit && !showMaximize);
                rbMinimize.SetCaptionFont(Font, (float)(Font.SizeInPoints * scale));
                }
            }

        private static void SetCaptionBounds(RoundButton button, int right, int top, int width, int height,
            bool roundLeft, bool roundRight) {
            ExtendedButtonSettings.RoundedCorners corners = ExtendedButtonSettings.RoundedCorners.None;
            if (roundLeft) {
                corners |= ExtendedButtonSettings.RoundedCorners.BottomLeft;
                }
            if (roundRight) {
                corners |= ExtendedButtonSettings.RoundedCorners.BottomRight;
                }

            button.ExtendedSettings.RoundCorners = corners;
            button.SetBounds(right - width, top, width, height);
            button.Invalidate();
            }

        protected void ForceIcoIcon() {
            // Keep an owned bitmap for painting; never dispose a caller's image.
            if (Settings.Title.Icon != null) {
                SetIcon(Settings.Title.Icon);
                }
            else if (Icon != null) {
                SetIcon(Icon);
                }
            }

        public void SetIcon(Icon icon) {
            if (icon == null) {
                throw new ArgumentNullException(nameof(icon));
                }
            using (Bitmap bitmap = icon.ToBitmap()) {
                SetIcon(bitmap);
                }
            }

        public void SetIcon(Image image) {
            if (image == null) {
                throw new ArgumentNullException(nameof(image));
                }
            Image previousImage = IconImage;
            IconImage = (Image)image.Clone();
            previousImage?.Dispose();
            SrcRect = new Rectangle(0, 0, IconImage.Width, IconImage.Height);
            // Portable icon transfer: encode image bytes instead of allocating a Win32 HICON.
            using (System.IO.MemoryStream encoded = new System.IO.MemoryStream()) {
                IconImage.Save(encoded, Majorsilence.Forms.Drawing.Imaging.ImageFormat.Png);
                encoded.Position = 0;
                Icon replacement = new Icon(encoded);
                Icon = replacement;
                ownedWindowIcon?.Dispose();
                ownedWindowIcon = replacement;
                }
            Invalidate();
            }
        #endregion wincontrols

        #region properties
        // Separate expandable entries share a named category in the property grid.
        [Category("MWin Customization")]
        [DisplayName("Frame")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public FrameSettings Frame {
            get {
                return settings.Frame;
                }
            }

        [Category("MWin Customization")]
        [DisplayName("Caption Buttons")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public CaptionButtonSettings CaptionButtons {
            get {
                return settings.CaptionButtons;
                }
            }

        [Category("MWin Customization")]
        [DisplayName("Title")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public TitleSettings Title {
            get {
                return settings.Title;
                }
            }

        [Category("MWin Customization")]
        [DisplayName("Menu Separator")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public MenuSeparatorSettings MenuSeparator {
            get {
                return settings.MenuSeparator;
                }
            }

        [Category("MWin Customization")]
        [DisplayName("Status Bar")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public StatusBarSettings StatusBar {
            get {
                return settings.StatusBar;
                }
            }

        [Category("MWin Customization")]
        [DisplayName("Tool Strips")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public ToolStripSettings ToolStrips {
            get {
                return settings.ToolStrips;
                }
            }

        // The public container shares the same groups as the designer entries.
        // Only those entries serialize, avoiding duplicate assignments.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MWindowSettings Settings {
            get {
                return settings;
                }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                    }
                if (ReferenceEquals(settings, value)) {
                    return;
                    }
                settings.PropertyChanged -= SettingsChanged;
                settings = value;
                settings.PropertyChanged += SettingsChanged;
                UpdateSettingsIcon();
                ApplyCaptionColors();
                ApplySettings();
                }
            }

        // Keep the old container name as a hidden alias.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MWindowSettings ExtendedSettings {
            get {
                return Settings;
                }
            set {
                Settings = value;
                }
            }

        private void SettingsChanged(object sender, PropertyChangedEventArgs arguments) {
            if (arguments.PropertyName == "Title.Icon") {
                UpdateSettingsIcon();
                }
            if (arguments.PropertyName.StartsWith("CaptionButtons.Exit.", StringComparison.Ordinal)) {
                rbExit?.SetCaptionColors(settings.CaptionButtons.Exit);
                }
            else if (arguments.PropertyName.StartsWith("CaptionButtons.MinMax.", StringComparison.Ordinal)) {
                ApplyMinMaxColors();
                }
            if (arguments.PropertyName == "StatusBar.Text") {
                ApplyStatusBarSettings();
                return;
                }
            ApplySettings();
            }

        private void UpdateSettingsIcon() {
            if (settings.Title.Icon != null) {
                SetIcon(settings.Title.Icon);
                }
            else {
                IconImage?.Dispose();
                IconImage = null;
                Icon = null;
                ownedWindowIcon?.Dispose();
                ownedWindowIcon = null;
                }
            }

        private void ApplySettings() {
            if (IsDisposed || Disposing) {
                return;
                }

            // Shape and alignment used to refresh only on resize. Nested property
            // changes must also discard those cached values and update drawing pens.
            HasBeenResized = true;
            if (toolStripRenderer != null) {
                toolStripRenderer.Settings = settings.ToolStrips;
                toolStripRenderer.MenuSeparator = settings.MenuSeparator;
                }
            // Match the Windows title color setting in the portable renderer.
            if (((SolidBrush)AppBrushTitle).Color != settings.Title.TextColor) {
                AppBrushTitle.Dispose();
                AppBrushTitle = new SolidBrush(settings.Title.TextColor);
                }
            AppPenBorder.Color = settings.Frame.BorderColor;
            AppPenBorder.Width = settings.Frame.BorderThickness;
            AppPenSeparator.Color = settings.MenuSeparator.Color;
            AppPenSeparator.Width = settings.MenuSeparator.Thickness;
            AppPenTitleDivider.Color = settings.MenuSeparator.TitleDividerColor(settings.Frame.BorderColor);
            AppPenTitleDivider.Width = settings.Frame.BorderThickness;
            AppPenSizeGrip.Color = settings.Frame.BorderColor;
            pUpdateAlignment();
            int border = Math.Max(0, settings.Frame.BorderThickness);
            int iconSize = Math.Max(1, settings.Title.Height - border);
            DestRect = new Rectangle(1 + border, 1 + border, iconSize, iconSize);
            ApplyStatusBarSettings();
            MoveButtons();
            PerformLayout();
            Invalidate(true);
            }
        #endregion properties

        #region events
        public event CloseClicked bClose;
        public delegate bool CloseClicked(object c, EventArgs e);
        public event MaximizeClicked bMaximize;
        public delegate bool MaximizeClicked(object c, EventArgs e);
        public event MinimizeClicked bMinimize;
        public delegate bool MinimizeClicked(object c, EventArgs e);
        #endregion events

        #region wndproc
        protected override void WndProc(ref Message m) {
            // this is SPECIFICALLY to abort a maximize call by double-clicking the titlebar
            if (m.Msg == WM_LBUTTONDBLCLK && !MaximizeBox) {
                m.Result = (IntPtr)0;
                return;
                }

            if (m.Msg == WM_NCHITTEST && Settings.Frame.ShowChrome) {
                // Keep ordinary client areas interactive while preserving native caption/resize handling.
                m.Result = (IntPtr)1; // HTCLIENT
                // WM_NCHITTEST supplies screen coordinates, including negative
                // monitor positions. Do not sample an unrelated current cursor.
                long coordinates = m.LParam.ToInt64();
                Point loc = PointToClient(new Point(unchecked((short)coordinates),
                    unchecked((short)(coordinates >> 16))));

                if (loc.Y <= TitleDividerY) {
                    m.Result = (IntPtr)2;
                    }

                bool blnRight = (loc.X > this.Width - (16 + Settings.Frame.BorderThickness));
                bool blnBottom = (loc.Y > this.Height - (16 + Settings.Frame.BorderThickness));
                if (Settings.Frame.Resizable)
                    if (blnRight && blnBottom) {
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                        return;
                        } else if (blnRight) {
                        m.Result = (IntPtr)HTRIGHT;
                        return;
                        } else if (blnBottom) {
                        m.Result = (IntPtr)HTBOTTOM;
                        return;
                        } else if (Settings.Frame.ShowSizeGrip) {
                        if (blnRight && blnBottom) {
                            m.Result = (IntPtr)HTBOTTOMRIGHT;
                            return;
                            }
                        }

                if (loc.X < DestRect.Right && loc.Y <= TitleDividerY) {
                    m.Result = (IntPtr)3;
                    return;
                    }

                return;
                }
            if (m.Msg == 0x0312) {
                Keys keys = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                }

            base.WndProc(ref m);
            }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                if (MaximizeBox) cp.Style |= WS_MAXIMIZEBOX;
                if (MinimizeBox) cp.Style |= WS_MINIMIZEBOX;
                return cp;
                }
            }
        #endregion wndproc

        #region pinvoke
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("gdi32.dll")]
        public static extern bool RoundRect(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidth, int nHeight);

        public static GraphicsPath RoundedRect(GraphicsPath path, Rectangle bounds, int radius) {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            bounds.Offset(1, 1);
            size = new Size(size.Width + 1, size.Height + 1);
            Rectangle arc = new Rectangle(bounds.Location, size);

            if (radius == 0) {
                path.AddRectangle(bounds);
                return path;
                }

            // top left arc
            path.AddArc(arc, 180, 90);

            // top right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
            }

        #endregion pinvoke

        #region drawing

        private void MWindow_Shown(object sender, EventArgs e) {
            if (this.Settings.MenuSeparator.Enabled && !rbMinimize.IsDisposed) {
                this.Controls.Remove(rbMinimize);
                rbMinimize.Dispose();
                }
            if (this.Settings.MenuSeparator.Enabled && !rbMaximize.IsDisposed) {
                this.Controls.Remove(rbMaximize);
                rbMaximize.Dispose();
                }
            }

        public void DrawWindow(Graphics g, Rectangle r, int d, Pen p, int topWidth = 0) {
            if (firstDraw) {
                AppBrushBackground?.Dispose();
                AppBrushBackground = new SolidBrush(this.BackColor);
                firstDraw = false;
                if (this.Settings.Title.Icon != null) {
                    SetIcon(this.Settings.Title.Icon);
                    }
                pBlankWindow(g);
                }
            int t = this.Settings.Frame.BorderThickness;

            if (WindowGP == null || HasBeenResized) {
                WindowGP?.Dispose();
                WindowGP = CreateFramePath(d);

                pUpdateAlignment();
                _WindowRect = new Rectangle(0, 0, Width, Height);
                _TitleTextRect = new Rectangle(0, 0, Width, Settings.Title.Height + 4);

                }

            HasBeenResized = false;
            pBlankWindow(g);
            g.FillPath(AppBrushBackground, WindowGP);
            if (!Settings.Title.BackgroundColor.IsEmpty) {
                // Clip to the rounded outline so the color-key corner mask survives.
                GraphicsState titleState = g.Save();
                g.SetClip(WindowGP, CombineMode.Intersect);
                using (SolidBrush titleBackground = new SolidBrush(Settings.Title.BackgroundColor)) {
                    g.FillRectangle(titleBackground, 0, 0, Width, TitleDividerY);
                    }
                g.Restore(titleState);
                }
            PaintStatusDivider(g);
            if (Settings.Frame.BorderThickness > 0) {
                g.DrawPath(p, WindowGP);
                }

            if (IconImage != null && ShowIcon) g.DrawImage(
                IconImage,
                DestRect.X + (Settings.Frame.BorderThickness / 2),
                DestRect.Y + (Settings.Frame.BorderThickness / 2),
                DestRect.Width + (Settings.Frame.BorderThickness / 2),
                DestRect.Height + (Settings.Frame.BorderThickness / 2)
                );

            if (Settings.Frame.ShowSizeGrip) {
                g.DrawLine(AppPenSizeGrip, this.Width - (this.Settings.Frame.BorderThickness + 2), this.Height - 12 - t, this.Width - 12 - t, this.Height - (this.Settings.Frame.BorderThickness + 2));
                g.DrawLine(AppPenSizeGrip, this.Width - (this.Settings.Frame.BorderThickness + 2), this.Height - 16 - t, this.Width - 16 - t, this.Height - (this.Settings.Frame.BorderThickness + 2));
                g.DrawLine(AppPenSizeGrip, this.Width - (this.Settings.Frame.BorderThickness + 2), this.Height - 20 - t, this.Width - 20 - t, this.Height - (this.Settings.Frame.BorderThickness + 2));
                }

            if (Settings.MenuSeparator.Enabled && Settings.MenuSeparator.Thickness > 0) {
                int menuDividerY = TitleDividerY + TitleSettings.DefaultHeight;
                g.DrawLine(AppPenSeparator, 1, menuDividerY, ClientSize.Width - 1, menuDividerY);
                }

            if (Settings.Frame.BorderThickness > 0) {
                g.DrawLine(AppPenTitleDivider, 1, TitleDividerY, ClientSize.Width - 1, TitleDividerY);
                }


            }

        //TODO moving things around and optimizing a bit here so we aren't creating a boatload of objects on each paint event, wasting cycles
        protected override void OnPaint(PaintEventArgs e) {
            if (!Settings.Frame.ShowChrome) {
                base.OnPaint(e);
                return;
                }
            if (HasBeenResized) MoveButtons();
            if (this.BackColor != OldBackColor) {
                // A color change replaces an owned GDI brush.
                AppBrushBackground?.Dispose();
                AppBrushBackground = new SolidBrush(this.BackColor);
                OldBackColor = this.BackColor;
                }
            if (AppPenBorder.Color != this.Settings.Frame.BorderColor) AppPenBorder.Color = this.Settings.Frame.BorderColor;
            if (AppPenBorder.Width != this.Settings.Frame.BorderThickness) AppPenBorder.Width = this.Settings.Frame.BorderThickness;

            Graphics g = e.Graphics; // I HAD a using(graphics g = e.graphics){} block here BUT C# doesn't like it.. no idea why. Maybe not disposable?
            int vOffset = (24 - (int)g.MeasureString(this.Text, Settings.Title.Font).Height) / 2;

            DrawWindow(g, this.ClientRectangle, this.Settings.Frame.CornerRadius, AppPenBorder, (int)g.MeasureString(" " + this.Text + " ", Settings.Title.Font).Width);

            // Respect the public title visibility setting when painting custom chrome.
            if (Settings.Title.ShowTitle) {
                g.DrawString(this.Text, Settings.Title.Font, AppBrushTitle, _TitleTextRect, TitleTextAlignment);
                }
            // StatusText lives in the owned status strip; painting it here too
            // would duplicate text beneath hosted progress/items.

            }

        protected internal void pUpdateAlignment() {
            if (Settings.StatusBar.Alignment == Alignment.Left) StatusTextAlignment.Alignment = StringAlignment.Near;
            else if (Settings.StatusBar.Alignment == Alignment.Right) StatusTextAlignment.Alignment = StringAlignment.Far;
            else if (Settings.StatusBar.Alignment == Alignment.Center) StatusTextAlignment.Alignment = StringAlignment.Center;

            if (Settings.Title.Alignment == Alignment.Left) TitleTextAlignment.Alignment = StringAlignment.Near;
            else if (Settings.Title.Alignment == Alignment.Right) TitleTextAlignment.Alignment = StringAlignment.Far;
            else if (Settings.Title.Alignment == Alignment.Center) TitleTextAlignment.Alignment = StringAlignment.Center;
            }

        protected internal void pBlankWindow(Graphics g) {
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.FillRectangle(Brushes.Fuchsia, _WindowRect);
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            }

        #endregion drawing

        #region buttons
        private void rbExit_Click(object sender, EventArgs e) {
            if (bClose != null) {
                bClose(sender, e);
                }
            this.Close();
            }

        private void rbMinimize_Click(object sender, EventArgs e) {
            if (bMinimize != null) {
                if (bMinimize(sender, e) == false)
                    return;
                }
            this.WindowState = FormWindowState.Minimized;
            }

        private void rbMaximize_Click(object sender, EventArgs e) {
            if (!this.Settings.CaptionButtons.MaximizeBox) return;
            if (bMaximize != null) {
                if (bMaximize(sender, e) == false)
                    return;
                }

            if (this.WindowState == FormWindowState.Maximized)
                this.WindowState = FormWindowState.Normal;
            else
                this.WindowState = FormWindowState.Maximized;
            }
        #endregion buttons

        private void MWin_Resize(object sender, EventArgs e) {
            if (LastSize != this.Size) {
                HasBeenResized = true;
                LastSize = this.Size;

                }
            }


        }
    }
