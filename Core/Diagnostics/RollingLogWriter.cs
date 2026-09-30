using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace FocusBear.Core.Diagnostics
{
    /// <summary>
    /// The app log's writer. It used to open, append one line to and close the (25 MB) log file for every line,
    /// hundreds of times a minute. Lines now go to a buffer that is appended to the file in one write about a
    /// second after the first of them arrives (no timer runs while nothing is logged), and the file is closed again
    /// between writes, so Report Problem, the log upload and the daily rotation can still read and move it.
    ///
    /// Once the file reaches <see cref="MaxBytes"/> it is rolled: <c>Debug_Logs.txt</c> becomes
    /// <c>Debug_Logs.1.txt</c>, <c>.1</c> becomes <c>.2</c> and so on, keeping <see cref="Keep"/> old files.
    /// The live file keeps its name, which is what Report Problem zips and the upload sends.
    /// </summary>
    public sealed class RollingLogWriter
    {
        public const long DefaultMaxBytes = 10L * 1024 * 1024;
        public const int DefaultKeep = 3;
        /// <summary>Lines kept while the file can't be written; the oldest go first.</summary>
        public const int MaxBufferedLines = 20000;

        private readonly object _lock = new object();
        private readonly object _fileLock = new object();
        private readonly Func<string> _path;
        private readonly Timer _timer;
        private readonly TimeSpan _delay;
        private List<string> _pending = new List<string>();
        private bool _scheduled;

        /// <param name="path">The live log file, read on every write (the logs folder can move, e.g. per E2E instance).</param>
        public RollingLogWriter(Func<string> path, long maxBytes = DefaultMaxBytes, int keep = DefaultKeep, TimeSpan? flushDelay = null)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            MaxBytes = Math.Max(1, maxBytes);
            Keep = Math.Max(0, keep);
            _delay = flushDelay ?? TimeSpan.FromSeconds(1);
            _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public long MaxBytes { get; }
        public int Keep { get; }

        /// <summary>Queues a line; it reaches the file on the next flush.</summary>
        public void Write(string line)
        {
            lock (_lock)
            {
                if (_pending.Count >= MaxBufferedLines) _pending.RemoveAt(0);
                _pending.Add(line ?? "");
                if (_scheduled) return;
                _scheduled = true;
            }
            try { _timer.Change(_delay, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
        }

        /// <summary>Appends everything queued now (also called on exit and before the file is read or moved).</summary>
        public void Flush()
        {
            lock (_fileLock)
            {
                List<string> lines;
                lock (_lock)
                {
                    _scheduled = false;
                    if (_pending.Count == 0) return;
                    lines = _pending;
                    _pending = new List<string>();
                }
                try
                {
                    // Inside the try: this runs on a timer thread, where an exception would end the process, and the
                    // app's path getter creates the logs folder (which can throw).
                    string path = _path();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var text = new StringBuilder();
                    foreach (string line in lines) text.Append(line).Append(Environment.NewLine);
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text.ToString());
                    long length;
                    bool creating = !File.Exists(path);
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        length = stream.Length;
                    }
                    // NTFS tunnelling gives a file created under a name renamed away moments ago (a roll, the daily
                    // rotation) the old file's creation time; the rotation reads it, so stamp a new file as new.
                    if (creating)
                    {
                        try { File.SetCreationTime(path, DateTime.Now); } catch (Exception) { }
                    }
                    if (length >= MaxBytes) Roll(path);
                }
                catch (Exception)
                {
                    // The file is locked or the disk is full: keep the lines (up to the cap) for the next try.
                    lock (_lock)
                    {
                        lines.AddRange(_pending);
                        if (lines.Count > MaxBufferedLines) lines.RemoveRange(0, lines.Count - MaxBufferedLines);
                        _pending = lines;
                    }
                }
            }
        }

        /// <summary><c>name.txt</c> → <c>name.1.txt</c> → … → <c>name.{Keep}.txt</c>; the oldest is deleted.</summary>
        private void Roll(string path)
        {
            try
            {
                if (Keep == 0) { File.Delete(path); return; }
                string oldest = RolledPath(path, Keep);
                if (File.Exists(oldest)) File.Delete(oldest);
                for (int i = Keep - 1; i >= 1; i--)
                {
                    string from = RolledPath(path, i);
                    if (File.Exists(from)) File.Move(from, RolledPath(path, i + 1));
                }
                File.Move(path, RolledPath(path, 1));
            }
            catch (Exception)
            {
                // Someone has the file open without sharing delete: try again on the next write.
            }
        }

        public static string RolledPath(string path, int index)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "." + index + Path.GetExtension(path));
        }
    }
}
