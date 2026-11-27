using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
#if MWIN_PORTABLE
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using SystemColors = Majorsilence.Forms.SystemColors;
#else
using System.Windows.Forms;
#endif

namespace MashWin.Settings {
    /// <summary>Applies a caller-supplied theme to a window and its named control groups.</summary>
    public static partial class UserColors {
        public struct Theme {
            public WindowTheme Window { get; set; }
            public ToolbarTheme Buttons { get; set; }
            public TextTheme Text { get; set; }
            public BrowserTheme Files { get; set; }
            public MenuTheme Menus { get; set; }
            public Color Accent { get; set; }
            public Color SelectionBackground { get; set; }
            public Color SelectionForeground { get; set; }
            public Color DisabledForeground { get; set; }
            public bool ConfigureSystemColors { get; set; }
            }

        public struct WindowTheme {
            public Color Border { get; set; }
            public Color Background { get; set; }
            public Color Divider { get; set; }
            public Color Foreground { get; set; }
            public Color TitleBackground { get; set; }
            public Color TitleForeground { get; set; }
            public FontSpec Font { get; set; }
            public FontSpec TitleFont { get; set; }
            public int? DividerMargin { get; set; }
            public int? TitleHeight { get; set; }
            public int? StatusHeight { get; set; }
            public CaptionTheme MinMax { get; set; }
            public CaptionTheme Exit { get; set; }
            }

        public struct CaptionTheme {
            public Color Background { get; set; }
            public Color Foreground { get; set; }
            public Color Highlight { get; set; }
            public Color Pressed { get; set; }
            public Color HighlightOuter { get; set; }
            public Color PressedOuter { get; set; }
            }

        public readonly struct FontSpec {
            public string Family { get; }
            public float Size { get; }

            public FontSpec(string family, float size) {
                Family = family;
                Size = size;
                }
            }

        public struct ToolbarTheme {
            public FontSpec Font { get; set; }
            public Color Background { get; set; }
            public Color Foreground { get; set; }
            public Color Border { get; set; }
            public int? BorderThickness { get; set; }
            public int? CornerRadius { get; set; }
            public ExtendedButtonSettings.RoundedCorners? RoundedCorners { get; set; }
            }

        public struct TextTheme {
            public FontSpec Font { get; set; }
            public Color Foreground { get; set; }
            }

        public struct BrowserTheme {
            public FontSpec Font { get; set; }
            public FontSpec HeaderFont { get; set; }
            public Color Background { get; set; }
            public Color Foreground { get; set; }
            public Color HeaderBackground { get; set; }
            public Color HeaderHighlight { get; set; }
            public Color HeaderForeground { get; set; }
            }

        public struct MenuTheme {
            public Color BarBackground { get; set; }
            public Color Background { get; set; }
            public Color Foreground { get; set; }
            public Color DisabledForeground { get; set; }
            public Color Highlight { get; set; }
            public Color HighlightForeground { get; set; }
            public Color Pressed { get; set; }
            public Color PressedForeground { get; set; }
            public Color Checked { get; set; }
            public Color CheckedForeground { get; set; }
            public Color ImageMargin { get; set; }
            public Color Border { get; set; }
            public Color Separator { get; set; }
            }

        /// <summary>Controls that opt into each group. All controls in Navigation use Text.</summary>
        public struct Targets {
            public ToolStrip Toolbar { get; set; }
            public TableLayoutPanel Navigation { get; set; }
            public Control[] NavigationLabels { get; set; }
            public ToolStrip[] Menus { get; set; }
            public ListView FileList { get; set; }
            public TreeView FolderTree { get; set; }
            public bool StatusDivider { get; set; }
            public bool InheritOwnerAppearance { get; set; }
            public bool PreserveFrameGeometry { get; set; }
            }

        private static readonly ConditionalWeakTable<MWindow, WindowThemeBinding> Bindings = new();

