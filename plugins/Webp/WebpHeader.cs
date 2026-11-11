using System.Buffers.Binary;

namespace Mash.UI.MNXFile.GUI.Plugins.Webp;

internal static class WebpHeader {
    internal static ImageProviderStatus Inspect(ReadOnlySpan<byte> encoded, out int width, out int height) {
        width = 0;
        height = 0;
        if (encoded.Length < 12 || !encoded.StartsWith("RIFF"u8)) {
            return ImageProviderStatus.InvalidImage;
            }
        if (!encoded.Slice(8, 4).SequenceEqual("WEBP"u8)) {
            return ImageProviderStatus.InvalidImage;
            }

        uint riffSize = BinaryPrimitives.ReadUInt32LittleEndian(encoded[4..]);
        if (riffSize < 4 || riffSize > encoded.Length - 8) {
            return ImageProviderStatus.InvalidImage;
            }

        int end = (int)riffSize + 8;
        int position = 12;
        int payloadWidth = 0;
        int payloadHeight = 0;
        while (position < end) {
            if (end - position < 8) {
                return ImageProviderStatus.InvalidImage;
                }
            ReadOnlySpan<byte> tag = encoded.Slice(position, 4);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(encoded.Slice(position + 4, 4));
            position += 8;
            long paddedLength = (long)chunkSize + (chunkSize & 1);
            if (paddedLength > end - position) {
                return ImageProviderStatus.InvalidImage;
                }

            ReadOnlySpan<byte> chunk = encoded.Slice(position, (int)chunkSize);
            if (tag.SequenceEqual("VP8X"u8)) {
                if (chunk.Length != 10) {
                    return ImageProviderStatus.InvalidImage;
                    }
                if ((chunk[0] & 2) != 0) {
                    return ImageProviderStatus.AnimatedImage;
                    }
                width = ReadDimension(chunk[4..]);
                height = ReadDimension(chunk[7..]);
                }
            else if (tag.SequenceEqual("VP8 "u8)) {
                if (!ReadLossyDimensions(chunk, out payloadWidth, out payloadHeight)) {
                    return ImageProviderStatus.InvalidImage;
                    }
                }
            else if (tag.SequenceEqual("VP8L"u8)) {
                if (!ReadLosslessDimensions(chunk, out payloadWidth, out payloadHeight)) {
                    return ImageProviderStatus.InvalidImage;
                    }
                }
            else if (tag.SequenceEqual("ANIM"u8) || tag.SequenceEqual("ANMF"u8)) {
                return ImageProviderStatus.AnimatedImage;
                }
            position += (int)paddedLength;
            }

        if (payloadWidth == 0 || payloadHeight == 0) {
            return ImageProviderStatus.InvalidImage;
            }
        if (width == 0) {
            width = payloadWidth;
            height = payloadHeight;
            }
        if (width != payloadWidth || height != payloadHeight) {
            return ImageProviderStatus.InvalidImage;
            }
        if (width > ImageProviderProtocol.MaxDimension || height > ImageProviderProtocol.MaxDimension) {
            return ImageProviderStatus.InvalidImage;
            }
        return ImageProviderStatus.Success;
        }

    private static int ReadDimension(ReadOnlySpan<byte> bytes) {
        return 1 + bytes[0] + (bytes[1] << 8) + (bytes[2] << 16);
        }

    private static bool ReadLossyDimensions(ReadOnlySpan<byte> chunk, out int width, out int height) {
        width = 0;
        height = 0;
        if (chunk.Length < 10 || (chunk[0] & 1) != 0) {
            return false;
            }
        if (chunk[3] != 0x9d || chunk[4] != 0x01 || chunk[5] != 0x2a) {
            return false;
            }
        width = BinaryPrimitives.ReadUInt16LittleEndian(chunk[6..]) & 0x3fff;
        height = BinaryPrimitives.ReadUInt16LittleEndian(chunk[8..]) & 0x3fff;
        return width > 0 && height > 0;
        }

    private static bool ReadLosslessDimensions(ReadOnlySpan<byte> chunk, out int width, out int height) {
        width = 0;
        height = 0;
        if (chunk.Length < 5 || chunk[0] != 0x2f) {
            return false;
            }
        uint dimensions = BinaryPrimitives.ReadUInt32LittleEndian(chunk[1..]);
        if (dimensions >> 29 != 0) {
            return false;
            }
        width = 1 + (int)(dimensions & 0x3fff);
        height = 1 + (int)((dimensions >> 14) & 0x3fff);
        return true;
        }
    }
