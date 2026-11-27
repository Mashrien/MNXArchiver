using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Diagnostics;
#if !GTK3
using Mash.GUI.MNXArchiver;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using FormsApplication = Majorsilence.Forms.Application;
#endif

internal static class Program {
    private static int Main(string[] args) {
        string evidence = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(evidence);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(evidence, "data"));
        PreparePluginFixtures(evidence);
#if GTK3
        CheckGtk(evidence);
#else
        CheckMWin(evidence);
#endif
        return 0;
        }

#if !GTK3
    private static void CheckMWin(string evidence) {
        HeadlessRenderer.Use();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        PrepareGreenHighlight(typeof(AboutForm).Assembly);
        FormsApplication.SetDefaultFont(new Majorsilence.Forms.Drawing.Font("Noto Sans", 9));
        Type callbackType = typeof(Func<IReadOnlyDictionary<string, string>, bool>);
        ConstructorInfo constructor = typeof(AboutForm).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [callbackType], null) ?? throw new Exception("Missing actual About dialog constructor.");
        using AboutForm dialog = (AboutForm)constructor.Invoke([new Func<IReadOnlyDictionary<string, string>, bool>(_ => false)]);
        dialog.Show();
        FormsApplication.DoEvents();
        Require(dialog.Text == "About MNX Archiver", "Incorrect About window title.");
        Control[] controls = Descendants(dialog.Controls).ToArray();
        Require(controls.Any(control => control.Text == "MNX Archive Manager"), "Missing About heading.");
        Require(controls.Any(control => control.Text.StartsWith("GUI v" + typeof(AboutForm).Assembly.GetName().Version + "     MNX v0.", StringComparison.Ordinal)), "About must display the actual GUI assembly and semantic MNX format versions.");
        ListView[] lists = controls.OfType<ListView>().ToArray();
        Require(lists.Length == 2 && lists.All(list => list.Columns.Count == 5 && list.HeaderStyle == ColumnHeaderStyle.None && list.BorderStyle == BorderStyle.None), "Provider lists must have five borderless columns without headers.");
        Require(lists[0].Items.Cast<ListViewItem>().Any(item => item.Text == "FMT" && item.SubItems[1].Text == "MNX"), "Built-in MNX must be listed as a format provider.");
        foreach (ListView list in lists) {
            Require(list.Columns[2].Text == "Version" && list.Columns[3].Text == "Formats", "Version must sit between Name and Formats.");
            foreach (ListViewItem item in list.Items) {
                CheckProviderCells(item.SubItems[1].Text, item.SubItems[2].Text, item.SubItems[3].Text, item.SubItems[4].Text);
                }
            }
        foreach (string kind in new string[] { "IMG", "ANI", "TXT", "SRC" }) {
            Require(lists[1].Items.Cast<ListViewItem>().Any(item => item.Text == kind), "Missing implemented preview capability: " + kind);
            }
        Require(controls.OfType<LinkLabel>().Single().Text == "github.com/Mashrien/MNXArchiver", "Source URL uses the wrong repository.");
        Require(controls.OfType<Button>().Single(button => button.Text == "Reload").Visible == false, "Reload must initially be hidden.");
        if (Environment.GetEnvironmentVariable("MNX_ABOUT_PLUGIN_FIXTURES") is not null) {
            Stopwatch timeout = Stopwatch.StartNew();
            while (!lists.SelectMany(list => list.Items.Cast<ListViewItem>()).Any(item => item.ForeColor.ToArgb() == System.Drawing.Color.FromArgb(50, 170, 50).ToArgb())) {
                Require(timeout.Elapsed < TimeSpan.FromSeconds(5), "Changed installed plugin did not turn green after restart. Rows: " + string.Join("; ", lists[0].Items.Cast<ListViewItem>().Select(row => row.SubItems[1].Text)) + ". " + string.Join("; ", controls.OfType<Label>().Select(label => label.Text)));
                FormsApplication.DoEvents();
                Thread.Sleep(20);
                }
            Require(lists[1].Items.Cast<ListViewItem>().Any(item => item.Text == "VID"), "Installed video provider must appear as a real preview capability.");
            }
        byte[] image = HeadlessRenderer.CapturePng(dialog);
        File.WriteAllBytes(Path.Combine(evidence, "about-mwin.png"), image);
        dialog.Close();
        AppendDiagnosticSamples(typeof(DebugForm).Assembly);
        using DebugForm debug = new DebugForm();
        debug.Show();
        FormsApplication.DoEvents();
        ListView log = Descendants(debug.Controls).OfType<ListView>().Single();
        File.WriteAllBytes(Path.Combine(evidence, "debug-mwin.png"), HeadlessRenderer.CapturePng(debug));
        Require(log.BackColor.ToArgb() == System.Drawing.Color.Black.ToArgb() && log.Columns.Count == 3 && log.HeaderStyle == ColumnHeaderStyle.None,
            "Debug Window must preserve the black borderless three-column log surface: " + log.BackColor + ", " + log.Columns.Count + ", " + log.HeaderStyle);
        Require(log.Items.Cast<ListViewItem>().Any(row => row.SubItems[2].Text == "Critical diagnostic sample." && row.ForeColor.ToArgb() == System.Drawing.Color.Red.ToArgb()),
            "Critical log rows must render in PackRat red.");
        File.WriteAllBytes(Path.Combine(evidence, "debug-mwin.png"), HeadlessRenderer.CapturePng(debug));
        debug.Close();
        Require(!debug.IsDisposed && !debug.Visible, "Closing Debug Window must hide it for reopening.");
        debug.Show();
        Require(debug.Visible, "Hidden Debug Window must reopen.");
        Console.WriteLine("PASS: actual MWin Debug Window rendering, severity colors and hide/reopen lifecycle.");
        Console.WriteLine("PASS: actual MWin About dialog versions, layout, provider rows and rendered screenshot.");
        }