        /// <summary>Applies colors, fonts, and metrics on the window's UI thread.</summary>
        public static void Apply(in Theme theme, MWindow window, Targets targets = default) {
            ArgumentNullException.ThrowIfNull(window);
            ObjectDisposedException.ThrowIf(window.IsDisposed, window);
            WindowThemeBinding binding = Bindings.GetValue(window, owner => new WindowThemeBinding(owner));
            ApplyBackendPalette(theme, targets);
            window.SuspendLayout();
            try {
                if (targets.InheritOwnerAppearance) {
                    binding.InheritOwnerAppearance(targets.PreserveFrameGeometry);
                    }
                binding.ApplyWindow(theme.Window, targets.StatusDivider);
                ApplyMenus(window.Settings.ToolStrips, theme.Menus, theme.Text.Foreground);
                binding.ApplyControls(theme, targets);
                }
            finally {
                window.ResumeLayout(true);
                }
            window.Invalidate(true);
            }

        private static Color Use(Color configured, Color fallback) {
            return configured.IsEmpty ? fallback : configured;
            }

        private static void ApplyMenus(ToolStripSettings settings, MenuTheme theme, Color text) {
            settings.BackgroundColor = Use(theme.BarBackground, settings.BackgroundColor);
            settings.MenuBackgroundColor = Use(theme.Background, settings.MenuBackgroundColor);
            settings.TextColor = Use(text, Use(theme.Foreground, settings.TextColor));
            settings.DisabledTextColor = Use(theme.DisabledForeground, settings.DisabledTextColor);
            settings.BorderColor = Use(theme.Border, settings.BorderColor);
            settings.HoverColor = Use(theme.Highlight, settings.HoverColor);
            settings.HoverTextColor = Use(theme.HighlightForeground, settings.HoverTextColor);
            settings.PressedColor = Use(theme.Pressed, settings.PressedColor);
            settings.PressedTextColor = Use(theme.PressedForeground, settings.PressedTextColor);
            settings.CheckedColor = Use(theme.Checked, settings.CheckedColor);
            settings.CheckedTextColor = Use(theme.CheckedForeground, settings.CheckedTextColor);
            if (!theme.ImageMargin.IsEmpty) {
                settings.ImageMarginColor = theme.ImageMargin;
                }
            if (!theme.Separator.IsEmpty) {
                settings.SeparatorColor = theme.Separator;
                }
            }

        static partial void ApplyBackendPalette(in Theme theme, Targets targets);

        private sealed class WindowThemeBinding {
            private readonly MWindow _owner;
            private readonly Dictionary<(string Family, float Size, FontStyle Style, bool Caption), Font> _fonts = new();
            private readonly Dictionary<TableLayoutPanel, NavigationLayout> _navigation = new();

            internal WindowThemeBinding(MWindow owner) {
                _owner = owner;
                _owner.Disposed += Release;
                }

            private void Release(object sender, EventArgs arguments) {
                foreach (Font font in _fonts.Values) {
                    font.Dispose();
                    }
                foreach (NavigationLayout layout in _navigation.Values) {
                    layout.Dispose();
                    }
                _fonts.Clear();
                _navigation.Clear();
                }

            private Font StyledFont(FontSpec specification, Font fallback, bool caption = false) {
                if (string.IsNullOrWhiteSpace(specification.Family) && specification.Size <= 0) {
                    return fallback;
                    }
                string family = string.IsNullOrWhiteSpace(specification.Family) ? fallback.Name : specification.Family;
                float size = float.IsFinite(specification.Size) && specification.Size > 0 ? specification.Size : fallback.SizeInPoints;
                (string Family, float Size, FontStyle Style, bool Caption) key = (family, size, fallback.Style, caption);
                if (!_fonts.TryGetValue(key, out Font font)) {
                    font = caption ? TitleSettings.CreateCaptionFont(family, size) : new Font(family, size, fallback.Style);
                    _fonts.Add(key, font);
                    }
                return font;
                }

            internal void InheritOwnerAppearance(bool preserveFrameGeometry) {
                if (_owner.Owner is not MWindow parent) {
                    return;
                    }
                _owner.Settings.Frame.BorderColor = parent.Settings.Frame.BorderColor;
                if (!preserveFrameGeometry) {
                    _owner.Settings.Frame.BorderThickness = parent.Settings.Frame.BorderThickness;
                    _owner.Settings.Frame.CornerRadius = parent.Settings.Frame.CornerRadius;
                    }
                _owner.Settings.Title.Height = parent.Settings.Title.Height;
                _owner.Settings.Title.Font = parent.Settings.Title.Font;
                _owner.Settings.Title.Alignment = parent.Settings.Title.Alignment;
                _owner.Settings.ToolStrips.BackgroundColor = parent.Settings.ToolStrips.BackgroundColor;
                _owner.Settings.ToolStrips.TextColor = parent.Settings.ToolStrips.TextColor;
                }

