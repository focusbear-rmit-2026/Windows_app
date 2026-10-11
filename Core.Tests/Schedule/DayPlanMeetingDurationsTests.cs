using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the meeting-trim half of <c>DayFixPlanTests.swift</c>: shortening a meeting for the
    /// Day Plan only, on one day, reversibly — and never touching the calendar.
    /// </summary>
    [TestClass]
    public class DayPlanMeetingDurationsTests
    {
        private const string EventId = "event-1";
        private const int OneHourSeconds = 3600;
        private const int ThirtyMinuteSeconds = 30 * 60;

        private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Tomorrow = Day.AddDays(1);
        private static readonly DateTimeOffset Start = Day.AddHours(9);
        private static readonly DateTimeOffset End = Start.AddSeconds(OneHourSeconds);

        /// <summary>The trim is the Day Plan's, not the calendar's: one day, and reversible.</summary>
        [TestMethod]
        public void ShorteningAMeetingOnlyAffectsTheDayItWasShortenedOn()
        {
            var trims = new DayPlanMeetingDurations();
            trims.SetOverride(ThirtyMinuteSeconds, EventId, Day, OneHourSeconds);

            Assert.AreEqual(Start.AddSeconds(ThirtyMinuteSeconds), trims.TrimmedEnd(EventId, Day, Start, End));
            Assert.AreEqual(End, trims.TrimmedEnd(EventId, Tomorrow, Start, End),
                            "Tomorrow's occurrence keeps its real length");
            Assert.AreEqual(End, trims.TrimmedEnd("other", Day, Start, End), "Another meeting is untouched");
        }

        [TestMethod]
        public void AskingForTheMeetingsRealLengthClearsTheTrimInsteadOfStoringOne()
        {
            var trims = new DayPlanMeetingDurations();
            trims.SetOverride(ThirtyMinuteSeconds, EventId, Day, OneHourSeconds);

            Assert.IsNull(trims.SetOverride(OneHourSeconds, EventId, Day, OneHourSeconds));
            Assert.IsNull(trims.OverrideSeconds(EventId, Day));
            Assert.AreEqual(End, trims.TrimmedEnd(EventId, Day, Start, End));
        }

        /// <summary>
        /// A trim only ever shortens — a stored value longer than the meeting (it was extended on the
        /// calendar after being trimmed) must not stretch the slot back out.
        /// </summary>
        [TestMethod]
        public void ATrimNeverMakesAMeetingLongerThanItReallyIs()
        {
            var trims = new DayPlanMeetingDurations();
            trims.SetOverride(50 * 60, EventId, Day, OneHourSeconds);

            DateTimeOffset shorterEnd = Start.AddMinutes(20);
            Assert.AreEqual(shorterEnd, trims.TrimmedEnd(EventId, Day, Start, shorterEnd));
        }

        /// <summary>
        /// A trim below the floor is clamped rather than refused, so a mis-typed "10 seconds" still
        /// leaves a usable slot. The floor is shared with the habit trims deliberately.
        /// </summary>
        [TestMethod]
        public void ATrimIsClampedToTheSharedMinimum()
        {
            var trims = new DayPlanMeetingDurations();
            Assert.AreEqual(DayPlanHabitDurations.MinimumSeconds,
                            trims.SetOverride(5, EventId, Day, OneHourSeconds));
        }

        /// <summary>A meeting with no identifier cannot be trimmed — there is nothing to key on.</summary>
        [TestMethod]
        public void AMeetingWithNoIdentifierIsNeverTrimmed()
        {
            var trims = new DayPlanMeetingDurations();
            Assert.IsNull(trims.SetOverride(ThirtyMinuteSeconds, "", Day, OneHourSeconds));
            Assert.IsNull(trims.OverrideSeconds("", Day));
            Assert.AreEqual(0, trims.OverridesByDay.Count);
        }

        [TestMethod]
        public void ClearingATrimLeavesNoEmptyDayBehind()
        {
            var trims = new DayPlanMeetingDurations();
            trims.SetOverride(ThirtyMinuteSeconds, EventId, Day, OneHourSeconds);
            trims.ClearOverride(EventId, Day);

            Assert.AreEqual(0, trims.OverridesByDay.Count, "An emptied day is dropped, not kept blank");
        }

        /// <summary>The map is capped, and the days it keeps are the most recent ones.</summary>
        [TestMethod]
        public void OldDaysAreDroppedSoTheTrimMapCannotGrowWithoutBound()
        {
            var stored = new Dictionary<string, Dictionary<string, int>>();
            for (int dayOffset = 0; dayOffset < DayPlanMeetingDurations.RetainedDays + 10; dayOffset++)
            {
                stored[new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset)
                           .ToString(DayPlanDayKey.Format, CultureInfo.InvariantCulture)] =
                    new Dictionary<string, int> { { EventId, 600 } };
            }

            // Pruning happens on write, so one more trim is what trims the map.
            var trims = new DayPlanMeetingDurations(stored);
            trims.SetOverride(ThirtyMinuteSeconds, EventId, Day, OneHourSeconds);

            Assert.AreEqual(DayPlanMeetingDurations.RetainedDays, trims.OverridesByDay.Count);
            Assert.IsTrue(trims.OverridesByDay.ContainsKey(DayPlanDayKey.For(Day)),
                          "The day just written is one of the kept ones");

            string oldest = trims.OverridesByDay.Keys.OrderBy(key => key, StringComparer.Ordinal).First();
            string droppedLatest = stored.Keys.OrderBy(key => key, StringComparer.Ordinal)
                .Take(stored.Count + 1 - DayPlanMeetingDurations.RetainedDays).Last();
            Assert.IsTrue(string.CompareOrdinal(oldest, droppedLatest) > 0,
                          "The days kept are the most recent ones");
        }

        /// <summary>The trims are the caller's to persist; the store hands back a plain map.</summary>
        [TestMethod]
        public void TheStoredMapRoundTripsThroughTheHost()
        {
            var trims = new DayPlanMeetingDurations();
            trims.SetOverride(ThirtyMinuteSeconds, EventId, Day, OneHourSeconds);

            var reloaded = new DayPlanMeetingDurations(
                trims.OverridesByDay.ToDictionary(entry => entry.Key,
                                                  entry => new Dictionary<string, int>(entry.Value)));

            Assert.AreEqual(ThirtyMinuteSeconds, reloaded.OverrideSeconds(EventId, Day));
        }
    }

    /// <summary>
    /// Ported from <c>DayPlanUserActionReloadTests.swift</c> (Mac report 2026-09-21_20-47-13):
    /// navigating the Day Plan while a task name was being typed.
    /// </summary>
    [TestClass]
    public class DayPlanDateChangeTests
    {
        private static readonly DateTimeOffset Monday = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void OnlyARealDayChangeCountsAsADayChange()
        {
            DateTimeOffset mondayLater = Monday.AddHours(11);
            DateTimeOffset tuesday = Monday.AddDays(1).AddHours(-8);

            Assert.IsFalse(DayPlanDateChange.IsADifferentDay(Monday, Monday));
            Assert.IsFalse(DayPlanDateChange.IsADifferentDay(Monday, mondayLater),
                           "A refresh later the same day must not end editing");
            Assert.IsTrue(DayPlanDateChange.IsADifferentDay(Monday, tuesday));
            Assert.IsTrue(DayPlanDateChange.IsADifferentDay(tuesday, Monday),
                          "Backwards counts too — the report navigated to a PREVIOUS day");
        }

        [TestMethod]
        public void MidnightIsADayChangeEvenSecondsApart()
        {
            DateTimeOffset justBefore = new DateTimeOffset(2026, 9, 21, 23, 59, 59, TimeSpan.Zero);
            DateTimeOffset justAfter = new DateTimeOffset(2026, 9, 22, 0, 0, 1, TimeSpan.Zero);

            Assert.IsTrue(DayPlanDateChange.IsADifferentDay(justBefore, justAfter),
                          "Two seconds apart, but the Day Plan has rolled over");
        }

        /// <summary>
        /// 25 hours apart yet the same calendar day is impossible; 2 hours apart spanning midnight is
        /// not. A naive "more than 24h" test would get both wrong.
        /// </summary>
        [TestMethod]
        public void TheComparisonIsCalendarBasedNotAFixedInterval()
        {
            DateTimeOffset lateMonday = new DateTimeOffset(2026, 9, 21, 23, 0, 0, TimeSpan.Zero);
            DateTimeOffset earlyTuesday = new DateTimeOffset(2026, 9, 22, 1, 0, 0, TimeSpan.Zero);

            Assert.IsTrue(DayPlanDateChange.IsADifferentDay(lateMonday, earlyTuesday));
            Assert.IsTrue((earlyTuesday - lateMonday) < TimeSpan.FromHours(24));
        }
    }

    /// <summary>
    /// Ported from <c>DayPlanDestination.swift</c> (Mac report 2026-06-25_18-11-05): "if an allowed
    /// app habit is running, it should switch to the day plan task".
    /// </summary>
    [TestClass]
    public class DayPlanDestinationTests
    {
        /// <summary>
        /// While the allowed-activity player is up it owns the habits area, so the main window's own
        /// Day Plan pane refuses to show — aiming at it left the user looking at the Habit Player tab
        /// and nothing appeared to happen.
        /// </summary>
        [TestMethod]
        public void TheAllowedActivityPlayersOwnTabWinsWhileItIsOnScreen()
        {
            Assert.AreEqual(DayPlanTarget.AllowedActivityDayPlanTab, DayPlanDestination.Target(true));
        }

        [TestMethod]
        public void OtherwiseTheMainWindowsPaneIsTheDayPlan()
        {
            Assert.AreEqual(DayPlanTarget.MainWindowDayPlan, DayPlanDestination.Target(false));
        }
    }
}
