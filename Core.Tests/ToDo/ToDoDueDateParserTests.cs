using System;
using System.Globalization;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The typed due-date grammar, ported case-for-case from the Mac's <c>ToDoLogicTests</c>.
    /// Every case resolves against Mon 7 Sep 2026 10:00, as the Mac's do.
    /// </summary>
    [TestClass]
    public class ToDoDueDateParserTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 10, 0, 0);
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        private static DateTime Day(int year, int month, int day) => new DateTime(year, month, day);

        private static DateTime? Parsed(string input, CultureInfo culture = null) =>
            ToDoDueDateParser.Parse(input, Now, culture ?? English);

        [TestMethod]
        public void TheFixtureDayIsAMondaySoTheWeekdayCasesMeanWhatTheySay()
        {
            Assert.AreEqual(DayOfWeek.Monday, Now.DayOfWeek);
        }

        [TestMethod]
        public void ParserKeywords()
        {
            Assert.AreEqual(Day(2026, 9, 7), Parsed("today"));
            Assert.AreEqual(Day(2026, 9, 8), Parsed("Tomorrow"));
            Assert.AreEqual(Day(2026, 9, 8), Parsed("tmr"));
            Assert.AreEqual(Day(2026, 9, 7), Parsed("hoy"));
            Assert.AreEqual(Day(2026, 9, 8), Parsed("mañana"));
        }

        [TestMethod]
        public void ParserRelativeUnits()
        {
            Assert.AreEqual(Day(2026, 9, 9), Parsed("2d"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("3w"));
            Assert.AreEqual(Day(2026, 10, 7), Parsed("1m"));
            Assert.AreEqual(Day(2026, 9, 14), Parsed("1 week"));
            Assert.AreEqual(Day(2026, 9, 9), Parsed("in 2 days"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("in  3   weeks"), "whitespace is collapsed");
            Assert.AreEqual(Day(2026, 10, 7), Parsed("in 1 month"));
        }

        /// <summary>"2y = 2 years" (Mac report 2026-09-15_20-52-05): the field rejected it as unrecognised.</summary>
        [TestMethod]
        public void ParserRelativeUnitsUnderstandYears()
        {
            Assert.AreEqual(Day(2028, 9, 7), Parsed("2y"));
            Assert.AreEqual(Day(2027, 9, 7), Parsed("1 year"));
            Assert.AreEqual(Day(2029, 9, 7), Parsed("3 years"));
            Assert.AreEqual(Day(2028, 9, 7), Parsed("in 2 years"));
            Assert.AreEqual(Day(2025, 9, 7), Parsed("-1y"), "years back-date like every other unit");
        }

        /// <summary>
        /// `m` is months. Neither this grammar nor the web's snooze parser has a minutes token; the two
        /// differ only in that snooze spells months "mo", which is accepted here too.
        /// </summary>
        [TestMethod]
        public void ParserMeansMonthsByMAndTakesTheSnoozeSpellingsToo()
        {
            Assert.AreEqual(Parsed("1mo"), Parsed("1m"));
            Assert.AreEqual(Day(2026, 11, 7), Parsed("2mo"));
            Assert.AreEqual(Parsed("2w"), Parsed("2wk"));
            Assert.AreEqual(Day(2026, 9, 21), Parsed("2wks"));
            Assert.AreEqual(Parsed("2y"), Parsed("2yr"));
            Assert.AreEqual(Day(2028, 9, 7), Parsed("2yrs"));
        }

        /// <summary>Guarding the rules years share a pattern with: a bare number is still a day of the month.</summary>
        [TestMethod]
        public void YearsDidNotSwallowTheBareDayNumberOrTheOtherUnits()
        {
            Assert.AreEqual(Day(2026, 9, 23), Parsed("23"), "a bare number is a day of the month, not a count");
            Assert.AreEqual(Day(2026, 9, 9), Parsed("2d"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("3w"));
            Assert.IsNull(Parsed("2ys"));
            Assert.IsNull(Parsed("2ms"));
        }

        [TestMethod]
        public void ParserRelativeUnitsGoBackwards()
        {
            Assert.AreEqual(Day(2026, 9, 6), Parsed("-1d"), "yesterday");
            Assert.AreEqual(Day(2026, 8, 24), Parsed("-2w"));
            Assert.AreEqual(Day(2026, 8, 7), Parsed("-1m"));
            Assert.AreEqual(Day(2026, 9, 4), Parsed("-3 days"));
            Assert.AreEqual(Day(2026, 9, 9), Parsed("+2d"), "an explicit + reads like the bare number");
            Assert.AreEqual(Day(2026, 9, 7), Parsed("-0d"));
        }

        [TestMethod]
        public void ParserStillRejectsALoneSign()
        {
            Assert.IsNull(Parsed("-d"));
            Assert.IsNull(Parsed("-"));
            Assert.IsNull(Parsed("--1d"));
        }

        [TestMethod]
        public void ParserWeekdaysAreTheComingOneNeverToday()
        {
            Assert.AreEqual(Day(2026, 9, 11), Parsed("fri"));
            Assert.AreEqual(Day(2026, 9, 11), Parsed("Friday"));
            Assert.AreEqual(Day(2026, 9, 11), Parsed("next fri"), "'next' reads like the bare weekday");
            Assert.AreEqual(Day(2026, 9, 14), Parsed("mon"), "today is Monday -> next Monday");
            Assert.AreEqual(Day(2026, 9, 14), Parsed("next monday"));
            Assert.AreEqual(Day(2026, 9, 13), Parsed("sun"));
        }

        [TestMethod]
        public void ParserBareDayOfMonthRollsForward()
        {
            Assert.AreEqual(Day(2026, 9, 28), Parsed("28"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("28th"));
            Assert.AreEqual(Day(2026, 10, 7), Parsed("7"), "today's day means next month");
            Assert.AreEqual(Day(2026, 10, 3), Parsed("3rd"));
            Assert.AreEqual(Day(2026, 10, 31), Parsed("31"), "September has no 31st -> October");
        }

        [TestMethod]
        public void ParserDayAndMonthNames()
        {
            Assert.AreEqual(Day(2026, 9, 28), Parsed("28 sep"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("sep 28"));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("28th September"));
            Assert.AreEqual(Day(2027, 9, 28), Parsed("Sept 28, 2027"));
            Assert.AreEqual(Day(2026, 9, 7), Parsed("7 sep"), "an explicit date may be today");
            Assert.AreEqual(Day(2027, 1, 1), Parsed("1 jan"), "already past this year -> next year");
            Assert.IsNull(Parsed("30 feb"));
        }

        [TestMethod]
        public void ParserNumericDates()
        {
            Assert.AreEqual(Day(2026, 9, 25), Parsed("25/9"));
            Assert.AreEqual(Day(2026, 9, 25), Parsed("25.9"));
            Assert.AreEqual(Day(2027, 9, 25), Parsed("25/9/2027"));
            Assert.AreEqual(Day(2027, 9, 25), Parsed("25/9/27"));
            Assert.AreEqual(Day(2027, 1, 1), Parsed("1/1"));
            Assert.AreEqual(Day(2026, 9, 25), Parsed("9/25"), "month/day is accepted when day/month cannot be");
            Assert.AreEqual(Day(2026, 9, 25), Parsed("2026-09-25"));
            Assert.AreEqual(Day(2026, 12, 25), Parsed("2026-12-25"));
        }

        [TestMethod]
        public void ParserRejectsNonsense()
        {
            foreach (string input in new[] { "", "   ", "soonish", "32", "0", "13/13", "in days", "2026-13-01", "next", "2x" })
            {
                Assert.IsNull(Parsed(input), "'" + input + "' must not change the date");
            }
        }

        /// <summary>
        /// The Mac reads the app language's month and weekday names alongside the English ones. Only
        /// the full names are asserted here: .NET takes its abbreviations from ICU, which has changed
        /// its Spanish spellings between versions ("sep" / "sept"), so pinning one would test the
        /// platform rather than this rule.
        /// </summary>
        [TestMethod]
        public void ParserUnderstandsTheAppLanguage()
        {
            CultureInfo spanish = CultureInfo.GetCultureInfo("es");

            Assert.AreEqual(Day(2026, 9, 11), Parsed("viernes", spanish));
            Assert.AreEqual(Day(2026, 9, 28), Parsed("28 septiembre", spanish));
            Assert.AreEqual(Day(2026, 9, 11), Parsed("fri", spanish), "English always works too");
        }

        // MARK: The live preview beneath the typed field

        private static ToDoDueDatePreview Preview(string input, DateTime? saved = null, bool settled = true) =>
            ToDoDueDatePreviewResolver.Preview(input, saved, settled, Now, English);

        [TestMethod]
        public void PreviewFollowsTheTypedTextNotTheSavedDate()
        {
            DateTime saved = Day(2026, 9, 10);

            Assert.AreEqual(ToDoDueDatePreview.ForDay(Day(2026, 9, 9)), Preview("2d", saved),
                            "typing replaces the stale date immediately");
            Assert.AreEqual(ToDoDueDatePreview.ForDay(Day(2026, 9, 11)), Preview("fri", saved));
            Assert.AreEqual(ToDoDueDatePreview.ForDay(Day(2026, 9, 28)), Preview("28 sep", saved));
        }

        [TestMethod]
        public void PreviewResolvesLiveEvenBeforeTypingSettles()
        {
            Assert.AreEqual(ToDoDueDatePreview.ForDay(Day(2026, 9, 9)), Preview("2d", null, false),
                            "a parse never waits on the settle delay");
        }

        [TestMethod]
        public void PreviewHoldsBackTheHintUntilTypingSettles()
        {
            DateTime saved = Day(2026, 9, 10);

            // "28th" is typed through "2", "28", "28t" — none of the drafts may flash the red hint.
            Assert.AreEqual(ToDoDueDatePreview.ForDay(saved), Preview("28t", saved, false));
            Assert.AreEqual(ToDoDueDatePreview.Blank, Preview("28t", null, false));
            Assert.AreEqual(ToDoDueDatePreview.ForDay(Day(2026, 9, 28)), Preview("28th", saved, false));
        }

        [TestMethod]
        public void PreviewMarksNonsenseOnceTypingSettles()
        {
            Assert.AreEqual(ToDoDueDatePreview.Unrecognised, Preview("soonish", Day(2026, 9, 10)));
            Assert.AreEqual(ToDoDueDatePreview.Unrecognised, Preview("2x"));
            Assert.AreEqual(ToDoDueDatePreview.Unrecognised, Preview("13/13"));
        }

        [TestMethod]
        public void PreviewOfAnEmptyFieldFallsBackToTheSavedDate()
        {
            DateTime saved = Day(2026, 9, 10);

            Assert.AreEqual(ToDoDueDatePreview.ForDay(saved), Preview("", saved),
                            "clearing shows the existing due date, not an error");
            Assert.AreEqual(ToDoDueDatePreview.ForDay(saved), Preview("   ", saved));
            Assert.AreEqual(ToDoDueDatePreview.Blank, Preview("", null));
        }
    }
}
