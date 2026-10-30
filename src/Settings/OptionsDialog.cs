using Mash.UI.MNXFile.GUI.Icons;
using System.Globalization;
using Gtk;
using Mash.UI.MNXFile.GUI.Backends;
using Action = System.Action;

namespace Mash.UI.MNXFile.GUI.Settings;

/// <summary>Expandable settings editor. Ordinary changes are committed only by Save.</summary>
internal sealed class OptionsDialog : IDisposable {
    internal Dialog Window { get; }
    private readonly AppSettings _settings;
    private readonly DefaultKeyStore _keys;
    private readonly List<Action> _readControls = [];
    private readonly Entry _password = new Entry { Visibility = false, WidthChars = 30 };
    private readonly Entry _confirmation = new Entry { Visibility = false, WidthChars = 30 };
    private readonly Label _match = new Label();
    private readonly Button _store = new Button("STORE DEFAULT");
    private readonly Button _clear = new Button("Clear");
    private readonly CheckButton _encrypt;
    private readonly Entry _temp;
    private readonly CheckButton _overrideTemp;

    internal OptionsDialog(Gtk.Window parent, AppSettings settings) {
        _settings = settings.Copy();
        _keys = new DefaultKeyStore(_settings);
        Window = new Dialog("MNX Options", parent, DialogFlags.Modal, "Cancel", ResponseType.Cancel, "Save", ResponseType.Accept) {
            DestroyWithParent = true
            };
        GtkWindowAppearance.Apply(Window);
        Window.SetDefaultSize(780, 720);
        Notebook tabs = new Notebook();
        Window.ContentArea.PackStart(tabs, true, true, 0);
        Box generalPage = Page(tabs, "General");
        Box compressionPage = Page(tabs, "Compression & Encryption");
        Box previewPage = Page(tabs, "Previews");
        Box displayPage = Page(tabs, "Preview Display");
        ConfigureBackendChoice(generalPage);
        Box appearance = Group(generalPage, "Appearance");
        SpinButton opacity = Number(appearance, "Window opacity (%)", "Interface", "OpacityPercent", 100, 10, 100, 5);
        opacity.Name = "interfaceOpacityNumericUpDown";
        appearance.PackStart(new Label("100% is opaque. Lower values reveal the desktop. Errors stay opaque. Changes apply on Save.") { Xalign = 0, Wrap = true }, false, false, 0);
        Box general = Group(generalPage, "Files and working folders");
        Box temporary = Reveal(general, "Override temporary-file path", "General", "OverrideTemp", false, out _overrideTemp);
        _temp = new Entry { Text = _settings.Get("General", "TempPath", Path.GetTempPath()), Hexpand = true };
        Button change = new Button("Change…");
        Row(temporary, _temp, change);
        _readControls.Add(() => _settings.Set("General", "TempPath", _temp.Text));
        change.Clicked += (_, _) => ChooseTemp();

        Box defaults = compressionPage;
        Box compression = Reveal(defaults, "Set Compressor", "Defaults", "Compress", true, out _);
        ComboBoxText codec = Choice(compression, "Compressor", "Defaults", "Compressor", "Snappy", "Snappy", "LZ4", "Brotli");
        SpinButton level = Number(compression, "Level", "Defaults", "Level", 4, 1, 9, 1);
        level.Sensitive = codec.ActiveText == "Brotli";
        codec.Changed += (_, _) => level.Sensitive = codec.ActiveText == "Brotli";
        Label effort = new Label("Snappy and LZ4 use their fixed fast effort; levels apply to Brotli.") { Xalign = 0, Wrap = true };
        compression.PackStart(effort, false, false, 0);
        Box encryption = Reveal(defaults, "Specify Default Encryption", "Defaults", "Encrypt", false, out _encrypt);
        Label warning = new Label {
            Markup = "<b><span foreground='red'>(WARNING: INSECURE - PRIVATE/PERSONAL COMPUTER USE ONLY - NEVER ENABLE ON SHARED MACHINES!)</span></b>",
            Wrap = true,
            Xalign = 0
            };
        encryption.PackStart(warning, false, false, 0);
        Row(encryption, new Label("Password:"), _password);
        Row(encryption, new Label("Repeat password:"), _confirmation, _match);
        Row(encryption, _clear, _store);
        encryption.PackStart(new Label("Saved-key actions take effect immediately. Clear also disables the saved default.") { Wrap = true, Xalign = 0 }, false, false, 0);
        _password.Changed += (_, _) => UpdatePasswordState();
        _confirmation.Changed += (_, _) => UpdatePasswordState();
        _store.Clicked += (_, _) => StoreKey();
        _clear.Clicked += (_, _) => ClearKey();
        UpdatePasswordState();

        Box previews = Reveal(previewPage, "Enable file previews (experimental)", "Previews", "Enabled", false, out _);
        Box activation = Group(previews, "Activation");
        Box realtime = Group(previews, "Memory limits");
        Number(realtime, "Maximum input buffer (MiB)", "Previews", "BufferMiB", 32, 16, 128, 16);
        Number(realtime, "Decoded image/animation memory (MiB)", "Previews", "DecodedMiB", AppSettings.DefaultDecodedMiB, 64, 1024, 64);
        Number(realtime, "Combined preview memory (MiB)", "Previews", "TotalMiB", AppSettings.DefaultTotalPreviewMiB, 256, 4096, 256);
        RadioButton hover = new RadioButton("Mouse-Over With Delay");
        RadioButton doubleClick = new RadioButton(hover, "Double-Click");
        RadioButton singleClick = new RadioButton(hover, "Single-Click");
        doubleClick.Active = _settings.Trigger == "Double-Click";
        singleClick.Active = _settings.Trigger == "Single-Click";
        hover.Active = _settings.Trigger == "Mouse-Over With Delay";
        SpinButton delay = new SpinButton(AppSettings.MinimumHoverDelay, 2000, 250) { Value = _settings.HoverDelay, Numeric = true, Sensitive = hover.Active };
        activation.PackStart(new Label("Activation Trigger") { Xalign = 0 }, false, false, 0);
        Row(activation, hover, delay, new Label("milliseconds."));
        activation.PackStart(singleClick, false, false, 0);
        activation.PackStart(doubleClick, false, false, 0);
        hover.Toggled += (_, _) => delay.Sensitive = hover.Active;
        _readControls.Add(() => {
            _settings.Set("Previews", "Trigger", hover.Active ? "Mouse-Over With Delay" : singleClick.Active ? "Single-Click" : "Double-Click");
            _settings.Set("Previews", "DelayMs", ((int)delay.Value / 250) * 250);
            });

        Box text = Group(previews, "Text Files");
        TypeCategory(text, "Plain Text (txt, ini, conf, cfg, md, readme, nfo…)", "Text", "Plain", "PlainTypes", true);
        TypeCategory(text, "Source Code (common source-code files)", "Text", "Source", "SourceTypes", true);
        CheckButton external = new CheckButton("Use External Source Viewer — NOT YET IMPLEMENTED") { Sensitive = false };
        Button viewer = new Button("Source viewer…") { Sensitive = false };
        Row(text, external, viewer);
        Box images = Group(previews, "Image Files");
        TypeCategory(images, "Static (formats supported by installed image decoders)", "Images", "Static", "StaticTypes", true);
        CheckButton animated = Check(images, "Animated [EXPERIMENTAL] — GIF when supported internally", "Images", "Animated", false);
        animated.TooltipText = "GIF animation uses the installed GTK decoder. Unsupported animations are never opened externally.";
        Label displayNote = new Label("These settings apply when file previews are enabled on the Previews tab.") { Xalign = 0, Wrap = true };
        displayPage.PackStart(displayNote, false, false, 0);
        Box presentation = Group(displayPage, "Preview Presentation Options");
        Box customize = Reveal(presentation, "Customize Display", "Presentation", "Customize", false, out _);
        RadioButton tooltip = new RadioButton("Tooltip");
        RadioButton window = new RadioButton(tooltip, "Window");
        window.Active = _settings.Get("Presentation", "Mode", "Window") != "Tooltip";
        tooltip.Active = !window.Active;
        customize.PackStart(tooltip, false, false, 0);
        Box tooltipSettings = Column();
        customize.PackStart(tooltipSettings, false, false, 0);
        Choice(tooltipSettings, "Anchor To:", "Presentation", "Anchor", "File", "File", "Cursor", "Bottom-Right");
        customize.PackStart(window, false, false, 0);
        Box windowSettings = Column();
        customize.PackStart(windowSettings, false, false, 0);
        Choice(windowSettings, "Spawn Preview", "Presentation", "WindowType", "Dialog", "Dialog", "Window");
        Choice(windowSettings, "from the", "Presentation", "Origin", "Center", "Center", "Top-Left", "Right of Main Window");
        Choice(windowSettings, "of the", "Presentation", "RelativeTo", "Program Window", "Program Window", "Current Screen");
        void UpdatePresentation() {
            tooltipSettings.Sensitive = tooltip.Active;
            windowSettings.Sensitive = window.Active;
            }
        tooltip.Toggled += (_, _) => UpdatePresentation();
        UpdatePresentation();
        _readControls.Add(() => _settings.Set("Presentation", "Mode", tooltip.Active ? "Tooltip" : "Window"));

        Box limits = Reveal(presentation, "Specify Max X/Y", "Presentation", "LimitSize", false, out _);
        RadioButton pixels = new RadioButton("Set Pixels");
        RadioButton ratio = new RadioButton(pixels, "Set Ratio");
        ratio.Active = _settings.Get("Presentation", "SizeMode", "Ratio") != "Pixels";
        pixels.Active = !ratio.Active;
        limits.PackStart(pixels, false, false, 0);
        Box pixelSettings = Column();
        limits.PackStart(pixelSettings, false, false, 0);
        string[] resolutions = AppSettings.Resolutions.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
        Choice(pixelSettings, "Limit Previews to X", "Presentation", "MaxX", "800", resolutions);
        Choice(pixelSettings, "by Y", "Presentation", "MaxY", "640", resolutions);
        limits.PackStart(ratio, false, false, 0);
        Box ratioSettings = Column();
        limits.PackStart(ratioSettings, false, false, 0);
        Choice(ratioSettings, "Limit Previews to (%)", "Presentation", "Percent", "66", AppSettings.Percentages.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray());
        Choice(ratioSettings, "percent of", "Presentation", "SizeRelativeTo", "Main Window", "Main Window", "Current Screen");
        void UpdateLimits() {
            pixelSettings.Sensitive = pixels.Active;
            ratioSettings.Sensitive = ratio.Active;
            }
        pixels.Toggled += (_, _) => UpdateLimits();
        UpdateLimits();
        _readControls.Add(() => _settings.Set("Presentation", "SizeMode", pixels.Active ? "Pixels" : "Ratio"));
        Choice(presentation, "Scale Mode", "Presentation", "ScaleMode", "Scale", "Scale", "Zoom", "Stretch", "Center");
        realtime.PackStart(new Label("Memory only: configurable input buffer; bounded decoded images and text. No preview temporary files.") { Wrap = true, Xalign = 0 }, false, false, 0);
        }

