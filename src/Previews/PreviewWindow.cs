using System.Globalization;
using System.Runtime.InteropServices;
using Gtk;
using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Settings;

namespace Mash.GUI.MNXArchiver.Previews;

/// <summary>Owns the preview buffer, native decoder and animation timer until its window closes.</summary>
internal sealed class PreviewWindow : IDisposable {
    internal Gtk.Window Window { get; }
    internal long MemoryCost { get; private set; }
    internal bool Independent { get; }
    internal bool IsPopup { get; }
    internal bool UsesWayland => IsWaylandDisplay(Window.Display);
    internal static bool IsWaylandDisplay(Gdk.Display display) {
        return OperatingSystem.IsLinux()
            && Marshal.PtrToStringUTF8(g_type_name_from_instance(display.Handle)) == "GdkWaylandDisplay";
        }
    internal bool IsAnimating => !_closed && (_frames is not null || _cachedImage?.FrameCount > 1 && _timer != 0);
    internal (int Width, int Height) ImageSize => (_image?.Width ?? 0, _image?.Height ?? 0);
    private readonly int _border = 8;
    private readonly bool _relativeDialog;
    private readonly PreviewData _data;
    private readonly string _fileName;
    private readonly AppSettings _settings;
    private readonly long _availableMemory;
    private CachedImageDecoder? _cachedImage;
    private Gdk.PixbufLoader? _loader;
    private Gdk.PixbufAnimation? _animation;
    private Gdk.PixbufAnimationIter? _frames;
    private Gdk.Pixbuf? _image;
    private DrawingArea? _canvas;
    private uint _timer;
    private bool _closed;
    private int _nextFrame;
    internal event Action<PreviewWindow>? Closed;

