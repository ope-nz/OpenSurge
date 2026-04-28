using System.Windows.Forms;
using System.Drawing;

namespace OpenSurge
{
    partial class MainForm
    {
        void InitializeComponent()
        {
            this.menuStrip      = new MenuStrip();
            this.miFile         = new ToolStripMenuItem("&File");
            this.miOpenSession  = new ToolStripMenuItem("&Open Session...");
            this.miImportHar    = new ToolStripMenuItem("Import from &HAR...");
            this.miSaveSession  = new ToolStripMenuItem("&Save Session As...");
            this.miExit         = new ToolStripMenuItem("E&xit");

            this.grpSession      = new GroupBox();
            this.lblSessionFile  = new Label();
            this.txtSessionFile  = new TextBox();
            this.lblBaseUrl      = new Label();
            this.txtBaseUrl      = new TextBox();
            this.lblRequests     = new Label();
            this.lstRequests     = new ListBox();
            this.btnAddRequest   = new Button();
            this.btnRemoveRequest= new Button();

            this.grpConfig     = new GroupBox();
            this.lblThreads    = new Label();
            this.nudThreads    = new NumericUpDown();
            this.lblDuration   = new Label();
            this.nudDuration   = new NumericUpDown();
            this.lblWarmup     = new Label();
            this.nudWarmup     = new NumericUpDown();
            this.chkRamp       = new CheckBox();
            this.chkShuffle    = new CheckBox();
            this.lblToken      = new Label();
            this.txtToken      = new TextBox();

            this.grpRun        = new GroupBox();
            this.btnRun        = new Button();
            this.lblElapsed    = new Label();
            this.lblLiveStats  = new Label();
            this.lblRunStatus  = new Label();

            this.grpResults    = new GroupBox();
            this.btnOpenReport = new Button();
            this.lblResultPath = new Label();

            this.lblStatus     = new Label();

            // --- MenuStrip ---
            this.miOpenSession.ShortcutKeys  = Keys.Control | Keys.O;
            this.miOpenSession.Click        += MenuOpenSession_Click;
            this.miImportHar.ShortcutKeys    = Keys.Control | Keys.I;
            this.miImportHar.Click          += MenuImportHar_Click;
            this.miSaveSession.ShortcutKeys  = Keys.Control | Keys.S;
            this.miSaveSession.Click        += MenuSaveSession_Click;
            this.miSaveSession.Enabled       = false;
            this.miExit.Click               += (s, e) => Close();

            this.miFile.DropDownItems.Add(miOpenSession);
            this.miFile.DropDownItems.Add(miImportHar);
            this.miFile.DropDownItems.Add(miSaveSession);
            this.miFile.DropDownItems.Add(new ToolStripSeparator());
            this.miFile.DropDownItems.Add(miExit);

            this.menuStrip.Items.Add(miFile);
            this.MainMenuStrip = this.menuStrip;

            // --- grpSession (shifted down 24px for menu) ---
            this.grpSession.Text     = "Session";
            this.grpSession.Location = new Point(8, 32);
            this.grpSession.Size     = new Size(784, 195);

            this.lblSessionFile.Text      = "File:";
            this.lblSessionFile.Location  = new Point(8, 24);
            this.lblSessionFile.Size      = new Size(70, 20);
            this.lblSessionFile.TextAlign = ContentAlignment.MiddleRight;

            this.txtSessionFile.Location  = new Point(82, 22);
            this.txtSessionFile.Size      = new Size(692, 23);
            this.txtSessionFile.ReadOnly  = true;
            this.txtSessionFile.BackColor = System.Drawing.Color.LightYellow;

            this.lblBaseUrl.Text      = "Base URL:";
            this.lblBaseUrl.Location  = new Point(8, 54);
            this.lblBaseUrl.Size      = new Size(70, 20);
            this.lblBaseUrl.TextAlign = ContentAlignment.MiddleRight;

            this.txtBaseUrl.Location     = new Point(82, 52);
            this.txtBaseUrl.Size         = new Size(692, 23);
            this.txtBaseUrl.TextChanged += (s, e) => RefreshRequestList();

            this.lblRequests.Text      = "Requests:";
            this.lblRequests.Location  = new Point(8, 84);
            this.lblRequests.Size      = new Size(70, 20);
            this.lblRequests.TextAlign = ContentAlignment.MiddleRight;

            this.lstRequests.Location             = new Point(82, 82);
            this.lstRequests.Size                 = new Size(692, 72);
            this.lstRequests.SelectionMode        = SelectionMode.One;
            this.lstRequests.ScrollAlwaysVisible  = true;
            this.lstRequests.Font                 = new Font("Consolas", 8.5f);
            this.lstRequests.DoubleClick         += LstRequests_DoubleClick;
            this.lstRequests.SelectedIndexChanged+= LstRequests_SelectedIndexChanged;

            this.btnAddRequest.Text     = "Add Request...";
            this.btnAddRequest.Location = new Point(82, 158);
            this.btnAddRequest.Size     = new Size(110, 26);
            this.btnAddRequest.Click   += BtnAddRequest_Click;

            this.btnRemoveRequest.Text     = "Remove";
            this.btnRemoveRequest.Location = new Point(196, 158);
            this.btnRemoveRequest.Size     = new Size(76, 26);
            this.btnRemoveRequest.Enabled  = false;
            this.btnRemoveRequest.Click   += BtnRemoveRequest_Click;

            this.grpSession.Controls.AddRange(new Control[] {
                lblSessionFile, txtSessionFile,
                lblBaseUrl, txtBaseUrl,
                lblRequests, lstRequests,
                btnAddRequest, btnRemoveRequest
            });

            // --- grpConfig ---
            this.grpConfig.Text     = "Test Configuration";
            this.grpConfig.Location = new Point(8, 234);
            this.grpConfig.Size     = new Size(784, 115);

            this.lblThreads.Text      = "Threads:";
            this.lblThreads.Location  = new Point(8, 24);
            this.lblThreads.Size      = new Size(65, 20);
            this.lblThreads.TextAlign = ContentAlignment.MiddleRight;

            this.nudThreads.Location  = new Point(77, 22);
            this.nudThreads.Size      = new Size(60, 23);
            this.nudThreads.Minimum   = 1;
            this.nudThreads.Maximum   = 500;
            this.nudThreads.Value     = 5;

            this.lblDuration.Text      = "Duration (s):";
            this.lblDuration.Location  = new Point(148, 24);
            this.lblDuration.Size      = new Size(85, 20);
            this.lblDuration.TextAlign = ContentAlignment.MiddleRight;

            this.nudDuration.Location  = new Point(237, 22);
            this.nudDuration.Size      = new Size(70, 23);
            this.nudDuration.Minimum   = 1;
            this.nudDuration.Maximum   = 3600;
            this.nudDuration.Value     = 60;

            this.lblWarmup.Text      = "Warmup (s):";
            this.lblWarmup.Location  = new Point(318, 24);
            this.lblWarmup.Size      = new Size(80, 20);
            this.lblWarmup.TextAlign = ContentAlignment.MiddleRight;

            this.nudWarmup.Location  = new Point(402, 22);
            this.nudWarmup.Size      = new Size(60, 23);
            this.nudWarmup.Minimum   = 0;
            this.nudWarmup.Maximum   = 600;
            this.nudWarmup.Value     = 0;

            this.chkRamp.Text     = "Ramp Threads";
            this.chkRamp.Location = new Point(476, 22);
            this.chkRamp.Size     = new Size(130, 22);

            this.chkShuffle.Text     = "Shuffle Requests";
            this.chkShuffle.Location = new Point(8, 54);
            this.chkShuffle.Size     = new Size(140, 22);

            this.lblToken.Text      = "Bearer Token:";
            this.lblToken.Location  = new Point(8, 84);
            this.lblToken.Size      = new Size(85, 20);
            this.lblToken.TextAlign = ContentAlignment.MiddleRight;

            this.txtToken.Location  = new Point(97, 82);
            this.txtToken.Size      = new Size(677, 23);

            this.grpConfig.Controls.AddRange(new Control[] {
                lblThreads, nudThreads, lblDuration, nudDuration,
                lblWarmup, nudWarmup, chkRamp, chkShuffle,
                lblToken, txtToken
            });

            // --- grpRun ---
            this.grpRun.Text     = "Run";
            this.grpRun.Location = new Point(8, 356);
            this.grpRun.Size     = new Size(784, 108);

            this.btnRun.Text      = "Run Test";
            this.btnRun.Location  = new Point(8, 20);
            this.btnRun.Size      = new Size(108, 38);
            this.btnRun.BackColor = Color.FromArgb(46, 204, 113);
            this.btnRun.ForeColor = Color.White;
            this.btnRun.FlatStyle = FlatStyle.Flat;
            this.btnRun.Font      = new Font(this.btnRun.Font, FontStyle.Bold);
            this.btnRun.Enabled   = false;
            this.btnRun.Click    += BtnRun_Click;

            this.lblElapsed.Text      = "Elapsed: --";
            this.lblElapsed.Location  = new Point(126, 26);
            this.lblElapsed.Size      = new Size(650, 20);
            this.lblElapsed.ForeColor = Color.DimGray;

            this.lblLiveStats.Text      = "Requests: 0 | Errors: 0 | Req/s: 0.0";
            this.lblLiveStats.Location  = new Point(126, 50);
            this.lblLiveStats.Size      = new Size(650, 20);
            this.lblLiveStats.ForeColor = Color.DimGray;

            this.lblRunStatus.Text      = "";
            this.lblRunStatus.Location  = new Point(8, 78);
            this.lblRunStatus.Size      = new Size(768, 22);
            this.lblRunStatus.ForeColor = Color.DimGray;

            this.grpRun.Controls.AddRange(new Control[] {
                btnRun, lblElapsed, lblLiveStats, lblRunStatus
            });

            // --- grpResults ---
            this.grpResults.Text     = "Results";
            this.grpResults.Location = new Point(8, 471);
            this.grpResults.Size     = new Size(784, 60);

            this.btnOpenReport.Text     = "Open Report";
            this.btnOpenReport.Location = new Point(8, 22);
            this.btnOpenReport.Size     = new Size(108, 27);
            this.btnOpenReport.Enabled  = false;
            this.btnOpenReport.Click   += BtnOpenReport_Click;

            this.lblResultPath.Text         = "";
            this.lblResultPath.Location     = new Point(124, 26);
            this.lblResultPath.Size         = new Size(652, 20);
            this.lblResultPath.ForeColor    = Color.DimGray;
            this.lblResultPath.AutoEllipsis = true;

            this.grpResults.Controls.AddRange(new Control[] { btnOpenReport, lblResultPath });

            // --- Status bar ---
            this.lblStatus.Text         = "Ready";
            this.lblStatus.Location     = new Point(8, 540);
            this.lblStatus.Size         = new Size(784, 20);
            this.lblStatus.BorderStyle  = BorderStyle.Fixed3D;
            this.lblStatus.ForeColor    = Color.DimGray;
            this.lblStatus.AutoEllipsis = true;

            // --- Form ---
            this.Text            = "OpenSurge";
            this.ClientSize      = new Size(800, 569);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox     = false;
            this.StartPosition   = FormStartPosition.CenterScreen;

            this.Controls.AddRange(new Control[] {
                menuStrip,
                grpSession, grpConfig, grpRun, grpResults, lblStatus
            });

            ((System.ComponentModel.ISupportInitialize)nudThreads).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudWarmup).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudThreads).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudWarmup).EndInit();
        }

        MenuStrip menuStrip;
        ToolStripMenuItem miFile, miOpenSession, miImportHar, miSaveSession, miExit;
        GroupBox grpSession, grpConfig, grpRun, grpResults;
        Label    lblSessionFile, lblBaseUrl, lblRequests;
        TextBox  txtSessionFile, txtBaseUrl, txtToken;
        ListBox  lstRequests;
        Button   btnAddRequest, btnRemoveRequest;
        Label    lblThreads, lblDuration, lblWarmup, lblToken;
        NumericUpDown nudThreads, nudDuration, nudWarmup;
        CheckBox chkRamp, chkShuffle;
        Button   btnRun, btnOpenReport;
        Label    lblElapsed, lblLiveStats, lblRunStatus;
        Label    lblResultPath, lblStatus;
    }
}
