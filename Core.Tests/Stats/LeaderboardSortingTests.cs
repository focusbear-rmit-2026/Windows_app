using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>Ported from the leaderboard half of the Mac's StatsLogicTests.</summary>
    [TestClass]
    public class LeaderboardSortingTests
    {
        private const double DefaultWindowDays = 90;

        /// <summary>
        /// A column's numbers: current streak, the server's percentage (null = the server sent none),
        /// days completed and the window they were completed in. Mirrors the Mac test's tuple helper.
        /// </summary>
        private struct Column
        {
            public Column(double streak, double? percent, double done, double days = DefaultWindowDays)
            {
                Streak = streak;
                Percent = percent;
                Done = done;
                Days = days;
            }

            public double Streak { get; private set; }
            public double? Percent { get; private set; }
            public double Done { get; private set; }
            public double Days { get; private set; }
        }

        private static readonly Column Nothing = new Column(0, 0, 0);

        private static LeaderboardUser User(string name, Column? morning = null, Column? micro = null,
                                            Column? evening = null, Column? focus = null)
        {
            Column m = morning ?? Nothing;
            Column b = micro ?? Nothing;
            Column e = evening ?? Nothing;
            Column f = focus ?? Nothing;

            return new LeaderboardUser
            {
                Id = name,
                Username = name,
                MorningRoutinesStreak = m.Streak,
                MicroBreaksStreak = b.Streak,
                EveningRoutinesStreak = e.Streak,
                FocusModesStreak = f.Streak,
                MorningPercentNumberDayOfStatsCompleted = m.Percent,
                MicroPercentNumberDayOfStatsCompleted = b.Percent,
                EveningPercentNumberDayOfStatsCompleted = e.Percent,
                MorningNumberDaysCompleted = m.Done,
                MorningNumDaysOfStats = m.Days,
                MicroBreaksNumberDaysCompleted = b.Done,
                MicroBreaksNumDaysOfStats = b.Days,
                EveningNumberDaysCompleted = e.Done,
                EveningNumDaysOfStats = e.Days,
                FocusModesNumberDaysCompleted = f.Done,
                FocusModesNumDaysOfStats = f.Days
            };
        }

        private static string[] Names(IEnumerable<LeaderboardUser> users)
        {
            return users.Select(user => user.Username ?? string.Empty).ToArray();
        }

        /// <summary>
        /// The reported bug: rows came out in rank order (77%, 10%, 28%, 0%, 2%) because the sort used
        /// the raw streak count. A column must order by the percentage it shows.
        /// </summary>
        [TestMethod]
        public void BreakStreakSortsByPercentageNotStreakCount()
        {
            LeaderboardUser[] users =
            {
                User("jeznag", micro: new Column(19, 77, 69)),
                User("andrea", micro: new Column(0, 10, 9)),
                User("bev", micro: new Column(0, 28, 25)),
                User("gwest", micro: new Column(0, 0, 0)),
                User("richter", micro: new Column(0, 2, 2))
            };

            CollectionAssert.AreEqual(new[] { "jeznag", "bev", "andrea", "richter", "gwest" },
                Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.MicroBreak, false)));
            CollectionAssert.AreEqual(new[] { "gwest", "richter", "andrea", "bev", "jeznag" },
                Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.MicroBreak, true)));
        }

        [TestMethod]
        public void EachColumnSortsByItsOwnPercentage()
        {
            LeaderboardUser[] users =
            {
                User("a", new Column(1, 10, 9), new Column(1, 90, 81), new Column(1, 50, 45), new Column(1, null, 9)),
                User("b", new Column(1, 90, 81), new Column(1, 10, 9), new Column(1, 20, 18), new Column(1, null, 81)),
                User("c", new Column(1, 50, 45), new Column(1, 50, 45), new Column(1, 90, 81), new Column(1, null, 45))
            };

            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.Morning, false)));
            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.MicroBreak, false)));
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.Evening, false)));
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.FocusMode, false)));
            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.Morning, true)));
            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.FocusMode, true)));
        }

        [TestMethod]
        public void TiesBreakOnDaysCompletedThenStreakThenServerOrder()
        {
            // Same 50%: more days completed wins; same days: longer streak wins; identical: server order.
            LeaderboardUser[] users =
            {
                User("first", evening: new Column(2, 50, 30, 60)),
                User("second", evening: new Column(9, 50, 30, 60)),
                User("third", evening: new Column(3, 50, 45, 90)),
                User("fourth", evening: new Column(2, 50, 30, 60))
            };

            CollectionAssert.AreEqual(new[] { "third", "second", "first", "fourth" },
                Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.Evening, false)));
            CollectionAssert.AreEqual(new[] { "first", "fourth", "second", "third" },
                Names(LeaderboardSorting.Sort(users, LeaderboardSortKey.Evening, true)),
                "ascending flips the comparison but fully tied rows keep the server's order");
        }

        [TestMethod]
        public void FocusModePercentageComesFromTheDayPair()
        {
            Assert.AreEqual(93, LeaderboardSorting.Metric(User("x", focus: new Column(138, null, 84)), LeaderboardSortKey.FocusMode).Percentage);
            Assert.AreEqual(138, LeaderboardSorting.Metric(User("x", focus: new Column(138, null, 84)), LeaderboardSortKey.FocusMode).Streak);
            Assert.AreEqual(33, LeaderboardSorting.Metric(User("x", focus: new Column(0, null, 1, 3)), LeaderboardSortKey.FocusMode).Percentage);
            Assert.AreEqual(0, LeaderboardSorting.Metric(User("x", focus: new Column(0, null, 0, 0)), LeaderboardSortKey.FocusMode).Percentage,
                "no window → 0%, not NaN");
            Assert.AreEqual(100, LeaderboardSorting.Percentage(90, 90));
        }

        [TestMethod]
        public void ServerPercentageWinsWhenPresentAndIsDerivedOtherwise()
        {
            Assert.AreEqual(50, LeaderboardSorting.Metric(User("x", morning: new Column(1, 50, 88)), LeaderboardSortKey.Morning).Percentage,
                "the web shows the server's percentage column as-is");
            Assert.AreEqual(98, LeaderboardSorting.Metric(User("x", morning: new Column(1, null, 88)), LeaderboardSortKey.Morning).Percentage);
        }

        [TestMethod]
        public void SortKeysAreTheBackendStreakTypes()
        {
            CollectionAssert.AreEqual(
                new[] { "morning_routines_streak", "micro_breaks_streak", "evening_routines_streak", "focus_modes_streak" },
                LeaderboardSortKeys.All.Select(LeaderboardSortKeys.WireValue).ToArray());
        }

        /// <summary>
        /// The rank printed beside a row is its position in the DISPLAYED order. The server ranks by
        /// its own streak_type, so once the table is sorted by the column the user clicked, printing
        /// the server's rank gave a table whose numbers ran out of order.
        /// </summary>
        [TestMethod]
        public void RanksFollowTheDisplayedSortOrderNotTheServersRank()
        {
            LeaderboardUser[] users =
            {
                User("jeznag", micro: new Column(19, 77, 69)),
                User("andrea", micro: new Column(0, 10, 9)),
                User("bev", micro: new Column(0, 28, 25))
            };
            users[0].ServerRank = 1;
            users[1].ServerRank = 2;
            users[2].ServerRank = 3;

            IList<LeaderboardRow> ascending = LeaderboardSorting.Rank(users, LeaderboardSortKey.MicroBreak, true);

            CollectionAssert.AreEqual(new[] { "andrea", "bev", "jeznag" }, Names(ascending.Select(row => row.User)));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ascending.Select(row => row.Rank).ToArray(),
                "the column reads 1,2,3 down the page whichever way it is sorted");
            CollectionAssert.AreEqual(new[] { 2.0, 3.0, 1.0 }, ascending.Select(row => row.User.ServerRank.Value).ToArray(),
                "…which is not the server's rank, and that is the whole point");
        }

        [TestMethod]
        public void RankCarriesTheMetricTheRowIsSortedBy()
        {
            IList<LeaderboardRow> rows = LeaderboardSorting.Rank(
                new[] { User("jeznag", micro: new Column(19, 77, 69)) }, LeaderboardSortKey.MicroBreak, false);

            Assert.AreEqual(77, rows[0].Metric.Percentage);
            Assert.AreEqual(69, rows[0].Metric.CompletedDays);
            Assert.AreEqual(19, rows[0].Metric.Streak);
        }

        /// <summary>
        /// The API sends some of these columns as numeric STRINGS — the reason the Mac decodes them
        /// through FlexibleNumber rather than as plain numbers.
        /// </summary>
        [TestMethod]
        public void ANumericStringFromTheApiIsReadAsANumber()
        {
            JObject json = JObject.Parse(
                "{\"id\":\"u1\",\"username\":\"jeznag\",\"rank\":\"3\",\"micro_breaks_streak\":19," +
                "\"micro_percent_number_day_of_stats_completed\":\"77\"}");

            LeaderboardUser user = LeaderboardUser.FromJson(json);

            Assert.AreEqual(3, user.ServerRank);
            Assert.AreEqual(19, user.MicroBreaksStreak);
            Assert.AreEqual(77, LeaderboardSorting.Metric(user, LeaderboardSortKey.MicroBreak).Percentage);
        }

        [TestMethod]
        public void AColumnTheServerOmittedIsNotZeroItIsAbsent()
        {
            LeaderboardUser user = LeaderboardUser.FromJson(JObject.Parse(
                "{\"id\":\"u1\",\"number_days_completed\":45,\"num_days_of_stats\":90}"));

            Assert.IsNull(user.MorningNumberDaysCompleted);
            Assert.AreEqual(50, LeaderboardSorting.Metric(user, LeaderboardSortKey.FocusMode).Percentage,
                "an absent per-column day pair falls back to the shared one rather than reading as 0/0");
        }
    }
}
