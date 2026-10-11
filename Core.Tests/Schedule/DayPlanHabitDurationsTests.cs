using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>: habits shortened for one day (report
    /// 2026-09-15_20-29-02) and the typed-length follow-up ("needs to let me type a duration").
    /// </summary>
    [TestClass]
    public class DayPlanHabitDurationsTests
    {
        private const int RunMinutes = 30;
        private const int TrimmedMinutes = 10;
        private static readonly int RealSeconds = RunMinutes * HabitDurationText.SecondsPerMinute;
        private static readonly int TrimmedSeconds = TrimmedMinutes * HabitDurationText.SecondsPerMinute;

        private static readonly DateTimeOffset Monday =
            new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(10));
        private static readonly DateTimeOffset Tuesday = Monday.AddDays(1);

        [TestMethod]
        public void ShorteningAHabitAppliesToThatDayOnly()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();

            Assert.AreEqual(TrimmedSeconds, trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds));
            Assert.AreEqual(TrimmedSeconds, trims.EffectiveSeconds("run", Monday, RealSeconds));
            Assert.AreEqual(RealSeconds, trims.EffectiveSeconds("run", Tuesday, RealSeconds),
                            "Shortening Monday leaves every other day alone");
            Assert.AreEqual(RealSeconds, trims.EffectiveSeconds("stretch", Monday, RealSeconds),
                            "Only the habit that was shortened is affected");
        }

        [TestMethod]
        public void ShorteningNeverChangesTheHabitsRealDuration()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds);

            // The store only ever reports a trim; the real length is whatever the caller passes in.
            Assert.AreEqual(TrimmedSeconds, trims.OverrideSeconds("run", Monday));
            Assert.AreEqual(RealSeconds, trims.EffectiveSeconds("run", Tuesday, RealSeconds));
        }

        [TestMethod]
        public void ShortenedHabitSurvivesAReloadOfTheStore()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds);

            // A fresh store over the persisted map is what a relaunch sees.
            DayPlanHabitDurations reopened = new DayPlanHabitDurations(
                trims.OverridesByDay.ToDictionary(day => day.Key, day => day.Value));

            Assert.AreEqual(TrimmedSeconds, reopened.OverrideSeconds("run", Monday));
        }

        [TestMethod]
        public void ShorteningClearsBackToTheFullDuration()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds);

            trims.ClearOverride("run", Monday);

            Assert.IsNull(trims.OverrideSeconds("run", Monday));
            Assert.AreEqual(RealSeconds, trims.EffectiveSeconds("run", Monday, RealSeconds));
        }

        [TestMethod]
        public void AskingForTheRealLengthOrLongerClearsTheTrimInsteadOfStoringIt()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds);

            Assert.IsNull(trims.SetOverride(RealSeconds, "run", Monday, RealSeconds),
                          "This only ever shortens — asking for the full length removes the trim");
            Assert.IsNull(trims.OverrideSeconds("run", Monday));
        }

        [TestMethod]
        public void ATrimIsNeverShorterThanTheFloorAndNeverLongerThanTheHabit()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();

            Assert.AreEqual(DayPlanHabitDurations.MinimumSeconds,
                            trims.SetOverride(1, "run", Monday, RealSeconds), "Clamped up to the floor");

            // A stored trim longer than the habit (the habit was shortened in settings since) must not
            // lengthen it.
            trims.SetOverride(RealSeconds - 60, "run", Monday, RealSeconds);
            Assert.AreEqual(120, trims.EffectiveSeconds("run", Monday, 120),
                            "Never longer than the habit's real length");
        }

        [TestMethod]
        public void TrimsAreNeverStoredForAnEmptyHabitId()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();

            Assert.IsNull(trims.SetOverride(600, string.Empty, Monday, 1800));
            Assert.IsNull(trims.OverrideSeconds(string.Empty, Monday));
            Assert.IsNull(trims.OverrideSeconds(null, Monday));
        }

        [TestMethod]
        public void OldShortenedDaysArePrunedAwayInDateOrder()
        {
            Dictionary<string, Dictionary<string, int>> map = new Dictionary<string, Dictionary<string, int>>();
            for (int day = 1; day <= DayPlanHabitDurations.RetainedDays + 5; day++)
            {
                map[string.Format(CultureInfo.InvariantCulture, "2026-01-{0:00}", day)] =
                    new Dictionary<string, int> { { "h", 600 } };
            }

            DayPlanDayKey.PruneOldestDays(map, DayPlanHabitDurations.RetainedDays);

            Assert.AreEqual(DayPlanHabitDurations.RetainedDays, map.Count);
            Assert.IsFalse(map.ContainsKey("2026-01-01"), "The oldest days go first");
            Assert.IsTrue(map.ContainsKey(string.Format(CultureInfo.InvariantCulture, "2026-01-{0:00}",
                                                        DayPlanHabitDurations.RetainedDays + 5)));
        }

        [TestMethod]
        public void ShortenOptionsAreOnlyLengthsShorterThanTheHabit()
        {
            CollectionAssert.AreEqual(new List<int> { 20 * 60, 15 * 60, 10 * 60, 5 * 60 },
                                      DayPlanHabitDurations.Options(25 * 60).ToList());
            CollectionAssert.AreEqual(new List<int> { 5 * 60 }, DayPlanHabitDurations.Options(6 * 60).ToList());
            CollectionAssert.AreEqual(new List<int> { DayPlanHabitDurations.MinimumSeconds },
                                      DayPlanHabitDurations.Options(3 * 60).ToList(),
                                      "A habit below the shortest preset still gets the one-minute trim");
            Assert.AreEqual(0, DayPlanHabitDurations.Options(DayPlanHabitDurations.MinimumSeconds).Count,
                            "A habit already at the floor has nothing shorter to offer");
        }

        [TestMethod]
        public void MinutesLabelNeverRoundsARealHabitDownToZero()
        {
            Assert.AreEqual(2, DayPlanHabitDurations.MinutesLabel(90));
            Assert.AreEqual(1, DayPlanHabitDurations.MinutesLabel(20));
            Assert.AreEqual(30, DayPlanHabitDurations.MinutesLabel(30 * 60));
            // Swift rounds halves away from zero; .NET's default is banker's rounding, which would call
            // this 2. The port pins the Swift answer.
            Assert.AreEqual(3, DayPlanHabitDurations.MinutesLabel(150));
        }

        // MARK: - Typing a length

        [TestMethod]
        public void TypedLengthUsesTheDayPlansOwnDurationGrammar()
        {
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(90 * 60),
                            DayPlanHabitDurations.TypedTrimFor("1h30", 2 * 60 * 60));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(90 * 60),
                            DayPlanHabitDurations.TypedTrimFor("90min", 2 * 60 * 60));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(2 * 60 * 60),
                            DayPlanHabitDurations.TypedTrimFor("2 hrs", 3 * 60 * 60));
        }

        [TestMethod]
        public void TypedBareNumberIsMinutes()
        {
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(10 * 60),
                            DayPlanHabitDurations.TypedTrimFor("10", RealSeconds));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(25 * 60),
                            DayPlanHabitDurations.TypedTrimFor(" 25 ", RealSeconds));
        }

        [TestMethod]
        public void TypedLengthLongerThanTheHabitIsRefusedRatherThanLengtheningIt()
        {
            // "4h" on a 30-minute habit: four hours, not four minutes — and this only ever shortens.
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotShorter,
                            DayPlanHabitDurations.TypedTrimFor("4h", RealSeconds));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotShorter,
                            DayPlanHabitDurations.TypedTrimFor("30", RealSeconds),
                            "Exactly the real length is not shorter either");
        }

        [TestMethod]
        public void TypedLengthThatIsntADurationIsRejected()
        {
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotADuration,
                            DayPlanHabitDurations.TypedTrimFor(string.Empty, 1800));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotADuration,
                            DayPlanHabitDurations.TypedTrimFor("soon", 1800));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotADuration,
                            DayPlanHabitDurations.TypedTrimFor("0", 1800));
        }

        [TestMethod]
        public void TypedLengthIsClampedToTheFloorAndStillCantLengthen()
        {
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.Apply(DayPlanHabitDurations.MinimumSeconds),
                            DayPlanHabitDurations.TypedTrimFor("1", 1800));
            Assert.AreEqual(DayPlanHabitDurations.TypedTrim.NotShorter,
                            DayPlanHabitDurations.TypedTrimFor("1", DayPlanHabitDurations.MinimumSeconds),
                            "A habit already at the floor cannot be trimmed further");
        }

        [TestMethod]
        public void ATypedLengthGoesThroughTheSameEffectiveSecondsPathAsAPreset()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            DayPlanHabitDurations.TypedTrim typed = DayPlanHabitDurations.TypedTrimFor("12", RealSeconds);
            Assert.AreEqual(DayPlanHabitDurations.TypedTrimOutcome.Apply, typed.Outcome,
                            "12 minutes is shorter than 30 and should apply");

            trims.SetOverride(typed.Seconds, "run", Monday, RealSeconds);

            Assert.AreEqual(12 * 60, trims.EffectiveSeconds("run", Monday, RealSeconds));
            // The stale-trim guard still holds for a typed value: the habit shrinking in settings wins.
            Assert.AreEqual(5 * 60, trims.EffectiveSeconds("run", Monday, 5 * 60));
        }

        // MARK: - The marker in the day's list

        [TestMethod]
        public void TheListMarkerAndTheActionBarCaptionReadTheSameFact()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            Func<DateTimeOffset, bool> isShortened = day => trims.OverrideSeconds("run", day).HasValue;

            Assert.IsFalse(isShortened(Monday));
            trims.SetOverride(TrimmedSeconds, "run", Monday, RealSeconds);
            Assert.IsTrue(isShortened(Monday));
            Assert.IsFalse(isShortened(Tuesday), "Only the day that was shortened is marked");
            trims.ClearOverride("run", Monday);
            Assert.IsFalse(isShortened(Monday), "Cleared, the marker goes away");
        }

        /// <summary>
        /// A trim is only useful if the schedule actually lays the habit out at the shorter length, and
        /// everything after it moves earlier by the time the trim freed.
        /// </summary>
        [TestMethod]
        public void ShortenedHabitReflowsTheRestOfTheDay()
        {
            DayPlanHabitDurations trims = new DayPlanHabitDurations();
            DateTimeOffset now = Monday;
            trims.SetOverride(TrimmedSeconds, "run", now, RealSeconds);

            // Exactly what the Day Plan composes: the real duration, passed through the trim.
            Func<List<ScheduleHabitInput>> inputs = () => new List<ScheduleHabitInput>
            {
                new ScheduleHabitInput(0, "Run", trims.EffectiveSeconds("run", now, RealSeconds), id: "run"),
                new ScheduleHabitInput(1, "Journal", 10 * 60),
            };

            List<ScheduleBlock> blocks = ScheduleBuilder
                .Build(new[] { new ScheduleRoutineSegment("Morning", inputs(), now, now.AddHours(3), true) },
                       new List<ScheduleMeetingInput>(), now, now.AddHours(9), 0)
                .Where(b => b.Kind == ScheduleBlockKind.Habit).OrderBy(b => b.Start).ToList();

            CollectionAssert.AreEqual(new List<string> { "Run", "Journal" }, blocks.Select(b => b.Title).ToList());
            Assert.AreEqual(TrimmedSeconds, blocks[0].DurationSeconds, "The run is laid out at its trimmed length");
            Assert.AreEqual(now.AddSeconds(TrimmedSeconds), blocks[1].Start,
                            "Everything after it moves earlier by the time the trim freed");

            trims.ClearOverride("run", now);
            List<ScheduleBlock> full = ScheduleBuilder
                .Build(new[] { new ScheduleRoutineSegment("Morning", inputs(), now, now.AddHours(3), true) },
                       new List<ScheduleMeetingInput>(), now, now.AddHours(9), 0)
                .Where(b => b.Kind == ScheduleBlockKind.Habit).OrderBy(b => b.Start).ToList();

            Assert.AreEqual(RealSeconds, full[0].DurationSeconds, "Cleared, the habit is back to its real length");
            Assert.AreEqual(now.AddSeconds(RealSeconds), full[1].Start);
        }
    }
}
