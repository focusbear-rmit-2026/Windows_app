using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    [TestClass]
    public class DayPlanDurationsSeamTests
    {
        [TestMethod]
        public void TheFieldReadsHoursAsHours()
        {
            // The field kept only its digits: "1h30" was 130 minutes, "4h" was 4.
            Assert.AreEqual(90, DayPlanDurations.FromFieldText("1h30"));
            Assert.AreEqual(240, DayPlanDurations.FromFieldText("4h"));
        }

        [TestMethod]
        public void WhatTheFieldAlreadyAcceptedStillWorks()
        {
            Assert.AreEqual(45, DayPlanDurations.FromFieldText("45"));
            Assert.AreEqual(45, DayPlanDurations.FromFieldText(" 45m "));
            Assert.AreEqual(24 * 60, DayPlanDurations.FromFieldText("5000"), "still clamped to a day");
        }

        [TestMethod]
        public void NothingReadableLeavesTheDurationAlone()
        {
            Assert.IsNull(DayPlanDurations.FromFieldText(""));
            Assert.IsNull(DayPlanDurations.FromFieldText(null));
            Assert.IsNull(DayPlanDurations.FromFieldText("0"));
            Assert.IsNull(DayPlanDurations.FromFieldText("soon"));
        }

        [TestMethod]
        public void AnImportedDurationIsSecondsInEitherShape()
        {
            Assert.AreEqual(5, DayPlanDurations.FromTodoDuration(JToken.Parse("300")));
            Assert.AreEqual(5, DayPlanDurations.FromTodoDuration(new JValue("300")), "the API sends a numeric string");
        }

        [TestMethod]
        public void AFractionalDurationNoLongerFallsBackToFifteen()
        {
            Assert.AreEqual(5, DayPlanDurations.FromTodoDuration(new JValue(300.5)));
            Assert.AreEqual(60, DayPlanDurations.FromTodoDuration(JToken.Parse("3600.0")));
        }

        [TestMethod]
        public void NoDurationIsTheFifteenMinuteDefault()
        {
            Assert.AreEqual(15, DayPlanDurations.FromTodoDuration(null));
            Assert.AreEqual(15, DayPlanDurations.FromTodoDuration(JValue.CreateNull()));
            Assert.AreEqual(15, DayPlanDurations.FromTodoDuration(new JValue("0")));
        }

        [TestMethod]
        public void ASubMinuteDurationIsStillAMinute()
        {
            Assert.AreEqual(1, DayPlanDurations.FromTodoDuration(new JValue(30)));
        }

        // The tasks panel's duration is now an inline field (report 2026-09-28_20-58-44: "It doesn't let me change the
        // duration. When I click/tab it doesn't open an input box"). Mac: DayPlanTasks.commitDuration.

        [TestMethod]
        public void TheRowFieldShowsMinutesWithAnM()
        {
            Assert.AreEqual("15m", DayPlanDurations.RowLabel(15));
            Assert.AreEqual("90m", DayPlanDurations.RowLabel(90));
        }

        [TestMethod]
        public void TheRowFieldSavesWhatIsTyped()
        {
            Assert.AreEqual(30, DayPlanDurations.CommitRowField("30", 15));
            Assert.AreEqual(60, DayPlanDurations.CommitRowField("1h", 15));
            Assert.AreEqual(45, DayPlanDurations.CommitRowField("45m", 15));
            Assert.AreEqual(15, DayPlanDurations.CommitRowField("15m", 15), "left as shown");
        }

        [TestMethod]
        public void AnUnreadableRowFieldKeepsTheDuration()
        {
            Assert.AreEqual(25, DayPlanDurations.CommitRowField("", 25));
            Assert.AreEqual(25, DayPlanDurations.CommitRowField("soon", 25));
            Assert.AreEqual(25, DayPlanDurations.CommitRowField(null, 25));
            Assert.AreEqual(25, DayPlanDurations.CommitRowField("0", 25));
        }
    }
}
