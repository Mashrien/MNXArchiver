using Mash.GUI.MNXArchiver.Plugins;

namespace Mash.GUI.MNXArchiver.About;

internal sealed record AboutProviderRow(string Type, string Name, string Formats, string Library, string LibraryFile = "");

/// <summary>Toolkit-independent descriptions of the providers actually loaded by this process.</summary>
internal sealed class AboutModel {
    internal const string SourceUrl = "https://github.com/Mashrien/MNXArchiver";
    internal string GuiVersion => typeof(AboutModel).Assembly.GetName().Version?.ToString() ?? "Unknown";
    internal string FormatVersion => MNX.LibraryInfo.FormatVersion;
    internal string DirectoryPath { get; }
    internal IReadOnlyList<AboutProviderRow> Formats { get; }
    internal IReadOnlyList<AboutProviderRow> Functions { get; }
    internal IReadOnlyList<string> Issues { get; }

    internal AboutModel(PluginCatalog catalog) {
        DirectoryPath = catalog.DirectoryPath;
        Issues = catalog.Issues;
        List<AboutProviderRow> formats = new List<AboutProviderRow> {
            new AboutProviderRow("FMT", "MNX", ".mnx v" + FormatVersion, "Built-in"),
            };
        List<AboutProviderRow> functions = new List<AboutProviderRow> {
            new AboutProviderRow("IMG", "Image PREVIEW", "PNG, JPEG, BMP, GIF, TIFF, ICO", "Built-in"),
            new AboutProviderRow("ANI", "Animation PREVIEW", "GIF", "Built-in"),
            new AboutProviderRow("TXT", "Text PREVIEW", "Plain text and configuration", "Built-in"),
            new AboutProviderRow("SRC", "Source PREVIEW", "Source code", "Built-in"),
            };
        foreach (NativeArchiveModule module in catalog.Modules) {
            PluginFormat format = module.Format;
            formats.Add(Row("FMT", format.Name, format.Extensions, format.Version, module.LibraryName));
            }
        foreach (NativeImageProvider provider in catalog.ImageProviders) {
            ImageProviderDescription description = provider.Description;
            formats.Add(Row("IMG", description.Name, description.Extensions, description.Version, provider.LibraryName));
            // The current image-provider ABI exposes static decoding; it does not advertise animation.
            functions.Add(Row("IMG", description.Name + " PREVIEW", description.Extensions, description.Version, provider.LibraryName));
            }
        foreach (NativeVideoProvider provider in catalog.VideoProviders) {
            VideoProviderDescription description = provider.Description;
            formats.Add(Row("VID", description.Name, description.Extensions, description.Version, provider.LibraryName));
            functions.Add(Row("VID", description.Name + " PREVIEW", description.Extensions, description.Version, provider.LibraryName));
            }
        Formats = formats.AsReadOnly();
        Functions = functions.AsReadOnly();
        }

    private static AboutProviderRow Row(string type, string name, string[] extensions, string version, string library) {
        return new AboutProviderRow(type, name, string.Join(", ", extensions) + " v" + version,
            Path.GetFileNameWithoutExtension(library), library);
        }
    }
