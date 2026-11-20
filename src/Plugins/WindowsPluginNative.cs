using System.Runtime.InteropServices;

namespace Mash.GUI.MNXArchiver.Plugins;

internal static unsafe partial class WindowsPluginNative {
    [StructLayout(LayoutKind.Sequential)]
    internal struct ModuleInfo {
        internal nint BaseAddress;
        internal uint ImageSize;
        internal nint EntryPoint;
        }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string name);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true)]
    internal static partial uint GetModuleFileName(nint module, char* name, uint capacity);

    [LibraryImport("kernel32.dll", EntryPoint = "K32GetModuleInformation")]
    internal static partial int GetModuleInformation(nint process, nint module, out ModuleInfo info, uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW")]
    internal static partial int GetModuleHandleEx(uint flags, nint address, nint* result);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint LoadLibrary(string name, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true)]
    internal static partial nint GetExportOrdinal(nint module, nint ordinal);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint VirtualAlloc(nint address, nuint size, uint allocation, uint protection);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int VirtualFree(nint address, nuint size, uint operation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int VirtualProtect(nint address, nuint size, uint protection, out uint previous);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int FlushInstructionCache(nint process, nint address, nuint size);

    [LibraryImport("kernel32.dll")]
    internal static partial byte RtlAddFunctionTable(nint table, uint count, ulong imageBase);

    [LibraryImport("kernel32.dll")]
    internal static partial byte RtlDeleteFunctionTable(nint table);

    [LibraryImport("ntdll.dll")]
    internal static partial int NtQueryInformationProcess(nint process, int informationClass, nint* information, uint length, nint returned);

    [LibraryImport("ntdll.dll")]
    internal static partial int LdrLockLoaderLock(uint flags, nint disposition, out nuint cookie);

    [LibraryImport("ntdll.dll")]
    internal static partial int LdrUnlockLoaderLock(uint flags, nuint cookie);
    }
