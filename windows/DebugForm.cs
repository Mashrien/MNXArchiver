using Mash.GUI.MNXArchiver.Diagnostics;

namespace Mash.GUI.MNXArchiver;

public partial class DebugForm : MnxDialog {
    private long _cursor;
    private bool _shuttingDown;

    public DebugForm() {
        InitializeComponent();
        logTimer.Tick += (_, _) => UpdateLog();
        FormClosing += (_, args) => {
            if (!_shuttingDown) {
                args.Cancel = true;
                logTimer.Stop();
                Hide();
                }
            };
        Resize += (_, _) => ResizeMessageColumn();
        }

    protected override void OnLoad(EventArgs args) {
        base.OnLoad(args);
        // The debug surface deliberately keeps PackRat's black, colored-text appearance.
        logList.BackColor = Color.Black;
        logList.ForeColor = Color.Gray;
        }

    protected override void OnShown(EventArgs args) {
        base.OnShown(args);
        UpdateLog();
        ResizeMessageColumn();
        logTimer.Start();
        }

    internal void ShowLog(Form owner) {
        Show(owner);
        UpdateLog();
        logTimer.Start();
        Activate();
        }

    private void UpdateLog() {
        DebugLogBatch batch = DebugLog.Buffer.ReadBatch(_cursor);
        _cursor = batch.Cursor;
        if (batch.Records.Count == 0) {
            return;
            }
        logList.BeginUpdate();
        try {
            foreach (DebugLogRecord record in batch.Records) {
                ListViewItem row = new ListViewItem(new string[] { record.Type.ToString(), record.Caller, record.Message }) {
                    ForeColor = DebugLog.ColorFor(record.Type),
                    UseItemStyleForSubItems = true,
                    ToolTipText = record.Message,
                    };
                logList.Items.Add(row);
                }
            while (logList.Items.Count > DebugLog.Buffer.Capacity) {
                logList.Items.RemoveAt(0);
                }
            logList.EnsureVisible(logList.Items.Count - 1);
            }
        finally {
            logList.EndUpdate();
            }
        }

    private void ResizeMessageColumn() {
        logList.Columns[2].Width = Math.Max(300, logList.ClientSize.Width - 420);
        }

    internal void Shutdown() {
        _shuttingDown = true;
        logTimer.Stop();
        Dispose();
        }
    }
