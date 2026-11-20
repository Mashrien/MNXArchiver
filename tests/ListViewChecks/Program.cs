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

using Majorsilence.Forms.Drawing.Font headerFont = new Majorsilence.Forms.Drawing.Font("DejaVu Sans Mono", 20);
int rowHeight = view.Items[0].Bounds.Height;
int headerHeight = view.Items[0].Bounds.Top;
view.SetHeaderAppearance(Color.Navy, Color.Purple, headerFont, Color.Lime);
view.DrawToBitmap(screenshot, view.ClientRectangle);
int styledHeight = view.Items[0].Bounds.Top;
Check(styledHeight > headerHeight && view.Items[0].Bounds.Height == rowHeight,
    "The header font should grow the header without changing the height of file rows.");
Check(screenshot.GetPixel(130, 8).ToArgb() == Color.Navy.ToArgb() &&
    screenshot.GetPixel(300, 8).ToArgb() == Color.Navy.ToArgb(),
    "The header background should cover its columns and the space beyond them.");
Check(ColorPixels(screenshot, new Rectangle(0, 0, 130, styledHeight), Color.Lime) > 10,
    "The configured header text color was missing.");
view.MovePointer(new Point(30, 8));
view.DrawToBitmap(screenshot, view.ClientRectangle);
Check(screenshot.GetPixel(130, 8).ToArgb() == Color.Purple.ToArgb() &&
    screenshot.GetPixel(250, 8).ToArgb() == Color.Navy.ToArgb(),
    "Only the hovered header should use the highlight color.");
view.MovePointer(Centre(0));
view.DrawToBitmap(screenshot, view.ClientRectangle);
Check(screenshot.GetPixel(130, 8).ToArgb() == Color.Navy.ToArgb(),
    "The hover highlight should clear when the pointer enters the file rows.");
int clickedColumn = -1;
view.ColumnClick += (_, arguments) => clickedColumn = arguments.Column;
view.ClickAt(new Point(210, styledHeight - 3));
Check(clickedColumn == 1, "Header click detection should follow the enlarged header.");
view.SetHeaderAppearance(Color.DarkRed, Color.Olive, headerFont, Color.White);
view.DrawToBitmap(screenshot, view.ClientRectangle);
Check(screenshot.GetPixel(130, 8).ToArgb() == Color.DarkRed.ToArgb(),
    "Reapplying header appearance should update the background.");

using Majorsilence.Forms.Drawing.Font browserFont = new Majorsilence.Forms.Drawing.Font("DejaVu Sans Mono", 18);
view.ClearSelection();
int previousRowHeight = view.Items[0].Bounds.Height;
view.Font = browserFont;
view.ForeColor = Color.Yellow;
view.BackColor = Color.DarkGreen;
view.Items[0].ForeColor = Color.Magenta;
view.DrawToBitmap(screenshot, view.ClientRectangle);
Check(view.Items[0].Bounds.Height > previousRowHeight, "The configured file list font should grow file rows.");
Check(ColorPixels(screenshot, view.Items[0].Bounds, Color.Magenta) > 10,
    "Configuring the list text color should preserve per-file colors.");
Check(ColorPixels(screenshot, view.Items[1].Bounds, Color.Yellow) > 10,
    "The configured file list text color was missing.");
Check(screenshot.GetPixel(300, view.Items[1].Bounds.Top + 3).ToArgb() == Color.DarkGreen.ToArgb(),
    "The configured file list background was missing.");

using TreeView tree = new TreeView {
    Size = new Size(320, 120),
    BackColor = Color.MidnightBlue,
    ForeColor = Color.White,
    };
TreeNode folder = tree.Nodes.Add("Documents");
form.Controls.Add(tree);
using Bitmap treeImage = new Bitmap(tree.Width, tree.Height);
tree.DrawToBitmap(treeImage, tree.ClientRectangle);
int originalTreeRowHeight = folder.Bounds.Height;
tree.Font = browserFont;
tree.DrawToBitmap(treeImage, tree.ClientRectangle);
Check(folder.Bounds.Height > originalTreeRowHeight, "The configured folder tree font should grow tree rows.");
Check(ColorPixels(treeImage, folder.Bounds, Color.White) > 10, "The configured folder tree text color was missing.");
Check(treeImage.GetPixel(300, 100).ToArgb() == Color.MidnightBlue.ToArgb(), "The configured folder tree background was missing.");

foreach (string failure in failures) {
    Console.Error.WriteLine("FAIL: " + failure);
    }
if (failures.Count == 0) {
    Console.WriteLine("PASS: Details icons, selection and drag gestures, file list and tree appearance, and column headers.");
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
    return ColorPixels(screenshot, new Rectangle(row.Left, row.Top, 24, row.Height), color);
    }

int ColorPixels(Bitmap image, Rectangle bounds, Color color) {
    int count = 0;
    for (int y = bounds.Top; y < bounds.Bottom; y++) {
        for (int x = bounds.Left; x < bounds.Right; x++) {
            if (image.GetPixel(x, y).ToArgb() == color.ToArgb()) {
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

    public void MovePointer(Point point) =>
        OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
    }
