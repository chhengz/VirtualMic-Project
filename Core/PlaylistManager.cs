using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VirtualMicMixer.Core
{
    public class PlaylistEntry
    {
        public string FilePath { get; set; }
        public string Title { get; set; }   // display name (no extension)
        public string Ext { get; set; }   // e.g. ".mp3"
        public int OriginalIndex { get; set; }
    }

    public class PlaylistManager
    {
        // ── Supported extensions ─────────────────────────────────────────────
        private static readonly HashSet<string> AudioExts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus"
        };

        // ── Internal state ────────────────────────────────────────────────────
        private List<PlaylistEntry> _master = new List<PlaylistEntry>(); // original order
        private List<PlaylistEntry> _queue = new List<PlaylistEntry>(); // playback order
        private int _currentIndex = -1;

        private readonly Random _rng = new Random();

        // ── Public state ──────────────────────────────────────────────────────
        public bool ShuffleEnabled { get; private set; }
        public bool RepeatAll { get; private set; }
        public bool RepeatOne { get; private set; }

        public int Count => _master.Count;
        public bool HasTracks => _master.Count > 0;
        public int CurrentIndex => _currentIndex;          // index in _queue

        public PlaylistEntry CurrentTrack =>
            (_currentIndex >= 0 && _currentIndex < _queue.Count)
                ? _queue[_currentIndex] : null;

        /// <summary>Returns a snapshot of the current playback queue.</summary>
        public IReadOnlyList<PlaylistEntry> Queue => _queue.AsReadOnly();

        // ── Load folder ───────────────────────────────────────────────────────
        /// <summary>Scans a folder (non-recursive by default) and replaces the playlist.</summary>
        public List<PlaylistEntry> LoadFolder(string folder, bool recursive = false)
        {
            var option = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(folder, "*.*", option)
                .Where(f => AudioExts.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _master.Clear();
            for (int i = 0; i < files.Count; i++)
            {
                _master.Add(new PlaylistEntry
                {
                    FilePath = files[i],
                    Title = Path.GetFileNameWithoutExtension(files[i]),
                    Ext = Path.GetExtension(files[i]).ToUpperInvariant().TrimStart('.'),
                    OriginalIndex = i
                });
            }

            RebuildQueue();
            _currentIndex = _queue.Count > 0 ? 0 : -1;
            return _queue.ToList();
        }

        /// <summary>Adds individual files to the existing playlist.</summary>
        public void AddFiles(IEnumerable<string> files)
        {
            foreach (var f in files)
            {
                if (!AudioExts.Contains(Path.GetExtension(f))) continue;
                int idx = _master.Count;
                _master.Add(new PlaylistEntry
                {
                    FilePath = f,
                    Title = Path.GetFileNameWithoutExtension(f),
                    Ext = Path.GetExtension(f).ToUpperInvariant().TrimStart('.'),
                    OriginalIndex = idx
                });
            }
            RebuildQueue();
        }

        /// <summary>Clears all tracks.</summary>
        public void Clear()
        {
            _master.Clear();
            _queue.Clear();
            _currentIndex = -1;
        }

        // ── Navigation ────────────────────────────────────────────────────────
        /// <summary>Jump to a specific queue index.</summary>
        public PlaylistEntry GoTo(int queueIndex)
        {
            if (queueIndex < 0 || queueIndex >= _queue.Count) return null;
            _currentIndex = queueIndex;
            return _queue[_currentIndex];
        }

        /// <summary>Advance to next track. Returns null if end of playlist.</summary>
        public PlaylistEntry Next()
        {
            if (_queue.Count == 0) return null;

            if (RepeatOne) return _queue[_currentIndex];

            int next = _currentIndex + 1;
            if (next >= _queue.Count)
            {
                if (RepeatAll)
                {
                    if (ShuffleEnabled) RebuildQueue();
                    next = 0;
                }
                else return null; // end of playlist
            }

            _currentIndex = next;
            return _queue[_currentIndex];
        }

        /// <summary>Go back to previous track.</summary>
        public PlaylistEntry Previous()
        {
            if (_queue.Count == 0) return null;
            int prev = Math.Max(0, _currentIndex - 1);
            _currentIndex = prev;
            return _queue[_currentIndex];
        }

        // ── Playback modes ────────────────────────────────────────────────────
        public void SetShuffle(bool enabled)
        {
            ShuffleEnabled = enabled;
            var current = CurrentTrack;
            RebuildQueue();
            // Restore position to the same track
            if (current != null)
                _currentIndex = _queue.FindIndex(e => e.FilePath == current.FilePath);
            if (_currentIndex < 0) _currentIndex = 0;
        }

        public void SetRepeatAll(bool enabled)
        {
            RepeatAll = enabled;
            if (enabled) RepeatOne = false;
        }

        public void SetRepeatOne(bool enabled)
        {
            RepeatOne = enabled;
            if (enabled) RepeatAll = false;
        }

        // ── Remove a track by queue index ─────────────────────────────────────
        public void RemoveAt(int queueIndex)
        {
            if (queueIndex < 0 || queueIndex >= _queue.Count) return;
            var entry = _queue[queueIndex];
            _master.Remove(entry);
            _queue.RemoveAt(queueIndex);
            if (_currentIndex >= _queue.Count)
                _currentIndex = _queue.Count - 1;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RebuildQueue()
        {
            if (ShuffleEnabled)
            {
                _queue = _master.OrderBy(_ => _rng.Next()).ToList();
            }
            else
            {
                _queue = _master.OrderBy(e => e.OriginalIndex).ToList();
            }
        }
    }
}