using System;

namespace FocusBear.Core
{
    /// <summary>
    /// Lets a poller skip a file it already found empty until the file changes. The 5 s background tick read the
    /// offline-activity queue on a thread-pool task every time, although it is empty nearly always; now it is read
    /// again only when its size or last write time differ from the empty read.
    /// </summary>
    public sealed class UnchangedFileGate
    {
        private readonly object _lock = new object();
        private long? _length;
        private DateTime? _lastWriteUtc;
        private bool _known;

        /// <param name="length">The file's size, or null when it doesn't exist.</param>
        public bool ShouldRead(long? length, DateTime? lastWriteUtc)
        {
            lock (_lock) return !_known || _length != length || _lastWriteUtc != lastWriteUtc;
        }

        /// <summary>The file, as it was when this read found nothing to do.</summary>
        public void RememberEmpty(long? length, DateTime? lastWriteUtc)
        {
            lock (_lock) { _known = true; _length = length; _lastWriteUtc = lastWriteUtc; }
        }

        public void Forget()
        {
            lock (_lock) _known = false;
        }
    }
}
