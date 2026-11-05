using System.Drawing.Drawing2D;
using System.Globalization;
using Mash.UI.MNXFile.GUI.Previews;
using Mash.UI.MNXFile.GUI.Settings;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Owns preview windows and their bounded plaintext snapshots.</summary>
public sealed partial class PreviewForm : MnxDialog {
    private readonly PreviewData? _data;
    private readonly string _scaleMode = "Scale";
    private readonly bool _hoverTooltip;
    private readonly bool _dialog;
    private MemoryStream? _imageStream;
    private Image? _image;
    private CachedImageDecoder? _cachedImage;
    private readonly List<Bitmap> _cachedFrames = new List<Bitmap>();
    private System.Windows.Forms.Timer? _cachedTimer;
    private int _cachedFrameIndex;
    private int _cachedPlays;
    private ImageSurface? _surface;
    private EventHandler? _animationCallback;
    private Font? _textFont;
    private bool _released;
    internal bool Independent { get; }
    internal bool IsTooltip { get; }
    internal long MemoryCost { get; }
    internal Size? ImageSize => _image?.Size;
    internal bool IsAnimating => !_released && (_animationCallback is not null || _cachedTimer?.Enabled == true);

    public PreviewForm() {
        InitializeComponent();
        closePreviewButton.Click += (_, _) => Close();
        }

    internal PreviewForm(Form owner, PreviewData data, string name, AppSettings settings, Point anchor, bool hover, ImagePreflight.ImageInfo? imageInfo, long memoryCost) : this() {
        _data = data;
        MemoryCost = memoryCost;
        Independent = settings.IndependentWindows;
        bool customized = settings.Flag("Presentation", "Customize");
        IsTooltip = customized && settings.Get("Presentation", "Mode", "Window") == "Tooltip";
        _hoverTooltip = IsTooltip && hover;
        _dialog = !IsTooltip && (!customized || settings.Get("Presentation", "WindowType", "Dialog") == "Dialog");
        _scaleMode = settings.Choice("Presentation", "ScaleMode", "Scale", "Scale", "Zoom", "Stretch", "Center");
        Text = "Preview — " + name;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = Independent;
        FormBorderStyle = FormBorderStyle.None;
        Settings.Frame.ShowChrome = !IsTooltip;
        Settings.Frame.Resizable = !_dialog && !IsTooltip;
        Settings.Frame.ShowSizeGrip = !_dialog && !IsTooltip;
        MinimizeBox = !_dialog && !IsTooltip;
        MaximizeBox = !_dialog && !IsTooltip;
        KeyPreview = true;
        Padding = new Padding(IsTooltip ? 2 : _dialog ? 0 : 8);
        if (_dialog) {
            Settings.Frame.BorderThickness = 0;
            Settings.Frame.CornerRadius = 0;
            ShowIcon = false;
            }
        if (!IsTooltip && customized && settings.Get("Presentation", "Origin", "Center") == "Right of Main Window") {
            // The GTK4 adapter uses a parent-relative surface for this owned dialog.
            Tag = _dialog ? "MNX.Preview.RightDialog" : "MNX.Preview.RightWindow";
            }
        BackColor = SystemColors.Window;
        KeyDown += (_, args) => {
            if (args.KeyCode == Keys.Escape) {
                args.Handled = true;
                Close();
                }
            };
        if (_dialog) {
            Deactivate += (_, _) => Close();
            }
        try {
            if (data.Kind == PreviewKind.Text) {
                _textFont = new Font(FontFamily.GenericMonospace, 10);
                PreviewTextBox text = textPreview;
                text.Visible = true;
                imageSurface.Visible = false;
                text.Font = _textFont;
                text.Text = data.Text();
                text.PassThrough = _hoverTooltip;
                if (_dialog || IsTooltip) {
                    text.MouseDown += (_, _) => Close();
                    }
                }
            else {
                if (!imageInfo.HasValue) {
                    throw new IOException("The image has not passed header validation.");
                    }
                if (CachedImageDecoder.IsGif(data.Bytes) || settings.HalfResolutionPreviews) {
                    try {
                        LoadCachedImage(data, imageInfo.Value, settings.DecodedPreviewBytes);
                        }
                    catch (NotSupportedException) when (!CachedImageDecoder.IsGif(data.Bytes)) {
                        // Keep native-only static formats (such as TIFF) available.
                        // Preflight already counted the full-size temporary raster.
                        _imageStream = new MemoryStream(data.Bytes, false);
                        using Image source = Image.FromStream(_imageStream, false, true);
                        Bitmap reduced = new Bitmap(imageInfo.Value.Width, imageInfo.Value.Height);
                        _image = reduced;
                        using Graphics drawing = Graphics.FromImage(reduced);
                        drawing.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        drawing.DrawImage(source, new Rectangle(0, 0, reduced.Width, reduced.Height));
                        }
                    }
                else {
                    _imageStream = new MemoryStream(data.Bytes, false);
                    _image = Image.FromStream(_imageStream, false, true);
                    }
                if (_image is null || _image.Width <= 0 || _image.Height <= 0 || _image.Width > imageInfo.Value.Width || _image.Height > imageInfo.Value.Height) {
                    throw new IOException("The decoded image dimensions disagree with its validated header.");
                    }
                _surface = imageSurface;
                textPreview.Visible = false;
                imageSurface.Visible = true;
                _surface.PassThrough = _hoverTooltip;
                _surface.Paint += PaintImage;
                _surface.MouseDown += (_, _) => Close();
                if (_cachedImage is null && imageInfo.Value.Frames > 1 && ImageAnimator.CanAnimate(_image)) {
                    _animationCallback = (_, _) => {
                        if (_released || IsDisposed || !IsHandleCreated) {
                            return;
                            }
                        try {
                            BeginInvoke(new Action(() => {
                                if (!_released) {
                                    _surface?.Invalidate();
                                    }
                                }));
                            }
                        catch (InvalidOperationException) {
                            // A pending animation frame can race the UI window closing.
                            }
                        };
                    ImageAnimator.Animate(_image, _animationCallback);
                    }
                }
            closePreviewButton.Visible = !_dialog && !IsTooltip;
            MouseDown += (_, _) => {
                if (_dialog || IsTooltip || data.Kind == PreviewKind.Image) {
                    Close();
                    }
                };
            Place(owner, settings, anchor);
            }
        catch {
            Dispose();
            throw;
            }
        }

