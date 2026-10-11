using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FocusBear.Core.ToDo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary><c>GET /user/stats/onboarding</c> — the Streak and Progress tabs.</summary>
    public sealed class OnboardingStats
    {
        public int Level { get; set; }
        public int? MorningRoutineCompletionStreakDays { get; set; }
        public int? EveningRoutineCompletionStreakDays { get; set; }
        public int? FocusModeCompletionStreakDays { get; set; }
        public int? BreakRoutineCompletionStreakDays { get; set; }
        public double? AverageMorningRoutinesCompletionPercentage { get; set; }
        public double? AverageEveningRoutinesCompletionPercentage { get; set; }
        public double? AverageNumFocusModesCompletedPerDay { get; set; }
        public int? RoutinesThreshold { get; set; }
        public int? FocusModesThreshold { get; set; }
        public bool? HasChattedWithFocusBear { get; set; }
        public bool? HasEditedFocusMode { get; set; }
        public bool? HasEditedAlwaysBlockedUrls { get; set; }
        public bool? HasInstalledMobileApp { get; set; }
        public bool? HasEditedSettings { get; set; }
    }

    /// <summary>A habit's log-quantity question, and whether its daily figure is a total or a mean.</summary>
    public sealed class LogQuantityQuestion
    {
        public string Id { get; set; }
        public string Question { get; set; }
        /// <summary>Settings key <c>log_summary_type</c>: "SUM" or "AVG".</summary>
        public string LogSummaryType { get; set; }

        public bool IsAveraged => string.Equals(LogSummaryType ?? "", "AVG", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The slice of a routine activity the Habit Stats tab needs (from <c>GET /user-settings</c>).</summary>
    public sealed class StatsActivity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HabitIcon { get; set; }
        public DateTime? CreatedAt { get; set; }
        public IList<LogQuantityQuestion> Questions { get; set; } = new List<LogQuantityQuestion>();

        /// <summary>Icon and name, as the web and the Mac title a habit.</summary>
        public string Title => string.Join(" ", new[] { HabitIcon, Name }.Where(s => !string.IsNullOrEmpty(s)));
    }

    public sealed class StatsUserSettings
    {
        public IList<StatsActivity> MorningActivities { get; set; } = new List<StatsActivity>();
        public IList<StatsActivity> BreakActivities { get; set; } = new List<StatsActivity>();
        public IList<StatsActivity> EveningActivities { get; set; } = new List<StatsActivity>();
        public DateTime? CreatedAt { get; set; }

        /// <summary>Web <c>getUniqueRoutineActivities</c>: morning, break, evening — first occurrence of each id wins.</summary>
        public IList<StatsActivity> UniqueActivities
        {
            get
            {
                var seen = new HashSet<string>();
                return MorningActivities.Concat(BreakActivities).Concat(EveningActivities)
                    .Where(a => a.Id != null && seen.Add(a.Id)).ToList();
            }
        }
    }

    /// <summary><c>GET /completed-activity/:id/stats</c>.</summary>
    public sealed class CompletedActivityStats
    {
        public IList<ActivityStatItem> Items { get; set; } = new List<ActivityStatItem>();
        /// <summary>"duration" or "quantity" — what each item's summary measures.</summary>
        public string StatType { get; set; }

        /// <summary>
        /// True when the summary counts seconds. For a quantity habit it is the logged amount, so an "hours
        /// spent" figure derived from it would be nonsense and must be hidden.
        /// </summary>
        public bool IsDurationBased => string.Equals(StatType ?? "duration", "duration", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>GET /completed-focus-blocks</c> item. <see cref="DurationSeconds"/> is wall-clock seconds.</summary>
    public sealed class FocusSessionSummary
    {
        public string Name { get; set; }
        public DateTime? StartTime { get; set; }
        public double? DurationSeconds { get; set; }
        public double? FocusAlignmentScore { get; set; }
        public double? AverageTabCount { get; set; }
        public double? AverageTabDuration { get; set; }
    }

    /// <summary><c>GET /user/weekly-focus-block-summary</c> row.</summary>
    public sealed class FocusLogEntry
    {
        public string Id { get; set; }
        /// <summary>Kept exactly as sent: sorting compares these as ISO strings, as the Mac and the web do.</summary>
        public string StartTime { get; set; }
        public string FinishTime { get; set; }
        public double? FocusDurationSeconds { get; set; }
        public string Intention { get; set; }
        public string Achievements { get; set; }
        public string Distractions { get; set; }
        public string CreatedAt { get; set; }
        public bool IsManual { get; set; }
        public string FocusModeId { get; set; }
        public string FocusModeName { get; set; } = "";
    }

    /// <summary><c>GET /user/focus-modes</c> — only what manual-session creation needs.</summary>
    public sealed class FocusModeSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsManualEntry { get; set; }
    }

    public sealed class LeaderboardResponse
    {
        public LeaderboardUser UserRank { get; set; }
        public IList<LeaderboardUser> UsersRankings { get; set; } = new List<LeaderboardUser>();
    }

    /// <summary>
    /// Reads the stats endpoints. Every reader takes the raw body and returns null for anything unusable,
    /// so the caller has one failure path. Parsed without date conversion: Newtonsoft's default turns ISO
    /// strings into DateTime, and the focus log sorts on those strings exactly as sent.
    /// </summary>
    public static class StatsWire
    {
        public static JToken Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    return JToken.ReadFrom(reader);
            }
            catch (JsonException) { return null; }
        }

        private static string Str(JToken t) => t != null && t.Type == JTokenType.String ? (string)t : null;
        private static int? Int(JToken t)
        {
            double? d = FlexibleNumber.Read(t);
            return d.HasValue ? (int?)SwiftRounding.RoundedToInt(d.Value) : null;
        }
        private static bool? Bool(JToken t) => t != null && t.Type == JTokenType.Boolean ? (bool?)(bool)t : null;
        private static DateTime? Instant(JToken t) => ToDoDates.Parse(Str(t));

        public static OnboardingStats ReadOnboardingStats(string json)
        {
            if (!(Parse(json) is JObject o)) return null;
            return new OnboardingStats
            {
                Level = Int(o["level"]) ?? 0,
                MorningRoutineCompletionStreakDays = Int(o["morning_routine_completion_streak_days"]),
                EveningRoutineCompletionStreakDays = Int(o["evening_routine_completion_streak_days"]),
                FocusModeCompletionStreakDays = Int(o["focus_mode_completion_streak_days"]),
                BreakRoutineCompletionStreakDays = Int(o["break_routine_completion_streak_days"]),
                AverageMorningRoutinesCompletionPercentage = FlexibleNumber.Read(o["average_morning_routines_completion_percentage"]),
                AverageEveningRoutinesCompletionPercentage = FlexibleNumber.Read(o["average_evening_routines_completion_percentage"]),
                AverageNumFocusModesCompletedPerDay = FlexibleNumber.Read(o["average_num_focus_modes_completed_per_day"]),
                RoutinesThreshold = Int(o["routines_threshold"]),
                FocusModesThreshold = Int(o["focus_modes_threshold"]),
                HasChattedWithFocusBear = Bool(o["has_chatted_with_focus_bear"]),
                HasEditedFocusMode = Bool(o["has_edited_focus_mode"]),
                HasEditedAlwaysBlockedUrls = Bool(o["has_edited_always_blocked_urls"]),
                HasInstalledMobileApp = Bool(o["has_installed_mobile_app"]),
                HasEditedSettings = Bool(o["has_edited_settings"]),
            };
        }

        /// <summary><c>GET /user/long-term-goals</c>: a plain array of strings.</summary>
        public static List<string> ReadGoals(string json)
        {
            if (!(Parse(json) is JArray a)) return null;
            return a.Select(Str).Where(s => s != null).ToList();
        }

        public static StatsUserSettings ReadUserSettings(string json)
        {
            if (!(Parse(json) is JObject o)) return null;
            return new StatsUserSettings
            {
                MorningActivities = Activities(o["morning_activities"]),
                BreakActivities = Activities(o["break_activities"]),
                EveningActivities = Activities(o["evening_activities"]),
                CreatedAt = Instant(o["created_at"]),
            };
        }

        private static IList<StatsActivity> Activities(JToken token)
        {
            var list = new List<StatsActivity>();
            foreach (var a in (token as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                string id = Str(a["id"]);
                if (id == null) continue;
                var data = a["activity_data"] as JObject;
                list.Add(new StatsActivity
                {
                    Id = id,
                    // /user-settings carries the name and icon at the top level on current payloads and
                    // inside activity_data on older ones.
                    Name = Str(a["name"]) ?? Str(data?["name"]),
                    HabitIcon = Str(a["habit_icon"]) ?? Str(data?["habit_icon"]),
                    CreatedAt = Instant(a["created_at"]),
                    Questions = ((a["log_quantity_questions"] ?? data?["log_quantity_questions"]) as JArray)?.OfType<JObject>()
                        .Where(q => Str(q["id"]) != null)
                        .Select(q => new LogQuantityQuestion { Id = Str(q["id"]), Question = Str(q["question"]), LogSummaryType = Str(q["log_summary_type"]) })
                        .ToList() ?? new List<LogQuantityQuestion>(),
                });
            }
            return list;
        }

        /// <summary><c>POST /completed-activity/consistency-stats</c>: completed days by activity id.</summary>
        public static Dictionary<string, int> ReadConsistency(string json)
        {
            if (!(Parse(json) is JObject o) || !(o["habits"] is JArray habits)) return null;
            var result = new Dictionary<string, int>();
            foreach (var h in habits.OfType<JObject>())
            {
                string id = Str(h["activity_id"]);
                int? days = Int(h["completed_days"]);
                if (id != null && days.HasValue) result[id] = days.Value;
            }
            return result;
        }

        public static CompletedActivityStats ReadActivityStats(string json)
        {
            if (!(Parse(json) is JObject o)) return null;
            return new CompletedActivityStats { Items = Items(o["items"]), StatType = Str(o["stat_type"]) };
        }

        /// <summary><c>GET /completed-activity/log-question/:id/stats</c>.</summary>
        public static IList<ActivityStatItem> ReadQuestionStats(string json)
        {
            return Parse(json) is JObject o ? Items(o["items"]) : null;
        }

        private static IList<ActivityStatItem> Items(JToken token)
        {
            var list = new List<ActivityStatItem>();
            foreach (var i in (token as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                DateTimeOffset? date = null;
                if (DateTimeOffset.TryParse(Str(i["date"]), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) date = parsed;
                list.Add(new ActivityStatItem
                {
                    Date = date,
                    Count = Int(i["count"]) ?? 0,
                    Summary = FlexibleNumber.Read(i["summary"]) ?? 0,
                });
            }
            return list;
        }

        public static List<FocusSessionSummary> ReadFocusSessions(string json)
        {
            if (!(Parse(json) is JArray a)) return null;
            return a.OfType<JObject>().Select(s => new FocusSessionSummary
            {
                Name = Str(s["name"]),
                StartTime = Instant(s["start_time"]),
                DurationSeconds = FlexibleNumber.Read(s["duration"]),
                FocusAlignmentScore = FlexibleNumber.Read((s["metadata"] as JObject)?["focus_alignment_score"]),
                AverageTabCount = FlexibleNumber.Read((s["metadata"] as JObject)?["average_tab_count"]),
                AverageTabDuration = FlexibleNumber.Read((s["metadata"] as JObject)?["average_tab_duration"]),
            }).ToList();
        }

        public static List<FocusLogEntry> ReadFocusLog(string json)
        {
            if (!(Parse(json) is JArray a)) return null;
            return a.OfType<JObject>().Where(e => Str(e["id"]) != null).Select(e => new FocusLogEntry
            {
                Id = Str(e["id"]),
                StartTime = Str(e["start_time"]),
                FinishTime = Str(e["finish_time"]),
                FocusDurationSeconds = FlexibleNumber.Read(e["focus_duration_seconds"]),
                Intention = Str(e["intention"]),
                Achievements = Str(e["achievements"]),
                Distractions = Str(e["distractions"]),
                CreatedAt = Str(e["created_at"]),
                IsManual = Bool((e["metadata"] as JObject)?["is_manual"]) ?? false,
                FocusModeId = Str((e["focus_mode"] as JObject)?["id"]),
                FocusModeName = Str((e["focus_mode"] as JObject)?["name"]) ?? "",
            }).ToList();
        }

        public static List<FocusModeSummary> ReadFocusModes(string json)
        {
            if (!(Parse(json) is JArray a)) return null;
            return a.OfType<JObject>().Where(m => Str(m["id"]) != null).Select(m => new FocusModeSummary
            {
                Id = Str(m["id"]),
                Name = Str(m["name"]),
                IsManualEntry = Bool((m["metadata"] as JObject)?["isManualEntry"]) ?? false,
            }).ToList();
        }

        public static LeaderboardResponse ReadLeaderboard(string json)
        {
            if (!(Parse(json) is JObject o) || !(o["users_rankings"] is JArray rows)) return null;
            return new LeaderboardResponse
            {
                UserRank = o["user_rank"] is JObject me ? LeaderboardUser.FromJson(me) : null,
                UsersRankings = rows.OfType<JObject>().Select(LeaderboardUser.FromJson).Where(u => u.Id != null).ToList(),
            };
        }

        // ------------------------------------------------------------------ request bodies

        public static JObject GoalsBody(IEnumerable<string> goals) => new JObject { ["goals"] = new JArray(goals) };

        public static JObject ConsistencyBody(IEnumerable<string> activityIds, int days, string timezone) => new JObject
        {
            ["activity_ids"] = new JArray(activityIds),
            ["days_number"] = days,
            ["timezone"] = timezone,
        };

        public static JObject CreateFocusBlockBody(string intention, string achievements, string distractions, DateTime start, DateTime finish, string focusModeId)
        {
            var body = new JObject
            {
                ["intention"] = intention ?? "",
                ["achievements"] = achievements ?? "",
                ["distractions"] = distractions ?? "",
                ["focus_duration_seconds"] = Math.Max((int)SwiftRounding.Rounded((finish - start).TotalSeconds), 0),
                ["start_time"] = Iso(start),
                ["finish_time"] = Iso(finish),
            };
            if (!string.IsNullOrEmpty(focusModeId)) body["focus_mode_id"] = focusModeId;
            return body;
        }

        public static JObject UpdateFocusBlockBody(string intention, string achievements, string distractions) => new JObject
        {
            ["intention"] = intention ?? "",
            ["achievements"] = achievements ?? "",
            ["distractions"] = distractions ?? "",
        };

        public static JObject UsernameBody(string username) => new JObject { ["username"] = username };

        /// <summary>ISO-8601 in UTC with milliseconds, as the Mac's <c>StatsDates.iso</c> writes it.</summary>
        public static string Iso(DateTime local) =>
            local.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}
