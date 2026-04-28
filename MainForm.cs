using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenSurge
{
    partial class MainForm : Form
    {
        const string ConfigFile = "config.json";

        readonly string _appDir = AppDomain.CurrentDomain.BaseDirectory;
        Config _config;
        List<RequestBlock> _requests;
        HttpExecutor _executor;
        CancellationTokenSource _cts;
        System.Windows.Forms.Timer _pollTimer;
        string _lastCsvFile;
        string _lastHtmlFile;
        Stopwatch _testStopwatch;
        int _testDurationSeconds;
        int _warmupSeconds;

        public MainForm()
        {
            InitializeComponent();

            string iconPath = Path.Combine(_appDir, "icon.ico");
            if (File.Exists(iconPath))
                this.Icon = new System.Drawing.Icon(iconPath);

            _config = Config.Load(Path.Combine(_appDir, ConfigFile));
            ApplyConfigToUI();

            _pollTimer          = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 1000;
            _pollTimer.Tick    += PollTimer_Tick;
        }

        void ApplyConfigToUI()
        {
            txtBaseUrl.Text    = _config.BaseUrl;
            nudThreads.Value   = Math.Max(nudThreads.Minimum,
                                 Math.Min(nudThreads.Maximum, _config.Threads));
            nudDuration.Value  = Math.Max(nudDuration.Minimum,
                                 Math.Min(nudDuration.Maximum, _config.DurationSeconds));
            nudWarmup.Value    = Math.Max(nudWarmup.Minimum,
                                 Math.Min(nudWarmup.Maximum, _config.WarmupSeconds));
            chkRamp.Checked    = _config.Ramp;
            chkShuffle.Checked = _config.Shuffle;
            txtToken.Text      = _config.BearerToken;

            if (!string.IsNullOrEmpty(_config.SessionFile) && File.Exists(_config.SessionFile))
                LoadSession(_config.SessionFile);
        }

        void SaveConfigFromUI()
        {
            _config.BaseUrl         = txtBaseUrl.Text.Trim();
            _config.Threads         = (int)nudThreads.Value;
            _config.DurationSeconds = (int)nudDuration.Value;
            _config.WarmupSeconds   = (int)nudWarmup.Value;
            _config.Ramp            = chkRamp.Checked;
            _config.Shuffle         = chkShuffle.Checked;
            _config.BearerToken     = txtToken.Text.Trim();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            SaveConfigFromUI();
            _config.Save(Path.Combine(_appDir, ConfigFile));
            base.OnFormClosing(e);
        }

        void MenuOpenSession_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title  = "Open WebSurge Session File";
                dlg.Filter = "WebSurge files (*.websurge)|*.websurge|All files (*.*)|*.*";
                if (dlg.ShowDialog() == DialogResult.OK)
                    LoadSession(dlg.FileName);
            }
        }

        void MenuImportHar_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title  = "Import HAR File";
                dlg.Filter = "HAR files (*.har)|*.har|All files (*.*)|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                var imported = HarImporter.Import(dlg.FileName);
                if (imported.Count == 0)
                {
                    MessageBox.Show("No requests found in the HAR file.", "Import HAR",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _requests = imported;
                txtSessionFile.Text = dlg.FileName;
                _config.SessionFile = "";   // HAR path; don't auto-restore on next launch
                RefreshRequestList();
                lblStatus.Text = imported.Count + " requests imported from HAR: "
                               + System.IO.Path.GetFileName(dlg.FileName);
            }
        }

        void LoadSession(string path)
        {
            try
            {
                _requests = Session.Parse(path);
                txtSessionFile.Text = path;
                _config.SessionFile = path;
                RefreshRequestList();
                lblStatus.Text = _requests.Count + " requests loaded from " + Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                MessageBox.Show("Could not load session file:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void RefreshRequestList()
        {
            lstRequests.Items.Clear();
            if (_requests != null)
                foreach (var r in _requests)
                    lstRequests.Items.Add(RequestDisplayLine(r));
            bool hasRequests = _requests != null && _requests.Count > 0;
            btnRun.Enabled           = hasRequests;
            miSaveSession.Enabled    = hasRequests;
            btnRemoveRequest.Enabled = false;
        }

        string RequestDisplayLine(RequestBlock r)
        {
            string origin;
            if (r.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(r.Path);
                    origin = uri.GetLeftPart(UriPartial.Authority);
                }
                catch { origin = "?"; }
            }
            else
            {
                string baseUrl = txtBaseUrl.Text.Trim();
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    try
                    {
                        var uri = new Uri(baseUrl);
                        origin = uri.GetLeftPart(UriPartial.Authority);
                    }
                    catch { origin = baseUrl; }
                }
                else
                {
                    origin = "?";
                }
            }
            return r.Method.PadRight(8) + origin.PadRight(40) + r.Name;
        }

        void MenuSaveSession_Click(object sender, EventArgs e)
        {
            if (_requests == null || _requests.Count == 0) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title      = "Save Session As";
                dlg.Filter     = "WebSurge files (*.websurge)|*.websurge|All files (*.*)|*.*";
                dlg.DefaultExt = "websurge";
                if (!string.IsNullOrEmpty(_config.SessionFile))
                    dlg.FileName = Path.GetFileName(_config.SessionFile);
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    Session.Save(dlg.FileName, _requests);
                    _config.SessionFile = dlg.FileName;
                    txtSessionFile.Text = dlg.FileName;
                    lblStatus.Text = "Session saved: " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    MessageBox.Show("Could not save session:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        void BtnAddRequest_Click(object sender, EventArgs e)
        {
            using (var form = new AddRequestForm())
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                if (_requests == null) _requests = new List<RequestBlock>();
                _requests.Add(form.Result);
                RefreshRequestList();
                lstRequests.SelectedIndex = _requests.Count - 1;
                lblStatus.Text = _requests.Count + " requests";
            }
        }

        void BtnRemoveRequest_Click(object sender, EventArgs e)
        {
            int idx = lstRequests.SelectedIndex;
            if (idx < 0 || _requests == null || idx >= _requests.Count) return;
            _requests.RemoveAt(idx);
            RefreshRequestList();
            if (_requests.Count > 0)
                lstRequests.SelectedIndex = Math.Min(idx, _requests.Count - 1);
            lblStatus.Text = _requests.Count + " requests";
        }

        void LstRequests_DoubleClick(object sender, EventArgs e)
        {
            int idx = lstRequests.SelectedIndex;
            if (idx < 0 || _requests == null || idx >= _requests.Count) return;
            using (var form = new AddRequestForm(_requests[idx]))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _requests[idx] = form.Result;
                RefreshRequestList();
                lstRequests.SelectedIndex = idx;
            }
        }

        void LstRequests_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemoveRequest.Enabled = lstRequests.SelectedIndex >= 0;
        }

        void BtnRun_Click(object sender, EventArgs e)
        {
            // Stop if running
            if (_cts != null)
            {
                _cts.Cancel();
                btnRun.Text = "Run Test";
                lblRunStatus.Text = "Stopping...";
                return;
            }

            if (_requests == null || _requests.Count == 0)
            {
                MessageBox.Show("Please load a .websurge session file first.", "No Session",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string baseUrl = txtBaseUrl.Text.Trim();

            // Build timestamped CSV path so reruns don't overwrite each other
            string stamp  = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _lastCsvFile  = Path.Combine(_appDir, "results-" + stamp + ".csv");
            _lastHtmlFile = _lastCsvFile.Replace(".csv", ".html");

            _testDurationSeconds = (int)nudDuration.Value;
            _warmupSeconds       = (int)nudWarmup.Value;
            _testStopwatch       = Stopwatch.StartNew();

            _executor = new HttpExecutor();
            _executor.TestCompleted += OnTestCompleted;

            _cts = new CancellationTokenSource();

            // Switch UI to running state
            btnRun.Text           = "Stop Test";
            btnRun.BackColor      = System.Drawing.Color.FromArgb(231, 76, 60);
            btnOpenReport.Enabled = false;
            lblResultPath.Text    = "";
            lblRunStatus.Text     = "Running...";
            lblElapsed.Text       = "Elapsed: 0s";
            lblLiveStats.Text     = "Requests: 0 | Errors: 0 | Req/s: 0.0";
            lblStatus.Text        = "Test started: " + _lastCsvFile;

            _pollTimer.Start();

            var requests     = new List<RequestBlock>(_requests);
            var bearerToken  = txtToken.Text.Trim();
            var concurrency  = (int)nudThreads.Value;
            var duration     = _testDurationSeconds;
            var warmup       = _warmupSeconds;
            var ramp         = chkRamp.Checked;
            var shuffle      = chkShuffle.Checked;
            var csvFile      = _lastCsvFile;
            var token        = _cts.Token;

            Task.Run(() => _executor.Run(
                requests, baseUrl, bearerToken,
                concurrency, duration, warmup,
                ramp, shuffle, csvFile, token));
        }

        void PollTimer_Tick(object sender, EventArgs e)
        {
            if (_executor == null || _testStopwatch == null) return;

            int elapsed = (int)_testStopwatch.Elapsed.TotalSeconds;
            int total   = _testDurationSeconds + _warmupSeconds;
            lblElapsed.Text = "Elapsed: " + elapsed + "s / " + total + "s";

            int  completed = _executor.Stats.RequestsCompleted;
            int  errors    = _executor.Stats.ErrorCount;
            double rps     = elapsed > 0
                ? Math.Round(completed / (double)elapsed, 1)
                : 0.0;

            lblLiveStats.Text = "Requests: " + completed
                              + " | Errors: " + errors
                              + " | Req/s: " + rps.ToString("F1");
        }

        void OnTestCompleted(string csvPath)
        {
            // Fired from thread pool -- marshal to UI thread
            if (InvokeRequired)
            {
                Invoke((Action<string>)OnTestCompleted, csvPath);
                return;
            }

            _pollTimer.Stop();
            if (_cts != null) _cts.Dispose();
            _cts = null;

            btnRun.Text      = "Run Test";
            btnRun.BackColor = System.Drawing.Color.FromArgb(46, 204, 113);
            lblRunStatus.Text = "Generating report...";
            lblStatus.Text    = "Test complete. Generating report...";

            // Final stats update
            PollTimer_Tick(null, null);

            // Generate HTML report on background thread
            Task.Run(() =>
            {
                try { ReportGenerator.Generate(csvPath); }
                catch (Exception ex) { Logger.Error(ex); }

                Invoke((Action)(() =>
                {
                    if (File.Exists(_lastHtmlFile))
                    {
                        btnOpenReport.Enabled = true;
                        lblResultPath.Text    = _lastHtmlFile;
                        lblRunStatus.Text     = "Report ready";
                        lblStatus.Text        = "Complete. Report: " + _lastHtmlFile;
                    }
                    else
                    {
                        lblRunStatus.Text = "Test complete (report not generated)";
                        lblStatus.Text    = "Complete.";
                    }
                    _executor       = null;
                    _testStopwatch  = null;
                }));
            });
        }

        void BtnOpenReport_Click(object sender, EventArgs e)
        {
            if (File.Exists(_lastHtmlFile))
                Process.Start(_lastHtmlFile);
        }
    }
}
