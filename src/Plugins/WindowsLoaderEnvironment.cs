using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Mash.GUI.MNXArchiver.Plugins;

/// <summary>Validated entry points and private loader layout for the running Windows process.</summary>
internal sealed unsafe class WindowsLoaderEnvironment {
    private const int DataTableEntrySize = 0x120;
    private const int DdagSize = 0x50;
    private const int BaseIndexNodeOffset = 0xc8;
    private const int ImageBaseOffset = 0x30;
    private const int VerifiedTreeRva = 0x16d4a8;
    private const uint ProcessAttachCalled = 0x00080000;
    private const string VerifiedNtdllHash = "36B1A62E4BF6BF7D68458FFF6E8A33B39F6527EC6A46E55ECDD12CDE83894C8D";

    internal static Lazy<WindowsLoaderEnvironment> Current { get; } = new(Probe);

    private readonly nint _peb;
    private readonly nint _tree;
    private readonly delegate* unmanaged[Stdcall]<nint, nint, byte, nint, void> _insert;
    private readonly delegate* unmanaged[Stdcall]<nint, nint, void> _remove;
    private readonly delegate* unmanaged[Stdcall]<nint, int> _registerTls;
    private readonly delegate* unmanaged[Stdcall]<nint, nint, int> _releaseTls;

    private WindowsLoaderEnvironment(nint module, WindowsTlsSymbols symbols) {
        _tree = module + VerifiedTreeRva;
        _insert = (delegate* unmanaged[Stdcall]<nint, nint, byte, nint, void>)NativeLibrary.GetExport(module, "RtlRbInsertNodeEx");
        _remove = (delegate* unmanaged[Stdcall]<nint, nint, void>)NativeLibrary.GetExport(module, "RtlRbRemoveNode");
        _registerTls = (delegate* unmanaged[Stdcall]<nint, int>)(module + symbols.HandleTlsRva);
        _releaseTls = (delegate* unmanaged[Stdcall]<nint, nint, int>)(module + symbols.ReleaseTlsRva);

        nint* processInfo = stackalloc nint[6];
        int status = WindowsPluginNative.NtQueryInformationProcess(-1, 0, processInfo, 48, 0);
        if (status != 0 || processInfo[1] == 0) {
            throw Unverified("Windows did not return the process loader state.");
            }
        _peb = processInfo[1];
        }

