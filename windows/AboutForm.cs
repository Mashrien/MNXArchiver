using Mash.GUI.MNXArchiver.About;
using Mash.GUI.MNXArchiver.Plugins;
using System.Diagnostics;

namespace Mash.GUI.MNXArchiver;

public partial class AboutForm : MnxDialog {
    private PluginDirectoryWatch? _watch;
    private IReadOnlyDictionary<string, string>? _restartBaseline;
    private Func<IReadOnlyDictionary<string, string>, bool>? _restart;
    private readonly Dictionary<ListViewItem, string> _pluginItems = new Dictionary<ListViewItem, string>();

    public AboutForm() {
        InitializeComponent();
        }

    internal AboutForm(Func<IReadOnlyDictionary<string, string>, bool> restart) : this() {
        _restart = restart;
        AboutModel model = new AboutModel(PluginCatalog.Installed);
        versionLabel.Text = "GUI v" + model.GuiVersion + "     MNX v" + model.FormatVersion;
        directoryLabel.Text = model.DirectoryPath;
        formatLabel.Text = "Format Providers (" + model.Formats.Count + ")";
        functionLabel.Text = "Function Providers (" + model.Functions.Count + ")";
        AddRows(formatList, model.Formats);
        AddRows(functionList, model.Functions);
        if (model.Issues.Count > 0) {
            noteLabel.Text = string.Join("; ", model.Issues.Take(3)) + "\n" + noteLabel.Text;
            }
        sourceLink.LinkClicked += (_, _) => {
            try {
                using Process? browser = Process.Start(new ProcessStartInfo(AboutModel.SourceUrl) { UseShellExecute = true });
                }
            catch (Exception error) {
                MessageBox.Show(this, error.Message, "Could not open source repository");
                }
            };
        pluginsButton.Click += async (_, _) => await OpenPlugins();
        reloadButton.Click += (_, _) => {
            if (_restartBaseline is not null && _restart?.Invoke(_restartBaseline) == true) {
                Close();
                }
            };
        Shown += async (_, _) => await HighlightUpdatedRows(model.DirectoryPath);
        FormClosed += (_, _) => _watch?.Dispose();
        }

    private void AddRows(ListView list, IReadOnlyList<AboutProviderRow> rows) {
        foreach (AboutProviderRow row in rows) {
            ListViewItem item = new ListViewItem(new string[] { row.Type, row.Name, row.Formats, row.Library });
            list.Items.Add(item);
            if (row.LibraryFile.Length > 0) {
                _pluginItems.Add(item, row.LibraryFile);
                }
            }
        }

    private async Task HighlightUpdatedRows(string directory) {
        if (AboutRestart.Restored is not AboutRestart restored) {
            return;
            }
        try {
            Dictionary<string, string> current = await Task.Run(() => PluginSnapshot.Capture(directory));
            if (IsDisposed) {
                return;
                }
            foreach (KeyValuePair<ListViewItem, string> item in _pluginItems) {
                if (PluginSnapshot.IsUpdated(item.Value, restored.PreviousPlugins, current)) {
                    item.Key.ForeColor = Color.FromArgb(50, 170, 50);
                    }
                }
            }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            noteLabel.Text = error.Message;
            }
        }

    private async Task OpenPlugins() {
        pluginsButton.Enabled = false;
        try {
            if (_watch?.IsStopped == true) {
                _watch = null;
                }
            _watch ??= new PluginDirectoryWatch(PluginCatalog.Installed.DirectoryPath,
                action => {
                    if (!IsDisposed && IsHandleCreated) {
                        BeginInvoke(action);
                        }
                    }, () => reloadButton.Visible = true);
            await _watch.StartAsync();
            _restartBaseline ??= _watch.Baseline;
            if (!IsDisposed) {
                PluginCatalog.Installed.OpenDirectory();
                }
            }
        catch (OperationCanceledException) {
            }
        catch (Exception error) {
            if (!IsDisposed) {
                MessageBox.Show(this, error.Message, "Could not open plugins folder");
                }
            }
        finally {
            if (!IsDisposed) {
                pluginsButton.Enabled = true;
                }
            }
        }
    }
