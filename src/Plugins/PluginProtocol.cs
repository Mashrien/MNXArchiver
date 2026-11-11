using System.Text.Json.Serialization;

namespace Mash.UI.MNXFile.GUI.Plugins;

[Flags]
internal enum PluginActions : uint {
    None = 0,
    Extract = 1 << 0,
    Add = 1 << 1,
    Replace = 1 << 2,
    Create = 1 << 3,
    Delete = 1 << 4,
    }

internal sealed record PluginFormatVersion(string Version, string MagicHex, PluginActions Actions);
internal sealed record PluginFormat(string Id, string Name, string Version, string MinHostVersion, string MaxHostVersion,
    string[] Extensions, PluginFormatVersion[] Versions, string CurrentVersion);
internal sealed record PluginEntry(string Path, long Size);
internal sealed record PluginOutput(string EntryPath, string Destination);

internal sealed record PluginRequest {
    public string Operation { get; init; } = "";
    public string HostVersion { get; init; } = "";
    public string MinPluginVersion { get; init; } = "";
    public string MaxPluginVersion { get; init; } = "";
    public string? ArchivePath { get; init; }
    public string? Folder { get; init; }
    public string[]? Sources { get; init; }
    public string? EntryPath { get; init; }
    public string[]? EntryPaths { get; init; }
    public PluginOutput[]? Outputs { get; init; }
    public bool Overwrite { get; init; }
    }

internal sealed record PluginResponse {
    public string? Error { get; init; }
    public bool Cancelled { get; init; }
    public PluginFormat? Format { get; init; }
    public PluginEntry[]? Entries { get; init; }
    }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PluginRequest))]
[JsonSerializable(typeof(PluginResponse))]
internal partial class PluginJson : JsonSerializerContext { }

internal static class PluginProtocol {
    internal const uint AbiVersion = 1;
    internal const int MaxMessageBytes = 64 * 1024 * 1024;
    internal const int MaxEntries = 250_000;
    internal const int MaxPathCharacters = 4096;
    internal const int MaxPathDepth = 128;
    internal const int MaxPathComponent = 255;
    internal static string HostVersion => typeof(PluginProtocol).Assembly.GetName().Version!.ToString(3);
    internal const string MinPluginVersion = "1.0.0";
    internal const string MaxPluginVersion = "1.0.999";

    internal static bool Supports(string value, string minimum, string maximum) {
        return Version.TryParse(value, out Version? version) && Version.TryParse(minimum, out Version? first)
            && Version.TryParse(maximum, out Version? last) && version >= first && version <= last;
        }
    }
