using Mash.UI.MNXFile.GUI.Icons;
using Mash.UI.MNXFile.GUI.Plugins;
using Gtk;
using VirtualFile = Mash.IO.Format.MNX.VFile.VFile;
using ArchiveFile = Mash.IO.Format.MNX.MNX;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    private readonly MenuItem _previewEntry = new MenuItem("Preview");
    private readonly MenuItem _deleteEntry = new MenuItem("Delete…");
    private readonly MenuItem _infoEntry = new MenuItem("Properties…");
    private readonly MenuItem _replaceEntry = new MenuItem("Replace File…") { NoShowAll = true };

    private void AddEntryActions() {
        OwnedIcons.SetMenuImage(_previewEntry, "search");
        OwnedIcons.SetMenuImage(_deleteEntry, "delete");
        OwnedIcons.SetMenuImage(_infoEntry, "info");
        _entryMenu.Append(_previewEntry);
        _entryMenu.Append(_replaceEntry);
        _entryMenu.Append(_deleteEntry);
        _entryMenu.Append(new SeparatorMenuItem());
        _entryMenu.Append(_infoEntry);
        _previewEntry.Activated += (_, _) => {
            if (_contextSelection.Length == 1) {
                PreviewEntry(_contextSelection[0]);
                }
            };
        _deleteEntry.Activated += (_, _) => DeleteEntries(_contextSelection);
        _replaceEntry.Activated += (_, _) => ReplacePluginFile(_contextSelection);
        _infoEntry.Activated += (_, _) => EntryProperties(_contextSelection);
        }
    private void EntryProperties(Guid[] ids) {
        if (_operationActive || IsLayoutPreview || _browser is null || ids.Length == 0) {
            return;
            }
        if (_pluginArchive is { } plugin) {
            string format = $"{plugin.Format.Name} {plugin.FormatVersion}";
            if (ids.Length == 1) {
                ArchiveBrowser.Entry entry = _browser.Get(ids[0]);
                string kind = entry.IsDirectory ? "Directory" : "File";
                string size = entry.IsDirectory ? "" : $"\nSize: {entry.Bytes:N0} bytes";
                Message("Entry properties", $"{_browser.PathOf(entry.Id)}\n\nType: {kind}{size}\nFormat: {format}");
                }
            else {
                Message("Selection properties", $"{ids.Length:N0} entries selected\nIndexed file bytes: {ids.Sum(id => (decimal)_browser.Get(id).Bytes):N0}\nFormat: {format}");
                }
            return;
            }
        ArchiveFile archive = _archive!;
        if (ids.Length > 1) {
            ulong bytes = 0;
            foreach (Guid id in ids) {
                bytes += _browser.Get(id).Bytes;
                }
            Message("Selection properties", $"{ids.Length:N0} entries selected\nIndexed file bytes: {bytes:N0}\nCompression: {archive.Compression}\nEncrypted: {archive.IsEncrypted}");
            return;
            }
        VirtualFile? file = archive.GetFile(ids[0]);
        if (file is null) {
            return;
            }
        Message("Entry properties", $"{_browser.PathOf(file.FileID)}\n\nType: {(file.IsDirectory ? "Directory" : "File")}\nSize: {file.SizeOriginal:N0} bytes\nCreated: {file.CreationDateUtc.ToLocalTime():G}\nModified: {file.ModificationDateUtc.ToLocalTime():G}\nAccessed: {file.AccessDateUtc.ToLocalTime():G}\nAttributes: {file.Attributes}\nID: {file.FileID}\nChunks: {file.ChunkCount:N0}\nPhysical extents: {file.ExtentCount:N0}\nCompression: {archive.Compression}\nEncrypted: {archive.IsEncrypted}");
        }
    private void DeleteEntries(Guid[] ids) {
        if (_operationActive || IsLayoutPreview || _browser is null || ids.Length == 0 || _pluginArchive?.Supports(PluginActions.Delete) == false) {
            return;
            }
        using MessageDialog question = OwnedIcons.Dialog(_window, DialogFlags.Modal, MessageType.Warning, ButtonsType.YesNo, false, "{0}", $"Delete {ids.Length:N0} selected archive entries?\n\nFolders include all their descendants. This cannot be undone; source files on disk are unaffected.");
        question.Title = "Delete from archive";
        ResponseType answer = (ResponseType)question.Run();
        question.Hide();
        if (answer != ResponseType.Yes) {
            return;
            }
        Guid location = _current;
        foreach (Guid ancestor in _browser.Ancestors(_current)) {
            if (ids.Contains(ancestor)) {
                location = _browser.Parent(ancestor);
                break;
                }
            }
        if (_pluginArchive is { } plugin) {
            DeletePluginEntries(plugin, ids, _browser.PathOf(location));
            return;
            }
        ArchiveFile archive = _archive!;
        try {
            RunTransfer("Deleting archive entries", () => {
                foreach (Guid id in ids) {
                    if (archive.GetFile(id) is not null && archive.DeleteFileFromArchive(id) != ArchiveFile.ReturnCode.Success) {
                        throw new IOException("Could not delete archive entry " + id);
                        }
                    }
                return true;
                });
            _status.Text = "Selected archive entries deleted. Compact the archive to reclaim free space.";
            }
        catch (Exception error) {
            Message("Could not delete all entries", error.Message, MessageType.Error);
            }
        finally {
            RefreshArchive(location);
            }
        }
    }