            internal void ApplyWindow(WindowTheme theme, bool statusDivider) {
                MWindowSettings settings = _owner.Settings;
                _owner.BackColor = Use(theme.Background, _owner.BackColor);
                _owner.ForeColor = Use(theme.Foreground, _owner.ForeColor);
                _owner.Font = StyledFont(theme.Font, _owner.Font);
                settings.Frame.BorderColor = Use(theme.Border, settings.Frame.BorderColor);
                if (!theme.Divider.IsEmpty) {
                    settings.MenuSeparator.Color = theme.Divider;
                    }
                else if (!theme.Border.IsEmpty) {
                    settings.MenuSeparator.Color = theme.Border;
                    }
                if (statusDivider) {
                    settings.StatusBar.ShowSeparator = true;
                    }
                if (theme.TitleHeight is int titleHeight) {
                    settings.Title.Height = titleHeight;
                    }
                if (theme.StatusHeight is int statusHeight) {
                    settings.StatusBar.Height = statusHeight;
                    }
                settings.Title.BackgroundColor = Use(theme.TitleBackground, settings.Title.BackgroundColor);
                settings.Title.TextColor = Use(theme.TitleForeground, settings.Title.TextColor);
                settings.Title.Font = StyledFont(theme.TitleFont, settings.Title.Font, caption: true);
                settings.StatusBar.Font = StyledFont(theme.Font, settings.StatusBar.Font);
                ApplyCaption(settings.CaptionButtons.MinMax, theme.MinMax);
                ApplyCaption(settings.CaptionButtons.Exit, theme.Exit);
                }

            private static void ApplyCaption(CaptionButtonColors settings, CaptionTheme theme) {
                settings.Background = Use(theme.Background, settings.Background);
                settings.Foreground = Use(theme.Foreground, settings.Foreground);
                if (!theme.Highlight.IsEmpty) {
                    settings.Highlight = theme.Highlight;
                    }
                if (!theme.Pressed.IsEmpty) {
                    settings.Pressed = theme.Pressed;
                    }
                if (settings is ExitCaptionButtonColors exit) {
                    if (!theme.Highlight.IsEmpty || !theme.HighlightOuter.IsEmpty) {
                        exit.HighlightOuter = theme.HighlightOuter;
                        }
                    if (!theme.Pressed.IsEmpty || !theme.PressedOuter.IsEmpty) {
                        exit.PressedOuter = theme.PressedOuter;
                        }
                    }
                }

            internal void ApplyControls(in Theme theme, Targets targets) {
                Color divider = Use(theme.Window.Divider, _owner.Settings.Frame.BorderColor);
                int thickness = Math.Max(0, _owner.Settings.MenuSeparator.Thickness);
                int margin = Math.Clamp(theme.Window.DividerMargin ?? 6, 0, 48);
                ApplyToolbar(targets.Toolbar, theme.Buttons, divider, margin, thickness);
                ApplyText(targets, theme.Text, divider, margin, thickness);
                ApplyBrowser(targets, theme.Files);
                }

            private void ApplyToolbar(ToolStrip toolbar, ToolbarTheme theme, Color divider, int margin, int thickness) {
                if (toolbar == null) {
                    return;
                    }
                ToolStripSettings settings = _owner.Settings.ToolStrips;
                toolbar.Font = StyledFont(theme.Font, toolbar.Font);
                if (toolbar.Renderer is MToolStripRenderer renderer) {
                    renderer.ConfigureToolbar(toolbar,
                        Use(theme.Background, settings.BackgroundColor),
                        Use(theme.Foreground, settings.TextColor),
                        Use(theme.Border, _owner.Settings.Frame.BorderColor),
                        Math.Clamp(theme.BorderThickness ?? settings.BorderThickness, 0, 8),
                        Math.Clamp(theme.CornerRadius ?? settings.CornerRadius, 0, 64),
                        divider, thickness, theme.RoundedCorners ?? ExtendedButtonSettings.RoundedCorners.All);
                    }
                toolbar.Padding = new Padding(8 + margin, 4 + margin, 8 + margin, 4 + margin + thickness);
#if MWIN_PORTABLE
                SizePortableToolbar(toolbar);
#endif
                }

