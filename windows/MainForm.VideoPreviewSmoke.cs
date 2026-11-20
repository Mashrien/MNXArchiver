using Mash.GUI.MNXArchiver.Plugins;
using Mash.GUI.MNXArchiver.Settings;
using MNX;

namespace Mash.GUI.MNXArchiver;

public sealed partial class MainForm {
    private async Task RunVideoPreviewSmoke(string work, List<string> evidence) {
        string? source = Environment.GetEnvironmentVariable("MNX_GUI_VIDEO_SMOKE_INPUT");
        if (string.IsNullOrWhiteSpace(source)) {
            return;
            }

        AppSettings previous = _settings;
        try {
            _settings = previous.Copy();
            _settings.Set("Previews", "Enabled", true);
            _settings.Set("Previews", "Trigger", "Mouse-Over With Delay");
            _settings.Set("Presentation", "Customize", true);
            _settings.Set("Presentation", "WindowType", "Dialog");
            _settings.Set("Presentation", "Origin", "Right of Main Window");
            ResetPreviews();
            string archivePath = Path.Combine(work, "video-preview.mnx");
            string typeScript = Path.Combine(work, "preview-source.ts");
            File.WriteAllText(typeScript, "export const value: number = 42;\n");
            await CreateArchive(new string[] { Path.GetFullPath(source), typeScript }, new CreationSettings(archivePath, ArchiveCompression.None, 0, null, false));
            Guid entry = _browser!.Children(_browser.Root).Single(file => file.Name == Path.GetFileName(source)).Id;
            Guid script = _browser.Children(_browser.Root).Single(file => file.Name == Path.GetFileName(typeScript)).Id;
            await PreviewEntry(script, false);
            Require(_previews.Windows.SingleOrDefault() is PreviewForm, "TypeScript did not fall back to a text preview.");
            ResetPreviews();
            await PreviewEntry(entry, true);
            Require(_previews.Count == 0, "Hover opened a video preview.");
            Require(CanPreview(entry), "Explicit video preview is unavailable. Install the video provider before running this smoke test.");
            await PreviewEntry(entry, false);
            Require(_previews.Windows.SingleOrDefault() is VideoPreviewForm, "Video preview did not open. " + statusMessage.Text);
            VideoPreviewForm video = (VideoPreviewForm)_previews.Windows.Single();
            Require(!video.Settings.Frame.Resizable && !video.MaximizeBox && video.Bounds.Left == Bounds.Right,
                "Video dialog must use fixed chrome and sit next to its owner.");
            Button playPause = (Button)video.Controls.Find("videoPlayPause", true).Single();
            TrackBar timeline = (TrackBar)video.Controls.Find("videoSeek", true).Single();
            Label position = (Label)video.Controls.Find("videoPosition", true).Single();
            await WaitForVideoPreview(() => playPause.Text == "Pause" && timeline.Value > 0, "Video position did not advance.");
            playPause.PerformClick();
            await WaitForVideoPreview(() => playPause.Text == "Play", "Pause did not stop playback.");
            int pausedPosition = timeline.Value;
            await Task.Delay(200);
            Require(timeline.Value == pausedPosition, "The video position changed while paused.");

            string previousPosition = position.Text;
            timeline.Value = timeline.Maximum / 2;
            await WaitForVideoPreview(() => position.Text != previousPosition, "Seeking did not update the displayed position.");
            Require(playPause.Text == "Play", "Seeking resumed paused playback.");
            ImageSurface surface = video.Controls.OfType<ImageSurface>().Single();
            using (Bitmap frame = new Bitmap(surface.Width, surface.Height)) {
                surface.DrawToBitmap(frame, surface.ClientRectangle);
                frame.Save(Path.Combine(work, "video-preview-frame.png"));
                }
            playPause.PerformClick();
            await WaitForVideoPreview(() => playPause.Text == "Pause" && timeline.Value > timeline.Maximum / 2, "Playback did not resume after seeking.");

            await CompactArchive();
            Require(video.IsDisposed && video.StopPlayback().IsCompletedSuccessfully && _previews.Count == 0,
                "Archive compaction left a video preview or entry stream open.");
            await PreviewEntry(entry, false);
            VideoPreviewForm reopened = (VideoPreviewForm)_previews.Windows.Single();
            await OpenArchive(archivePath, showErrors: false);
            Require(reopened.IsDisposed && reopened.StopPlayback().IsCompletedSuccessfully && _previews.Count == 0,
                "Switching archives left a video preview or entry stream open.");
            evidence.Add("PASS: archived video skips hover and supports playback, pause, seek and stream cleanup before archive compaction or switching");
            await CheckVideoPresentation(entry);
            evidence.Add("PASS: video follows dialog, window and tooltip presentation and placement");

            NativeArchiveModule? rpa = PluginCatalog.Installed.Modules.FirstOrDefault(module => module.Format.Id == "renpy.rpa"
                && (module.Current.Actions & PluginActions.StreamEntry) != 0);
            if (rpa is not null) {
                await CreatePluginArchive(rpa, Path.Combine(work, "video-preview.rpa"), new string[] { Path.GetFullPath(source) }, overwrite: false);
                Guid rpaEntry = _browser!.Children(_browser.Root).Single().Id;
                Require(CanPreview(rpaEntry), "The RPA video was not eligible for streaming.");
                await PreviewEntry(rpaEntry, false);
                Require(_previews.Windows.SingleOrDefault() is VideoPreviewForm, "RPA video did not open. " + statusMessage.Text);
                VideoPreviewForm rpaVideo = (VideoPreviewForm)_previews.Windows.Single();
                await DeleteEntries(new Guid[] { rpaEntry }, confirmed: true);
                Require(rpaVideo.IsDisposed && rpaVideo.StopPlayback().IsCompletedSuccessfully && _previews.Count == 0,
                    "Deleting an RPA entry left its video stream open.");
                evidence.Add("PASS: RPA streamed an archived video and closed its reader before deletion");
                }
            }
        finally {
            _settings = previous;
            ResetPreviews();
            }
        }