    private static WindowsLoaderEnvironment Probe() {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10) || RuntimeInformation.ProcessArchitecture != Architecture.X64) {
            throw Unverified("Executable plugins require Windows 10 or newer on x64.");
            }
        nint module = WindowsPluginNative.GetModuleHandle("ntdll.dll");
        if (module == 0 || WindowsPluginNative.GetModuleInformation(-1, module, out WindowsPluginNative.ModuleInfo info,
            (uint)sizeof(WindowsPluginNative.ModuleInfo)) == 0 || info.BaseAddress != module) {
            throw Unverified("The loaded ntdll.dll image could not be identified.");
            }

        char[] name = new char[32768];
        uint length;
        fixed (char* buffer = name) {
            length = WindowsPluginNative.GetModuleFileName(module, buffer, (uint)name.Length);
            }
        if (length == 0 || length >= name.Length) {
            throw Unverified("The loaded ntdll.dll path could not be identified.");
            }
        byte[] bytes = File.ReadAllBytes(new string(name, 0, (int)length));
        // The private loader-entry and tree layout was runtime-tested against this exact image.
        if (Convert.ToHexString(SHA256.HashData(bytes)) != VerifiedNtdllHash) {
            throw Unverified("The running ntdll.dll layout has not passed the in-process loader checks.");
            }
        using PEReader image = new PEReader(new MemoryStream(bytes, writable: false));
        PEHeader header = image.PEHeaders.PEHeader ?? throw Unverified("ntdll.dll is missing a PE header.");
        if (header.SizeOfImage != info.ImageSize || image.PEHeaders.CoffHeader.Machine != Machine.Amd64) {
            throw Unverified("The ntdll.dll on disk differs from the loaded image.");
            }

        SectionHeader codeSection = image.PEHeaders.SectionHeaders.Single(section => section.Name == ".text");
        SectionHeader dataSection = image.PEHeaders.SectionHeaders.Single(section => section.Name == ".data");
        int codeLength = Math.Min(codeSection.VirtualSize, codeSection.SizeOfRawData);
        DirectoryEntry exceptions = header.ExceptionTableDirectory;
        ReadOnlySpan<byte> code = LiveRange(module, header.SizeOfImage, codeSection.VirtualAddress, codeLength);
        ReadOnlySpan<byte> unwind = LiveRange(module, header.SizeOfImage, exceptions.RelativeVirtualAddress, exceptions.Size);
        if (!code.SequenceEqual(bytes.AsSpan(codeSection.PointerToRawData, codeLength))
            || !unwind.SequenceEqual(image.GetSectionData(exceptions.RelativeVirtualAddress).GetContent(0, exceptions.Size).AsSpan())
            || VerifiedTreeRva < dataSection.VirtualAddress
            || VerifiedTreeRva + 16 > dataSection.VirtualAddress + dataSection.VirtualSize) {
            throw Unverified("The loaded ntdll.dll does not match the validated loader layout.");
            }
        int acquire = checked((int)(NativeLibrary.GetExport(module, "RtlAcquireSRWLockExclusive") - module));
        int release = checked((int)(NativeLibrary.GetExport(module, "RtlReleaseSRWLockExclusive") - module));
        WindowsTlsSymbols symbols;
        try {
            symbols = WindowsLoaderSignatures.Resolve(code, codeSection.VirtualAddress, dataSection.VirtualAddress,
                dataSection.VirtualSize, unwind, acquire, release);
            }
        catch (NotSupportedException error) {
            throw Unverified(error.Message);
            }

        return new WindowsLoaderEnvironment(module, symbols);
        }

    private static ReadOnlySpan<byte> LiveRange(nint module, int imageSize, int rva, int length) {
        if (rva <= 0 || length <= 0 || (long)rva + length > imageSize) {
            throw Unverified("The Windows loader image contains an invalid section range.");
            }
        return new ReadOnlySpan<byte>((void*)(module + rva), length);
        }

    private static NotSupportedException Unverified(string detail) {
        return new NotSupportedException("Disabled [Windows loader build unverified]. " + detail);
        }

    internal void Register(nint image, int imageSize, int entryRva, ulong preferred, int timestamp, string path, Action attach) {
        Registration result = new Registration(image, imageSize, entryRva, preferred, timestamp, path);
        try {
            Lock(out nuint cookie);
            try {
                nint loader = *(nint*)(_peb + 0x18);
                if (loader == 0 || (*(nuint*)(_tree + sizeof(nint)) & 1) != 0) {
                    throw Unverified("The Windows loader tree is unavailable or encoded.");
                    }
                InsertTree(result.Entry, image);
                result.InTree = true;
                for (int index = 0; index < 3; index++) {
                    AppendList(loader + 0x10 + index * 0x10, result.Entry + index * 0x10);
                    }
                result.InLists = true;
                int status = _registerTls(result.Entry);
                if (status != 0) {
                    throw new BadImageFormatException($"Windows static TLS registration returned 0x{status:X8}.");
                    }
                result.HasTls = true;
                attach();
                result.FinishAttach();
                }
            finally {
                WindowsPluginNative.LdrUnlockLoaderLock(0, cookie);
                }
            }
        catch {
            try {
                Rollback(result);
                }
            catch (Exception cleanupError) {
                throw new RegistrationCleanupException(cleanupError);
                }
            throw;
            }
        }

    internal sealed class RegistrationCleanupException(Exception innerError) :
        InvalidOperationException("Windows could not fully remove the executable plugin's loader registration.", innerError);

    private void Rollback(Registration registration) {
        if (registration.InTree || registration.InLists || registration.HasTls) {
            Lock(out nuint cookie);
            try {
                if (registration.HasTls && _releaseTls(registration.Entry, 0) != 0) {
                    throw new InvalidOperationException("Windows refused to remove a plugin's TLS registration.");
                    }
                if (registration.InLists) {
                    for (int index = 0; index < 3; index++) {
                        RemoveList(registration.Entry + index * 0x10);
                        }
                    }
                if (registration.InTree) {
                    _remove(_tree, registration.Entry + BaseIndexNodeOffset);
                    }
                }
            finally {
                WindowsPluginNative.LdrUnlockLoaderLock(0, cookie);
                }
            }
        registration.Free();
        }

    private static void Lock(out nuint cookie) {
        if (WindowsPluginNative.LdrLockLoaderLock(0, 0, out cookie) != 0) {
            throw Unverified("Windows did not grant the loader lock.");
            }
        }

    private void InsertTree(nint entry, nint image) {
        nint parent = 0;
        nint node = *(nint*)_tree;
        byte right = 0;
        for (int depth = 0; node != 0; depth++) {
            if (depth == 128) {
                throw Unverified("The loader tree does not match the expected shape.");
                }
            parent = node;
            nuint address = *(nuint*)(node - BaseIndexNodeOffset + ImageBaseOffset);
            if (address == (nuint)image) {
                throw new BadImageFormatException("A plugin image already occupies this loader entry.");
                }
            right = (nuint)image > address ? (byte)1 : (byte)0;
            node = *(nint*)(node + right * sizeof(nint));
            }
        _insert(_tree, parent, right, entry + BaseIndexNodeOffset);
        }

    private static void AppendList(nint head, nint node) {
        nint tail = ((nint*)head)[1];
        ((nint*)node)[0] = head;
        ((nint*)node)[1] = tail;
        *(nint*)tail = node;
        ((nint*)head)[1] = node;
        }

    private static void RemoveList(nint node) {
        nint next = ((nint*)node)[0];
        nint previous = ((nint*)node)[1];
        ((nint*)next)[1] = previous;
        *(nint*)previous = next;
        }

    private sealed class Registration {
        private readonly nint _fullName;
        private readonly nint _fileName;
        internal nint Entry { get; }
        internal bool InTree;
        internal bool InLists;
        internal bool HasTls;

        internal Registration(nint image, int imageSize, int entryRva, ulong preferred, int timestamp, string path) {
            string fileName = Path.GetFileName(path);
            if (path.Length > 16000) {
                throw new PathTooLongException();
                }
            try {
                _fullName = Marshal.StringToHGlobalUni(path);
                _fileName = Marshal.StringToHGlobalUni(fileName);
                Entry = (nint)NativeMemory.AllocZeroed(DataTableEntrySize + DdagSize);
                nint ddag = Entry + DataTableEntrySize;
                *(nint*)(Entry + ImageBaseOffset) = image;
                *(nint*)(Entry + 0x38) = image + entryRva;
                *(int*)(Entry + 0x40) = imageSize;
                WriteUnicodeString(Entry + 0x48, _fullName, path.Length);
                WriteUnicodeString(Entry + 0x58, _fileName, fileName.Length);
                // ImageDll, loader list/index membership and completed loader processing.
                // Windows can send thread notifications once process attach succeeds.
                *(uint*)(Entry + 0x68) = 0x0000aac4;
                *(ushort*)(Entry + 0x6c) = ushort.MaxValue;
                *(nint*)(Entry + 0x70) = Entry + 0x70;
                *(nint*)(Entry + 0x78) = Entry + 0x70;
                *(int*)(Entry + 0x80) = timestamp;
                *(nint*)(Entry + 0x98) = ddag;
                ((nint*)ddag)[0] = Entry + 0xa0;
                ((nint*)ddag)[1] = Entry + 0xa0;
                *(nint*)(Entry + 0xa0) = ddag;
                *(nint*)(Entry + 0xa8) = ddag;
                *(int*)(ddag + 0x18) = 1;
                *(int*)(ddag + 0x38) = 9;
                *(ulong*)(Entry + 0xf8) = preferred;
                *(int*)(Entry + 0x10c) = 4;
                *(int*)(Entry + 0x114) = 1;
                }
            catch {
                Free();
                throw;
                }
            }

        internal void FinishAttach() {
            nint ddag = Entry + DataTableEntrySize;
            *(uint*)(ddag + 0x18) = uint.MaxValue;
            *(uint*)(Entry + 0x68) |= ProcessAttachCalled;
            }

        private static void WriteUnicodeString(nint at, nint buffer, int characters) {
            ((ushort*)at)[0] = checked((ushort)(characters * 2));
            ((ushort*)at)[1] = checked((ushort)(characters * 2 + 2));
            *(nint*)(at + 8) = buffer;
            }

        internal void Free() {
            NativeMemory.Free((void*)Entry);
            Marshal.FreeHGlobal(_fileName);
            Marshal.FreeHGlobal(_fullName);
            }
        }
    }