    internal PreviewWindow(Gtk.Window parent, string title, PreviewData data, AppSettings settings, Gdk.Rectangle anchor, int cursorX, int cursorY, long availableMemory = long.MaxValue) {
        _data = data;
        _fileName = title;
        _settings = settings;
        _availableMemory = availableMemory;
        Independent = settings.IndependentWindows;
        bool customized = settings.Flag("Presentation", "Customize");
        IsPopup = customized && settings.Get("Presentation", "Mode", "Window") == "Tooltip";
        bool dialog = !IsPopup && (!customized || settings.Get("Presentation", "WindowType", "Dialog") == "Dialog");
        _relativeDialog = dialog && customized && settings.Get("Presentation", "Origin", "Center") == "Right of Main Window";
        Window = new Gtk.Window(IsPopup || _relativeDialog ? WindowType.Popup : WindowType.Toplevel) {
            Title = "Preview — " + title,
            Decorated = !IsPopup && !dialog,
            Resizable = !IsPopup && !dialog,
            SkipTaskbarHint = !Independent,
            TypeHint = IsPopup ? Gdk.WindowTypeHint.Tooltip : dialog ? Gdk.WindowTypeHint.Dialog : Gdk.WindowTypeHint.Normal,
            AcceptFocus = !IsPopup,
            DestroyWithParent = !Independent
            };
        _border = dialog ? 0 : 8;
        GtkWindowAppearance.Apply(Window);
        if (!Independent) {
            Window.TransientFor = parent;
            }
        Window.DeleteEvent += (_, args) => {
            args.RetVal = true;
            Dispose();
            };
        Window.Destroyed += (_, _) => Dispose();
        Window.AddEvents((int)(Gdk.EventMask.ButtonPressMask | Gdk.EventMask.FocusChangeMask | Gdk.EventMask.KeyPressMask));
        Window.KeyPressEvent += (_, args) => {
            if (args.Event.Key == Gdk.Key.Escape) {
                Dispose();
                args.RetVal = true;
                }
            };
        if (dialog) {
            Window.FocusOutEvent += (_, _) => Dispose();
            }
        Window.ButtonPressEvent += (_, _) => {
            if (dialog || IsPopup || data.Kind == PreviewKind.Image) {
                Dispose();
                }
            };
        try {
            Box layout = new Box(Orientation.Vertical, 6) { BorderWidth = (uint)_border };
            if (dialog || IsPopup || data.Kind == PreviewKind.Image) {
                EventBox clickSurface = new EventBox { AboveChild = true, VisibleWindow = false };
                clickSurface.AddEvents((int)Gdk.EventMask.ButtonPressMask);
                clickSurface.ButtonPressEvent += CloseOnClick;
                clickSurface.Add(layout);
                Window.Add(clickSurface);
                }
            else {
                Window.Add(layout);
                }
            if (dialog) {
                Box titleBar = new Box(Orientation.Horizontal, 0);
                titleBar.StyleContext.AddClass("titlebar");
                Label titleText = new Label(Window.Title) { Xalign = 0, Ellipsize = Pango.EllipsizeMode.End, Hexpand = true, MarginStart = 8 };
                Button closeTitle = new Button("×") { TooltipText = "Close preview", WidthRequest = 28, HeightRequest = 28 };
                closeTitle.Clicked += (_, _) => Dispose();
                titleBar.PackStart(titleText, true, true, 0);
                titleBar.PackEnd(closeTitle, false, false, 0);
                layout.PackStart(titleBar, false, false, 0);
                layout.Spacing = 0;
                }
            if (data.Kind == PreviewKind.Text) {
                string content = data.Text();
                MemoryCost = data.Bytes.Length + 4L * content.Length;
                CheckAvailableMemory(MemoryCost);
                ScrolledWindow scroll = new ScrolledWindow { Hexpand = true, Vexpand = true };
                TextView text = new TextView { Editable = false, CursorVisible = false, Monospace = true, WrapMode = WrapMode.WordChar };
                text.Buffer.Text = content;
                if (dialog) {
                    text.AddEvents((int)Gdk.EventMask.ButtonPressMask);
                    text.ButtonPressEvent += CloseOnClick;
                    }
                scroll.Add(text);
                layout.PackStart(scroll, true, true, 0);
                }
            else {
                DecodeImage();
                _canvas = new DrawingArea { Hexpand = true, Vexpand = true };
                _canvas.AddEvents((int)Gdk.EventMask.ButtonPressMask);
                _canvas.ButtonPressEvent += CloseOnClick;
                _canvas.Drawn += DrawImage;
                layout.PackStart(_canvas, true, true, 0);
                }
            if (!IsPopup && !dialog) {
                Button close = new Button("Close");
                close.Clicked += (_, _) => Dispose();
                layout.PackEnd(close, false, false, 0);
                }
            Place(parent, anchor, cursorX, cursorY);
            }
        catch {
            Dispose();
            throw;
            }
        }

    [GLib.ConnectBefore]
    private void CloseOnClick(object sender, ButtonPressEventArgs args) {
        args.RetVal = true;
        Dispose();
        }

