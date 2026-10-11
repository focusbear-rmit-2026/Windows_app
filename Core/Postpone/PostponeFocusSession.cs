using System;

namespace FocusBear.Core.Postpone
{
    /// <summary>Something that would pause or end the running focus session.</summary>
    public enum FocusSessionControl
    {
        /// <summary>Pause / resume, from the tray, the focus screen, the Focus Button or a voice command.</summary>
        Pause,
        /// <summary>Stop / End Session, from any of the same places.</summary>
        Stop,
        /// <summary>A focus-mode-finished event from another device.</summary>
        RemoteFinish,
    }

    /// <summary>What the app does with a <see cref="FocusSessionControl"/>.</summary>
    public enum PostponeSessionResponse
    {
        /// <summary>The usual handling.</summary>
        Proceed,
        /// <summary>Refused: the session keeps running and the full focus screen comes back to the front.</summary>
        KeepRunning,
        /// <summary>End the session quietly (no completion screen) and bring the postponed routine back now.</summary>
        EndAndResumeRoutine,
    }

    /// <summary>Who started the running focus session, as far as the pause / stop rules care.</summary>
    public enum AutoStartedSessionKind
    {
        /// <summary>No session, or one the user started themselves.</summary>
        None,
        /// <summary>The session an accountability-buddy approval started to cover a postponed routine.</summary>
        BuddyPostpone,
        /// <summary>A Late No More meeting session ("attend and stay focused in your meeting: …").</summary>
        Meeting,
    }

    /// <summary>
    /// The rules for focus sessions the app starts for the user rather than the user starting them: the
    /// accountability buddy's approved-postpone session, and Late No More meeting sessions.
    ///
    /// Neither can be paused. While a routine is postponed, Stop isn't a way out of either: it ends the session
    /// quietly and hands the user straight back to their habits, full screen (the Mac's
    /// <c>stopFocusModeAndResumeRoutine</c>, #2398; a Mac meeting session that starts over a postponed routine
    /// runs super-strict for the same reason, <c>meetingIsProtectingARoutine</c>). With nothing postponed, Stop
    /// works as usual. Another device can't end the buddy session (#2640); a meeting session is usually mirrored
    /// from the device that started it, so that device's finish still applies. When the postponed time runs out,
    /// the postpone timer resumes the routine as for any postpone.
    ///
    /// The buddy session is recognised by its temporary focus mode, whose id starts with <see cref="ModeIdPrefix"/>;
    /// a meeting session by the intention Late No More gives it, which is also what reaches other devices.
    /// </summary>
    public static class PostponeFocusSession
    {
        public const string ModeIdPrefix = "ai-buddy-";

        /// <summary>The Mac's <c>startMeetingFocusSession</c> goal: "attend and stay focused in your meeting: {title}".</summary>
        public const string MeetingIntentionPrefix = "attend and stay focused in your meeting";

        /// <summary>The same goal for a meeting with no title.</summary>
        public const string UntitledMeetingIntention = "your meeting";

        public static bool IsPostponeMode(string focusModeId) =>
            focusModeId != null && focusModeId.StartsWith(ModeIdPrefix, StringComparison.Ordinal);

        public static bool IsMeetingIntention(string intention)
        {
            string text = (intention ?? "").Trim();
            return text.StartsWith(MeetingIntentionPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, UntitledMeetingIntention, StringComparison.OrdinalIgnoreCase);
        }

        public static AutoStartedSessionKind Classify(bool isFocusModeRunning, string runningFocusModeId, string intention)
        {
            if (!isFocusModeRunning) return AutoStartedSessionKind.None;
            if (IsPostponeMode(runningFocusModeId)) return AutoStartedSessionKind.BuddyPostpone;
            return IsMeetingIntention(intention) ? AutoStartedSessionKind.Meeting : AutoStartedSessionKind.None;
        }

        /// <summary>No Pause for either kind of auto-started session.</summary>
        public static bool IsUnpausable(AutoStartedSessionKind kind) => kind != AutoStartedSessionKind.None;

        /// <summary>Stop means "back to my habits": an auto-started session while a routine is postponed.</summary>
        public static bool StopResumesRoutine(AutoStartedSessionKind kind, bool routineIsPostponed) =>
            kind != AutoStartedSessionKind.None && routineIsPostponed;

        /// <summary>
        /// Whether the session runs super strict (the Mac's <c>startScopedAiFocusSession(superStrict:)</c>): the buddy's
        /// postpone session always, a meeting session only while it covers a postponed routine. Super strict locks the
        /// goal and taking time off, so -5 can't whittle the session covering a postponed routine down to a minute.
        /// </summary>
        public static bool RunsSuperStrict(AutoStartedSessionKind kind, bool routineIsPostponed) =>
            kind == AutoStartedSessionKind.BuddyPostpone || (kind == AutoStartedSessionKind.Meeting && routineIsPostponed);

        /// <summary>True while a buddy-approved postpone session is the running focus session.</summary>
        public static bool IsLocked(bool isFocusModeRunning, string runningFocusModeId) =>
            isFocusModeRunning && IsPostponeMode(runningFocusModeId);

        /// <param name="isPaused">Whether the session is paused (only possible if it was paused before these rules applied).</param>
        /// <param name="routineIsPostponed">Whether a postponed routine (habits or the focus summary) is waiting.</param>
        public static PostponeSessionResponse Decide(FocusSessionControl control, AutoStartedSessionKind kind, bool isPaused, bool routineIsPostponed)
        {
            if (kind == AutoStartedSessionKind.None) return PostponeSessionResponse.Proceed;
            switch (control)
            {
                case FocusSessionControl.Pause:
                    // Resuming is always allowed; pausing never is.
                    return isPaused ? PostponeSessionResponse.Proceed : PostponeSessionResponse.KeepRunning;
                case FocusSessionControl.Stop:
                    return StopResumesRoutine(kind, routineIsPostponed) ? PostponeSessionResponse.EndAndResumeRoutine : PostponeSessionResponse.Proceed;
                case FocusSessionControl.RemoteFinish:
                    return kind == AutoStartedSessionKind.BuddyPostpone ? PostponeSessionResponse.KeepRunning : PostponeSessionResponse.Proceed;
                default:
                    return PostponeSessionResponse.Proceed;
            }
        }
    }
}
