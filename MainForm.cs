using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VirtualMicMixer.Core;

namespace VirtualMicMixer
{
    public partial class MainForm : Form
    {
        // ── Core objects ──────────────────────────────────────────────────────
        private readonly AudioEngine _engine = new AudioEngine();
        private readonly PlaylistManager _playlist = new PlaylistManager();

        // ── Device lists ──────────────────────────────────────────────────────
        private List<AudioDeviceInfo> _outputs;
        private List<AudioDeviceInfo> _inputs;

        // ── State flags ───────────────────────────────────────────────────────
        private bool _engineRunning;
        private bool _musicPaused;

        // ── Colors for toggle-button active state ─────────────────────────────
        private static readonly Color ClrToggleOn = Color.FromArgb(0, 110, 180);
        private static readonly Color ClrToggleOff = Color.FromArgb(58, 58, 64);

        // ─────────────────────────────────────────────────────────────────────
        public MainForm()
        {
            InitializeComponent();
            LoadDevices();
            UpdateUI();
            RefreshPlaylistView();

            this.FormClosing += (s, e) => _engine.Dispose();
            _engine.MusicFinished += Engine_MusicFinished;
        }

        // ══ DEVICE ENUMERATION ═══════════════════════════════════════════════
        private void LoadDevices()
        {
            _outputs = DeviceManager.GetOutputDevices();
            _inputs = DeviceManager.GetInputDevices();

            cmbVbCable.Items.Clear();
            cmbMonitor.Items.Clear();
            cmbMicInput.Items.Clear();
            cmbMonitor.Items.Add("(none)");

            foreach (var d in _outputs)
            {
                string label = d.IsVbCable ? $"★ {d.Name}" : d.Name;
                cmbVbCable.Items.Add(label);
                cmbMonitor.Items.Add(label);
            }
            foreach (var d in _inputs)
                cmbMicInput.Items.Add(d.Name);

            int vbIdx = DeviceManager.FindVbCableOutputIndex();
            cmbVbCable.SelectedIndex = vbIdx >= 0 ? vbIdx : (cmbVbCable.Items.Count > 0 ? 0 : -1);
            cmbMonitor.SelectedIndex = 0;
            if (cmbMicInput.Items.Count > 0) cmbMicInput.SelectedIndex = 0;

            if (vbIdx < 0)
                SetStatus("⚠  VB-CABLE not detected. Install VB-CABLE and restart.", Color.Orange);
        }

        // ══ ENGINE ════════════════════════════════════════════════════════════
        private void BtnInitEngine_Click(object sender, EventArgs e)
        {
            if (_engineRunning)
            {
                _engine.Stop();
                _engineRunning = false;
                tmrPosition.Stop();
                btnInitEngine.Text = "▶  Start Engine";
                btnInitEngine.BackColor = Color.FromArgb(0, 120, 60);
                lblEngineStatus.Text = "Engine stopped";
                lblEngineStatus.ForeColor = Color.FromArgb(160, 160, 160);
                SetStatus("Engine stopped.");
                UpdateUI();
                return;
            }

            if (cmbVbCable.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a VB-CABLE output device.", "No device",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int vbIdx = cmbVbCable.SelectedIndex;
            int monIdx = cmbMonitor.SelectedIndex <= 0 ? -1 : cmbMonitor.SelectedIndex - 1;

            try
            {
                _engine.Initialise(vbIdx, monIdx);
                _engineRunning = true;
                tmrPosition.Start();
                btnInitEngine.Text = "⏹  Stop Engine";
                btnInitEngine.BackColor = Color.FromArgb(140, 40, 40);
                lblEngineStatus.Text = "● Running";
                lblEngineStatus.ForeColor = Color.FromArgb(80, 220, 100);
                SetStatus($"Engine running → {_outputs[vbIdx].Name}");
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start engine:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══ PLAYLIST – FOLDER LOADING ═════════════════════════════════════════
        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select a folder containing audio files";
                dlg.ShowNewFolderButton = false;

                if (!string.IsNullOrEmpty(txtFolderPath.Text) &&
                    Directory.Exists(txtFolderPath.Text))
                    dlg.SelectedPath = txtFolderPath.Text;

                if (dlg.ShowDialog() != DialogResult.OK) return;

                txtFolderPath.Text = dlg.SelectedPath;
                bool recursive = chkSubfolders.Checked;

                var entries = _playlist.LoadFolder(dlg.SelectedPath, recursive);

                if (entries.Count == 0)
                {
                    SetStatus("⚠  No audio files found in the selected folder.", Color.Orange);
                    RefreshPlaylistView();
                    return;
                }

                RefreshPlaylistView();
                SetStatus($"Loaded {entries.Count} tracks from: {Path.GetFileName(dlg.SelectedPath)}");
                UpdateInfoPanel();
            }
        }

        private void BtnAddFiles_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Add audio files to playlist";
                dlg.Filter = "Audio files|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a;*.wma;*.opus|All files|*.*";
                dlg.Multiselect = true;

                if (dlg.ShowDialog() != DialogResult.OK) return;

                _playlist.AddFiles(dlg.FileNames);
                RefreshPlaylistView();
                SetStatus($"Added {dlg.FileNames.Length} file(s). Total: {_playlist.Count} tracks.");
            }
        }

