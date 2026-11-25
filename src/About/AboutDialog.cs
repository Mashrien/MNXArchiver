using Gtk;
using Mash.GUI.MNXArchiver.Plugins;

namespace Mash.GUI.MNXArchiver.About;

internal sealed class AboutDialog : Dialog {
    private readonly Button _reload;
    private readonly Button _plugins;
    private readonly Label _note;
    private readonly AboutModel _model;
    private readonly Func<IReadOnlyDictionary<string, string>, bool> _restart;
    private readonly List<(ListStore Store, TreeIter Row, string Library)> _pluginRows = new List<(ListStore, TreeIter, string)>();
    private PluginDirectoryWatch? _watch;
    private IReadOnlyDictionary<string, string>? _restartBaseline;
    private bool _closed;

    internal AboutDialog(Window owner, Func<IReadOnlyDictionary<string, string>, bool> restart)
        : base("About MNX Archiver", owner, DialogFlags.Modal) {
        _restart = restart;
        _model = new AboutModel(PluginCatalog.Installed);
        SetDefaultSize(780, 700);
        Resizable = false;
        BorderWidth = 12;
        Box content = ContentArea;
        content.Spacing = 8;
        content.PackStart(new Label { Markup = "<span size='x-large' weight='bold'>MNX Archive Manager</span>", Xalign = 0 }, false, false, 0);
        content.PackStart(new Label("GUI v" + _model.GuiVersion + "     MNX v" + _model.FormatVersion) { Xalign = 0 }, false, false, 0);
        content.PackStart(new Label("By Mashrien Aurea     Copyright © MashCode 2026") { Xalign = 0 }, false, false, 0);
        LinkButton source = new LinkButton(AboutModel.SourceUrl, "github.com/Mashrien/MNXArchiver") { Halign = Align.Start };
        CssProvider sourceStyle = new CssProvider();
        sourceStyle.LoadFromData("button:link, button:visited { color: #0000ff; }");
        source.StyleContext.AddProvider(sourceStyle, 700);
        sourceStyle.Dispose();
        content.PackStart(source, false, false, 0);
        Frame functionality = new Frame("Extended Functionality");
        Box providers = new Box(Orientation.Vertical, 6) { BorderWidth = 10 };
        providers.PackStart(new Label("Format Providers (" + _model.Formats.Count + ")") { Xalign = 0 }, false, false, 0);
        providers.PackStart(ProviderList(_model.Formats), true, true, 0);
        providers.PackStart(new Label("Function Providers (" + _model.Functions.Count + ")") { Xalign = 0 }, false, false, 0);
        providers.PackStart(ProviderList(_model.Functions), true, true, 0);
        functionality.Add(providers);
        content.PackStart(functionality, true, true, 0);
        content.PackStart(new Label("Plugin Directory") { Xalign = 0 }, false, false, 0);
        Frame path = new Frame { ShadowType = ShadowType.In };
        path.Add(new Label(_model.DirectoryPath) { Xalign = 0, Selectable = true, Margin = 6 });
        content.PackStart(path, false, false, 0);
        _note = new Label("Program must be restarted to load newly installed plugins.") { Xalign = 0, Wrap = true };
        _note.StyleContext.AddClass("dim-label");
        if (_model.Issues.Count > 0) {
            _note.Text = string.Join("; ", _model.Issues.Take(3)) + "\n" + _note.Text;
            }
        content.PackStart(_note, false, false, 0);
        Box buttons = new Box(Orientation.Horizontal, 8) { MarginTop = 8 };
        _plugins = new Button("Plugins Folder");
        _reload = new Button("Reload") { NoShowAll = true };
        Button close = new Button("Close");
        buttons.PackStart(_plugins, false, false, 0);
        buttons.PackStart(_reload, false, false, 0);
        buttons.PackEnd(close, false, false, 0);
        content.PackStart(buttons, false, false, 0);
        _plugins.Clicked += async (_, _) => await OpenPlugins();
        _reload.Clicked += (_, _) => {
            if (_restartBaseline is not null && _restart(_restartBaseline)) {
                Respond(ResponseType.Close);
                }
            };
        close.Clicked += (_, _) => Respond(ResponseType.Close);
        Response += (_, _) => StopWatch();
        Destroyed += (_, _) => StopWatch();
        DeleteEvent += (_, _) => StopWatch();
        Shown += async (_, _) => await HighlightUpdatedRows();
        ShowAll();
        }

    private ScrolledWindow ProviderList(IReadOnlyList<AboutProviderRow> rows) {
        ListStore store = new ListStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool));
        TreeView view = new TreeView(store) { HeadersVisible = false, EnableSearch = false };
        view.Selection.Mode = SelectionMode.None;
        int[] widths = [45, 185, 72, 224, 170];
        for (int index = 0; index < widths.Length; index++) {
            CellRendererText cell = new CellRendererText();
            TreeViewColumn column = new TreeViewColumn("", cell, "text", index) { MinWidth = widths[index], Resizable = false };
            column.SetCellDataFunc(cell, SetProviderColor);
            view.AppendColumn(column);
            }
        foreach (AboutProviderRow provider in rows) {
            TreeIter row = store.AppendValues(provider.Type, provider.Name, provider.Version, provider.Formats, provider.Library, false);
            if (provider.LibraryFile.Length > 0) {
                _pluginRows.Add((store, row, provider.LibraryFile));
                }
            }
        ScrolledWindow scroll = new ScrolledWindow { ShadowType = ShadowType.None, HeightRequest = 150 };
        scroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
        scroll.Add(view);
        return scroll;
        }

    private static void SetProviderColor(TreeViewColumn column, CellRenderer renderer, ITreeModel model, TreeIter row) {
        CellRendererText text = (CellRendererText)renderer;
        text.Foreground = (bool)model.GetValue(row, 5) ? "#32aa32" : null!;
        }

    private async Task HighlightUpdatedRows() {
        if (AboutRestart.Restored is not AboutRestart restored) {
            return;
            }
        try {
            Dictionary<string, string> current = await Task.Run(() => PluginSnapshot.Capture(_model.DirectoryPath));
            Application.Invoke((_, _) => {
                if (!_closed) {
                    foreach ((ListStore store, TreeIter row, string library) in _pluginRows) {
                        if (PluginSnapshot.IsUpdated(library, restored.PreviousPlugins, current)) {
                            store.SetValue(row, 5, true);
                            }
                        }
                    }
                });
            }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            Application.Invoke((_, _) => {
                if (!_closed) {
                    _note.Text = error.Message;
                    }
                });
            }
        }

    private async Task OpenPlugins() {
        _plugins.Sensitive = false;
        try {
            if (_watch?.IsStopped == true) {
                _watch = null;
                }
            _watch ??= new PluginDirectoryWatch(_model.DirectoryPath,
                action => Application.Invoke((_, _) => action()), () => _reload.Show());
            await _watch.StartAsync();
            _restartBaseline ??= _watch.Baseline;
            Application.Invoke((_, _) => {
                if (!_closed) {
                    try {
                        PluginCatalog.Installed.OpenDirectory();
                        }
                    catch (Exception error) {
                        _note.Text = error.Message;
                        }
                    _plugins.Sensitive = true;
                    }
                });
            }
        catch (OperationCanceledException) {
            }
        catch (Exception error) {
            Application.Invoke((_, _) => {
                if (!_closed) {
                    _note.Text = error.Message;
                    _plugins.Sensitive = true;
                    }
                });
            }
        }

    private void StopWatch() {
        _closed = true;
        _watch?.Dispose();
        }
    }
