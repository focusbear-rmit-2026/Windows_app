using System;
using System.Collections.Generic;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// How often the routine's pep talk appears (the Mac's <c>PepTalkRoutineDisplay</c>, #2617). It lives on the
    /// routine's "visualize" screen, which comes round again after every habit, so the pep talk was requested
    /// again and again (26 times in one day on the Mac) and used up the motivational-summary rate limit. The
    /// intended behaviour is once per routine start per day: once in the morning, once in the evening. Keyed by
    /// routine AND day, so postponing and resuming the morning routine does not earn a second one.
    /// </summary>
    public sealed class PepTalkRoutineDisplay
    {
        private readonly Dictionary<string, DateTime> _lastShown = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        /// <summary>The app-wide record: the screen it lives on is rebuilt per habit, so instance state can't hold it.</summary>
        public static PepTalkRoutineDisplay Shared { get; } = new PepTalkRoutineDisplay();

        public bool ShouldShow(string routine, DateTime now)
        {
            if (string.IsNullOrEmpty(routine)) return false;
            return !_lastShown.TryGetValue(routine, out DateTime shown) || shown.Date != now.Date;
        }

        public void Record(string routine, DateTime now)
        {
            if (!string.IsNullOrEmpty(routine)) _lastShown[routine] = now;
        }
    }
}
