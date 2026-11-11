namespace Mash.UI.MNXFile.GUI.Plugins;

internal static class ArchiveModuleDescriptor {
    internal static void Validate(PluginFormat format) {
        ValidateReleaseRanges(format);
        ValidateIdentity(format);
        ValidateExtensions(format.Extensions);
        ValidateSignatures(format.Versions, format.CurrentVersion);
        }

    private static void ValidateReleaseRanges(PluginFormat format) {
        if (!PluginProtocol.Supports(format.Version, PluginProtocol.MinPluginVersion, PluginProtocol.MaxPluginVersion)) {
            throw new NotSupportedException($"Module release {format.Version} is outside {PluginProtocol.MinPluginVersion}–{PluginProtocol.MaxPluginVersion}.");
            }
        if (!PluginProtocol.Supports(PluginProtocol.HostVersion, format.MinHostVersion, format.MaxHostVersion)) {
            throw new NotSupportedException($"Module requires MNXArchiver {format.MinHostVersion}–{format.MaxHostVersion}; this host is {PluginProtocol.HostVersion}.");
            }
        }

    private static void ValidateIdentity(PluginFormat format) {
        if (string.IsNullOrWhiteSpace(format.Id) || format.Id.Length > 80) {
            throw new InvalidDataException("The archive module ID is missing or too long.");
            }
        foreach (char character in format.Id) {
            if (char.IsAsciiLetterOrDigit(character) || character == '.' || character == '-') {
                continue;
                }
            throw new InvalidDataException("The archive module ID may contain letters, digits, periods and hyphens.");
            }

        if (string.IsNullOrWhiteSpace(format.Name) || format.Name.Length > 80) {
            throw new InvalidDataException("The archive module name is missing or too long.");
            }
        if (format.Name.Any(character => char.IsControl(character) || character == '|')) {
            throw new InvalidDataException("The archive module name contains an invalid character.");
            }
        }

    private static void ValidateExtensions(string[] extensions) {
        if (extensions is null || extensions.Length is < 1 or > 16) {
            throw new InvalidDataException("The archive module must declare between 1 and 16 extensions.");
            }
        foreach (string extension in extensions) {
            if (extension is null || extension.Length is < 2 or > 16) {
                throw new InvalidDataException("The module declared an invalid file extension.");
                }
            if (extension[0] != '.' || !extension.Skip(1).All(char.IsAsciiLetterOrDigit)) {
                throw new InvalidDataException("A module extension must start with a period followed by letters or digits.");
                }
            if (extension.Equals(".mnx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".mnq", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("The .mnx and .mnq extensions are reserved.");
                }
            }
        }

    private static void ValidateSignatures(PluginFormatVersion[] entries, string currentVersion) {
        if (entries is null || entries.Length is < 1 or > 32) {
            throw new InvalidDataException("The archive module must declare between 1 and 32 archive versions.");
            }

        HashSet<string> versions = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PluginFormatVersion entry in entries) {
            if (entry is null || !Version.TryParse(entry.Version, out _)) {
                throw new InvalidDataException("The module declared an invalid archive version.");
                }
            if (!versions.Add(entry.Version)) {
                throw new InvalidDataException("The module declared a duplicate archive version.");
                }
            ValidateSignature(entry.MagicHex);
            if (!signatures.Add(entry.MagicHex)) {
                throw new InvalidDataException("The module declared a duplicate archive signature.");
                }
            }
        if (!versions.Contains(currentVersion)) {
            throw new InvalidDataException("The module's current archive version is not declared.");
            }
        }

    private static void ValidateSignature(string hex) {
        if (hex is null || hex.Length is < 2 or > 128) {
            throw new InvalidDataException("An archive signature must contain between 1 and 64 bytes.");
            }
        if (hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit)) {
            throw new InvalidDataException("The module's archive signature must use pairs of hexadecimal digits.");
            }

        ReadOnlySpan<byte> signature = Convert.FromHexString(hex);
        if (signature.StartsWith("MNX"u8) || "MNX"u8.StartsWith(signature)) {
            throw new InvalidDataException("MNX archive signatures are reserved for the built-in engine.");
            }
        }
    }
