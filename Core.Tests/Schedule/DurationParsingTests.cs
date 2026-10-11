using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>'s to-do <c>duration</c> section: the API renders
    /// <c>duration</c> as a STRING ("300"), so the old integer cast always failed and every imported
    /// to-do fell back to 15 minutes — inflating the Day Plan's totals.
    /// </summary>
    [TestClass]
    public class TodoFieldParsingTests
    {
        [TestMethod]
        public void DurationParsesTheStringTheApiActuallySends()
        {
            Assert.AreEqual(300, TodoFieldParsing.DurationSeconds("300"));
            Assert.AreEqual(5, TodoFieldParsing.DurationMinutes("300"));
            Assert.AreEqual(60, TodoFieldParsing.DurationMinutes("3600"));
        }

        [TestMethod]
        public void DurationStillParsesNumbers()
        {
            Assert.AreEqual(5, TodoFieldParsing.DurationMinutes(300));
            Assert.AreEqual(15, TodoFieldParsing.DurationMinutes(900L));
            Assert.AreEqual(5, TodoFieldParsing.DurationMinutes(300.0));
        }

        /// <summary>Unset / unparseable durations fall back to the 15-minute default, and a sub-minute
        /// duration still reads as at least a minute.</summary>
        [TestMethod]
        public void DurationFallsBackWhenUnset()
        {
            Assert.AreEqual(15, TodoFieldParsing.DurationMinutes("0"));
            Assert.AreEqual(15, TodoFieldParsing.DurationMinutes(null));
            Assert.AreEqual(15, TodoFieldParsing.DurationMinutes("not a number"));
            Assert.AreEqual(1, TodoFieldParsing.DurationMinutes("30"), "30s rounds up to a minute, not zero");
        }

        [TestMethod]
        public void ACallerCanAskForItsOwnFallback()
        {
            Assert.AreEqual(25, TodoFieldParsing.DurationMinutes(null, 25));
        }
    }

    /// <summary>
    /// The Day Plan's shared duration grammar. Its bug: the duration fields filtered out everything but
    /// digits, so "4h" became 4 minutes and "1h30" became 130 (report 2026-07-15_20-25-27).
    /// </summary>
    [TestClass]
    public class DurationFieldParserTests
    {
        [TestMethod]
        public void EveryFormAPersonTypesMeansWhatItSays()
        {
            Assert.AreEqual(90, DurationFieldParser.DurationMinutesFromFieldText("1h30"));
            Assert.AreEqual(90, DurationFieldParser.DurationMinutesFromFieldText("90min"));
            Assert.AreEqual(120, DurationFieldParser.DurationMinutesFromFieldText("2 hrs"));
            Assert.AreEqual(240, DurationFieldParser.DurationMinutesFromFieldText("4h"),
                            "four hours, not four minutes");
            Assert.AreEqual(45, DurationFieldParser.DurationMinutesFromFieldText("45m"));
        }

        [TestMethod]
        public void ABareNumberInAFieldIsMinutes()
        {
            Assert.AreEqual(25, DurationFieldParser.DurationMinutesFromFieldText(" 25 "));
            Assert.AreEqual(10, DurationFieldParser.DurationMinutesFromFieldText("10"));
        }

        [TestMethod]
        public void NothingThatReadsAsALengthIsRejected()
        {
            Assert.IsNull(DurationFieldParser.DurationMinutesFromFieldText(string.Empty));
            Assert.IsNull(DurationFieldParser.DurationMinutesFromFieldText("soon"));
            Assert.IsNull(DurationFieldParser.DurationMinutesFromFieldText("0"));
            Assert.IsNull(DurationFieldParser.DurationMinutesFromFieldText(null));
        }

        /// <summary>
        /// A token buried in a TITLE must stand alone, so "swimming" is not read as a length — but a
        /// bare number in a title is left alone too, which is exactly why the field has its own rule.
        /// </summary>
        [TestMethod]
        public void ATokenInATitleMustStandAloneAndIsStrippedOut()
        {
            DurationFieldParser.DurationToken token = DurationFieldParser.ExtractDurationToken("write report 1h30");
            Assert.AreEqual(90, token.Minutes);
            Assert.AreEqual("write report", token.Title);

            Assert.IsNull(DurationFieldParser.ExtractDurationToken("go swimming").Minutes);
            Assert.IsNull(DurationFieldParser.ExtractDurationToken("call Bob 5").Minutes,
                          "a bare number in a title is part of the title");
            Assert.IsNull(DurationFieldParser.ExtractDurationToken("stretch 0m").Minutes,
                          "zero is not a duration, so it stays in the title");
        }

        [TestMethod]
        public void TheLastTokenWins()
        {
            Assert.AreEqual(20, DurationFieldParser.ExtractDurationToken("10m of warm up 20m").Minutes);
        }

        [TestMethod]
        public void ATypedLengthIsClampedToADay()
        {
            Assert.AreEqual(DurationFieldParser.MaxDurationMinutes,
                            DurationFieldParser.DurationMinutesFromFieldText("9999"));
        }
    }

    /// <summary>Compact duration labels. Seconds matter — a 1-second "log it" habit used to read "0m".</summary>
    [TestClass]
    public class HabitDurationTextTests
    {
        [TestMethod]
        public void ShortLabelsKeepTheUnitThatMatters()
        {
            Assert.AreEqual("45s", HabitDurationText.Short(45));
            Assert.AreEqual("1m 30s", HabitDurationText.Short(90));
            Assert.AreEqual("5m", HabitDurationText.Short(300));
            Assert.AreEqual("1h 5m", HabitDurationText.Short(3900));
            Assert.AreEqual("1s", HabitDurationText.Short(1), "a one-second habit must not read as 0m");
            Assert.AreEqual("0s", HabitDurationText.Short(0));
        }
    }
}
