using System.Collections.Generic;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace VirtualMicMixer.Core
{
    public class AudioDeviceInfo
    {
        public string Name { get; set; }
        public int WaveIndex { get; set; }   // -1 if WASAPI-only
        public string DeviceId { get; set; } // WASAPI device ID
        public bool IsVbCable { get; set; }
    }

    public static class DeviceManager
    {
        // ── Output devices (WaveOut) ─────────────────────────────────────────
        public static List<AudioDeviceInfo> GetOutputDevices()
        {
            var list = new List<AudioDeviceInfo>();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var cap = WaveOut.GetCapabilities(i);
                list.Add(new AudioDeviceInfo
                {
                    Name = cap.ProductName,
                    WaveIndex = i,
                    IsVbCable = cap.ProductName.ToUpperInvariant().Contains("CABLE")
                });
            }
            return list;
        }

        // ── Input devices (WaveIn / real mic) ───────────────────────────────
        public static List<AudioDeviceInfo> GetInputDevices()
        {
            var list = new List<AudioDeviceInfo>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var cap = WaveIn.GetCapabilities(i);
                list.Add(new AudioDeviceInfo
                {
                    Name = cap.ProductName,
                    WaveIndex = i,
                    IsVbCable = cap.ProductName.ToUpperInvariant().Contains("CABLE")
                });
            }
            return list;
        }

        // ── Find VB-CABLE output index (WaveOut) ────────────────────────────
        public static int FindVbCableOutputIndex()
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var cap = WaveOut.GetCapabilities(i);
                if (cap.ProductName.ToUpperInvariant().Contains("CABLE"))
                    return i;
            }
            return -1; // not found
        }

        // ── Find VB-CABLE input index (WaveIn – "CABLE Output") ─────────────
        // VB-CABLE exposes its *output* as a WaveIn source.
        // This is NOT used for routing; it's listed for user info only.
        public static int FindVbCableInputIndex()
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var cap = WaveIn.GetCapabilities(i);
                if (cap.ProductName.ToUpperInvariant().Contains("CABLE"))
                    return i;
            }
            return -1;
        }
    }
}