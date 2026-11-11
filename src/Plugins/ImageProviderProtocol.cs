using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Mash.GUI.MNXArchiver.Plugins;

internal enum ImageProviderStatus : int {
    Success = 0,
    InvalidImage = 1,
    AnimatedImage = 2,
    InvalidDestination = 3,
    DecodeFailed = 4,
    }

[StructLayout(LayoutKind.Sequential)]
internal struct ImageProviderImageInfo {
    internal int Width;
    internal int Height;
    internal int FrameCount;
    internal int TotalPlays;
    }

internal sealed record ImageProviderDescription(string Id, string Name, string Version, string MinHostVersion,
    string MaxHostVersion, string[] Extensions);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ImageProviderDescription))]
internal partial class ImageProviderJson : JsonSerializerContext { }

internal static class ImageProviderProtocol {
    internal const uint AbiVersion = 1;
    internal const int MaxDescriptionBytes = 16 * 1024;
    internal const int MaxDimension = 16384;
    internal const int MaxFrames = 1024;
    }
