using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// The habit-stats maths behind the Stats screen. Two of these assert behaviour that reads like
    /// a bug and is not — the streak anchored on yesterday, and a 31-day window over a 30-day
    /// denominator. Both are the web dashboard's arithmetic, and a client that "fixed" either would
    /// simply disagree with the website for the same user.
    /// </summary>
    [TestClass]
    public class HabitStatsPortTests
    {
        private static readonly DateTime Today = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        private static ActivityStatItem Day(int daysAgo, int count = 1, double summary = 0)
        {
            DateTime date = StatsCalendar.AddDays(-daysAgo, Today);
            return new ActivityStatItem
            {
                Date = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
                Count = count,
                Summary = summary,
            };
        }

        // ---- HabitConsistencyRanking -----------------------------------------------------------

        [TestMethod]
        public void AHabitYoungerThanTheWindowIsScoredAgainstTheDaysItHasExisted()
        {
            DateTime createdTenDaysAgo = StatsCalendar.AddDays(-9, Today);
            Assert.AreEqual(10, HabitConsistencyRanking.WindowDays(createdTenDaysAgo, Today),
                            "inclusive of both ends");
            Assert.AreEqual(HabitConsistencyRanking.WindowMaxDays,
                            HabitConsistencyRanking.WindowDays(StatsCalendar.AddDays(-500, Today), Today),
                            "an old habit is capped at the window");
            Assert.AreEqual(HabitConsistencyRanking.WindowMaxDays,
                            HabitConsistencyRanking.WindowDays(null, Today),
                            "unknown creation date assumes the whole window");
            Assert.AreEqual(1, HabitConsistencyRanking.WindowDays(Today, Today),
                            "created today is one day, never zero — it is a denominator");
        }

        [TestMethod]
        public void PercentagesAreClampedAndRoundedAwayFromZero()
        {
            var activities = new[] { new ConsistencyInput { ActivityId = "a", CreatedAt = StatsCalendar.AddDays(-89, Today) } };

            // 45/90 = 50%. 46/90 = 51.11 -> 51.
            Assert.AreEqual(50, HabitConsistencyRanking.Build(activities, new Dictionary<string, int> { { "a", 45 } }, Today)[0].Percentage);
            Assert.AreEqual(51, HabitConsistencyRanking.Build(activities, new Dictionary<string, int> { { "a", 46 } }, Today)[0].Percentage);
            // More completions than days in the window cannot report over 100%.
            Assert.AreEqual(100, HabitConsistencyRanking.Build(activities, new Dictionary<string, int> { { "a", 200 } }, Today)[0].Percentage);
        }

        [TestMethod]
        public void AHabitWithNoStatsEntryIsDroppedNotShownAsZero()
        {
            var activities = new[]
            {
                new ConsistencyInput { ActivityId = "known" },
                new ConsistencyInput { ActivityId = "nodata" },
            };
            var ranked = HabitConsistencyRanking.Build(activities, new Dictionary<string, int> { { "known", 10 } }, Today);

            Assert.AreEqual(1, ranked.Count, "\"no data\" and \"never did it\" are different claims");
            Assert.AreEqual("known", ranked[0].ActivityId);
        }

        [TestMethod]
        public void TheLeastConsistentSectionOnlyAppearsBeyondTheTopFive()
        {
            Assert.IsFalse(HabitConsistencyRanking.SplitRanked(Ranked(5)).ShowsLeastSection,
                           "five habits are all 'most consistent'; a second list would repeat them");
            Assert.IsTrue(HabitConsistencyRanking.SplitRanked(Ranked(6)).ShowsLeastSection);
            Assert.IsFalse(HabitConsistencyRanking.SplitRanked(Ranked(0)).ShowsLeastSection);
        }

        [TestMethod]
        public void TheLeastConsistentListIsReversedSoTheWorstComesFirst()
        {
            // Ranked 100..93 descending; the top five are 100..96, leaving 95,94,93 — shown 93,94,95.
            var split = HabitConsistencyRanking.SplitRanked(Ranked(8));

            Assert.AreEqual(5, split.MostConsistent.Count);
            CollectionAssert.AreEqual(new[] { 100, 99, 98, 97, 96 },
                                      split.MostConsistent.Select(i => i.Percentage).ToArray());
            CollectionAssert.AreEqual(new[] { 93, 94, 95 },
                                      split.LeastConsistent.Select(i => i.Percentage).ToArray(),
                                      "the reversal is the point of the second list");
        }

        private static IReadOnlyList<HabitConsistencyItem> Ranked(int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => new HabitConsistencyItem("h" + i, 100 - i, 100, 100 - i))
                .ToList();
        }

        // ---- HabitStatSummary ------------------------------------------------------------------

        [TestMethod]
        public void TheStreakIsAnchoredOnYesterdayNotToday()
        {
            var onlyToday = new[] { Day(0) };
            Assert.AreEqual(0, HabitStatSummary.Compute(onlyToday, Today).CurrentStreak,
                            "a habit done only today reads 0 — the dashboard does the same, and a " +
                            "client that ticked up immediately would disagree with the web all day");

            var threeEndingYesterday = new[] { Day(1), Day(2), Day(3) };
            Assert.AreEqual(3, HabitStatSummary.Compute(threeEndingYesterday, Today).CurrentStreak);
        }

        [TestMethod]
        public void AGapBreaksTheStreak()
        {
            var withGap = new[] { Day(1), Day(2), Day(4), Day(5) };
            Assert.AreEqual(2, HabitStatSummary.Compute(withGap, Today).CurrentStreak);
        }

        [TestMethod]
        public void HoursSpentIsSecondsToOneDecimal()
        {
            var items = new[] { Day(1, summary: 3600), Day(2, summary: 1800) };
            Assert.AreEqual(1.5, HabitStatSummary.Compute(items, Today).HoursSpent, 0.0001);
        }

        [TestMethod]
        public void TotalCompletionsSumsEveryDaysCount()
        {
            var items = new[] { Day(1, count: 2), Day(2, count: 3) };
            Assert.AreEqual(5, HabitStatSummary.Compute(items, Today).TotalCompletions);
        }

        [TestMethod]
        public void TheThirtyDayWindowExaminesThirtyOneDaysAndCanExceedOneHundredPercent()
        {
            // Offsets 0..30 inclusive over a denominator of 30. This is the web's arithmetic; it is
            // asserted so nobody "tidies" it into disagreeing with the dashboard.
            var everyDay = Enumerable.Range(0, 31).Select(d => Day(d)).ToArray();
            Assert.AreEqual(103, HabitStatSummary.Compute(everyDay, Today).Last30DaysPercentage);
        }

        [TestMethod]
        public void AnEmptyHistoryIsAllZeroes()
        {
            var summary = HabitStatSummary.Compute(new ActivityStatItem[0], Today);
            Assert.AreEqual(0, summary.TotalCompletions);
            Assert.AreEqual(0, summary.CurrentStreak);
            Assert.AreEqual(0.0, summary.HoursSpent, 0.0001);
            Assert.AreEqual(0, summary.Last30DaysPercentage);
        }

        // ---- LogQuantityGraph ------------------------------------------------------------------

        [TestMethod]
        public void GraphPointsAreOldestFirstWhateverOrderTheyArrivedIn()
        {
            var points = LogQuantityGraph.Points(new[] { Day(1, summary: 10), Day(5, summary: 50), Day(3, summary: 30) });

            CollectionAssert.AreEqual(new[] { 50.0, 30.0, 10.0 }, points.Select(p => p.Value).ToArray());
        }

        [TestMethod]
        public void AnItemWithNoDateIsSkippedRatherThanPlottedAtTheEpoch()
        {
            var points = LogQuantityGraph.Points(new[]
            {
                Day(1, summary: 10),
                new ActivityStatItem { Date = null, Count = 1, Summary = 99 },
            });

            Assert.AreEqual(1, points.Count);
            Assert.AreEqual(10.0, points[0].Value, 0.0001);
        }

        // ---- ProductivityPeriods ---------------------------------------------------------------

        [TestMethod]
        public void BucketStartCollapsesToTheDayMonthOrYear()
        {
            var instant = new DateTime(2026, 3, 15, 13, 45, 30, DateTimeKind.Utc);

            Assert.AreEqual(new DateTime(2026, 3, 15), ProductivityPeriods.BucketStart(ProductivityPeriod.Daily, instant));
            Assert.AreEqual(new DateTime(2026, 3, 1), ProductivityPeriods.BucketStart(ProductivityPeriod.Monthly, instant));
            Assert.AreEqual(new DateTime(2026, 1, 1), ProductivityPeriods.BucketStart(ProductivityPeriod.Yearly, instant));
        }

        [TestMethod]
        public void TheFocusLogCarriesTheYearOnDailyRowsAndTheChartDoesNot()
        {
            Assert.AreEqual("MMM dd", ProductivityPeriods.LabelPattern(ProductivityPeriod.Daily));
            Assert.AreEqual("MMM dd, yyyy", ProductivityPeriods.FocusLogLabelPattern(ProductivityPeriod.Daily));
            // Only daily differs between the two.
            Assert.AreEqual(ProductivityPeriods.LabelPattern(ProductivityPeriod.Monthly),
                            ProductivityPeriods.FocusLogLabelPattern(ProductivityPeriod.Monthly));
        }

        // ---- HumanizedFocusTime ----------------------------------------------------------------

        [TestMethod]
        public void UnderAnHourStaysInMinutes()
        {
            var humanized = HumanizedFocusTime.From(47.4);
            Assert.AreEqual(HumanizedFocusTime.Unit.Minutes, humanized.TimeUnit);
            Assert.AreEqual(47.0, humanized.Value, 0.0001);
        }

        [TestMethod]
        public void BetweenOneAndFortyEightHoursReadsAsHoursToOneDecimal()
        {
            var humanized = HumanizedFocusTime.From(195);          // 3.25h
            Assert.AreEqual(HumanizedFocusTime.Unit.Hours, humanized.TimeUnit);
            Assert.AreEqual(3.3, humanized.Value, 0.0001);

            // 47 hours is still hours — the threshold is 48, not 24.
            Assert.AreEqual(HumanizedFocusTime.Unit.Hours, HumanizedFocusTime.From(47 * 60).TimeUnit);
        }

        [TestMethod]
        public void FortyEightHoursAndBeyondBecomesDaysPlusLeftoverHours()
        {
            var humanized = HumanizedFocusTime.From(53 * 60);      // 2 days, 5 hours
            Assert.AreEqual(HumanizedFocusTime.Unit.Days, humanized.TimeUnit);
            Assert.AreEqual(2.0, humanized.Value, 0.0001);
            Assert.AreEqual(5, humanized.RemainingHours);
        }

        [TestMethod]
        public void TheBoundariesThemselves()
        {
            Assert.AreEqual(HumanizedFocusTime.Unit.Minutes, HumanizedFocusTime.From(59.9).TimeUnit);
            Assert.AreEqual(HumanizedFocusTime.Unit.Hours, HumanizedFocusTime.From(60).TimeUnit);
            Assert.AreEqual(HumanizedFocusTime.Unit.Days, HumanizedFocusTime.From(48 * 60).TimeUnit);
            Assert.AreEqual(0.0, HumanizedFocusTime.From(0).Value, 0.0001);
        }
    }
}