#else
    private static void CheckGtk(string evidence) {
        Gtk.Application.Init();
        PrepareGreenHighlight(typeof(Mash.GUI.MNXArchiver.EntryPoints.Gtk3Frontend).Assembly);
        using Gtk.Window owner = new Gtk.Window("About test owner");
        Type dialogType = typeof(Mash.GUI.MNXArchiver.EntryPoints.Gtk3Frontend).Assembly.GetType("Mash.GUI.MNXArchiver.About.AboutDialog", true)!;
        using Gtk.Dialog dialog = (Gtk.Dialog)Activator.CreateInstance(dialogType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [owner, new Func<IReadOnlyDictionary<string, string>, bool>(_ => false)], null)!;
        dialog.ShowAll();
        while (Gtk.Application.EventsPending()) {
            Gtk.Application.RunIteration();
            }
        Require(dialog.Title == "About MNX Archiver", "Incorrect GTK About window title.");
        Gtk.Widget[] widgets = GtkDescendants(dialog).ToArray();
        Require(widgets.OfType<Gtk.Label>().Any(label => label.Text == "MNX Archive Manager"), "Missing GTK About heading.");
        Gtk.TreeView[] lists = widgets.OfType<Gtk.TreeView>().ToArray();
        Require(lists.Length == 2 && lists.All(list => !list.HeadersVisible && list.Columns.Length == 5), "GTK providers must use five columns without headers.");
        foreach (Gtk.TreeView list in lists) {
            Require(list.Model.GetIterFirst(out Gtk.TreeIter row), "Provider list is empty.");
            do {
                CheckProviderCells((string)list.Model.GetValue(row, 1), (string)list.Model.GetValue(row, 2),
                    (string)list.Model.GetValue(row, 3), (string)list.Model.GetValue(row, 4));
                } while (list.Model.IterNext(ref row));
            }
        if (Environment.GetEnvironmentVariable("MNX_ABOUT_PLUGIN_FIXTURES") is not null) {
            Stopwatch timeout = Stopwatch.StartNew();
            while (!HasUpdatedGtkRow(lists)) {
                Require(timeout.Elapsed < TimeSpan.FromSeconds(5), "Changed GTK plugin did not become green after restart. " + string.Join("; ", widgets.OfType<Gtk.Label>().Select(label => label.Text)));
                while (Gtk.Application.EventsPending()) {
                    Gtk.Application.RunIteration();
                    }
                Thread.Sleep(20);
                }
            Require(lists[0].Model.GetIterFirst(out Gtk.TreeIter builtIn) && !(bool)lists[0].Model.GetValue(builtIn, 5),
                "Built-in MNX must keep its ordinary color after plugin restart.");
            }
        foreach (Gtk.TreeView list in lists) {
            list.QueueDraw();
            }
        Stopwatch paint = Stopwatch.StartNew();
        while (paint.Elapsed < TimeSpan.FromMilliseconds(180)) {
            while (Gtk.Application.EventsPending()) {
                Gtk.Application.RunIteration();
                }
            Thread.Sleep(10);
            }
        IntPtr pixels = gdk_pixbuf_get_from_window(dialog.Window.Handle, 0, 0, dialog.Allocation.Width, dialog.Allocation.Height);
        Require(pixels != IntPtr.Zero, "Could not capture GTK About dialog.");
        using Gdk.Pixbuf image = new Gdk.Pixbuf(pixels);
        image.Save(Path.Combine(evidence, "about-gtk3.png"), "png");
        dialog.Respond(Gtk.ResponseType.Close);
        dialog.Destroy();
        Assembly frontend = typeof(Mash.GUI.MNXArchiver.EntryPoints.Gtk3Frontend).Assembly;
        AppendDiagnosticSamples(frontend);
        Type debugType = frontend.GetType("Mash.GUI.MNXArchiver.Diagnostics.DebugWindow", true)!;
        using IDisposable debug = (IDisposable)Activator.CreateInstance(debugType,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [owner], null)!;
        MethodInfo show = debugType.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!;
        show.Invoke(debug, null);
        while (Gtk.Application.EventsPending()) {
            Gtk.Application.RunIteration();
            }
        Gtk.Window debugWindow = (Gtk.Window)debugType.GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(debug)!;
        Gtk.TreeView log = GtkDescendants(debugWindow).OfType<Gtk.TreeView>().Single();
        Require(log.Columns.Length == 3 && !log.HeadersVisible && log.Model.IterNChildren() >= 3,
            "GTK Debug Window must show actual diagnostic records in three columns.");
        using Gdk.Pixbuf debugImage = new Gdk.Pixbuf(gdk_pixbuf_get_from_window(debugWindow.Window.Handle, 0, 0, debugWindow.Allocation.Width, debugWindow.Allocation.Height));
        debugImage.Save(Path.Combine(evidence, "debug-gtk3.png"), "png");
        Console.WriteLine("PASS: actual GTK3 Debug Window rendering and bounded log records.");
        Console.WriteLine("PASS: actual GTK3 About dialog headings, provider columns and rendered screenshot.");
        }

