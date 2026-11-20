using System.Diagnostics;
using Gtk;
using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Previews;
using MNX;

namespace Mash.GUI.MNXArchiver;

internal static class VideoPreviewSmoke {
    internal static void Run(MainWindow host, string fixture) {
        using TemporaryDirectory work = new TemporaryDirectory();
        string mnxPath = Path.Combine(work.Path, "video.mnx");
        using (Archive archive = new Archive()) {
            archive.Options.Compression = ArchiveCompression.Snappy;
            archive.Create(new FileInfo(mnxPath));
            archive.AddFileNow(new FileInfo(fixture));
            }
        host.TestSettings.Set("Previews", "Enabled", true);
        host.TestSettings.Set("Presentation", "Customize", true);
        host.TestSettings.Set("Presentation", "WindowType", "Window");
        TestPlayback(host, mnxPath);
        TestTypeScript(host, work.Path);
        TestPresentation(host, mnxPath);

        NativeArchiveModule? rpa = PluginCatalog.Installed.Modules.FirstOrDefault(module => module.Format.Id == "renpy.rpa");
        if (rpa is not null && rpa.Format.Versions.Any(version => version.Actions.HasFlag(PluginActions.StreamEntry))) {
            string rpaPath = Path.Combine(work.Path, "video.rpa");
            OpenedModuleArchive.Create(rpa, rpaPath, [fixture], overwrite: false);
            TestPlayback(host, rpaPath);
            }
        Console.WriteLine("PASS: GTK3 video window decodes, pauses, seeks while paused, resumes and closes before archive disposal.");
        }

    private static void TestPresentation(MainWindow host, string path) {
        if (!host.OpenArchive(path, showErrors: false)) {
            throw new InvalidOperationException("Could not open the video presentation fixture.");
            }
        ArchiveBrowser browser = host.TestBrowser!;
        Guid video = browser.Children(browser.Root).Single().Id;
        host.TestSettings.Set("Presentation", "WindowType", "Dialog");
        host.TestSettings.Set("Presentation", "Origin", "Right of Main Window");
        host.PreviewEntry(video);
        WaitFor(() => host.TestVideoPreview?.Window.IsMapped == true, "The video dialog did not map.");
        GtkVideoPreviewWindow dialog = host.TestVideoPreview!;
        if (dialog.Window.Decorated || dialog.Window.Resizable || dialog.Window.TypeHint != Gdk.WindowTypeHint.Dialog
            || dialog.Window.TransientFor is not Gtk.Window parent) {
            throw new InvalidOperationException("The video dialog did not use fixed owned dialog chrome.");
            }
        if (!PreviewWindow.IsWaylandDisplay(parent.Display)) {
            parent.Window.GetOrigin(out int ownerX, out int ownerY);
            parent.GetSize(out int ownerWidth, out _);
            dialog.Window.GetPosition(out int x, out int y);
            if (x != ownerX + ownerWidth || y != ownerY) {
                throw new InvalidOperationException($"The video dialog mapped at {x},{y} instead of {ownerX + ownerWidth},{ownerY}.");
                }
            }
        dialog.Dispose();

        host.TestSettings.Set("Presentation", "Mode", "Tooltip");
        host.TestSettings.Set("Presentation", "Anchor", "Bottom-Right");
        host.TestSettings.Set("Presentation", "LimitSize", true);
        host.TestSettings.Set("Presentation", "SizeMode", "Pixels");
        host.TestSettings.Set("Presentation", "MaxX", 256);
        host.TestSettings.Set("Presentation", "MaxY", 128);
        host.PreviewEntry(video);
        WaitFor(() => host.TestVideoPreview?.Window.IsMapped == true, "The video tooltip did not map.");
        GtkVideoPreviewWindow tooltip = host.TestVideoPreview!;
        tooltip.Window.GetSize(out int width, out int height);
        if (tooltip.Window.Decorated || tooltip.Window.TypeHint != Gdk.WindowTypeHint.Tooltip || width != 256 || height != 128) {
            throw new InvalidOperationException($"Video tooltip was {width}×{height}, decorated {tooltip.Window.Decorated}, hint {tooltip.Window.TypeHint}.");
            }
        if (!PreviewWindow.IsWaylandDisplay(parent.Display)) {
            parent.Window.GetOrigin(out int offsetX, out int offsetY);
            parent.GetSize(out int parentWidth, out int parentHeight);
            tooltip.Window.GetPosition(out int x, out int y);
            if (x != offsetX + parentWidth - width - 16 || y != offsetY + parentHeight - height - 16) {
                throw new InvalidOperationException("The video tooltip did not stay inside the owner's bottom-right corner.");
                }
            }
        host.TestWidget<MenuItem>("menu_close").Activate();
        host.TestSettings.Set("Presentation", "Mode", "Window");
        host.TestSettings.Set("Presentation", "WindowType", "Window");
        host.TestSettings.Set("Presentation", "Origin", "Center");
        Console.WriteLine("PASS: GTK3 video dialog and tooltip honor preview settings.");
        }

