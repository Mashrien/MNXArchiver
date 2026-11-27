using Gtk;

namespace Mash.GUI.MNXArchiver.Diagnostics;

/// <summary>GTK fallback for the same bounded in-memory diagnostic history.</summary>
internal sealed class DebugWindow : IDisposable {
    private readonly Window _window;
    private readonly ListStore _rows;
    private readonly TreeView _view;
    private long _cursor;
    private uint _timer;
    private int _rowCount;
    private bool _disposed;

    internal DebugWindow(Window owner) {
        _window = new Window("Debug Window") { TransientFor = owner, DestroyWithParent = true, Opacity = owner.Opacity };
        _window.SetDefaultSize(1020, 480);
        _rows = new ListStore(typeof(string), typeof(string), typeof(string), typeof(string));
        _view = new TreeView(_rows) { HeadersVisible = false, EnableSearch = false, TooltipColumn = 2 };
        int[] widths = [140, 260, 600];
        for (int index = 0; index < 3; index++) {
            CellRendererText cell = new CellRendererText();
            TreeViewColumn column = new TreeViewColumn("", cell, "text", index, "foreground", 3) {
                MinWidth = widths[index],
                Resizable = true,
                Expand = index == 2,
                };
            _view.AppendColumn(column);
            }
        CssProvider appearance = new CssProvider();
        appearance.LoadFromData("treeview.view { background-color: #000000; color: #808080; font-family: monospace; font-size: 10pt; }");
        _view.StyleContext.AddProvider(appearance, 700);
        appearance.Dispose();
        ScrolledWindow scroll = new ScrolledWindow { ShadowType = ShadowType.None };
        scroll.Add(_view);
        _window.Add(scroll);
        _window.DeleteEvent += (_, args) => {
            args.RetVal = true;
            _window.Hide();
            StopTimer();
            };
        _window.Destroyed += (_, _) => Dispose();
        }

    internal void Show() {
        if (_disposed) {
            return;
            }
        _window.ShowAll();
        _window.Present();
        UpdateLog();
        if (_timer == 0) {
            _timer = GLib.Timeout.Add(150, () => {
                if (_disposed) {
                    _timer = 0;
                    return false;
                    }
                UpdateLog();
                return true;
                });
            }
        }

    private void UpdateLog() {
        DebugLogBatch batch = DebugLog.Buffer.ReadBatch(_cursor);
        _cursor = batch.Cursor;
        TreeIter last = default;
        foreach (DebugLogRecord record in batch.Records) {
            System.Drawing.Color color = DebugLog.ColorFor(record.Type);
            string foreground = "#" + color.R.ToString("x2") + color.G.ToString("x2") + color.B.ToString("x2");
            last = _rows.AppendValues(record.Type.ToString(), record.Caller, record.Message, foreground);
            _rowCount++;
            }
        while (_rowCount > DebugLog.Buffer.Capacity && _rows.GetIterFirst(out TreeIter oldest)) {
            _rows.Remove(ref oldest);
            _rowCount--;
            }
        if (batch.Records.Count > 0) {
            using TreePath path = _rows.GetPath(last);
            _view.ScrollToCell(path, null, false, 0, 0);
            }
        }

    private void StopTimer() {
        if (_timer != 0) {
            GLib.Source.Remove(_timer);
            _timer = 0;
            }
        }

    public void Dispose() {
        if (_disposed) {
            return;
            }
        _disposed = true;
        StopTimer();
        _window.Destroy();
        _rows.Dispose();
        _window.Dispose();
        }
    }
