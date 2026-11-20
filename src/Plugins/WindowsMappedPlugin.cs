using System.Buffers.Binary;
using System.ComponentModel;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mash.GUI.MNXArchiver.Plugins;

/// <summary>Maps the designated dual-mode PE in the GUI process and retains it until exit.</summary>
internal sealed unsafe class WindowsMappedPlugin {
    private const uint ReadWrite = 4;
    private const uint CommitReserve = 0x3000;
    private const uint Release = 0x8000;
    private const uint SearchDefaultDirectories = 0x1000;
    private const int MaximumImageSize = 512 * 1024 * 1024;
    private static readonly object _loadGate = new();
    private static WindowsMappedPlugin[] _mapped = [];

    private readonly nint _base;
    private readonly int _imageSize;
    private readonly List<nint> _imports = new();
    private readonly Dictionary<string, nint> _exports = new(StringComparer.Ordinal);
    private nint _functionTable;

    private WindowsMappedPlugin(nint address, int size) {
        _base = address;
        _imageSize = size;
        }

    internal static WindowsMappedPlugin Load(string path) {
        if (new FileInfo(path).Length > MaximumImageSize) {
            throw new BadImageFormatException("The executable plugin is too large.");
            }
        byte[] bytes = File.ReadAllBytes(path);
        using PEReader file = new PEReader(new MemoryStream(bytes, writable: false));
        PEHeader header = CheckHeader(file, bytes.Length);
        lock (_loadGate) {
            nint address = Allocate(header);
            WindowsMappedPlugin plugin = new WindowsMappedPlugin(address, header.SizeOfImage);
            WindowsMappedPlugin[] previous = Volatile.Read(ref _mapped);
            bool started = false;
            bool retain = false;
            try {
                // Keep the distributed EXE intact. Set IMAGE_FILE_DLL in the mapped PE header.
                bytes.AsSpan(0, header.SizeOfHeaders).CopyTo(plugin.Bytes(0, header.SizeOfHeaders));
                int flags = file.PEHeaders.CoffHeaderStartOffset + 18;
                Span<byte> characteristics = plugin.Bytes(flags, 2);
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(characteristics);
                BinaryPrimitives.WriteUInt16LittleEndian(characteristics, (ushort)(value | 0x2000));
                plugin.CopySections(file.PEHeaders.SectionHeaders, bytes);
                plugin.ReadExports(header.ExportTableDirectory, file.PEHeaders.SectionHeaders);
                plugin.RequirePluginExports();
                WindowsLoaderEnvironment environment = WindowsLoaderEnvironment.Current.Value;
                plugin.Relocate(header.BaseRelocationTableDirectory, header.ImageBase);
                plugin.ResolveImports(header.ImportTableDirectory, header.ImportAddressTableDirectory);
                nint[] callbacks = plugin.ReadTlsCallbacks(header.ThreadLocalStorageTableDirectory, file.PEHeaders.SectionHeaders);
                plugin.RegisterUnwind(header.ExceptionTableDirectory, file.PEHeaders.SectionHeaders);
                plugin.ProtectSections(file.PEHeaders.SectionHeaders, header.SizeOfHeaders);

                // GetModuleHandleExW imports from any mapped plugin can now identify
                // their own image. Do not allocate or modify this list in the callback.
                WindowsMappedPlugin[] installed = [.. previous, plugin];
                environment.Register(address, header.SizeOfImage, header.AddressOfEntryPoint,
                    header.ImageBase, file.PEHeaders.CoffHeader.TimeDateStamp, path, () => {
                        Volatile.Write(ref _mapped, installed);
                        started = true;
                        plugin.Attach(header.AddressOfEntryPoint, callbacks);
                        });
                return plugin;
                }
            catch (WindowsLoaderEnvironment.RegistrationCleanupException) {
                retain = true;
                Volatile.Write(ref _mapped, previous);
                throw;
                }
            catch {
                if (started) {
                    Volatile.Write(ref _mapped, previous);
                    }
                throw;
                }
            finally {
                if (!started && !retain) {
                    plugin.ReleaseImage();
                    }
                }
            }
        }

    private void Attach(int entryRva, nint[] callbacks) {
        foreach (nint callback in callbacks) {
            ((delegate* unmanaged[Stdcall]<nint, uint, nint, void>)callback)(_base, 1, 0);
            }
        delegate* unmanaged[Stdcall]<nint, uint, nint, int> entry =
            (delegate* unmanaged[Stdcall]<nint, uint, nint, int>)(_base + entryRva);
        if (entry(_base, 1, 0) == 0) {
            throw new BadImageFormatException("The executable plugin's DLL entry point rejected startup.");
            }
        }

