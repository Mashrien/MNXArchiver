using Mash.GUI.MNXArchiver.Icons;
using Gtk;
using Mash.GUI.MNXArchiver.Settings;
using MNX;
using DiskFile = System.IO.File;

namespace Mash.GUI.MNXArchiver;

internal sealed record ArchiveCreationSettings(string Path, ArchiveCompression Compression, int Level, string? Password, bool Overwrite);
/// <summary>The small setup form used by file-manager creation and the New action.</summary>
internal sealed class ArchiveCreationDialog : IDisposable {
    internal Dialog Window { get; }
    internal Entry Output { get; } = new() {
        Hexpand = true,
        WidthChars = 48
        };
    internal CheckButton Compress { get; } = new("Enable compression") {
        Active = true
        };
    internal ComboBoxText Codec { get; } = new();
    internal SpinButton Level { get; } = new(1, 9, 1) {
        Value = 4,
        Numeric = true
        };
    internal CheckButton Encrypt { get; } = new("Enable encryption");
    internal Entry Password { get; } = new() {
        Visibility = false,
        WidthChars = 30
        };
    internal Entry Confirmation { get; } = new() {
        Visibility = false,
        WidthChars = 30
        };

    private readonly string[] _inputs;
    internal ArchiveCreationDialog(Gtk.Window parent, string[] inputs, AppSettings? defaults = null, string? defaultPassword = null) {
        _inputs = inputs;
        Window = new Dialog(
            "Add to MNX Archive",
            parent,
            DialogFlags.Modal,
            "Cancel",
            ResponseType.Cancel,
            "Create Archive",
            ResponseType.Accept
            ) {
            DestroyWithParent = true,
            Resizable = false
                };
        GtkWindowAppearance.Apply(Window);
        Window.ContentArea.BorderWidth = 16;
        Grid grid = new Grid {
            RowSpacing = 12,
            ColumnSpacing = 12
            };
        Window.ContentArea.PackStart(grid, true, true, 0);
        string first = Path.TrimEndingDirectorySeparator(inputs[0]);
        string suggested = Directory.Exists(first) ? Path.GetFileName(first) : Path.GetFileNameWithoutExtension(first);
        if (string.IsNullOrEmpty(suggested)) {
            suggested = "Archive";
            }

        // Appending an extension must not overflow the source OS's component limit.
        while (System.Text.Encoding.UTF8.GetByteCount(suggested + ".mnx") > 255 && !OperatingSystem.IsWindows() || (suggested.Length + 4 > 255 && OperatingSystem.IsWindows())) {
            suggested = suggested[..(suggested.Length - (char.IsLowSurrogate(suggested[suggested.Length - 1]) ? 2 : 1))];
            }

        Output.Text = Path.Combine((Path.GetDirectoryName(first) ?? first), suggested + ".mnx");
        if (DiskFile.Exists(Output.Text) && inputs.Contains(Output.Text)) {
            Output.Text = Path.Combine(Path.GetDirectoryName(first)!, "Archive.mnx");
            }

        Button browse = new Button("Browse…");
        browse.Clicked += (_, _) => Browse();
        grid.Attach(new Label("Archive filename:") { Xalign = 0 }, 0, 0, 1, 1);
        grid.Attach(Output, 1, 0, 2, 1);
        grid.Attach(browse, 3, 0, 1, 1);
        grid.Attach(Compress, 0, 1, 2, 1);
        Codec.Append("Snappy", "Snappy");
        Codec.Append("LZ4", "LZ4");
        Codec.Append("Brotli", "Brotli");
        Codec.ActiveId = "Snappy";
        grid.Attach(Codec, 2, 1, 2, 1);
        grid.Attach(new Label("Compression level:") { Xalign = 0 }, 0, 2, 2, 1);
        grid.Attach(Level, 2, 2, 2, 1);
        grid.Attach(Encrypt, 0, 3, 4, 1);
        grid.Attach(new Label("Password:") { Xalign = 0 }, 0, 4, 1, 1);
        grid.Attach(Password, 1, 4, 3, 1);
        grid.Attach(new Label("Repeat password:") { Xalign = 0 }, 0, 5, 1, 1);
        grid.Attach(Confirmation, 1, 5, 3, 1);
        if (defaults is not null) {
            Compress.Active = defaults.Flag("Defaults", "Compress", true);
            Codec.ActiveId = defaults.Choice("Defaults", "Compressor", "Snappy", "Snappy", "LZ4", "Brotli");
            Level.Value = defaults.Number("Defaults", "Level", 4, 1, 9);
            Encrypt.Active = defaultPassword is not null;
            Password.Text = defaultPassword ?? "";
            Confirmation.Text = defaultPassword ?? "";
            }
        Compress.Toggled += (_, _) => UpdateEnabled();
        Codec.Changed += (_, _) => UpdateEnabled();
        Encrypt.Toggled += (_, _) => UpdateEnabled();
        UpdateEnabled();
        }

