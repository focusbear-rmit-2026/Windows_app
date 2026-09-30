using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.LateNoMore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.LateNoMore
{
    [TestClass]
    public class LateNoMoreSchedulerTests
    {
        private static readonly DateTime Ten = new DateTime(2026, 9, 26, 10, 0, 0);

        private static LateNoMoreEvent Meeting(string title = "Standup", DateTime? start = null, int minutes = 30, string calendar = "primary",
            string url = "https://meet.google.com/abc-defg-hij", bool declined = false, string id = "e1") =>
            new LateNoMoreEvent(id, title, start ?? Ten, (start ?? Ten).AddMinutes(minutes), calendar, "google", url, declined);

        private static List<LateNoMoreAlert> Due(LateNoMoreConfig c, DateTime now, ISet<string> fired, params LateNoMoreEvent[] events) =>
            LateNoMoreScheduler.DueAlerts(c, events, now, fired);

        [TestMethod]
        public void TheHeadsUpThenTheMainAlertEachFireOnce()
        {
            var c = LateNoMoreConfig.Defaults();
            var fired = new HashSet<string>();
            var e = Meeting();

            Assert.AreEqual(0, Due(c, Ten.AddMinutes(-16), fired, e).Count, "nothing before the heads-up");

            var headsUp = Due(c, Ten.AddMinutes(-15), fired, e).Single();
            Assert.AreEqual(LateNoMoreAlertKind.HeadsUp, headsUp.Kind);
            Assert.AreEqual(15, headsUp.LeadMinutes);
            foreach (var k in LateNoMoreScheduler.StagesToMarkFired(c, headsUp)) fired.Add(k);
            Assert.AreEqual(0, Due(c, Ten.AddMinutes(-10), fired, e).Count, "the heads-up fires once");

            var main = Due(c, Ten.AddMinutes(-2), fired, e).Single();
            Assert.AreEqual(LateNoMoreAlertKind.Main, main.Kind);
            foreach (var k in LateNoMoreScheduler.StagesToMarkFired(c, main)) fired.Add(k);
            Assert.AreEqual(0, Due(c, Ten, fired, e).Count, "and the main alert once");
        }

        [TestMethod]
        public void AMissedHeadsUpIsNeverShownAfterTheMainAlert()
        {
            var c = LateNoMoreConfig.Defaults();
            var fired = new HashSet<string>();
            // The app started 90 s before the meeting: straight to the main alert.
            var alert = Due(c, Ten.AddSeconds(-90), fired, Meeting()).Single();
            Assert.AreEqual(LateNoMoreAlertKind.Main, alert.Kind);
            foreach (var k in LateNoMoreScheduler.StagesToMarkFired(c, alert)) fired.Add(k);
            Assert.IsTrue(fired.Contains(LateNoMoreScheduler.FiredKey(Meeting(), 15)), "the heads-up counts as done too");
        }

        [TestMethod]
        public void AMeetingInProgressStillAlertsUntilItIsStale()
        {
            var c = LateNoMoreConfig.Defaults();
            Assert.AreEqual(1, Due(c, Ten.AddMinutes(10), new HashSet<string>(), Meeting()).Count, "started 10 minutes ago: still worth a nudge");
            Assert.AreEqual(0, Due(c, Ten.AddMinutes(16), new HashSet<string>(), Meeting(minutes: 60)).Count, "started over 15 minutes ago: stale, as on the Mac");
            Assert.AreEqual(0, Due(c, Ten.AddMinutes(5), new HashSet<string>(), Meeting(minutes: 5)).Count, "already ended");
        }

        [TestMethod]
        public void FiltersMatchTheSettings()
        {
            var now = Ten.AddMinutes(-2);
            var c = LateNoMoreConfig.Defaults();
            Assert.AreEqual(LateNoMoreScheduler.Skip.None, LateNoMoreScheduler.WhySkipped(c, Meeting(), now));
            Assert.AreEqual(LateNoMoreScheduler.Skip.Keyword, LateNoMoreScheduler.WhySkipped(c, Meeting("Deep work [NO-NOTIFY]"), now), "title contains the keyword, any case");
            Assert.AreEqual(LateNoMoreScheduler.Skip.Declined, LateNoMoreScheduler.WhySkipped(c, Meeting(declined: true), now));

            c.SetCalendarOn("primary", false, new[] { "primary", "team" });
            Assert.AreEqual(LateNoMoreScheduler.Skip.Calendar, LateNoMoreScheduler.WhySkipped(c, Meeting(), now));
            Assert.AreEqual(LateNoMoreScheduler.Skip.None, LateNoMoreScheduler.WhySkipped(c, Meeting(calendar: "team"), now));
            Assert.AreEqual(LateNoMoreScheduler.Skip.None, LateNoMoreScheduler.WhySkipped(c, Meeting(calendar: null), now), "an unknown calendar alerts");

            c = LateNoMoreConfig.Defaults();
            var early = Ten.Date.AddHours(6).AddMinutes(30);
            Assert.AreEqual(LateNoMoreScheduler.Skip.OutsideHours, LateNoMoreScheduler.WhySkipped(c, Meeting(start: early), early.AddMinutes(-2)));

            c.RequireMeetingUrl = true;
            Assert.AreEqual(LateNoMoreScheduler.Skip.NoMeetingLink, LateNoMoreScheduler.WhySkipped(c, Meeting(url: null), now));

            c.Enabled = false;
            Assert.AreEqual(LateNoMoreScheduler.Skip.Disabled, LateNoMoreScheduler.WhySkipped(c, Meeting(), now));
            Assert.AreEqual(0, Due(c, now, new HashSet<string>(), Meeting()).Count);
        }

        [TestMethod]
        public void ADismissedMeetingDoesNotAlert()
        {
            var c = LateNoMoreConfig.Defaults();
            var dismissals = new LateNoMoreDismissals();
            // Dismissed on the phone: a different id, the same title, a start 20 s off.
            dismissals.Record(new LateNoMoreMeeting("google-xyz", "standup ", Ten.AddSeconds(20)), Ten.AddMinutes(-20));
            Assert.AreEqual(0, LateNoMoreScheduler.DueAlerts(c, new[] { Meeting() }, Ten.AddMinutes(-2), new HashSet<string>(), dismissals).Count);
        }

        [TestMethod]
        public void AMovedMeetingIsANewMeeting()
        {
            var c = LateNoMoreConfig.Defaults();
            var fired = new HashSet<string>();
            foreach (var a in Due(c, Ten.AddMinutes(-2), fired, Meeting()))
                foreach (var k in LateNoMoreScheduler.StagesToMarkFired(c, a)) fired.Add(k);
            var moved = Meeting(start: Ten.AddHours(1));
            Assert.AreEqual(1, Due(c, Ten.AddMinutes(58), fired, moved).Count);
        }

        [TestMethod]
        public void BackToBackMeetingsEachAlert()
        {
            var c = LateNoMoreConfig.Defaults();
            var a = Meeting("Lunch", Ten, 15, id: "a");
            var b = Meeting("UX checkin", Ten.AddMinutes(15), 20, id: "b");
            var due = Due(c, Ten.AddMinutes(13), new HashSet<string>(), a, b);
            CollectionAssert.AreEqual(new[] { "Lunch", "UX checkin" }, due.Select(x => x.Meeting.Title).ToList());
        }

        [TestMethod]
        public void NextAlertingSkipsFilteredMeetings()
        {
            var c = LateNoMoreConfig.Defaults();
            var next = LateNoMoreScheduler.NextAlerting(c, new[] { Meeting("no-notify: gym", Ten, id: "x"), Meeting("Retro", Ten.AddHours(1), id: "y") }, Ten.AddMinutes(-30));
            Assert.AreEqual("Retro", next.Title);
        }

        [TestMethod]
        public void CountdownReadsLikeTheMacAlert()
        {
            Assert.AreEqual("Your meeting starts in 2:00", LateNoMoreCountdown.Text(TimeSpan.FromMinutes(2), out bool late));
            Assert.IsFalse(late);
            Assert.AreEqual("Your meeting starts in 0:09", LateNoMoreCountdown.Text(TimeSpan.FromSeconds(9.6), out _));
            Assert.AreEqual("Your meeting is starting now", LateNoMoreCountdown.Text(TimeSpan.FromMilliseconds(300), out late));
            Assert.IsFalse(late);
            Assert.AreEqual("Your meeting started 1:05 ago", LateNoMoreCountdown.Text(TimeSpan.FromSeconds(-65), out late));
            Assert.IsTrue(late, "late turns the countdown red");
            Assert.AreEqual(15, LateNoMoreCountdown.MinutesLeft(Ten, Ten.AddMinutes(-14.5)));
            Assert.AreEqual(1, LateNoMoreCountdown.MinutesLeft(Ten, Ten.AddMinutes(1)));
        }
    }

    [TestClass]
    public class LateNoMoreSessionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 26, 10, 0, 0);

        [TestMethod]
        public void TheGoalIsTheMacMeetingIntention()
        {
            Assert.AreEqual("attend and stay focused in your meeting: Standup", LateNoMoreSession.Goal("  Standup "));
            Assert.AreEqual("your meeting", LateNoMoreSession.Goal("   "));
            Assert.IsTrue(FocusBear.Core.Postpone.PostponeFocusSession.IsMeetingIntention(LateNoMoreSession.Goal("Retro")),
                "the intention is what the pause / stop rules recognise a meeting session by");
            Assert.IsTrue(FocusBear.Core.Postpone.PostponeFocusSession.IsMeetingIntention(LateNoMoreSession.Goal(null)));
        }

        [TestMethod]
        public void MinutesCoverTheRestOfTheMeetingClamped()
        {
            Assert.AreEqual(29, LateNoMoreSession.Minutes(Now.AddMinutes(29.9), Now));
            Assert.AreEqual(1, LateNoMoreSession.Minutes(Now.AddSeconds(20), Now));
            Assert.AreEqual(240, LateNoMoreSession.Minutes(Now.AddHours(6), Now));
            Assert.AreEqual(0, LateNoMoreSession.Minutes(Now.AddSeconds(-1), Now), "a meeting that's over starts nothing");
        }

        [TestMethod]
        public void IDontNeedToAttendEndsOnlyThatMeetingsSession()
        {
            // Mac btnBackToHabitsClicked: "I don't need to attend" cancels the session the alert auto-started.
            string running = LateNoMoreSession.Goal("Standup");
            Assert.IsTrue(LateNoMoreSession.IsSessionFor(running, " Standup"));
            Assert.IsTrue(LateNoMoreSession.IsSessionFor("your meeting", ""), "an untitled meeting's session");
            Assert.IsFalse(LateNoMoreSession.IsSessionFor(running, "Retro"), "another meeting's session keeps running");
            Assert.IsFalse(LateNoMoreSession.IsSessionFor("write the report", "Standup"), "a session the user started is never touched");
            Assert.IsFalse(LateNoMoreSession.IsSessionFor(null, "Standup"));
        }
    }
}
