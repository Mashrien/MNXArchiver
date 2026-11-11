namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainForm {
    private int _minimumNameColumnWidth;
    private bool _updatingFileColumns;
    private bool _folderPaneWidthInitialized;

    private void InitializeFolderPaneWidth() {
        if (_folderPaneWidthInitialized) {
            return;
            }
        _folderPaneWidthInitialized = true;
        int usableWidth = ClientSize.Width - Padding.Horizontal;
        // MWin's Linux adapter already includes the drawn border in its padding.
        if (OperatingSystem.IsWindows() && Frame.ShowChrome) {
            usableWidth -= Math.Max(0, Frame.BorderThickness) * 2;
            }
        int preferredWidth = (int)Math.Round(usableWidth * 0.225);
        int maximumWidth = browserSplit.Width - browserSplit.SplitterWidth - browserSplit.Panel2MinSize;
        if (maximumWidth >= browserSplit.Panel1MinSize) {
            browserSplit.SplitterDistance = Math.Clamp(preferredWidth, browserSplit.Panel1MinSize, maximumWidth);
            }
        }

    private void ConfigureFileListLayout() {
        _minimumNameColumnWidth = nameColumn.Width;
        fileList.ClientSizeChanged += (_, _) => ResizeFileColumns();
        fileList.ColumnWidthChanged += (_, arguments) => {
            if (_updatingFileColumns) {
                return;
                }
            if (arguments.ColumnIndex == nameColumn.Index) {
                // Keep a width deliberately chosen by the user as the new minimum.
                _minimumNameColumnWidth = Math.Max(1, nameColumn.Width);
                }
            ResizeFileColumns();
            };
        ResizeFileColumns();
        }

    private void ResizeFileColumns() {
        if (_updatingFileColumns || fileList.ClientSize.Width <= 0) {
            return;
            }
        int metadataWidth = 0;
        foreach (ColumnHeader column in fileList.Columns) {
            if (!ReferenceEquals(column, nameColumn)) {
                metadataWidth += Math.Max(0, column.Width);
                }
            }
        // Reserve the border, inner padding and vertical scroll strip. Metadata
        // keeps its chosen widths; only Name absorbs space added to the window.
        int availableWidth = fileList.ClientSize.Width - fileList.Padding.Horizontal -
            SystemInformation.VerticalScrollBarWidth - 2;
        int nameWidth = Math.Max(_minimumNameColumnWidth, availableWidth - metadataWidth);
        if (nameColumn.Width == nameWidth) {
            return;
            }
        _updatingFileColumns = true;
        try {
            nameColumn.Width = nameWidth;
            }
        finally {
            _updatingFileColumns = false;
            }
        }
    }