        private void BtnClearPlaylist_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0) return;
            var result = MessageBox.Show(
                "Clear the entire playlist?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            _engine.StopMusic();
            _musicPaused = false;
            _playlist.Clear();
            RefreshPlaylistView();
            lblNowPlaying.Text = "— No track selected —";
            trkSeek.Value = 0;
            lblMusicPos.Text = "0:00";
            lblMusicDur.Text = "/ 0:00";
            UpdateInfoPanel();
            SetStatus("Playlist cleared.");
            UpdateUI();
        }

        // ══ PLAYLIST – LISTVIEW ═══════════════════════════════════════════════
        private void RefreshPlaylistView()
        {
            lstPlaylist.BeginUpdate();
            lstPlaylist.Items.Clear();

            var queue = _playlist.Queue;
            for (int i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                var item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(entry.Title);
                item.SubItems.Add(entry.Ext);
                item.Tag = i; // queue index

                if (i == _playlist.CurrentIndex)
                {
                    item.BackColor = Color.FromArgb(0, 80, 140);
                    item.ForeColor = Color.White;
                }

                lstPlaylist.Items.Add(item);
            }

            int count = _playlist.Count;
            lblTrackCount.Text = count == 1 ? "1 track" : $"{count} tracks";
            lstPlaylist.EndUpdate();
        }

        private void HighlightCurrentInList()
        {
            foreach (ListViewItem item in lstPlaylist.Items)
            {
                int qi = (int)item.Tag;
                if (qi == _playlist.CurrentIndex)
                {
                    item.BackColor = Color.FromArgb(0, 80, 140);
                    item.ForeColor = Color.White;
                    item.EnsureVisible();
                }
                else
                {
                    item.BackColor = Color.FromArgb(34, 34, 38);
                    item.ForeColor = Color.FromArgb(210, 210, 215);
                }
            }
        }

        private void LstPlaylist_DoubleClick(object sender, EventArgs e)
        {
            if (lstPlaylist.SelectedItems.Count == 0) return;
            int qi = (int)lstPlaylist.SelectedItems[0].Tag;
            var entry = _playlist.GoTo(qi);
            if (entry != null) PlayEntry(entry);
        }

        private void LstPlaylist_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && lstPlaylist.SelectedItems.Count > 0)
            {
                int qi = (int)lstPlaylist.SelectedItems[0].Tag;
                bool isCurrent = (qi == _playlist.CurrentIndex);
                _playlist.RemoveAt(qi);
                if (isCurrent) { _engine.StopMusic(); _musicPaused = false; }
                RefreshPlaylistView();
                UpdateUI();
            }
            else if (e.KeyCode == Keys.Return && lstPlaylist.SelectedItems.Count > 0)
            {
                LstPlaylist_DoubleClick(sender, e);
            }
        }

        // ══ PLAYLIST – MODE TOGGLES ═══════════════════════════════════════════
        private void BtnShuffle_Click(object sender, EventArgs e)
        {
            bool newState = !_playlist.ShuffleEnabled;
            _playlist.SetShuffle(newState);
            SetToggle(btnShuffle, newState);
            RefreshPlaylistView();
            UpdateInfoPanel();
            SetStatus(newState ? "Shuffle ON" : "Shuffle OFF");
        }

        private void BtnRepeatAll_Click(object sender, EventArgs e)
        {
            bool newState = !_playlist.RepeatAll;
            _playlist.SetRepeatAll(newState);
            SetToggle(btnRepeatAll, newState);
            if (newState) SetToggle(btnRepeatOne, false);
            UpdateInfoPanel();
            SetStatus(newState ? "Repeat All ON" : "Repeat All OFF");
        }

        private void BtnRepeatOne_Click(object sender, EventArgs e)
        {
            bool newState = !_playlist.RepeatOne;
            _playlist.SetRepeatOne(newState);
            SetToggle(btnRepeatOne, newState);
            if (newState) SetToggle(btnRepeatAll, false);
            UpdateInfoPanel();
            SetStatus(newState ? "Repeat One ON" : "Repeat One OFF");
        }

        private void SetToggle(Button btn, bool on)
        {
            btn.Tag = on;
            btn.BackColor = on ? ClrToggleOn : ClrToggleOff;
        }

        // ══ TRANSPORT BUTTONS ═════════════════════════════════════════════════
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (!_engineRunning) { SetStatus("⚠  Start the engine first.", Color.Orange); return; }

            // Resume if paused
            if (_musicPaused && _engine.IsMusicLoaded)
            {
                _engine.ResumeMusic();
                _musicPaused = false;
                SetStatus($"▶  {_playlist.CurrentTrack?.Title ?? "Playing"}");
                UpdateUI();
                return;
            }

            // Play current playlist track
            var entry = _playlist.CurrentTrack;
            if (entry == null)
            {
                SetStatus("⚠  No tracks in playlist. Load a folder first.", Color.Orange);
                return;
            }
            PlayEntry(entry);
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            if (!_engine.IsMusicLoaded) return;
            if (_musicPaused)
            {
                _engine.ResumeMusic();
                _musicPaused = false;
                SetStatus($"▶  {_playlist.CurrentTrack?.Title ?? "Playing"}");
            }
            else
            {
                _engine.PauseMusic();
                _musicPaused = true;
                SetStatus("⏸  Paused");
            }
            UpdateUI();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            _engine.StopMusic();
            _musicPaused = false;
            trkSeek.Value = 0;
            lblMusicPos.Text = "0:00";
            SetStatus("⏹  Stopped");
            UpdateUI();
        }

        private void BtnPrev_Click(object sender, EventArgs e)
        {
            if (!_engineRunning) return;

            // If more than 3 s into a track, restart it instead of going back
            if (_engine.IsMusicLoaded && _engine.MusicPosition.TotalSeconds > 3)
            {
                _engine.SeekMusic(TimeSpan.Zero);
                return;
            }

            var entry = _playlist.Previous();
            if (entry != null) PlayEntry(entry);
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (!_engineRunning) return;
            var entry = _playlist.Next();
            if (entry != null) PlayEntry(entry);
            else
            {
                _engine.StopMusic();
                _musicPaused = false;
                SetStatus("⏹  End of playlist.");
                UpdateUI();
            }
        }

        // ══ PLAYBACK CORE ═════════════════════════════════════════════════════
        private void PlayEntry(PlaylistEntry entry)
        {
            if (!_engineRunning) return;

            try
            {
                _engine.LoadMusic(entry.FilePath, chkLoop.Checked);
                _engine.MusicVolume = trkMusicVol.Value / 100f;
                _musicPaused = false;

                trkSeek.Maximum = Math.Max(1, (int)_engine.MusicDuration.TotalSeconds);
                lblMusicDur.Text = $"/ {FormatTime(_engine.MusicDuration)}";
                lblNowPlaying.Text = entry.Title;

                HighlightCurrentInList();
                UpdateInfoPanel();
                SetStatus($"▶  {entry.Title}");
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot play:\n{entry.FilePath}\n\n{ex.Message}",
                    "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // Try skipping to next track automatically
                var next = _playlist.Next();
                if (next != null) PlayEntry(next);
            }
        }

        // ══ AUTO-ADVANCE ON TRACK END ═════════════════════════════════════════
        //
        // MusicFinished is raised on a ThreadPool thread (AudioEngine design).
        // We must NOT call LoadMusic/PlayEntry synchronously here — the audio
        // render thread may still be finishing its Read() and RemoveMixerInput
        // would deadlock.
        //
        // Safe pattern:
        //   1. BeginInvoke  → hop to UI thread (non-blocking from pool thread)
        //   2. Short Timer  → let the audio thread finish its current buffer (~150 ms)
        //   3. Timer.Tick   → NOW safe to call PlayEntry / LoadMusic
        //
        private void Engine_MusicFinished(object sender, EventArgs e)
        {
            // Always arrives on a pool thread – BeginInvoke is non-blocking here
            BeginInvoke(new Action(() =>
            {
                _musicPaused = false;

                var next = _playlist.Next();
                if (next == null)
                {
                    trkSeek.Value = 0;
                    lblMusicPos.Text = "0:00";
                    SetStatus("⏹  Playlist finished.");
                    UpdateUI();
                    return;
                }

                // Short delay: give the audio thread ~150 ms to exit Read() cleanly
                var t = new System.Windows.Forms.Timer { Interval = 150 };
                t.Tick += (ts, te) =>
                {
                    t.Stop();
                    t.Dispose();
                    PlayEntry(next);   // safe: audio thread has returned by now
                };
                t.Start();
            }));
        }

        // ══ SEEK ══════════════════════════════════════════════════════════════
        private void TrkSeek_Scroll(object sender, EventArgs e)
        {
            if (!_engine.IsMusicLoaded) return;
            _engine.SeekMusic(TimeSpan.FromSeconds(trkSeek.Value));
        }

        // ══ POSITION TIMER ════════════════════════════════════════════════════
        private void TmrPosition_Tick(object sender, EventArgs e)
        {
            if (!_engine.IsMusicLoaded) return;
            var pos = _engine.MusicPosition;
            lblMusicPos.Text = FormatTime(pos);
            if (_engine.MusicDuration.TotalSeconds > 0)
                trkSeek.Value = Math.Min(trkSeek.Maximum, (int)pos.TotalSeconds);
        }

        // ══ VOLUME / MUTE ═════════════════════════════════════════════════════
        private void TrkMusicVol_Changed(object sender, EventArgs e)
        {
            _engine.MusicVolume = trkMusicVol.Value / 100f;
            lblMusicVol.Text = $"{trkMusicVol.Value}%";
        }

        private void TrkMicVol_Changed(object sender, EventArgs e)
        {
            _engine.MicVolume = trkMicVol.Value / 100f;
            lblMicVol.Text = $"{trkMicVol.Value}%";
        }

        private void ChkMuteMusic_Changed(object sender, EventArgs e) =>
            _engine.MusicMuted = chkMuteMusic.Checked;

        private void ChkMuteMic_Changed(object sender, EventArgs e) =>
            _engine.MicMuted = chkMuteMic.Checked;

        // ══ MICROPHONE ════════════════════════════════════════════════════════
        private void BtnEnableMic_Click(object sender, EventArgs e)
        {
            if (!_engineRunning) { SetStatus("⚠  Start the engine first.", Color.Orange); return; }
            if (cmbMicInput.SelectedIndex < 0) { SetStatus("⚠  No microphone selected.", Color.Orange); return; }
            try
            {
                _engine.EnableMic(cmbMicInput.SelectedIndex);
                _engine.MicVolume = trkMicVol.Value / 100f;
                SetStatus($"🎤  Mic active: {_inputs[cmbMicInput.SelectedIndex].Name}");
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot enable microphone:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDisableMic_Click(object sender, EventArgs e)
        {
            _engine.DisableMic();
            SetStatus("Mic disabled.");
            UpdateUI();
        }

        // ══ INFO PANEL UPDATE ═════════════════════════════════════════════════
        private void UpdateInfoPanel()
        {
            var track = _playlist.CurrentTrack;

            _lblInfoFolderVal.Text =
                string.IsNullOrEmpty(txtFolderPath.Text) ? "—" :
                Path.GetFileName(txtFolderPath.Text.TrimEnd('\\', '/'));

            _lblInfoTrackVal.Text = track?.Title ?? "—";

            _lblInfoIdxVal.Text =
                _playlist.Count > 0
                    ? $"{_playlist.CurrentIndex + 1} / {_playlist.Count}"
                    : "—";

            string mode = "Normal";
            if (_playlist.ShuffleEnabled) mode = "Shuffle";
            if (_playlist.RepeatAll) mode = "Repeat All";
            if (_playlist.RepeatOne) mode = "Repeat One";
            _lblInfoModeVal.Text = mode;
        }

        // ══ UI STATE ══════════════════════════════════════════════════════════
        private void UpdateUI()
        {
            bool eng = _engineRunning;
            bool music = _engine.IsMusicLoaded;
            bool hasTracks = _playlist.HasTracks;

            btnPlay.Enabled = eng;
            btnPause.Enabled = eng && music;
            btnStop.Enabled = eng && music;
            btnPrev.Enabled = eng && hasTracks;
            btnNext.Enabled = eng && hasTracks;
            trkSeek.Enabled = eng && music;
            trkMusicVol.Enabled = eng;
            chkMuteMusic.Enabled = eng;

            btnEnableMic.Enabled = eng;
            btnDisableMic.Enabled = eng;
            trkMicVol.Enabled = eng;
            chkMuteMic.Enabled = eng;

            cmbVbCable.Enabled = !eng;
            cmbMonitor.Enabled = !eng;

            btnPause.Text = _musicPaused ? "⏵" : "⏸";
        }

        // ══ HELPERS ═══════════════════════════════════════════════════════════
        private void SetStatus(string msg, Color? color = null)
        {
            tsslStatus.Text = msg;
            tsslStatus.ForeColor = color ?? Color.FromArgb(180, 180, 180);
        }

        private static string FormatTime(TimeSpan t) =>
            t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
    }
}