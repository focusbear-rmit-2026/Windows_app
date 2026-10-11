using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>'s <c>MeetingsCacheTests</c>: the staleness policy that
    /// keeps a blocking calendar query off the UI thread — served from cache, refreshed in the
    /// background, never dropped.
    /// </summary>
    [TestClass]
    public class MeetingsCacheTests
    {
        private static readonly DateTimeOffset Today =
            new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(10));

        private static MeetingsCache.Key Key(DateTimeOffset? day = null, bool includeDismissed = false)
        {
            return new MeetingsCache.Key(day ?? Today, includeDismissed);
        }

        private static ScheduleMeetingInput Meeting(string id, int hour)
        {
            DateTimeOffset start = Today.AddHours(hour);
            return new ScheduleMeetingInput(id, "📅 " + id, start, start.AddMinutes(30));
        }

        [TestMethod]
        public void FirstLookupServesNothingAndAsksForARefresh()
        {
            MeetingsCache cache = new MeetingsCache();

            MeetingsCache.Lookup lookup = cache.Get(Key());

            Assert.AreEqual(0, lookup.Meetings.Count);
            Assert.IsTrue(lookup.NeedsRefresh);
        }

        [TestMethod]
        public void CachedDayIsServedWithoutAnotherFetch()
        {
            MeetingsCache cache = new MeetingsCache();
            var stored = new List<ScheduleMeetingInput> { Meeting("standup", 9) };
            cache.Store(stored, Key());

            MeetingsCache.Lookup lookup = cache.Get(Key());

            CollectionAssert.AreEqual(stored, lookup.Meetings.ToList());
            Assert.IsFalse(lookup.NeedsRefresh, "a warm entry must not send the caller back to the calendar");
        }

        [TestMethod]
        public void InvalidationStillServesTheOldValueWhileAskingForARefresh()
        {
            MeetingsCache cache = new MeetingsCache();
            var stored = new List<ScheduleMeetingInput> { Meeting("standup", 9) };
            cache.Store(stored, Key());

            CollectionAssert.AreEqual(new List<MeetingsCache.Key> { Key() }, cache.InvalidateAll().ToList());

            MeetingsCache.Lookup lookup = cache.Get(Key());
            CollectionAssert.AreEqual(stored, lookup.Meetings.ToList(),
                                      "a stale entry is served, never dropped — dropping it would put the UI "
                                      + "thread back on the blocking calendar query");
            Assert.IsTrue(lookup.NeedsRefresh);
        }

        [TestMethod]
        public void StoringTheRefreshClearsTheStaleMark()
        {
            MeetingsCache cache = new MeetingsCache();
            cache.Store(new[] { Meeting("standup", 9) }, Key());
            cache.InvalidateAll();

            cache.Store(new[] { Meeting("standup", 9), Meeting("retro", 15) }, Key());

            Assert.IsFalse(cache.Get(Key()).NeedsRefresh);
        }

        [TestMethod]
        public void StoreReportsWhetherTheUiNeedsReRendering()
        {
            MeetingsCache cache = new MeetingsCache();
            var stored = new List<ScheduleMeetingInput> { Meeting("standup", 9) };

            Assert.IsTrue(cache.Store(stored, Key()), "first fetch is a change");
            Assert.IsFalse(cache.Store(new List<ScheduleMeetingInput> { Meeting("standup", 9) }, Key()),
                           "an unchanged calendar must not re-render");
            Assert.IsTrue(cache.Store(stored.Concat(new[] { Meeting("retro", 15) }).ToList(), Key()));
        }

        [TestMethod]
        public void RefreshIsCoalescedUntilItFinishes()
        {
            MeetingsCache cache = new MeetingsCache();

            Assert.IsTrue(cache.BeginRefresh(Key()));
            Assert.IsFalse(cache.BeginRefresh(Key()), "a second tick must not stack another query");
            cache.EndRefresh(Key());
            Assert.IsTrue(cache.BeginRefresh(Key()));
        }

        [TestMethod]
        public void DaysAndDismissedVisibilityAreCachedSeparately()
        {
            MeetingsCache cache = new MeetingsCache();
            DateTimeOffset yesterday = Today.AddDays(-1);
            cache.Store(new[] { Meeting("standup", 9) }, Key());

            Assert.IsTrue(cache.Get(Key(yesterday)).NeedsRefresh);
            Assert.IsTrue(cache.Get(Key(includeDismissed: true)).NeedsRefresh);
            Assert.IsFalse(cache.Get(Key()).NeedsRefresh);
        }

        [TestMethod]
        public void HasValueSeparatesAnUnreadDayFromAClearOne()
        {
            MeetingsCache cache = new MeetingsCache();

            Assert.IsFalse(cache.HasValue(Key()), "nothing read yet — the empty lookup is a placeholder");
            cache.Store(new List<ScheduleMeetingInput>(), Key());
            Assert.IsTrue(cache.HasValue(Key()), "a day read and found empty is known to be clear");
            Assert.AreEqual(0, cache.Get(Key()).Meetings.Count, "both cases still serve [] to the UI thread");
        }

        [TestMethod]
        public void AStaleEntryStillCountsAsKnown()
        {
            MeetingsCache cache = new MeetingsCache();
            cache.Store(new[] { Meeting("standup", 9) }, Key());
            cache.InvalidateAll();

            Assert.IsTrue(cache.HasValue(Key()), "stale is a real answer one refresh old, not an absent one");
        }

        [TestMethod]
        public void InvalidationOnlyRefreshesDaysThatWereActuallyCached()
        {
            MeetingsCache cache = new MeetingsCache();

            Assert.AreEqual(0, cache.InvalidateAll().Count);

            cache.Store(new[] { Meeting("standup", 9) }, Key());
            cache.Store(new List<ScheduleMeetingInput>(), Key(includeDismissed: true));

            CollectionAssert.AreEquivalent(
                new List<MeetingsCache.Key> { Key(), Key(includeDismissed: true) },
                cache.InvalidateAll().ToList());
        }
    }
}
