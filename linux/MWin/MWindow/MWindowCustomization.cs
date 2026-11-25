using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Drawing;
using System.Runtime.CompilerServices;

namespace MashWin {
    // Every group owns its values and reports edits to its containing settings.
    [TypeConverter(typeof(MashWindowSettingsConverter))]
    public abstract class CustomizationGroup : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetValue<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) {
                return;
                }
            field = value;
            OnPropertyChanged(propertyName);
            }

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    public sealed class FrameSettings : CustomizationGroup {
        private bool resizable = true;
        private bool showChrome = true;

        [DefaultValue(true)]
        [NotifyParentProperty(true)]
        public bool ShowChrome {
            get { return showChrome; }
            set { SetValue(ref showChrome, value); }
            }

        private Color borderColor = Color.Gray;
        private int borderThickness = 1;
        private int cornerRadius = 5;
        private bool showSizeGrip = false;

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool Resizable {
            get {
                return resizable;
                }
            set {
                SetValue(ref resizable, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(typeof(Color), "Gray")]
        public Color BorderColor {
            get {
                return borderColor;
                }
            set {
                SetValue(ref borderColor, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(1)]
        public int BorderThickness {
            get {
                return borderThickness;
                }
            set {
                SetValue(ref borderThickness, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(5)]
        public int CornerRadius {
            get {
                return cornerRadius;
                }
            set {
                SetValue(ref cornerRadius, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(false)]
        public bool ShowSizeGrip {
            get {
                return showSizeGrip;
                }
            set {
                SetValue(ref showSizeGrip, value);
                }
            }
        }

    public sealed class CaptionButtonSettings : CustomizationGroup {
        private bool exitBox = true;
        private bool maximizeBox = true;
        private bool minimizeBox = true;

        public CaptionButtonSettings() {
            MinMax.PropertyChanged += (sender, arguments) => OnPropertyChanged(nameof(MinMax) + "." + arguments.PropertyName);
            Exit.PropertyChanged += (sender, arguments) => OnPropertyChanged(nameof(Exit) + "." + arguments.PropertyName);
            }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [NotifyParentProperty(true)]
        public CaptionButtonColors MinMax { get; } = new CaptionButtonColors(Color.LightBlue, Color.CornflowerBlue);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [NotifyParentProperty(true)]
        public ExitCaptionButtonColors Exit { get; } = new ExitCaptionButtonColors();

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool ExitBox {
            get {
                return exitBox;
                }
            set {
                SetValue(ref exitBox, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool MaximizeBox {
            get {
                return maximizeBox;
                }
            set {
                SetValue(ref maximizeBox, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool MinimizeBox {
            get {
                return minimizeBox;
                }
            set {
                SetValue(ref minimizeBox, value);
                }
            }
        }

    public class CaptionButtonColors : CustomizationGroup {
        private readonly Color defaultHighlight;
        private readonly Color defaultPressed;
        private Color background = Color.Empty;
        private Color foreground = Color.LightSlateGray;
        private Color highlight;
        private Color pressed;

        internal CaptionButtonColors(Color defaultHighlight, Color defaultPressed) {
            this.defaultHighlight = defaultHighlight;
            this.defaultPressed = defaultPressed;
            highlight = defaultHighlight;
            pressed = defaultPressed;
            }

        [DefaultValue(typeof(Color), "")]
        [NotifyParentProperty(true)]
        public Color Background {
            get { return background; }
            set { SetValue(ref background, value); }
            }

        [DefaultValue(typeof(Color), "LightSlateGray")]
        [NotifyParentProperty(true)]
        public Color Foreground {
            get { return foreground; }
            set { SetValue(ref foreground, value); }
            }

        [NotifyParentProperty(true)]
        public Color Highlight {
            get { return highlight; }
            set { SetValue(ref highlight, value); }
            }

        [NotifyParentProperty(true)]
        public Color Pressed {
            get { return pressed; }
            set { SetValue(ref pressed, value); }
            }

        public void ResetHighlight() {
            Highlight = defaultHighlight;
            }

        public void ResetPressed() {
            Pressed = defaultPressed;
            }

        private bool ShouldSerializeHighlight() {
            return Highlight != defaultHighlight;
            }

        private bool ShouldSerializePressed() {
            return Pressed != defaultPressed;
            }

        // Keep the original gradient for untouched minimize/maximize colors.
        internal virtual Color HighlightOuterColor => highlight == defaultHighlight ? Color.Transparent : Color.Empty;
        internal virtual Color PressedOuterColor => pressed == defaultPressed ? Color.Black : Color.Empty;
        }

    public sealed class ExitCaptionButtonColors : CaptionButtonColors {
        private Color highlightOuter = Color.Transparent;
        private Color pressedOuter = Color.Black;

        internal ExitCaptionButtonColors() : base(Color.PaleVioletRed, Color.Red) {
            }

        [DefaultValue(typeof(Color), "Transparent")]
        [Description("Outer color of the highlight gradient. Empty uses a solid fill.")]
        [NotifyParentProperty(true)]
        public Color HighlightOuter {
            get { return highlightOuter; }
            set { SetValue(ref highlightOuter, value); }
            }

        [DefaultValue(typeof(Color), "Black")]
        [Description("Outer color of the pressed gradient. Empty uses a solid fill.")]
        [NotifyParentProperty(true)]
        public Color PressedOuter {
            get { return pressedOuter; }
            set { SetValue(ref pressedOuter, value); }
            }

        internal override Color HighlightOuterColor => highlightOuter;
        internal override Color PressedOuterColor => pressedOuter;
        }

    public sealed class TitleSettings : CustomizationGroup {
        internal const int DefaultHeight = 35;

        private int height = DefaultHeight;
        private Color textColor = Color.FromArgb(190, Color.DarkCyan);
        private Color backgroundColor = Color.Empty;

        [DefaultValue(DefaultHeight)]
        [NotifyParentProperty(true)]
        public int Height {
            get { return height; }
            set { SetValue(ref height, Math.Max(18, Math.Min(256, value))); }
            }

        // Empty preserves the form background; a desktop theme can supply chrome color.
        [NotifyParentProperty(true)]
        public Color BackgroundColor {
            get { return backgroundColor; }
            set { SetValue(ref backgroundColor, value); }
            }


        // The title brush must be configurable for dark desktop palettes too.
        [NotifyParentProperty(true)]
        public Color TextColor {
            get { return textColor; }
            set { SetValue(ref textColor, value); }
            }

        private Image icon;
        internal static Font CreateCaptionFont(string family, float points) {
            return new Font(family, points) { Weight = SkiaSharp.SKFontStyleWeight.SemiBold };
            }

        private Font font = CreateCaptionFont("Segoe UI", 12f);
        private bool showTitle = true;
        private Alignment alignment = Alignment.Left;

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(null)]
        public Image Icon {
            get {
                return icon;
                }
            set {
                SetValue(ref icon, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]

        public Font Font {
            get {
                return font;
                }
            set {
                SetValue(ref font, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool ShowTitle {
            get {
                return showTitle;
                }
            set {
                SetValue(ref showTitle, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(MashWin.Alignment.Left)]
        public Alignment Alignment {
            get {
                return alignment;
                }
            set {
                SetValue(ref alignment, value);
                }
            }
        }

    public sealed class MenuSeparatorSettings : CustomizationGroup {
        private bool enabled = true;
        private Color color = Color.Empty;
        private int thickness = 1;

        internal Color TitleDividerColor(Color frameColor) {
            return color.IsEmpty ? frameColor : color;
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(true)]
        public bool Enabled {
            get {
                return enabled;
                }
            set {
                SetValue(ref enabled, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(typeof(Color), "Gray")]
        public Color Color {
            get {
                return color.IsEmpty ? Color.Gray : color;
                }
            set {
                SetValue(ref color, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(1)]
        public int Thickness {
            get {
                return thickness;
                }
            set {
                SetValue(ref thickness, value);
                }
            }
        }

    public sealed class StatusBarSettings : CustomizationGroup {
        private Font font = new Font("Segoe UI", 9f);
        private string text = "";
        private Alignment alignment = Alignment.Left;
        private bool showSeparator = false;
        private bool visible = true;
        private int height = 24;
        private int horizontalPadding = 4;

        [DefaultValue(true)]
        [NotifyParentProperty(true)]
        public bool Visible {
            get { return visible; }
            set { SetValue(ref visible, value); }
            }

        [DefaultValue(24)]
        [NotifyParentProperty(true)]
        public int Height {
            get { return height; }
            set { SetValue(ref height, Math.Max(18, Math.Min(256, value))); }
            }

        [DefaultValue(4)]
        [NotifyParentProperty(true)]
        public int HorizontalPadding {
            get { return horizontalPadding; }
            set { SetValue(ref horizontalPadding, Math.Max(0, Math.Min(64, value))); }
            }


        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]

        public Font Font {
            get {
                return font;
                }
            set {
                SetValue(ref font, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue("")]
        public string Text {
            get {
                return text;
                }
            set {
                SetValue(ref text, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(MashWin.Alignment.Left)]
        public Alignment Alignment {
            get {
                return alignment;
                }
            set {
                SetValue(ref alignment, value);
                }
            }

        [Browsable(true)]
        [NotifyParentProperty(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DefaultValue(false)]
        public bool ShowSeparator {
            get {
                return showSeparator;
                }
            set {
                SetValue(ref showSeparator, value);
                }
            }
        }
    }
