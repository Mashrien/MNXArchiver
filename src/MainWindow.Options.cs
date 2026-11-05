using Gtk;
using Mash.UI.MNXFile.GUI.Settings;

namespace Mash.UI.MNXFile.GUI;

public sealed partial class MainWindow {
    private AppSettings _settings = new AppSettings();
    private void ConfigureOptions(AppSettings? initialSettings) {
        try {
            _settings = initialSettings ?? AppSettings.Load();
            }
        catch (Exception error) {
            _status.Text = "Could not load options: " + error.Message;
            }
        ApplyWindowAppearance();
        Widget<MenuItem>("menu_options").Activated += (_, _) => {
            if (_operationActive) {
                return;
                }
            ClosePreviews();
            using OptionsDialog dialog = new OptionsDialog(_window, _settings);
            AppSettings? chosen = dialog.Run();
            if (dialog.ExitRequested) {
                RequestQuit();
                return;
                }
            if (chosen is not null) {
                _settings = chosen;
                // Keep existing drag leases alive; a new transfer uses the new directory.
                if (_dragFiles is not null) {
                    _oldDragFiles.Add(_dragFiles);
                    _dragFiles = null;
                    }
                }
            else {
                // Clear is intentionally immediate, including on Cancel.
                try {
                    _settings = AppSettings.Load();
                    }
                catch (Exception error) {
                    _status.Text = error.Message;
                    }
                }
            ApplyWindowAppearance();
            };
        }
    private void ApplyWindowAppearance() {
        GtkWindowAppearance.Configure(_settings.OpacityPercent);
        }

    private readonly List<DragFiles> _oldDragFiles = [];
    private string? DefaultPassword() {
        if (!_settings.Flag("Defaults", "Encrypt")) {
            return null;
            }
        DefaultKeyStore keys = new DefaultKeyStore(_settings);
        return UiWorker.Run(keys.Read);
        }
    }