            private void ApplyText(Targets targets, TextTheme theme, Color divider, int margin, int thickness) {
                if (targets.Menus != null) {
                    foreach (ToolStrip menu in targets.Menus) {
                        menu.Font = StyledFont(theme.Font, menu.Font);
                        menu.ForeColor = Use(theme.Foreground, menu.ForeColor);
                        }
                    }
                TableLayoutPanel panel = targets.Navigation;
                if (panel == null) {
                    return;
                    }
                int controlHeight = 28;
                foreach (Control control in panel.Controls) {
                    control.Font = StyledFont(theme.Font, control.Font);
                    control.ForeColor = Use(theme.Foreground, control.ForeColor);
                    controlHeight = Math.Max(controlHeight, control.Font.Height + 10);
                    }
                if (!_navigation.TryGetValue(panel, out NavigationLayout layout)) {
                    layout = new NavigationLayout(panel);
                    _navigation.Add(panel, layout);
                    }
                layout.Apply(targets.NavigationLabels, divider, margin, thickness, controlHeight);
                }

            private void ApplyBrowser(Targets targets, BrowserTheme theme) {
                if (targets.FolderTree is TreeView tree) {
                    tree.Font = StyledFont(theme.Font, tree.Font);
                    tree.BackColor = Use(theme.Background, tree.BackColor);
                    tree.ForeColor = Use(theme.Foreground, tree.ForeColor);
                    }
                if (targets.FileList is ListView list) {
                    list.Font = StyledFont(theme.Font, list.Font);
                    list.BackColor = Use(theme.Background, list.BackColor);
                    list.ForeColor = Use(theme.Foreground, list.ForeColor);
                    if (!theme.HeaderBackground.IsEmpty || !theme.HeaderHighlight.IsEmpty ||
                        !theme.HeaderForeground.IsEmpty || !string.IsNullOrEmpty(theme.HeaderFont.Family) ||
                        theme.HeaderFont.Size > 0) {
                        FileHeaderAppearance.Apply(list, Use(theme.HeaderBackground, SystemColors.Control),
                            Use(theme.HeaderHighlight, SystemColors.Highlight),
                            StyledFont(theme.HeaderFont, list.Font), Use(theme.HeaderForeground, SystemColors.ControlText));
                        }
                    }
                }
            }

        private sealed class NavigationLayout : IDisposable {
            private readonly TableLayoutPanel _panel;
            private readonly Dictionary<int, float> _columnWidths = new();
            private Color _divider;
            private int _thickness;

            internal NavigationLayout(TableLayoutPanel panel) {
                _panel = panel;
                _panel.Paint += PaintDivider;
                }

            internal void Apply(Control[] labels, Color divider, int margin, int thickness, int controlHeight) {
                _divider = divider;
                _thickness = thickness;
                if (labels != null) {
                    foreach (Control label in labels) {
                        FitLabel(label);
                        }
                    }
                _panel.Padding = new Padding(margin, 2 + margin, margin, 2 + margin + thickness);
                _panel.Height = controlHeight + _panel.Padding.Vertical;
                _panel.Invalidate();
                }

            private void FitLabel(Control label) {
                int column = _panel.GetColumn(label);
                if (label.Parent != _panel || column < 0 || column >= _panel.ColumnStyles.Count) {
                    return;
                    }
                ColumnStyle style = _panel.ColumnStyles[column];
                if (style.SizeType != SizeType.Absolute) {
                    return;
                    }
                if (!_columnWidths.TryGetValue(column, out float baseline)) {
                    baseline = style.Width;
                    _columnWidths.Add(column, baseline);
                    }
                style.Width = Math.Max(baseline, TextRenderer.MeasureText(label.Text, label.Font).Width + 6);
                }

            private void PaintDivider(object sender, PaintEventArgs arguments) {
                if (_thickness > 0) {
                    using SolidBrush brush = new SolidBrush(_divider);
                    arguments.Graphics.FillRectangle(brush, 0, _panel.ClientSize.Height - _thickness,
                        _panel.ClientSize.Width, _thickness);
                    }
                }

            public void Dispose() {
                _panel.Paint -= PaintDivider;
                }
            }
        }
    }
