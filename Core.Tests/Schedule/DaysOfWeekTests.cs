using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>DaysOfWeekTests.swift</c> — the shared days-of-week gate used when
    /// filtering routines and morning/evening habits for the current day.
    /// </summary>
    [TestClass]
    public class DaysOfWeekTests
    {
        private static readonly string[] AllDays = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
        private static readonly string[] Weekdays = { "MON", "TUE", "WED", "THU", "FRI" };

        [TestMethod]
        public void AllMatchesEveryDay()
        {
            foreach (string day in AllDays)
            {
                Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "all" }, day),
                              "'all' should match " + day);
            }
        }

        [TestMethod]
        public void WeekdaysMatchesMondayToFriday()
        {
            foreach (string day in Weekdays)
            {
                Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekdays" }, day),
                              "'weekdays' should match " + day);
            }
        }

        [TestMethod]
        public void WeekdaysExcludesSaturdayAndSunday()
        {
            Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekdays" }, "SAT"));
            // Regression: the old inline checks tested "sat" twice, so Sunday incorrectly counted as a
            // weekday.
            Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekdays" }, "SUN"));
        }

        [TestMethod]
        public void WeekendMatchesSaturdayAndSunday()
        {
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, "SAT"));
            // Regression: the same duplicated "sat" meant 'weekend' never matched Sunday.
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, "SUN"));
        }

        [TestMethod]
        public void WeekendExcludesWeekdays()
        {
            foreach (string day in Weekdays)
            {
                Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, day),
                               "'weekend' should not match " + day);
            }
        }

        [TestMethod]
        public void NamedDayMatchesCaseInsensitively()
        {
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "MON", "WED" }, "WED"));
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "wed" }, "WED"));
            Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "MON", "WED" }, "TUE"));
        }

        [TestMethod]
        public void EmptyListMatchesNothing()
        {
            foreach (string day in AllDays)
            {
                Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new List<string>(), day));
            }
        }

        [TestMethod]
        public void WeekdayCodesAreTheEnglishServerValues()
        {
            // 2026-07-05 is a Sunday. The code must be the English "SUN" the server sends, whatever the
            // machine's culture — the Mac pins a fixed en_US_POSIX formatter for exactly this reason.
            DateTimeOffset sunday = new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.FromHours(10));
            Assert.AreEqual("SUN", WeekdayCode.For(sunday));
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, WeekdayCode.For(sunday)));

            DateTimeOffset saturday = sunday.AddDays(-1);
            Assert.AreEqual("SAT", WeekdayCode.For(saturday));
            Assert.AreEqual("MON", WeekdayCode.For(sunday.AddDays(1)));
        }

        [TestMethod]
        public void ASpanishMachineStillProducesEnglishCodes()
        {
            // The bug the fixed locale exists to prevent: a localized day name ("SÁB") matches no server
            // value, so every habit silently stops being scheduled.
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
                DateTimeOffset saturday = new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.FromHours(10));
                Assert.AreEqual("SAT", WeekdayCode.For(saturday));
                Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, WeekdayCode.For(saturday)));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        /// <summary>
        /// The weekend test is a CONTAINS, not an equality, on the Mac — so a full day name is
        /// recognised too. Ported as-is rather than tightened: a server value this port rejects but the
        /// Mac accepts would silently unschedule habits on one platform only.
        /// </summary>
        [TestMethod]
        public void AFullDayNameIsStillRecognisedAsTheWeekend()
        {
            Assert.IsTrue(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekend" }, "Saturday"));
            Assert.IsFalse(RoutineDaysOfWeek.IsScheduledForWeekday(new[] { "weekdays" }, "Sunday"));
        }
    }
}