    private static Box Page(Notebook tabs, string title) {
        ScrolledWindow scroll = new ScrolledWindow { HscrollbarPolicy = PolicyType.Never };
        Box body = Column();
        body.BorderWidth = 16;
        scroll.Add(body);
        tabs.AppendPage(scroll, new Label(title));
        return body;
        }

    private void ConfigureBackendChoice(Box page) {
        BackendAvailability? available = BackendRuntime.Availability;
        if (available is null) {
            return;
            }
        Box group = Group(page, "Interface");
        ComboBoxText choice = new ComboBoxText { Name = "interfaceBackendComboBox" };
        choice.Append("auto", BackendSelection.Label(GuiBackend.Automatic));
        choice.Append("mwin", BackendSelection.Label(GuiBackend.MWin));
        choice.Append("gtk3", BackendSelection.Label(GuiBackend.Gtk3));
        choice.ActiveId = BackendSelection.Key(BackendSelection.ParsePreference(_settings.Get("Interface", "Backend", "auto")));
        choice.Sensitive = available.MWinAvailable && available.Gtk3Available;
        Row(group, new Label("Preferred interface:"), choice);
        group.PackStart(new Label("Currently using: " + BackendSelection.Label(BackendRuntime.Current)) { Xalign = 0 }, false, false, 0);
        group.PackStart(new Label("Changes apply after restarting MNX. Automatic prefers MWin.") { Xalign = 0, Wrap = true }, false, false, 0);
        if (!choice.Sensitive) {
            group.PackStart(new Label("Only one interface is available. Install the other toolkit to enable selection.") { Xalign = 0, Wrap = true }, false, false, 0);
            }
        _readControls.Add(() => {
            if (choice.Sensitive) {
                _settings.Set("Interface", "Backend", choice.ActiveId ?? "auto");
                }
            });
        }

