namespace Mash.UI.MNXFile.GUI;

internal static class Program {
    [STAThread]
    private static int Main(string[] args) {
        ApplicationConfiguration.Initialize();
        try {
            bool smoke = args.Length > 0 && args[0] == "--smoke-test";
            if (smoke && args.Length != 2) {
                throw new ArgumentException("Use --smoke-test DIRECTORY.");
                }
            using MainForm form = smoke
                ? new MainForm(new Settings.AppSettings(Path.Combine(Path.GetFullPath(args[1]), "isolated-settings")))
                : new MainForm();
            int result = 0;
            form.Shown += async (_, _) => {
                try {
                    if (smoke) {
                        await form.RunSmokeTest(args[1]);
                        form.Close();
                        }
                    else if (args.Length == 1 && args[0] == "--preview") {
                        form.LoadPreview();
                        }
                    else if (args.Length > 0 && args[0] is "--help" or "-h") {
                        MessageBox.Show(form, "mnx-gui [ARCHIVE | FILES/FOLDERS...]\nmnx-gui --extract-here [--] ARCHIVE\nmnx-gui --extract-to [--] ARCHIVE\nmnx-gui --preview\nmnx-gui --smoke-test DIRECTORY", "MNX Archive Manager");
                        form.Close();
                        }
                    else if (args.Length > 0) {
                        string? extraction = args[0] is "--extract-here" or "--extract-to" ? args[0] : null;
                        string[] inputs = extraction is null ? args : args.Skip(1).ToArray();
                        if (inputs.Length > 0 && inputs[0] == "--") {
                            inputs = inputs.Skip(1).ToArray();
                            }
                        if (extraction is not null) {
                            if (inputs.Length != 1) {
                                throw new ArgumentException(extraction + " requires exactly one archive filename.");
                                }
                            if (!await form.ExtractArchive(inputs[0], extraction == "--extract-here")) {
                                result = 1;
                                form.Close();
                                }
                            }
                        else if (inputs.Length == 1 && MainForm.ShouldOpenArchive(inputs[0])) {
                            if (!await form.OpenArchive(inputs[0])) {
                                result = 1;
                                form.Close();
                                }
                            }
                        else {
                            await form.OpenInputs(inputs);
                            }
                        }
                    }
                catch (Exception error) {
                    result = 1;
                    if (smoke) {
                        Directory.CreateDirectory(args[1]);
                        System.IO.File.WriteAllText(Path.Combine(args[1], "FAIL.txt"), error.ToString());
                        form.Close();
                        }
                    else {
                        MessageBox.Show(form, error.Message, "MNX Archive Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        form.Close();
                        }
                    }
                };
            Application.Run(form);
            return result;
            }
        catch (Exception error) {
            if (args.Length == 2 && args[0] == "--smoke-test") {
                Directory.CreateDirectory(args[1]);
                System.IO.File.WriteAllText(Path.Combine(args[1], "FAIL.txt"), error.ToString());
                return 1;
                }
            MessageBox.Show(error.Message, "MNX Archive Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
            }
        }
    }
