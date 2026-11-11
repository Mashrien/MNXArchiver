using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Bitmap = Majorsilence.Forms.Drawing.Bitmap;

HeadlessRenderer.Use();
using Form form = new Form { ClientSize = new Size(360, 260), FormBorderStyle = FormBorderStyle.None };
using ImageList images = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
AddImage("folder", Color.Orange);
AddImage("file", Color.Cyan);
using TestListView view = new TestListView {
    View = View.Details,
    MultiSelect = true,
    FullRowSelect = true,
    SmallImageList = images,
    Size = new Size(320, 210)
    };
view.Columns.Add(new ColumnHeader { Text = "Name", Width = 165 });
view.Columns.Add(new ColumnHeader { Text = "Type", Width = 100 });
view.Items.Add(new ListViewItem(new[] { "Documents", "Folder" }, "folder"));
view.Items.Add(new ListViewItem(new[] { "Notes.txt", "Text file" }, "file"));
view.Items.Add(new ListViewItem(new[] { "Readme.txt", "Text file" }, 1));
view.Items.Add(new ListViewItem(new[] { "Unknown", "Other" }, "missing"));
form.Controls.Add(view);
form.Show();
HeadlessRenderer.CapturePng(form);

List<string> failures = new List<string>();
using Bitmap screenshot = new Bitmap(view.Width, view.Height);
view.DrawToBitmap(screenshot, view.ClientRectangle);
Check(IconPixels(0, Color.Orange) > 40, "The keyed folder icon was absent from its Details row.");
Check(IconPixels(1, Color.Cyan) > 40, "The keyed file icon was absent from its Details row.");
Check(IconPixels(2, Color.Cyan) > 40, "The indexed file icon was absent from its Details row.");
Check(IconPixels(3, Color.Orange) == 0 && IconPixels(3, Color.Cyan) == 0,
    "A missing image key drew another row's icon.");

view.ClickAt(Centre(0));
view.ClickAt(Centre(2), Keys.Control);
ExpectSelection("Ctrl-click adds a row", 0, 2);
view.ClickAt(Centre(0));
ExpectSelection("Plain click on the first selected row", 0);

view.ClickAt(Centre(2), Keys.Shift);
ExpectSelection("Shift-click extends the range", 0, 1, 2);
view.ClickAt(Centre(3));
ExpectSelection("Plain click on an unselected row", 3);

view.ClearSelection();
view.ClickAt(Centre(3));
view.ClickAt(Centre(1), Keys.Control);
view.ClickAt(new Point(35, 12));
ExpectSelection("Column header click preserves selection", 1, 3);
view.ClickAt(new Point(35, 165));
ExpectSelection("Blank area click clears selection");

view.ClearSelection();
view.ClickAt(Centre(0));
view.ClickAt(Centre(1), Keys.Control);
Point origin = Centre(1);
bool sawDrag = false;
view.ItemDrag += (_, arguments) => {
    sawDrag = ReferenceEquals(arguments.Item, view.Items[1]);
    ExpectSelection("Drag start preserves all selected rows", 0, 1);
    };
HeadlessRenderer.MouseDown(form, origin.X, origin.Y);
HeadlessRenderer.MouseMove(form, origin.X + 70, origin.Y, MouseButtons.Left);
HeadlessRenderer.MouseUp(form, origin.X + 70, origin.Y);
Check(sawDrag, "Dragging the selected row did not raise ItemDrag.");
ExpectSelection("Drag release preserves all selected rows", 0, 1);
bool activated = false;
view.ItemActivate += (_, _) => activated = true;
HeadlessRenderer.Click(form, origin.X + 70, origin.Y);
ExpectSelection("A click after a drag selects the clicked row", 1);
Check(!activated, "The release of a drag was counted as the first click of a double-click.");

foreach (string failure in failures) {
    Console.Error.WriteLine("FAIL: " + failure);
    }
if (failures.Count == 0) {
    Console.WriteLine("PASS: Details icons, Ctrl and Shift selection, plain and blank clicks, and multi-row drag.");
    }
return failures.Count == 0 ? 0 : 1;

void AddImage(string key, Color color) {
    using Bitmap bitmap = new Bitmap(16, 16);
    for (int y = 0; y < bitmap.Height; y++) {
        for (int x = 0; x < bitmap.Width; x++) {
            bitmap.SetPixel(x, y, color);
            }
        }
    images.Images.Add(key, bitmap);
    }

Point Centre(int index) {
    Rectangle bounds = view.Items[index].Bounds;
    return new Point(bounds.Left + 35, bounds.Top + bounds.Height / 2);
    }

int IconPixels(int index, Color color) {
    Rectangle row = view.Items[index].Bounds;
    int count = 0;
    for (int y = row.Top; y < row.Bottom; y++) {
        for (int x = row.Left; x < row.Left + 24; x++) {
            if (screenshot.GetPixel(x, y).ToArgb() == color.ToArgb()) {
                count++;
                }
            }
        }
    return count;
    }

void ExpectSelection(string gesture, params int[] expected) {
    int[] selected = view.SelectedIndices.Cast<int>().ToArray();
    Check(selected.SequenceEqual(expected),
        $"{gesture} selected [{string.Join(", ", selected)}], expected [{string.Join(", ", expected)}].");
    }

void Check(bool condition, string failure) {
    if (!condition) {
        failures.Add(failure);
        }
    }

sealed class TestListView : ListView {
    public void ClickAt(Point point, Keys modifiers = Keys.None) =>
        OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, Point.Empty, keyData: modifiers));
    }
