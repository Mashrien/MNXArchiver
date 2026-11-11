using Mash.UI.MNXFile.GUI.Plugins;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI.Previews;

internal enum PreviewKind {
    Unsupported,
    Text,
    Image
    }

/// <summary>A bounded plaintext snapshot; never extracted to disk.</summary>
internal sealed class PreviewData : IDisposable {
    internal const int InputLimit = 32 * 1024 * 1024;
    internal const int TextLimit = 2 * 1024 * 1024;
    internal const long DecodedLimit = AppSettings.DefaultDecodedMiB * 1024L * 1024;
    internal byte[] Bytes { get; }
    internal PreviewKind Kind { get; }
    internal bool Truncated { get; }
    internal PreviewData(byte[] bytes, PreviewKind kind, bool truncated) {
        Bytes = bytes;
        Kind = kind;
        Truncated = truncated;
        }
    internal static PreviewKind Classify(string name, AppSettings settings) {
        string extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        string plain = "txt,ini,conf,config,cfg,md,markdown,readme,nfo,log,csv,tsv,json,xml,yaml,yml,toml,properties,srt,sub,env";
        string source = "cs,csx,c,cc,cpp,cxx,h,hpp,m,mm,pl,pm,py,pyw,rpy,js,jsx,ts,tsx,java,kt,kts,rs,go,rb,php,phtml,sh,bash,zsh,fish,ps1,psm1,bat,cmd,lua,r,sql,html,htm,css,scss,sass,less,vue,svelte,swift,dart,vb,fs,fsx,ex,exs,erl,hrl,clj,cljs,scala,groovy,asm,s,pas,pp,d,zig,nim,cmake,make,gradle,proto";
        if (settings.Flag("Text", "Plain", true) && (HasType(extension, plain, settings.Get("Text", "PlainTypes", "")) || name.Equals("readme", StringComparison.OrdinalIgnoreCase) || name.Equals("license", StringComparison.OrdinalIgnoreCase) || name.Equals("changelog", StringComparison.OrdinalIgnoreCase))) {
            return PreviewKind.Text;
            }
        if (settings.Flag("Text", "Source", true) && (HasType(extension, source, settings.Get("Text", "SourceTypes", "")) || name.Equals("Makefile", StringComparison.OrdinalIgnoreCase) || name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))) {
            return PreviewKind.Text;
            }
        if (settings.Flag("Images", "Static", true) && HasType(extension, "png,jpg,jpeg,jpe,bmp,gif,tif,tiff,ico,webp,pnm,ppm,pgm,pbm,tga", settings.Get("Images", "StaticTypes", ""))) {
            return PreviewKind.Image;
            }
        if (settings.Flag("Images", "Static", true) && PluginCatalog.Installed.ImageProviderFor(name) is not null) {
            return PreviewKind.Image;
            }
        if (extension == "gif" && settings.Flag("Images", "Animated")) {
            return PreviewKind.Image;
            }
        return PreviewKind.Unsupported;
        }

    private static bool HasType(string extension, string defaults, string additional) {
        return extension.Length > 0 && (defaults + "," + additional).Split(',').Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

    internal static PreviewData Read(ArchiveFile archive, Guid id, PreviewKind kind, CancellationToken cancellation,
        int bufferLimit = InputLimit) {
        ulong size = archive.GetFile(id)?.SizeOriginal ?? throw new FileNotFoundException("The archived entry no longer exists.");
        return ReadIntoBuffer(size, kind, cancellation, bufferLimit, buffer => {
            using Stream stream = archive.OpenRead(id);
            ReadStream(stream, buffer, cancellation);
            });
        }

    internal static PreviewData Read(OpenedModuleArchive archive, Guid id, PreviewKind kind, CancellationToken cancellation,
        int bufferLimit = InputLimit) {
        ulong size = archive.Browser.Get(id).Bytes;
        return ReadIntoBuffer(size, kind, cancellation, bufferLimit, buffer => archive.ReadEntry(id, buffer, cancellation));
        }

    private static PreviewData ReadIntoBuffer(ulong size, PreviewKind kind, CancellationToken cancellation,
        int bufferLimit, Action<byte[]> fill) {
        bufferLimit = Math.Clamp(bufferLimit, 16 * 1024 * 1024, AppSettings.MaximumPreviewBufferMiB * 1024 * 1024);
        if (size > (ulong)bufferLimit) {
            throw new IOException("Preview skipped: this file exceeds the configured input-buffer limit.");
            }

        int readLimit = kind == PreviewKind.Text ? TextLimit : bufferLimit;
        int length = (int)Math.Min(size, (ulong)readLimit);
        cancellation.ThrowIfCancellationRequested();
        byte[] data = new byte[length];
        try {
            fill(data);
            cancellation.ThrowIfCancellationRequested();
            return new PreviewData(data, kind, size > (ulong)length);
            }
        catch {
            CryptographicOperations.ZeroMemory(data);
            throw;
            }
        }

    private static void ReadStream(Stream stream, byte[] destination, CancellationToken cancellation) {
        int position = 0;
        while (position < destination.Length) {
            cancellation.ThrowIfCancellationRequested();
            int length = Math.Min(65536, destination.Length - position);
            stream.ReadExactly(destination.AsSpan(position, length));
            position += length;
            }
        }

    internal string Text() {
        ReadOnlySpan<byte> bytes = Bytes;
        Encoding encoding = new UTF8Encoding(false, false);
        int offset = 0;
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe })) {
            encoding = Encoding.Unicode;
            offset = 2;
            }
        else if (bytes.StartsWith(new byte[] { 0xfe, 0xff })) {
            encoding = Encoding.BigEndianUnicode;
            offset = 2;
            }
        else if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) {
            offset = 3;
            }
        else if (bytes.Contains((byte)0)) {
            throw new IOException("This file contains binary data, not plain text.");
            }
        string text = encoding.GetString(bytes[offset..]);
        if (text.Contains('\0')) {
            throw new IOException("This file contains binary data, not plain text.");
            }
        if (Truncated) {
            text += "\n\n[Preview limited to the first 2 MiB of text.]";
            }
        return text;
        }

    // GIF decoders may retain a full canvas for every frame. Count before decoding.
    internal static int CheckGif(byte[] data, bool animated, long decodedLimit = DecodedLimit) {
        if (data.Length < 13 || !(data.AsSpan(0, 6).SequenceEqual("GIF89a"u8) || data.AsSpan(0, 6).SequenceEqual("GIF87a"u8))) {
            return 0;
            }
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8));
        int position = 13;
        if ((data[10] & 128) != 0) {
            position += 3 * (1 << ((data[10] & 7) + 1));
            }
        int frames = 0;
        while (position < data.Length) {
            byte marker = data[position++];
            if (marker == 0x3b) {
                break;
                }
            if (marker == 0x21) {
                position++;
                SkipBlocks(data, ref position);
                }
            else if (marker == 0x2c) {
                if (position + 9 > data.Length) {
                    throw new IOException("The GIF frame header is truncated.");
                    }
                int frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 4));
                int frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 6));
                int left = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
                int top = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 2));
                if (frameWidth <= 0 || frameHeight <= 0 || left + frameWidth > width || top + frameHeight > height) {
                    throw new IOException("The GIF frame exceeds the decoded preview budget.");
                    }
                CheckDecodedBudget((long)frameWidth * frameHeight * 4, decodedLimit);
                int flags = data[position + 8];
                position += 9;
                if ((flags & 128) != 0) {
                    position += 3 * (1 << ((flags & 7) + 1));
                    }
                position++; // LZW code size
                SkipBlocks(data, ref position);
                frames++;
                CheckDecodedBudget((long)width * height * 4 * (frames + 2L), decodedLimit);
                }
            else {
                throw new IOException("The GIF contains an invalid block.");
                }
            }
        if (frames > 1 && !animated) {
            throw new IOException("Animated previews are disabled. Enable Animated [EXPERIMENTAL] in Options.");
            }
        return frames;
        }
    internal static void CheckDecodedBudget(long bytes, long limit) {
        if (bytes > limit) {
            long requiredMiB = bytes / (1024 * 1024) + (bytes % (1024 * 1024) == 0 ? 0 : 1);
            throw new IOException($"The image needs at least {requiredMiB:N0} MiB of decoded memory; the configured limit is {limit / (1024 * 1024):N0} MiB. Increase decoded image/animation memory in Options.");
            }
        }
    private static void SkipBlocks(byte[] data, ref int position) {
        while (position < data.Length) {
            int length = data[position++];
            if (length == 0) {
                return;
                }
            position = checked(position + length);
            }
        throw new IOException("The GIF data is truncated.");
        }

    public void Dispose() {
        CryptographicOperations.ZeroMemory(Bytes);
        }
    }
