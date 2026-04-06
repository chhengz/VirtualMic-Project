using System;
using System.Drawing;
using System.Windows.Forms;

namespace VirtualMicMixer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        // ── Device selectors ─────────────────────────────────────────────────
        private Label lblVbCable, lblMonitor, lblMicInput;
        private ComboBox cmbVbCable, cmbMonitor, cmbMicInput;
        private Button btnInitEngine;
        private Label lblEngineStatus;

        // ── Music / transport ────────────────────────────────────────────────
        private GroupBox grpMusic;
        private Label lblNowPlaying;
        private Button btnPlay, btnPause, btnStop, btnPrev, btnNext;
        private CheckBox chkLoop, chkMuteMusic;
        private TrackBar trkMusicVol;
        private Label lblMusicVol, lblMusicPos, lblMusicDur;
        private TrackBar trkSeek;
        private Timer tmrPosition;

        // ── Playlist panel ────────────────────────────────────────────────────
        private GroupBox grpPlaylist;
        private Label lblFolder;
        private TextBox txtFolderPath;
        private Button btnBrowseFolder, btnAddFiles, btnClearPlaylist;
        private CheckBox chkSubfolders;
        private Button btnShuffle, btnRepeatAll, btnRepeatOne;
        private ListView lstPlaylist;
        private Label lblTrackCount;

        // ── Mic section ──────────────────────────────────────────────────────
        private GroupBox grpMic;
        private Button btnEnableMic, btnDisableMic;
        private CheckBox chkMuteMic;
        private TrackBar trkMicVol;
        private Label lblMicVol;

        // ── Status bar ───────────────────────────────────────────────────────
        private StatusStrip statusStrip;
        private ToolStripStatusLabel tsslStatus;

        // ── Dynamic info labels ──────────────────────────────────────────────
        private Label _lblInfoFolderVal, _lblInfoTrackVal, _lblInfoIdxVal, _lblInfoModeVal;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            this.SuspendLayout();

            // ── Form ─────────────────────────────────────────────────────────
            this.Text = "Virtual Mic Mixer  –  VB-CABLE + NAudio";
            this.Size = new Size(860, 600);
            this.MinimumSize = new Size(860, 680);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(28, 28, 32);
            this.ForeColor = Color.FromArgb(220, 220, 220);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ══ DEVICE PANEL ════════════════════════════════════════════════
            var pnlDevices = MakePanel(8, 8, 838, 110);
            pnlDevices.BorderStyle = BorderStyle.FixedSingle;

            var lblDev = MakeLabel("Audio Devices", 8, 6, bold: true);
            lblDev.ForeColor = Color.FromArgb(100, 180, 255);

            lblVbCable = MakeLabel("VB-CABLE Output (virtual mic):", 8, 28);
            cmbVbCable = MakeCombo(232, 25, 300);

            lblMonitor = MakeLabel("Monitor / Headphones:", 8, 55);
            cmbMonitor = MakeCombo(232, 52, 300);

            lblMicInput = MakeLabel("Microphone Input:", 8, 82);
            cmbMicInput = MakeCombo(232, 79, 240);

            btnInitEngine = MakeButton("▶  Start Engine", 700, 50, 130, 32);
            btnInitEngine.BackColor = Color.FromArgb(0, 120, 60);
            btnInitEngine.Click += BtnInitEngine_Click;

            lblEngineStatus = MakeLabel("Engine stopped", 700, 88, bold: false);
            lblEngineStatus.ForeColor = Color.FromArgb(160, 160, 160);
            lblEngineStatus.Width = 130;

            pnlDevices.Controls.AddRange(new Control[]
            {
                lblDev, lblVbCable, cmbVbCable,
                lblMonitor, cmbMonitor,
                lblMicInput, cmbMicInput,
                btnInitEngine, lblEngineStatus
            });

            // ══ PLAYLIST GROUP (left) ════════════════════════════════════════
            grpPlaylist = MakeGroup("  📂  Folder Playlist", 8, 126, 420, 380);

            lblFolder = MakeLabel("Folder:", 10, 22);
            txtFolderPath = new TextBox
            {
                Location = new Point(65, 19),
                Size = new Size(234, 22),
                BackColor = Color.FromArgb(48, 48, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true
            };
            btnBrowseFolder = MakeButton("Browse…", 306, 18, 72, 26);
            btnBrowseFolder.Click += BtnBrowseFolder_Click;

            chkSubfolders = new CheckBox
            {
                Text = "Include subfolders",
                Location = new Point(10, 50),
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 180, 180)
            };

            btnAddFiles = MakeButton("+ Add Files", 210, 46, 90, 24);
            btnAddFiles.Click += BtnAddFiles_Click;

            btnClearPlaylist = MakeButton("✕ Clear", 308, 46, 70, 24);
            btnClearPlaylist.BackColor = Color.FromArgb(100, 35, 35);
            btnClearPlaylist.Click += BtnClearPlaylist_Click;

            // Mode toggle buttons
            btnShuffle = MakeToggleButton("⇀ Shuffle", 10, 78, 88, 26);
            btnShuffle.Click += BtnShuffle_Click;

            btnRepeatAll = MakeToggleButton("↺ Repeat All", 106, 78, 100, 26);
            btnRepeatAll.Click += BtnRepeatAll_Click;

            btnRepeatOne = MakeToggleButton("↻ Repeat One", 214, 78, 104, 26);
            btnRepeatOne.Click += BtnRepeatOne_Click;

            lblTrackCount = MakeLabel("0 tracks", 330, 83);
            lblTrackCount.ForeColor = Color.FromArgb(130, 130, 130);
            lblTrackCount.Width = 82;

            // Playlist ListView
            lstPlaylist = new ListView
            {
                Location = new Point(8, 112),
                Size = new Size(400, 255),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Color.FromArgb(34, 34, 38),
                ForeColor = Color.FromArgb(210, 210, 215),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5f)
            };
            lstPlaylist.Columns.Add("#", 36);
            lstPlaylist.Columns.Add("Title", 278);
            lstPlaylist.Columns.Add("Type", 50);
            lstPlaylist.DoubleClick += LstPlaylist_DoubleClick;
            lstPlaylist.KeyDown += LstPlaylist_KeyDown;

            grpPlaylist.Controls.AddRange(new Control[]
            {
                lblFolder, txtFolderPath, btnBrowseFolder,
                chkSubfolders, btnAddFiles, btnClearPlaylist,
                btnShuffle, btnRepeatAll, btnRepeatOne, lblTrackCount,
                lstPlaylist
            });

            // ══ MUSIC TRANSPORT GROUP (right) ════════════════════════════════
            grpMusic = MakeGroup("  🎵  Now Playing", 436, 126, 410, 380);

            // Now-playing label
            lblNowPlaying = new Label
            {
                Location = new Point(10, 22),
                Size = new Size(386, 36),
                Text = "— No track selected —",
                ForeColor = Color.FromArgb(160, 200, 255),
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                AutoEllipsis = true
            };

            // Transport row
            btnPrev = MakeButton("⏮", 10, 68, 48, 38);
            btnPrev.Font = new Font("Segoe UI", 12f);
            btnPrev.Click += BtnPrev_Click;

            btnPlay = MakeButton("⏵", 66, 68, 60, 38);
            btnPlay.BackColor = Color.FromArgb(0, 130, 70);
            btnPlay.Font = new Font("Segoe UI", 13f);
            btnPlay.Click += BtnPlay_Click;

            btnPause = MakeButton("⏸", 134, 68, 60, 38);
            btnPause.Font = new Font("Segoe UI", 12f);
            btnPause.Click += BtnPause_Click;

            btnStop = MakeButton("⏹", 202, 68, 48, 38);
            btnStop.BackColor = Color.FromArgb(140, 40, 40);
            btnStop.Font = new Font("Segoe UI", 12f);
            btnStop.Click += BtnStop_Click;

            btnNext = MakeButton("⏭", 258, 68, 48, 38);
            btnNext.Font = new Font("Segoe UI", 12f);
            btnNext.Click += BtnNext_Click;

            chkLoop = new CheckBox
            {
                Text = "Loop track",
                Location = new Point(318, 78),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200)
            };

            // Seek row
            var lblSeekHdr = MakeLabel("Position:", 10, 122);
            trkSeek = new TrackBar
            {
                Location = new Point(78, 116),
                Size = new Size(238, 30),
                Minimum = 0,
                Maximum = 1000,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(40, 40, 44)
            };
            trkSeek.Scroll += TrkSeek_Scroll;

            lblMusicPos = MakeLabel("0:00", 322, 122);
            lblMusicPos.Width = 38;
            lblMusicDur = MakeLabel("/ 0:00", 362, 122);
            lblMusicDur.Width = 50;

            // Volume row
            var lblMusVolHdr = MakeLabel("Volume:", 10, 162);
            trkMusicVol = MakeVolumeTrack(78, 156, 218);
            trkMusicVol.ValueChanged += TrkMusicVol_Changed;
            lblMusicVol = MakeLabel("100%", 302, 162);
            lblMusicVol.Width = 45;

            chkMuteMusic = new CheckBox
            {
                Text = "Mute",
                Location = new Point(352, 163),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            chkMuteMusic.CheckedChanged += ChkMuteMusic_Changed;

            // ── Info panel ────────────────────────────────────────────────────
            var pnlInfo = new Panel
            {
                Location = new Point(8, 210),
                Size = new Size(390, 160),
                BackColor = Color.FromArgb(30, 30, 34),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblInfoHdr = MakeLabel("Playlist Info", 8, 8, bold: true);
            lblInfoHdr.ForeColor = Color.FromArgb(100, 180, 255);

            var lblInfoFolderKey = MakeLabel("Folder:", 8, 34);
            _lblInfoFolderVal = new Label
            {
                Location = new Point(65, 34),
                Size = new Size(316, 16),
                Text = "—",
                ForeColor = Color.FromArgb(190, 190, 190),
                AutoEllipsis = true,
                AutoSize = false
            };

            var lblInfoTrackKey = MakeLabel("Track:", 8, 58);
            _lblInfoTrackVal = new Label
            {
                Location = new Point(65, 58),
                Size = new Size(316, 16),
                Text = "—",
                ForeColor = Color.FromArgb(190, 190, 190),
                AutoEllipsis = true,
                AutoSize = false
            };

            var lblInfoIdxKey = MakeLabel("Index:", 8, 82);
            _lblInfoIdxVal = MakeLabel("—", 65, 82);
            _lblInfoIdxVal.ForeColor = Color.FromArgb(190, 190, 190);

            var lblInfoModeKey = MakeLabel("Mode:", 8, 106);
            _lblInfoModeVal = MakeLabel("Normal", 65, 106);
            _lblInfoModeVal.ForeColor = Color.FromArgb(100, 220, 100);

            pnlInfo.Controls.AddRange(new Control[]
            {
                lblInfoHdr,
                lblInfoFolderKey, _lblInfoFolderVal,
                lblInfoTrackKey,  _lblInfoTrackVal,
                lblInfoIdxKey,    _lblInfoIdxVal,
                lblInfoModeKey,   _lblInfoModeVal
            });

            grpMusic.Controls.AddRange(new Control[]
            {
                lblNowPlaying,
                btnPrev, btnPlay, btnPause, btnStop, btnNext, chkLoop,
                lblSeekHdr, trkSeek, lblMusicPos, lblMusicDur,
                lblMusVolHdr, trkMusicVol, lblMusicVol, chkMuteMusic,
                pnlInfo
            });

            // ── Position timer ────────────────────────────────────────────────
            tmrPosition = new Timer(components) { Interval = 250 };
            tmrPosition.Tick += TmrPosition_Tick;

            // ══ MIC GROUP ═══════════════════════════════════════════════════
            grpMic = MakeGroup("  🎤  Microphone", 8, 514, 838, 90);

            btnEnableMic = MakeButton("Enable Mic", 10, 24, 100, 30);
            btnEnableMic.BackColor = Color.FromArgb(0, 100, 160);
            btnEnableMic.Click += BtnEnableMic_Click;

            btnDisableMic = MakeButton("Disable Mic", 118, 24, 100, 30);
            btnDisableMic.BackColor = Color.FromArgb(100, 40, 40);
            btnDisableMic.Click += BtnDisableMic_Click;

            var lblMicVolHdr = MakeLabel("Mic Volume:", 240, 30);
            trkMicVol = MakeVolumeTrack(330, 24, 200);
            trkMicVol.ValueChanged += TrkMicVol_Changed;
            lblMicVol = MakeLabel("100%", 536, 30);
            lblMicVol.Width = 45;

            chkMuteMic = new CheckBox
            {
                Text = "Mute Mic",
                Location = new Point(596, 30),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            chkMuteMic.CheckedChanged += ChkMuteMic_Changed;

            grpMic.Controls.AddRange(new Control[]
            {
                btnEnableMic, btnDisableMic,
                lblMicVolHdr, trkMicVol, lblMicVol, chkMuteMic
            });

            // ══ STATUS BAR ══════════════════════════════════════════════════
            statusStrip = new StatusStrip { BackColor = Color.FromArgb(18, 18, 22) };
            tsslStatus = new ToolStripStatusLabel("Ready  –  Select devices and click Start Engine")
            {
                ForeColor = Color.FromArgb(180, 180, 180),
                Spring = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            statusStrip.Items.Add(tsslStatus);

            // ══ ADD TO FORM ══════════════════════════════════════════════════
            this.Controls.AddRange(new Control[]
            {
                pnlDevices, grpPlaylist, grpMusic, grpMic, statusStrip
            });

            this.ResumeLayout(false);
        }

        // ── UI factory helpers ────────────────────────────────────────────────
        private Panel MakePanel(int x, int y, int w, int h) =>
            new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.FromArgb(40, 40, 44)
            };

        private GroupBox MakeGroup(string text, int x, int y, int w, int h) =>
            new GroupBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = Color.FromArgb(180, 210, 255),
                BackColor = Color.FromArgb(36, 36, 40)
            };

        private Label MakeLabel(string text, int x, int y, bool bold = false) =>
            new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = bold ? new Font("Segoe UI", 9f, FontStyle.Bold) : this.Font
            };

        private ComboBox MakeCombo(int x, int y, int w) =>
            new ComboBox
            {
                Location = new Point(x, y),
                Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(52, 52, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

        private Button MakeButton(string text, int x, int y, int w, int h) =>
            new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.FromArgb(58, 58, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(85, 85, 92) },
                Cursor = Cursors.Hand
            };

        private Button MakeToggleButton(string text, int x, int y, int w, int h)
        {
            var btn = MakeButton(text, x, y, w, h);
            btn.Tag = false;
            return btn;
        }

        private TrackBar MakeVolumeTrack(int x, int y, int w) =>
            new TrackBar
            {
                Location = new Point(x, y),
                Width = w,
                Minimum = 0,
                Maximum = 200,
                Value = 100,
                TickFrequency = 20,
                SmallChange = 5,
                BackColor = Color.FromArgb(42, 42, 46)
            };
    }
}