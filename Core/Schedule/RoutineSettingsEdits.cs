using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Keys of the user-settings JSON touched by the Day Plan's editors. Mirrors the Mac's
    /// <c>RoutineSettingsKey</c> (DayPlanRoutineDraft.swift), which mirrors the web dashboard.
    /// </summary>
    public static class RoutineSettingsKey
    {
        public const string MorningActivities = "morning_activities";
        public const string EveningActivities = "evening_activities";
        public const string StartupTime = "startup_time";
        public const string ShutdownTime = "shutdown_time";
        public const string EveningCutoffTime = "cutoff_time_for_non_high_priority_activities";

        public const string Id = "id";
        public const string Icon = "habit_icon";
        public const string Name = "name";
        public const string DurationSeconds = "duration_seconds";
        public const string CompletionRequirements = "completion_requirements";
        public const string Priority = "priority";
        public const string DaysOfWeek = "days_of_week";

        public const string PriorityHigh = "HIGH";
        public const string PriorityStandard = "STANDARD";
    }

    /// <summary>
    /// "Edit routine timing" from the Day Plan's routine menu — when the morning routine starts, when
    /// the evening routine starts, and the evening's global cutoff. Ported from the Mac's
    /// <c>DayPlanRoutineDraft.applyRoutineTiming</c> / <c>isEveningCutoffValid</c> and
    /// <c>RoutineTimingView</c>. Pure: it edits a settings JSON object and says whether the edit was
    /// valid; the host app decides how to save it.
    /// </summary>
    public static class RoutineTiming
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>"HH:mm" or "HH:mm:ss" to minutes past midnight; null when it isn't a time of day.</summary>
        public static int? MinutesFromTimeString(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] parts = text.Trim().Split(':');
            if (parts.Length < 2) return null;
            int hours, minutes;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours)) return null;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes)) return null;
            if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return null;
            return hours * 60 + minutes;
        }

        /// <summary>
        /// A time of day as a person types it into the timing field: "7:30", "07:30", "19:30", "7:30pm",
        /// "7:30 PM", "7pm", "7.30am", "12am" (midnight). Null when it doesn't read as one.
        /// </summary>
        public static int? ParseTypedTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim().ToLowerInvariant().Replace(" ", "").Replace(".m.", "m").Replace(".m", "m");
            bool? pm = null;
            if (t.EndsWith("am") || t.EndsWith("pm")) { pm = t.EndsWith("pm"); t = t.Substring(0, t.Length - 2); }
            else if (t.EndsWith("a") || t.EndsWith("p")) { pm = t.EndsWith("p"); t = t.Substring(0, t.Length - 1); }
            string[] parts = t.Split(':', '.');
            if (parts.Length > 2 || parts[0].Length == 0 || parts[0].Length > 2) return null;
            int hours, minutes = 0;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours)) return null;
            if (parts.Length == 2
                && (parts[1].Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes)))
                return null;
            if (minutes > 59) return null;
            if (pm.HasValue)
            {
                if (hours < 1 || hours > 12) return null;
                hours = hours % 12 + (pm.Value ? 12 : 0);
            }
            else if (hours > 23)
            {
                return null;
            }
            return hours * 60 + minutes;
        }

        /// <summary>Minutes past midnight to the settings' 24-hour "HH:mm".</summary>
        public static string TimeStringFromMinutes(int minutes)
        {
            int m = ((minutes % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":"
                   + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The evening cutoff must fall inside the evening routine's window, which runs from the
        /// evening start round midnight to the next morning's start. No cutoff is always valid; with no
        /// known morning start any real time of day is. Per-habit late cutoffs are separate and are not
        /// checked here.
        /// </summary>
        public static bool IsEveningCutoffValid(int? cutoff, int eveningStart, int? morningStart)
        {
            if (eveningStart < 0 || eveningStart >= MinutesPerDay) return false;
            if (!cutoff.HasValue) return true;
            if (cutoff.Value < 0 || cutoff.Value >= MinutesPerDay) return false;
            if (!morningStart.HasValue || morningStart.Value < 0 || morningStart.Value >= MinutesPerDay) return true;
            int routineWindow = (morningStart.Value - eveningStart + MinutesPerDay) % MinutesPerDay;
            int untilCutoff = (cutoff.Value - eveningStart + MinutesPerDay) % MinutesPerDay;
            return routineWindow > 0 && untilCutoff <= routineWindow;
        }

        /// <summary>The routine's start from the settings: startup time for the morning, shutdown
        /// time for the evening. Null when unset or unreadable.</summary>
        public static int? StartMinutes(JObject settings, RoutineSettingsSection section)
        {
            if (settings == null) return null;
            switch (section)
            {
                case RoutineSettingsSection.Morning:
                    return MinutesFromTimeString((string)settings[RoutineSettingsKey.StartupTime]);
                case RoutineSettingsSection.Evening:
                    return MinutesFromTimeString((string)settings[RoutineSettingsKey.ShutdownTime]);
                default:
                    return null;
            }
        }

        /// <summary>The global evening cutoff, or null when none is set.</summary>
        public static int? EveningCutoffMinutes(JObject settings)
        {
            return settings == null ? null : MinutesFromTimeString((string)settings[RoutineSettingsKey.EveningCutoffTime]);
        }

        /// <summary>
        /// Writes a new start (and, for the evening, the global cutoff — null removes it) into the
        /// settings. Invalid input changes nothing and returns false. Custom routines are not supported
        /// on Windows yet.
        /// </summary>
        public static bool Apply(JObject settings, RoutineSettingsSection section, int minutes, int? eveningCutoffMinutes = null)
        {
            if (settings == null || minutes < 0 || minutes >= MinutesPerDay) return false;
            switch (section)
            {
                case RoutineSettingsSection.Morning:
                    settings[RoutineSettingsKey.StartupTime] = TimeStringFromMinutes(minutes);
                    return true;
                case RoutineSettingsSection.Evening:
                    int? morning = StartMinutes(settings, RoutineSettingsSection.Morning);
                    if (!IsEveningCutoffValid(eveningCutoffMinutes, minutes, morning)) return false;
                    settings[RoutineSettingsKey.ShutdownTime] = TimeStringFromMinutes(minutes);
                    if (eveningCutoffMinutes.HasValue)
                        settings[RoutineSettingsKey.EveningCutoffTime] = TimeStringFromMinutes(eveningCutoffMinutes.Value);
                    else
                        settings.Remove(RoutineSettingsKey.EveningCutoffTime);
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>The <c>take_notes</c> / <c>log_summary_type</c> values the server uses.</summary>
    public static class HabitNotes
    {
        public const string None = "NO_NOTE";
        public const string During = "DURING_ACTIVITY";
        public const string End = "END_OF_ACTIVITY";
        public const string SummarySum = "SUM";
        public const string SummaryAverage = "AVG";
    }

    /// <summary>
    /// The Day Plan's "Edit" action on a habit: reads a habit's entry from the settings JSON into the
    /// editor's <see cref="HabitEditValues"/> and writes the edited values back — every field the Mac's
    /// editor offers (DayPlanRoutineDraft.habitEditValues / writeHabitEntry): timer or completion,
    /// priority, days, late cutoff, videos, allowed apps / sites / focus mode, micro-breaks, the
    /// guilt-free popup, log-data questions, notes, checklist, instructions, images and tags. Keys the
    /// editor doesn't show (question descriptions, image file paths, random choices…) are left as they were.
    /// </summary>
    public static class HabitSettingsEdit
    {
        public const string CutoffTime = "cutoff_time_for_doing_activity";
        public const string VideoUrls = "video_urls";
        public const string AllowedApps = "allowed_apps";
        public const string AllowedUrls = "allowed_urls";
        public const string RunMicroBreaks = "run_micro_breaks";
        public const string AllowedFocusModeId = "allowed_focus_mode_id";
        public const string ShowSavedDistractingWebsites = "show_saved_distracting_websites";
        public const string LogQuantity = "log_quantity";
        public const string LogQuantityQuestions = "log_quantity_questions";
        public const string LogSummaryType = "log_summary_type";
        public const string TakeNotes = "take_notes";
        public const string ChoiceType = "choice_type";
        public const string ChoiceTypeCheckList = "check_list";
        public const string CheckList = "check_list";
        public const string Choices = "choices";
        public const string TextInstructions = "text_instructions";
        public const string ImageUrls = "image_urls";
        public const string ImageUrl = "url";
        public const string Tags = "tags";
        public const string QuestionId = "id";
        public const string Question = "question";
        public const string MinValue = "min_value";
        public const string MaxValue = "max_value";

        /// <summary>The habit's routine and its JSON entry, or null when the id isn't in the settings.</summary>
        public static Tuple<RoutineSettingsSection, JObject> Locate(JObject settings, string habitId)
        {
            var list = LocateInList(settings, habitId);
            return list == null ? null : Tuple.Create(list.Item1, (JObject)list.Item2[list.Item3]);
        }

        private static Tuple<RoutineSettingsSection, JArray, int> LocateInList(JObject settings, string habitId)
        {
            if (settings == null || string.IsNullOrEmpty(habitId)) return null;
            var lists = new[]
            {
                Tuple.Create(RoutineSettingsSection.Morning, RoutineSettingsKey.MorningActivities),
                Tuple.Create(RoutineSettingsSection.Evening, RoutineSettingsKey.EveningActivities),
            };
            foreach (var list in lists)
            {
                JArray array = settings[list.Item2] as JArray;
                if (array == null) continue;
                for (int i = 0; i < array.Count; i++)
                {
                    JObject entry = array[i] as JObject;
                    if (entry != null && (string)entry[RoutineSettingsKey.Id] == habitId)
                        return Tuple.Create(list.Item1, array, i);
                }
            }
            return null;
        }

        /// <summary>Removes the habit from its routine (the web's plain splice). False when it isn't there.</summary>
        public static bool Delete(JObject settings, string habitId)
        {
            var located = LocateInList(settings, habitId);
            if (located == null) return false;
            located.Item2.RemoveAt(located.Item3);
            return true;
        }

        /// <summary>The server's day list (codes or the "all"/"weekdays"/"weekend" tokens) as codes.
        /// A habit with no list runs every day.</summary>
        public static ISet<string> ExpandDays(IEnumerable<string> days)
        {
            var result = new HashSet<string>();
            List<string> list = (days ?? Enumerable.Empty<string>()).Where(d => d != null).ToList();
            if (list.Count == 0) return new HashSet<string>(WeekdayCode.All);
            foreach (string code in WeekdayCode.All)
            {
                if (RoutineDaysOfWeek.IsScheduledForWeekday(list, code)) result.Add(code);
            }
            return result;
        }

        /// <summary>Codes in the web's Monday-first order.</summary>
        public static List<string> OrderedDays(IEnumerable<string> days)
        {
            var set = new HashSet<string>((days ?? Enumerable.Empty<string>()).Select(d => d.ToUpperInvariant()));
            return WeekdayCode.All.Where(set.Contains).ToList();
        }

        private static List<string> Strings(JToken token)
        {
            JArray arr = token as JArray;
            if (arr == null) return new List<string>();
            return arr.Where(t => t.Type == JTokenType.String).Select(t => (string)t).ToList();
        }

        private static int? Int(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Integer) return (int)token;
            if (token.Type == JTokenType.Float) return (int)Math.Round((double)token);
            return null;
        }

        private static bool Bool(JToken token)
        {
            return token != null && token.Type == JTokenType.Boolean && (bool)token;
        }

        public static HabitEditValues Read(JObject entry)
        {
            string completion = (string)entry[RoutineSettingsKey.CompletionRequirements] ?? string.Empty;
            int duration = Int(entry[RoutineSettingsKey.DurationSeconds]) ?? 0;
            JArray days = entry[RoutineSettingsKey.DaysOfWeek] as JArray;
            string priority = (string)entry[RoutineSettingsKey.Priority];
            int? cutoff = RoutineTiming.MinutesFromTimeString((string)entry[CutoffTime]);

            var questions = new List<HabitLogQuestionEdit>();
            foreach (JObject q in (entry[LogQuantityQuestions] as JArray ?? new JArray()).OfType<JObject>())
            {
                int? min = Int(q[MinValue]), max = Int(q[MaxValue]);
                // The API stores "unset" as 0/0 (the web denormalises on the boundary); show that as blank.
                bool unset = (min ?? 0) == 0 && (max ?? 0) == 0;
                questions.Add(new HabitLogQuestionEdit((string)q[QuestionId] ?? Guid.NewGuid().ToString(),
                                                       (string)q[Question] ?? string.Empty,
                                                       unset ? string.Empty : (min ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                       unset ? string.Empty : (max ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            var images = (entry[ImageUrls] as JArray ?? new JArray()).OfType<JObject>()
                .Select(o => (string)o[ImageUrl]).Where(u => !string.IsNullOrEmpty(u)).ToList();
            JArray choices = entry[Choices] as JArray;

            return new HabitEditValues
            {
                Icon = (string)entry[RoutineSettingsKey.Icon] ?? string.Empty,
                Name = (string)entry[RoutineSettingsKey.Name] ?? string.Empty,
                // As the app's parser decides it: only an explicit completion requirement makes a habit
                // a non-timer "I'm done" habit.
                UsesTimer = completion.Trim().Length == 0,
                DurationSeconds = duration,
                CompletionRequirement = completion,
                IsHighPriority = priority == null
                                 || string.Equals(priority, RoutineSettingsKey.PriorityHigh, StringComparison.OrdinalIgnoreCase),
                DaysOfWeek = ExpandDays(days == null ? null : days.Select(d => (string)d)),
                CutoffEnabled = cutoff.HasValue,
                CutoffMinutes = cutoff ?? 21 * 60 + 30,
                VideoUrls = Strings(entry[VideoUrls]),
                AllowedApps = Strings(entry[AllowedApps]),
                AllowedUrls = Strings(entry[AllowedUrls]),
                RunMicroBreaks = Bool(entry[RunMicroBreaks]),
                AllowedFocusModeId = (string)entry[AllowedFocusModeId] ?? string.Empty,
                ShowSavedDistractingWebsites = Bool(entry[ShowSavedDistractingWebsites]),
                LogQuantity = Bool(entry[LogQuantity]),
                LogQuestions = questions,
                LogSummaryType = (string)entry[LogSummaryType] ?? HabitNotes.SummarySum,
                TakeNotes = (string)entry[TakeNotes] ?? HabitNotes.None,
                Checklist = Strings(entry[CheckList]),
                TextInstructions = (string)entry[TextInstructions] ?? string.Empty,
                ImageUrls = images,
                Tags = Strings(entry[Tags]),
                RandomChoiceCount = choices == null ? 0 : choices.Count,
            };
        }

        public static string ReadIcon(JObject entry)
        {
            return (string)entry[RoutineSettingsKey.Icon] ?? string.Empty;
        }

        /// <summary>The web strips the scheme from allowed URLs before saving.</summary>
        public static string StripScheme(string url)
        {
            return System.Text.RegularExpressions.Regex.Replace(url ?? string.Empty, "^https?://", string.Empty,
                                                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Writes the editor's fields onto the entry (Mac writeHabitEntry). Days are only rewritten when they
        /// actually changed, so a habit stored as "weekdays" isn't needlessly expanded into five codes. Keys the
        /// API validates as "absent or typed" (a UUID, a military time) are removed rather than written empty.
        /// </summary>
        public static void Write(JObject entry, RoutineSettingsSection section, string icon, HabitEditValues values)
        {
            entry[RoutineSettingsKey.Icon] = (icon ?? values.Icon ?? string.Empty).Trim();
            entry[RoutineSettingsKey.Name] = values.TrimmedName;
            if (values.UsesTimer)
            {
                entry[RoutineSettingsKey.DurationSeconds] = values.DurationSeconds;
                entry.Remove(RoutineSettingsKey.CompletionRequirements);   // web: undefined => timer habit
            }
            else
            {
                entry[RoutineSettingsKey.CompletionRequirements] = (values.CompletionRequirement ?? string.Empty).Trim();
            }
            if (HabitEditValidator.ShowsPriority(section))
            {
                entry[RoutineSettingsKey.Priority] = values.IsHighPriority
                    ? RoutineSettingsKey.PriorityHigh
                    : RoutineSettingsKey.PriorityStandard;
            }
            // The API rejects a cutoff on a morning habit, so the key is only touched for evening habits.
            if (section == RoutineSettingsSection.Evening)
            {
                if (values.IsHighPriority && values.CutoffEnabled)
                    entry[CutoffTime] = RoutineTiming.TimeStringFromMinutes(values.CutoffMinutes);
                else
                    entry.Remove(CutoffTime);
            }
            if (HabitEditValidator.ShowsDaysOfWeek(section))
            {
                JArray stored = entry[RoutineSettingsKey.DaysOfWeek] as JArray;
                ISet<string> before = ExpandDays(stored == null ? null : stored.Select(d => (string)d));
                if (!before.SetEquals(values.DaysOfWeek ?? new HashSet<string>()))
                    entry[RoutineSettingsKey.DaysOfWeek] = new JArray(OrderedDays(values.DaysOfWeek));
            }

            entry[VideoUrls] = new JArray(HabitEditValues.Cleaned(values.VideoUrls));
            IReadOnlyList<string> apps = values.UsesFocusMode ? new List<string>() : HabitEditValues.Cleaned(values.AllowedApps);
            IReadOnlyList<string> urls = values.UsesFocusMode
                ? new List<string>()
                : HabitEditValues.Cleaned(values.AllowedUrls).Select(StripScheme).ToList();
            entry[AllowedApps] = new JArray(apps);
            entry[AllowedUrls] = new JArray(urls);
            entry[RunMicroBreaks] = values.RunMicroBreaks && (apps.Count > 0 || urls.Count > 0);
            if (values.UsesFocusMode) entry[AllowedFocusModeId] = values.AllowedFocusModeId;
            else entry.Remove(AllowedFocusModeId);
            entry[ShowSavedDistractingWebsites] = values.ShowSavedDistractingWebsites;

            // A habit with random choices logs per choice; the API rejects log_quantity / a non-SUM
            // log_summary_type on the parent, so its log keys are left exactly as they were.
            if (values.RandomChoiceCount == 0) WriteLogQuestions(entry, values);
            entry[TakeNotes] = string.IsNullOrEmpty(values.TakeNotes) ? HabitNotes.None : values.TakeNotes;
            if (values.RandomChoiceCount == 0) WriteChecklist(entry, HabitEditValues.Cleaned(values.Checklist));
            entry[TextInstructions] = values.TextInstructions ?? string.Empty;
            WriteImages(entry, HabitEditValues.Cleaned(values.ImageUrls));
            IReadOnlyList<string> tags = HabitEditValues.Cleaned(values.Tags);
            if (tags.Count > 0 || entry[Tags] != null) entry[Tags] = new JArray(tags);
        }

        private static void WriteLogQuestions(JObject entry, HabitEditValues values)
        {
            List<JObject> existing = (entry[LogQuantityQuestions] as JArray ?? new JArray()).OfType<JObject>().ToList();
            var questions = new JArray();
            if (values.LogQuantity)
            {
                foreach (HabitLogQuestionEdit q in values.LogQuestions)
                {
                    // Merge onto the stored question so keys this editor doesn't show (descriptions) survive.
                    JObject dict = existing.FirstOrDefault(e => (string)e[QuestionId] == q.Id) ?? new JObject();
                    dict = (JObject)dict.DeepClone();
                    dict[QuestionId] = q.Id;
                    dict[Question] = (q.Question ?? string.Empty).Trim();
                    int n;
                    dict[MinValue] = int.TryParse((q.MinValue ?? string.Empty).Trim(), out n) ? n : 0;
                    dict[MaxValue] = int.TryParse((q.MaxValue ?? string.Empty).Trim(), out n) ? n : 0;
                    dict[LogSummaryType] = values.LogSummaryType;
                    questions.Add(dict);
                }
            }
            entry[LogQuantity] = values.LogQuantity && questions.Count > 0;
            entry[LogQuantityQuestions] = questions;
            entry[LogSummaryType] = values.LogSummaryType;
        }

        private static void WriteChecklist(JObject entry, IReadOnlyList<string> items)
        {
            if (items.Count > 0)
            {
                entry[ChoiceType] = ChoiceTypeCheckList;
                entry[CheckList] = new JArray(items);
            }
            else
            {
                entry.Remove(CheckList);
                if ((string)entry[ChoiceType] == ChoiceTypeCheckList) entry.Remove(ChoiceType);
            }
        }

        private static void WriteImages(JObject entry, IReadOnlyList<string> urls)
        {
            List<JObject> existing = (entry[ImageUrls] as JArray ?? new JArray()).OfType<JObject>().ToList();
            var images = new JArray();
            foreach (string url in urls)
            {
                JObject kept = existing.FirstOrDefault(e => (string)e[ImageUrl] == url);
                images.Add(kept != null ? kept.DeepClone() : new JObject { [ImageUrl] = url });
            }
            entry[ImageUrls] = images;
        }

        /// <summary>The display name the app builds from an entry: icon, a space, then the name.</summary>
        public static string DisplayName(string icon, string name)
        {
            string i = (icon ?? string.Empty).Trim();
            string n = (name ?? string.Empty).Trim();
            return i.Length > 0 ? i + " " + n : n;
        }
    }
}
