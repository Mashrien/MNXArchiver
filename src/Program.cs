using Gtk;
using Mash.IO.Format.MNX.Archive.Codecs;
using Mash.IO.Format.MNX.Archive;
using Mash.IO.Format.MNX;

namespace Mash.UI.MNXFile.GUI;

internal static class Program {
    internal static bool IsArchiveOpenRequest(string[] paths) {
        return paths.Length == 1 && (Path.GetExtension(paths[0]).Equals(".mnx", StringComparison.OrdinalIgnoreCase) || FileTransfer.IsArchive(paths[0]));
        }

    [STAThread]
    private static int Main(string[] args) {
        return Run(args);
        }

    internal static int Run(string[] args) {
        if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h")) {
            Console.WriteLine("mnx-gui [ARCHIVE | FILES/FOLDERS...]");
            Console.WriteLine("mnx-gui --extract-here [--] ARCHIVE");
            Console.WriteLine("mnx-gui --extract-to [--] ARCHIVE");
            Console.WriteLine("mnx-gui --preview | --smoke-test [SCREENSHOT_DIRECTORY]");
            return 0;
            }

        bool smokeTest = args.Length > 0 && args[0] == "--smoke-test";
        bool preview = args.Length == 1 && args[0] == "--preview";
        string? extraction = null;
        string[] inputs = [];
        try {
            if (smokeTest) {
                if (args.Length > 2) {
                    throw new ArgumentException("--smoke-test accepts one screenshot directory.");
                    }
                }
            else if (!preview) {
                if (args.Length > 0 && args[0] is "--extract-here" or "--extract-to") {
                    extraction = args[0];
                    args = args.Skip(1).ToArray();
                    }

                if (args.Length > 0 && args[0] == "--") {
                    args = args.Skip(1).ToArray();
                    }

                if (extraction is not null && args.Length != 1) {
                    throw new ArgumentException(extraction + " requires exactly one archive filename.");
                    }

                inputs = args.Select(FileTransfer.LocalPath).ToArray();
                }

            Gtk3TypeResolver.Install();
            Application.Init();
            // Set desktop identity after GtkSharp's initialization, before any
            // windows are created.
            GLib.Global.ProgramName = "org.mash.MNXFile";
            GLib.Global.ApplicationName = "MNX Archive Manager";
            Gdk.Global.ProgramClass = "org.mash.MNXFile";
            using MainWindow window = smokeTest
                ? new MainWindow(Settings.AppSettings.Load(Path.Combine(args.Length > 1 ? args[1] : Path.GetTempPath(), "isolated-settings")))
                : new MainWindow();
            window.Show();
            if (smokeTest) {
                string screenshots = args.Length > 1 ? args[1] : Path.GetTempPath();
                window.RunSmokeTest(screenshots);
                Console.WriteLine("PASS: GTK navigation, drag/drop, extraction progress and cancellation, archive creation and recovery");
                return 0;
                }

            if (preview) {
                window.LoadPreview();
                }
            else if (extraction is not null) {
                window.ExtractArchive(inputs[0], extraction == "--extract-here");
                }
            else if (inputs.Length > 0) {
                if (IsArchiveOpenRequest(inputs)) {
                    if (!window.OpenArchive(inputs[0])) {
                        return 1;
                        }
                    }
                else {
                    window.OpenInputs(inputs);
                    }
                }

            Application.Run();
            return 0;
            }
        catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
            }
        finally {
            Icons.OwnedIcons.Dispose();
            }
        }
    }
