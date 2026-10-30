using Gtk;
using Mash.UI.MNXFile.GUI.Previews;
using Mash.UI.MNXFile.GUI.Settings;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    internal const int MaxPreviewWindows = 10;
    private readonly List<PreviewWindow> _previews = [];
    private CancellationTokenSource? _previewCancellation;
    private Task<PreviewData>? _previewRead;
    private uint _previewPoll;
    private uint _hoverTimer;
    private uint _hoverWatch;
    private Guid _hoveredFile;
    private bool _stoppingPreviews;
    private bool _loadingPreview;
    private bool _previewForHover;
    private Guid _singleClickFile;
    private int _singleClickX;
    private int _singleClickY;
    private long _hoverGeneration;
    internal int TestPreviewCount => _previews.Count;
    internal IReadOnlyList<PreviewWindow> TestPreviews => _previews;
    internal AppSettings TestSettings => _settings;

    private void ConfigurePreviews() {
        _list.AddEvents((int)(Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask));
        // GTK may consume motion in its drag handler. EventAfter still observes
        // handled events without interfering with drag selection or navigation.
        _list.WidgetEventAfter += OnListPointerEvent;
        // Only hover previews follow pointer/focus departure. A click-triggered
        // tooltip must stay visible while the pointer moves onto its image.
        _list.LeaveNotifyEvent += (_, _) => CancelHover(closePopups: _settings.Trigger == "Mouse-Over With Delay");
        _list.ButtonPressEvent += (_, _) => CancelHover(closePopups: true);
        _list.DragBegin += (_, _) => { _singleClickFile = Guid.Empty; CancelHover(closePopups: true); };
        _window.FocusOutEvent += (_, _) => CancelHover(closePopups: _settings.Trigger == "Mouse-Over With Delay");
        }

    private Guid FileAt(double x, double y, Gdk.Window eventWindow, out Gdk.Rectangle anchor) {
        anchor = default;
        int binX = (int)x;
        int binY = (int)y;
        if (eventWindow.Handle != _list.BinWindow.Handle) {
            _list.ConvertWidgetToBinWindowCoords(binX, binY, out binX, out binY);
            }
        if (!_list.GetPathAtPos(binX, binY, out TreePath path)) {
            return Guid.Empty;
            }
        using (path) {
            if (!_files.GetIter(out TreeIter row, path) || (bool)_files.GetValue(row, 7)) {
                return Guid.Empty;
                }
            Guid.TryParse((string)_files.GetValue(row, 6), out Guid id);
            anchor = _list.GetCellArea(path, _list.Columns[0]);
            _list.BinWindow.GetOrigin(out int rootX, out int rootY);
            anchor.X += rootX;
            anchor.Y += rootY;
            return id;
            }
        }

    private void OnListPointerEvent(object sender, WidgetEventAfterArgs args) {
        if (args.Event is Gdk.EventButton button && button.Button == 1) {
            if (button.Type == Gdk.EventType.ButtonPress) {
                _singleClickFile = FileAt(button.X, button.Y, button.Window, out _);
                _singleClickX = (int)button.X;
                _singleClickY = (int)button.Y;
                }
            else if (button.Type == Gdk.EventType.ButtonRelease) {
                Guid pressed = _singleClickFile;
                _singleClickFile = Guid.Empty;
                Guid released = FileAt(button.X, button.Y, button.Window, out _);
                if (_settings.Trigger == "Single-Click" && pressed != Guid.Empty && released == pressed && !Drag.CheckThreshold(_list, _singleClickX, _singleClickY, (int)button.X, (int)button.Y)) {
                    PreviewEntry(pressed);
                    }
                }
            }
        else if (args.Event is Gdk.EventMotion motion) {
            UpdatePreviewHover(motion.X, motion.Y, motion.Window, motion.State, (int)motion.XRoot, (int)motion.YRoot);
            }
        else if (args.Event is Gdk.EventCrossing crossing && crossing.Type == Gdk.EventType.EnterNotify) {
            // A direct move into a row can produce only an Enter event, without
            // a following motion event until the pointer moves again.
            UpdatePreviewHover(crossing.X, crossing.Y, crossing.Window, crossing.State, (int)crossing.XRoot, (int)crossing.YRoot);
            }
        }

    private void StartHoverWatch() {
        if (_hoverWatch != 0 || !_settings.Previews || _settings.Trigger != "Mouse-Over With Delay") {
            return;
            }
        // Check row identity while the pointer is in the list. GTK's drag
        // gestures can consume motion; this also catches scrolling beneath a
        // stationary cursor. No archive I/O happens in this timer.
        _hoverWatch = GLib.Timeout.Add(75, () => {
            if (_disposed || _operationActive || _stoppingPreviews || !_settings.Previews || _settings.Trigger != "Mouse-Over With Delay") {
                _hoverWatch = 0;
                CancelHover(closePopups: true);
                return false;
                }
            Gdk.Device pointer = _list.Display.DefaultSeat.Pointer;
            Gdk.Window? hit = pointer.GetWindowAtPosition(out int x, out int y);
            if (hit is null || hit.Handle != _list.BinWindow.Handle) {
                _hoverWatch = 0;
                CancelHover(closePopups: true);
                return false;
                }
            _list.BinWindow.GetOrigin(out int rootX, out int rootY);
            _list.BinWindow.GetDevicePosition(pointer, out _, out _, out Gdk.ModifierType state);
            UpdatePreviewHover(x, y, _list.BinWindow, state, rootX + x, rootY + y);
            return true;
            });
        }

    private void UpdatePreviewHover(double x, double y, Gdk.Window eventWindow, Gdk.ModifierType state, int cursorX, int cursorY) {
        if (!_settings.Previews || _settings.Trigger != "Mouse-Over With Delay" || _operationActive || _loadingPreview || _stoppingPreviews || (state & Gdk.ModifierType.Button1Mask) != 0) {
            return;
            }
        StartHoverWatch();
        Guid id = FileAt(x, y, eventWindow, out Gdk.Rectangle anchor);
        if (id != Guid.Empty && !CanPreview(id)) {
            id = Guid.Empty;
            }
        if (id == _hoveredFile) {
            return;
            }
        CancelHover(closePopups: true);
        _hoveredFile = id;
        if (id == Guid.Empty) {
            return;
            }
        _hoverTimer = GLib.Timeout.Add((uint)_settings.HoverDelay, () => {
            if (id == _hoveredFile && !_operationActive && _previewRead is not null) {
                return true;
                }
            _hoverTimer = 0;
            if (id == _hoveredFile && !_operationActive) {
                PreviewEntry(id, anchor, cursorX, cursorY, hoverRequest: true);
                }
            return false;
            });
        }

    private void CancelHover(bool closePopups = false) {
        _hoverGeneration++;
        if (_previewForHover) {
            _previewCancellation?.Cancel();
            }
        if (_hoverTimer != 0) {
            GLib.Source.Remove(_hoverTimer);
            _hoverTimer = 0;
            }
        _hoveredFile = Guid.Empty;
        if (closePopups) {
            foreach (PreviewWindow preview in _previews.ToArray()) {
                if (preview.IsPopup) {
                    preview.Dispose();
                    }
                }
            }
        }

    private bool CanPreview(Guid id) {
        if (!_settings.Previews || _operationActive || _archive is null || _browser is null || id == Guid.Empty) {
            return false;
            }
        ArchiveBrowser.Entry entry = _browser.Get(id);
        return !entry.IsDirectory && entry.Bytes <= (ulong)_settings.PreviewBufferBytes && PreviewData.Classify(entry.Name, _settings) != PreviewKind.Unsupported;
        }

    internal void PreviewEntry(Guid id, Gdk.Rectangle anchor = default, int cursorX = 0, int cursorY = 0, bool hoverRequest = false) {
        if (!CanPreview(id) || _previewRead is not null || _stoppingPreviews || _loadingPreview) {
            _status.Text = "Preview unavailable: enable a supported type in Options; files must fit the configured preview buffer.";
            return;
            }
        if (!_settings.IndependentWindows) {
            foreach (PreviewWindow preview in _previews.ToArray()) {
                if (!preview.Independent) {
                    preview.Dispose();
                    }
                }
            }
        if (_previews.Count >= MaxPreviewWindows) {
            _status.Text = "Close a preview first: at most 10 preview windows may be open.";
            return;
            }
        if (_previews.Sum(preview => preview.MemoryCost) >= _settings.TotalPreviewBytes) {
            _status.Text = "Close a preview first: the combined preview memory limit has been reached.";
            return;
            }
        if (anchor.Width == 0 && anchor.Height == 0) {
            anchor = PreviewAnchor(id);
            Gdk.Device pointer = _list.Display.DefaultSeat.Pointer;
            _window.Window.GetDevicePosition(pointer, out int pointerX, out int pointerY, out _);
            _window.Window.GetOrigin(out int originX, out int originY);
            cursorX = originX + pointerX;
            cursorY = originY + pointerY;
            }
        ArchiveFile archive = _archive!;
        ArchiveBrowser.Entry entry = _browser!.Get(id);
        AppSettings settings = _settings.Copy();
        PreviewKind kind = PreviewData.Classify(entry.Name, settings);
        long hoverGeneration = _hoverGeneration;
        _previewForHover = hoverRequest;
        _previewCancellation = new CancellationTokenSource();
        CancellationToken cancellation = _previewCancellation.Token;
        _previewRead = Task.Run(() => PreviewData.Read(archive, id, kind, cancellation, settings.PreviewBufferBytes));
        _status.Text = "Reading preview: " + entry.Name;
        _previewPoll = GLib.Timeout.Add(25, () => {
            if (_previewRead is null) {
                _previewPoll = 0;
                return false;
                }
            if (!_previewRead.IsCompleted) {
                return true;
                }
            Task<PreviewData> task = _previewRead;
            _previewRead = null;
            _previewPoll = 0;
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            PreviewData? data = null;
            _loadingPreview = true;
            try {
                data = task.GetAwaiter().GetResult();
                if (hoverRequest && hoverGeneration != _hoverGeneration) {
                    return false;
                    }
                long availableMemory = settings.TotalPreviewBytes - _previews.Sum(open => open.MemoryCost);
                PreviewWindow preview = new PreviewWindow(_window, entry.Name, data, settings, anchor, cursorX, cursorY, availableMemory);
                data = null; // ownership moves into the preview window
                _previews.Add(preview);
                preview.Closed += closed => _previews.Remove(closed);
                preview.Show();
                _status.Text = "Preview: " + entry.Name;
                }
            catch (OperationCanceledException) {
                }
            catch (Exception error) {
                _status.Text = "Could not preview " + entry.Name + ": " + error.Message;
                }
            finally {
                data?.Dispose();
                _loadingPreview = false;
                _previewForHover = false;
                }
            return false;
            });
        }

    private Gdk.Rectangle PreviewAnchor(Guid id) {
        if (_files.GetIterFirst(out TreeIter row)) {
            do {
                if (Guid.TryParse((string)_files.GetValue(row, 6), out Guid rowId) && rowId == id) {
                    using TreePath path = _files.GetPath(row);
                    Gdk.Rectangle anchor = _list.GetCellArea(path, _list.Columns[0]);
                    _list.BinWindow.GetOrigin(out int rootX, out int rootY);
                    anchor.X += rootX;
                    anchor.Y += rootY;
                    return anchor;
                    }
                }
            while (_files.IterNext(ref row));
            }
        _window.Window.GetOrigin(out int windowX, out int windowY);
        return new Gdk.Rectangle(windowX, windowY, 1, 1);
        }

    private void StopPreviewRead() {
        if (_stoppingPreviews) {
            return;
            }
        _stoppingPreviews = true;
        if (_hoverWatch != 0) {
            GLib.Source.Remove(_hoverWatch);
            _hoverWatch = 0;
            }
        try {
            CancelHover(closePopups: true);
            if (_previewPoll != 0) {
                GLib.Source.Remove(_previewPoll);
                _previewPoll = 0;
                }
            _previewCancellation?.Cancel();
            if (_previewRead is not null) {
                Task<PreviewData> task = _previewRead;
                _previewRead = null;
                // Finish the bounded read before closing/mutating its archive. Pump
                // input while the worker releases its stream, without starting more work.
                while (!task.IsCompleted) {
                    for (int count = 0; count < 16 && Application.EventsPending(); count++) {
                        Application.RunIteration(false);
                        }
                    Thread.Sleep(10);
                    }
                if (task.IsCompletedSuccessfully) {
                    task.Result.Dispose();
                    }
                else {
                    _ = task.Exception; // observe a failed worker, including cancellation
                    }
                }
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            }
        finally {
            _stoppingPreviews = false;
            }
        }
    private void ClosePreviews() {
        StopPreviewRead();
        foreach (PreviewWindow preview in _previews.ToArray()) {
            preview.Dispose();
            }
        }
    }
