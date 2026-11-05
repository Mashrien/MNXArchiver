using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Mash.UI.MNXFile.GUI.Previews;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;
using VirtualFile = Mash.IO.Format.MNX.VFile.VFile;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Windows-only smoke hooks. Call on the STA UI thread with an existing visible owner.</summary>
internal static class PreviewSmokeTests {
    internal static void Run(Form owner, string evidenceDirectory) {
        string directory = Path.Combine(Path.GetTempPath(), "mnx-options-smoke-" + Guid.NewGuid().ToString("N"));
        try {
            CheckOverrideInputReader(directory);
            AppSettings settings = new AppSettings(directory);
            DefaultKeyStore keys = new DefaultKeyStore(settings);
            string password = "Windows DPAPI あいうえお smoke password";
            keys.Store(password);
            Require(keys.Exists && keys.Read() == password, "The Windows-protected default password did not round trip.");
            byte[] envelope = System.IO.File.ReadAllBytes(settings.KeyPath);
            envelope[envelope.Length - 1] ^= 1;
            System.IO.File.WriteAllBytes(settings.KeyPath, envelope);
            bool tamperingRejected = false;
            try {
                keys.Read();
                }
            catch (System.Security.Cryptography.CryptographicException) {
                tamperingRejected = true;
                }
            Require(tamperingRejected, "A modified default-password envelope was accepted.");
            keys.Clear();
            Require(!keys.Exists, "Clearing the saved default left its key file behind.");
            settings.Set("FutureExtension", "KeepThis", "untouched");
            settings.Set("Previews", "Enabled", true);
            settings.Set("Presentation", "Customize", true);
            settings.Set("Presentation", "Mode", "Window");
            settings.Set("Presentation", "WindowType", "Window");
            settings.Set("Presentation", "RelativeTo", "Current Screen");
            using (OptionsForm options = new OptionsForm(settings)) {
                options.SaveSettings();
                Require(options.DialogResult == DialogResult.OK, "Options save did not finish.");
                }
            AppSettings reloaded = AppSettings.Load(directory);
            Require(reloaded.Get("FutureExtension", "KeepThis", "") == "untouched", "Options lost an unknown INI key.");
            Require(reloaded.PreviewBufferBytes == 32 * 1024 * 1024, "The default preview buffer changed.");
            Require(reloaded.DecodedPreviewBytes == 512L * 1024 * 1024 && reloaded.TotalPreviewBytes == 1024L * 1024 * 1024,
                "Decoded and combined preview memory defaults were not retained by the options editor.");
            AppSettings boundedMemory = settings.Copy();
            boundedMemory.Set("Previews", "DecodedMiB", 1);
            boundedMemory.Set("Previews", "TotalMiB", 1);
            Require(boundedMemory.DecodedPreviewBytes == 64L * 1024 * 1024 && boundedMemory.TotalPreviewBytes == 256L * 1024 * 1024,
                "Decoded and combined preview memory minima were not enforced.");
            boundedMemory.Set("Previews", "DecodedMiB", 9999);
            boundedMemory.Set("Previews", "TotalMiB", 9999);
            Require(boundedMemory.DecodedPreviewBytes == 1024L * 1024 * 1024 && boundedMemory.TotalPreviewBytes == 2048L * 1024 * 1024,
                "Decoded and combined preview memory maxima were not enforced.");
            Require(reloaded.Get("Previews", "DelayMs", "") == "750", "The default hover delay changed.");
            AppSettings shortDelay = settings.Copy();
            shortDelay.Set("Previews", "DelayMs", 250);
            Require(shortDelay.HoverDelay == 500, "A saved short hover delay was not clamped to 500 ms.");
            using (OptionsForm options = new OptionsForm(shortDelay)) {
                options.SaveSettings();
                }
            Require(AppSettings.Load(directory).Get("Previews", "DelayMs", "") == "500", "The options editor did not persist the 500 ms minimum.");
            Require(AppSettings.Extensions(" .CS, txt,cs ") == "cs,txt", "Custom extensions were not sanitized.");

            using (PreviewManager previews = new PreviewManager(owner, settings)) {
                Require(previews.CanPreview("readme.txt"), "Text classification failed.");
                Require(previews.CanPreview("picture.png"), "PNG classification failed.");
                Require(!previews.CanPreview("picture.webp"), "Unsupported native image type was advertised.");
                byte[] firstText = Encoding.UTF8.GetBytes("MNX Windows read-only preview\r\nUnicode: λ 文件");
                previews.Show(new PreviewData(firstText, PreviewKind.Text, false), "readme.txt", owner.Location, false);
                Application.DoEvents();
                Require(previews.Count == 1, "Text preview was not shown.");
                RichTextBox? text = previews.Windows[0].Controls.OfType<RichTextBox>().FirstOrDefault();
                Require(text is not null && text.ReadOnly && text.Text.Contains("文件"), "The text preview is not read-only Unicode text.");
                previews.Windows[0].Close();
                Require(firstText.All(value => value == 0), "Closing text did not clear its input snapshot.");

                byte[] png = Png();
                previews.Show(new PreviewData(png, PreviewKind.Image, false), "image.png", owner.Location, false);
                Application.DoEvents();
                Require(previews.Windows[0] is PreviewForm image && image.ImageSize == new Size(4, 3), "The bounded PNG did not decode.");
                using (Bitmap rendered = new Bitmap(previews.Windows[0].Width, previews.Windows[0].Height)) {
                    previews.Windows[0].DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
                    }
                previews.Windows[0].Close();
                Require(png.All(value => value == 0), "Closing an image did not clear its input snapshot.");

                foreach (ImageFormat format in new ImageFormat[] { ImageFormat.Jpeg, ImageFormat.Bmp, ImageFormat.Tiff, ImageFormat.Gif }) {
                    using Bitmap bitmap = new Bitmap(4, 3);
                    using MemoryStream encoded = new MemoryStream();
                    bitmap.Save(encoded, format);
                    byte[] bytes = encoded.ToArray();
                    previews.Show(new PreviewData(bytes, PreviewKind.Image, false), "native-image", owner.Location, false);
                    Application.DoEvents();
                    Require(previews.Windows.Count == 1 && previews.Windows[0] is PreviewForm nativeImage && nativeImage.ImageSize == new Size(4, 3), "A built-in image format failed: " + format);
                    previews.Windows[0].Close();
                    Require(bytes.All(value => value == 0), "Native image disposal did not clear its snapshot.");
                    }
                using (MemoryStream encoded = new MemoryStream()) {
                    SystemIcons.Information.Save(encoded);
                    byte[] bytes = encoded.ToArray();
                    previews.Show(new PreviewData(bytes, PreviewKind.Image, false), "icon.ico", owner.Location, false);
                    Application.DoEvents();
                    Require(previews.Windows.Count == 1, "ICO preview failed.");
                    previews.Windows[0].Close();
                    }

                for (int cycle = 0; cycle < 30; cycle++) {
                    byte[] bytes = Png();
                    previews.Show(new PreviewData(bytes, PreviewKind.Image, false), "repeated-image.png", owner.Location, false);
                    Require(previews.Count == 1, "Sequential image preview did not acquire exactly one active slot.");
                    Form imageWindow = previews.Windows[0];
                    if (cycle % 5 == 0) {
                        imageWindow.Close();
                        }
                    else if (cycle % 5 == 1) {
                        imageWindow.Controls.OfType<Button>().Single(button => button.Text == "Close").PerformClick();
                        }
                    else if (cycle % 5 == 2) {
                        imageWindow.Dispose();
                        }
                    else if (cycle % 5 == 3) {
                        Control imageSurface = imageWindow.Controls.OfType<ImageSurface>().Single();
                        SendMessage(imageSurface.Handle, 0x0201, new IntPtr(1), new IntPtr(10 | (10 << 16)));
                        }
                    else {
                        SendMessage(imageWindow.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        }
                    Application.DoEvents();
                    Require(imageWindow.IsDisposed && bytes.All(value => value == 0), "Closing a repeated image did not dispose its window and plaintext snapshot.");
                    Require(previews.Count == 0, "Disposed image retained an active preview slot at cycle " + cycle + ".");
                    }

                for (int index = 0; index < PreviewManager.MaximumWindows; index++) {
                    previews.Show(new PreviewData(Encoding.UTF8.GetBytes("Window " + index), PreviewKind.Text, false), "window.txt", owner.Location, false);
                    }
                Require(previews.Count == PreviewManager.MaximumWindows, "Independent previews did not retain ten windows.");
                byte[] rejected = Encoding.UTF8.GetBytes("eleventh");
                ExpectRejected(() => previews.Show(new PreviewData(rejected, PreviewKind.Text, false), "extra.txt", owner.Location, false));
                Require(rejected.All(value => value == 0), "Rejected preview did not clear its input snapshot.");
                for (int cycle = 0; cycle < 20; cycle++) {
                    previews.Windows[0].Dispose();
                    Require(previews.Count == PreviewManager.MaximumWindows - 1, "Disposing a preview at the limit did not return its slot.");
                    previews.Show(new PreviewData(Png(), PreviewKind.Image, false), "replacement.png", owner.Location, false);
                    Require(previews.Count == PreviewManager.MaximumWindows, "An available preview slot could not be reused.");
                    }
                }

            settings.Set("Presentation", "Mode", "Tooltip");
            settings.Set("Presentation", "Anchor", "File");
            byte[] tooltipBytes = Encoding.UTF8.GetBytes("Hover tooltip");
            using (PreviewManager previews = new PreviewManager(owner, settings)) {
                previews.Show(new PreviewData(tooltipBytes, PreviewKind.Text, false), "hover.txt", owner.Location, true);
                Application.DoEvents();
                Require(previews.Count == 1 && previews.Windows[0].FormBorderStyle == FormBorderStyle.None, "Tooltip has a border or was not shown.");
                previews.HideHover();
                Require(previews.Count == 0 && tooltipBytes.All(value => value == 0), "Hover leave did not destroy its snapshot.");
                }

            Require(PreviewForm.ImageBounds("Scale", new Size(100, 50), new Size(100, 100)) == new RectangleF(0, 25, 100, 50), "Scale geometry failed.");
            Require(PreviewForm.ImageBounds("Zoom", new Size(100, 50), new Size(100, 100)) == new RectangleF(-50, 0, 200, 100), "Zoom geometry failed.");
            Require(PreviewForm.ImageBounds("Stretch", new Size(100, 50), new Size(100, 100)) == new RectangleF(0, 0, 100, 100), "Stretch geometry failed.");
            Require(PreviewForm.ImageBounds("Center", new Size(100, 50), new Size(200, 100)) == new RectangleF(50, 25, 100, 50), "Center geometry failed.");
            CheckHalfResolution(owner, settings);
            CheckImageLimits();
            CheckCombinedImageBudget(owner, settings);
            CheckArchiveAnimations(owner, settings, evidenceDirectory);
            settings.Set("Images", "Animated", true);
            settings.Set("Presentation", "Mode", "Window");
            using (PreviewManager previews = new PreviewManager(owner, settings)) {
                byte[] animation = AnimatedGif();
                previews.Show(new PreviewData(animation, PreviewKind.Image, false), "animated.gif", owner.Location, false);
                Application.DoEvents();
                Require(previews.Count == 1 && previews.Windows[0] is PreviewForm animated && animated.ImageSize == new Size(1, 1), "Animated GIF did not decode.");
                previews.Windows[0].Close();
                Require(animation.All(value => value == 0), "Animated GIF close did not release its snapshot.");
                }
            }
        finally {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, true);
                }
            }
        }

    private static void CheckHalfResolution(Form owner, AppSettings defaults) {
        AppSettings settings = defaults.Copy();
        settings.Set("MNX.Previews.OVERRIDE", "DebugFlags", 1);
        settings.Set("Images", "Animated", true);
        using PreviewManager previews = new PreviewManager(owner, settings);
        byte[] png = Png();
        previews.Show(new PreviewData(png, PreviewKind.Image, false), "half.png", owner.Location, false);
        PreviewForm image = (PreviewForm)previews.Windows[0];
        Require(image.ImageSize == new Size(2, 2), "DebugFlags bit 0 did not decode a half-resolution preview (rounding odd dimensions up).");
        image.Close();
        Require(png.All(value => value == 0), "A half-resolution preview retained its source snapshot after closing.");
        byte[] gif = Convert.FromBase64String("R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAFAAAACwAAAAABAAEAAAICQABCBxIsCCAgAAh+QQBFAABACwAAAAABAAEAIEAAP8AAAAAAAAAAAAICQABCBxIsCCAgAA7");
        previews.Show(new PreviewData(gif, PreviewKind.Image, false), "half.gif", owner.Location, false);
        PreviewForm animation = (PreviewForm)previews.Windows[0];
        Require(animation.ImageSize == new Size(2, 2) && animation.IsAnimating, "Half-resolution GIF pixels or animation were lost.");
        animation.Close();
        Require(gif.All(value => value == 0), "Closing a reduced GIF did not clear its source snapshot.");
        settings.Set("MNX.Previews.OVERRIDE", "DebugFlags", 2);
        using PreviewManager unknownBit = new PreviewManager(owner, settings);
        unknownBit.Show(new PreviewData(Png(), PreviewKind.Image, false), "unknown-bit.png", owner.Location, false);
        Require(((PreviewForm)unknownBit.Windows[0]).ImageSize == new Size(4, 3), "An unknown debug bit enabled half-resolution decoding.");
        unknownBit.Windows[0].Close();
        }

    private static void CheckOverrideInputReader(string directory) {
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "large-preview-input.txt");
        using (FileStream source = new FileStream(input, FileMode.CreateNew, FileAccess.Write)) {
            source.SetLength(129L * 1024 * 1024 + 1);
            source.WriteByte(65);
            }
        using ArchiveFile archive = new ArchiveFile();
        archive.Options.Compression = Mash.IO.Format.MNX.Archive.Codecs.ArchiveCompression.Snappy;
        archive.Create(new FileInfo(Path.Combine(directory, "large-preview.mnx")));
        archive.AddFileNow(new FileInfo(input));
        VirtualFile entry = archive.Files.Single(file => !file.IsDirectory);
        using PreviewData data = PreviewData.Read(archive, entry.FileID, PreviewKind.Text, CancellationToken.None, 512 * 1024 * 1024);
        // Text still reads only its normal 2 MiB excerpt. This catches a hidden
        // 128 MiB input clamp without allocating a huge image or decoded frames.
        Require(data.Bytes.Length == PreviewData.TextLimit && data.Bytes[0] == 65 && data.Truncated,
            "The preview reader rejected an overridden input size or lost its text bound.");
        }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr first, IntPtr second);

    private static byte[] Png() {
        using Bitmap bitmap = new Bitmap(4, 3);
        bitmap.SetPixel(0, 0, Color.Red);
        using MemoryStream stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
        }
    private static void CheckImageLimits() {
        byte[] metadataPng = PngWithMetadata();
        ExpectRejected(() => ImagePreflight.Inspect(metadataPng, false, 1024 * 1024, halfResolution: true));
        Require(ImagePreflight.Inspect(metadataPng, false, 8 * 1024 * 1024, halfResolution: true).MemoryCost > 4 * 1024 * 1024,
            "Reducing pixels discarded the PNG's expanded metadata reservation.");
        byte[] largerAnimation = LargerAnimation();
        ImagePreflight.ImageInfo largerInfo = ImagePreflight.Inspect(largerAnimation, true);
        Require(largerInfo.Frames == 144 && largerInfo.MemoryCost > 64L * 1024 * 1024,
            "An ordinary animation larger than the old decoded limit was rejected.");
        ExpectRejected(() => ImagePreflight.Inspect(largerAnimation, true, 64L * 1024 * 1024));
        Require(ImagePreflight.Inspect(largerAnimation, true, 256L * 1024 * 1024).Frames == 144,
            "A configured decoded budget was not applied.");
        ExpectRejected(() => PreviewData.CheckGif(largerAnimation, true, 64L * 1024 * 1024));
        Require(PreviewData.CheckGif(largerAnimation, true, 256L * 1024 * 1024) == 144,
            "The shared GIF preflight did not honor its configured decoded budget.");
        byte[] hugePng = Png();
        BinaryPrimitives.WriteUInt32BigEndian(hugePng.AsSpan(16, 4), 16000);
        BinaryPrimitives.WriteUInt32BigEndian(hugePng.AsSpan(20, 4), 16000);
        ExpectRejected(() => ImagePreflight.Inspect(hugePng, false));
        byte[] gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        ImagePreflight.ImageInfo single = ImagePreflight.Inspect(gif, false);
        Require(single.Width == 1 && single.Height == 1 && single.Frames == 1, "GIF header parsing failed.");
        byte[] frames = AnimatedGif();
        ExpectRejected(() => ImagePreflight.Inspect(frames, false));
        Require(ImagePreflight.Inspect(frames, true).Frames == 2, "Animated GIF frame count failed.");
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6, 2), 16000);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8, 2), 16000);
        ExpectRejected(() => ImagePreflight.Inspect(gif, true));
        ExpectRejected(() => ImagePreflight.Inspect(new byte[] { 1, 2, 3, 4 }, true));
        }
    private static byte[] PngWithMetadata() {
        byte[] png = Png();
        using MemoryStream metadata = new MemoryStream();
        metadata.Write("note\0\0"u8);
        using (System.IO.Compression.ZLibStream compressor = new System.IO.Compression.ZLibStream(metadata, System.IO.Compression.CompressionLevel.Fastest, true)) {
            byte[] piece = new byte[8192];
            for (int index = 0; index < 256; index++) {
                compressor.Write(piece);
                }
            }
        using MemoryStream result = new MemoryStream();
        result.Write(png, 0, png.Length - 12);
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, (int)metadata.Length);
        result.Write(length);
        result.Write("zTXt"u8);
        metadata.Position = 0;
        metadata.CopyTo(result);
        result.Write(new byte[4]); // Header-validation fixture: CRC is not read by preflight.
        result.Write(png, png.Length - 12, 12);
        return result.ToArray();
        }

    private static byte[] LargerAnimation() {
        byte[] gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6, 2), 600);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8, 2), 338);
        int imageStart = Array.IndexOf(gif, (byte)0x2c, 19);
        using MemoryStream output = new MemoryStream();
        output.Write(gif, 0, imageStart);
        for (int frame = 0; frame < 144; frame++) {
            output.Write(gif, imageStart, gif.Length - imageStart - 1);
            }
        output.WriteByte(0x3b);
        return output.ToArray();
        }

    private static void CheckCombinedImageBudget(Form owner, AppSettings defaults) {
        AppSettings settings = defaults.Copy();
        settings.Set("Images", "Animated", true);
        settings.Set("Presentation", "Mode", "Window");
        settings.Set("Previews", "TotalMiB", 256);
        using PreviewManager previews = new PreviewManager(owner, settings);
        previews.Show(new PreviewData(LargerAnimation(), PreviewKind.Image, false), "first.gif", owner.Location, false);
        previews.Show(new PreviewData(LargerAnimation(), PreviewKind.Image, false), "second.gif", owner.Location, false);
        byte[] rejected = LargerAnimation();
        ExpectRejected(() => previews.Show(new PreviewData(rejected, PreviewKind.Image, false), "third.gif", owner.Location, false));
        Require(previews.Count == 2 && rejected.All(value => value == 0), "The combined decoded reservation was not enforced.");
        previews.Windows[0].Close();
        previews.Show(new PreviewData(LargerAnimation(), PreviewKind.Image, false), "replacement.gif", owner.Location, false);
        Require(previews.Count == 2, "Closing a large image did not return its decoded memory reservation.");
        previews.Windows[0].Close();
        }

    private static void CheckArchiveAnimations(Form owner, AppSettings defaults, string evidenceDirectory) {
        string? path = Environment.GetEnvironmentVariable("MNX_GUI_LARGE_GIF_ARCHIVE");
        if (string.IsNullOrEmpty(path)) {
            return;
            }
        AppSettings settings = defaults.Copy();
        settings.Set("Images", "Animated", true);
        settings.Set("Presentation", "Mode", "Window");
        using ArchiveFile archive = new ArchiveFile().Open(path);
        VirtualFile[] candidates = archive.Files.Where(file => file.Name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            && file.SizeOriginal >= 8_000_000 && file.SizeOriginal <= 20_000_000).ToArray();
        Require(candidates.Length > 0, "The large-GIF test archive contained no matching samples.");
        List<string> evidence = new List<string>();
        using PreviewManager previews = new PreviewManager(owner, settings);
        foreach (long target in new long[] { 9_000_000, 15_000_000 }) {
            VirtualFile sample = candidates.OrderBy(file => Math.Abs((long)file.SizeOriginal - target)).First();
            PreviewData data = PreviewData.Read(archive, sample.FileID, PreviewKind.Image, CancellationToken.None, settings.PreviewBufferBytes);
            byte[] bytes = data.Bytes;
            ImagePreflight.ImageInfo info = ImagePreflight.Inspect(bytes, true);
            previews.Show(data, "Large GIF test", owner.Location, false);
            Application.DoEvents();
            Require(previews.Count == 1 && previews.Windows[0] is PreviewForm preview
                && preview.ImageSize == new Size(info.Width, info.Height) && preview.IsAnimating,
                "An actual archived large GIF did not decode and start animating.");
            previews.Windows[0].Close();
            Require(previews.Count == 0 && bytes.All(value => value == 0), "Closing a large archived GIF did not release its preview.");
            evidence.Add($"PASS: archived GIF {sample.SizeOriginal:N0} bytes, {info.Width}x{info.Height}, {info.Frames} frames, decoded reservation {info.MemoryCost / (1024 * 1024):N0} MiB; animation enabled; close freed slot and cleared data");
            }
        System.IO.File.WriteAllLines(Path.Combine(evidenceDirectory, "large-gif-results.txt"), evidence);
        }
    private static byte[] AnimatedGif() {
        byte[] gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        int imageStart = Array.IndexOf(gif, (byte)0x2c, 19);
        using MemoryStream animated = new MemoryStream();
        animated.Write(gif, 0, gif.Length - 1);
        animated.Write(gif, imageStart, gif.Length - imageStart - 1);
        animated.WriteByte(0x3b);
        return animated.ToArray();
        }
    private static void ExpectRejected(Action action) {
        try {
            action();
            }
        catch (IOException) {
            return;
            }
        throw new InvalidOperationException("An unsafe or disallowed preview was accepted.");
        }
    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
            }
        }
    }