    internal bool TryGetExport(string name, out nint address) {
        return _exports.TryGetValue(name, out address);
        }

    private static PEHeader CheckHeader(PEReader file, int length) {
        PEHeaders headers = file.PEHeaders;
        PEHeader? header = headers.PEHeader;
        if (header is null || file.HasMetadata || headers.CoffHeader.Machine != Machine.Amd64
            || header.Magic != PEMagic.PE32Plus || (headers.CoffHeader.Characteristics & Characteristics.Dll) != 0
            || header.Subsystem != Subsystem.WindowsCui) {
            throw new BadImageFormatException("Expected a native x64 console plugin.");
            }

        if (header.SizeOfImage <= 0 || header.SizeOfImage > MaximumImageSize
            || header.SizeOfHeaders <= 0 || header.SizeOfHeaders > length || header.SizeOfHeaders > header.SizeOfImage
            || header.DelayImportTableDirectory.Size != 0 || ((int)header.DllCharacteristics & 0x4000) != 0
            || header.ThreadLocalStorageTableDirectory.Size < 40
            || !IsCode(headers.SectionHeaders, header.AddressOfEntryPoint)) {
            throw new BadImageFormatException("The executable plugin has an unsupported PE layout.");
            }

        int previousEnd = header.SizeOfHeaders;
        foreach (SectionHeader section in headers.SectionHeaders) {
            int size = Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (section.VirtualAddress < previousEnd || (section.VirtualAddress & 0xfff) != 0
                || section.VirtualSize < 0 || section.SizeOfRawData < 0 || section.PointerToRawData < 0
                || (long)section.VirtualAddress + size > header.SizeOfImage
                || (long)section.PointerToRawData + section.SizeOfRawData > length) {
                throw new BadImageFormatException("The executable plugin contains an invalid section range.");
                }
            previousEnd = section.VirtualAddress + size;
            }
        DirectoryEntry imports = header.ImportAddressTableDirectory;
        DirectoryEntry exceptions = header.ExceptionTableDirectory;
        if (!SectionContains(headers.SectionHeaders, imports.RelativeVirtualAddress, imports.Size,
                SectionCharacteristics.MemRead, SectionCharacteristics.MemExecute)
            || !SectionContains(headers.SectionHeaders, exceptions.RelativeVirtualAddress, exceptions.Size,
                SectionCharacteristics.MemRead, SectionCharacteristics.MemWrite | SectionCharacteristics.MemExecute)) {
            throw new BadImageFormatException("The executable plugin's import or unwind table is outside its expected section.");
            }
        return header;
        }

    private static bool IsCode(IEnumerable<SectionHeader> sections, int rva) {
        return SectionContains(sections, rva, 1, SectionCharacteristics.MemExecute, SectionCharacteristics.MemWrite);
        }

    private static bool SectionContains(IEnumerable<SectionHeader> sections, int rva, int size,
        SectionCharacteristics required, SectionCharacteristics prohibited = 0) {
        if (size <= 0) {
            return false;
            }
        foreach (SectionHeader section in sections) {
            if ((section.SectionCharacteristics & required) == required && (section.SectionCharacteristics & prohibited) == 0
                && rva >= section.VirtualAddress && (long)rva + size <= (long)section.VirtualAddress + section.VirtualSize) {
                return true;
                }
            }
        return false;
        }