    private void DecodeImage() {
        string? format = BuiltInImageFormat(_data.Bytes, _fileName);
        if (format == "gif") {
            ImagePreflight.ImageInfo info = ImagePreflight.Inspect(_data.Bytes, _settings.Flag("Images", "Animated"),
                _settings.DecodedPreviewBytes, _settings.HalfResolutionPreviews);
            DecodeCachedImage(info);
            return;
            }
        if (format is null) {
            NativeImageProvider? provider = PluginCatalog.Installed.ImageProviderFor(_fileName);
            if (provider is not null) {
                ImagePreflight.ImageInfo info = ImagePreflight.InspectProvider(_data.Bytes, provider,
                    _settings.DecodedPreviewBytes, _settings.HalfResolutionPreviews, _settings.Flag("Images", "Animated"));
                DecodeCachedImage(info);
                return;
                }
            format = PlatformImageFormat(_data.Bytes);
            }

        int frameCount = PreviewData.CheckGif(_data.Bytes, _settings.Flag("Images", "Animated"), _settings.DecodedPreviewBytes);
        _loader = Gdk.PixbufLoader.NewWithType(format);
        bool tooLarge = false;
        long requiredDecodedMemory = 0;
        _loader.SizePrepared += (_, size) => {
            bool invalidDimensions = size.Width <= 0 || size.Height <= 0 || size.Width > 16384 || size.Height > 16384;
            requiredDecodedMemory = invalidDimensions ? long.MaxValue : (long)size.Width * size.Height * 4 * (frameCount == 0 ? 2L : frameCount + 2L);
            int outputWidth = size.Width;
            int outputHeight = size.Height;
            if (_settings.HalfResolutionPreviews && !invalidDimensions) {
                outputWidth = Math.Max(1, (size.Width + 1) / 2);
                outputHeight = Math.Max(1, (size.Height + 1) / 2);
                // Platform codecs for static formats may keep a full-size working
                // raster while producing the reduced output. Reserve both sizes.
                requiredDecodedMemory = (long)size.Width * size.Height * 8 + (long)outputWidth * outputHeight * 8;
                }
            if (invalidDimensions || requiredDecodedMemory > _settings.DecodedPreviewBytes
                || _data.Bytes.Length + requiredDecodedMemory > _availableMemory) {
                tooLarge = true;
                _loader.SetSize(1, 1);
                }
            else if (_settings.HalfResolutionPreviews) {
                _loader.SetSize(outputWidth, outputHeight);
                }
            };
        // Feed headers separately. SizePrepared can reject huge dimensions before
        // the decoder sees the image body, without throwing through a native callback.
        using MemoryStream input = new MemoryStream(_data.Bytes, false);
        byte[] piece = new byte[4096];
        while (true) {
            int count = input.Read(piece, 0, piece.Length);
            if (count == 0) {
                break;
                }
            _loader.Write(piece, (ulong)count);
            if (tooLarge) {
                PreviewData.CheckDecodedBudget(requiredDecodedMemory, _settings.DecodedPreviewBytes);
                CheckAvailableMemory(_data.Bytes.Length + requiredDecodedMemory);
                throw new IOException("The image dimensions are invalid.");
                }
            }
        _loader.Close();
        _animation = ImageDecoder.Animation(_loader);
        if (!_animation.IsStaticImage) {
            if (frameCount == 0) {
                throw new IOException("Only bounded GIF animations are currently supported.");
                }
            _frames = ImageDecoder.Iterator(_animation);
            _image = _frames.Pixbuf;
            _nextFrame = Environment.TickCount;
            _timer = GLib.Timeout.Add(25, () => {
                if (_closed || _frames is null) {
                    _timer = 0;
                    return false;
                    }
                if (Environment.TickCount - _nextFrame >= 0) {
                    if (_frames.Advance(IntPtr.Zero)) {
                        Gdk.Pixbuf next = _frames.Pixbuf;
                        if (!ReferenceEquals(next, _image)) {
                            _image?.Dispose();
                            _image = next;
                            }
                        _canvas?.QueueDraw();
                        }
                    int delay = _frames.DelayTime;
                    if (delay < 0) {
                        _timer = 0;
                        return false;
                        }
                    _nextFrame = unchecked(Environment.TickCount + Math.Max(25, delay));
                    }
                return true;
                });
            }
        else {
            _image = _animation.StaticImage;
            }
        MemoryCost = _data.Bytes.Length + requiredDecodedMemory;
        CheckAvailableMemory(MemoryCost);
        }

