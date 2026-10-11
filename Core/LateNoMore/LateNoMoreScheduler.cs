using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.LateNoMore
{
    public enum LateNoMoreAlertKind
    {
        /// <summary>The Mac's 15-minute "starting in 15 minutes — I'll remind you again" notice.</summary>
        HeadsUp,
        /// <summary>The Mac's 2-minute alert: countdown, join link, intention prompts, attend / don't need to attend.</summary>
        Main,
    }

    public sealed class LateNoMoreAlert
    {
        public LateNoMoreAlert(LateNoMoreEvent meeting, int leadMinutes, LateNoMoreAlertKind kind)
        {
            Meeting = meeting;
            LeadMinutes = leadMinutes;
            Kind = kind;
        }

        public LateNoMoreEvent Meeting { get; }
        public int LeadMinutes { get; }
        public LateNoMoreAlertKind Kind { get; }
    }

    /// <summary>
    /// Which meetings alert, and when — the Mac Late No More app's <c>EventWarningManager.checkEvents</c> (calendars,
    /// hours, 15 / 2 minute warnings) with its JS predecessor's keyword rule (<c>ignoreByEvent</c>: title contains the
    /// keyword, ignoring case), the mobile app's "require a meeting link" option, and Focus Bear's stale-trigger guard
    /// (a meeting that started more than 15 minutes ago, or has ended, never alerts). Pure, so it is unit tested.
    /// </summary>
    public static class LateNoMoreScheduler
    {
        /// <summary>Focus Bear ignores an about-to-start trigger for a meeting that began longer ago than this.</summary>
        public static readonly TimeSpan StaleAfterStart = TimeSpan.FromMinutes(15);

        public enum Skip
        {
            None,
            Disabled,
            Declined,
            Over,
            Calendar,
            Keyword,
            OutsideHours,
            NoMeetingLink,
            Dismissed,
        }

        /// <summary>Why a meeting wouldn't alert (ignoring timing), or <see cref="Skip.None"/>.</summary>
        public static Skip WhySkipped(LateNoMoreConfig config, LateNoMoreEvent e, DateTime now, LateNoMoreDismissals dismissals = null)
        {
            if (config == null || !config.Enabled) return Skip.Disabled;
            if (e == null) return Skip.Over;
            if (e.IsDeclined) return Skip.Declined;
            if (e.End <= now || now - e.Start > StaleAfterStart) return Skip.Over;
            // An event with no calendar id can't be matched to a switched-off calendar: it alerts.
            if (e.CalendarId != null && !config.IsCalendarOn(e.CalendarId)) return Skip.Calendar;
            if (MatchedKeyword(config, e.Title) != null) return Skip.Keyword;
            if (!config.IsWithinActiveHours(e.Start)) return Skip.OutsideHours;
            if (config.RequireMeetingUrl && string.IsNullOrEmpty(e.MeetingUrl)) return Skip.NoMeetingLink;
            if (dismissals != null && dismissals.IsDismissed(e.AsMeeting(), now)) return Skip.Dismissed;
            return Skip.None;
        }

        public static string MatchedKeyword(LateNoMoreConfig config, string title)
        {
            if (config?.IgnoredKeywords == null || string.IsNullOrEmpty(title)) return null;
            return config.IgnoredKeywords.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k)
                && CultureInfo.InvariantCulture.CompareInfo.IndexOf(title, k.Trim(), CompareOptions.IgnoreCase) >= 0);
        }

        public static string FiredKey(LateNoMoreEvent e, int lead) => e.Key + "#" + lead.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The alerts due at <paramref name="now"/>. For each meeting only the latest stage that has come due is
        /// returned (a missed heads-up is never shown after the main alert's time), and a stage already in
        /// <paramref name="fired"/> is never returned again. The caller adds <see cref="StagesToMarkFired"/> to it.
        /// </summary>
        public static List<LateNoMoreAlert> DueAlerts(LateNoMoreConfig config, IEnumerable<LateNoMoreEvent> events, DateTime now,
            ISet<string> fired, LateNoMoreDismissals dismissals = null)
        {
            var result = new List<LateNoMoreAlert>();
            if (config == null || !config.Enabled || events == null) return result;
            var leads = (config.AlertLeadMinutes ?? new List<int>()).Distinct().ToList();
            if (leads.Count == 0) return result;
            int main = leads.Min();
            var seen = new HashSet<string>();
            foreach (LateNoMoreEvent e in events.OrderBy(x => x.Start))
            {
                if (!seen.Add(e.Key)) continue;
                if (WhySkipped(config, e, now, dismissals) != Skip.None) continue;
                var due = leads.Where(l => now >= e.Start.AddMinutes(-l)).ToList();
                if (due.Count == 0) continue;
                int stage = due.Min();
                if (fired != null && fired.Contains(FiredKey(e, stage))) continue;
                result.Add(new LateNoMoreAlert(e, stage, stage == main ? LateNoMoreAlertKind.Main : LateNoMoreAlertKind.HeadsUp));
            }
            return result;
        }

        /// <summary>The stage shown plus every earlier (larger) one, so a skipped heads-up doesn't pop up afterwards.</summary>
        public static IEnumerable<string> StagesToMarkFired(LateNoMoreConfig config, LateNoMoreAlert alert) =>
            (config?.AlertLeadMinutes ?? new List<int>()).Where(l => l >= alert.LeadMinutes).Select(l => FiredKey(alert.Meeting, l));

        /// <summary>The next meeting that will alert (for the settings screen's status line), or null.</summary>
        public static LateNoMoreEvent NextAlerting(LateNoMoreConfig config, IEnumerable<LateNoMoreEvent> events, DateTime now, LateNoMoreDismissals dismissals = null) =>
            (events ?? Enumerable.Empty<LateNoMoreEvent>()).Where(e => e.Start > now && WhySkipped(config, e, now, dismissals) == Skip.None)
                .OrderBy(e => e.Start).FirstOrDefault();
    }

    /// <summary>The alert window's countdown line (Mac <c>customAlertVc.updateCountdown</c>), in plainer English.</summary>
    public static class LateNoMoreCountdown
    {
        public static string Text(TimeSpan untilStart, out bool late)
        {
            late = untilStart < TimeSpan.Zero;
            long total = (long)Math.Floor(Math.Abs(untilStart.TotalSeconds));
            if (!late && untilStart.TotalSeconds > 0 && untilStart.TotalSeconds < 1) total = 0;
            string clock = (total / 60).ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
            if (late) return "Your meeting started " + clock + " ago";
            if (total == 0) return "Your meeting is starting now";
            return "Your meeting starts in " + clock;
        }

        /// <summary>Whole minutes left for the heads-up notice ("starting in 15 minutes"), never below one.</summary>
        public static int MinutesLeft(DateTime start, DateTime now) => Math.Max(1, (int)Math.Ceiling((start - now).TotalMinutes));
    }

    /// <summary>
    /// The focus session Late No More starts for a meeting (Mac <c>startMeetingFocusSession</c>): the meeting title as
    /// the intention, for the meeting's remaining time, clamped to 1..240 minutes, on the Meetings block list. The
    /// intention is also what marks it: <see cref="Postpone.PostponeFocusSession.IsMeetingIntention"/> recognises it
    /// (here, and on other devices, which receive the intention), and the pause / stop rules key off that.
    /// </summary>
    public static class LateNoMoreSession
    {
        public const int MaxMinutes = 240;

        public static string Goal(string title)
        {
            string trimmed = (title ?? "").Trim();
            return trimmed.Length == 0
                ? Postpone.PostponeFocusSession.UntitledMeetingIntention
                : Postpone.PostponeFocusSession.MeetingIntentionPrefix + ": " + trimmed;
        }

        /// <summary>
        /// Whether the running session's intention is the one Late No More gives this meeting, i.e. the session it
        /// started for it ("I don't need to attend" ends that session, as on the Mac, and never anything else).
        /// </summary>
        public static bool IsSessionFor(string runningIntention, string meetingTitle) =>
            runningIntention != null && string.Equals(runningIntention.Trim(), Goal(meetingTitle), StringComparison.OrdinalIgnoreCase);

        /// <summary>Minutes to run for a meeting ending at <paramref name="end"/>; 0 when it is already over.</summary>
        public static int Minutes(DateTime end, DateTime now)
        {
            double seconds = (end - now).TotalSeconds;
            if (seconds <= 0) return 0;
            return Math.Min(Math.Max((int)(seconds / 60), 1), MaxMinutes);
        }
    }
}