    private static nint Allocate(PEHeader header) {
        nint address = WindowsPluginNative.VirtualAlloc((nint)header.ImageBase, (nuint)header.SizeOfImage, CommitReserve, ReadWrite);
        if (address == 0) {
            address = WindowsPluginNative.VirtualAlloc(0, (nuint)header.SizeOfImage, CommitReserve, ReadWrite);
            }
        if (address == 0) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not map the executable plugin.");
            }
        return address;
        }

    private void CopySections(IEnumerable<SectionHeader> sections, byte[] bytes) {
        foreach (SectionHeader section in sections) {
            bytes.AsSpan(section.PointerToRawData, section.SizeOfRawData).CopyTo(Bytes(section.VirtualAddress, section.SizeOfRawData));
            }
        }

    private void ReadExports(DirectoryEntry directory, IEnumerable<SectionHeader> sections) {
        int start = directory.RelativeVirtualAddress;
        ReadOnlySpan<byte> header = Bytes(start, directory.Size);
        if (header.Length < 40) {
            throw new BadImageFormatException("The executable plugin does not declare exports.");
            }
        int addressCount = Int32(header, 20);
        int nameCount = Int32(header, 24);
        if (addressCount is < 1 or > 8192 || nameCount < 0 || nameCount > addressCount) {
            throw new BadImageFormatException("The executable plugin export counts are invalid.");
            }

        ReadOnlySpan<byte> addresses = Bytes(Int32(header, 28), addressCount * 4);
        ReadOnlySpan<byte> names = Bytes(Int32(header, 32), nameCount * 4);
        ReadOnlySpan<byte> ordinals = Bytes(Int32(header, 36), nameCount * 2);
        for (int index = 0; index < nameCount; index++) {
            int ordinal = BinaryPrimitives.ReadUInt16LittleEndian(ordinals[(index * 2)..]);
            if (ordinal >= addressCount) {
                throw new BadImageFormatException("The executable plugin has an invalid export ordinal.");
                }
            int address = Int32(addresses, ordinal * 4);
            if (address <= 0 || address >= _imageSize || (address >= start && address < start + directory.Size)) {
                throw new BadImageFormatException("The executable plugin has an invalid or forwarded export.");
                }
            string name = Name(Int32(names, index * 4));
            if (name.StartsWith("mnx_", StringComparison.Ordinal) && !IsCode(sections, address)) {
                throw new BadImageFormatException("An MNX plugin export does not point to executable code.");
                }
            _exports.Add(name, _base + address);
            }
        }

    private void RequirePluginExports() {
        if (!_exports.ContainsKey("mnx_plugin_dual_mode_v1")) {
            throw new BadImageFormatException("The executable does not declare the MNX dual-mode entry point.");
            }
        if (!_exports.ContainsKey("mnx_plugin_abi_version")
            && !_exports.ContainsKey("mnx_image_provider_abi_version")
            && !_exports.ContainsKey("mnx_video_provider_abi_version")) {
            throw new BadImageFormatException("The executable does not export an MNX plugin ABI.");
            }
        }

    private void Relocate(DirectoryEntry directory, ulong preferred) {
        long delta = (long)((ulong)_base - preferred);
        if (delta == 0) {
            return;
            }
        ReadOnlySpan<byte> relocations = Bytes(directory.RelativeVirtualAddress, directory.Size);
        if (relocations.IsEmpty) {
            throw new BadImageFormatException("The executable plugin has no relocation table.");
            }
        while (!relocations.IsEmpty) {
            if (relocations.Length < 8) {
                throw new BadImageFormatException("The executable plugin relocation block is incomplete.");
                }
            int page = Int32(relocations, 0);
            int size = Int32(relocations, 4);
            if (size < 8 || size > relocations.Length || (size & 1) != 0) {
                throw new BadImageFormatException("The executable plugin relocation block has an invalid size.");
                }
            for (int offset = 8; offset < size; offset += 2) {
                ushort relocation = BinaryPrimitives.ReadUInt16LittleEndian(relocations[offset..]);
                int kind = relocation >> 12;
                if (kind == 0) {
                    continue;
                    }
                if (kind != 10) {
                    throw new BadImageFormatException("The executable plugin uses an unsupported relocation.");
                    }
                Span<byte> pointer = Bytes(checked(page + (relocation & 0xfff)), 8);
                long original = BinaryPrimitives.ReadInt64LittleEndian(pointer);
                BinaryPrimitives.WriteInt64LittleEndian(pointer, unchecked(original + delta));
                }
            relocations = relocations[size..];
            }
        }

    private void ResolveImports(DirectoryEntry directory, DirectoryEntry addressTable) {
        ReadOnlySpan<byte> descriptors = Bytes(directory.RelativeVirtualAddress, directory.Size);
        const int descriptorSize = 20;
        for (int offset = 0; offset + descriptorSize <= descriptors.Length; offset += descriptorSize) {
            ReadOnlySpan<byte> descriptor = descriptors[offset..];
            int nameRva = Int32(descriptor, 12);
            if (nameRva == 0) {
                return;
                }
            string library = Name(nameRva);
            if (library.IndexOfAny(['/', '\\', ':']) >= 0) {
                throw new BadImageFormatException("Executable plugin imports must name a library directly.");
                }
            nint imported = WindowsPluginNative.LoadLibrary(library, 0, SearchDefaultDirectories);
            if (imported == 0) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not load plugin dependency " + library + ".");
                }
            _imports.Add(imported);
            int lookupRva = Int32(descriptor, 0);
            int slotRva = Int32(descriptor, 16);
            ResolveThunks(imported, lookupRva == 0 ? slotRva : lookupRva, slotRva, addressTable);
            }
        if (!descriptors.IsEmpty) {
            throw new BadImageFormatException("The executable plugin import table is incomplete.");
            }
        }

    private void ResolveThunks(nint module, int lookup, int slot, DirectoryEntry addressTable) {
        for (; ; lookup = checked(lookup + 8), slot = checked(slot + 8)) {
            if (slot < addressTable.RelativeVirtualAddress
                || (long)slot + 8 > (long)addressTable.RelativeVirtualAddress + addressTable.Size) {
                throw new BadImageFormatException("An executable plugin import falls outside the address table.");
                }
            ulong import = BinaryPrimitives.ReadUInt64LittleEndian(Bytes(lookup, 8));
            if (import == 0) {
                return;
                }
            nint address;
            if ((import & (1UL << 63)) != 0) {
                address = WindowsPluginNative.GetExportOrdinal(module, (nint)(import & 0xffff));
                }
            else {
                string name = Name(checked((int)import + 2));
                address = name == "GetModuleHandleExW"
                    ? (nint)(delegate* unmanaged[Stdcall]<uint, nint, nint*, int>)&LookupMappedModule
                    : NativeLibrary.GetExport(module, name);
                }
            if (address == 0) {
                throw new EntryPointNotFoundException("The executable plugin imports an unavailable ordinal.");
                }
            BinaryPrimitives.WriteInt64LittleEndian(Bytes(slot, 8), address);
            }
        }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LookupMappedModule(uint flags, nint address, nint* result) {
        const uint PinFromAddress = 5;
        const uint UnchangedRefcountFromAddress = 6;
        if (flags is PinFromAddress or UnchangedRefcountFromAddress && result != null) {
            foreach (WindowsMappedPlugin plugin in Volatile.Read(ref _mapped)) {
                if ((nuint)(address - plugin._base) < (nuint)plugin._imageSize) {
                    *result = plugin._base;
                    return 1;
                    }
                }
            }
        return WindowsPluginNative.GetModuleHandleEx(flags, address, result);
        }

    private void ProtectSections(IEnumerable<SectionHeader> sections, int headerSize) {
        SetProtection(0, headerSize, 2);
        foreach (SectionHeader section in sections) {
            bool executable = (section.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0;
            bool writable = (section.SectionCharacteristics & SectionCharacteristics.MemWrite) != 0;
            bool readable = (section.SectionCharacteristics & SectionCharacteristics.MemRead) != 0;
            if (executable && writable) {
                throw new BadImageFormatException("The executable plugin contains a writable code section.");
                }
            uint protection = executable ? (readable ? 0x20U : 0x10U) : (writable ? 4U : readable ? 2U : 1U);
            SetProtection(section.VirtualAddress, Math.Max(section.VirtualSize, section.SizeOfRawData), protection);
            }
        if (WindowsPluginNative.FlushInstructionCache(-1, _base, (nuint)_imageSize) == 0) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not flush the executable plugin's code.");
            }
        }

    private void SetProtection(int rva, int size, uint protection) {
        if (size > 0 && WindowsPluginNative.VirtualProtect(_base + rva, (nuint)size, protection, out _) == 0) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not protect an executable plugin section.");
            }
        }

    private void RegisterUnwind(DirectoryEntry directory, IEnumerable<SectionHeader> sections) {
        const int runtimeFunctionSize = 12;
        ReadOnlySpan<byte> entries = Bytes(directory.RelativeVirtualAddress, directory.Size);
        if (directory.Size <= 0 || directory.Size % runtimeFunctionSize != 0) {
            throw new BadImageFormatException("The executable plugin unwind table is invalid.");
            }
        for (int offset = 0; offset < entries.Length; offset += runtimeFunctionSize) {
            ReadOnlySpan<byte> entry = entries[offset..];
            int start = Int32(entry, 0);
            int end = Int32(entry, 4);
            int info = Int32(entry, 8) & ~3;
            if (start <= 0 || end <= start
                || !SectionContains(sections, start, end - start, SectionCharacteristics.MemExecute, SectionCharacteristics.MemWrite)
                || !SectionContains(sections, info, 4, SectionCharacteristics.MemRead, SectionCharacteristics.MemWrite)) {
                throw new BadImageFormatException("The executable plugin has an out-of-range unwind record.");
                }
            }
        nint table = _base + directory.RelativeVirtualAddress;
        if (WindowsPluginNative.RtlAddFunctionTable(table, (uint)(directory.Size / runtimeFunctionSize), (ulong)_base) == 0) {
            throw new BadImageFormatException("Windows could not register the executable plugin's unwind table.");
            }
        _functionTable = table;
        }

    private nint[] ReadTlsCallbacks(DirectoryEntry directory, IEnumerable<SectionHeader> sections) {
        const SectionCharacteristics Read = SectionCharacteristics.MemRead;
        const SectionCharacteristics Write = SectionCharacteristics.MemWrite;
        const SectionCharacteristics Execute = SectionCharacteristics.MemExecute;
        if (!SectionContains(sections, directory.RelativeVirtualAddress, 40, Read, Execute)) {
            throw new BadImageFormatException("The executable plugin TLS directory is outside readable data.");
            }
        Span<byte> tls = Bytes(directory.RelativeVirtualAddress, 40);
        long rawStart = BinaryPrimitives.ReadInt64LittleEndian(tls);
        long rawEnd = BinaryPrimitives.ReadInt64LittleEndian(tls[8..]);
        long index = BinaryPrimitives.ReadInt64LittleEndian(tls[16..]);
        long callbacks = BinaryPrimitives.ReadInt64LittleEndian(tls[24..]);
        int template = checked((int)(rawStart - _base));
        int length = checked((int)(rawEnd - rawStart));
        int indexRva = checked((int)(index - _base));
        uint zeroFill = BinaryPrimitives.ReadUInt32LittleEndian(tls[32..]);
        if (length < 0 || (long)length + zeroFill > 64 * 1024
            || !SectionContains(sections, template, Math.Max(length, 1), Read, Execute)
            || !SectionContains(sections, indexRva, 4, Read | Write, Execute)) {
            throw new BadImageFormatException("The executable plugin has an invalid TLS template or index address.");
            }
        if (callbacks == 0) {
            return [];
            }
        List<nint> result = new List<nint>();
        int start = checked((int)(callbacks - _base));
        for (int offset = 0; offset < 64 * 8; offset += 8) {
            int callbackSlot = checked(start + offset);
            if (!SectionContains(sections, callbackSlot, 8, Read, Execute)) {
                throw new BadImageFormatException("The executable plugin TLS callback list is outside readable data.");
                }
            long pointer = BinaryPrimitives.ReadInt64LittleEndian(Bytes(callbackSlot, 8));
            if (pointer == 0) {
                return result.ToArray();
                }
            if (!IsCode(sections, checked((int)(pointer - _base)))) {
                throw new BadImageFormatException("A TLS callback does not point to executable code.");
                }
            result.Add((nint)pointer);
            }
        throw new BadImageFormatException("The executable plugin TLS callback list is too long.");
        }

    private Span<byte> Bytes(int rva, int length) {
        if (rva < 0 || length < 0 || (long)rva + length > _imageSize) {
            throw new BadImageFormatException("The executable plugin contains an out-of-range address.");
            }
        return new Span<byte>((void*)(_base + rva), length);
        }

    private string Name(int rva) {
        if (rva <= 0 || rva >= _imageSize) {
            throw new BadImageFormatException("The executable plugin contains an invalid symbol name.");
            }
        ReadOnlySpan<byte> value = Bytes(rva, Math.Min(_imageSize - rva, 1024));
        int end = value.IndexOf((byte)0);
        if (end <= 0) {
            throw new BadImageFormatException("The executable plugin contains an unterminated symbol name.");
            }
        return Encoding.ASCII.GetString(value[..end]);
        }

    private static int Int32(ReadOnlySpan<byte> source, int offset) {
        return BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
        }

    private void ReleaseImage() {
        if (_functionTable != 0) {
            WindowsPluginNative.RtlDeleteFunctionTable(_functionTable);
            }
        foreach (nint imported in _imports) {
            NativeLibrary.Free(imported);
            }
        WindowsPluginNative.VirtualFree(_base, 0, Release);
        }
    }
