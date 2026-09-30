using System;
using System.Globalization;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>Ported from the parseSnoozeDate half of the Mac's <c>SmartSnoozeTests.swift</c>, plus the Windows phrasings kept.</summary>
    [TestClass]
    public class DayPlanSnoozeParserTests
    {
        /// <summary>Tuesday 15 September 2026, as on the Mac.</summary>
        private static readonly DateTime September15 = new DateTime(2026, 9, 15);

        private static DateTime? Parsed(string input, DateTime? from = null) =>
            DayPlanSnoozeParser.Parse(input, from ?? September15, CultureInfo.GetCultureInfo("en-AU"));

        private static DateTime D(int year, int month, int day) => new DateTime(year, month, day);

        [TestMethod]
        public void AWeekdayWithAnExplicitDayNumberDoesNotSkipToday()
        {
            Assert.AreEqual(D(2026, 9, 18), Parsed("fri 18th", D(2026, 9, 18)));
            Assert.AreEqual(D(2026, 9, 18), Parsed("Friday 18th"));
            Assert.AreEqual(D(2026, 9, 18), Parsed("Fri, 18"));
            Assert.AreEqual(D(2026, 9, 25), Parsed("fri", D(2026, 9, 18)));
            Assert.IsNull(Parsed("fri 19th"), "a conflicting day number must not be silently ignored");
        }

        [TestMethod]
        public void AnExplicitWeekdayDateCanMoveASaturdayTaskBackToFriday()
        {
            foreach (var input in new[] { "fri 18th", "fri 18", "Friday 18th" })
                Assert.AreEqual(D(2026, 9, 18), Parsed(input, D(2026, 9, 19)));
            Assert.IsNull(Parsed("fri 31st", D(2026, 9, 19)));
        }

        [TestMethod]
        public void ABareDayNumberIsThatDayOfTheCurrentMonth()
        {
            Assert.AreEqual(D(2026, 9, 23), Parsed("23"));
            Assert.AreEqual(D(2026, 9, 23), Parsed("23rd"));
            Assert.AreEqual(D(2026, 9, 23), Parsed(" 23RD "));
            Assert.AreEqual(D(2026, 9, 1), Parsed("1st", D(2026, 9, 1)));
            Assert.AreEqual(D(2026, 9, 2), Parsed("2nd", D(2026, 9, 1)));
        }

        [TestMethod]
        public void ADayAlreadyPastThisMonthIsNextMonth_AndOneTheMonthLacksRollsOn()
        {
            Assert.AreEqual(D(2026, 10, 3), Parsed("3"));
            Assert.AreEqual(D(2026, 10, 14), Parsed("14th"));
            Assert.AreEqual(D(2027, 1, 5), Parsed("5", D(2026, 12, 20)));
            Assert.AreEqual(D(2027, 3, 31), Parsed("31", D(2027, 2, 1)));
            Assert.AreEqual(D(2027, 3, 30), Parsed("30", D(2027, 2, 1)));
            Assert.AreEqual(D(2028, 2, 29), Parsed("29", D(2028, 2, 1)));
        }

        [TestMethod]
        public void NumbersThatAreNotDaysOfTheMonthAreRejected()
        {
            Assert.IsNull(Parsed("0"));
            Assert.IsNull(Parsed("32"));
            Assert.IsNull(Parsed("99"));
        }

        [TestMethod]
        public void TheEstablishedGrammarsParseAsOnTheMac()
        {
            Assert.AreEqual(September15, Parsed("today"));
            Assert.AreEqual(September15, Parsed("tod"));
            Assert.AreEqual(D(2026, 9, 16), Parsed("tom"));
            Assert.AreEqual(D(2026, 9, 16), Parsed("tomorrow"));
            Assert.AreEqual(D(2026, 9, 14), Parsed("yesterday"));
            Assert.AreEqual(D(2026, 9, 14), Parsed("-1d"));
            Assert.AreEqual(D(2026, 9, 17), Parsed("2d"));
            Assert.AreEqual(D(2026, 9, 29), Parsed("2w"));
            Assert.AreEqual(D(2026, 12, 15), Parsed("3mo"));
            Assert.AreEqual(D(2027, 9, 15), Parsed("1y"));
            Assert.AreEqual(D(2026, 9, 22), Parsed("tue"), "next occurrence, strictly after today");
            Assert.AreEqual(D(2026, 9, 18), Parsed("fri"));
            Assert.AreEqual(D(2027, 7, 1), Parsed("1 Jul"), "a date already past rolls forward a year");
            Assert.AreEqual(D(2026, 12, 1), Parsed("1 Dec"));
            Assert.IsNull(Parsed(""));
            Assert.IsNull(Parsed("   "));
            Assert.IsNull(Parsed("not a date"));
        }

        [TestMethod]
        public void WhatWindowsAlreadyAcceptedStillWorks()
        {
            Assert.AreEqual(D(2026, 9, 22), Parsed("next tue"), "the old help text advertised it");
            Assert.AreEqual(D(2026, 9, 18), Parsed("this fri"));
            Assert.AreEqual(D(2026, 9, 22), Parsed("next week"));
            Assert.AreEqual(D(2026, 10, 15), Parsed("next month"));
            Assert.AreEqual(D(2026, 9, 17), Parsed("2 days"));
            Assert.AreEqual(D(2026, 10, 1), Parsed("1 Oct"));
        }
    }
}
