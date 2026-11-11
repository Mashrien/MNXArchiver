using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mash.GUI.MNXArchiver.Plugins.Rpa;

internal static partial class RpaPaths {
    internal static StringComparer FileSystemComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static void ValidateEntryPath(string? path) {
        if (string.IsNullOrEmpty(path) || path.Length > PluginProtocol.MaxPathCharacters)
            throw new InvalidDataException("An RPA entry path is empty or exceeds the filename limit.");

        foreach (char character in path) {
            if (character == '\\' || character == ':' || char.IsControl(character))
                throw new InvalidDataException($"The RPA entry path '{path}' contains an invalid character.");
            }

        int depth = 0;
        foreach (Range range in path.AsSpan().Split('/')) {
            ReadOnlySpan<char> component = path.AsSpan()[range];
            if (++depth > PluginProtocol.MaxPathDepth || component.IsEmpty
                || component.Length > PluginProtocol.MaxPathComponent
                || component.SequenceEqual(".") || component.SequenceEqual(".."))
                throw new InvalidDataException($"The RPA entry path '{path}' is not a safe relative path.");
            }
        }

    internal static string FullFilePath(string? path, string description) {
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"The {description} must be an absolute filesystem path.");

        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Path.GetDirectoryName(fullPath) is null)
            throw new InvalidDataException($"The {description} cannot be a filesystem root.");
        if (OperatingSystem.IsWindows())
            ValidateWindowsPath(fullPath);
        return fullPath;
        }

    private static void ValidateWindowsPath(string path) {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new InvalidDataException("Windows device and extended namespace paths are unsupported.");

        string root = Path.GetPathRoot(path)!;
        ReadOnlySpan<char> relative = path.AsSpan(root.Length);
        foreach (Range range in relative.Split(Path.DirectorySeparatorChar)) {
            ReadOnlySpan<char> component = relative[range];
            if (component.IsEmpty || component[^1] is '.' or ' ' || component.Contains(':'))
                throw new InvalidDataException($"The Windows path '{path}' contains an unsafe component.");

            int dot = component.IndexOf('.');
            ReadOnlySpan<char> stem = (dot >= 0 ? component[..dot] : component).TrimEnd(' ');
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)
                || (stem.Length == 4 && stem[3] is >= '0' and <= '9' or '¹' or '²' or '³'
                    && (stem[..3].Equals("COM", StringComparison.OrdinalIgnoreCase)
                        || stem[..3].Equals("LPT", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException($"The Windows path '{path}' refers to a reserved device.");
            }
        }

    internal static FileAttributes? Inspect(string path) {
        string? current = path;
        FileAttributes? leaf = null;
        while (current is not null) {
            FileAttributes? attributes = ReadAttributes(current);
            if (attributes.HasValue) {
                if ((attributes.Value & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    throw new InvalidDataException($"The path '{current}' refers to a symbolic link or device.");
                if (current != path && (attributes.Value & FileAttributes.Directory) == 0)
                    throw new InvalidDataException($"The path '{current}' is not a directory.");
                }
            if (current == path)
                leaf = attributes;
            current = Path.GetDirectoryName(current);
            }
        return leaf;
        }

    private static FileAttributes? ReadAttributes(string path) {
        try {
            return File.GetAttributes(path);
            }
        catch (FileNotFoundException) {
            return null;
            }
        catch (DirectoryNotFoundException) {
            return null;
            }
        }

    internal static void RequireFile(string path) {
        FileAttributes attributes = Inspect(path) ?? throw new FileNotFoundException($"The file '{path}' does not exist.");
        if ((attributes & FileAttributes.Directory) != 0)
            throw new InvalidDataException($"The path '{path}' is a directory.");
        RequireRegularLinuxFile(path);
        }

    internal static bool CheckOutput(string path, bool overwrite) {
        FileAttributes? attributes = Inspect(path);
        if (attributes.HasValue && ((attributes.Value & FileAttributes.Directory) != 0 || !overwrite))
            throw new IOException($"The destination '{path}' already exists.");
        if (attributes.HasValue)
            RequireRegularLinuxFile(path);
        return attributes.HasValue;
        }

    private static void RequireRegularLinuxFile(string path) {
        if (!OperatingSystem.IsLinux())
            return;

        const uint queryType = 1; // STATX_TYPE
        const ushort typeMask = 0xf000;
        const ushort regularFile = 0x8000;
        LinuxFileStatus status = ReadLinuxStatus(path, queryType);
        if ((status.Mode & typeMask) != regularFile)
            throw new InvalidDataException($"The path '{path}' is not a regular file.");
        }

    internal static bool HasSameFileIdentity(string first, string second) {
        if (OperatingSystem.IsWindows()) {
            WindowsFileIdentity leftIdentity = ReadWindowsIdentity(first);
            WindowsFileIdentity rightIdentity = ReadWindowsIdentity(second);
            return leftIdentity.Volume == rightIdentity.Volume && leftIdentity.First == rightIdentity.First
                && leftIdentity.Second == rightIdentity.Second;
            }
        if (!OperatingSystem.IsLinux())
            return false;

        const uint queryInode = 0x100; // STATX_INO
        LinuxFileStatus left = ReadLinuxStatus(first, queryInode);
        LinuxFileStatus right = ReadLinuxStatus(second, queryInode);
        return left.Inode == right.Inode && left.DeviceMajor == right.DeviceMajor
            && left.DeviceMinor == right.DeviceMinor;
        }

    private static LinuxFileStatus ReadLinuxStatus(string path, uint fields) {
        const int currentDirectory = -100; // AT_FDCWD
        const int doNotFollowLinks = 0x100; // AT_SYMLINK_NOFOLLOW
        if (Statx(currentDirectory, path, doNotFollowLinks, fields, out LinuxFileStatus status) != 0
            || (status.ValidFields & fields) != fields)
            throw new IOException($"Could not inspect the file '{path}'.");
        return status;
        }

    // The Linux statx ABI uses these offsets on all supported architectures.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxFileStatus {
        [FieldOffset(0)]
        internal uint ValidFields;
        [FieldOffset(28)]
        internal ushort Mode;
        [FieldOffset(32)]
        internal ulong Inode;
        [FieldOffset(136)]
        internal uint DeviceMajor;
        [FieldOffset(140)]
        internal uint DeviceMinor;
        }

    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(int directory, string path, int flags, uint mask, out LinuxFileStatus status);

    private static WindowsFileIdentity ReadWindowsIdentity(string path) {
        const uint shareReadWriteDelete = 7;
        const uint openExisting = 3;
        const uint openReparsePoint = 0x00200000;
        const int fileIdInfo = 18;
        string extendedPath = path.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + path[2..]
            : @"\\?\" + path;
        using SafeFileHandle file = OpenWindowsFile(extendedPath, 0, shareReadWriteDelete, 0,
            openExisting, openReparsePoint, 0);
        if (file.IsInvalid || !GetWindowsFileInformation(file, fileIdInfo, out WindowsFileIdentity identity,
                (uint)Marshal.SizeOf<WindowsFileIdentity>()))
            throw new IOException($"Could not inspect the identity of '{path}'.");
        return identity;
        }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileIdentity {
        internal ulong Volume;
        internal ulong First;
        internal ulong Second;
        }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle OpenWindowsFile(string path, uint access, uint shareMode,
        nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowsFileInformation(SafeFileHandle file, int informationClass,
        out WindowsFileIdentity information, uint informationSize);
    }