    private void DecodeCachedImage(ImagePreflight.ImageInfo info) {
        MemoryCost = _data.Bytes.Length + info.MemoryCost;
        CheckAvailableMemory(MemoryCost);
        _cachedImage = info.DecodeCached(_data.Bytes, _settings.DecodedPreviewBytes);
        _image = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, true, 8, info.Width, info.Height);
        _cachedImage.CopyRgbaFrame(0, _image.Pixels, _image.Rowstride);
        if (_cachedImage.FrameCount <= 1) {
            return;
            }
        int frameIndex = 0;
        int plays = 0;
        _nextFrame = unchecked(Environment.TickCount + _cachedImage.Delay(0));
        _timer = GLib.Timeout.Add(10, () => {
            if (_closed || _cachedImage is null || _image is null) {
                _timer = 0;
                return false;
                }
            if (Environment.TickCount - _nextFrame < 0) {
                return true;
                }
            int next = frameIndex + 1;
            if (next == _cachedImage.FrameCount) {
                plays++;
                if (_cachedImage.TotalPlays > 0 && plays >= _cachedImage.TotalPlays) {
                    _timer = 0;
                    return false;
                    }
                next = 0;
                }
            frameIndex = next;
            _cachedImage.CopyRgbaFrame(next, _image.Pixels, _image.Rowstride);
            _canvas?.QueueDraw();
            _nextFrame = unchecked(Environment.TickCount + _cachedImage.Delay(next));
            return true;
            });
        }

    private void CheckAvailableMemory(long bytes) {
        if (bytes > _availableMemory) {
            throw new IOException($"Close a preview first: the combined preview memory limit is {_settings.TotalPreviewBytes / (1024 * 1024):N0} MiB. You can change it in Options.");
            }
        }

    private static string? BuiltInImageFormat(byte[] data, string fileName) {
        ReadOnlySpan<byte> bytes = data;
        if (bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10})) {
            return "png";
            }
        if (bytes.StartsWith(new byte[] {255,216,255})) {
            return "jpeg";
            }
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) {
            return "gif";
            }
        if (bytes.StartsWith("BM"u8)) {
            return "bmp";
            }
        if (bytes.StartsWith("II*"u8) || bytes.StartsWith(new byte[] {77,77,0,42})) {
            return "tiff";
            }
        if (bytes.StartsWith(new byte[] {0,0,1,0}) || bytes.StartsWith(new byte[] {0,0,2,0})) {
            return "ico";
            }
        if (bytes.Length > 2 && bytes[0] == 'P' && bytes[1] >= '1' && bytes[1] <= '6' && char.IsWhiteSpace((char)bytes[2])) {
            return "pnm";
            }
        if (Path.GetExtension(fileName).Equals(".tga", StringComparison.OrdinalIgnoreCase) && bytes.Length >= 18 && bytes[1] <= 1 && new byte[] {1,2,3,9,10,11}.Contains(bytes[2])) {
            return "tga";
            }
        return null;
        }

    private static string PlatformImageFormat(byte[] data) {
        ReadOnlySpan<byte> bytes = data;
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8)) {
            // The GTK WebP loader may decode animation without reporting all frames.
            if (bytes.Length >= 21 && bytes.Slice(12,4).SequenceEqual("VP8X"u8) && (bytes[20] & 2) != 0) {
                throw new IOException("Animated WebP previews are not yet supported; GIF animation is available.");
                }
            return "webp";
            }
        throw new IOException("This image signature is not supported by the in-memory previewer.");
        }

    internal static (double X, double Y, double Width, double Height) ImageBounds(string mode, int imageWidth, int imageHeight, int width, int height) {
        double horizontal = (double)width / imageWidth;
        double vertical = (double)height / imageHeight;
        if (mode == "Scale") {
            horizontal = vertical = Math.Min(horizontal, vertical);
            }
        else if (mode == "Zoom") {
            horizontal = vertical = Math.Max(horizontal, vertical);
            }
        else if (mode == "Center") {
            horizontal = vertical = 1;
            }
        return ((width - imageWidth * horizontal) / 2, (height - imageHeight * vertical) / 2, imageWidth * horizontal, imageHeight * vertical);
        }
    private void DrawImage(object sender, DrawnArgs args) {
        if (_image is null || _canvas is null || _closed) {
            return;
            }
        (double x, double y, double width, double height) = ImageBounds(_settings.Choice("Presentation", "ScaleMode", "Scale", "Scale", "Zoom", "Stretch", "Center"), _image.Width, _image.Height, _canvas.Allocation.Width, _canvas.Allocation.Height);
        Cairo.Context drawing = args.Cr;
        drawing.Save();
        drawing.Rectangle(0, 0, _canvas.Allocation.Width, _canvas.Allocation.Height);
        drawing.Clip();
        drawing.Translate(x, y);
        drawing.Scale(width / _image.Width, height / _image.Height);
        Gdk.CairoHelper.SetSourcePixbuf(drawing, _image, 0, 0);
        drawing.Paint();
        drawing.Restore();
        args.RetVal = true;
        }

    private void Place(Gtk.Window parent, Gdk.Rectangle anchor, int cursorX, int cursorY) {
        Gdk.Rectangle monitor = parent.Display.GetMonitorAtWindow(parent.Window).Workarea;
        // GTK's GetPosition includes the window-manager title bar on X11.
        // All preview anchors use client coordinates, including the bottom-right.
        parent.Window.GetOrigin(out int parentX, out int parentY);
        parent.GetSize(out int parentWidth, out int parentHeight);
        int width = Math.Min(800, Math.Max(64, parentWidth * 2 / 3));
        int height = Math.Min(640, Math.Max(64, parentHeight * 2 / 3));
        if (_settings.Flag("Presentation", "LimitSize")) {
            if (_settings.Get("Presentation", "SizeMode", "Ratio") == "Pixels") {
                width = ValidResolution("MaxX", 800);
                height = ValidResolution("MaxY", 640);
                }
            else {
                double.TryParse(_settings.Get("Presentation", "Percent", "66"), CultureInfo.InvariantCulture, out double percent);
                if (!AppSettings.Percentages.Contains(percent)) {
                    percent = 66;
                    }
                bool useScreen = _settings.Get("Presentation", "SizeRelativeTo", "Main Window") == "Current Screen";
                width = (int)((useScreen ? monitor.Width : parentWidth) * percent / 100);
                height = (int)((useScreen ? monitor.Height : parentHeight) * percent / 100);
                }
            }
        width = Math.Clamp(width, 64, Math.Max(64, monitor.Width - 32));
        height = Math.Clamp(height, 64, Math.Max(64, monitor.Height - 64));
        if (IsPopup) {
            width = Math.Min(width, parentWidth);
            height = Math.Min(height, parentHeight);
            }
        if (_image is not null && _canvas is not null) {
            string scaleMode = _settings.Choice("Presentation", "ScaleMode", "Scale", "Scale", "Zoom", "Stretch", "Center");
            if (scaleMode == "Scale" || scaleMode == "Center") {
                // Measure the border and any Close button separately. They consume
                // space, but must not leave an unused band around the image canvas.
                Window.Child.ShowAll();
                Window.Child.GetPreferredSize(out Requisition minimum, out Requisition decoration);
                int canvasWidth = Math.Max(1, width - _border * 2);
                int canvasHeight = Math.Max(1, height - decoration.Height);
                double scale = Math.Min((double)canvasWidth / _image.Width, (double)canvasHeight / _image.Height);
                if (scaleMode == "Center") {
                    // Center keeps original pixels; crop only when the configured
                    // limit is smaller than the original image.
                    canvasWidth = Math.Min(canvasWidth, _image.Width);
                    canvasHeight = Math.Min(canvasHeight, _image.Height);
                    }
                else {
                    canvasWidth = Math.Max(1, (int)Math.Floor(_image.Width * scale));
                    canvasHeight = Math.Max(1, (int)Math.Floor(_image.Height * scale));
                    }
                width = canvasWidth + _border * 2;
                height = canvasHeight + decoration.Height;
                }
            }
        Window.SetDefaultSize(width, height);
        bool relativeScreen = _settings.Flag("Presentation", "Customize") && _settings.Get("Presentation", "RelativeTo", "Program Window") == "Current Screen";
        int x = relativeScreen ? monitor.X : parentX;
        int y = relativeScreen ? monitor.Y : parentY;
        bool centered = !_settings.Flag("Presentation", "Customize") || _settings.Get("Presentation", "Origin", "Center") == "Center";
        if (centered) {
            x += ((relativeScreen ? monitor.Width : parentWidth) - width) / 2;
            y += ((relativeScreen ? monitor.Height : parentHeight) - height) / 2;
            }
        if (IsPopup) {
            string location = _settings.Get("Presentation", "Anchor", "File");
            x = location == "Cursor" ? cursorX : location == "Bottom-Right" ? parentX + parentWidth - width - 16 : anchor.X;
            y = location == "Cursor" ? cursorY : location == "Bottom-Right" ? parentY + parentHeight - height - 16 : anchor.Y;
            }
        bool rightOfMain = !IsPopup && _settings.Flag("Presentation", "Customize") && _settings.Get("Presentation", "Origin", "Center") == "Right of Main Window";
        if (rightOfMain) {
            x = parentX + parentWidth;
            y = parentY;
            }
        if (IsPopup || _relativeDialog) {
            if (IsPopup) {
                x = Math.Clamp(x, parentX, Math.Max(parentX, parentX + parentWidth - width));
                y = Math.Clamp(y, parentY, Math.Max(parentY, parentY + parentHeight - height));
                }
            Window.Realize();
            if (UsesWayland) {
                // Wayland positions popups relative to their parent surface.
                // Global monitor offsets can push them offscreen; the compositor
                // slides the parent-relative anchor back into view when necessary.
                int localX = x - parentX;
                int localY = y - parentY;
                if (IsPopup && _settings.Get("Presentation", "Anchor", "File") == "Bottom-Right") {
                    // GTK can include invisible shadows and a client-side title
                    // bar in the native surface. Anchor to the actual content box.
                    Gdk.Rectangle clientArea = parent.Child.Allocation;
                    localX = clientArea.X + clientArea.Width - width - 16;
                    localY = clientArea.Y + clientArea.Height - height - 16;
                    }
                Gdk.Rectangle localAnchor = new Gdk.Rectangle(localX, localY, 1, 1);
                Window.Window.TransientFor = parent.Window;
                const int slideBothAxes = (1 << 2) | (1 << 3);
                gdk_window_move_to_rect(Window.Window.Handle, ref localAnchor,
                    Gdk.Gravity.NorthWest, Gdk.Gravity.NorthWest, slideBothAxes, 0, 0
                    );
                return;
                }
            }
        if (rightOfMain) {
            Window.Move(x, y);
            return;
            }
        Window.Move(Math.Clamp(x, monitor.X, Math.Max(monitor.X, monitor.X + monitor.Width - width)), Math.Clamp(y, monitor.Y, Math.Max(monitor.Y, monitor.Y + monitor.Height - height)));
        }
    // GtkSharp 3.24 does not bind move_to_rect. GDK's parent-relative popup
    // API is required on Wayland, where arbitrary global window moves are absent.
    [DllImport("libgobject-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr g_type_name_from_instance(IntPtr instance);
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gdk_window_move_to_rect(IntPtr window, ref Gdk.Rectangle anchor,
        Gdk.Gravity rectangleAnchor, Gdk.Gravity windowAnchor, int hints, int offsetX, int offsetY
        );

    private int ValidResolution(string key, int fallback) {
        int number = _settings.Number("Presentation", key, fallback, 64, 3072);
        return AppSettings.Resolutions.Contains(number) ? number : fallback;
        }
    internal void Show() {
        if (IsPopup && _settings.Trigger == "Mouse-Over With Delay") {
            Window.Realize();
            // Hover popups must not steal pointer events from the row they cover.
            Window.Window.PassThrough = true;
            // An empty input region also excludes native child windows such as
            // TextView and EventBox, which pass-through alone does not cover.
            using Cairo.Region inputRegion = new Cairo.Region();
            Window.InputShapeCombineRegion(inputRegion);
            }
        Window.ShowAll();
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
        Window.Hide();
        _image?.Dispose();
        _cachedImage?.Dispose();
        _cachedImage = null;
        _frames?.Dispose();
        _animation?.Dispose();
        if (_loader is not null) {
            try {
                _loader.Close();
                }
            catch (GLib.GException) {
                // A failed decoder still owns buffers that must be released.
                }
            _loader.Dispose();
            }
        _data.Dispose();
        Closed?.Invoke(this);
        Window.Dispose();
        }
    }
