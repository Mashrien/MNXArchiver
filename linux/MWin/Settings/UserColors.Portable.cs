using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing;
using SkiaSharp;
using FormsTheme = Majorsilence.Forms.Theme;

namespace MashWin.Settings {
    public static partial class UserColors {
        static partial void ApplyBackendPalette(in Theme theme, Targets targets) {
            if (!theme.ConfigureSystemColors || targets.InheritOwnerAppearance) {
                return;
                }

            Dictionary<string, Color> colors = new();
            SetColors(colors, theme.Window.Background, "Control", "Menu", "MenuBar", "Info", "AppWorkspace");
            SetColors(colors, theme.Window.Foreground, "ControlText", "MenuText", "InfoText", "CaptionText");
            SetColors(colors, theme.Files.Background, "Window");
            SetColors(colors, theme.Files.Foreground, "WindowText");
            SetColors(colors, theme.Window.Background, "ButtonFace", "ControlLight", "ControlLightLight",
                "ButtonHighlight", "ScrollBar", "InactiveCaption");
            SetColors(colors, theme.DisabledForeground, "GrayText");
            SetColors(colors, theme.SelectionBackground, "Highlight");
            SetColors(colors, theme.SelectionForeground, "HighlightText");
            SetColors(colors, theme.Accent, "HotTrack");
            SetColors(colors, theme.Window.TitleBackground, "ActiveCaption");
            SetColors(colors, theme.Window.TitleForeground, "ActiveCaptionText");
            SetColors(colors, theme.Window.Border, "ControlDark", "ControlDarkDark", "WindowFrame",
                "ActiveBorder", "InactiveBorder", "ButtonShadow");
            SystemColorPalette.SetOverrides(colors);

            Color background = theme.Window.Background;
            if (!background.IsEmpty) {
                int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
                FormsTheme.SetBuiltInTheme(brightness < 128 ? BuiltInTheme.Dark : BuiltInTheme.Light);
                }
            FormsTheme.BeginUpdate();
            try {
                FormsTheme.BackgroundColor = Skia(theme.Window.Background, FormsTheme.BackgroundColor);
                FormsTheme.ForegroundColor = Skia(theme.Window.Foreground, FormsTheme.ForegroundColor);
                FormsTheme.ForegroundDisabledColor = Skia(theme.DisabledForeground, FormsTheme.ForegroundDisabledColor);
                FormsTheme.ForegroundColorOnAccent = Skia(theme.SelectionForeground, FormsTheme.ForegroundColorOnAccent);
                FormsTheme.AccentColor = Skia(theme.Accent, FormsTheme.AccentColor);
                FormsTheme.AccentColor2 = Skia(theme.SelectionBackground, FormsTheme.AccentColor2);
                FormsTheme.TextSelectionBackgroundColor = Skia(theme.SelectionBackground, FormsTheme.TextSelectionBackgroundColor);
                FormsTheme.ControlLowColor = Skia(theme.Files.Background, FormsTheme.ControlLowColor);
                FormsTheme.ControlMidColor = Skia(theme.Window.Background, FormsTheme.ControlMidColor);
                FormsTheme.BorderLowColor = Skia(theme.Window.Border, FormsTheme.BorderLowColor);
                FormsTheme.BorderMidColor = Skia(theme.Window.Border, FormsTheme.BorderMidColor);
                }
            finally {
                FormsTheme.EndUpdate();
                }
            }

        private static void SizePortableToolbar(ToolStrip toolbar) {
            // The portable horizontal strip does not grow when padding consumes
            // its item area. Reserve the measured row plus the divider and padding.
            if (toolbar.Orientation != Orientation.Horizontal) {
                return;
                }
            int rowHeight = 0;
            foreach (ToolStripItem item in toolbar.Items) {
                int height = item.GetPreferredSize(Size.Empty).Height + item.Margin.Vertical;
                rowHeight = Math.Max(rowHeight, height);
                }
            int stripFrame = Math.Max(0, toolbar.Height - toolbar.ClientSize.Height);
            int minimumHeight = rowHeight + toolbar.Padding.Vertical + stripFrame;
            if (toolbar.Height < minimumHeight) {
                toolbar.Height = minimumHeight;
                }
            }

        private static void SetColors(Dictionary<string, Color> target, Color color, params string[] names) {
            if (color.IsEmpty) {
                return;
                }
            foreach (string name in names) {
                target.Add(name, color);
                }
            }

        private static SKColor Skia(Color color, SKColor fallback) {
            return color.IsEmpty ? fallback : new SKColor(unchecked((uint)color.ToArgb()));
            }
        }
    }
