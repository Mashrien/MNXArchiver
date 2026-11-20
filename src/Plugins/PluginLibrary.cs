using System.Runtime.InteropServices;

namespace Mash.GUI.MNXArchiver.Plugins;

/// <summary>Resolves ABI exports from an OS-loaded library or a mapped Windows plugin.</summary>
internal readonly struct PluginLibrary {
    private readonly nint _library;
    private readonly WindowsMappedPlugin? _executable;

    internal PluginLibrary(string path, bool windowsExecutable) {
        if (windowsExecutable) {
            _executable = WindowsMappedPlugin.Load(path);
            }
        else {
            _library = NativeLibrary.Load(path, typeof(PluginLibrary).Assembly,
                DllImportSearchPath.SafeDirectories | DllImportSearchPath.UseDllDirectoryForDependencies);
            }
        }

    internal bool TryGetExport(string name, out nint address) {
        if (_executable is not null) {
            return _executable.TryGetExport(name, out address);
            }
        return NativeLibrary.TryGetExport(_library, name, out address);
        }

    internal nint GetExport(string name) {
        if (!TryGetExport(name, out nint address)) {
            throw new EntryPointNotFoundException("The plugin does not export " + name + ".");
            }
        return address;
        }
    }
