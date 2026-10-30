namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private sealed class DetailsScrollProbe : ListView {
        internal void ClickHeader(Point location) {
            OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0));
            }
        }

    private static void PaintListProbe(ListView list) {
        using Bitmap image = new Bitmap(list.Width, list.Height);
        list.DrawToBitmap(image, list.ClientRectangle);
        }

    private void CheckDetailsHorizontalScrolling() {
        using DetailsScrollProbe list = new DetailsScrollProbe();
        list.View = View.Details;
        list.Size = new Size(340, 140);
        list.Columns.Add(new ColumnHeader { Text = "Name", Width = 310 });
        list.Columns.Add(new ColumnHeader { Text = "Type", Width = 120 });
        list.Columns.Add(new ColumnHeader { Text = "Bytes", Width = 180 });
        list.Columns.Add(new ColumnHeader { Text = "Created", Width = 160 });
        for (int index = 0; index < 20; index++) {
            ListViewItem item = new ListViewItem("File " + index);
            item.SubItems.Add("TXT");
            item.SubItems.Add("1,024");
            item.SubItems.Add("2026-10-24");
            list.Items.Add(item);
            }
        PaintListProbe(list);
        HorizontalScrollBar? horizontal = list.GetChildAtPoint(new Point(list.Width / 2, list.Height - 8)) as HorizontalScrollBar;
        Require(horizontal is not null && horizontal.Visible,
            "Details columns wider than the viewport require a horizontal scrollbar.");
        VerticalScrollBar? vertical = list.GetChildAtPoint(new Point(list.Width - 8, list.Height / 2)) as VerticalScrollBar;
        Require(vertical is not null && vertical.Visible && !horizontal!.Bounds.IntersectsWith(vertical.Bounds),
            "Both scrollbars must remain reachable without overlapping.");
        int originalLeft = list.Items[0].SubItems[3].Bounds.Left;
        horizontal!.Value = Math.Max(0, horizontal.Maximum - horizontal.LargeChange + 1);
        PaintListProbe(list);
        Rectangle lastCell = list.Items[0].SubItems[3].Bounds;
        Require(lastCell.Left < originalLeft && lastCell.Right <= vertical!.Left + 1,
            "Horizontal scrolling did not reveal the final metadata cell.");
        Point cellPoint = new Point(lastCell.Left + 5, lastCell.Top + lastCell.Height / 2);
        Require(ReferenceEquals(list.HitTest(cellPoint.X, cellPoint.Y).SubItem, list.Items[0].SubItems[3]),
            "Cell hit testing did not follow the horizontal scroll position.");
        int clickedColumn = -1;
        list.ColumnClick += (_, arguments) => clickedColumn = arguments.Column;
        list.ClickHeader(new Point(lastCell.Left + 5, 10));
        Require(clickedColumn == 3, "Scrolled headers must still target the correct sort column.");
        list.Width = 1100;
        PaintListProbe(list);
        Require(!horizontal.Visible && horizontal.Value == 0,
            "Growing the viewport must hide the horizontal bar and reset its offset.");
        list.Width = 340;
        PaintListProbe(list);
        Require(horizontal.Visible, "Shrinking the viewport must restore horizontal scrolling.");
        list.Scrollable = false;
        PaintListProbe(list);
        Require(!horizontal.Visible && !vertical!.Visible, "Scrollable=false must hide both bars.");
        list.Scrollable = true;
        list.Columns[3].DisplayIndex = 0;
        PaintListProbe(list);
        Rectangle reordered = list.Items[0].SubItems[3].Bounds;
        Point reorderedPoint = new Point(reordered.Left + 5, reordered.Top + reordered.Height / 2);
        Require(ReferenceEquals(list.HitTest(reorderedPoint.X, reorderedPoint.Y).SubItem, list.Items[0].SubItems[3]),
            "Reordering columns must preserve cell hit testing.");
        list.RightToLeft = RightToLeft.Yes;
        list.RightToLeftLayout = true;
        PaintListProbe(list);
        horizontal.Value = Math.Max(0, horizontal.Maximum - horizontal.LargeChange + 1);
        PaintListProbe(list);
        Rectangle mirrored = list.Items[0].SubItems[2].Bounds;
        Point mirroredPoint = new Point(mirrored.Left + 5, mirrored.Top + mirrored.Height / 2);
        Require(ReferenceEquals(list.HitTest(mirroredPoint.X, mirroredPoint.Y).SubItem, list.Items[0].SubItems[2]),
            "Mirrored scrolling must preserve cell hit testing.");
        list.ClickHeader(new Point(mirrored.Left + 5, 10));
        Require(clickedColumn == 2, "Mirrored scrolled headers must keep their column identity.");
        list.RightToLeftLayout = false;
        list.RightToLeft = RightToLeft.No;
        list.Items.Clear();
        PaintListProbe(list);
        Require(horizontal.Visible && !vertical!.Visible,
            "An empty Details list must still expose overflowing headers.");
        foreach (ColumnHeader column in list.Columns) {
            column.Width = 40;
            }
        PaintListProbe(list);
        Require(!horizontal.Visible && horizontal.Value == 0,
            "Reducing the columns to fit must clear horizontal scrolling.");
        }
    }
