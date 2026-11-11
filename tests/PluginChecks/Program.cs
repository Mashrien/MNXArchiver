using Mash.UI.MNXFile.GUI;
using Mash.UI.MNXFile.GUI.Plugins;

if (args.Length == 1 && args[0] == "--check-plugin-path") {
    string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "mnxarchiver", "plugins");
    Require(PluginCatalog.Installed.DirectoryPath == expected, "An empty or relative XDG_CONFIG_HOME redirected plugin discovery.");
    Console.WriteLine("PASS: untrusted relative configuration paths cannot redirect native discovery.");
    return;
    }

bool checkWebp = args.Length == 3 && args[2] == "--check-webp";
if (args.Length != 2 && !checkWebp) {
    throw new ArgumentException("Pass the fixture plugin directory and a Python-produced hello.rpa.");
    }
PluginCatalog catalog = PluginCatalog.Discover(args[0]);
Require(catalog.Modules.Count == 1, string.Join("\n", catalog.Issues));
NativeArchiveModule module = catalog.Modules.Single();
Require(module.Format.Id == "renpy.rpa" && module.Format.CurrentVersion == "3.0", "The RPA descriptor did not survive discovery.");
foreach ((string file, string reason) in new (string, string)[] {
    ("bad-magic.so", "bad-magic.so:"), ("bad-abi.so", "Plugin ABI 2"),
    ("new-module.so", "Module release 1.1.0"), ("old-module.so", "Module release 0.9.9"),
    ("new-host-required.so", "requires MNXArchiver 2.1.0"), ("reject-negotiation.so", "refused negotiation"),
    }) {
    Require(catalog.Issues.Any(issue => issue.StartsWith(file + ":", StringComparison.Ordinal) && issue.Contains(reason, StringComparison.Ordinal)),
        "Expected rejection did not occur: " + file + "\n" + string.Join("\n", catalog.Issues));
    }
Console.WriteLine("PASS: native library magic, ABI, old/new peer versions and module refusal.");
if (checkWebp) {
    ImageProviderChecks.Run(catalog);
    }

DirectoryInfo work = Directory.CreateTempSubdirectory("mnx-plugin-checks-");
try {
    string path = Path.Combine(work.FullName, "renamed.dat");
    File.Copy(args[1], path);
    OpenedModuleArchive archive = OpenedModuleArchive.Open(catalog.Match(path)!, path);
    Guid folder = archive.Browser.Resolve("/folder");
    Guid hello = archive.Browser.Children(folder).Single().Id;
    Require(archive.Browser.Get(hello).Name == "hello.txt", "The folder tree did not preserve the archived path.");
    Require(archive.Supports(PluginActions.Extract | PluginActions.Add | PluginActions.Replace | PluginActions.Create | PluginActions.Delete),
        "The RPA actions were lost.");
    Require(archive.Supports(PluginActions.ReadEntry), "The RPA format did not advertise bounded entry reads for previews.");
    byte[] preview = new byte[5];
    archive.ReadEntry(hello, preview, CancellationToken.None);
    Require(preview.AsSpan().SequenceEqual("hello"u8), "The preview read did not return the requested prefix.");
    Reject(() => archive.ReadEntry(hello, new byte[32], CancellationToken.None));

    string extracted = Path.Combine(work.FullName, "hello.txt");
    archive.PlanExtraction([hello], extracted, true).Extract(false);
    Require(File.ReadAllText(extracted) == "hello from Ren'Py", "Independent fixture extraction changed file bytes.");

    string addition = Path.Combine(work.FullName, "new.txt");
    File.WriteAllText(addition, "new payload");
    archive = archive.Add([addition], "/folder");
    Require(archive.Browser.Children(folder).Count == 2, "Add did not refresh the existing folder.");
    byte[] before = File.ReadAllBytes(path);
    Reject(() => archive.Add([addition], "/folder"));
    Require(File.ReadAllBytes(path).SequenceEqual(before), "Duplicate add modified the original archive.");

    string replacement = Path.Combine(work.FullName, "replacement.txt");
    File.WriteAllText(replacement, "replacement payload");
    archive = archive.Replace(replacement, hello);
    string outputDirectory = Path.Combine(work.FullName, "extracted");
    archive.PlanExtraction([folder, hello], outputDirectory, false).Extract(false);
    Require(File.ReadAllText(Path.Combine(outputDirectory, "folder", "hello.txt")) == "replacement payload"
        && File.ReadAllText(Path.Combine(outputDirectory, "folder", "new.txt")) == "new payload",
        "Replace or folder extraction changed the archive structure or untouched payload.");
    Reject(() => archive.PlanExtraction([hello], path, true));
    archive = archive.Delete([folder, hello]);
    Require(archive.Browser.Children(archive.Browser.Root).Count == 0 && File.Exists(path),
        "Deleting selected folder members did not leave a valid empty archive.");

    string createdPath = Path.Combine(work.FullName, "new.rpa");
    OpenedModuleArchive created = OpenedModuleArchive.Create(module, createdPath, [addition], false);
    Require(created.Browser.Children(created.Browser.Root).Single().Name == "new.txt", "Create did not return its listing.");
    using CancellationTokenSource cancelled = new();
    cancelled.Cancel();
    Reject(() => created.Add([replacement], "/", cancelled.Token));
    Reject(() => PluginCatalog.Discover(Path.Combine(work.FullName, "no-modules")).Match(createdPath));
    File.WriteAllText(Path.Combine(work.FullName, "invalid.rpa"), "RPA-2.0 0000000000000000\n");
    Reject(() => catalog.Match(Path.Combine(work.FullName, "invalid.rpa")));
    Console.WriteLine("PASS: signature routing, open, add, replace, extract, delete, create, cancellation and safe rejection.");
    }
finally {
    work.Delete(recursive: true);
    }

static void Require(bool value, string message) {
    if (!value) {
        throw new InvalidDataException(message);
        }
    }

static void Reject(Action action) {
    try {
        action();
        }
    catch (Exception error) when (error is IOException or NotSupportedException or OperationCanceledException) {
        return;
        }
    throw new InvalidDataException("The invalid plugin operation was accepted.");
    }
