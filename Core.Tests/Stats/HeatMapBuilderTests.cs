using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>Ported from the heat-map half of the Mac's StatsLogicTests.</summary>
    [TestClass]
    public class HeatMapBuilderTests
    {
        private static DateTime Day(int year, int month, int day)
        {
            return new DateTime(year, month, day);
        }

        private static IList<string> AllDayKeys(HeatMapModel model)
        {
            return model.Columns.SelectMany(column => column).Select(cell => cell.DayKey).ToList();
        }

        // Wed 26 Aug → Tue 8 Sep 2026, "today" = Sun 6 Sep: starts mid-week and crosses a month boundary.
        [TestMethod]
        public void GridPadsToWholeWeeksAndGivesEveryDayACell()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>
            {
                { StatsDates.DayKey(Day(2026, 8, 26)), 2 },
                { StatsDates.DayKey(Day(2026, 9, 1)), 1 }
            };

            HeatMapModel model = HeatMapBuilder.Grid(Day(2026, 8, 26), Day(2026, 9, 8), Day(2026, 9, 6), counts);

            Assert.AreEqual(3, model.Columns.Count);
            Assert.IsTrue(model.Columns.All(column => column.Count == HeatMapBuilder.DaysInWeek));
            Assert.AreEqual(Day(2026, 8, 23), model.Columns[0][0].Date, "columns start on Sunday, like the web dashboard's");
            Assert.AreEqual(Day(2026, 9, 12), model.Columns[2][6].Date);
            Assert.AreEqual(DayOfWeek.Sunday, model.Columns[0][0].Date.DayOfWeek);

            Assert.AreEqual(21, AllDayKeys(model).Distinct().Count(), "every day appears exactly once");
            Assert.AreEqual(2, model.Columns[0][3].Count);
            Assert.AreEqual(1, model.Columns[1][2].Count);
            Assert.AreEqual(2, model.MaxCount);
        }

        [TestMethod]
        public void GridMarksDaysAfterTodayAsFuture()
        {
            HeatMapModel model = HeatMapBuilder.Grid(Day(2026, 8, 26), Day(2026, 9, 8), Day(2026, 9, 6),
                new Dictionary<string, int>());

            string[] futureKeys = model.Columns.SelectMany(column => column)
                .Where(cell => cell.IsFuture).Select(cell => cell.DayKey).ToArray();

            CollectionAssert.AreEqual(Enumerable.Range(7, 6).Select(day => StatsDates.DayKey(Day(2026, 9, day))).ToArray(),
                futureKeys);
            Assert.IsFalse(model.Columns[2][0].IsFuture, "today itself is not future");
        }

        [TestMethod]
        public void MonthLabelSitsOnTheWeekContainingTheFirst()
        {
            HeatMapModel model = HeatMapBuilder.Grid(Day(2026, 8, 26), Day(2026, 9, 8), Day(2026, 9, 6),
                new Dictionary<string, int>());

            CollectionAssert.AreEqual(new[] { 1 }, model.MonthLabels.Select(label => label.WeekIndex).ToArray(),
                "Aug would overlap Sep on the adjacent column");
            Assert.AreEqual(StatsDates.Format(Day(2026, 9, 1), "MMM"), model.MonthLabels[0].Label);
        }

        [TestMethod]
        public void FirstColumnGetsItsOwnMonthWhenThereIsRoom()
        {
            HeatMapModel model = HeatMapBuilder.Grid(Day(2026, 8, 16), Day(2026, 9, 8), Day(2026, 9, 6),
                new Dictionary<string, int>());

            Assert.AreEqual(4, model.Columns.Count);
            CollectionAssert.AreEqual(new[] { 0, 2 }, model.MonthLabels.Select(label => label.WeekIndex).ToArray());
            CollectionAssert.AreEqual(
                new[] { StatsDates.Format(Day(2026, 8, 1), "MMM"), StatsDates.Format(Day(2026, 9, 1), "MMM") },
                model.MonthLabels.Select(label => label.Label).ToArray());
        }

        [TestMethod]
        public void BuildCoversFiveWeeksBackPlusTheCurrentWeek()
        {
            DateTime today = Day(2026, 9, 4);   // a Friday

            HeatMapModel model = HeatMapBuilder.Build(new ActivityStatItem[0], null, today);

            Assert.AreEqual(HeatMapBuilder.WeeksToShow + 1, model.Columns.Count);
            Assert.AreEqual(Day(2026, 7, 26), model.Columns[0][0].Date);
            Assert.AreEqual(Day(2026, 9, 5), model.Columns.Last().Last().Date);
            Assert.IsTrue(StatsCalendar.DaysBetween(model.Columns[0][0].Date, today) + 1 <= HeatMapBuilder.FetchDays,
                "the request must cover the whole grid");
        }

        [TestMethod]
        public void BuildStartsAtTheJoinWeekForANewAccount()
        {
            HeatMapModel model = HeatMapBuilder.Build(new ActivityStatItem[0], Day(2026, 8, 20), Day(2026, 9, 4));

            Assert.AreEqual(Day(2026, 8, 16), model.Columns[0][0].Date,
                "a user who joined three weeks ago gets three columns, not six mostly-empty ones");
        }

        /// <summary>
        /// Per-day stat rows are serialised as UTC midnight, so the day a row names is its UTC
        /// calendar day. Reading it in local time shifts half the user's history by one square.
        /// </summary>
        [TestMethod]
        public void CountsAreKeyedByTheServersUtcDayNotTheLocalOne()
        {
            // 23:30 on the 7th in a +10 zone is still the 7th in UTC — the day the server meant.
            ActivityStatItem item = new ActivityStatItem
            {
                Date = new DateTimeOffset(2026, 9, 8, 9, 30, 0, TimeSpan.FromHours(10)),
                Count = 3
            };

            IDictionary<string, int> counts = HeatMapBuilder.CountsByDay(new[] { item });

            Assert.AreEqual(3, counts["2026-09-07"]);
            Assert.IsFalse(counts.ContainsKey("2026-09-08"));
        }

        [TestMethod]
        public void CountsForTheSameDayAreSummed()
        {
            ActivityStatItem[] items =
            {
                new ActivityStatItem { Date = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), Count = 2 },
                new ActivityStatItem { Date = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), Count = 3 },
                new ActivityStatItem { Date = null, Count = 9 }
            };

            IDictionary<string, int> counts = HeatMapBuilder.CountsByDay(items);

            Assert.AreEqual(5, counts["2026-09-07"]);
            Assert.AreEqual(1, counts.Count, "a row with no date is dropped rather than counted against today");
        }

        [TestMethod]
        public void IntensityIsTheQuartileOfTheBusiestDay()
        {
            Assert.AreEqual(0, HeatMapBuilder.IntensityLevel(0, 8), "nothing completed is not an intensity");
            Assert.AreEqual(1, HeatMapBuilder.IntensityLevel(2, 8));
            Assert.AreEqual(2, HeatMapBuilder.IntensityLevel(4, 8));
            Assert.AreEqual(3, HeatMapBuilder.IntensityLevel(6, 8));
            Assert.AreEqual(4, HeatMapBuilder.IntensityLevel(8, 8));
            Assert.AreEqual(4, HeatMapBuilder.IntensityLevel(3, 0), "a zero max must not divide by zero");
        }
    }

    /// <summary>
    /// Ported from the per-level artwork half of the Mac's StatsLogicTests. The assertion that each
    /// name loads from the asset catalog cannot come across — there is no catalog here — so only the
    /// keying rule does.
    /// </summary>
    [TestClass]
    public class LevelArtworkTests
    {
        /// <summary>
        /// Mirrors <c>getLevelIconAndName</c> in the web dashboard, where the level is a direct index
        /// into [Novice, Apprentice, Explorer, Disciplined, Diligent, Strategist, Mentor, Scholar,
        /// Guardian, Achiever]. Level 6 = Mentor is the case in the bug report's screenshot.
        /// </summary>
        [TestMethod]
        public void ArtworkIsKeyedByLevelIndexTheSameWayTheWebDashboardKeysIt()
        {
            Assert.AreEqual("levelNovice", LevelArtwork.AssetName(0));
            Assert.AreEqual("levelMentor", LevelArtwork.AssetName(6));
            Assert.AreEqual("levelAchiever", LevelArtwork.AssetName(LevelArtwork.MaxLevel));
        }

        [TestMethod]
        public void EveryLevelUpToTheMaxHasArtworkMapped()
        {
            for (int level = 0; level <= LevelArtwork.MaxLevel; level++)
            {
                Assert.IsNotNull(LevelArtwork.AssetName(level), "level " + level + " has no artwork mapped");
            }
        }

        [TestMethod]
        public void AnUnknownLevelHasNoArtworkSoTheViewCanFallBack()
        {
            Assert.IsNull(LevelArtwork.AssetName(-1));
            Assert.IsNull(LevelArtwork.AssetName(LevelArtwork.MaxLevel + 1));
        }
    }
}
