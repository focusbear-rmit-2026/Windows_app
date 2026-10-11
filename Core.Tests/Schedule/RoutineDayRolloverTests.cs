using System;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>DaysOfWeekTests.swift</c>, "isHabitAlreadyShown (crash regression)".
    ///
    /// <c>AppDelegate.checkLastHabitShowDate</c> force-unwrapped the global start time, so a nil start
    /// time took the app down on launch with EXC_BREAKPOINT — reported as "Focus Bear quit
    /// unexpectedly", trace readSettingsAndLoadApp → checkWindowState → checkLastHabitShowDate.
    /// </summary>
    [TestClass]
    public class RoutineDayRolloverTests
    {
        private static readonly TimeSpan Offset = TimeSpan.FromHours(10);

        private static DateTimeOffset Moment(int hour, int minute, int day = 10)
        {
            return new DateTimeOffset(2026, 9, day, hour, minute, 0, Offset);
        }

        private static readonly RoutineStartTime SevenAm = new RoutineStartTime(7, 0);

        [TestMethod]
        public void NoRoutineStartTimeDoesNotCrashAndComparesOnCalendarDay()
        {
            // The crash case: settings not loaded yet, or a profile whose startup time never parsed.
            Assert.IsFalse(RoutineDayRollover.IsHabitAlreadyShown(Moment(9, 0, day: 9), Moment(9, 0), null));
            Assert.IsTrue(RoutineDayRollover.IsHabitAlreadyShown(Moment(9, 0), Moment(11, 0), null));
        }

        [TestMethod]
        public void BeforeRoutineStartStillCountsAsYesterdaysRoutineDay()
        {
            // 01:00 against an 07:00 start is still the 9th's routine day, so the 9th's showing stands.
            Assert.IsTrue(RoutineDayRollover.IsHabitAlreadyShown(Moment(8, 0, day: 9), Moment(1, 0), SevenAm));
        }

        [TestMethod]
        public void AfterRoutineStartANewDayHasNotBeenShown()
        {
            Assert.IsFalse(RoutineDayRollover.IsHabitAlreadyShown(Moment(8, 0, day: 9), Moment(9, 0), SevenAm));
        }

        [TestMethod]
        public void SameRoutineDayCountsAsAlreadyShown()
        {
            Assert.IsTrue(RoutineDayRollover.IsHabitAlreadyShown(Moment(8, 0), Moment(9, 0), SevenAm));
        }

        [TestMethod]
        public void AFutureLastShownDateIsTreatedAsAlreadyShown()
        {
            // A clock change or a restored backup can leave the stored date ahead of now; don't re-show.
            Assert.IsTrue(RoutineDayRollover.IsHabitAlreadyShown(Moment(9, 0, day: 11), Moment(9, 0), null));
        }
    }
}