    private static Box Column() {
        return new Box(Orientation.Vertical, 8);
        }
    private static void Row(Box parent, params Widget[] widgets) {
        Box row = new Box(Orientation.Horizontal, 8);
        foreach (Widget widget in widgets) {
            row.PackStart(widget, widget.Hexpand, widget.Hexpand, 0);
            }
        parent.PackStart(row, false, false, 0);
        }
    private static Box Group(Box parent, string title) {
        Frame frame = new Frame(title);
        Box content = Column();
        content.BorderWidth = 12;
        frame.Add(content);
        parent.PackStart(frame, false, false, 0);
        return content;
        }
    private CheckButton Check(Box parent, string title, string section, string key, bool fallback) {
        CheckButton check = new CheckButton(title) { Active = _settings.Flag(section, key, fallback) };
        parent.PackStart(check, false, false, 0);
        _readControls.Add(() => _settings.Set(section, key, check.Active));
        return check;
        }
    private Box Reveal(Box parent, string title, string section, string key, bool fallback, out CheckButton check) {
        check = Check(parent, title, section, key, fallback);
        CheckButton toggle = check;
        Box content = Column();
        content.MarginStart = 20;
        content.NoShowAll = !check.Active;
        content.Visible = check.Active;
        parent.PackStart(content, false, false, 0);
        toggle.Toggled += (_, _) => {
            content.NoShowAll = !toggle.Active;
            if (toggle.Active) {
                content.ShowAll();
                }
            else {
                content.Hide();
                }
            };
        return content;
        }
    private ComboBoxText Choice(Box parent, string label, string section, string key, string fallback, params string[] values) {
        ComboBoxText choice = new ComboBoxText();
        foreach (string value in values) {
            choice.Append(value, value);
            }
        choice.ActiveId = _settings.Choice(section, key, fallback, values);
        Row(parent, new Label(label) { Xalign = 0 }, choice);
        _readControls.Add(() => _settings.Set(section, key, choice.ActiveId ?? fallback));
        return choice;
        }
    private SpinButton Number(Box parent, string label, string section, string key, int fallback, int min, int max, int step) {
        SpinButton value = new SpinButton(min, max, step) { Value = _settings.Number(section, key, fallback, min, max), Numeric = true };
        Row(parent, new Label(label), value);
        _readControls.Add(() => _settings.Set(section, key, value.ValueAsInt));
        return value;
        }
    private void TypeCategory(Box parent, string label, string section, string enabled, string key, bool fallback) {
        Box row = new Box(Orientation.Horizontal, 8);
        parent.PackStart(row, false, false, 0);
        Check(row, label, section, enabled, fallback);
        Button add = new Button("Add types…");
        row.PackEnd(add, false, false, 0);
        add.Clicked += (_, _) => {
            using Dialog dialog = new Dialog("Add preview types", Window, DialogFlags.Modal, "Cancel", ResponseType.Cancel, "Okay", ResponseType.Accept);
            GtkWindowAppearance.Apply(dialog);
            dialog.ContentArea.BorderWidth = 12;
            dialog.ContentArea.PackStart(new Label("Comma-separated list of extensions for " + label.Split('(')[0].Trim() + " previewing"), false, false, 6);
            Entry input = new Entry { Text = _settings.Get(section, key, ""), WidthChars = 55 };
            dialog.ContentArea.PackStart(input, false, false, 6);
            dialog.ShowAll();
            while ((ResponseType)dialog.Run() == ResponseType.Accept) {
                try {
                    _settings.Set(section, key, AppSettings.Extensions(input.Text));
                    break;
                    }
                catch (ArgumentException error) {
                    Error(dialog, error.Message);
                    }
                }
            dialog.Hide();
            };
        }
    private void ChooseTemp() {
        using FileChooserDialog chooser = new FileChooserDialog("Select temporary folder", Window, FileChooserAction.SelectFolder, "Cancel", ResponseType.Cancel, "Choose", ResponseType.Accept);
        GtkWindowAppearance.Apply(chooser);
        if (Directory.Exists(_temp.Text)) {
            chooser.SetCurrentFolder(_temp.Text);
            }
        if ((ResponseType)chooser.Run() == ResponseType.Accept) {
            try {
                AppSettings.ValidateTemp(chooser.Filename);
                _temp.Text = chooser.Filename;
                }
            catch (Exception error) {
                Error(chooser, error.Message);
                }
            }
        chooser.Hide();
        }
    private void UpdatePasswordState() {
        bool stored = _keys.Exists;
        bool matches = _password.Text.Length > 0 && _password.Text == _confirmation.Text;
        _match.Markup = stored ? "<span foreground='green'>✓ Stored</span>" : matches ? "<span foreground='green'>● ✓</span>" : "<span foreground='red'>● ✕</span>";
        _password.Sensitive = !stored;
        _confirmation.Sensitive = !stored;
        _store.Sensitive = !stored && matches;
        _clear.Sensitive = true;
        }
    private void StoreKey() {
        try {
            string password = _password.Text;
            Busy(() => _keys.Store(password));
            _password.Text = "";
            _confirmation.Text = "";
            UpdatePasswordState();
            }
        catch (Exception error) {
            Error(Window, error.Message);
            }
        }
    private void ClearKey() {
        _password.Text = "";
        _confirmation.Text = "";
        _encrypt.Active = false;
        // Persist disabling immediately, even if the main settings editor is cancelled.
        try {
            AppSettings current = AppSettings.Load(_settings.DirectoryPath);
            current.Set("Defaults", "Encrypt", false);
            current.Save();
            Busy(_keys.Clear);
            }
        catch (Exception error) {
            Error(Window, error.Message);
            }
        UpdatePasswordState();
        }
    private void Busy(Action action) {
        Window.Sensitive = false;
        try {
            UiWorker.Run(() => { action(); return true; });
            }
        finally {
            Window.Sensitive = true;
            }
        }
    internal AppSettings? Run() {
        Window.ShowAll();
        while ((ResponseType)Window.Run() == ResponseType.Accept) {
            try {
                foreach (Action read in _readControls) {
                    read();
                    }
                if (_overrideTemp.Active) {
                    AppSettings.ValidateTemp(_temp.Text);
                    }
                if (_encrypt.Active && !_keys.Exists) {
                    throw new ArgumentException("Store a matching default password first, or disable default encryption.");
                    }
                _settings.Save();
                Window.Hide();
                return _settings;
                }
            catch (Exception error) {
                Error(Window, error.Message);
                }
            }
        Window.Hide();
        return null;
        }
    private static void Error(Gtk.Window parent, string message) {
        using MessageDialog dialog = OwnedIcons.Dialog(parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, false, "{0}", message);
        dialog.Run();
        dialog.Hide();
        }
    public void Dispose() {
        _password.Text = "";
        _confirmation.Text = "";
        Window.Hide();
        Window.Dispose();
        }
    }

internal static class UiWorker {
    // Only used for explicit, modal credential operations. Archive work has its own progress loop.
    internal static T Run<T>(Func<T> work) {
        Task<T> task = Task.Run(work);
        while (!task.IsCompleted) {
            for (int count = 0; count < 32 && Application.EventsPending(); count++) {
                Application.RunIteration(false);
                }
            Thread.Sleep(10);
            }
        return task.GetAwaiter().GetResult();
        }
    }
