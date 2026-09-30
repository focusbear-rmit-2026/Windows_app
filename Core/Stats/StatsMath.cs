using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The Stats screen's tabs, in the web's <c>STATS.EMBEDDED_TABS</c> order. The raw values are the
    /// Mac's, so a remembered tab reads the same wherever it was saved.
    /// </summary>
    public enum StatsTab { Goals, Progress, Streak, PepTalk, Leaderboard, HabitStats, Productivity, FocusLog }

    public static class StatsTabs
    {
        /// <summary>Web <c>STATS_TAB_INDICES.STREAK</c> is the default selection.</summary>
        public const StatsTab Default = StatsTab.Streak;

        public static readonly StatsTab[] All =
        {
            StatsTab.Goals, StatsTab.Progress, StatsTab.Streak, StatsTab.PepTalk,
            StatsTab.Leaderboard, StatsTab.HabitStats, StatsTab.Productivity, StatsTab.FocusLog,
        };

        public static string RawValue(this StatsTab tab)
        {
            string name = tab.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static StatsTab Parse(string raw)
        {
            foreach (var tab in All) if (tab.RawValue() == raw) return tab;
            return Default;
        }
    }

    /// <summary>Text helpers shared by every tab.</summary>
    public static class StatsText
    {
        /// <summary>Fills <c>{name}</c> placeholders: <c>Fill("Level {level}", ("level", "3"))</c>.</summary>
        public static string Fill(string template, params (string Key, string Value)[] values)
        {
            string result = template ?? "";
            foreach (var (key, value) in values) result = result.Replace("{" + key + "}", value);
            return result;
        }

        /// <summary>"12" for a whole number, "12.5" otherwise — the Mac's <c>StatsChart.plainNumber</c>.</summary>
        public static string PlainNumber(double value)
        {
            double rounded = SwiftRounding.Rounded(value);
            return value == rounded
                ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }

    // ================================================================== productivity

    /// <summary>
    /// The Productivity tab: alignment-weighted focused minutes over the last 30 days, months or years.
    /// Port of the Mac's <c>ProductivitySummary</c>, itself the web's <c>dashboard/stats/productivity</c>.
    /// </summary>
    public sealed class ProductivitySummary
    {
        public const int DataLimit = 30;
        public const double DefaultAlignmentScore = 100;
        /// <summary>Switch the chart to hours once a bucket reaches this many minutes.</summary>
        public const double HoursThresholdMinutes = 1000;

        public double FocusedMinutes { get; set; }
        public double AverageMinutesPerDay { get; set; }
        public GraphPoint MostFocusedDay { get; set; }
        public double AverageTabCount { get; set; }
        public double AverageTabDuration { get; set; }
        public IList<GraphPoint> Chart { get; set; } = new List<GraphPoint>();
        public bool UsesHours { get; set; }

        public bool HasTabMetrics => AverageTabCount > 0 || AverageTabDuration > 0;

        /// <summary>Web <c>getDateRangeForSummary</c>: 30 days / months / years back, aligned to the period start.</summary>
        public static (DateTime From, DateTime To) DateRange(ProductivityPeriod period, DateTime now)
        {
            DateTime back;
            switch (period)
            {
                case ProductivityPeriod.Monthly: back = now.AddMonths(-DataLimit); break;
                case ProductivityPeriod.Yearly: back = now.AddYears(-DataLimit); break;
                default: back = now.AddDays(-DataLimit); break;
            }
            return (ProductivityPeriods.BucketStart(period, back), now);
        }

        /// <summary>Wall-clock minutes weighted by the session's alignment score (100 when it has none).</summary>
        public static double FocusedMinutesOf(FocusSessionSummary session)
        {
            double score = session.FocusAlignmentScore ?? DefaultAlignmentScore;
            return Math.Max(session.DurationSeconds ?? 0, 0) * (score / 100) / 60;
        }

        public static ProductivitySummary Compute(IEnumerable<FocusSessionSummary> sessions, ProductivityPeriod period)
        {
            var summary = new ProductivitySummary();
            var byDay = new Dictionary<string, (DateTime Day, double Minutes)>();
            var byBucket = new Dictionary<DateTime, double>();
            int tabSessions = 0;
            double tabCountTotal = 0, tabDurationTotal = 0;

            foreach (var session in sessions ?? Enumerable.Empty<FocusSessionSummary>())
            {
                if (session.StartTime == null) continue;
                DateTime start = session.StartTime.Value;
                double minutes = FocusedMinutesOf(session);
                summary.FocusedMinutes += minutes;
                string dayKey = StatsDates.DayKey(start);
                byDay.TryGetValue(dayKey, out var day);
                byDay[dayKey] = (start.Date, day.Minutes + minutes);
                DateTime bucket = ProductivityPeriods.BucketStart(period, start);
                byBucket.TryGetValue(bucket, out double bucketMinutes);
                byBucket[bucket] = bucketMinutes + minutes;
                if (session.AverageTabCount != null || session.AverageTabDuration != null)
                {
                    tabSessions++;
                    tabCountTotal += session.AverageTabCount ?? 0;
                    tabDurationTotal += session.AverageTabDuration ?? 0;
                }
            }

            if (byDay.Count > 0)
            {
                summary.AverageMinutesPerDay = SwiftRounding.Rounded(summary.FocusedMinutes / byDay.Count);
                // The Mac keeps the first of several equal maxima; so does a stable ordered scan.
                var best = byDay.Values.Aggregate((a, b) => b.Minutes > a.Minutes ? b : a);
                summary.MostFocusedDay = new GraphPoint(StatsDates.DayKey(best.Day), SwiftRounding.Rounded(best.Minutes), best.Day);
            }
            if (tabSessions > 0)
            {
                summary.AverageTabCount = SwiftRounding.Rounded(tabCountTotal / tabSessions * 10) / 10;
                summary.AverageTabDuration = SwiftRounding.Rounded(tabDurationTotal / tabSessions);
            }

            var buckets = byBucket.Keys.OrderBy(d => d).ToList();
            if (buckets.Count > DataLimit) buckets = buckets.Skip(buckets.Count - DataLimit).ToList();
            double maxMinutes = buckets.Count == 0 ? 0 : buckets.Max(b => byBucket[b]);
            summary.UsesHours = maxMinutes >= HoursThresholdMinutes;
            summary.Chart = buckets.Select(bucket =>
            {
                double minutes = byBucket[bucket];
                double value = summary.UsesHours ? SwiftRounding.Rounded(minutes / 60 * 10) / 10 : SwiftRounding.Rounded(minutes);
                return new GraphPoint(StatsDates.Format(bucket, ProductivityPeriods.LabelPattern(period)), value, bucket);
            }).ToList();
            return summary;
        }
    }

    // ================================================================== focus log

    /// <summary>
    /// The focus log's columns. Start and finish share one column: two date columns left the intention —
    /// the only column whose content varies — truncated. Sorting and day-filtering it use the start time.
    /// </summary>
    public enum FocusLogColumn { Duration, Time, Intention, Distractions, FocusMode }

    /// <summary>Port of the Mac's <c>FocusLogMath</c>, itself the web's <c>dashboard/stats/focusSummary</c> helpers.</summary>
    public static class FocusLogMath
    {
        public const string CsvFileName = "focus_summary.csv";
        public const int PageSize = 50;

        /// <summary>Web <c>getEffectiveFocusDurationSeconds</c>: finish − start when at least the stored duration, else the stored value.</summary>
        public static int EffectiveDurationSeconds(FocusLogEntry entry)
        {
            int stored = SwiftRounding.RoundedToInt(entry.FocusDurationSeconds ?? 0);
            DateTime? start = ToDoDates.Parse(entry.StartTime), finish = ToDoDates.Parse(entry.FinishTime);
            if (start == null || finish == null) return stored;
            int wallClock = SwiftRounding.RoundedToInt((finish.Value - start.Value).TotalSeconds);
            return wallClock >= stored ? wallClock : stored;
        }

        /// <summary>Web <c>getValidFocusSummaryColumnValue</c>: intentions arrive form-encoded from some clients.</summary>
        public static string DisplayText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "-";
            string spaced = raw.Replace("+", " ");
            try { return Uri.UnescapeDataString(spaced); }
            catch (UriFormatException) { return spaced; }
        }

        /// <summary>Web <c>sortFocusSummariesByColumn</c> — always descending, stable.</summary>
        public static List<FocusLogEntry> Sorted(IEnumerable<FocusLogEntry> entries, FocusLogColumn column)
        {
            switch (column)
            {
                case FocusLogColumn.Duration: return entries.OrderByDescending(EffectiveDurationSeconds).ToList();
                case FocusLogColumn.Intention: return entries.OrderByDescending(e => (e.Intention ?? "").ToLowerInvariant(), StringComparer.Ordinal).ToList();
                case FocusLogColumn.Distractions: return entries.OrderByDescending(e => (e.Distractions ?? "").ToLowerInvariant(), StringComparer.Ordinal).ToList();
                case FocusLogColumn.FocusMode: return entries.OrderByDescending(e => (e.FocusModeName ?? "").ToLowerInvariant(), StringComparer.Ordinal).ToList();
                default: return entries.OrderByDescending(e => e.StartTime ?? "", StringComparer.Ordinal).ToList();
            }
        }

        /// <summary>
        /// Web <c>getFocusSummaryFilteredData</c>: text columns match case-insensitively, the time column matches
        /// the calendar day, the duration column matches its humanised text.
        /// </summary>
        public static List<FocusLogEntry> Filtered(IEnumerable<FocusLogEntry> entries, FocusLogColumn column, string text, DateTime? day,
                                                   Func<int, string> humanize)
        {
            string needle = (text ?? "").Trim().ToLowerInvariant();
            return entries.Where(entry =>
            {
                switch (column)
                {
                    case FocusLogColumn.Duration:
                        return needle.Length == 0 || humanize(EffectiveDurationSeconds(entry)).ToLowerInvariant().Contains(needle);
                    case FocusLogColumn.Time:
                        if (day == null) return true;
                        DateTime? start = ToDoDates.Parse(entry.StartTime);
                        return start != null && start.Value.Date == day.Value.Date;
                    case FocusLogColumn.Intention:
                        return needle.Length == 0 || DisplayText(entry.Intention).ToLowerInvariant().Contains(needle);
                    case FocusLogColumn.Distractions:
                        return needle.Length == 0 || DisplayText(entry.Distractions).ToLowerInvariant().Contains(needle);
                    default:
                        return needle.Length == 0 || (entry.FocusModeName ?? "").ToLowerInvariant().Contains(needle);
                }
            }).ToList();
        }

        /// <summary>Web <c>getFocusSummaryGraphLabelsAndData</c>: seconds per bucket keyed by created_at, latest 30.</summary>
        public static List<GraphPoint> GraphPoints(IEnumerable<FocusLogEntry> entries, ProductivityPeriod period)
        {
            var byBucket = new Dictionary<DateTime, double>();
            foreach (var entry in entries)
            {
                DateTime? created = ToDoDates.Parse(entry.CreatedAt);
                if (created == null) continue;
                DateTime bucket = ProductivityPeriods.BucketStart(period, created.Value);
                byBucket.TryGetValue(bucket, out double seconds);
                byBucket[bucket] = seconds + EffectiveDurationSeconds(entry);
            }
            var buckets = byBucket.Keys.OrderBy(d => d).ToList();
            if (buckets.Count > ProductivitySummary.DataLimit) buckets = buckets.Skip(buckets.Count - ProductivitySummary.DataLimit).ToList();
            return buckets.Select(b => new GraphPoint(StatsDates.Format(b, ProductivityPeriods.FocusLogLabelPattern(period)), byBucket[b], b)).ToList();
        }

        /// <summary>Web <c>downloadFocusSummaryCSVFile</c> (same columns and raw values), with proper CSV quoting.</summary>
        public static string Csv(IEnumerable<FocusLogEntry> entries, IList<string> headers)
        {
            var lines = new List<string> { string.Join(",", headers.Select(CsvField)) };
            foreach (var e in entries)
            {
                lines.Add(string.Join(",", new[]
                {
                    e.FocusModeName ?? "", EffectiveDurationSeconds(e).ToString(CultureInfo.InvariantCulture), e.StartTime ?? "", e.FinishTime ?? "",
                    e.Intention ?? "", e.Achievements ?? "", e.Distractions ?? "",
                }.Select(CsvField)));
            }
            return string.Join("\n", lines);
        }

        private static string CsvField(string value)
        {
            if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0 && value.IndexOf('\n') < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// "1 hour, 25 minutes" — the Mac's full-style DateComponentsFormatter, at most two units: seconds
        /// under a minute, else days/hours/minutes. The unit words are passed in so they can be localised.
        /// </summary>
        public static string HumanizeSeconds(int seconds, DurationWords words)
        {
            seconds = Math.Max(seconds, 0);
            if (seconds < 60) return Unit(seconds, words.Second, words.Seconds);
            int minutes = seconds / 60;
            int days = minutes / (60 * 24), hours = minutes / 60 % 24, mins = minutes % 60;
            var parts = new List<string>();
            if (days > 0) parts.Add(Unit(days, words.Day, words.Days));
            if (hours > 0) parts.Add(Unit(hours, words.Hour, words.Hours));
            if (mins > 0 || parts.Count == 0) parts.Add(Unit(mins, words.Minute, words.Minutes));
            return string.Join(", ", parts.Take(2));
        }

        private static string Unit(int n, string one, string many) => n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);

        /// <summary>Validation for a manual session (web <c>FocusSummaryValidationMessage</c>); null when valid.</summary>
        public static ManualSessionProblem? Validate(DateTime start, DateTime finish, DateTime now)
        {
            if (start > now) return ManualSessionProblem.StartInFuture;
            if (finish > now) return ManualSessionProblem.FinishInFuture;
            if (finish <= start) return ManualSessionProblem.FinishNotAfterStart;
            return null;
        }

        /// <summary>Web <c>resolveManualFocusModeId</c>: the "Manual Entry" focus mode when there is one; the server makes one otherwise.</summary>
        public static string ManualFocusModeId(IEnumerable<FocusModeSummary> modes) =>
            modes?.FirstOrDefault(m => m.IsManualEntry)?.Id;

        /// <summary>Web <c>mergeFocusSummariesById</c>: keep order, drop rows already listed.</summary>
        public static List<FocusLogEntry> Merge(IList<FocusLogEntry> current, IEnumerable<FocusLogEntry> page)
        {
            var known = new HashSet<string>(current.Select(e => e.Id));
            return current.Concat(page.Where(e => known.Add(e.Id))).ToList();
        }
    }

    public enum ManualSessionProblem { StartInFuture, FinishInFuture, FinishNotAfterStart }

    /// <summary>Unit words for <see cref="FocusLogMath.HumanizeSeconds"/>; English by default.</summary>
    public sealed class DurationWords
    {
        public string Second { get; set; } = "second";
        public string Seconds { get; set; } = "seconds";
        public string Minute { get; set; } = "minute";
        public string Minutes { get; set; } = "minutes";
        public string Hour { get; set; } = "hour";
        public string Hours { get; set; } = "hours";
        public string Day { get; set; } = "day";
        public string Days { get; set; } = "days";

        public static readonly DurationWords English = new DurationWords();
    }

    // ================================================================== levels

    public enum LevelRequirementKind { Morning, MicroBreak, Evening, FocusMode, Chat, FocusModeSetup }

    public sealed class LevelRequirement
    {
        public LevelRequirement(LevelRequirementKind kind, int current, int target)
        {
            Kind = kind;
            Current = current;
            Target = target;
        }

        public LevelRequirementKind Kind { get; }
        public int Current { get; }
        public int Target { get; }
        /// <summary>Yes/no goals ("Chat with Focus Bear") show a tick instead of a counter.</summary>
        public bool IsBoolean => Kind == LevelRequirementKind.Chat || Kind == LevelRequirementKind.FocusModeSetup;
        public bool IsMet => Current >= Target;
    }

    /// <summary>Port of the Mac's <c>LevelTargets</c>, itself the web's <c>GOALS_LEVEL_TARGET</c>.</summary>
    public static class LevelTargets
    {
        public const int MaxLevel = 9;
        public const int VisibleLevels = 7;
        public const int ChatAndFocusModeLevel = 3;

        /// <summary>(morning, micro break, evening, focus mode) streak days per level.</summary>
        private static readonly Dictionary<int, int[]> Table = new Dictionary<int, int[]>
        {
            { 1, new[] { 1, 1, 1, 1 } }, { 2, new[] { 2, 2, 2, 2 } }, { 3, new[] { 5, 5, 5, 5 } },
            { 4, new[] { 7, 7, 7, 10 } }, { 5, new[] { 10, 10, 10, 15 } }, { 6, new[] { 15, 15, 15, 30 } },
            { 7, new[] { 30, 30, 30, 50 } }, { 8, new[] { 60, 60, 60, 100 } }, { 9, new[] { 100, 100, 100, 200 } },
        };

        public static IList<LevelRequirement> Requirements(int level, OnboardingStats stats)
        {
            if (!Table.TryGetValue(level, out int[] t)) return new List<LevelRequirement>();
            var list = new List<LevelRequirement>
            {
                new LevelRequirement(LevelRequirementKind.Morning, stats.MorningRoutineCompletionStreakDays ?? 0, t[0]),
                new LevelRequirement(LevelRequirementKind.MicroBreak, stats.BreakRoutineCompletionStreakDays ?? 0, t[1]),
                new LevelRequirement(LevelRequirementKind.Evening, stats.EveningRoutineCompletionStreakDays ?? 0, t[2]),
                new LevelRequirement(LevelRequirementKind.FocusMode, stats.FocusModeCompletionStreakDays ?? 0, t[3]),
            };
            if (level == ChatAndFocusModeLevel)
            {
                list.Add(new LevelRequirement(LevelRequirementKind.Chat, stats.HasChattedWithFocusBear == true ? 1 : 0, 1));
                list.Add(new LevelRequirement(LevelRequirementKind.FocusModeSetup, stats.HasEditedFocusMode == true ? 1 : 0, 1));
            }
            return list;
        }

        /// <summary>
        /// Web <c>computeVisibleHeaderRange</c>: which of the Start / Level 1…9 / End headers (indices 0…10) are
        /// shown — a window of seven ending at the current level once past level 5. [start, end).
        /// </summary>
        public static (int Start, int End) VisibleHeaderRange(int level)
        {
            int windowEnd = VisibleLevels - 1;
            int start = level < windowEnd ? 0 : level - windowEnd + 1;
            int end = level >= windowEnd ? level + 1 : VisibleLevels;
            return (start, Math.Min(end, MaxLevel + 2));
        }
    }

    /// <summary>Explainer links the web's onboarding rows open (<c>GOALS_INSTRUCTION_URL</c>, <c>DOWNLOAD_LINK.MOBILE</c>).</summary>
    public static class StatsExternalLinks
    {
        public const string SetupBlockedUrls = "https://youtu.be/3U3ktaodAzk";
        public const string InstallMobileApp = "https://youtube.com/shorts/zv6be2sNOsk";
        public const string EditHabits = "https://youtu.be/BCVdt210PGA";
        public const string IosApp = "https://apps.apple.com/us/app/focus-bear/id1673296334";
        public const string AndroidApp = "https://play.google.com/store/apps/details?id=com.focusbear";
    }

    // ================================================================== goals, username

    public static class LongTermGoals
    {
        /// <summary>What Save sends: trimmed, blanks dropped.</summary>
        public static List<string> Cleaned(IEnumerable<string> goals) =>
            (goals ?? Enumerable.Empty<string>()).Select(g => (g ?? "").Trim()).Where(g => g.Length > 0).ToList();
    }

    public static class LeaderboardUsername
    {
        /// <summary>The web validates against <c>/^[A-Za-z_]*$/</c>; an empty name is not a name.</summary>
        public static bool IsValid(string username) =>
            !string.IsNullOrEmpty(username) && username.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_');

        public static bool CanUpdate(string draft, string saved, bool isSaving) => IsValid(draft) && draft != saved && !isSaving;
    }

    /// <summary>Sunday-first short weekday names in the app's language, for the heat map's rows.</summary>
    public static class StatsWeekdays
    {
        public static IList<string> Short(string languageCode)
        {
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(string.IsNullOrEmpty(languageCode) ? "en" : languageCode); }
            catch (CultureNotFoundException) { culture = CultureInfo.GetCultureInfo("en"); }
            // .NET's AbbreviatedDayNames are already Sunday-first, like the web's moment() weeks.
            return culture.DateTimeFormat.AbbreviatedDayNames.ToList();
        }
    }
}
