using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan's calendar cache against a fake backend answering in the real wire shapes. On 26 Sep a
    /// network blip made calendar-datas fail, the refresh read it as "no calendars connected", and today's and
    /// tomorrow's meetings vanished from the Day Plan and from Late No More until the next refresh.
    /// </summary>
    [TestClass]
    public class DayPlanCalendarSeamTests
    {
        private sealed class FakeCalendarApi : IDayPlanCalendarApi
        {
            public bool Offline;
            public readonly List<string> EventReads = new List<string>();
            public readonly List<string> Meetings = new List<string>();

            public Task<string> GetCalendarDatas(string platform)
            {
                if (Offline) return Task.FromResult<string>(null);
                return Task.FromResult(platform == "google"
                    ? "{\"calendars\":[{\"account\":\"me@example.com\",\"requires_reauth\":false,\"items\":[{\"calendar_id\":\"primary\",\"summary\":\"Me\",\"is_selected\":true}]}],\"keywords\":[]}"
                    : "{\"calendars\":[],\"keywords\":[]}");
            }

            public Task<string> GetCalendarEvents(string platform, string account, string range = null)
            {
                EventReads.Add(range);
                if (Offline) return Task.FromResult<string>(null);
                return Task.FromResult("[" + string.Join(",", Meetings) + "]");
            }

            public Task<string> GetEventColours() => Task.FromResult(Offline ? null : "[]");
        }

        private static string Meeting(string id, DateTime localStart, int minutes = 30) =>
            "{\"external_id\":\"" + id + "\",\"summary\":\"Standup\",\"calendar_id\":\"primary\"," +
            "\"event_begins\":\"" + localStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "\"," +
            "\"event_ends\":\"" + localStart.AddMinutes(minutes).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "\"}";

        private static DateTime Later => DateTime.Today.AddHours(23);   // today, and never already over by much

        [TestMethod]
        public async Task ANetworkBlipKeepsTheMeetingsAlreadyLoaded()
        {
            var api = new FakeCalendarApi();
            api.Meetings.Add(Meeting("m1", Later));
            var calendar = new DayPlanCalendarManager(api);
            await calendar.RefreshAsync(true, 0, null, 2);
            Assert.AreEqual(1, calendar.MeetingsFor(DateTime.Today, untrimmed: true).Count);

            api.Offline = true;
            await calendar.RefreshAsync(true, 0, null, 2);

            Assert.AreEqual(1, calendar.MeetingsFor(DateTime.Today, untrimmed: true).Count, "the Day Plan keeps today's meeting");
            Assert.AreEqual(1, calendar.LateNoMoreEvents(DateTime.Today, DateTime.Today.AddDays(1)).Count, "Late No More still alerts for it");
        }

        [TestMethod]
        public async Task AFailedReadDoesNotRecordTheDaysAsFreeOrTheWindowAsLoaded()
        {
            var api = new FakeCalendarApi { Offline = true };
            var calendar = new DayPlanCalendarManager(api);

            await calendar.RefreshAsync(true, 0, null, 7);

            Assert.IsFalse(calendar.IsDayLoaded(DateTime.Today), "an unread day is unknown, not meeting-free (date picker)");
            Assert.IsFalse(calendar.IsWindowFresh(7, 600), "EnsureWindowAsync must read again rather than price days from nothing");

            api.Offline = false;
            await calendar.RefreshAsync(true, 0, null, 7);
            Assert.IsTrue(calendar.IsDayLoaded(DateTime.Today));
            Assert.IsTrue(calendar.IsWindowFresh(7, 600));
        }

        [TestMethod]
        public async Task ADeletedMeetingStillGoesOnAFullRead()
        {
            var api = new FakeCalendarApi();
            api.Meetings.Add(Meeting("m1", Later));
            var calendar = new DayPlanCalendarManager(api);
            await calendar.RefreshAsync(true, 0, null, 2);

            api.Meetings.Clear();
            await calendar.RefreshAsync(true, 0, null, 2);

            Assert.AreEqual(0, calendar.MeetingsFor(DateTime.Today, untrimmed: true).Count);
        }

        [TestMethod]
        public async Task TheDatePickersSingleDayReadLeavesADayUnknownWhenCalendarDatasFails()
        {
            var api = new FakeCalendarApi { Offline = true };
            var calendar = new DayPlanCalendarManager(api);
            DateTime farDay = DateTime.Today.AddDays(120);

            List<DateTime> read = await calendar.LoadDaysAsync(new[] { farDay });

            Assert.AreEqual(0, read.Count);
            Assert.IsFalse(calendar.IsDayLoaded(farDay), "would have been badged as a free day");
        }

        [TestMethod]
        public async Task ARefreshSaysChangedOnlyWhenTheDayPlanWouldDrawSomethingNew()
        {
            // The Day Plan re-renders on true, and polls every 90 s: an unchanged calendar must not rebuild it.
            var api = new FakeCalendarApi();
            api.Meetings.Add(Meeting("m1", Later));
            var calendar = new DayPlanCalendarManager(api);

            Assert.IsTrue(await calendar.RefreshAsync(true, 0, null, 2), "first read");
            Assert.IsFalse(await calendar.RefreshAsync(true, 0, null, 2), "same meetings");

            api.Meetings[0] = Meeting("m1", Later, minutes: 45);
            Assert.IsTrue(await calendar.RefreshAsync(true, 0, null, 2), "a meeting moved");

            api.Offline = true;
            Assert.IsFalse(await calendar.RefreshAsync(true, 0, null, 2), "a failed read keeps the plan as it was");
        }

        [TestMethod]
        public async Task ANewJoinLinkOrADismissalSaysChanged()
        {
            // The signature covered only id, start, end, title and colours: a meeting that gained its Meet link, or
            // one dismissed on another device, left the Day Plan (and its join button) as it was.
            var api = new FakeCalendarApi();
            api.Meetings.Add(Meeting("m1", Later));
            var calendar = new DayPlanCalendarManager(api);
            await calendar.RefreshAsync(true, 0, null, 2);

            api.Meetings[0] = Meeting("m1", Later).TrimEnd('}') + ",\"hangoutLink\":\"https://meet.google.com/abc-defg-hij\"}";
            Assert.IsTrue(await calendar.RefreshAsync(true, 0, null, 2), "the join link is new");
            Assert.IsFalse(await calendar.RefreshAsync(true, 0, null, 2));

            Assert.IsTrue(calendar.RenderSignatureChangedBy(() => calendar.SetDismissed(DateTime.Today, new[] { "m1" })), "a dismissal redraws");
        }

        [TestMethod]
        public async Task AMeetingPastMidnightBlocksTheNextMorningToo()
        {
            var api = new FakeCalendarApi();
            api.Meetings.Add(Meeting("late", DateTime.Today.AddHours(23.5), minutes: 60));   // 23:30-00:30
            var calendar = new DayPlanCalendarManager(api);
            await calendar.RefreshAsync(true, 0, null, 2);

            Assert.AreEqual(1, calendar.MeetingsFor(DateTime.Today, untrimmed: true).Count);
            var tomorrow = calendar.MeetingsFor(DateTime.Today.AddDays(1), untrimmed: true);
            Assert.AreEqual(1, tomorrow.Count, "its first half hour is tomorrow's");
            Assert.AreEqual(DateTime.Today.AddDays(1).AddMinutes(30), tomorrow[0].End);
            Assert.AreEqual(1, calendar.LateNoMoreEvents(DateTime.Today, DateTime.Today.AddDays(2)).Count, "Late No More alerts for it once");
        }

        [TestMethod]
        public async Task TheDatePickersDayReadKeepsAMeetingFromTheNightBefore()
        {
            var api = new FakeCalendarApi();
            DateTime far = DateTime.Today.AddDays(120);
            api.Meetings.Add(Meeting("late", far.AddMinutes(-30), minutes: 60));   // the provider returns overlapping events
            var calendar = new DayPlanCalendarManager(api);

            await calendar.LoadDaysAsync(new[] { far });

            Assert.AreEqual(1, calendar.MeetingsFor(far, untrimmed: true).Count);
        }
    }
}
