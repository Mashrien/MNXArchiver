using Mash.GUI.MNXArchiver.Backends;
using Mash.GUI.MNXArchiver.Settings;

if (args.Length == 2 && args[0] == "--probe-backend") {
    return BackendProbe.RunChild(args[1]);
    }
int checks = 0;
void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
        }
    checks++;
    }
void Reject(Action operation, string expectedMessage) {
    try {
        operation();
        }
    catch (ArgumentException error) {
        Require(error.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase), error.Message);
        return;
        }
    catch (InvalidOperationException error) {
        Require(error.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase), error.Message);
        return;
        }
    throw new InvalidOperationException("Expected rejection containing: " + expectedMessage);
    }
BackendAvailability both = new BackendAvailability(true, true);
BackendAvailability gtkOnly = new BackendAvailability(false, true, "GTK4 absent");
BackendAvailability mwinOnly = new BackendAvailability(true, false, "", "GTK3 absent");
Require(BackendSelection.Select(GuiBackend.Automatic, false, both).Backend == GuiBackend.MWin,
    "Automatic selection must prefer MWin when both toolkits are installed.");
Require(BackendSelection.Select(GuiBackend.Automatic, false, gtkOnly).Backend == GuiBackend.Gtk3,
    "Automatic selection must work with GTK3 alone.");
Require(BackendSelection.Select(GuiBackend.Automatic, false, mwinOnly).Backend == GuiBackend.MWin,
    "Automatic selection must work with GTK4 alone.");
Require(BackendSelection.Select(GuiBackend.Gtk3, false, both).Backend == GuiBackend.Gtk3,
    "A saved GTK3 choice must override the automatic preference.");
BackendSelection fallback = BackendSelection.Select(GuiBackend.MWin, false, gtkOnly);
Require(fallback.Backend == GuiBackend.Gtk3 && fallback.Warning.Contains("MWin", StringComparison.Ordinal),
    "A missing saved backend must fall back with a useful warning.");
Reject(() => BackendSelection.Select(GuiBackend.MWin, true, gtkOnly), "GTK4 absent");
Reject(() => BackendSelection.Select(GuiBackend.Gtk3, true, mwinOnly), "GTK3 absent");
Reject(() => BackendSelection.Select(GuiBackend.Automatic, false, new BackendAvailability(false, false)), "GTK");
BackendCommandLine parsed = BackendCommandLine.Parse(new[] { "--backend", "gTk4", "/tmp/あいう.mnx" });
Require(parsed.Preference == GuiBackend.MWin && parsed.HasOverride, "GTK4 alias parsing failed.");
Require(parsed.Arguments.SequenceEqual(new[] { "/tmp/あいう.mnx" }), "Unicode archive argument changed.");
parsed = BackendCommandLine.Parse(new[] { "--extract-here", "--backend=GTK3", "--", "--backend=mwin" });
Require(parsed.Preference == GuiBackend.Gtk3 && parsed.Arguments.SequenceEqual(new[] { "--extract-here", "--", "--backend=mwin" }),
    "Backend parsing damaged an extraction request or literal filename.");
