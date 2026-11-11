using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mash.UI.MNXFile.GUI.Plugins;

/// <summary>One trusted native archive module. The library stays loaded until process exit.</summary>
internal sealed unsafe class ArchivePlugin {
    private readonly delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte**, int*, int> _request;
    private readonly delegate* unmanaged[Cdecl]<byte*, void> _release;
    private readonly (PluginFormatVersion Version, byte[] Magic)[] _signatures;
    internal string LibraryName { get; }
    internal PluginFormat Format { get; }
    internal PluginFormatVersion Current { get; }
    internal int SignatureLength { get; }

    internal ArchivePlugin(string path) {
        LibraryName = Path.GetFileName(path);
        nint library = NativeLibrary.Load(path, typeof(ArchivePlugin).Assembly,
            DllImportSearchPath.SafeDirectories | DllImportSearchPath.UseDllDirectoryForDependencies);
        delegate* unmanaged[Cdecl]<uint> abi = (delegate* unmanaged[Cdecl]<uint>)NativeLibrary.GetExport(library, "mnx_plugin_abi_version");
        uint version = abi();
        if (version != PluginProtocol.AbiVersion) {
            throw new NotSupportedException($"Plugin ABI {version} is unsupported (expected {PluginProtocol.AbiVersion}).");
            }

        _request = (delegate* unmanaged[Cdecl]<byte*, int, nint, nint, byte**, int*, int>)NativeLibrary.GetExport(library, "mnx_plugin_request");
        _release = (delegate* unmanaged[Cdecl]<byte*, void>)NativeLibrary.GetExport(library, "mnx_plugin_release");
        Format = Invoke(new PluginRequest { Operation = "describe" }).Format
            ?? throw new InvalidDataException("The plugin returned no format descriptor.");
        ValidateFormat(Format);
        Current = Format.Versions.Single(entry => entry.Version == Format.CurrentVersion);
        _signatures = Format.Versions.Select(entry => (entry, Convert.FromHexString(entry.MagicHex))).ToArray();
        SignatureLength = _signatures.Max(entry => entry.Magic.Length);

        // The module independently validates both release ranges. Refusing
        // negotiate leaves it unavailable even if its descriptor looked valid.
        Invoke(new PluginRequest { Operation = "negotiate" });
        }

    private static void ValidateFormat(PluginFormat format) {
        if (!PluginProtocol.Supports(format.Version, PluginProtocol.MinPluginVersion, PluginProtocol.MaxPluginVersion)) {
            throw new NotSupportedException($"Module release {format.Version} is outside {PluginProtocol.MinPluginVersion}–{PluginProtocol.MaxPluginVersion}.");
            }
        if (!PluginProtocol.Supports(PluginProtocol.HostVersion, format.MinHostVersion, format.MaxHostVersion)) {
            throw new NotSupportedException($"Module requires MNXArchiver {format.MinHostVersion}–{format.MaxHostVersion}; this host is {PluginProtocol.HostVersion}.");
            }
        if (string.IsNullOrWhiteSpace(format.Id) || format.Id.Length > 80
            || format.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-')
            || string.IsNullOrWhiteSpace(format.Name) || format.Name.Length > 80 || format.Name.Any(character => char.IsControl(character) || character == '|')
            || format.Extensions is not { Length: > 0 and <= 16 }
            || format.Versions is not { Length: > 0 and <= 32 }) {
            throw new InvalidDataException("Invalid archive plugin descriptor.");
            }
        foreach (string extension in format.Extensions) {
            if (extension is null || extension.Length is < 2 or > 16 || extension[0] != '.' || !extension.Skip(1).All(char.IsAsciiLetterOrDigit)
                || extension.Equals(".mnx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mnq", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("The plugin declared an invalid file extension.");
                }
            }
        HashSet<string> versions = new(StringComparer.Ordinal);
        HashSet<string> signatures = new(StringComparer.OrdinalIgnoreCase);
        foreach (PluginFormatVersion entry in format.Versions) {
            if (entry is null || !Version.TryParse(entry.Version, out _) || !versions.Add(entry.Version)
                || entry.MagicHex is null || entry.MagicHex.Length is < 2 or > 128 || entry.MagicHex.Length % 2 != 0
                || !entry.MagicHex.All(Uri.IsHexDigit) || !signatures.Add(entry.MagicHex)) {
                throw new InvalidDataException("The plugin declared an invalid or duplicate archive signature.");
                }
            ReadOnlySpan<byte> signature = Convert.FromHexString(entry.MagicHex);
            if (signature.StartsWith("MNX"u8) || "MNX"u8.StartsWith(signature)) {
                throw new InvalidDataException("MNX archive signatures are reserved for the built-in engine.");
                }
            }
        if (!versions.Contains(format.CurrentVersion)) {
            throw new InvalidDataException("The plugin's current archive version is not declared.");
            }
        }

    internal PluginMatch? Match(ReadOnlySpan<byte> header) {
        PluginMatch? result = null;
        foreach ((PluginFormatVersion version, byte[] magic) in _signatures) {
            if (header.StartsWith(magic) && (result is null || magic.Length > result.SignatureLength)) {
                result = new PluginMatch(this, version, magic.Length);
                }
            }
        return result;
        }

    internal PluginResponse Invoke(PluginRequest input, CancellationToken cancellation = default) {
        cancellation.ThrowIfCancellationRequested();
        PluginRequest request = input with {
            HostVersion = PluginProtocol.HostVersion,
            MinPluginVersion = PluginProtocol.MinPluginVersion,
            MaxPluginVersion = PluginProtocol.MaxPluginVersion,
            };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(request, PluginJson.Default.PluginRequest);
        if (json.Length > PluginProtocol.MaxMessageBytes) {
            throw new IOException("The archive request is too large.");
            }

        byte* response = null;
        int length = 0;
        GCHandle state = default;
        try {
            nint cancel = 0;
            if (cancellation.CanBeCanceled) {
                state = GCHandle.Alloc(cancellation);
                cancel = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsCancelled;
                }

            fixed (byte* bytes = json) {
                int status = _request(bytes, json.Length, cancel, state.IsAllocated ? GCHandle.ToIntPtr(state) : 0, &response, &length);
                if (status != 0 || response == null || length <= 0 || length > PluginProtocol.MaxMessageBytes) {
                    throw new IOException($"Invalid archive plugin response (status {status}, length {length}).");
                    }
                }
            PluginResponse result = JsonSerializer.Deserialize(new ReadOnlySpan<byte>(response, length), PluginJson.Default.PluginResponse)
                ?? throw new IOException("The archive plugin returned an empty response.");
            if (result.Cancelled) {
                throw new OperationCanceledException(result.Error ?? "Archive operation cancelled.", cancellation);
                }
            if (result.Error is not null) {
                throw new IOException(result.Error);
                }
            return result;
            }
        finally {
            if (response != null) {
                _release(response);
                }
            if (state.IsAllocated) {
                state.Free();
                }
            }
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsCancelled(nint state) {
        try {
            return GCHandle.FromIntPtr(state).Target is CancellationToken token && token.IsCancellationRequested ? 1 : 0;
            }
        catch {
            return 1;
            }
        }
    }

internal sealed record PluginMatch(ArchivePlugin Module, PluginFormatVersion Version, int SignatureLength);
