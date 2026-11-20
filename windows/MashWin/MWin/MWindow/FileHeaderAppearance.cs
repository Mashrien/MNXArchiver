using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MashWin;

internal static class FileHeaderAppearance {
    private static readonly ConditionalWeakTable<ListView, HeaderWindow> Headers = new();

    internal static void Apply(ListView list, Color background, Color highlight, Font font, Color text) {
        Headers.GetValue(list, static control => new HeaderWindow(control)).Apply(background, highlight, font, text);
    }

    // A native header sends its paint notifications to its parent list view. Intercept only
    // those notifications so the list continues to handle row drawing and header interaction.
    private sealed class HeaderWindow : NativeWindow {
        private const int WmSetFont = 0x0030;
        private const int WmWindowPosChanged = 0x0047;
        private const int WmNotify = 0x004e;
        private const int LvmGetHeader = 0x101f;
        private const int HdmGetItemRect = 0x1207;
        private const uint RelayoutWindow = 0x0002 | 0x0004 | 0x0010; // NOMOVE | NOZORDER | NOACTIVATE
        private const int NmCustomDraw = -12;
        private const int PrePaint = 0x0001;
        private const int PostPaint = 0x0002;
        private const int ItemPrePaint = 0x00010001;
        private const int NotifyPostPaint = 0x0010;
        private const int NotifyItemDraw = 0x0020;
        private const int SkipDefault = 0x0004;
        private const int ItemPressed = 0x0001;

        private readonly ListView _list;
        private readonly HeaderPointer _pointer = new();
        private Font _font;
        private nint _fontHandle;
        private nint _header;
        private Color _background;
        private Color _highlight;
        private Color _text;

        internal HeaderWindow(ListView list) {
            _list = list;
            list.HandleCreated += (_, _) => Attach();
            list.HandleDestroyed += (_, _) => _header = 0;
            list.Disposed += (_, _) => {
                if (_fontHandle != 0) {
                    DeleteObject(_fontHandle);
                    _fontHandle = 0;
                }
            };
        }

        internal void Apply(Color background, Color highlight, Font font, Color text) {
            bool fontChanged = _font is null || !_font.Equals(font);
            _font = font;
            _background = background;
            _highlight = highlight;
            _text = text;

            nint previousFont = _fontHandle;
            if (fontChanged) {
                _fontHandle = font.ToHfont();
            }

            if (_list.IsHandleCreated && Handle == 0) {
                Attach();
            }
            else if (fontChanged) {
                ApplyHeaderFont();
            }

            if (fontChanged && previousFont != 0) {
                DeleteObject(previousFont);
            }
            if (_header != 0) {
                InvalidateRect(_header, 0, false);
            }
        }

        private void Attach() {
            AssignHandle(_list.Handle);
            _header = SendMessage(Handle, LvmGetHeader, 0, 0);
            if (_header == 0) {
                return;
            }
            _pointer.Attach(_header);
            ApplyHeaderFont();
        }

        private void ApplyHeaderFont() {
            if (_header == 0 || _fontHandle == 0) {
                return;
            }

            SendMessage(_header, WmSetFont, _fontHandle, 0);
            // Recalculate the list's native header layout using the new header font metrics.
            if (GetWindowRect(Handle, out NativeRectangle bounds)) {
                WindowPosition position = new() {
                    Window = Handle,
                    X = bounds.Left,
                    Y = bounds.Top,
                    Width = bounds.Right - bounds.Left,
                    Height = bounds.Bottom - bounds.Top,
                    Flags = RelayoutWindow,
                };
                SendMessage(Handle, WmWindowPosChanged, 0, ref position);
            }
        }

        protected override void WndProc(ref Message message) {
            if (message.Msg == WmNotify && message.LParam != 0 && _header != 0) {
                Notification notification = Marshal.PtrToStructure<Notification>(message.LParam);
                if (notification.Window == _header && notification.Code == NmCustomDraw) {
                    CustomDraw draw = Marshal.PtrToStructure<CustomDraw>(message.LParam);
                    switch (draw.Stage) {
                        case PrePaint:
                            message.Result = NotifyItemDraw | NotifyPostPaint;
                            return;
                        case ItemPrePaint:
                            if (draw.Item < (nuint)_list.Columns.Count) {
                                DrawColumn(draw);
                                message.Result = SkipDefault;
                                return;
                            }
                            break;
                        case PostPaint:
                            DrawTrailingBackground(draw.DeviceContext);
                            break;
                    }
                }
            }

            base.WndProc(ref message);

            if (message.Msg == WmSetFont) {
                ApplyHeaderFont();
            }
        }

