using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The leaderboard's sortable columns. The wire value is the backend <c>StreakTypes</c> value sent
    /// as <c>streak_type</c>, which picks the top-N pool and the <c>rank</c> the server returns.
    /// </summary>
    public enum LeaderboardSortKey
    {
        Morning,
        MicroBreak,
        Evening,
        FocusMode
    }

    public static class LeaderboardSortKeys
    {
        public const string Morning = "morning_routines_streak";
        public const string MicroBreak = "micro_breaks_streak";
        public const string Evening = "evening_routines_streak";
        public const string FocusMode = "focus_modes_streak";

        /// <summary>Declared in the order the columns appear, which the wire values must keep.</summary>
        public static readonly LeaderboardSortKey[] All =
        {
            LeaderboardSortKey.Morning,
            LeaderboardSortKey.MicroBreak,
            LeaderboardSortKey.Evening,
            LeaderboardSortKey.FocusMode
        };

        public static string WireValue(LeaderboardSortKey key)
        {
            switch (key)
            {
                case LeaderboardSortKey.Morning: return Morning;
                case LeaderboardSortKey.MicroBreak: return MicroBreak;
                case LeaderboardSortKey.Evening: return Evening;
                case LeaderboardSortKey.FocusMode: return FocusMode;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
    }

    /// <summary>
    /// One leaderboard column for one user: the completion percentage over the stats window, the
    /// <c>(completed/window)</c> day pair behind it, and the current streak.
    /// </summary>
    public struct LeaderboardMetric : IEquatable<LeaderboardMetric>
    {
        public LeaderboardMetric(double percentage, double completedDays, double windowDays, double streak)
            : this(percentage, completedDays, windowDays, windowDays, streak)
        {
        }

        public LeaderboardMetric(double percentage, double completedDays, double windowDays, double windowSpanDays, double streak)
        {
            Percentage = percentage;
            CompletedDays = completedDays;
            WindowDays = windowDays;
            WindowSpanDays = windowSpanDays;
            Streak = streak;
        }

        public double Percentage { get; private set; }
        public double CompletedDays { get; private set; }

        /// <summary>
        /// How many days the metric was MEASURED against â€” not the same as the span the window covers.
        /// Every streak walk forgives days (a weekend with no focus session, a day a routine was not
        /// scheduled), and since backend #2424 the tally forgives exactly the same days, which is why a
        /// 149-day focus streak reads "84 / 84" inside a 90-day window rather than "84 / 90".
        /// </summary>
        public double WindowDays { get; private set; }

        /// <summary>Days the stats window covers (server <c>num_days_of_stats</c>), forgiven days included.</summary>
        public double WindowSpanDays { get; private set; }
        public double Streak { get; private set; }

        /// <summary>Days inside the window that counted as neither a hit nor a miss â€” what the streak forgave.</summary>
        public double ForgivenDays { get { return Math.Max(WindowSpanDays - WindowDays, 0); } }
        public bool HasForgivenDays { get { return ForgivenDays > 0; } }

        public bool Equals(LeaderboardMetric other)
        {
            return Percentage == other.Percentage && CompletedDays == other.CompletedDays
                && WindowDays == other.WindowDays && WindowSpanDays == other.WindowSpanDays && Streak == other.Streak;
        }

        public override bool Equals(object obj)
        {
            return obj is LeaderboardMetric && Equals((LeaderboardMetric)obj);
        }

        public override int GetHashCode()
        {
            return Percentage.GetHashCode() ^ CompletedDays.GetHashCode() ^ Streak.GetHashCode();
        }
    }

    /// <summary>
    /// A leaderboard row as the API sends it.
    ///
    /// Every number is nullable because the server omits columns it has nothing for, and a missing
    /// value is zero rather than an error. The per-column day pairs fall back to the shared
    /// <see cref="NumberDaysCompleted"/> / <see cref="NumDaysOfStats"/> when absent.
    /// </summary>
    public sealed class LeaderboardUser
    {
        public string Id { get; set; }
        public string Username { get; set; }

        /// <summary>
        /// The rank the SERVER assigned, in the server's own <c>streak_type</c> order. It is NOT the
        /// number to print beside a row — see <see cref="LeaderboardSorting.Rank"/>.
        /// </summary>
        public double? ServerRank { get; set; }

        public double? MorningRoutinesStreak { get; set; }
        public double? EveningRoutinesStreak { get; set; }
        public double? FocusModesStreak { get; set; }
        public double? MicroBreaksStreak { get; set; }

        public double? MorningPercentNumberDayOfStatsCompleted { get; set; }
        public double? EveningPercentNumberDayOfStatsCompleted { get; set; }
        public double? MicroPercentNumberDayOfStatsCompleted { get; set; }

        public double? MorningNumberDaysCompleted { get; set; }
        public double? MorningNumDaysOfStats { get; set; }
        public double? EveningNumberDaysCompleted { get; set; }
        public double? EveningNumDaysOfStats { get; set; }
        public double? MicroBreaksNumberDaysCompleted { get; set; }
        public double? MicroBreaksNumDaysOfStats { get; set; }
        public double? FocusModesNumberDaysCompleted { get; set; }
        public double? FocusModesNumDaysOfStats { get; set; }

        public double? NumDaysOfStats { get; set; }
        public double? NumberDaysCompleted { get; set; }

        /// <summary>
        /// Reads one <c>users_rankings</c> row. Every numeric field goes through
        /// <see cref="FlexibleNumber.Read"/> because the API sends some of these as numeric STRINGS.
        /// </summary>
        public static LeaderboardUser FromJson(JObject json)
        {
            return new LeaderboardUser
            {
                Id = (string)json["id"],
                Username = (string)json["username"],
                ServerRank = FlexibleNumber.Read(json["rank"]),
                MorningRoutinesStreak = FlexibleNumber.Read(json[LeaderboardSortKeys.Morning]),
                EveningRoutinesStreak = FlexibleNumber.Read(json[LeaderboardSortKeys.Evening]),
                FocusModesStreak = FlexibleNumber.Read(json[LeaderboardSortKeys.FocusMode]),
                MicroBreaksStreak = FlexibleNumber.Read(json[LeaderboardSortKeys.MicroBreak]),
                MorningPercentNumberDayOfStatsCompleted = FlexibleNumber.Read(json["morning_percent_number_day_of_stats_completed"]),
                EveningPercentNumberDayOfStatsCompleted = FlexibleNumber.Read(json["evening_percent_number_day_of_stats_completed"]),
                MicroPercentNumberDayOfStatsCompleted = FlexibleNumber.Read(json["micro_percent_number_day_of_stats_completed"]),
                MorningNumberDaysCompleted = FlexibleNumber.Read(json["morning_number_days_completed"]),
                MorningNumDaysOfStats = FlexibleNumber.Read(json["morning_num_days_of_stats"]),
                EveningNumberDaysCompleted = FlexibleNumber.Read(json["evening_number_days_completed"]),
                EveningNumDaysOfStats = FlexibleNumber.Read(json["evening_num_days_of_stats"]),
                MicroBreaksNumberDaysCompleted = FlexibleNumber.Read(json["micro_breaks_number_days_completed"]),
                MicroBreaksNumDaysOfStats = FlexibleNumber.Read(json["micro_breaks_num_days_of_stats"]),
                FocusModesNumberDaysCompleted = FlexibleNumber.Read(json["focus_modes_number_days_completed"]),
                FocusModesNumDaysOfStats = FlexibleNumber.Read(json["focus_modes_num_days_of_stats"]),
                NumDaysOfStats = FlexibleNumber.Read(json["num_days_of_stats"]),
                NumberDaysCompleted = FlexibleNumber.Read(json["number_days_completed"])
            };
        }
    }

    /// <summary>
    /// A stats number as the API actually sends it: sometimes a JSON number, sometimes the same value
    /// as a string. Mirrors the Mac's <c>FlexibleNumber</c>, which exists for the same reason.
    /// </summary>
    public static class FlexibleNumber
    {
        /// <summary>The value, or null when the field is absent, null, or not numeric at all.</summary>
        public static double? Read(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return (double)token;
            }

            double parsed;
            if (token.Type == JTokenType.String
                && double.TryParse((string)token, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }
    }

    /// <summary>One row as the table draws it: the user, the column's numbers, and its displayed rank.</summary>
    public sealed class LeaderboardRow
    {
        public LeaderboardRow(int rank, LeaderboardUser user, LeaderboardMetric metric)
        {
            Rank = rank;
            User = user;
            Metric = metric;
        }

        /// <summary>1-based position in the order the table is showing.</summary>
        public int Rank { get; private set; }

        public LeaderboardUser User { get; private set; }
        public LeaderboardMetric Metric { get; private set; }
    }

    /// <summary>
    /// Ordering and ranking for the leaderboard table. Port of the Mac's <c>LeaderboardSorting</c>.
    /// </summary>
    public static class LeaderboardSorting
    {
        private const double FullPercentage = 100.0;

        /// <summary>
        /// Whole-number percentage, like the server's <c>*_percent_number_day_of_stats_completed</c>
        /// columns. A user with no stats window scores 0 rather than NaN.
        /// </summary>
        public static double Percentage(double completedDays, double windowDays)
        {
            if (windowDays <= 0)
            {
                return 0;
            }

            return SwiftRounding.Rounded(completedDays / windowDays * FullPercentage);
        }

        /// <summary>
        /// The numbers behind one column for one user.
        ///
        /// Morning / break / evening use the SERVER's percentage, because that is the number the
        /// column prints and the web shows. Focus mode has no percentage column on the server, so it
        /// is derived from the day pair the API does return.
        /// </summary>
        public static LeaderboardMetric Metric(LeaderboardUser user, LeaderboardSortKey key)
        {
            double completed;
            double window;
            double? serverPercent;
            double streak;

            switch (key)
            {
                case LeaderboardSortKey.Morning:
                    completed = user.MorningNumberDaysCompleted ?? Or0(user.NumberDaysCompleted);
                    window = user.MorningNumDaysOfStats ?? Or0(user.NumDaysOfStats);
                    serverPercent = user.MorningPercentNumberDayOfStatsCompleted;
                    streak = Or0(user.MorningRoutinesStreak);
                    break;
                case LeaderboardSortKey.MicroBreak:
                    completed = user.MicroBreaksNumberDaysCompleted ?? Or0(user.NumberDaysCompleted);
                    window = user.MicroBreaksNumDaysOfStats ?? Or0(user.NumDaysOfStats);
                    serverPercent = user.MicroPercentNumberDayOfStatsCompleted;
                    streak = Or0(user.MicroBreaksStreak);
                    break;
                case LeaderboardSortKey.Evening:
                    completed = user.EveningNumberDaysCompleted ?? Or0(user.NumberDaysCompleted);
                    window = user.EveningNumDaysOfStats ?? Or0(user.NumDaysOfStats);
                    serverPercent = user.EveningPercentNumberDayOfStatsCompleted;
                    streak = Or0(user.EveningRoutinesStreak);
                    break;
                case LeaderboardSortKey.FocusMode:
                    completed = user.FocusModesNumberDaysCompleted ?? Or0(user.NumberDaysCompleted);
                    window = user.FocusModesNumDaysOfStats ?? Or0(user.NumDaysOfStats);
                    serverPercent = null;
                    streak = Or0(user.FocusModesStreak);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key));
            }

            // A window shorter than the measured days would mean the server disagrees with itself; take
            // the larger so ForgivenDays stays a count rather than going negative.
            double windowSpan = Math.Max(Or0(user.NumDaysOfStats), window);
            return new LeaderboardMetric(serverPercent ?? Percentage(completed, window), completed, window, windowSpan, streak);
        }

        /// <summary>
        /// The caption under a leaderboard column: "88 days completed out of last 90 days". When the streak
        /// forgave days that sentence becomes false â€” the denominator is the measured-day count â€” and it read
        /// as lost history ("why is it saying 84/84? I've clearly got > 90 days of data"). So name the
        /// forgiven days instead, leading with the window: 84 + 6 = 90 accounts for every day on its face.
        /// </summary>
        public static string CompletionSummary(LeaderboardMetric metric, string daysCompletedOutOf, string daysCompletedOutOfExpected)
        {
            string completed = StatsText.PlainNumber(metric.CompletedDays);
            if (!metric.HasForgivenDays)
                return StatsText.Fill(daysCompletedOutOf, ("completed", completed), ("total", StatsText.PlainNumber(metric.WindowDays)));
            return StatsText.Fill(daysCompletedOutOfExpected, ("completed", completed),
                                  ("forgiven", StatsText.PlainNumber(metric.ForgivenDays)), ("window", StatsText.PlainNumber(metric.WindowSpanDays)));
        }

        /// <summary>
        /// Stable order by the column's PERCENTAGE, then days completed, then current streak.
        ///
        /// The reported bug: rows came out 77%, 10%, 28%, 0%, 2% because the sort used the raw streak
        /// count while the column printed the percentage. Whatever a column shows is what it must
        /// sort by. <paramref name="ascending"/> flips the comparison; rows that tie on all three
        /// keep the server's order either way.
        /// </summary>
        public static IList<LeaderboardUser> Sort(IList<LeaderboardUser> users, LeaderboardSortKey key, bool ascending)
        {
            List<LeaderboardUser> sorted = new List<LeaderboardUser>(users.Count);
            foreach (LeaderboardRow row in Rank(users, key, ascending))
            {
                sorted.Add(row.User);
            }

            return sorted;
        }

        /// <summary>
        /// The rows in displayed order, each carrying the rank to PRINT beside it.
        ///
        /// That rank is the row's position in this order, never
        /// <see cref="LeaderboardUser.ServerRank"/>. The server ranks by its own <c>streak_type</c>,
        /// so once the table is sorted by the column the user clicked — or by a percentage the server
        /// does not rank on at all — printing the server's rank gave a table whose numbers ran out of
        /// order (fixed on the Mac in "Fix sorted leaderboard ranks").
        /// </summary>
        public static IList<LeaderboardRow> Rank(IList<LeaderboardUser> users, LeaderboardSortKey key, bool ascending)
        {
            List<Tuple<int, LeaderboardUser, LeaderboardMetric>> ordered =
                new List<Tuple<int, LeaderboardUser, LeaderboardMetric>>(users.Count);
            for (int serverPosition = 0; serverPosition < users.Count; serverPosition++)
            {
                ordered.Add(Tuple.Create(serverPosition, users[serverPosition], Metric(users[serverPosition], key)));
            }

            // The server's order is the final tie-break, so it has to survive the sort: List.Sort is
            // not stable, which is why the original position is compared explicitly rather than
            // relied on.
            ordered.Sort((left, right) =>
            {
                int comparison = CompareMetrics(left.Item3, right.Item3);
                if (comparison != 0)
                {
                    return ascending ? comparison : -comparison;
                }

                return left.Item1.CompareTo(right.Item1);
            });

            List<LeaderboardRow> displayed = new List<LeaderboardRow>(ordered.Count);
            for (int position = 0; position < ordered.Count; position++)
            {
                displayed.Add(new LeaderboardRow(position + 1, ordered[position].Item2, ordered[position].Item3));
            }

            return displayed;
        }

        private static int CompareMetrics(LeaderboardMetric left, LeaderboardMetric right)
        {
            int byPercentage = left.Percentage.CompareTo(right.Percentage);
            if (byPercentage != 0)
            {
                return byPercentage;
            }

            int byCompletedDays = left.CompletedDays.CompareTo(right.CompletedDays);
            if (byCompletedDays != 0)
            {
                return byCompletedDays;
            }

            return left.Streak.CompareTo(right.Streak);
        }

        private static double Or0(double? value)
        {
            return value ?? 0;
        }
    }
}
