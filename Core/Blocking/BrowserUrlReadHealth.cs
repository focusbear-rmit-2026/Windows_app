using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Whether the "what page is open?" reads that every website check depends on are still
    /// answering. Ported from the Mac's <c>BrowserUrlReadHealth</c>.
    ///
    /// Report 2026-09-11_16-56-33 ("it's not blocking anything"): the read started returning nothing
    /// after the screen locked and kept doing so for three hours. Nothing was logged — the read ran
    /// every second, no URL ever came back, so the blocking check was never reached and every site
    /// stayed open, while the menu's own self-check still reported no blocking issue.
    ///
    /// A single failed read is normal (a window closing, a slow tab, one timed-out script), so only a
    /// RUN of failures counts, and the verdict is reported once per transition rather than per tick.
    /// </summary>
    public struct BrowserUrlReadHealth
    {
        /// <summary>
        /// Consecutive failures before the reads are called broken. At one read per second, a handful
        /// of ticks is long enough to rule out a single slow answer.
        /// </summary>
        public const int FailureThreshold = 8;

        public int ConsecutiveFailures { get; private set; }
        public bool IsFailing { get; private set; }
        public DateTime? FailingSince { get; private set; }

        /// <summary>
        /// Records one read. Returns true when THIS read changed the verdict, so the caller logs the
        /// transition instead of every tick.
        /// </summary>
        public bool Record(bool succeeded, DateTime now)
        {
            if (succeeded)
            {
                ConsecutiveFailures = 0;
                if (!IsFailing) return false;
                IsFailing = false;
                FailingSince = null;
                return true;
            }

            ConsecutiveFailures++;
            if (IsFailing || ConsecutiveFailures < FailureThreshold) return false;
            IsFailing = true;
            FailingSince = now;
            return true;
        }
    }
}
