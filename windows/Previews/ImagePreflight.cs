using System.Buffers.Binary;
using System.IO.Compression;
using Mash.UI.MNXFile.GUI.Previews;

namespace Mash.UI.MNXFile.GUI;

/// <summary>Reads dimensions and frame metadata before handing bytes to GDI+.</summary>
internal readonly struct ImagePreflight {
    internal readonly record struct ImageInfo(int Width, int Height, int Frames, long MemoryCost);
    private const int MaximumDimension = 16384;
    private const int MaximumFrames = 1024;

    private readonly long _decodedLimit;
    private readonly bool _halfResolution;

    private ImagePreflight(long decodedLimit, bool halfResolution) {
        _decodedLimit = decodedLimit;
        _halfResolution = halfResolution;
        }

    internal static ImageInfo Inspect(byte[] data, bool animated, long decodedLimit = PreviewData.DecodedLimit, bool halfResolution = false) {
        ImagePreflight reader = new ImagePreflight(decodedLimit, halfResolution);
        ImageInfo source = reader.ReadImage(data, animated);
        if (!halfResolution) {
            return source;
            }
        // Bounded applies the reduced pixel cost during parsing. Preserve all
        // metadata and multi-page costs accumulated by each format's reader.
        int width = Math.Max(1, (source.Width + 1) / 2);
        int height = Math.Max(1, (source.Height + 1) / 2);
        return new ImageInfo(width, height, source.Frames, source.MemoryCost);
        }

    private ImageInfo ReadImage(byte[] data, bool animated) {
        ReadOnlySpan<byte> bytes = data;
        if (bytes.Length >= 24 && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) {
            return Png(data, 0, data.Length);
            }
        if (bytes.Length >= 13 && (bytes.StartsWith("GIF89a"u8) || bytes.StartsWith("GIF87a"u8))) {
            return Gif(data, animated);
            }
        if (bytes.Length >= 4 && bytes.StartsWith(new byte[] { 255, 216 })) {
            return Jpeg(data);
            }
        if (bytes.Length >= 26 && bytes.StartsWith("BM"u8)) {
            uint headerLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(14, 4));
            if (headerLength == 12) {
                return Bounded(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18, 2)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(20, 2)), 1);
                }
            if (headerLength < 40) {
                throw new IOException("The bitmap header is unsupported.");
                }
            Require(data, 14, checked((int)headerLength));
            uint compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(30, 4));
            if (compression == 4 || compression == 5) {
                throw new IOException("Bitmap previews containing nested JPEG or PNG streams are unsupported.");
                }
            int width = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(18, 4));
            int height = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(22, 4));
            return Bounded(width, checked((int)Math.Abs((long)height)), 1);
            }
        if (bytes.Length >= 6 && bytes.StartsWith(new byte[] { 0, 0, 1, 0 })) {
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
            if (count < 1 || count > MaximumFrames) {
                throw new IOException("The icon has an invalid number of images.");
                }
            Require(data, 6, checked(count * 16));
            long decoded = 0;
            int maximumWidth = 0;
            int maximumHeight = 0;
            for (int index = 0; index < count; index++) {
                int entry = 6 + index * 16;
                int width = data[entry] == 0 ? 256 : data[entry];
                int height = data[entry + 1] == 0 ? 256 : data[entry + 1];
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(entry + 8, 4));
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(entry + 12, 4));
                Require(data, checked((int)offset), checked((int)length));
                long entryCost = Bounded(width, height, 1).MemoryCost;
                // Icon entries may contain PNGs whose own IHDR disagrees with the directory.
                if (length >= 24 && bytes.Slice((int)offset, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) {
                    ImageInfo png = Png(data, (int)offset, (int)length);
                    entryCost = png.MemoryCost;
                    if (png.Width != width || png.Height != height) {
                        throw new IOException("The icon image dimensions do not match its directory.");
                        }
                    }
                else {
                    Require(data, (int)offset, 12);
                    uint dibSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice((int)offset, 4));
                    if (dibSize != 12 && dibSize < 40 || dibSize > length) {
                        throw new IOException("The icon bitmap header is unsupported or truncated.");
                        }
                    if (dibSize >= 40) {
                        Require(data, (int)offset, (int)dibSize);
                        uint compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice((int)offset + 16, 4));
                        if (compression == 4 || compression == 5) {
                            throw new IOException("Icons containing nested compressed bitmap streams are unsupported.");
                            }
                        }
                    int dibWidth = dibSize == 12 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice((int)offset + 4, 2)) : BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice((int)offset + 4, 4));
                    int dibHeight = dibSize == 12 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice((int)offset + 6, 2)) : BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice((int)offset + 8, 4));
                    if (dibWidth != width || Math.Abs((long)dibHeight) != height * 2L) {
                        throw new IOException("The icon bitmap dimensions do not match its directory.");
                        }
                    }
                decoded += entryCost;
                maximumWidth = Math.Max(maximumWidth, width);
                maximumHeight = Math.Max(maximumHeight, height);
                }
            CheckBudget(decoded);
            return new ImageInfo(maximumWidth, maximumHeight, 1, decoded);
            }
        if (bytes.Length >= 8 && (bytes.StartsWith(new byte[] { 73, 73, 42, 0 }) || bytes.StartsWith(new byte[] { 77, 77, 0, 42 }))) {
            return Tiff(data);
            }
        throw new IOException("This image format is unsupported by the Windows previewer. Use PNG, JPEG, BMP, GIF, TIFF or ICO.");
        }

    private ImageInfo Png(byte[] data, int start, int length) {
        Require(data, start, length);
        if (length < 33 || !data.AsSpan(start + 12, 4).SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 8, 4)) != 13) {
            throw new IOException("The PNG header is invalid.");
            }
        int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 16, 4)));
        int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 20, 4)));
        ImageInfo result = Bounded(width, height, 1);
        int position = start + 8;
        int end = checked(start + length);
        long metadataCost = 0;
        while (position < end) {
            if (end - position < 12) {
                throw new IOException("The PNG chunk header is truncated.");
                }
            uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
            if (chunkLength > end - position - 12) {
                throw new IOException("The PNG chunk length is invalid.");
                }
            int count = (int)chunkLength;
            ReadOnlySpan<byte> kind = data.AsSpan(position + 4, 4);
            if (kind.SequenceEqual("acTL"u8)) {
                throw new IOException("Animated PNG previews are unsupported; GIF animation is available.");
                }
            if (kind.SequenceEqual("iCCP"u8) || kind.SequenceEqual("zTXt"u8) || kind.SequenceEqual("iTXt"u8)) {
                ReadOnlySpan<byte> payload = data.AsSpan(position + 8, count);
                int separator = payload.IndexOf((byte)0);
                if (separator < 0 || separator + 2 >= count) {
                    throw new IOException("The PNG metadata header is invalid.");
                    }
                int compressedStart = separator + 2;
                bool compressed = true;
                if (kind.SequenceEqual("iTXt"u8)) {
                    compressed = payload[separator + 1] == 1;
                    compressedStart++;
                    for (int field = 0; field < 2; field++) {
                        int terminator = payload.Slice(compressedStart).IndexOf((byte)0);
                        if (terminator < 0) {
                            throw new IOException("The PNG text metadata header is invalid.");
                            }
                        compressedStart += terminator + 1;
                        }
                    }
                if (compressed) {
                    using MemoryStream source = new MemoryStream(data, position + 8 + compressedStart, count - compressedStart, false);
                    using ZLibStream inflater = new ZLibStream(source, CompressionMode.Decompress);
                    byte[] piece = new byte[8192];
                    while (true) {
                        int read = inflater.Read(piece, 0, piece.Length);
                        if (read == 0) {
                            break;
                            }
                        metadataCost += read * 2L;
                        CheckBudget(result.MemoryCost + metadataCost);
                        }
                    }
                }
            position += count + 12;
            }
        return new ImageInfo(width, height, 1, result.MemoryCost + metadataCost);
        }

    private ImageInfo Gif(byte[] data, bool animated) {
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2));
        Bounded(width, height, 1, gif: true);
        int position = 13;
        if ((data[10] & 128) != 0) {
            position += 3 * (1 << ((data[10] & 7) + 1));
            }
        Require(data, 0, position);
        int frames = 0;
        bool trailer = false;
        while (position < data.Length) {
            byte marker = data[position++];
            if (marker == 0x3b) {
                trailer = true;
                break;
                }
            if (marker == 0x21) {
                Require(data, position, 1);
                position++;
                GifBlocks(data, ref position);
                }
            else if (marker == 0x2c) {
                Require(data, position, 9);
                int left = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position, 2));
                int top = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 2, 2));
                int frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 4, 2));
                int frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 6, 2));
                if (frameWidth < 1 || frameHeight < 1 || left + frameWidth > width || top + frameHeight > height) {
                    throw new IOException("The GIF frame lies outside its canvas.");
                    }
                int flags = data[position + 8];
                position += 9;
                if ((flags & 128) != 0) {
                    position += 3 * (1 << ((flags & 7) + 1));
                    }
                Require(data, position, 1);
                position++;
                GifBlocks(data, ref position);
                frames++;
                Bounded(width, height, frames, gif: true);
                }
            else {
                throw new IOException("The GIF contains an invalid block.");
                }
            }
        if (!trailer || frames == 0) {
            throw new IOException("The GIF is incomplete.");
            }
        if (frames > 1 && !animated) {
            throw new IOException("Animated previews are disabled. Enable Animated GIF in Options.");
            }
        return Bounded(width, height, frames, gif: true);
        }
    private void GifBlocks(byte[] data, ref int position) {
        while (true) {
            Require(data, position, 1);
            int length = data[position++];
            if (length == 0) {
                return;
                }
            Require(data, position, length);
            position += length;
            }
        }
    private ImageInfo Jpeg(byte[] data) {
        int position = 2;
        ImageInfo? result = null;
        while (position < data.Length) {
            if (data[position++] != 255) {
                throw new IOException("The JPEG header is invalid.");
                }
            while (position < data.Length && data[position] == 255) {
                position++;
                }
            Require(data, position, 1);
            int marker = data[position++];
            if (marker == 0xda || marker == 0xd9) {
                break;
                }
            if (marker == 0xd8 || marker == 1 || marker >= 0xd0 && marker <= 0xd7) {
                continue;
                }
            Require(data, position, 2);
            int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position, 2));
            if (length < 2) {
                throw new IOException("The JPEG segment length is invalid.");
                }
            Require(data, position, length);
            if (marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc) {
                if (length < 8) {
                    throw new IOException("The JPEG dimensions are truncated.");
                    }
                int height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 3, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 5, 2));
                ImageInfo dimensions = Bounded(width, height, 1);
                if (result.HasValue && (result.Value.Width != width || result.Value.Height != height)) {
                    throw new IOException("The JPEG contains inconsistent dimensions.");
                    }
                result = dimensions;
                }
            position += length;
            }
        return result ?? throw new IOException("The JPEG dimensions could not be read safely.");
        }
    private ImageInfo Tiff(byte[] data) {
        bool littleEndian = data[0] == 73;
        uint offset = Read32(data, 4, littleEndian);
        HashSet<uint> visited = new HashSet<uint>();
        long decoded = 0;
        int width = 0;
        int height = 0;
        while (offset != 0) {
            if (!visited.Add(offset) || visited.Count > MaximumFrames) {
                throw new IOException("The TIFF page directory is cyclic or too large.");
                }
            int directory = checked((int)offset);
            int count = Read16(data, directory, littleEndian);
            Require(data, directory + 2, checked(count * 12 + 4));
            int pageWidth = 0;
            int pageHeight = 0;
            for (int index = 0; index < count; index++) {
                int entry = directory + 2 + index * 12;
                int tag = Read16(data, entry, littleEndian);
                int type = Read16(data, entry + 2, littleEndian);
                uint values = Read32(data, entry + 4, littleEndian);
                if (tag == 330 || tag == 34665) {
                    // Nested image directories can hide unbudgeted image allocations.
                    throw new IOException("TIFF previews with nested image directories are unsupported.");
                    }
                if (tag == 256 || tag == 257) {
                    if (values != 1 || type != 3 && type != 4) {
                        throw new IOException("The TIFF dimension field is unsupported.");
                        }
                    int dimension = type == 3 ? Read16(data, entry + 8, littleEndian) : checked((int)Read32(data, entry + 8, littleEndian));
                    if (tag == 256) {
                        pageWidth = dimension;
                        }
                    else {
                        pageHeight = dimension;
                        }
                    }
                }
            decoded += Bounded(pageWidth, pageHeight, 1).MemoryCost;
            CheckBudget(decoded);
            width = Math.Max(width, pageWidth);
            height = Math.Max(height, pageHeight);
            offset = Read32(data, directory + 2 + count * 12, littleEndian);
            }
        if (visited.Count == 0) {
            throw new IOException("The TIFF contains no image pages.");
            }
        return new ImageInfo(width, height, 1, decoded);
        }
    private ushort Read16(byte[] bytes, int position, bool littleEndian) {
        Require(bytes, position, 2);
        return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position, 2)) : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position, 2));
        }
    private uint Read32(byte[] bytes, int position, bool littleEndian) {
        Require(bytes, position, 4);
        return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position, 4)) : BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
        }
    private ImageInfo Bounded(int width, int height, int frames, bool gif = false) {
        if (width < 1 || height < 1 || width > MaximumDimension || height > MaximumDimension || frames < 1 || frames > MaximumFrames) {
            throw new IOException("The image dimensions or frame count exceed safe preview limits.");
            }
        int outputWidth = _halfResolution ? Math.Max(1, (width + 1) / 2) : width;
        int outputHeight = _halfResolution ? Math.Max(1, (height + 1) / 2) : height;
        // GIF caches are explicitly 32bpp. Other formats keep the high-depth
        // allowance, plus the full temporary raster some static decoders need.
        int bytesPerPixel = gif ? 4 : 8;
        long memory = checked((long)outputWidth * outputHeight * bytesPerPixel * (frames + 2L));
        if (_halfResolution && !gif) {
            memory += (long)width * height * 8;
            }
        CheckBudget(memory);
        return new ImageInfo(width, height, frames, memory);
        }
    private void CheckBudget(long memory) {
        PreviewData.CheckDecodedBudget(memory, _decodedLimit);
        }
    private void Require(byte[] data, int position, int length) {
        if (position < 0 || length < 0 || position > data.Length - length) {
            throw new IOException("The image header or frame data is truncated.");
            }
        }
    }
