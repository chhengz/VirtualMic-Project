using System;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VirtualMicMixer.Core
{
    /// <summary>
    /// Core audio engine.
    ///
    /// Signal flow:
    ///   [Music file]  ──┐
    ///                   ├──► MixingSampleProvider ──► WaveOut → VB-CABLE Input
    ///   [Mic capture] ──┘
    ///
    /// VB-CABLE Input appears to other apps (Discord, Zoom…) as a microphone.
    ///
    /// THREAD SAFETY NOTE
    /// ──────────────────
    /// NAudio's WaveOut callback runs on a dedicated audio thread.
    /// Any cleanup of mixer inputs (RemoveMixerInput, Dispose) must NOT be
    /// called from inside that callback – doing so causes a deadlock because
    /// MixingSampleProvider holds a lock while Read() is executing.
    ///
    /// Solution: EndDetectingReader fires its "end" notification via
    /// ThreadPool.QueueUserWorkItem so the audio-thread Read() call returns
    /// first, then MusicFinished is raised on a pool thread, and MainForm
    /// marshals the actual LoadMusic / RemoveMusicChannel work back to the
    /// UI thread through BeginInvoke + a short Timer delay.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const int SAMPLE_RATE = 48000;
        private const int CHANNELS = 2;
        private static readonly WaveFormat MixFormat =
            WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, CHANNELS);

        // ── Mixer ────────────────────────────────────────────────────────────
        private MixingSampleProvider _mixer;

        // ── Output device ────────────────────────────────────────────────────
        private WaveOutEvent _vbCableOut;

        // ── Music playback ───────────────────────────────────────────────────
        private AudioFileReader _musicReader;
        private MixerChannel _musicChannel;

        // ── Mic capture ──────────────────────────────────────────────────────
        private WaveInEvent _micCapture;
        private BufferedWaveProvider _micBuffer;
        private MixerChannel _micChannel;
        private bool _micEnabled;

        // ── Public state ─────────────────────────────────────────────────────
        public bool IsRunning { get; private set; }
        public bool IsMusicLoaded => _musicReader != null;

        public TimeSpan MusicPosition => _musicReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan MusicDuration => _musicReader?.TotalTime ?? TimeSpan.Zero;

        // ── Events ───────────────────────────────────────────────────────────
        /// <summary>
        /// Raised on a ThreadPool thread when the current track reaches its end.
        /// Subscribers MUST marshal back to the UI thread before touching any
        /// audio-engine methods (use BeginInvoke + Timer).
        /// </summary>
        public event EventHandler MusicFinished;

        // ── Volume / mute properties ─────────────────────────────────────────
        public float MusicVolume
        {
            get => _musicChannel?.Volume ?? 1f;
            set { if (_musicChannel != null) _musicChannel.Volume = value; }
        }
        public float MicVolume
        {
            get => _micChannel?.Volume ?? 1f;
            set { if (_micChannel != null) _micChannel.Volume = value; }
        }
        public bool MusicMuted
        {
            get => _musicChannel?.IsMuted ?? false;
            set { if (_musicChannel != null) _musicChannel.IsMuted = value; }
        }
        public bool MicMuted
        {
            get => _micChannel?.IsMuted ?? false;
            set { if (_micChannel != null) _micChannel.IsMuted = value; }
        }

        // ════════════════════════════════════════════════════════════════════
        // INITIALISE
        // ════════════════════════════════════════════════════════════════════
        public void Initialise(int vbCableIndex, int monitorIndex = -1)
        {
            if (IsRunning) Stop();

            _mixer = new MixingSampleProvider(MixFormat) { ReadFully = true };

            _vbCableOut = new WaveOutEvent
            {
                DeviceNumber = vbCableIndex,
                DesiredLatency = 150           // ms – larger = more stable
            };
            _vbCableOut.Init(_mixer);
            _vbCableOut.Play();

            IsRunning = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // MUSIC PLAYBACK
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load and immediately start playing a file.
        /// Safe to call from the UI thread at any time (including from inside
        /// the MusicFinished handler, as long as that handler uses BeginInvoke
        /// + Timer so it runs after the audio callback has returned).
        /// </summary>
        public void LoadMusic(string filePath, bool loop = false)
        {
            // ① Remove the old channel BEFORE creating the new reader.
            //    RemoveMixerInput acquires the mixer lock; this is safe here
            //    because we are on the UI thread, which is NOT the audio thread.
            RemoveMusicChannelSafe();

            var reader = new AudioFileReader(filePath);

            ISampleProvider source = loop
                ? (ISampleProvider)new LoopingReader(reader)
                : new EndDetectingReader(reader, OnMusicEndBackground);

            var channel = new MixerChannel(source, "Music", MixFormat);

            // ② Store references, then add to mixer.
            _musicReader = reader;
            _musicChannel = channel;
            _mixer.AddMixerInput(channel);
        }

        public void PauseMusic() { if (_musicChannel != null) _musicChannel.IsMuted = true; }
        public void ResumeMusic() { if (_musicChannel != null) _musicChannel.IsMuted = false; }

        public void StopMusic() => RemoveMusicChannelSafe();

        public void SeekMusic(TimeSpan position)
        {
            if (_musicReader != null) _musicReader.CurrentTime = position;
        }

        // ════════════════════════════════════════════════════════════════════
        // MIC
        // ════════════════════════════════════════════════════════════════════
        public void EnableMic(int deviceIndex)
        {
            DisableMic();

            _micCapture = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(SAMPLE_RATE, 16, 1),
                BufferMilliseconds = 50
            };
            _micBuffer = new BufferedWaveProvider(_micCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };
            _micCapture.DataAvailable += (s, e) =>
                _micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            _micCapture.StartRecording();
            _micEnabled = true;

            ISampleProvider micSample = _micBuffer.ToSampleProvider().ToStereo();
            _micChannel = new MixerChannel(micSample, "Microphone", MixFormat);
            _mixer.AddMixerInput(_micChannel);
        }

        public void DisableMic()
        {
            if (!_micEnabled) return;
            if (_micChannel != null) { _mixer.RemoveMixerInput(_micChannel); _micChannel = null; }
            _micCapture?.StopRecording();
            _micCapture?.Dispose();
            _micCapture = null;
            _micBuffer = null;
            _micEnabled = false;
        }

        // ════════════════════════════════════════════════════════════════════
        // STOP / DISPOSE
        // ════════════════════════════════════════════════════════════════════
        public void Stop()
        {
            DisableMic();
            RemoveMusicChannelSafe();

            _vbCableOut?.Stop();
            _vbCableOut?.Dispose();
            _vbCableOut = null;

            IsRunning = false;
        }

        public void Dispose() => Stop();

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Remove and dispose the current music channel.
        /// Must only be called from the UI thread (or Stop/Dispose).
        /// </summary>
        private void RemoveMusicChannelSafe()
        {
            if (_musicChannel != null)
            {
                _mixer?.RemoveMixerInput(_musicChannel);
                _musicChannel = null;
            }
            _musicReader?.Dispose();
            _musicReader = null;
        }

        /// <summary>
        /// Called by EndDetectingReader from INSIDE the audio-render thread.
        /// We must NOT touch the mixer here.  Post the notification to a
        /// ThreadPool thread so the Read() call finishes first.
        /// </summary>
        private void OnMusicEndBackground()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // Small sleep so the audio thread's current Read() call and
                // any immediately following buffer-fill can finish cleanly
                // before the UI thread tears down the channel.
                Thread.Sleep(80);
                MusicFinished?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // LoopingReader – loops an AudioFileReader indefinitely
    // ════════════════════════════════════════════════════════════════════════
    internal class LoopingReader : ISampleProvider
    {
        private readonly AudioFileReader _reader;
        public WaveFormat WaveFormat => _reader.WaveFormat;
        public LoopingReader(AudioFileReader reader) => _reader = reader;

        public int Read(float[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _reader.Read(buffer, offset + total, count - total);
                if (read == 0) _reader.Position = 0;
                else total += read;
            }
            return total;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // EndDetectingReader – signals end-of-stream WITHOUT calling back on the
    // audio thread.  The _onEnd callback is guaranteed to run on a ThreadPool
    // thread, never inside Read().
    // ════════════════════════════════════════════════════════════════════════
    internal class EndDetectingReader : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action _onEnd;
        private int _endFired; // 0 = not yet, 1 = fired (Interlocked)

        public WaveFormat WaveFormat => _source.WaveFormat;

        public EndDetectingReader(ISampleProvider source, Action onEnd)
        {
            _source = source;
            _onEnd = onEnd;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);

            if (read == 0 && Interlocked.CompareExchange(ref _endFired, 1, 0) == 0)
            {
                // Post the notification OUTSIDE this call stack so the audio
                // thread's Read() returns before anything touches the mixer.
                ThreadPool.QueueUserWorkItem(_ => _onEnd?.Invoke());
            }

            return read;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // FftEventArgs – placeholder for future visualiser
    // ════════════════════════════════════════════════════════════════════════
    public class FftEventArgs : EventArgs
    {
        public float[] Data { get; }
        public FftEventArgs(float[] data) => Data = data;
    }
}