    private void LoadCachedImage(PreviewData data, ImagePreflight.ImageInfo info, long decodedLimit) {
        _cachedImage = new CachedImageDecoder(data.Bytes, info.Width, info.Height, decodedLimit);
        for (int index = 0; index < _cachedImage.FrameCount; index++) {
            SkiaSharp.SKBitmap pixels = _cachedImage.Frame(index);
            // These lightweight drawing wrappers borrow the cached pixels. Release
            // the wrappers before the decoder cache when the preview closes.
            Bitmap frame = new Bitmap(pixels.Width, pixels.Height, pixels.RowBytes,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb, pixels.GetPixels());
            _cachedFrames.Add(frame);
            }
        _image = _cachedFrames[0];
        if (_cachedFrames.Count <= 1) {
            return;
            }
        _cachedTimer = new System.Windows.Forms.Timer { Interval = _cachedImage.Delay(0) };
        _cachedTimer.Tick += (_, _) => {
            if (_released || _cachedImage is null) {
                return;
                }
            int next = _cachedFrameIndex + 1;
            if (next == _cachedFrames.Count) {
                _cachedPlays++;
                if (_cachedImage.TotalPlays > 0 && _cachedPlays >= _cachedImage.TotalPlays) {
                    _cachedTimer.Stop();
                    return;
                    }
                next = 0;
                }
            _cachedFrameIndex = next;
            _image = _cachedFrames[next];
            _cachedTimer.Interval = _cachedImage.Delay(next);
            _surface?.Invalidate();
            };
        _cachedTimer.Start();
        }

    protected override bool PreserveOwnFrameStyle => _dialog;
    partial void TrimPlatformImage();

