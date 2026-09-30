using System;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// What the user's cutoff time is allowed to say about a routine postpone: how long it may run,
    /// and when it must ask for the password first. Pure date maths, so the rules are testable without
    /// timers, settings or the AI buddy.
    ///
    /// The incident behind it (Mac report 2026-09-17_17-05-10) — cutoff 17:00:
    /// <list type="bullet">
    /// <item>17:00:02 — the app force-ended the running evening postpone because the cutoff arrived.</item>
    /// <item>17:00:07 — the shutdown ran and the Focus Summary appeared.</item>
    /// <item>17:00:12 — "I still need to work more" routed through a branch with NO password check.</item>
    /// <item>17:00:23 — the AI buddy approved 10 minutes; the routine was postponed to 17:10 and a
    /// focus session started "until 05:10 pm" — ten minutes past the cutoff, no password.</item>
    /// </list>
    ///
    /// Two rules come out of that. They answer different questions and apply at different points in
    /// the flow, which is why they are two functions rather than one.
    ///
    /// Not a to-do rule. It is here only because this port's slice is confined to this namespace; it
    /// belongs beside the routine/focus-session logic, next to
    /// <see cref="Stats.FocusExtensionCutoffLimit"/>, and moving it there changes nothing but its file.
    /// </summary>
    public static class CutoffPostponeLimit
    {
        private const double SecondsPerMinute = 60.0;

        /// <summary>
        /// Whether postponing right now needs the password the user asked for.
        ///
        /// <c>isCutoffStrict</c> is the setting "require a password for postponing the evening routine
        /// after the cutoff time". It means ASK, not FORBID: an earlier version of this rule read it as
        /// a declaration that the window was off-limits and refused outright, which locked the user out
        /// of their own escape hatch — the person who turns that setting on is the one who wants a way
        /// through, guarded by a password. The real bug was a postpone granted past the cutoff with no
        /// password asked; asking closes it, refusing overshoots it.
        ///
        /// The Mac still calls this member <c>isRefused</c>, from when it did refuse. The predicate is
        /// identical; only what the caller does with the answer changed.
        /// </summary>
        /// <param name="hasCutoff">The user has a cutoff time configured at all.</param>
        /// <param name="isAfterCutoff">We are already in the after-cutoff window.</param>
        /// <param name="isCutoffStrict">The after-cutoff postpone-password setting.</param>
        public static bool RequiresPassword(bool hasCutoff, bool isAfterCutoff, bool isCutoffStrict)
        {
            return hasCutoff && isAfterCutoff && isCutoffStrict;
        }

        /// <summary>
        /// How many minutes a postpone asked for now may actually run.
        ///
        /// The ceiling is the cutoff. This is an honesty fix as much as a strictness one: the app
        /// ALREADY abandons a running postpone the moment the cutoff arrives, so minutes that reached
        /// past it were always a promise the app would break. It therefore applies whenever a cutoff is
        /// configured, for strict and non-strict users alike.
        /// </summary>
        /// <param name="requestedMinutes">The user's picked minutes, or the AI buddy's approved duration.</param>
        /// <param name="now">The moment the postpone is being granted.</param>
        /// <param name="cutoff">Today's cutoff moment, or null when none is configured.</param>
        /// <param name="isAfterCutoff">
        /// Whether we are already past the cutoff. Passed in rather than derived from
        /// <paramref name="cutoff"/> because the window wraps overnight: at 03:00 with a 17:00 cutoff
        /// we are after YESTERDAY's cutoff even though today's cutoff moment is still hours away. Past
        /// the cutoff there is nothing to clamp to, so the request stands — the password rule owns that
        /// case, and a silently zero-minute postpone would be worse than either answer.
        /// </param>
        /// <param name="currentPostponeEnd">The end of the postpone already running, if any.</param>
        public static int GrantedMinutes(int requestedMinutes, DateTime now, DateTime? cutoff,
                                         bool isAfterCutoff, DateTime? currentPostponeEnd)
        {
            int? ceiling = MinutesUntilCutoff(now, cutoff, isAfterCutoff);
            int clamped = Math.Min(requestedMinutes, ceiling ?? requestedMinutes);
            return FlooredByRunningPostpone(clamped, currentPostponeEnd, now);
        }

        /// <summary>
        /// Whole minutes between <paramref name="now"/> and the cutoff, or null when no cutoff lies
        /// ahead — none is configured, or it has passed and there is nothing left to clamp to.
        ///
        /// Also the ceiling the postpone dialog trims its minutes dropdown to, so the user is never
        /// offered a length the grant would silently shorten. Less than a whole minute left is no room
        /// at all rather than a zero-minute postpone.
        /// </summary>
        public static int? MinutesUntilCutoff(DateTime now, DateTime? cutoff, bool isAfterCutoff)
        {
            if (!cutoff.HasValue || isAfterCutoff) return null;

            // Rounded DOWN: rounding up would end the postpone after the cutoff it is clamped to.
            int minutes = (int)((cutoff.Value - now).TotalSeconds / SecondsPerMinute);
            return minutes > 0 ? minutes : (int?)null;
        }

        /// <summary>
        /// A postpone must never come out SHORTER than the one already running — the meeting-coverage
        /// rule, which exists so a Late No More popup closing at its own meeting's end cannot hand the
        /// user back to their habits mid-call.
        ///
        /// So when the cutoff is nearer than a running postpone's end, the running postpone wins and
        /// the clamp does not cut it short. That is safe: the app abandons the running postpone at the
        /// cutoff anyway, so the cutoff is still enforced — by the mechanism that owns it — while the
        /// clamp keeps to its own job of not pushing the deadline out further.
        /// </summary>
        private static int FlooredByRunningPostpone(int minutes, DateTime? currentPostponeEnd, DateTime now)
        {
            if (!currentPostponeEnd.HasValue) return minutes;

            int runningMinutes = (int)Math.Ceiling((currentPostponeEnd.Value - now).TotalSeconds / SecondsPerMinute);
            return Math.Max(minutes, runningMinutes);
        }
    }
}
