using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mash.GUI.MNXArchiver.Plugins;

internal static partial class WindowsLoaderChecks {
    internal sealed record Sample(string Path, int HandleTlsRva, int ReleaseTlsRva, int TlsLockRva);

    [JsonSerializable(typeof(Sample[]))]
    private sealed partial class SignatureFixtures : JsonSerializerContext;

    internal static void CheckSignatures(string manifest) {
        Sample[] samples = JsonSerializer.Deserialize(File.ReadAllText(manifest), SignatureFixtures.Default.SampleArray)
            ?? throw new InvalidDataException("The Windows signature fixture manifest is empty.");
        foreach (Sample sample in samples) {
            byte[] image = File.ReadAllBytes(sample.Path);
            using PEReader reader = new PEReader(new MemoryStream(image, writable: false));
            SectionHeader text = reader.PEHeaders.SectionHeaders.Single(section => section.Name == ".text");
            SectionHeader data = reader.PEHeaders.SectionHeaders.Single(section => section.Name == ".data");
            DirectoryEntry directory = reader.PEHeaders.PEHeader!.ExceptionTableDirectory;
            ReadOnlySpan<byte> exceptions = reader.GetSectionData(directory.RelativeVirtualAddress).GetContent(0, directory.Size).AsSpan();
            Span<byte> code = image.AsSpan(text.PointerToRawData, Math.Min(text.SizeOfRawData, text.VirtualSize));
            WindowsTlsSymbols symbols = WindowsLoaderSignatures.Resolve(code, text.VirtualAddress,
                data.VirtualAddress, data.VirtualSize, exceptions,
                Export(reader, "RtlAcquireSRWLockExclusive"), Export(reader, "RtlReleaseSRWLockExclusive"));
            if (symbols.HandleTlsRva != sample.HandleTlsRva || symbols.ReleaseTlsRva != sample.ReleaseTlsRva
                || symbols.TlsLockRva != sample.TlsLockRva) {
                throw new InvalidDataException("The scanner disagrees with the matching PDB: " + sample.Path);
                }

            // The caller signature still matches. Break the shared lock reference in the release routine.
            int releaseCode = sample.ReleaseTlsRva - text.VirtualAddress;
            code[releaseCode + 24] ^= 0x10;
            try {
                WindowsLoaderSignatures.Resolve(code, text.VirtualAddress, data.VirtualAddress, data.VirtualSize, exceptions,
                    Export(reader, "RtlAcquireSRWLockExclusive"), Export(reader, "RtlReleaseSRWLockExclusive"));
                throw new InvalidOperationException("The scanner accepted a different lock address.");
                }
            catch (NotSupportedException) { }
            }
        Console.WriteLine($"PASS: {samples.Length} PDB-matched TLS signatures and wrong-lock rejections.");
        }

    private static int Export(PEReader image, string name) {
        DirectoryEntry directory = image.PEHeaders.PEHeader!.ExportTableDirectory;
        BlobReader exports = image.GetSectionData(directory.RelativeVirtualAddress).GetReader();
        exports.Offset = 20;
        int functions = exports.ReadInt32();
        int names = exports.ReadInt32();
        int functionsRva = exports.ReadInt32();
        int namesRva = exports.ReadInt32();
        int ordinalsRva = exports.ReadInt32();
        BlobReader addresses = image.GetSectionData(functionsRva).GetReader(0, checked(functions * 4));
        BlobReader namePointers = image.GetSectionData(namesRva).GetReader(0, checked(names * 4));
        BlobReader ordinals = image.GetSectionData(ordinalsRva).GetReader(0, checked(names * 2));
        for (int index = 0; index < names; index++) {
            int nameRva = namePointers.ReadInt32();
            int ordinal = ordinals.ReadUInt16();
            if (nameRva == 0) {
                continue;
                }
            ReadOnlySpan<byte> text = image.GetSectionData(nameRva).GetContent().AsSpan();
            if (System.Text.Encoding.ASCII.GetString(text[..text.IndexOf((byte)0)]) == name) {
                addresses.Offset = checked(ordinal * 4);
                return addresses.ReadInt32();
                }
            }
        throw new InvalidDataException("Missing fixture export " + name);
        }
    }