    private static void TestTypeScript(MainWindow host, string directory) {
        string source = Path.Combine(directory, "preview-source.ts");
        File.WriteAllText(source, "export const value: number = 42;\n");
        string path = Path.Combine(directory, "source.mnx");
        using (Archive archive = new Archive()) {
            archive.Create(new FileInfo(path));
            archive.AddFileNow(new FileInfo(source));
            }
        try {
            if (!host.OpenArchive(path, showErrors: false)) {
                throw new InvalidOperationException("Could not open the TypeScript preview fixture.");
                }
            ArchiveBrowser browser = host.TestBrowser!;
            host.PreviewEntry(browser.Children(browser.Root).Single().Id);
            WaitFor(() => host.TestPreviewCount == 1, "TypeScript did not fall back to a text preview.");
            }
        finally {
            host.TestWidget<MenuItem>("menu_close").Activate();
            }
        }

    private static void TestPlayback(MainWindow host, string path) {
        try {
            if (!host.OpenArchive(path, showErrors: false)) {
                throw new InvalidOperationException("Could not open the video preview fixture archive.");
                }
            ArchiveBrowser browser = host.TestBrowser!;
            Guid video = browser.Children(browser.Root).Single().Id;
            host.PreviewEntry(video);
            WaitFor(() => host.TestVideoPlayback?.PositionMs >= 150, "The video did not start playing.");
            VideoPlayback playback = host.TestVideoPlayback!;
            playback.Pause();
            long paused = playback.PositionMs;
            PumpFor(180);
            if (playback.IsPlaying || playback.PositionMs != paused) {
                throw new InvalidOperationException("Paused video advanced.");
                }
            playback.Seek(900);
            WaitFor(() => playback.PositionMs >= 900, "Seeking a paused video did not publish a frame.");
            if (playback.IsPlaying) {
                throw new InvalidOperationException("Seeking resumed a paused video.");
                }
            playback.Play();
            WaitFor(() => playback.PositionMs >= 1050, "Video did not resume.");
            }
        finally {
            host.TestWidget<MenuItem>("menu_close").Activate();
            }
        if (host.TestVideoPlayback is not null) {
            throw new InvalidOperationException("Closing the archive left its video window open.");
            }
        }

    private static void WaitFor(Func<bool> predicate, string message) {
        Stopwatch wait = Stopwatch.StartNew();
        while (!predicate()) {
            if (wait.ElapsedMilliseconds >= 5000) {
                throw new InvalidOperationException(message);
                }
            PumpFor(15);
            }
        }

    private static void PumpFor(int milliseconds) {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.ElapsedMilliseconds < milliseconds) {
            while (Application.EventsPending()) {
                Application.RunIteration(false);
                }
            Thread.Sleep(5);
            }
        }

    private sealed class TemporaryDirectory : IDisposable {
        internal string Path { get; } = Directory.CreateTempSubdirectory("mnx-gtk-video-").FullName;

        public void Dispose() {
            Directory.Delete(Path, recursive: true);
            }
        }
    }