    private async Task CheckVideoPresentation(Guid entry) {
        _settings.Set("Presentation", "Origin", "Top-Left");
        _settings.Set("Presentation", "WindowType", "Window");
        _settings.Set("Presentation", "LimitSize", true);
        _settings.Set("Presentation", "SizeMode", "Pixels");
        _settings.Set("Presentation", "MaxX", 384);
        _settings.Set("Presentation", "MaxY", 256);
        ResetPreviews();
        await PreviewEntry(entry, false);
        Require(_previews.Windows.SingleOrDefault() is VideoPreviewForm, "The configured video window did not open.");
        VideoPreviewForm window = (VideoPreviewForm)_previews.Windows.Single();
        Require(window.Settings.Frame.Resizable && window.MaximizeBox && window.Size == new Size(384, 256)
            && window.Location == Location, $"Video window placement was {window.Bounds}, owner {Bounds}, resizable {window.Settings.Frame.Resizable}.");
        window.Close();

        _settings.Set("Presentation", "Mode", "Tooltip");
        _settings.Set("Presentation", "Anchor", "Bottom-Right");
        ResetPreviews();
        await PreviewEntry(entry, false);
        Require(_previews.Windows.SingleOrDefault() is VideoPreviewForm, "The configured video tooltip did not open.");
        VideoPreviewForm tooltip = (VideoPreviewForm)_previews.Windows.Single();
        Require(!tooltip.Settings.Frame.ShowChrome && !tooltip.Settings.Frame.Resizable && Bounds.Contains(tooltip.Bounds)
            && tooltip.Bounds.Right == Bounds.Right - 16 && tooltip.Bounds.Bottom == Bounds.Bottom - 16
            && !tooltip.Controls.Find("videoPlayPause", true).Single().Visible,
            "Video tooltip must be control-free and anchored inside the bottom-right of the owner.");
        tooltip.Close();
        _settings.Set("Presentation", "Mode", "Window");
        ResetPreviews();
        }

    private static async Task WaitForVideoPreview(Func<bool> condition, string failure) {
        long start = Environment.TickCount64;
        while (!condition() && Environment.TickCount64 - start < 5000) {
            await Task.Delay(20);
            }
        Require(condition(), failure);
        }
    }