    protected override bool ShowWithoutActivation => IsTooltip;
    protected override CreateParams CreateParams {
        get {
            CreateParams parameters = base.CreateParams;
            if (IsTooltip) {
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                }
            if (_hoverTooltip) {
                parameters.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                }
            return parameters;
            }
        }
    protected override void WndProc(ref Message message) {
        if (_hoverTooltip && message.Msg == 0x0084) { // WM_NCHITTEST
            message.Result = new IntPtr(-1); // HTTRANSPARENT: keep the source list under the pointer.
            return;
            }
        if (IsTooltip && message.Msg == 0x0021) { // WM_MOUSEACTIVATE
            message.Result = new IntPtr(3); // MA_NOACTIVATE
            return;
            }
        base.WndProc(ref message);
        }
    internal static RectangleF ImageBounds(string mode, Size image, Size available) {
        if (image.Width <= 0 || image.Height <= 0 || available.Width <= 0 || available.Height <= 0) {
            return RectangleF.Empty;
            }
        float horizontal = (float)available.Width / image.Width;
        float vertical = (float)available.Height / image.Height;
        if (mode == "Scale") {
            horizontal = vertical = Math.Min(horizontal, vertical);
            }
        else if (mode == "Zoom") {
            horizontal = vertical = Math.Max(horizontal, vertical);
            }
        else if (mode == "Center") {
            horizontal = vertical = 1;
            }
        float width = image.Width * horizontal;
        float height = image.Height * vertical;
        return new RectangleF((available.Width - width) / 2, (available.Height - height) / 2, width, height);
        }
    private void PaintImage(object? sender, PaintEventArgs args) {
        if (_released || _image is null || _surface is null) {
            return;
            }
        if (_animationCallback is not null) {
            ImageAnimator.UpdateFrames(_image);
            }
        RectangleF bounds = ImageBounds(_scaleMode, _image.Size, _surface.ClientSize);
        if (bounds.IsEmpty) {
            return;
            }
        args.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        args.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        args.Graphics.DrawImage(_image, bounds);
        }
    private void Place(Form owner, AppSettings settings, Point anchor) {
        Rectangle screen = Screen.FromPoint(anchor).WorkingArea;
        Rectangle parent = owner.Bounds;
        int width = Math.Min(800, Math.Max(64, parent.Width * 2 / 3));
        int height = Math.Min(640, Math.Max(64, parent.Height * 2 / 3));
        if (settings.Flag("Presentation", "LimitSize")) {
            if (settings.Get("Presentation", "SizeMode", "Ratio") == "Pixels") {
                width = Resolution(settings, "MaxX", 800);
                height = Resolution(settings, "MaxY", 640);
                }
            else {
                double.TryParse(settings.Get("Presentation", "Percent", "66"), CultureInfo.InvariantCulture, out double percent);
                if (!AppSettings.Percentages.Contains(percent)) {
                    percent = 66;
                    }
                Rectangle relative = settings.Get("Presentation", "SizeRelativeTo", "Main Window") == "Current Screen" ? screen : parent;
                width = (int)(relative.Width * percent / 100);
                height = (int)(relative.Height * percent / 100);
                }
            }
        width = Math.Clamp(width, 64, Math.Max(64, screen.Width - 32));
        height = Math.Clamp(height, 64, Math.Max(64, screen.Height - 64));
        if (IsTooltip) {
            width = Math.Min(width, parent.Width);
            height = Math.Min(height, parent.Height);
            }
        Size = new Size(width, height);
        if (_dialog) {
            TrimImageContent();
            }
        else {
            TrimPlatformImage();
            }
        bool customized = settings.Flag("Presentation", "Customize");
        Rectangle origin = customized && settings.Get("Presentation", "RelativeTo", "Program Window") == "Current Screen" ? screen : parent;
        int x = origin.X;
        int y = origin.Y;
        if (!customized || settings.Get("Presentation", "Origin", "Center") == "Center") {
            x += (origin.Width - Width) / 2;
            y += (origin.Height - Height) / 2;
            }
        if (IsTooltip) {
            string location = settings.Get("Presentation", "Anchor", "File");
            Point position = location == "Cursor" ? Cursor.Position : location == "Bottom-Right" ? new Point(parent.Right - Width - 16, parent.Bottom - Height - 16) : anchor;
            x = position.X;
            y = position.Y;
            }
        if (!IsTooltip && customized && settings.Get("Presentation", "Origin", "Center") == "Right of Main Window") {
            Location = new Point(parent.Right, parent.Top);
            return;
            }
        Rectangle limits = IsTooltip ? Rectangle.Intersect(screen, parent) : screen;
        if (limits.Width <= 0 || limits.Height <= 0) {
            limits = parent;
            }
        Location = new Point(Math.Clamp(x, limits.Left, Math.Max(limits.Left, limits.Right - Width)), Math.Clamp(y, limits.Top, Math.Max(limits.Top, limits.Bottom - Height)));
        }
    private void TrimImageContent() {
        if (_image is null || (_scaleMode != "Scale" && _scaleMode != "Center")) {
            return;
            }
        PerformLayout();
        Size available = imageSurface.ClientSize;
        RectangleF displayed = ImageBounds(_scaleMode, _image.Size, available);
        int excessWidth = Math.Max(0, available.Width - (int)Math.Ceiling(displayed.Width));
        int excessHeight = Math.Max(0, available.Height - (int)Math.Ceiling(displayed.Height));
        Size = new Size(Math.Max(_dialog ? 96 : 16, Width - excessWidth), Math.Max(32, Height - excessHeight));
        PerformLayout();
        }

    private static int Resolution(AppSettings settings, string key, int fallback) {
        int value = settings.Number("Presentation", key, fallback, 64, 3072);
        return AppSettings.Resolutions.Contains(value) ? value : fallback;
        }
    protected override void Dispose(bool disposing) {
        if (disposing && !_released) {
            _released = true;
            if (_image is not null && _animationCallback is not null) {
                ImageAnimator.StopAnimate(_image, _animationCallback);
                }
            _cachedTimer?.Stop();
            _cachedTimer?.Dispose();
            _cachedTimer = null;
            if (_cachedImage is null) {
                _image?.Dispose();
                }
            foreach (Bitmap frame in _cachedFrames) {
                frame.Dispose();
                }
            _cachedFrames.Clear();
            _image = null;
            _cachedImage?.Dispose();
            _cachedImage = null;
            _imageStream?.Dispose();
            _imageStream = null;
            // RichEdit keeps its own text copy; release it before clearing the input snapshot.
            foreach (Control control in Controls) {
                if (control is RichTextBox text) {
                    text.Clear();
                    }
                }
            _data?.Dispose();
            components?.Dispose();
            }
        base.Dispose(disposing);
        if (disposing) {
            _textFont?.Dispose();
            _textFont = null;
            }
        }
    }
