using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the "Resilient list decode (days_of_week int/string mix)" section of the Mac app's
    /// <c>CustomBlockingScheduleTests.swift</c>.
    ///
    /// The regression behind it: the backend returned two schedules, one with
    /// <c>days_of_week: [0…6]</c> and the other with <c>["ALL"]</c>. The whole-array decode threw on
    /// the string element and dropped EVERY custom blocking schedule.
    /// </summary>
    [TestClass]
    public class BlockingScheduleWireTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 16, 12, 0, 0);

        /// <summary>Backend 0-based days [0…6] become calendar weekdays [1…7] (Sunday = 1).</summary>
        private static readonly int[] AllWeekdays = { 1, 2, 3, 4, 5, 6, 7 };

        private static IReadOnlyList<CustomBlockingSchedule> Decode(string json)
        {
            return BlockingScheduleWire.ParseList(json, Now);
        }

        private static int[] SortedDays(CustomBlockingSchedule schedule)
        {
            return schedule.DaysOfWeek.OrderBy(day => day).ToArray();
        }

        // MARK: - The day-of-week convention

        [TestMethod]
        public void TheWireIsZeroBasedNumbersAndTheAppIsOneBased()
        {
            // days_of_week on the wire is Date.getDay(): 0 = Sunday … 6 = Saturday. The app holds
            // Calendar weekdays, 1 = Sunday … 7 = Saturday. This +1 is the whole difference.
            CollectionAssert.AreEqual(AllWeekdays,
                BlockingScheduleDaysOfWeek.ToCalendarWeekdays(BlockingScheduleDaysOfWeek.FullWeekWire).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 6 },
                BlockingScheduleDaysOfWeek.ToWireDays(new[] { 1, 2, 7 }).ToArray());
        }

        [TestMethod]
        public void ThisNumberingIsNotTheMonToSunVocabularyHabitsUse()
        {
            // Two orderings of seven days live in this library and neither may be folded into the
            // other: blocking schedules speak numbers on a `days_of_week` field, habits and managed
            // policies speak "MON".."SUN" string codes on a different field entirely.
            Assert.AreEqual(7, WeekdayCode.All.Count);
            Assert.AreEqual("SUN", WeekdayCode.Sunday);
            Assert.AreEqual(BlockingScheduleDaysOfWeek.WireSunday, 0,
                "Sunday is 0 on the blocking-schedule wire and the string \"SUN\" for a habit");
            Assert.AreEqual(CalendarWeekday.Sunday, 1, "and 1 once it is in memory");
        }

        // MARK: - The shapes days_of_week arrives in

        [TestMethod]
        public void MixedIntAndAllStringDaysDecodeBothSchedules()
        {
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"
                [
                  { ""name"": ""Work"", ""days_of_week"": [0,1,2,3,4,5,6] },
                  { ""name"": ""Sleep Time"", ""days_of_week"": [""ALL""] }
                ]");

            Assert.AreEqual(2, schedules.Count);
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[0]));
            Assert.AreEqual("Sleep Time", schedules[1].Name);
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[1]));
        }

        [TestMethod]
        public void TheAllTokenIsCaseInsensitive()
        {
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": ""S"", ""days_of_week"": [""All""] } ]");
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[0]));
        }

        [TestMethod]
        public void DayNameStringsMapToSpecificWeekdays()
        {
            // ["mon","wed","fri"] → wire 1,3,5 → calendar weekdays 2,4,6
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": ""S"", ""days_of_week"": [""mon"",""wed"",""fri""] } ]");
            CollectionAssert.AreEqual(new[] { 2, 4, 6 }, SortedDays(schedules[0]));
        }

        [TestMethod]
        public void UnrecognizedDayNamesFallBackToTheFullWeek()
        {
            // Never silently disabled: a schedule that blocks too often is recoverable, one that
            // vanished is not.
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": ""S"", ""days_of_week"": [""notaday""] } ]");
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[0]));
        }

        [TestMethod]
        public void APartlyUnderstoodNameListIsTreatedAsNotUnderstood()
        {
            // Enforcing only the half that parsed would be a schedule the user never set.
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": ""S"", ""days_of_week"": [""mon"",""notaday""] } ]");
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[0]));
        }

        [TestMethod]
        public void AnAbsentDaysOfWeekMeansEveryDay()
        {
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": ""S"" } ]");
            CollectionAssert.AreEqual(AllWeekdays, SortedDays(schedules[0]));
        }

        [TestMethod]
        public void AbbreviationsTheBackendSendsAreUnderstood()
        {
            Assert.AreEqual(2, BlockingScheduleDaysOfWeek.ParseWire(JArray.Parse(@"[""tues""]"))[0]);
            Assert.AreEqual(4, BlockingScheduleDaysOfWeek.ParseWire(JArray.Parse(@"[""thurs""]"))[0]);
            Assert.AreEqual(4, BlockingScheduleDaysOfWeek.ParseWire(JArray.Parse(@"[""Thursday""]"))[0]);
        }

        // MARK: - One bad element

        [TestMethod]
        public void OneMalformedElementDoesNotDropTheValidOnes()
        {
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"
                [
                  { ""name"": ""Good"" },
                  ""totally broken"",
                  { ""name"": ""AlsoGood"" }
                ]");

            CollectionAssert.AreEqual(new[] { "Good", "AlsoGood" }, schedules.Select(s => s.Name).ToArray());
        }

        [TestMethod]
        public void AFieldOfTheWrongTypeCostsOnlyThatSchedule()
        {
            // Swift's decodeIfPresent substitutes a default for an ABSENT value, never for one of the
            // wrong type, so a numeric name fails that element and no other.
            IReadOnlyList<CustomBlockingSchedule> schedules = Decode(@"[ { ""name"": 7 }, { ""name"": ""Good"" } ]");
            CollectionAssert.AreEqual(new[] { "Good" }, schedules.Select(s => s.Name).ToArray());
        }

        // MARK: - The rest of the record

        [TestMethod]
        public void MissingFieldsTakeTheDocumentedDefaults()
        {
            CustomBlockingSchedule schedule = Decode(@"[ { } ]")[0];

            Assert.AreEqual(BlockingScheduleWire.DefaultName, schedule.Name);
            Assert.AreEqual(BlockingScheduleWire.DefaultStartTime, schedule.StartTimeString);
            Assert.AreEqual(BlockingScheduleWire.DefaultEndTime, schedule.EndTimeString);
            Assert.AreEqual(string.Empty, schedule.FocusModeId);
            Assert.AreEqual(FocusModeStrictness.Normal, schedule.BlockLevel);
            Assert.IsFalse(schedule.IsAIBlockingEnabled);
            Assert.IsFalse(schedule.IsMicroBreaksEnabled);
            Assert.IsFalse(string.IsNullOrEmpty(schedule.Id), "an id is invented rather than left null");
        }

        [TestMethod]
        public void BlockLevelStringsMapBothWays()
        {
            Assert.AreEqual(FocusModeStrictness.Normal, BlockingScheduleWire.ParseBlockLevel("gentle"));
            Assert.AreEqual(FocusModeStrictness.Strict, BlockingScheduleWire.ParseBlockLevel("strict"));
            Assert.AreEqual(FocusModeStrictness.SuperStrict, BlockingScheduleWire.ParseBlockLevel("super-strict"));
            Assert.AreEqual("super-strict", BlockingScheduleWire.BlockLevelWireValue(FocusModeStrictness.SuperStrict));
        }

        [TestMethod]
        public void AnUnknownBlockLevelIsNotGuessedUpward()
        {
            // Strictness escalates what the user cannot do; a level from a newer backend must land on
            // the gentlest reading, not the harshest.
            Assert.AreEqual(FocusModeStrictness.Normal, BlockingScheduleWire.ParseBlockLevel("apocalyptic"));
            Assert.AreEqual(FocusModeStrictness.Normal, BlockingScheduleWire.ParseBlockLevel(null));
        }

        [TestMethod]
        public void MicroBreaksRideInsideTheMetadataBlob()
        {
            CustomBlockingSchedule enabled = Decode(@"[ { ""metadata"": { ""is_micro_breaks_enabled"": true } } ]")[0];
            Assert.IsTrue(enabled.IsMicroBreaksEnabled);

            CustomBlockingSchedule otherKeys = Decode(@"[ { ""metadata"": { ""something_else"": true } } ]")[0];
            Assert.IsFalse(otherKeys.IsMicroBreaksEnabled);
        }

        [TestMethod]
        public void TimesAreParsedWithOrWithoutSecondsAndLandOnTheReferenceDate()
        {
            CustomBlockingSchedule schedule = Decode(@"[ { ""start_time"": ""09:30:15"", ""end_time"": ""18:00"" } ]")[0];
            Assert.AreEqual(new TimeSpan(9, 30, 15), schedule.StartTime.TimeOfDay);
            Assert.AreEqual(new TimeSpan(18, 0, 0), schedule.EndTime.TimeOfDay);
            Assert.AreEqual(Now.Date, schedule.StartTime.Date, "only the time of day is meaningful");
        }

        [TestMethod]
        public void AWindowThatEndsBeforeItStartsIsPushedToTheNextDay()
        {
            CustomBlockingSchedule schedule = Decode(@"[ { ""start_time"": ""22:00"", ""end_time"": ""02:00"" } ]")[0];
            Assert.AreEqual(Now.Date.AddDays(1).AddHours(2), schedule.EndTime);
        }

        [TestMethod]
        public void AnUnparseableTimeFallsBackToTheReferenceInstant()
        {
            CustomBlockingSchedule schedule = Decode(@"[ { ""start_time"": ""half past nine"" } ]")[0];
            Assert.AreEqual(Now, schedule.StartTime);
        }
    }
}
