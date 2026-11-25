using System.Security.Cryptography;

namespace Mash.GUI.MNXArchiver.About;

/// <summary>Content identities used across a full process restart, without loading a module twice.</summary>
internal static class PluginSnapshot {
    internal const int MaximumFiles = 512;

    internal static Dictionary<string, string> Capture(string directory, CancellationToken cancellation = default) {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(directory)) {
            return result;
            }
        foreach (string path in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal)) {
            cancellation.ThrowIfCancellationRequested();
            if (!Path.GetExtension(path).Equals(".mnxp", StringComparison.OrdinalIgnoreCase)) {
                continue;
                }
            if (result.Count >= MaximumFiles) {
                throw new IOException("The plugins directory contains more than 512 plugins.");
                }
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[65536];
            int count;
            while ((count = stream.Read(buffer)) > 0) {
                cancellation.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, count);
                }
            result.Add(Path.GetFileName(path), Convert.ToHexString(hash.GetHashAndReset()));
            }
        return result;
        }

    internal static bool IsUpdated(string libraryName, IReadOnlyDictionary<string, string>? before, IReadOnlyDictionary<string, string> current) {
        return before is not null && current.TryGetValue(libraryName, out string? hash)
            && (!before.TryGetValue(libraryName, out string? previous) || hash != previous);
        }

    internal static bool Differs(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second) {
        return first.Count != second.Count || second.Any(pair => !first.TryGetValue(pair.Key, out string? hash) || hash != pair.Value);
        }
    }
