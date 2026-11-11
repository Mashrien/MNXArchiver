using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>Discovers native extensions and routes archives and previews to their providers.</summary>
internal sealed class PluginCatalog {
    private static readonly Lazy<PluginCatalog> _installed = new(DiscoverInstalled);
    internal static PluginCatalog Installed => _installed.Value;
    internal IReadOnlyList<NativeArchiveModule> Modules { get; }
    internal IReadOnlyList<NativeImageProvider> ImageProviders { get; }
    internal IReadOnlyList<string> Issues { get; }
    internal string DirectoryPath { get; }
    private readonly int _headerLength;

    private PluginCatalog(string directory, List<NativeArchiveModule> modules, List<NativeImageProvider> imageProviders, List<string> issues) {
        DirectoryPath = directory;
        Modules = modules.AsReadOnly();
        ImageProviders = imageProviders.AsReadOnly();
        Issues = issues.AsReadOnly();
        _headerLength = Math.Max(4, modules.Select(module => module.SignatureLength).DefaultIfEmpty().Max());
        }

    private static PluginCatalog DiscoverInstalled() {
        string? root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        // An empty or relative XDG_CONFIG_HOME is ignored. Never discover
        // executable modules relative to the archive or process working directory.
        if (string.IsNullOrEmpty(root) || !Path.IsPathFullyQualified(root)) {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Path.IsPathFullyQualified(profile)) {
                return new PluginCatalog("(unavailable)", [], [], ["No absolute per-user module directory is available."]);
                }
            root = Path.Combine(profile, ".config");
            }
        return Discover(Path.Combine(root, "mnxarchiver", "plugins"));
        }

    internal static PluginCatalog Discover(string directory) {
        directory = Path.GetFullPath(directory);
        List<NativeArchiveModule> modules = new List<NativeArchiveModule>();
        List<NativeImageProvider> imageProviders = new List<NativeImageProvider>();
        List<string> issues = new List<string>();
        try {
            if (Directory.Exists(directory)) {
                string extension = OperatingSystem.IsWindows() ? ".dll" : ".so";
                foreach (string path in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal)) {
                    if (!Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                        }
                    try {
                        LoadExtension(path, modules, imageProviders);
                        }
                    catch (Exception error) {
                        issues.Add(Path.GetFileName(path) + ": " + error.Message);
                        }
                    }
                }
            }
        catch (Exception error) {
            issues.Add(error.Message);
            }
        return new PluginCatalog(directory, modules, imageProviders, issues);
        }

    private static void LoadExtension(string path, List<NativeArchiveModule> modules, List<NativeImageProvider> imageProviders) {
        CheckNativeLibrary(path);
        nint library = NativeLibrary.Load(path, typeof(PluginCatalog).Assembly,
            DllImportSearchPath.SafeDirectories | DllImportSearchPath.UseDllDirectoryForDependencies);
        if (NativeLibrary.TryGetExport(library, "mnx_image_provider_abi_version", out nint versionExport)) {
            AddImageProvider(path, library, versionExport, imageProviders);
            return;
            }

        NativeArchiveModule module = new NativeArchiveModule(path, library);
        if (modules.Any(existing => existing.Format.Id == module.Format.Id)) {
            throw new InvalidDataException("A module with the same ID is already installed.");
            }
        modules.Add(module);
        }

    private static void AddImageProvider(string path, nint library, nint versionExport, List<NativeImageProvider> providers) {
        NativeImageProvider provider = new NativeImageProvider(path, library, versionExport);
        foreach (NativeImageProvider existing in providers) {
            if (existing.Description.Id == provider.Description.Id) {
                throw new InvalidDataException("An image provider with the same ID is already installed.");
                }
            if (existing.Description.Extensions.Intersect(provider.Description.Extensions, StringComparer.OrdinalIgnoreCase).Any()) {
                throw new InvalidDataException("An installed image provider already handles one of these extensions.");
                }
            }
        providers.Add(provider);
        }

    internal NativeImageProvider? ImageProviderFor(string fileName) {
        string extension = Path.GetExtension(fileName);
        return ImageProviders.FirstOrDefault(provider =>
            provider.Description.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

    internal string AboutSummary() {
        List<string> lines = new() { $"Archive modules loaded: {Modules.Count}" };
        foreach (NativeArchiveModule module in Modules) {
            PluginFormat format = module.Format;
            string extensions = string.Join(", ", format.Extensions);
            string versions = string.Join(", ", format.Versions.Select(version => version.Version));
            lines.Add($"  {format.Name} {format.Version} ({module.LibraryName}) — {extensions}, format {versions}");
            }
        lines.Add("");
        lines.Add($"Image providers loaded: {ImageProviders.Count}");
        foreach (NativeImageProvider provider in ImageProviders) {
            ImageProviderDescription description = provider.Description;
            string extensions = string.Join(", ", description.Extensions);
            lines.Add($"  {description.Name} {description.Version} ({provider.LibraryName}) — {extensions}");
            }
        if (Issues.Count > 0) {
            lines.Add("");
            lines.Add("Module discovery issues:");
            foreach (string issue in Issues) {
                lines.Add("  " + issue);
                }
            }
        lines.Add("");
        lines.Add("Plugins directory: " + DirectoryPath);
        lines.Add("Restart MNXArchiver after adding or removing modules.");
        return string.Join(Environment.NewLine, lines);
        }

    internal void OpenDirectory() {
        if (!Path.IsPathFullyQualified(DirectoryPath)) {
            throw new IOException("No per-user plugins directory is available.");
            }
        Directory.CreateDirectory(DirectoryPath);
        using Process? opener = Process.Start(new ProcessStartInfo(DirectoryPath) { UseShellExecute = true });
        }

    private static void CheckNativeLibrary(string path) {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) {
            throw new InvalidDataException("Plugin libraries must be regular files.");
            }

        if (new FileInfo(path).Length < 20) {
            throw new InvalidDataException("Plugin library header is too short.");
            }

        using FileStream stream = File.OpenRead(path);
        Architecture architecture = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows()) {
            using PEReader reader = new(stream);
            Machine machine = architecture switch {
                Architecture.X64 => Machine.Amd64,
                Architecture.Arm64 => Machine.Arm64,
                Architecture.X86 => Machine.I386,
                _ => Machine.Unknown,
                };
            if (reader.PEHeaders.PEHeader is null || reader.HasMetadata
                || (reader.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) == 0
                || machine == Machine.Unknown || reader.PEHeaders.CoffHeader.Machine != machine) {
                throw new InvalidDataException("Expected a native DLL for this process architecture.");
                }
            return;
            }

        Span<byte> header = stackalloc byte[20];
        stream.ReadExactly(header);
        ushort elfMachine = architecture switch {
            Architecture.X64 => 62,
            Architecture.Arm64 => 183,
            Architecture.X86 => 3,
            Architecture.Arm => 40,
            _ => 0,
            };
        if (!header.StartsWith(new byte[] { 0x7f, 0x45, 0x4c, 0x46 }) || header[4] != (IntPtr.Size == 8 ? 2 : 1)
            || header[5] != 1 || header[6] != 1 || BinaryPrimitives.ReadUInt16LittleEndian(header[16..]) != 3
            || elfMachine == 0 || BinaryPrimitives.ReadUInt16LittleEndian(header[18..]) != elfMachine) {
            throw new InvalidDataException("Expected a native ELF shared library for this process architecture.");
            }
        }

    private bool ClaimsExtension(string path) {
        string extension = Path.GetExtension(path);
        return extension.Equals(".rpa", StringComparison.OrdinalIgnoreCase)
            || Modules.Any(module => module.Format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

    internal bool LooksLikeArchive(string path) {
        return !Directory.Exists(path)
            && (ClaimsExtension(path) || (Modules.Count > 0 && FindSignature(path, out _) is not null));
        }

    internal PluginMatch? Match(string path) {
        PluginMatch? result = FindSignature(path, out bool builtIn);
        if (!builtIn && result is null && ClaimsExtension(path)) {
            string details = Issues.Count == 0 ? "" : "\n\n" + string.Join("\n", Issues.Take(5));
            throw new NotSupportedException("No compatible plugin recognizes this archive's header/version. Plugin directory: " + DirectoryPath + details);
            }
        return result;
        }

    private PluginMatch? FindSignature(string path, out bool builtIn) {
        builtIn = false;
        if (!File.Exists(path) || new FileInfo(path).Length == 0) {
            return null;
            }
        Span<byte> header = stackalloc byte[_headerLength];
        int length;
        using (FileStream input = File.OpenRead(path)) {
            length = input.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            }

        // MNX retains ownership even when someone renames a built-in archive.
        builtIn = header[..length].StartsWith("MNX"u8);
        if (builtIn) {
            return null;
            }

        PluginMatch? best = null;
        bool ambiguous = false;
        foreach (NativeArchiveModule module in Modules) {
            PluginMatch? match = module.Match(header[..length]);
            if (match is null) {
                continue;
                }
            if (best is null || match.SignatureLength > best.SignatureLength) {
                best = match;
                ambiguous = false;
                }
            else if (match.SignatureLength == best.SignatureLength) {
                ambiguous = true;
                }
            }
        if (ambiguous) {
            throw new InvalidDataException("More than one installed plugin claims this archive signature.");
            }
        return best;
        }
    }
