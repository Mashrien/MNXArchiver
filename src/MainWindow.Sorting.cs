using Gtk;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    private void ConfigureColumnSorting() {
        // Display strings stay separate from numeric values. Sorting never reads
        // payloads or rebuilds the archive browser; GTK keeps row selection by ID.
        int[] sortColumns = { 1, 3, 8, 9, 10, 12 };
        foreach (int column in sortColumns) {
            int sortColumn = column;
            _files.SetSortFunc(sortColumn, (model, left, right) => CompareRows(model, left, right, sortColumn));
            }
        _files.SortColumnChanged += (_, _) => CancelHover(closePopups: true);
        _files.SetSortColumnId(1, SortType.Ascending);
        }

    private int CompareRows(ITreeModel model, TreeIter left, TreeIter right, int column) {
        bool leftDirectory = (bool)model.GetValue(left, 7);
        bool rightDirectory = (bool)model.GetValue(right, 7);
        if (leftDirectory != rightDirectory) {
            // GTK reverses the comparison for descending order; compensate only
            // for directory grouping so folders stay at the top in either order.
            _files.GetSortColumnId(out int currentColumn, out SortType direction);
            int directoryOrder = leftDirectory ? -1 : 1;
            return direction == SortType.Descending ? -directoryOrder : directoryOrder;
            }

        int comparison;
        switch (column) {
            case 8:
                comparison = ((ulong)model.GetValue(left, 8)).CompareTo((ulong)model.GetValue(right, 8));
                break;
            case 9:
                comparison = ((long)model.GetValue(left, 9)).CompareTo((long)model.GetValue(right, 9));
                break;
            case 12:
                comparison = ((uint)model.GetValue(left, 12)).CompareTo((uint)model.GetValue(right, 12));
                break;
            default:
                comparison = StringComparer.OrdinalIgnoreCase.Compare((string)model.GetValue(left, column), (string)model.GetValue(right, column));
                break;
            }
        if (comparison != 0) {
            return comparison;
            }
        string leftName = (string)model.GetValue(left, 1);
        string rightName = (string)model.GetValue(right, 1);
        comparison = StringComparer.OrdinalIgnoreCase.Compare(leftName, rightName);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(leftName, rightName);
        }
    }
