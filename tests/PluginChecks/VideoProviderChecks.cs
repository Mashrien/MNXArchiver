using System.Buffers.Binary;
using Mash.GUI.MNXArchiver.Plugins;
using MNX;

internal static class VideoProviderChecks {
    internal static void Run(string libraryDirectory, string fixture) {
        PluginCatalog catalog = PluginCatalog.Discover(libraryDirectory);
        NativeVideoProvider provider = catalog.VideoProviderFor(Path.GetFileName(fixture).ToUpperInvariant())
            ?? throw new InvalidDataException("The installed video provider was not discovered. " + string.Join(" ", catalog.Issues));
        using (NativeVideoStream video = provider.Open(File.OpenRead(fixture), halfResolution: false, CancellationToken.None)) {
            CheckFrames(video);
            }

        string workingDirectory = Directory.CreateTempSubdirectory("mnx-video-checks-").FullName;
        try {
            using Archive archive = new Archive();
            archive.Options.Compression = ArchiveCompression.Snappy;
            archive.Create(new FileInfo(Path.Combine(workingDirectory, "video.mnx")), "test-password");
            archive.AddFileNow(new FileInfo(fixture));
            Guid entry = archive.Files.Single().FileID;
            using NativeVideoStream video = provider.Open(archive.OpenRead(entry), halfResolution: true, CancellationToken.None);
            if (video.Info.Width != 80 || video.Info.Height != 45) {
                throw new InvalidDataException($"Half resolution output is {video.Info.Width}×{video.Info.Height}.");
                }
            CheckFrames(video);
            }
        finally {
            Directory.Delete(workingDirectory, recursive: true);
            }

        using MemoryStream wrongHeader = new MemoryStream("not a valid media header"u8.ToArray());
        try {
            using NativeVideoStream ignored = provider.Open(wrongHeader, halfResolution: false, CancellationToken.None);
            throw new Exception("The provider accepted bytes without a supported container signature.");
            }
        catch (IOException) {
            if (wrongHeader.CanRead) {
                throw new Exception("A failed provider open must dispose the input.");
                }
            }
        Console.WriteLine("PASS: native video discovery, clear and encrypted compressed inputs, half resolution, PTS, seek and bad-header refusal.");
        }

    private static void CheckFrames(NativeVideoStream video) {
        int stride = video.Info.Width * 4 + 32;
        byte[] pixels = new byte[stride * video.Info.Height];
        if (video.Info.DurationMs < 1400 || video.Info.FpsNumerator <= 0 || video.Info.FpsDenominator <= 0) {
            throw new InvalidDataException("The video did not report valid duration or advisory frame rate.");
            }
        long previous = -1;
        for (int index = 0; index < 6; index++) {
            if (!video.DecodeNextFrame(pixels, stride, out long position) || position <= previous) {
                throw new InvalidDataException("Presentation timestamps failed to advance.");
                }
            previous = position;
            }
        if (BinaryPrimitives.ReadUInt32LittleEndian(pixels.AsSpan(0, 4)) >> 24 != 255) {
            throw new InvalidDataException("The video frame did not use opaque BGRA.");
            }
        video.Seek(1100);
        if (!video.DecodeNextFrame(pixels, stride, out long soughtPosition) || soughtPosition < 1100) {
            throw new InvalidDataException("Seek did not land at or beyond the target time.");
            }
        video.Seek(0);
        if (!video.DecodeNextFrame(pixels, stride, out long beginning) || beginning != 0) {
            throw new InvalidDataException("Backward seek did not return to the start.");
            }
        video.Seek(video.Info.DurationMs);
        if (!video.DecodeNextFrame(pixels, stride, out long ending) || ending < video.Info.DurationMs - 500) {
            throw new InvalidDataException("Seeking to the endpoint did not return the final frame.");
            }
        }
    }
