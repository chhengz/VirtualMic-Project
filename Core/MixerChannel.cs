using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VirtualMicMixer.Core
{
    /// <summary>
    /// Wraps an ISampleProvider with per-channel volume and pan.
    /// Feed this into MixingSampleProvider.
    /// </summary>
    public class MixerChannel : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly VolumeSampleProvider _volumeProvider;
        private readonly PanningSampleProvider _panProvider;   // stereo only
        private readonly ISampleProvider _output;

        private float _volume = 1.0f;
        private float _pan = 0f;

        public WaveFormat WaveFormat => _output.WaveFormat;
        public string Name { get; }
        public bool IsMuted { get; set; }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0f, Math.Min(2f, value));
                _volumeProvider.Volume = IsMuted ? 0f : _volume;
            }
        }

        public float Pan
        {
            get => _pan;
            set
            {
                _pan = Math.Max(-1f, Math.Min(1f, value));
                if (_panProvider != null)
                    _panProvider.Pan = _pan;
            }
        }

        // ── Constructor ──────────────────────────────────────────────────────
        public MixerChannel(ISampleProvider source, string name, WaveFormat targetFormat)
        {
            Name = name;

            // Resample if needed
            ISampleProvider resampled = source;
            if (source.WaveFormat.SampleRate != targetFormat.SampleRate ||
                source.WaveFormat.Channels != targetFormat.Channels)
            {
                resampled = new MediaFoundationResampler(
                    source.ToWaveProvider(),
                    targetFormat).ToSampleProvider();
            }

            _volumeProvider = new VolumeSampleProvider(resampled) { Volume = _volume };

            // Pan only makes sense for stereo
            if (targetFormat.Channels == 2)
            {
                _panProvider = new PanningSampleProvider(
                    _volumeProvider.ToMono())
                { Pan = _pan };
                _output = _panProvider;
            }
            else
            {
                _output = _volumeProvider;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // Honour live mute toggle
            _volumeProvider.Volume = IsMuted ? 0f : _volume;
            return _output.Read(buffer, offset, count);
        }
    }
}