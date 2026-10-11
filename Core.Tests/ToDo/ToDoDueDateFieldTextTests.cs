using System;
using System.Globalization;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The portable half of the Mac's <c>ToDoDueDateFieldTests</c>: what text the field writes, when
    /// it re-seeds itself, and the round-trip rule that keeps the two from losing a date. The Mac's
    /// other cases in that file drive a real AppKit field editor and could not come along.
    /// </summary>
    [TestClass]
    public class ToDoDueDateFieldTextTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 9, 15);
        private static readonly DateTime Eleventh = new DateTime(2026, 9, 11);
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        private static DateTime Day(int year, int month, int day) => new DateTime(year, month, day);

        // MARK: Re-seeding (the calendar picker writes the day, not the text)

        [TestMethod]
        public void ADayChosenElsewhereReplacesTheTypedText()
        {
            Assert.AreEqual(ToDoDates.DayMonthYear(Day(2026, 9, 30)),
                            ToDoDueDateFieldText.Reseed("25", Day(2026, 9, 30), FixedNow, English),
                            "picking 30 Sep in the calendar must not leave '25' behind to overwrite it");
        }

        [TestMethod]
        public void TypingKeepsItsOwnTextWhileItAlreadyMeansThatDay()
        {
            Assert.IsNull(ToDoDueDateFieldText.Reseed("25", Day(2026, 9, 25), FixedNow, English),
                          "rewriting '25' to '25 Sep 2026' mid-word would move the caret under the user");
        }

        [TestMethod]
        public void ClearingTheDayEmptiesTheText()
        {
            Assert.AreEqual(string.Empty, ToDoDueDateFieldText.Reseed("25 Sep 2026", null, FixedNow, English));
        }

        [TestMethod]
        public void ATaskWithoutADueDateHasNoFieldText()
        {
            Assert.AreEqual(string.Empty, ToDoDueDateFieldText.Text(null, English));
        }

        // MARK: The text the field writes must be text the grammar reads back

        /// <summary>
        /// Four years a day at a time, so the format cannot drift into something the grammar drops on
        /// the next save. The span matters: "2y" makes a date two years out an ordinary thing to type
        /// (Mac report 2026-09-15_20-52-05), and a year-less rendering of a day already past in the
        /// current year would re-read as next year.
        /// </summary>
        [TestMethod]
        public void EveryRenderedDayParsesBackToItself()
        {
            const int daysToCheck = 1500;
            DateTime start = Day(2026, 9, 15);

            for (int offset = 0; offset < daysToCheck; offset++)
            {
                DateTime date = start.AddDays(offset);
                string rendered = ToDoDueDateFieldText.Text(date, English);
                Assert.AreEqual(date, ToDoDueDateParser.Parse(rendered, start, English),
                                "'" + rendered + "' must read back as the day it came from");
            }
        }

        [TestMethod]
        public void TheFieldsOwnTextAlwaysCarriesTheYearEvenForThisYear()
        {
            DateTime thisYear = Day(2026, 1, 4);
            string rendered = ToDoDueDateFieldText.Text(thisYear, English);

            StringAssert.Contains(rendered, "2026",
                                  "dropping the year here would re-read '4 Jan' as next January and move the task");
            Assert.AreEqual(thisYear, ToDoDueDateParser.Parse(rendered, FixedNow, English));
        }

        [TestMethod]
        public void FarFutureDatesSurviveTheRoundTrip()
        {
            foreach (DateTime date in new[] { Day(2027, 2, 28), Day(2028, 4, 4), Day(2028, 2, 29), Day(2029, 12, 31) })
            {
                string rendered = ToDoDueDateFieldText.Text(date, English);
                Assert.AreEqual(date, ToDoDueDateParser.Parse(rendered, FixedNow, English), "'" + rendered + "'");
            }
        }

        [TestMethod]
        public void TheWeekdayFormatIsNotUsedBecauseTheGrammarCannotReadItBack()
        {
            foreach (DateTime date in new[] { Eleventh, Day(2028, 4, 4) })
            {
                string rendered = ToDoDates.WeekdayDayMonth(date, FixedNow, English);
                Assert.IsNull(ToDoDueDateParser.Parse(rendered, FixedNow, English),
                              "'" + rendered + "' is the preview line's format, not the field's — seeding it would drop the date");
            }
        }
    }
}