#endif

    private static void AppendDiagnosticSamples(Assembly frontend) {
        Type sink = frontend.GetType("Mash.GUI.MNXArchiver.Diagnostics.DebugLog", true)!;
        Type category = frontend.GetType("Mash.GUI.MNXArchiver.Diagnostics.LogType", true)!;
        object buffer = sink.GetProperty("Buffer", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        MethodInfo write = buffer.GetType().GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic)!;
        write.Invoke(buffer, [Enum.Parse(category, "Critical"), "Checks->Render", "Critical diagnostic sample."]);
        write.Invoke(buffer, [Enum.Parse(category, "Info"), "Checks->Render", "Information diagnostic sample."]);
        write.Invoke(buffer, [Enum.Parse(category, "Warning"), "Checks->Render", "Warning diagnostic sample."]);
        }

    private static Dictionary<string, string>? _previousPlugins;

    private static void PreparePluginFixtures(string evidence) {
        string? fixtures = Environment.GetEnvironmentVariable("MNX_ABOUT_PLUGIN_FIXTURES");
        if (fixtures is null) {
            return;
            }
        string configuration = Path.Combine(evidence, "config");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configuration);
        string plugins = Path.Combine(configuration, "mnxarchiver", "plugins");
        Directory.CreateDirectory(plugins);
        _previousPlugins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string source in Directory.EnumerateFiles(fixtures, "*.mnxp")) {
            string destination = Path.Combine(plugins, Path.GetFileName(source));
            File.Copy(source, destination, true);
            _previousPlugins.Add(Path.GetFileName(source), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
            }
        string changed = Directory.EnumerateFiles(plugins, "*.mnxp").Order(StringComparer.Ordinal).First();
        // An ELF trailer is ignored by the loader, so this remains a real loadable provider with a changed hash.
        using FileStream update = new FileStream(changed, FileMode.Append, FileAccess.Write);
        update.WriteByte(0);
        }

    private static void PrepareGreenHighlight(Assembly frontend) {
        if (_previousPlugins is null) {
            return;
            }
        Type state = frontend.GetType("Mash.GUI.MNXArchiver.About.AboutRestart", true)!;
        MethodInfo write = state.GetMethod("WriteDescriptor", BindingFlags.Static | BindingFlags.NonPublic)!;
        string descriptor = (string)write.Invoke(null, [null, _previousPlugins])!;
        MethodInfo restore = state.GetMethod("RestoreArguments", BindingFlags.Static | BindingFlags.NonPublic)!;
        restore.Invoke(null, [new string[] { "--about-restart=" + descriptor }]);
        string plugins = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")!, "mnxarchiver", "plugins");
        Type snapshot = frontend.GetType("Mash.GUI.MNXArchiver.About.PluginSnapshot", true)!;
        MethodInfo capture = snapshot.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!;
        Dictionary<string, string> current = (Dictionary<string, string>)capture.Invoke(null, [plugins, CancellationToken.None])!;
        Require(current.Any(pair => _previousPlugins[pair.Key] != pair.Value), "Fixture mutation must change the real production snapshot.");
        Console.WriteLine("Verified a changed plugin in the real production snapshot.");
        }

