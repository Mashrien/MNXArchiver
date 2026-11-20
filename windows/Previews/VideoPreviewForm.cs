using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Mash.GUI.MNXArchiver.Previews;

namespace Mash.GUI.MNXArchiver;

internal sealed partial class VideoPreviewForm : MnxDialog {
    private const int TimelineSteps = 1000;
    private readonly VideoPlayback _playback;
    private readonly PreviewPresentation _presentation;
    private readonly int _stride;
    private readonly Size _frameSize;
    private readonly ImageSurface _surface = new ImageSurface();
    private readonly Button _playPause = new Button();
    private readonly TrackBar _seek = new TrackBar();
    private readonly Label _position = new Label();
    private readonly Label _errorMessage = new Label();
    private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 15 };
    private byte[]? _displayPixels;
    private Bitmap? _displayImage;
    private Task? _stopping;
    private bool _hasFrame;
    private bool _updatingTimeline;
    private bool _scrubbing;
    private long _displayedSecond = -1;

    internal bool Independent { get; }
    internal long MemoryCost { get; }

    internal VideoPreviewForm(Form owner, VideoPlayback playback, string name, PreviewPresentation presentation,
        Point anchor, Point cursor) {
        _playback = playback;
        _presentation = presentation;
        Independent = presentation.Independent;
        _frameSize = new Size(playback.Info.Width, playback.Info.Height);
        _stride = checked(_frameSize.Width * 4);
        int frameBytes = checked(_stride * _frameSize.Height);
        _displayPixels = GC.AllocateUninitializedArray<byte>(frameBytes, pinned: true);
        MemoryCost = 3L * frameBytes;

        Text = "Preview — " + name;
        bool fullChrome = !presentation.Dialog && !presentation.Tooltip;
        Settings.Frame.ShowChrome = !presentation.Tooltip;
        Settings.Frame.Resizable = fullChrome;
        Settings.Frame.ShowSizeGrip = fullChrome;
        MaximizeBox = fullChrome;
        MinimizeBox = fullChrome;
        ShowInTaskbar = Independent;
        ShowIcon = fullChrome;
        if (!fullChrome) {
            Settings.Frame.BorderThickness = 0;
            Settings.Frame.CornerRadius = 0;
            }
        if (presentation.BesideParent) {
            Tag = presentation.Dialog ? "MNX.Preview.RightDialog" : "MNX.Preview.RightWindow";
            }
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;

        _surface.Dock = DockStyle.Fill;
        _surface.BackColor = Color.Black;
        _surface.Paint += PaintFrame;
        _surface.MouseDown += (_, _) => Close();
        if (presentation.Dialog) {
            Deactivate += (_, _) => Close();
            }
        _playPause.Name = "videoPlayPause";
        _playPause.Dock = DockStyle.Fill;
        _playPause.Text = "Pause";
        _playPause.Click += (_, _) => TogglePlayback();
        _seek.Name = "videoSeek";
        _seek.AccessibleName = "Seek video";
        _seek.Dock = DockStyle.Fill;
        _seek.AutoSize = false;
        _seek.TickStyle = TickStyle.None;
        _seek.Maximum = TimelineSteps;
        _seek.SmallChange = 10;
        _seek.LargeChange = 50;
        _seek.Enabled = _playback.Info.DurationMs > 0;
        _seek.ValueChanged += (_, _) => SeekFromTimeline();
        _seek.MouseDown += (_, args) => _scrubbing = args.Button == MouseButtons.Left;
        _seek.MouseUp += (_, _) => _scrubbing = false;
        _seek.MouseLeave += (_, _) => _scrubbing = false;
        _position.Name = "videoPosition";
        _position.Dock = DockStyle.Fill;
        _position.TextAlign = ContentAlignment.MiddleRight;
        _errorMessage.Dock = DockStyle.Bottom;
        _errorMessage.AutoSize = true;
        _errorMessage.Visible = false;

        TableLayoutPanel controls = new TableLayoutPanel {
            Dock = DockStyle.Bottom,
            Height = 40,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 2, 8, 2),
            Visible = !presentation.Tooltip,
            };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        controls.Controls.Add(_playPause, 0, 0);
        controls.Controls.Add(_seek, 1, 0);
        controls.Controls.Add(_position, 2, 0);
        Controls.Add(_surface);
        Controls.Add(_errorMessage);
        Controls.Add(controls);
        Rectangle screen = Screen.FromPoint(anchor).WorkingArea;
        Rectangle parent = owner.Bounds;
        Size = presentation.SizeLimit(parent, screen);
        Location = presentation.Position(Size, parent, screen, anchor, cursor);
        controls.SizeChanged += (_, _) => AdjustControls(controls);
        AdjustControls(controls);
        KeyDown += (_, args) => {
            if (args.KeyCode == Keys.Escape) {
                args.Handled = true;
                Close();
                }
            };
        _timer.Tick += (_, _) => PresentPlayback();
        Shown += (_, _) => {
            _timer.Start();
            PresentPlayback();
            };
        }

    protected override bool PreserveOwnFrameStyle => _presentation.Dialog || _presentation.Tooltip;
    protected override bool ShowWithoutActivation => _presentation.Tooltip;

    private void AdjustControls(TableLayoutPanel controls) {
        bool compact = controls.Width < 300;
        controls.ColumnStyles[0].Width = compact ? 54 : 84;
        controls.ColumnStyles[2].Width = compact ? 0 : 145;
        _position.Visible = !compact;
        }

    private void TogglePlayback() {
        if (_playback.IsPlaying) {
            _playback.Pause();
            }
        else {
            _playback.Play();
            }
        }

    private void SeekFromTimeline() {
        long duration = _playback.Info.DurationMs;
        if (!_updatingTimeline && duration > 0) {
            long target = _seek.Value == TimelineSteps ? duration : (long)(duration * (_seek.Value / (double)TimelineSteps));
            _playback.Seek(target);
            }
        }

    private void PresentPlayback() {
        if (_displayPixels is null) {
            return;
            }
        if (_playback.TryCopyFrame(_displayPixels, _stride)) {
            _hasFrame = true;
            _surface.Invalidate();
            }
        if (_playback.Error is Exception error) {
            _timer.Stop();
            _playPause.Enabled = false;
            _seek.Enabled = false;
            _errorMessage.Text = "Video playback stopped. " + error.Message;
            _errorMessage.Visible = true;
            return;
            }

        string action = _playback.IsPlaying ? "Pause" : _playback.HasEnded ? "Replay" : "Play";
        if (_playPause.Text != action) {
            _playPause.Text = action;
            }
        long duration = _playback.Info.DurationMs;
        long position = Math.Max(0, _playback.PositionMs);
        if (duration > 0) {
            position = Math.Min(position, duration);
            if (!_scrubbing) {
                _updatingTimeline = true;
                _seek.Value = (int)(position / (double)duration * TimelineSteps);
                _updatingTimeline = false;
                }
            }
        long second = position / 1000;
        if (second != _displayedSecond) {
            _displayedSecond = second;
            _position.Text = duration > 0 ? FormatTime(second) + " / " + FormatTime(duration / 1000) : FormatTime(second);
            }
        }

    private static string FormatTime(long seconds) {
        long hours = seconds / 3600;
        return hours > 0 ? $"{hours}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
        }

    partial void PaintPlatformFrame(PaintEventArgs args, RectangleF bounds, IntPtr pixels, ref bool painted);

    private unsafe void PaintFrame(object? sender, PaintEventArgs args) {
        if (!_hasFrame || _displayPixels is null) {
            return;
            }
        RectangleF bounds = PreviewForm.ImageBounds(_presentation.ScaleMode, _frameSize, _surface.ClientSize);
        if (bounds.IsEmpty) {
            return;
            }
        fixed (byte* pointer = _displayPixels) {
            IntPtr pixels = new IntPtr(pointer);
            bool painted = false;
            PaintPlatformFrame(args, bounds, pixels, ref painted);
            if (!painted) {
                _displayImage ??= new Bitmap(_frameSize.Width, _frameSize.Height, _stride, PixelFormat.Format32bppRgb, pixels);
                args.Graphics.InterpolationMode = InterpolationMode.Bilinear;
                args.Graphics.DrawImage(_displayImage, bounds);
                }
            }
        }

    internal Task StopPlayback() {
        return _stopping ??= _playback.StopAsync();
        }

    protected override void Dispose(bool disposing) {
        if (disposing && _displayPixels is not null) {
            _ = StopPlayback();
            _timer.Stop();
            _timer.Dispose();
            _displayImage?.Dispose();
            Array.Clear(_displayPixels);
            _displayPixels = null;
            }
        base.Dispose(disposing);
        }
    }