parsed = BackendCommandLine.Parse(new[] { "--", "--backend", "gtk3" });
Require(!parsed.HasOverride && parsed.Arguments.SequenceEqual(new[] { "--", "--backend", "gtk3" }), "Literal -- arguments were consumed.");
Require(BackendCommandLine.Parse(new[] { "--backend=auto" }).HasOverride, "Explicit auto was lost.");
Reject(() => BackendCommandLine.Parse(new[] { "--backend=invalid" }), "backend");
Reject(() => BackendCommandLine.Parse(new[] { "--backend" }), "value");
Reject(() => BackendCommandLine.Parse(new[] { "--backend=gtk3", "--backend=mwin" }), "once");
Require(BackendSelection.ParsePreference("not-a-backend") == GuiBackend.Automatic, "Unknown INI value must recover to auto.");
Require(BackendSelection.ParsePreference("MWIN") == GuiBackend.MWin, "INI choice must be case insensitive.");
string directory = Path.Combine(Path.GetTempPath(), "mnx-backend-check-" + Guid.NewGuid().ToString("N"));
try {
    AppSettings settings = new AppSettings(directory);
    Require(settings.OpacityPercent == 100, "The main window must default to opaque.");
    settings.Set("Interface", "OpacityPercent", 65);
    Require(settings.OpacityPercent == 65, "A configured opacity was ignored.");
    settings.Set("Interface", "OpacityPercent", -1);
    Require(settings.OpacityPercent == 10, "Opacity must remain usable with an invalid low INI value.");
    settings.Set("Interface", "OpacityPercent", 101);
    Require(settings.OpacityPercent == 100, "Opacity must not exceed 100 percent.");
    settings.Set("Interface", "OpacityPercent", "broken");
    Require(settings.OpacityPercent == 100, "Invalid opacity text must recover to opaque.");
    settings.Set("Interface", "OpacityPercent", 65);
    settings.Set("Defaults", "Encrypt", true);
    settings.Set("Unknown", "Keep", "あいう");
    settings.Set("Interface", "Backend", "gtk3");
    settings.Save();
    Require(BackendRuntime.LoadPreference(directory) == GuiBackend.Gtk3, "Saved interface preference was not read.");
    AppSettings edited = AppSettings.Load(directory);
    AppSettings cancelled = edited.Copy();
    cancelled.Set("Interface", "Backend", "mwin");
    Require(BackendRuntime.LoadPreference(directory) == GuiBackend.Gtk3, "Unsaved options changed the preference.");
    edited.Set("Interface", "Backend", "mwin");
    edited.Save();
    AppSettings reloaded = AppSettings.Load(directory);
    Require(reloaded.OpacityPercent == 65, "Saving another interface option lost the opacity setting.");
    Require(reloaded.Flag("Defaults", "Encrypt") && reloaded.Get("Unknown", "Keep", "") == "あいう",
        "Saving the backend preference lost unrelated INI values.");
    Require(BackendRuntime.LoadPreference(directory) == GuiBackend.MWin, "MWin preference did not survive saving.");
    parsed = BackendCommandLine.Parse(new[] { "--backend=gtk3" });
    Require(BackendSelection.Select(parsed.Preference, parsed.HasOverride, both).Backend == GuiBackend.Gtk3,
        "CLI override failed to choose GTK3.");
    Require(BackendRuntime.LoadPreference(directory) == GuiBackend.MWin, "CLI override rewrote saved preference.");
    }
finally {
    Directory.Delete(directory, true);
    }
Console.WriteLine("PASS: " + checks + " backend policy, argument and INI checks.");

