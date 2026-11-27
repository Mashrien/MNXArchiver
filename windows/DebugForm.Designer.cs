#nullable enable
namespace Mash.GUI.MNXArchiver;

partial class DebugForm {
    private System.ComponentModel.IContainer? components;
    private ListView logList = null!;
    private System.Windows.Forms.Timer logTimer = null!;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            components?.Dispose();
            }
        base.Dispose(disposing);
        }

    private void InitializeComponent() {
        components = new System.ComponentModel.Container();
        logList = new ListView();
        logTimer = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        logList.BackColor = Color.Black;
        logList.ForeColor = Color.Gray;
        logList.BorderStyle = BorderStyle.None;
        logList.Dock = DockStyle.Fill;
        logList.View = View.Details;
        logList.HeaderStyle = ColumnHeaderStyle.None;
        logList.FullRowSelect = true;
        logList.MultiSelect = false;
        logList.ShowItemToolTips = true;
        logList.LabelWrap = false;
        logList.Font = new Font("Consolas", 9F, FontStyle.Regular);
        logList.Columns.Add("Severity", 140);
        logList.Columns.Add("Caller", 260);
        logList.Columns.Add("Message", 600);
        logList.Name = "logList";
        logTimer.Interval = 150;
        ClientSize = new Size(1020, 480);
        Controls.Add(logList);
        Text = "Debug Window";
        StartPosition = FormStartPosition.CenterParent;
        ResumeLayout(false);
        }
    }
