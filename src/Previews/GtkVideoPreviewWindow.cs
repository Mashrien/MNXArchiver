using System.Globalization;
using Gtk;

namespace Mash.GUI.MNXArchiver.Previews;

internal sealed unsafe class GtkVideoPreviewWindow : IDisposable {
    private readonly Gtk.Window _window;
    private readonly VideoPlayback _playback;
    private readonly PreviewPresentation _presentation;
    private readonly Cairo.ImageSurface _image;
    private readonly DrawingArea _canvas;
    private readonly Button _play;
    private readonly Scale _seek;
    private readonly Label _position;
    private readonly Action<Exception> _reportError;
    private uint _timer;
    private bool _updatingSeek;
    private bool _scrubbing;
    private bool _closed;

    internal VideoPlayback Playback => _playback;
    internal Gtk.Window Window => _window;
    internal bool Independent => _presentation.Independent;
    internal long MemoryCost { get; }
    internal event Action<GtkVideoPreviewWindow>? Closed;

    internal GtkVideoPreviewWindow(Gtk.Window owner, string name, VideoPlayback playback, PreviewPresentation presentation,
        Gdk.Rectangle anchor, int cursorX, int cursorY, Action<Exception> reportError) {
        _playback = playback;
        _presentation = presentation;
        _reportError = reportError;
        int width = playback.Info.Width;
        int height = playback.Info.Height;
        _image = new Cairo.ImageSurface(Cairo.Format.Rgb24, width, height);
        MemoryCost = checked(3L * width * height * 4);
        GtkPreviewPlacement placement = new GtkPreviewPlacement(owner, presentation);
        _window = placement.CreateWindow(name);
        _window.DeleteEvent += (_, args) => {
            args.RetVal = true;
            Dispose();
            };
        _window.Destroyed += (_, _) => Dispose();
        if (presentation.Dialog) {
            _window.FocusOutEvent += (_, _) => Dispose();
            }
        _window.KeyPressEvent += (_, args) => {
            if (args.Event.Key == Gdk.Key.Escape) {
                args.RetVal = true;
                Dispose();
                }
            };

        Box layout = new Box(Orientation.Vertical, 0);
        if (presentation.Dialog) {
            layout.PackStart(GtkPreviewPlacement.CreateTitleBar(_window.Title, Dispose), false, false, 0);
            }
        _canvas = new DrawingArea { Hexpand = true, Vexpand = true, WidthRequest = 1, HeightRequest = 1 };
        _canvas.Drawn += DrawFrame;
        _canvas.AddEvents((int)Gdk.EventMask.ButtonPressMask);
        _canvas.ButtonPressEvent += (_, _) => Dispose();
        layout.PackStart(_canvas, true, true, 0);
        System.Drawing.Size size = placement.SizeLimit;
        bool compact = size.Width < 300;
        Box controls = new Box(Orientation.Horizontal, compact ? 2 : 8) {
            BorderWidth = compact ? 2U : 8U,
            NoShowAll = presentation.Tooltip,
            };
        _play = new Button("Pause");
        _play.Clicked += TogglePlayback;
        controls.PackStart(_play, false, false, 0);
        _seek = new Scale(Orientation.Horizontal, 0, Math.Max(1, playback.Info.DurationMs), 1) {
            DrawValue = false,
            Sensitive = playback.Info.DurationMs > 0,
            Hexpand = true
            };
        _seek.AddEvents((int)(Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask));
        _seek.ButtonPressEvent += BeginScrubbing;
        _seek.WidgetEventAfter += EndScrubbing;
        _seek.ValueChanged += SeekChanged;
        controls.PackStart(_seek, true, true, 0);
        _position = new Label(PositionText()) { NoShowAll = compact };
        controls.PackEnd(_position, false, false, 0);
        layout.PackEnd(controls, false, false, 0);
        _window.Add(layout);
        layout.ShowAll();
        placement.Place(_window, size, anchor, cursorX, cursorY);
        }

    internal void Show() {
        _window.ShowAll();
        _timer = GLib.Timeout.Add(16, Poll);
        }

    private void TogglePlayback(object? sender, EventArgs args) {
        if (_playback.IsPlaying) {
            _playback.Pause();
            }
        else {
            _playback.Play();
            }
        }

    [GLib.ConnectBefore]
    private void BeginScrubbing(object sender, ButtonPressEventArgs args) {
        if (args.Event.Button == 1) {
            _scrubbing = true;
            }
        }

    private void EndScrubbing(object sender, WidgetEventAfterArgs args) {
        if (_scrubbing && args.Event is Gdk.EventButton button && button.Type == Gdk.EventType.ButtonRelease) {
            _scrubbing = false;
            _playback.Seek((long)_seek.Value);
            }
        }

    private void SeekChanged(object? sender, EventArgs args) {
        if (!_updatingSeek && !_scrubbing) {
            _playback.Seek((long)_seek.Value);
            }
        }

    private bool Poll() {
        if (_closed) {
            return false;
            }
        if (_playback.Error is Exception error) {
            _timer = 0;
            _reportError(error);
            Dispose();
            return false;
            }

        _image.Flush();
        if (_playback.TryCopyFrame(new Span<byte>((void*)_image.DataPtr, _image.Stride * _image.Height), _image.Stride)) {
            _image.MarkDirty();
            _canvas.QueueDraw();
            }

        string buttonText = _playback.IsPlaying ? "Pause" : "Play";
        if (_play.Label != buttonText) {
            _play.Label = buttonText;
            }
        if (!_scrubbing) {
            _updatingSeek = true;
            _seek.Value = _playback.PositionMs;
            _updatingSeek = false;
            }
        string positionText = PositionText();
        if (_position.Text != positionText) {
            _position.Text = positionText;
            }
        return true;
        }

    private string PositionText() {
        string elapsed = FormatTime(_playback.PositionMs);
        if (_playback.Info.DurationMs < 0) {
            return elapsed;
            }
        return elapsed + " / " + FormatTime(_playback.Info.DurationMs);
        }

    private static string FormatTime(long milliseconds) {
        long seconds = milliseconds / 1000;
        long hours = seconds / 3600;
        long minutes = (seconds / 60) % 60;
        long remaining = seconds % 60;
        if (hours > 0) {
            return string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{remaining:00}");
            }
        return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{remaining:00}");
        }

    private void DrawFrame(object sender, DrawnArgs args) {
        Cairo.Context drawing = args.Cr;
        drawing.SetSourceRGB(0, 0, 0);
        drawing.Paint();
        int width = _canvas.AllocatedWidth;
        int height = _canvas.AllocatedHeight;
        (double x, double y, double imageWidth, double imageHeight) =
            PreviewWindow.ImageBounds(_presentation.ScaleMode, _image.Width, _image.Height, width, height);
        if (imageWidth > 0 && imageHeight > 0) {
            drawing.Save();
            drawing.Rectangle(0, 0, width, height);
            drawing.Clip();
            drawing.Translate(x, y);
            drawing.Scale(imageWidth / _image.Width, imageHeight / _image.Height);
            drawing.SetSourceSurface(_image, 0, 0);
            drawing.Paint();
            drawing.Restore();
            }
        args.RetVal = true;
        }

    public void Dispose() {
        if (_closed) {
            return;
            }
        _closed = true;
        if (_timer != 0) {
            GLib.Source.Remove(_timer);
            _timer = 0;
            }
        _playback.StopAsync().GetAwaiter().GetResult();
        _image.Dispose();
        _window.Destroy();
        Closed?.Invoke(this);
        }
    }