    private void UpdateEnabled() {
        Codec.Sensitive = Compress.Active;
        Level.Sensitive = Compress.Active && Codec.ActiveId == "Brotli";
        Level.TooltipText = Level.Sensitive ? "1 is fastest; 9 uses more effort." : "Snappy and LZ4 use fixed fast compression effort.";
        Password.Sensitive = Encrypt.Active;
        Confirmation.Sensitive = Encrypt.Active;
        }

    private void Browse() {
        using FileChooserDialog chooser = new FileChooserDialog(
            "Create MNX archive",
            Window,
            FileChooserAction.Save,
            "Cancel",
            ResponseType.Cancel,
            "Choose",
            ResponseType.Accept
            );
        GtkWindowAppearance.Apply(chooser);
        chooser.CurrentName = Path.GetFileName(Output.Text);
        string? folder = Path.GetDirectoryName(Output.Text);
        if (Directory.Exists(folder)) {
            chooser.SetCurrentFolder(folder);
            }

        FileFilter filter = new FileFilter {
            Name = "MNX archives (*.mnx)"
            };
        filter.AddPattern("*.mnx");
        chooser.AddFilter(filter);
        if ((ResponseType)chooser.Run() == ResponseType.Accept) {
            Output.Text = chooser.Filename;
            }

        chooser.Hide();
        }

    internal ArchiveCreationSettings? Run() {
        Window.ShowAll();
        while ((ResponseType)Window.Run() == ResponseType.Accept) {
            try {
                string path = ValidateDestination(Output.Text, _inputs);
                if (Encrypt.Active && (Password.Text.Length == 0 || Password.Text != Confirmation.Text)) {
                    throw new ArgumentException("Enter a nonempty password and repeat it exactly.");
                    }

                bool overwrite = DiskFile.Exists(path);
                if (overwrite) {
                    using MessageDialog question = OwnedIcons.Dialog(
                        Window,
                        DialogFlags.Modal,
                        MessageType.Question,
                        ButtonsType.YesNo,
                        false,
                        "{0}",
                        "The archive already exists. Overwrite it?\n" + path
                        );
                    ResponseType response = (ResponseType)question.Run();
                    question.Hide();
                    if (response != ResponseType.Yes) {
                        continue;
                        }

                    using FileStream existing = DiskFile.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                    }

                ArchiveCompression compression = !Compress.Active ? ArchiveCompression.None : Codec.ActiveId switch {
                    "Brotli" => ArchiveCompression.Brotli,
                    "LZ4" => ArchiveCompression.LZ4,
                    _ => ArchiveCompression.Snappy
                    };
                ArchiveCreationSettings result = new ArchiveCreationSettings(
                    path,
                    compression,
                    compression == ArchiveCompression.Brotli ? Level.ValueAsInt : 0,
                    Encrypt.Active ? Password.Text : null,
                    overwrite
                    );
                Window.Hide();
                return result;
                }
            catch (Exception ex) {
                using MessageDialog message = OwnedIcons.Dialog(Window, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, false, "{0}", ex.Message);
                message.Title = "Cannot create archive";
                message.Run();
                message.Hide();
                }
            }

        Window.Hide();
        return null;
        }

    internal static string ValidateDestination(string value, string[] inputs) {
        if (string.IsNullOrEmpty(value)) {
            throw new ArgumentException("Choose an archive filename.");
            }

        string path = Path.GetFullPath(value);
        if (!path.EndsWith(".mnx", StringComparison.OrdinalIgnoreCase)) {
            path += ".mnx";
            }

        string folder = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(folder)) {
            throw new DirectoryNotFoundException("The destination folder does not exist: " + folder);
            }

        if (Directory.Exists(path)) {
            throw new IOException("The destination is a folder. Choose a filename.");
            }

        if (Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            throw new ArgumentException("The output filename contains characters this operating system cannot use.");
            }

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (string input in inputs) {
            string source = Path.GetFullPath(input);
            if (string.Equals(path, source, comparison) || (Directory.Exists(source) && path.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, comparison))) {
                throw new IOException("Choose an output outside the input files and folders so the archive cannot include itself.");
                }
            }

        // Probe the actual component as well as directory access. This catches
        // filesystem-specific length/reserved-name rules before any input is read.
        if (!DiskFile.Exists(path)) {
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {
                }
            }
        else {
            string probe = Path.Combine(folder, ".mnx-access-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {
                }
            }

        return path;
        }

    public void Dispose() {
        Password.Text = "";
        Confirmation.Text = "";
        Window.Hide();
        Window.Dispose();
        }
    }
