using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.LateNoMore
{
    /// <summary>
    /// The identity of one meeting reminder, reduced to the fields that survive crossing devices (the Mac's
    /// <c>LateNoMoreMeeting</c>). Ids differ per platform, so title + start time is what the dismissal contract keys on.
    /// </summary>
    public sealed class LateNoMoreMeeting
    {
        public LateNoMoreMeeting(string id, string title, DateTime start)
        {
            Id = id;
            Title = title ?? "";
            Start = start;
        }

        public string Id { get; }
        public string Title { get; }
        public DateTime Start { get; }
    }

    /// <summary>Wire constants of the three-legged dismissal contract (backend#2395, Mac <c>LateNoMoreSyncContract</c>).</summary>
    public static class LateNoMoreSyncContract
    {
        public const string DismissEndpointPath = "notifications/calendar-events/dismiss";
        /// <summary>Pusher Channels event on <c>private-&lt;userId&gt;</c> when another device dismissed a meeting.</summary>
        public const string DismissedPusherEvent = "late-no-more-event-dismissed";
        public const int MaxSummaryLength = 2500;
        public const int MaxDismissReasonLength = 255;
        public const double EventStartMatchToleranceSeconds = 60;

        /// <summary>"I don't need to attend": every device stops reminding (the value mobile sends too).</summary>
        public const string ReasonStopReminders = "stop_reminders";
        /// <summary>Acknowledged and attending here, so other devices needn't nag. The Mac sends "joined_on_mac".</summary>
        public const string ReasonJoinedOnWindows = "joined_on_windows";
    }

    /// <summary>Same-meeting rule shared by Mac, mobile and backend (Mac <c>LateNoMoreEventMatcher</c>).</summary>
    public static class LateNoMoreEventMatcher
    {
        /// <summary>
        /// 1. both carry the same non-empty id; or 2. titles equal once trimmed and case-folded (and not empty) AND the
        /// starts within <see cref="LateNoMoreSyncContract.EventStartMatchToleranceSeconds"/>.
        /// </summary>
        public static bool IsSameMeeting(LateNoMoreMeeting candidate, LateNoMoreMeeting target)
        {
            if (candidate == null || target == null) return false;
            if (!string.IsNullOrEmpty(candidate.Id) && !string.IsNullOrEmpty(target.Id) && candidate.Id == target.Id) return true;
            string t = Normalised(target.Title);
            if (t.Length == 0 || Normalised(candidate.Title) != t) return false;
            return Math.Abs((candidate.Start - target.Start).TotalSeconds) <= LateNoMoreSyncContract.EventStartMatchToleranceSeconds;
        }

        public static LateNoMoreMeeting FirstMatch(IEnumerable<LateNoMoreMeeting> candidates, LateNoMoreMeeting target) =>
            (candidates ?? Enumerable.Empty<LateNoMoreMeeting>()).FirstOrDefault(c => IsSameMeeting(c, target));

        private static string Normalised(string title) => (title ?? "").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Meetings the user already dealt with (here, or on another device via Pusher), so they don't alert again. In
    /// memory, like the Mac's <c>LateNoMoreDismissalRegistry</c>; entries go four hours after the meeting's start.
    /// </summary>
    public sealed class LateNoMoreDismissals
    {
        public static readonly TimeSpan Retention = TimeSpan.FromHours(4);
        private readonly object _lock = new object();
        private readonly List<LateNoMoreMeeting> _dismissed = new List<LateNoMoreMeeting>();

        public void Record(LateNoMoreMeeting meeting, DateTime now)
        {
            if (meeting == null) return;
            lock (_lock)
            {
                Purge(now);
                if (LateNoMoreEventMatcher.FirstMatch(_dismissed, meeting) == null) _dismissed.Add(meeting);
            }
        }

        public bool IsDismissed(LateNoMoreMeeting meeting, DateTime now)
        {
            lock (_lock)
            {
                Purge(now);
                return LateNoMoreEventMatcher.FirstMatch(_dismissed, meeting) != null;
            }
        }

        public int Count { get { lock (_lock) return _dismissed.Count; } }

        private void Purge(DateTime now) => _dismissed.RemoveAll(m => m.Start < now - Retention);
    }
}