#if !GTK3
    private static IEnumerable<Control> Descendants(Control.ControlCollection controls) {
        foreach (Control child in controls) {
            yield return child;
            foreach (Control descendant in Descendants(child.Controls)) {
                yield return descendant;
                }
            }
        }

#else
    private static bool HasUpdatedGtkRow(IEnumerable<Gtk.TreeView> lists) {
        foreach (Gtk.TreeView list in lists) {
            if (!list.Model.GetIterFirst(out Gtk.TreeIter row)) {
                continue;
                }
            do {
                if ((bool)list.Model.GetValue(row, 5)) {
                    return true;
                    }
                } while (list.Model.IterNext(ref row));
            }
        return false;
        }

    private static IEnumerable<Gtk.Widget> GtkDescendants(Gtk.Container parent) {
        foreach (Gtk.Widget child in parent.Children) {
            yield return child;
            if (child is Gtk.Container container) {
                foreach (Gtk.Widget descendant in GtkDescendants(container)) {
                    yield return descendant;
                    }
                }
            }
        }

#endif

    private static void CheckProviderCells(string name, string version, string formats, string library) {
        Require(formats.Length > 0 && !formats.Contains(" v"), "Extensions include a version for " + name);
        if (library != "Built-in" || name == "MNX") {
            Require(version.StartsWith('v') && Version.TryParse(version.Substring(1), out _), "Missing provider version for " + name);
            }
        else {
            Require(version.Length == 0, "Built-in previews have no separate plugin version.");
            }
        }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new Exception(message);
            }
        }

    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_pixbuf_get_from_window(IntPtr window, int x, int y, int width, int height);
    }
