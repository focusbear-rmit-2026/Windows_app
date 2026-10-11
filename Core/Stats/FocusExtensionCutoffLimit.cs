using System;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// How many extra minutes a focus session may be granted when the user has a cutoff time.
    ///
    /// The bug behind it (report 2026-09-17_17-05-10, cutoff 17:00): the app force-ends a postpone the
    /// moment the cutoff arrives, so minutes reaching past the cutoff were always a promise the app
    /// would break — the buddy approved ten minutes at 17:00:23 and the session ran "until 05:10 pm".
    /// Clamping here is therefore an honesty fix as much as a strictness one, and it applies to strict
    /// and non-strict users alike.
    ///
    /// Extra focus minutes are added to the EXISTING session end, not to the moment of approval:
    /// unlike routine meeting coverage, a session already running past the cutoff is not a floor that
    /// authorises yet more time.
    /// </summary>
    public static class FocusExtensionCutoffLimit
    {
        private const double SecondsPerMinute = 60.0;

        /// <param name="requested">Minutes the user picked, or the buddy's approved duration.</param>
        /// <param name="now">The moment the extension is being granted.</param>
        /// <param name="currentEnd">
        /// End of the session being extended, or null when a completed session is being restarted
        /// from now.
        /// </param>
        /// <param name="cutoff">Today's cutoff moment, or null when the user has none configured.</param>
        /// <param name="isAfterCutoff">
        /// Whether we are already past the cutoff. Passed in rather than derived from
        /// <paramref name="cutoff"/> because the window wraps overnight: at 03:00 with a 17:00 cutoff
        /// we are after YESTERDAY's cutoff even though today's cutoff moment is still hours away.
        /// </param>
        /// <param name="isCutoffStrict">
        /// The user's "require a password to postpone the evening routine after the cutoff" setting —
        /// their declaration that the after-cutoff window is the strict one.
        /// </param>
        /// <param name="protectsRoutine">
        /// Whether this session stands between the user and a routine (a routine postpone to recover,
        /// or a non-meeting buddy session). A focus session that protects nothing is not the cutoff's
        /// business.
        /// </param>
        public static int GrantedMinutes(int requested, DateTime now, DateTime? currentEnd, DateTime? cutoff,
                                         bool isAfterCutoff, bool isCutoffStrict, bool protectsRoutine)
        {
            if (requested <= 0)
            {
                return 0;
            }

            if (!protectsRoutine || !cutoff.HasValue)
            {
                return requested;
            }

            if (isAfterCutoff)
            {
                return isCutoffStrict ? 0 : requested;
            }

            DateTime measuredFrom = Max(now, currentEnd ?? now);
            int available = Math.Max(0, (int)Math.Floor((cutoff.Value - measuredFrom).TotalSeconds / SecondsPerMinute));
            return Math.Min(requested, available);
        }

        private static DateTime Max(DateTime first, DateTime second)
        {
            return first > second ? first : second;
        }
    }
}
