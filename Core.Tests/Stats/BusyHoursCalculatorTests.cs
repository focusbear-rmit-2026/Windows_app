using System;
using System.Collections.Generic;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// Ported from the Mac's BusyHoursCalculatorTests: the maths behind the
    /// <c>PUT /portfolio/busy-hours</c> report (#2418) — overlapping events count once, an event
    /// across midnight is shared between its days, and all-day / declined / cancelled / dismissed
    /// events do not occupy the user's time.
    /// </summary>
    [TestClass]
    public class BusyHoursCalculatorTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 20);

        private static string TodayKey
        {
            get { return BusyHoursCalculator.DayKey(Today); }
        }

        private static string TomorrowKey
        {
            get { return BusyHoursCalculator.DayKey(Day(1)); }
        }

        private static DateTime Day(int offset)
        {
            return Today.AddDays(offset);
        }

        private static DateTime At(int dayOffset, int hour, int minute = 0)
        {
            return Day(dayOffset).AddHours(hour).AddMinutes(minute);
        }

        private static BusyEventInput Event(DateTime start, DateTime end, bool allDay = false, bool cancelled = false,
                                            bool declined = false, bool dismissed = false)
        {
            return new BusyEventInput
            {
                Start = start,
                End = end,
                IsAllDay = allDay,
                IsCancelled = cancelled,
                IsDeclined = declined,
                IsDismissed = dismissed
            };
        }

        private static IDictionary<string, double> Hours(IEnumerable<BusyEventInput> events, int horizonDays = 3)
        {
            return BusyHoursCalculator.HoursByDay(events, Today, horizonDays);
        }

        [TestMethod]
        public void OverlappingEventsCountOnce()
        {
            // 9–10:30 and 10–11 overlap by 30 minutes: 2 hours of busy time, not 2.5.
            Assert.AreEqual(2, Hours(new[] { Event(At(0, 9), At(0, 10, 30)), Event(At(0, 10), At(0, 11)) })[TodayKey]);
        }

        [TestMethod]
        public void NestedAndTouchingEventsMerge()
        {
            // 9–12 contains 10–11; 12–13 touches 9–12 → one 9–13 block.
            Assert.AreEqual(4, Hours(new[]
            {
                Event(At(0, 10), At(0, 11)), Event(At(0, 9), At(0, 12)), Event(At(0, 12), At(0, 13))
            })[TodayKey]);
        }

        [TestMethod]
        public void MergeKeepsSeparateIntervalsApart()
        {
            IList<BusyInterval> merged = BusyHoursCalculator.Merge(new[]
            {
                new BusyInterval(At(0, 14), At(0, 15)),
                new BusyInterval(At(0, 9), At(0, 10))
            });

            CollectionAssert.AreEqual(
                new[] { new BusyInterval(At(0, 9), At(0, 10)), new BusyInterval(At(0, 14), At(0, 15)) },
                (System.Collections.ICollection)merged);
        }

        [TestMethod]
        public void EventAcrossMidnightIsSplitBetweenDays()
        {
            // 22:00 → 02:00 next day: 2 hours today, 2 hours tomorrow.
            IDictionary<string, double> result = Hours(new[] { Event(At(0, 22), At(1, 2)) });

            Assert.AreEqual(2, result[TodayKey]);
            Assert.AreEqual(2, result[TomorrowKey]);
        }

        [TestMethod]
        public void SplitAtMidnightProducesOnePiecePerDay()
        {
            IList<BusyInterval> pieces = BusyHoursCalculator.SplitAtMidnight(new BusyInterval(At(0, 23), At(2, 1)));

            Assert.AreEqual(3, pieces.Count);
            Assert.AreEqual(Day(1), pieces[0].End);
            Assert.AreEqual(new BusyInterval(Day(1), Day(2)), pieces[1]);
            Assert.AreEqual(Day(2), pieces[2].Start);
        }

        [TestMethod]
        public void AllDayEventsAreExcluded()
        {
            Assert.AreEqual(1, Hours(new[] { Event(Day(0), Day(1), allDay: true), Event(At(0, 9), At(0, 10)) })[TodayKey]);
        }

        [TestMethod]
        public void DeclinedEventsAreExcluded()
        {
            Assert.AreEqual(0.5, Hours(new[]
            {
                Event(At(0, 9), At(0, 10), declined: true), Event(At(0, 13), At(0, 13, 30))
            })[TodayKey]);
        }

        [TestMethod]
        public void CancelledAndDismissedEventsAreExcluded()
        {
            Assert.AreEqual(0, Hours(new[]
            {
                Event(At(0, 9), At(0, 10), cancelled: true), Event(At(0, 10), At(0, 11), dismissed: true)
            })[TodayKey]);
        }

        [TestMethod]
        public void InvertedEventIsIgnored()
        {
            Assert.IsFalse(Event(At(0, 10), At(0, 9)).IsBusy);
        }

        [TestMethod]
        public void EveryDayInHorizonIsReportedEvenWhenFree()
        {
            IDictionary<string, double> result = Hours(new BusyEventInput[0], horizonDays: 14);

            Assert.AreEqual(14, result.Count);
            foreach (double hours in result.Values)
            {
                Assert.AreEqual(0, hours, "A free day is reported as 0, not left out");
            }

            Assert.IsTrue(result.ContainsKey(TodayKey));
        }

        [TestMethod]
        public void EventsOutsideTheHorizonAreDropped()
        {
            // Yesterday's event and one three days out both fall outside a 3-day horizon (today, +1, +2).
            IDictionary<string, double> result = Hours(new[]
            {
                Event(At(-1, 9), At(-1, 10)), Event(At(3, 9), At(3, 10)), Event(At(2, 9), At(2, 10))
            }, horizonDays: 3);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1, result[BusyHoursCalculator.DayKey(Day(2))]);

            double total = 0;
            foreach (double hours in result.Values)
            {
                total += hours;
            }

            Assert.AreEqual(1, total);
        }

        [TestMethod]
        public void HoursRoundToTwoDecimals()
        {
            // 20 minutes = 0.3333… → 0.33
            Assert.AreEqual(0.33, Hours(new[] { Event(At(0, 9), At(0, 9, 20)) })[TodayKey]);
            Assert.AreEqual(2.46, BusyHoursCalculator.Rounded(2.456));
        }

        /// <summary>
        /// 1.005 is not representable in binary — it is really 1.00499999999999989…, so it rounds DOWN
        /// however the midpoint rule is set. The Mac's test allows either answer (accuracy 0.011); this
        /// pins the one both languages actually produce, so a future "fix" to the rounding is visible.
        /// </summary>
        [TestMethod]
        public void ARoundingMidpointThatIsNotReallyAMidpointRoundsDownInBothLanguages()
        {
            Assert.IsTrue(1.005 * 100 < 100.5, "the literal is below the midpoint before it is ever rounded");
            Assert.AreEqual(1.0, BusyHoursCalculator.Rounded(1.005));
        }

        [TestMethod]
        public void EmptyHorizonYieldsNothing()
        {
            Assert.AreEqual(0, Hours(new[] { Event(At(0, 9), At(0, 10)) }, horizonDays: 0).Count);
        }

        [TestMethod]
        public void FingerprintIsOrderIndependent()
        {
            const string source = "windows-outlook";
            string a = BusyHoursReporter.Fingerprint(source,
                new Dictionary<string, double> { { "2026-09-08", 1.5 }, { "2026-09-07", 2 } });
            string b = BusyHoursReporter.Fingerprint(source,
                new Dictionary<string, double> { { "2026-09-07", 2 }, { "2026-09-08", 1.5 } });
            string changed = BusyHoursReporter.Fingerprint(source,
                new Dictionary<string, double> { { "2026-09-07", 2 }, { "2026-09-08", 1.75 } });

            Assert.AreEqual(a, b, "A re-ordered day map is not a change");
            Assert.AreNotEqual(a, changed);
        }
    }
}
