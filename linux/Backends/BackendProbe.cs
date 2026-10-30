using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mash.UI.MNXFile.GUI.Backends;

/// <summary>Probe in short-lived copies of this executable, never mixing GTK major versions.</summary>
public static class BackendProbe {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetMajorVersion();

    public static BackendAvailability Detect() {
        string mwinError = ProbeProcess("mwin");
        string gtk3Error = ProbeProcess("gtk3");
        return new BackendAvailability(mwinError.Length == 0, gtk3Error.Length == 0, mwinError, gtk3Error);
        }

    private static string ProbeProcess(string backend) {
        try {
            string executable = Environment.ProcessPath ?? throw new IOException("Cannot locate the running MNX executable.");
            ProcessStartInfo launch = new ProcessStartInfo(executable);
            launch.UseShellExecute = false;
            launch.RedirectStandardError = true;
            launch.RedirectStandardOutput = true;
            // Framework-dependent development can be launched as `dotnet app.dll`.
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
                string entry = Assembly.GetEntryAssembly()?.Location ?? "";
                if (entry.Length == 0) {
                    throw new IOException("Cannot locate the managed MNX entry assembly.");
                    }
                launch.ArgumentList.Add(entry);
                }
            launch.ArgumentList.Add("--probe-backend");
            launch.ArgumentList.Add(backend);
            using Process probe = Process.Start(launch) ?? throw new IOException("Could not start a backend probe.");
            if (!probe.WaitForExit(5000)) {
                probe.Kill(true);
                probe.WaitForExit(1000);
                return "Dependency probe timed out after five seconds.";
                }
            string error = probe.StandardError.ReadToEnd().Trim();
            if (probe.ExitCode != 0) {
                return error.Length > 0 ? error : "Dependency probe exited with status " + probe.ExitCode + ".";
                }
            return "";
            }
        catch (Exception error) {
            return "Dependency probe failed: " + error.Message;
            }
        }

    public static int RunChild(string backend) {
        List<IntPtr> libraries = new List<IntPtr>();
        try {
            bool mwin = backend == "mwin";
            if (!mwin && backend != "gtk3") {
                throw new ArgumentException("Probe backend must be mwin or gtk3.");
                }
            string gtk = mwin ? "libgtk-4.so.1" : "libgtk-3.so.0";
            IntPtr handle = Load(gtk, libraries);
            IntPtr symbol = NativeLibrary.GetExport(handle, "gtk_get_major_version");
            GetMajorVersion version = Marshal.GetDelegateForFunctionPointer<GetMajorVersion>(symbol);
            uint expected = mwin ? 4U : 3U;
            if (version() != expected) {
                throw new IOException("Unexpected GTK library major version for " + gtk + ".");
                }
            if (mwin) {
                Load("libSkiaSharp.so", libraries);
                Load("libHarfBuzzSharp.so", libraries);
                }
            return 0;
            }
        catch (Exception error) {
            Console.Error.WriteLine(error.Message);
            return 2;
            }
        finally {
            // Process exit releases GTK's native state. Unloading one major version
            // and trying another in this process would leave unsafe global type state.
            for (int index = libraries.Count - 1; index >= 0; index--) {
                NativeLibrary.Free(libraries[index]);
                }
            }
        }

    private static IntPtr Load(string filename, List<IntPtr> libraries) {
        string besideExecutable = Path.Combine(AppContext.BaseDirectory, filename);
        string developmentAsset = Path.Combine(AppContext.BaseDirectory, "runtimes",
            RuntimeInformation.RuntimeIdentifier, "native", filename);
        IntPtr handle;
        if (!NativeLibrary.TryLoad(besideExecutable, out handle)
            && !NativeLibrary.TryLoad(developmentAsset, out handle)
            && !NativeLibrary.TryLoad(filename, out handle)) {
            throw new DllNotFoundException("Cannot load " + filename + " or one of its dependencies.");
            }
        libraries.Add(handle);
        return handle;
        }
    }