string overrideDirectory = Path.Combine(Path.GetTempPath(), "mnx-preview-overrides-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(overrideDirectory);
try {
    string overrideFile = Path.Combine(overrideDirectory, "mnxarchiver.conf");
    System.IO.File.WriteAllText(overrideFile, "[MNX.Previews]\nBufferMiB=32\nDecodedMiB=512\nTotalMiB=1024\n[MNX.Previews.OVERRIDE]\nBufferMiB=512\nDecodedMiB=5120\nTotalMiB=8192\nDebugFlags=1\n");
    AppSettings settings = AppSettings.Load(overrideDirectory);
    Require(settings.PreviewBufferBytes == 512 * 1024 * 1024,
        "The explicit input-buffer override was reset to the ordinary maximum.");
    Require(settings.DecodedPreviewBytes == 5120L * 1024 * 1024 && settings.TotalPreviewBytes == 8192L * 1024 * 1024,
        "The large decoded/combined overrides were reset or overflowed a 32-bit counter.");
    Require(settings.HalfResolutionPreviews, "DebugFlags bit 0 did not enable half-resolution previews.");
    settings.Save();
    AppSettings saved = AppSettings.Load(overrideDirectory);
    Require(saved.HalfResolutionPreviews && saved.Copy().HalfResolutionPreviews, "Debug flags did not survive save or Options copying.");
    foreach (int flags in new int[] { 0, 2, 128, 255, 256, -1 }) {
        saved.Set("MNX.Previews.OVERRIDE", "DebugFlags", flags);
        Require(saved.HalfResolutionPreviews == (flags == 255), "DebugFlags accepted an out-of-byte value or confused an unknown bit with bit 0.");
        }
    saved.Set("MNX.Previews.OVERRIDE", "DebugFlags", 1);
    Require(saved.TotalPreviewBytes == 8192L * 1024 * 1024 && saved.Get("Previews", "TotalMiB", "") == "1024",
        "Saving unrelated options must preserve the override and ordinary setting separately.");
    saved.Set("MNX.Previews.OVERRIDE", "BufferMiB", int.MaxValue);
    saved.Set("MNX.Previews.OVERRIDE", "DecodedMiB", int.MaxValue);
    saved.Set("MNX.Previews.OVERRIDE", "TotalMiB", int.MaxValue);
    Require(saved.PreviewBufferBytes == 512 * 1024 * 1024 && saved.DecodedPreviewBytes == 5120L * 1024 * 1024
        && saved.TotalPreviewBytes == 8192L * 1024 * 1024, "Overrides exceeded their hard maxima.");
    saved.Set("MNX.Previews.OVERRIDE", "BufferMiB", "broken");
    saved.Set("MNX.Previews.OVERRIDE", "DecodedMiB", "-1");
    Require(saved.PreviewBufferBytes == 32 * 1024 * 1024 && saved.DecodedPreviewBytes == 512L * 1024 * 1024,
        "Malformed overrides must retain normal settings.");
    // Revert reloads disk, preserving newer unrelated edits rather than saving
    // the old Options snapshot or leaving an empty override section behind.
    AppSettings latest = AppSettings.Load(overrideDirectory);
    latest.Set("Future", "Keep", "ひらがな");
    latest.Save();
    saved.RevertPreviewOverrides();
    AppSettings reverted = AppSettings.Load(overrideDirectory);
    Require(!reverted.HasPreviewOverrides && !System.IO.File.ReadAllText(overrideFile).Contains("[MNX.Previews.OVERRIDE]"),
        "Revert left the override section in the saved file.");
    Require(reverted.Get("Future", "Keep", "") == "ひらがな" && reverted.TotalPreviewBytes == 1024L * 1024 * 1024,
        "Revert changed unrelated settings or failed to restore ordinary limits.");
    Require(!reverted.HalfResolutionPreviews && !reverted.HasPreviewOverrides, "Reading default debug flags recreated the removed section.");
    Console.WriteLine("PASS: preview override limits and independent INI persistence.");
    }
finally {
    Directory.Delete(overrideDirectory, true);
    }


string paletteWork = Path.Combine(Path.GetTempPath(), "mnx-palette-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(paletteWork);
try {
    string kde = Path.Combine(paletteWork, "kdeglobals");
    System.IO.File.WriteAllText(kde, "[Colors:Window]\nBackgroundNormal=32,35,38\nForegroundNormal=252,252,252\n[Colors:View]\nBackgroundNormal=20,22,24\n[Colors:Window][IgnoredNested]\nBackgroundNormal=9,9,9\n[WM]\nactiveForeground=240,241,242\n");
    MWinColors imported = new MWinColors();
    imported.ImportKde(kde);
    Require(imported["WindowBackground"] == 0xFF202326 && imported["ViewBackground"] == 0xFF141618,
        "KDE desktop colors were not imported exactly.");
    MWinColors created = MWinColors.LoadOrCreate(paletteWork, () => imported);
    Require(created.CaptionHeight == 35 && created.CaptionFontPoints == 12f, "Default caption must scale to 35 pixels and 12 points.");
    string captionFile = Path.Combine(paletteWork, "caption-test.conf");
    File.WriteAllText(captionFile, "[mnx.display.mwin.window]\nwmTitleHeight=33\nColor.tfSize=10.5\n");
    MWinColors previousCaption = MWinColors.Read(captionFile);
    Require(previousCaption.CaptionHeight == 44 && previousCaption.CaptionFontPoints == 14f,
        "Existing caption settings must scale together.");
    File.AppendAllText(captionFile, "wmTitleScale=1\n");
    MWinColors unscaledCaption = MWinColors.Read(captionFile);
    Require(unscaledCaption.CaptionHeight == 33 && unscaledCaption.CaptionFontPoints == 10.5f,
        "An explicit scale of one must use the configured measurements verbatim.");
    string paletteFile = Path.Combine(paletteWork, MWinColors.FileName);
    string generated = System.IO.File.ReadAllText(paletteFile);
    Require(Path.GetFileName(paletteFile) == "gui.mwin.drawing.conf", "The drawing palette filename is incorrect.");
    Require(generated.Contains("[mnx.display.mwin.window]") && generated.Contains("Color.wmBack=#202326") &&
        generated.Contains("[mnx.display.mwin.buttons]") && generated.Contains("[mnx.display.mwin.text]") &&
        generated.Contains("[mnx.display.mwin.files]") && generated.Contains("BackColor=#141618"),
        "The drawing file must use the requested sections and desktop colors.");
    Require(created["TitleText"] == 0xFFF0F1F2, "Title colors did not survive creation.");
    Require(created.GetString(MWinColors.FilesSection, "Font", "") == "Segoe UI" &&
        created.GetFloat(MWinColors.FilesSection, "Size", 0, 6, 48) == 9f &&
        created.GetString(MWinColors.FilesSection, "ColumnHeaders.Font", "") == "Segoe UI" &&
        created.GetFloat(MWinColors.FilesSection, "ColumnHeaders.Size", 0, 6, 48) == 9f &&
        created.GetColor(MWinColors.FilesSection, "ColumnHeaders.BackColor", 0) == created["ButtonBackground"] &&
        created.GetColor(MWinColors.FilesSection, "ColumnHeaders.HighlightColor", 0) == created["Accent"] &&
        created.GetColor(MWinColors.FilesSection, "ColumnHeaders.Color", 0) == created["ButtonText"],
        "The drawing file must supply the default file and column header fonts and header palette.");
    Require(created["MenuBackground"] == imported["WindowBackground"], "Menu backgrounds must inherit the desktop palette.");
    Require(generated.Contains("[mnx.display.mwin.menus]"), "New palettes must include editable menu settings.");
    const string legacy = "[MNX.Display.MWin]\nWindowBackground=#202326\nWindowText=#FCFCFC\nSelectionBackground=#334455\nSelectionText=#EEEEEE\nTitleBackground=#334455\n[MNX.Display.MWin.Menus]\nBackground=#112233\nText=240,241,242\nHoverText=invalid\n";
    string legacyFile = Path.Combine(paletteWork, "mnx.mwin-colors.conf");
    System.IO.File.WriteAllText(legacyFile, legacy);
    System.IO.File.Delete(paletteFile);
    MWinColors.LoadOrCreate(paletteWork, () => throw new Exception("Migrating a palette must not read the desktop."));
    Require(System.IO.File.ReadAllText(legacyFile) == legacy, "Migrating colors changed the original palette.");
    Require(System.IO.File.ReadAllText(paletteFile).Contains("Color.wtBack=#334455"), "The legacy title background was not migrated.");
    MWinColors menuColors = MWinColors.Read(paletteFile);
    Require(menuColors["MenuBackground"] == 0xFF112233 && menuColors["MenuText"] == 0xFFF0F1F2,
        "Menu overrides did not load from their INI section.");
    Require(menuColors.GetColor(MWinColors.TextSection, "Color", 0) == 0xFFF0F1F2,
        "The legacy menu text override did not migrate to the drawing text color.");
    Require(menuColors["MenuBarBackground"] == 0xFF202326 && menuColors["MenuHoverText"] == 0xFFEEEEEE,
        "Missing or invalid menu colors must inherit the configured palette, not light defaults.");
    System.IO.File.WriteAllText(paletteFile, """
        [mnx.display.mwin.window]
        Color.wmBack=#123456
        Color.wmfColor=250,249,248
        Accent=999,0,0
        controlBox.Exit.Colors.Highlight=#ABCDEF, #235689 ; center, edge
        controlBox.Exit.Colors.Pressed=#873412
        controlBox.MinMax.Colors.Pressed=#112233,invalid
        dividerMargin=8
        wmTitleHeight=-1

        [mnx.display.mwin.files]
        BackColor=invalid
        Size=10.5
        ColumnHeaders.Size=NaN
        """);
    MWinColors edited = MWinColors.LoadOrCreate(paletteWork, () => throw new Exception("Existing palette must not read the desktop."));
    Require(edited["WindowBackground"] == 0xFF123456 && edited["WindowText"] == 0xFFFAF9F8,
        "Existing manual palette edits must take precedence.");
    Require(edited["ViewBackground"] == 0xFFFFFFFF && edited["Accent"] == 0xFF0078D7,
        "Invalid individual colors must retain readable fallbacks.");
    Require(edited.GetColorPair(MWinColors.WindowSection, "controlBox.Exit.Colors.Highlight", 0) == (0xFFABCDEFu, 0xFF235689u) &&
        edited.GetColorPair(MWinColors.WindowSection, "controlBox.Exit.Colors.Pressed", 0) == (0xFF873412u, (uint?)null) &&
        edited.GetColorPair(MWinColors.WindowSection, "controlBox.MinMax.Colors.Pressed", 1, 2) == (1u, 2u),
        "Caption buttons must accept a solid color or a center/edge pair and reject malformed pairs.");
    Require(edited.GetInt(MWinColors.WindowSection, "dividerMargin", 6, 0, 64) == 8 &&
        edited.GetInt(MWinColors.WindowSection, "wmTitleHeight", 26, 18, 256) == 26 &&
        edited.GetFloat(MWinColors.FilesSection, "Size", 9f, 6f, 48f) == 10.5f &&
        edited.GetFloat(MWinColors.FilesSection, "ColumnHeaders.Size", 9f, 6f, 48f) == 9f,
        "Drawing dimensions must respect numeric ranges and default when invalid.");
    Require(System.IO.File.ReadAllText(paletteFile).Contains("BackColor=invalid"), "Reading a palette must never rewrite user edits.");
    Console.WriteLine("PASS: drawing INI creation, KDE import, legacy migration, custom precedence and malformed color fallbacks.");
    }
finally {
    Directory.Delete(paletteWork, true);
    }

INI_Config typed = new INI_Config();
INI_Config.Section values = typed["Typed"];
values.Set("boolean", "yes");
values.Set("byte", "256");
values.Set("short", short.MinValue);
values.Set("int", int.MaxValue);
values.Set("long", long.MaxValue);
values.Set("longlong", Int128.MaxValue);
values.Set("float", 1.25f);
values.Set("double", 2.5d);
values.Set("bad", "NaN");
values.SetList("numbers", new int[] { 1, -2, 300 });
values.SetList("strings", new string[] { "ひらがな", "a;b", "  spaced  ", "say \"yes\"" });
Require(values.GetBool("boolean") && values.GetByte("byte", 42) == 42, "Typed bool/overflow fallbacks failed.");
Require(values.GetShort("short") == short.MinValue && values.GetInt("int") == int.MaxValue && values.GetLong("long") == long.MaxValue && values.GetLongLong("longlong") == Int128.MaxValue, "Integer widths did not round trip.");
Require(values.GetFloat("float") == 1.25f && values.GetDouble("double") == 2.5 && values.GetDouble("bad", 12) == 12, "Invariant numeric parsing failed.");
Require(values.GetList<int>("numbers").SequenceEqual(new int[] { 1, -2, 300 }), "Typed integer lists failed.");
Require(values.GetList<string>("strings").SequenceEqual(new string[] { "ひらがな", "a;b", "  spaced  ", "say \"yes\"" }), "Quoted Unicode string lists failed.");
string iniFile = Path.Combine(Path.GetTempPath(), "mnx-ini-" + Guid.NewGuid().ToString("N") + ".conf");
try {
    typed.Save(iniFile);
    INI_Config loaded = INI_Config.Load(iniFile);
    Require(loaded["tYpEd"].GetLong("LONG") == long.MaxValue && loaded["Typed"].GetList<string>("strings").Count == 4, "UTF8 INI persistence/case-insensitive sections failed.");
    bool rejected = false;
    try { loaded["Unsafe\nSection"].Set("x", 1); }
    catch (ArgumentException) { rejected = true; }
    Require(rejected, "INI section injection must be rejected.");
    }
finally { System.IO.File.Delete(iniFile); }
Console.WriteLine("PASS: portable typed INI values, overflow, Unicode semicolon lists, persistence and input validation.");
return 0;
