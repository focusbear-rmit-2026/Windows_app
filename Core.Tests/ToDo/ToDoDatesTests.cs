using System;
using System.Globalization;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoDatesTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 10, 0, 0);
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        private static DateTime Day(int year, int month, int day) => new DateTime(year, month, day);

        /// <summary>
        /// The API sends both shapes and the Mac keeps two ISO formatters for exactly that reason, so
        /// a parser that handled only one would silently drop every `created_at`.
        /// </summary>
        [TestMethod]
        public void IsoInstantsParseWithAndWithoutFractionalSeconds()
        {
            DateTime? withFractions = ToDoDates.Parse("2026-09-11T13:00:00.000Z");
            DateTime? without = ToDoDates.Parse("2026-09-11T13:00:00Z");
            DateTime? explicitOffset = ToDoDates.Parse("2026-09-11T13:00:00+00:00");

            Assert.IsNotNull(withFractions);
            Assert.AreEqual(withFractions, without);
            Assert.AreEqual(withFractions, explicitOffset);
        }

        [TestMethod]
        public void APlainCalendarDayParsesToThatDayAtMidnight()
        {
            Assert.AreEqual(Day(2026, 9, 11), ToDoDates.Parse("2026-09-11"));
            Assert.AreEqual("2026-09-11", ToDoDates.YmdFromIso("2026-09-11"));
        }

        /// <summary>
        /// The one place this port deliberately differs from a naive `DateTime.TryParse`: a wire field
        /// is ISO or nothing. Accepting whatever the running culture calls a date would make the same
        /// string mean different days on two machines.
        /// </summary>
        [TestMethod]
        public void NothingButTheTwoWireShapesIsADate()
        {
            Assert.IsNull(ToDoDates.Parse(null));
            Assert.IsNull(ToDoDates.Parse("   "));
            Assert.IsNull(ToDoDates.Parse("soonish"));
            Assert.IsNull(ToDoDates.Parse("9/25/2026"));
            Assert.IsNull(ToDoDates.Parse("11 Sep 2026"));
        }

        [TestMethod]
        public void DaysBetweenCountsWholeCalendarDaysAndIsNegativeInThePast()
        {
            Assert.AreEqual(0, ToDoDates.DaysBetween(Now, Day(2026, 9, 7)), "the time of day never rounds a day away");
            Assert.AreEqual(1, ToDoDates.DaysBetween(Now, Day(2026, 9, 8)));
            Assert.AreEqual(-3, ToDoDates.DaysBetween(Now, Day(2026, 9, 4)));
        }

        // MARK: Overdue / due-today chips

        [TestMethod]
        public void ADueDateInThePastIsOverdueAndTodayIsDueToday()
        {
            Assert.AreEqual(ToDoDueState.Overdue, ToDoDates.DueState("2026-09-06", false, Now));
            Assert.AreEqual(ToDoDueState.Today, ToDoDates.DueState("2026-09-07", false, Now));
            Assert.AreEqual(ToDoDueState.None, ToDoDates.DueState("2026-09-08", false, Now));
            Assert.AreEqual(ToDoDueState.None, ToDoDates.DueState(null, false, Now), "no due date, no chip");
        }

        /// <summary>A finished task is never nagged about, however long ago it was due.</summary>
        [TestMethod]
        public void ACompletedTaskIsNeverOverdue()
        {
            Assert.AreEqual(ToDoDueState.None, ToDoDates.DueState("2026-09-06", true, Now));
            Assert.AreEqual(ToDoDueState.None, ToDoDates.DueState("2026-09-07", true, Now));
        }

        // MARK: Labels

        [TestMethod]
        public void DueLabelNamesTheThreeDaysAroundTodayAndDatesTheRest()
        {
            Assert.AreEqual("Today", Label("2026-09-07"));
            Assert.AreEqual("Tomorrow", Label("2026-09-08"));
            Assert.AreEqual("Yesterday", Label("2026-09-06"));
            Assert.AreEqual("18 Jan", Label("2026-01-18"), "a day further out is just a date");
            Assert.AreEqual("18 Jan 2027", Label("2027-01-18"), "and carries its year once it leaves this one");
        }

        /// <summary>
        /// The list rows and the resolved line under the typed field must agree about when a year is
        /// worth showing, or scheduling far out looks like scheduling this year
        /// (Mac report 2026-09-15_20-52-05).
        /// </summary>
        [TestMethod]
        public void TheYearShowsOnlyWhenTheDayIsOutsideTheCurrentOne()
        {
            Assert.AreEqual("31 Dec", Label("2026-12-31"), "a day in this year does not repeat the year");
            Assert.AreEqual("4 Apr 2028", Label("2028-04-04"));

            StringAssert.Contains(ToDoDates.WeekdayDayMonth(Day(2028, 4, 4), Now, English), "2028");
            StringAssert.Contains(ToDoDates.WeekdayDayMonth(Day(2027, 1, 1), Now, English), "2027",
                                  "the very next year counts as outside this one");
            StringAssert.Contains(ToDoDates.WeekdayDayMonth(Day(2025, 4, 4), Now, English), "2025",
                                  "a past year is just as misleading as a future one");
            Assert.IsFalse(ToDoDates.WeekdayDayMonth(Day(2026, 12, 31), Now, English).Contains("2026"));
        }

        [TestMethod]
        public void DurationLabelDropsTheHalfThatIsZero()
        {
            Assert.AreEqual("1h 30m", ToDoDates.DurationLabel(1, 30));
            Assert.AreEqual("2h", ToDoDates.DurationLabel(2, 0));
            Assert.AreEqual("45m", ToDoDates.DurationLabel(0, 45));
            Assert.AreEqual("1h 30m", ToDoDates.DurationLabelFromSeconds(90 * 60));
            Assert.AreEqual("0m", ToDoDates.DurationLabelFromSeconds(-1), "a negative duration reads as none, not as a negative one");
        }

        private static string Label(string iso) =>
            ToDoDates.DueLabel(iso, Now, "Today", "Tomorrow", "Yesterday", English);
    }
}
