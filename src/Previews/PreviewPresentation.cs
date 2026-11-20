using System.Drawing;
using System.Globalization;
using Mash.GUI.MNXArchiver.Settings;

namespace Mash.GUI.MNXArchiver.Previews;

/// <summary>The configured appearance and placement for an archive preview.</summary>
internal sealed class PreviewPresentation {
    private readonly AppSettings _settings;
    private readonly bool _centered;
    private readonly bool _relativeScreen;

    internal bool Tooltip { get; }
    internal bool Dialog { get; }
    internal bool Independent { get; }
    internal bool BesideParent { get; }
    internal string Anchor { get; }
    internal string ScaleMode { get; }

    internal PreviewPresentation(AppSettings settings) {
        _settings = settings;
        bool customized = settings.Flag("Presentation", "Customize");
        Tooltip = customized && settings.Get("Presentation", "Mode", "Window") == "Tooltip";
        Dialog = !Tooltip && (!customized || settings.Get("Presentation", "WindowType", "Dialog") == "Dialog");
        Independent = settings.IndependentWindows;
        string origin = settings.Get("Presentation", "Origin", "Center");
        BesideParent = !Tooltip && customized && origin == "Right of Main Window";
        _centered = !customized || origin == "Center";
        _relativeScreen = customized && settings.Get("Presentation", "RelativeTo", "Program Window") == "Current Screen";
        Anchor = settings.Get("Presentation", "Anchor", "File");
        ScaleMode = settings.Choice("Presentation", "ScaleMode", "Scale", "Scale", "Zoom", "Stretch", "Center");
        }

    internal Size SizeLimit(Rectangle parent, Rectangle screen) {
        int width = Math.Min(800, Math.Max(64, parent.Width * 2 / 3));
        int height = Math.Min(640, Math.Max(64, parent.Height * 2 / 3));
        if (_settings.Flag("Presentation", "LimitSize")) {
            if (_settings.Get("Presentation", "SizeMode", "Ratio") == "Pixels") {
                width = Resolution("MaxX", 800);
                height = Resolution("MaxY", 640);
                }
            else {
                double.TryParse(_settings.Get("Presentation", "Percent", "66"), CultureInfo.InvariantCulture, out double percent);
                if (!AppSettings.Percentages.Contains(percent)) {
                    percent = 66;
                    }
                Rectangle relative = _settings.Get("Presentation", "SizeRelativeTo", "Main Window") == "Current Screen"
                    ? screen : parent;
                width = (int)(relative.Width * percent / 100);
                height = (int)(relative.Height * percent / 100);
                }
            }
        width = Math.Clamp(width, 64, Math.Max(64, screen.Width - 32));
        height = Math.Clamp(height, 64, Math.Max(64, screen.Height - 64));
        return Tooltip ? new Size(Math.Min(width, parent.Width), Math.Min(height, parent.Height)) : new Size(width, height);
        }

    private int Resolution(string key, int fallback) {
        int number = _settings.Number("Presentation", key, fallback, 64, 3072);
        return AppSettings.Resolutions.Contains(number) ? number : fallback;
        }

    internal Point Position(Size size, Rectangle parent, Rectangle screen, Point file, Point cursor) {
        if (BesideParent) {
            return new Point(parent.Right, parent.Top);
            }
        Rectangle origin = _relativeScreen ? screen : parent;
        Point location = origin.Location;
        if (Tooltip) {
            location = Anchor switch {
                "Cursor" => cursor,
                "Bottom-Right" => new Point(parent.Right - size.Width - 16, parent.Bottom - size.Height - 16),
                _ => file,
                };
            }
        else if (_centered) {
            location.Offset((origin.Width - size.Width) / 2, (origin.Height - size.Height) / 2);
            }
        Rectangle limits = Tooltip ? Rectangle.Intersect(screen, parent) : screen;
        if (limits.Width <= 0 || limits.Height <= 0) {
            limits = parent;
            }
        return new Point(
            Math.Clamp(location.X, limits.Left, Math.Max(limits.Left, limits.Right - size.Width)),
            Math.Clamp(location.Y, limits.Top, Math.Max(limits.Top, limits.Bottom - size.Height))
            );
        }
    }
