using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The calendar the Stats screen reckons in.
    ///
    /// Weeks start on SUNDAY regardless of locale, because the web dashboard's moment() does, and the
    /// heat-map columns have to line up with the dashboard's.
    /// </summary>
    public static class StatsCalendar
    {
        public const int DaysInWeek = 7;

        public static DateTime StartOfDay(DateTime date)
        {
            return date.Date;
        }

        public static DateTime AddDays(int days, DateTime date)
        {
            return date.AddDays(days);
        }

        public static int DaysBetween(DateTime from, DateTime to)
        {
            return (int)(to.Date - from.Date).TotalDays;
        }

        /// <summary><see cref="DayOfWeek.Sunday"/> is 0, so subtracting it lands on the Sunday of that week.</summary>
        public static DateTime StartOfWeek(DateTime date)
        {
            return date.Date.AddDays(-(int)date.DayOfWeek);
        }

        /// <summary>
        /// The day a per-day stat row names.
        ///
        /// Those rows are <c>date_trunc('day', finish_time AT TIME ZONE tz)</c> serialised as UTC
        /// midnight, so the day they name is the UTC calendar day — reading them in local time shifts
        /// half the user's history by one square.
        /// </summary>
        public static string ServerDayKey(DateTimeOffset instant)
        {
            return instant.UtcDateTime.ToString(StatsDates.DayKeyFormat, CultureInfo.InvariantCulture);
        }
    }

    public static class StatsDates
    {
        internal const string DayKeyFormat = "yyyy-MM-dd";

        /// <summary>Local-day key (web: <c>moment(date).format('YYYY-MM-DD')</c>).</summary>
        public static string DayKey(DateTime date)
        {
            return date.ToString(DayKeyFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A date rendered for display. Culture-sensitive on purpose: the month strip above the
        /// heat map is read by the user, not by the server.
        /// </summary>
        public static string Format(DateTime date, string pattern)
        {
            return date.ToString(pattern, CultureInfo.CurrentCulture);
        }
    }

    /// <summary>One square: a day, how many routines were completed on it, and whether it is yet to come.</summary>
    public sealed class HeatMapCell
    {
        public HeatMapCell(string dayKey, DateTime date, int count, bool isFuture)
        {
            DayKey = dayKey;
            Date = date;
            Count = count;
            IsFuture = isFuture;
        }

        public string DayKey { get; private set; }
        public DateTime Date { get; private set; }
        public int Count { get; private set; }

        /// <summary>After today — drawn as a placeholder so the last column is still a whole week.</summary>
        public bool IsFuture { get; private set; }
    }

    public sealed class HeatMapMonthLabel
    {
        public HeatMapMonthLabel(string label, int weekIndex)
        {
            Label = label;
            WeekIndex = weekIndex;
        }

        public string Label { get; private set; }

        /// <summary>Index of the column the label sits over.</summary>
        public int WeekIndex { get; private set; }
    }

    public sealed class HeatMapModel
    {
        public IList<IList<HeatMapCell>> Columns { get; set; }
        public IList<HeatMapMonthLabel> MonthLabels { get; set; }
        public int MaxCount { get; set; }

        public HeatMapModel()
        {
            Columns = new List<IList<HeatMapCell>>();
            MonthLabels = new List<HeatMapMonthLabel>();
        }

        public int TotalCount
        {
            get { return Columns.SelectMany(column => column).Sum(cell => cell.Count); }
        }
    }

    /// <summary>One day's completion count as the stats API reports it.</summary>
    public sealed class ActivityStatItem
    {
        /// <summary>The instant the API sent; only its UTC calendar day is meaningful.</summary>
        public DateTimeOffset? Date { get; set; }

        public int Count { get; set; }

        /// <summary>
        /// The day's logged quantity, in whatever unit the activity measures — seconds for a
        /// timed habit. The Mac reads this as <c>item.summary.doubleValue</c>; the heat map only
        /// needs <see cref="Count"/>, but the habit summary and the log-quantity graph need this.
        /// </summary>
        public double Summary { get; set; }
    }

    /// <summary>
    /// The routine-completion heat map. Port of the Mac's <c>HeatMapBuilder</c>, itself a port of the
    /// web dashboard's <c>processHeatMapData</c>.
    /// </summary>
    public static class HeatMapBuilder
    {
        public const int WeeksToShow = 5;
        public const int DaysInWeek = StatsCalendar.DaysInWeek;
        public const int IntensityLevels = 5;

        /// <summary>
        /// How many days the request has to cover. The grid starts at the beginning of the week five
        /// weeks back — up to six weeks ago — so a 35-day fetch leaves the oldest column blank on
        /// most weekdays.
        /// </summary>
        public const int FetchDays = (WeeksToShow + 1) * DaysInWeek;

        private const string MonthLabelPattern = "MMM";
        private const int FirstDayOfMonth = 1;

        public static IDictionary<string, int> CountsByDay(IEnumerable<ActivityStatItem> items)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (ActivityStatItem item in items)
            {
                if (!item.Date.HasValue)
                {
                    continue;
                }

                string key = StatsCalendar.ServerDayKey(item.Date.Value);
                int existing;
                counts.TryGetValue(key, out existing);
                counts[key] = existing + item.Count;
            }

            return counts;
        }

        /// <summary>
        /// The grid the screen shows: from the start of the week five weeks ago — or the week the
        /// account was created, whichever is later — through today, padded out to a whole final week.
        /// </summary>
        public static HeatMapModel Build(IEnumerable<ActivityStatItem> items, DateTime? joinDate, DateTime today)
        {
            DateTime endDate = StatsCalendar.StartOfDay(today);
            DateTime defaultStart = StatsCalendar.StartOfWeek(StatsCalendar.AddDays(-WeeksToShow * DaysInWeek, endDate));
            DateTime start = defaultStart;
            if (joinDate.HasValue)
            {
                DateTime joinWeek = StatsCalendar.StartOfWeek(joinDate.Value);
                if (joinWeek > defaultStart)
                {
                    start = joinWeek;
                }
            }

            return Grid(start, endDate, endDate, CountsByDay(items));
        }

        /// <summary>
        /// One column per week from the week containing <paramref name="start"/> through the week
        /// containing <paramref name="end"/>, every column a full seven cells (Sunday first), so no
        /// day in the range is skipped and no column is a ragged part-week.
        /// </summary>
        public static HeatMapModel Grid(DateTime start, DateTime end, DateTime today, IDictionary<string, int> counts)
        {
            HeatMapModel model = new HeatMapModel();
            model.MaxCount = counts.Count == 0 ? 0 : counts.Values.Max();

            DateTime todayStart = StatsCalendar.StartOfDay(today);
            DateTime lastWeekStart = StatsCalendar.StartOfWeek(end);
            DateTime weekStart = StatsCalendar.StartOfWeek(start);
            while (weekStart <= lastWeekStart)
            {
                model.Columns.Add(WeekColumn(weekStart, todayStart, counts));
                weekStart = StatsCalendar.AddDays(DaysInWeek, weekStart);
            }

            model.MonthLabels = MonthLabels(model.Columns);
            return model;
        }

        private static IList<HeatMapCell> WeekColumn(DateTime weekStart, DateTime today, IDictionary<string, int> counts)
        {
            List<HeatMapCell> column = new List<HeatMapCell>(DaysInWeek);
            for (int offset = 0; offset < DaysInWeek; offset++)
            {
                DateTime date = StatsCalendar.AddDays(offset, weekStart);
                string key = StatsDates.DayKey(date);
                int count;
                counts.TryGetValue(key, out count);
                column.Add(new HeatMapCell(key, date, count, date > today));
            }

            return column;
        }

        /// <summary>
        /// A month is labelled over the column holding its 1st. The first column also gets its own
        /// month UNLESS the next column is already labelled, where the two would overlap.
        /// </summary>
        public static IList<HeatMapMonthLabel> MonthLabels(IList<IList<HeatMapCell>> columns)
        {
            List<HeatMapMonthLabel> labels = new List<HeatMapMonthLabel>();
            for (int weekIndex = 0; weekIndex < columns.Count; weekIndex++)
            {
                HeatMapCell monthStart = columns[weekIndex].FirstOrDefault(cell => cell.Date.Day == FirstDayOfMonth);
                if (monthStart == null)
                {
                    continue;
                }

                labels.Add(new HeatMapMonthLabel(StatsDates.Format(monthStart.Date, MonthLabelPattern), weekIndex));
            }

            int firstLabelledWeek = labels.Count == 0 ? int.MaxValue : labels[0].WeekIndex;
            if (firstLabelledWeek > 1 && columns.Count > 0 && columns[0].Count > 0)
            {
                labels.Insert(0, new HeatMapMonthLabel(StatsDates.Format(columns[0][0].Date, MonthLabelPattern), 0));
            }

            return labels;
        }

        /// <summary>0 = nothing completed, 1…4 = quartiles of the busiest day (web <c>getColorIntensity</c>).</summary>
        public static int IntensityLevel(int count, int maxCount)
        {
            if (count <= 0)
            {
                return 0;
            }

            double intensity = Math.Min((double)count / Math.Max(maxCount, 1), 1);
            if (intensity <= 0.25) { return 1; }
            if (intensity <= 0.5) { return 2; }
            if (intensity <= 0.75) { return 3; }
            return 4;
        }
    }

    /// <summary>
    /// The per-level bear figure on the Progress tab, mirroring the web dashboard's
    /// <c>getLevelIconAndName</c> where a level is a direct INDEX into the artwork array.
    ///
    /// Keyed by the level index, never by the displayed level name — that name is config-driven and
    /// localised, so keying art off it would break the moment a translation or a server config change
    /// renamed a level.
    /// </summary>
    public static class LevelArtwork
    {
        private static readonly string[] AssetNames =
        {
            "levelNovice", "levelApprentice", "levelExplorer", "levelDisciplined", "levelDiligent",
            "levelStrategist", "levelMentor", "levelScholar", "levelGuardian", "levelAchiever"
        };

        public static int MaxLevel
        {
            get { return AssetNames.Length - 1; }
        }

        /// <summary>
        /// The asset name for a level, or null for a level this build ships no art for — a level the
        /// server introduced since — so the caller can fall back rather than draw nothing.
        /// </summary>
        public static string AssetName(int level)
        {
            if (level < 0 || level > MaxLevel)
            {
                return null;
            }

            return AssetNames[level];
        }
    }
}