        private void DrawColumn(CustomDraw draw) {
            Rectangle bounds = draw.Bounds.ToRectangle();
            ColumnHeader column = _list.Columns[(int)draw.Item];
            bool highlighted = _pointer.HotColumn == column.Index || (draw.State & ItemPressed) != 0;
            using Graphics graphics = Graphics.FromHdc(draw.DeviceContext);
            DrawBackground(graphics, bounds, highlighted ? _highlight : _background, true);

            int padding = Math.Max(1, (int)Math.Round(_list.DeviceDpi * (6.0 / 96)));
            bounds.Inflate(-padding, 0);
            if (bounds.Width <= 0) {
                return;
            }

            TextFormatFlags alignment = column.TextAlign switch {
                HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
                HorizontalAlignment.Right => TextFormatFlags.Right,
                _ => TextFormatFlags.Left,
            };
            TextRenderer.DrawText(graphics, column.Text, _font, bounds, _text,
                alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private void DrawTrailingBackground(nint deviceContext) {
            if (!GetClientRect(_header, out NativeRectangle client)) {
                return;
            }

            foreach (ColumnHeader column in _list.Columns) {
                if (column.DisplayIndex == _list.Columns.Count - 1 &&
                    SendMessage(_header, HdmGetItemRect, column.Index, out NativeRectangle lastColumn) != 0) {
                    client.Left = Math.Max(client.Left, lastColumn.Right);
                    break;
                }
            }
            if (client.Right <= client.Left) {
                return;
            }

            using Graphics graphics = Graphics.FromHdc(deviceContext);
            DrawBackground(graphics, client.ToRectangle(), _background, false);
        }

        private static void DrawBackground(Graphics graphics, Rectangle bounds, Color color, bool divider) {
            if (bounds.Width <= 0 || bounds.Height <= 0) {
                return;
            }

            using SolidBrush fill = new(color);
            graphics.FillRectangle(fill, bounds);
            using SolidBrush edge = new(ControlPaint.Dark(color));
            graphics.FillRectangle(edge, bounds.Left, bounds.Bottom - 1, bounds.Width, 1);
            if (divider) {
                graphics.FillRectangle(edge, bounds.Right - 1, bounds.Top, 1, bounds.Height);
            }
        }

        // Some common-control implementations omit CDIS_HOT from their draw notifications.
        // Use the header's hit test while leaving its pointer and resize handling intact.
        private sealed class HeaderPointer : NativeWindow {
            private const int WmMouseMove = 0x0200;
            private const int WmMouseLeave = 0x02a3;
            private const int HdmHitTest = 0x1206;
            private const uint HitHeader = 0x0002;
            private const uint TrackLeave = 0x0002;
            private bool _tracking;

            internal int HotColumn { get; private set; } = -1;

            internal void Attach(nint header) {
                _tracking = false;
                HotColumn = -1;
                AssignHandle(header);
            }

            protected override void WndProc(ref Message message) {
                int previousColumn = HotColumn;
                if (message.Msg == WmMouseMove) {
                    if (!_tracking) {
                        MouseTracking tracking = new() {
                            Size = (uint)Marshal.SizeOf<MouseTracking>(),
                            Window = Handle,
                            Flags = TrackLeave,
                        };
                        _tracking = TrackMouseEvent(ref tracking);
                    }
                    HeaderHitTest hit = new() {
                        X = unchecked((short)(int)message.LParam),
                        Y = unchecked((short)((int)message.LParam >> 16)),
                    };
                    SendMessage(Handle, HdmHitTest, 0, ref hit);
                    HotColumn = (hit.Flags & HitHeader) != 0 ? hit.Column : -1;
                }
                else if (message.Msg == WmMouseLeave) {
                    _tracking = false;
                    HotColumn = -1;
                }

                base.WndProc(ref message);

                if (previousColumn != HotColumn && Handle != 0) {
                    InvalidateRect(Handle, 0, false);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Notification {
            public nint Window;
            public nuint Id;
            public int Code;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CustomDraw {
            public Notification Header;
            public uint Stage;
            public nint DeviceContext;
            public NativeRectangle Bounds;
            public nuint Item;
            public uint State;
            public nint ItemData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPosition {
            public nint Window;
            public nint InsertAfter;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeaderHitTest {
            public int X;
            public int Y;
            public uint Flags;
            public int Column;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseTracking {
            public uint Size;
            public uint Flags;
            public nint Window;
            public uint HoverTime;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint window, int message, nint wParam, out NativeRectangle bounds);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint window, int message, nint wParam, ref WindowPosition position);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint window, int message, nint wParam, ref HeaderHitTest hit);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TrackMouseEvent(ref MouseTracking tracking);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(nint window, out NativeRectangle bounds);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(nint window, out NativeRectangle bounds);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(nint window, nint bounds, [MarshalAs(UnmanagedType.Bool)] bool erase);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(nint handle);
    }
}
