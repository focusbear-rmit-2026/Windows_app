using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.ToDo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Focus
{
    /// <summary>An accordion card of the advanced start screen. At most one is open at a time; all can be collapsed.</summary>
    public enum FocusStartCard
    {
        Duration,
        Goal,
        Blocking,
    }

    /// <summary>
    /// A focus duration as the start screen shows it: whole hours plus minutes, or "untimed" (the Mac's
    /// <c>FocusStartDuration</c>). An untimed session still needs a finish time downstream, so it runs 12 hours.
    /// </summary>
    public sealed class FocusStartDuration : IEquatable<FocusStartDuration>
    {
        public const int UntimedSessionMinutes = 12 * 60;
        public const int MinimumMinutes = 1;
        public const int MaximumMinutes = 8 * 60;
        public const int StepMinutes = 5;
        public const int DefaultMinutes = 25;

        public FocusStartDuration(int minutes, bool isUntimed = false)
        {
            Minutes = minutes;
            IsUntimed = isUntimed;
        }

        /// <summary>The length as the user set it, ignoring <see cref="IsUntimed"/>.</summary>
        public int Minutes { get; }
        public bool IsUntimed { get; }

        /// <summary>What the session actually runs for.</summary>
        public int EffectiveMinutes => IsUntimed ? UntimedSessionMinutes : Math.Max(MinimumMinutes, Minutes);

        public int HoursComponent => Minutes / 60;
        public int MinutesComponent => Minutes % 60;

        /// <summary>The slider's position, 0…1 across the 0–8 HRS track.</summary>
        public double SliderFraction => Math.Min(1, Math.Max(0, (double)Minutes / MaximumMinutes));

        public FocusStartDuration Stepped(int steps) => new FocusStartDuration(Clamp(Minutes + steps * StepMinutes), IsUntimed);

        public FocusStartDuration WithUntimed(bool untimed) => new FocusStartDuration(Minutes, untimed);

        /// <summary>From a slider position, snapped to the stepper's 5-minute grid.</summary>
        public static FocusStartDuration FromSliderFraction(double fraction, bool isUntimed = false)
        {
            double raw = Math.Round(MaximumMinutes * Math.Min(1, Math.Max(0, fraction)), MidpointRounding.AwayFromZero);
            int snapped = (int)Math.Round(raw / StepMinutes, MidpointRounding.AwayFromZero) * StepMinutes;
            return new FocusStartDuration(Clamp(snapped), isUntimed);
        }

        /// <summary>A stored minutes string ("25", junk, empty) as a duration the screen can show.</summary>
        public static FocusStartDuration FromStored(string minutes) =>
            new FocusStartDuration(int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? Clamp(value) : DefaultMinutes);

        public static int Clamp(int value) => Math.Min(MaximumMinutes, Math.Max(MinimumMinutes, value));

        public bool Equals(FocusStartDuration other) => other != null && Minutes == other.Minutes && IsUntimed == other.IsUntimed;
        public override bool Equals(object obj) => Equals(obj as FocusStartDuration);
        public override int GetHashCode() => Minutes * 2 + (IsUntimed ? 1 : 0);
    }

    /// <summary>
    /// The start screens' copy: the config's <c>focusStartVcInfo</c> block (shared with the Mac), with the
    /// Mac's English defaults. Placeholders are <c>&lt;COUNT&gt;</c>, <c>&lt;DURATION&gt;</c>, <c>&lt;NAME&gt;</c>, <c>&lt;TIME&gt;</c>.
    /// </summary>
    public sealed class FocusStartStrings
    {
        public const string Count = "<COUNT>";
        public const string DurationToken = "<DURATION>";

        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "titleSimple", "Start a focus session" },
            { "titleAdvancedPrefix", "Start " }, { "titleAdvancedEmphasis", "an advanced" }, { "titleAdvancedSuffix", " focus session" },
            { "linkAdvancedTop", "Top Secret/" }, { "linkAdvancedBottom", "Extra Powerful Options" },
            { "linkSimpleTop", "Go back/" }, { "linkSimpleBottom", "Regular Options" },
            { "labelSetSessionLength", "Set session length:" },
            { "buttonStartTimed", "Start a <DURATION> session" }, { "buttonStartUntimed", "Start an untimed session" },
            { "buttonStartSession", "Start Session" }, { "buttonStartingSession", "Starting…" }, { "bubbleReadyToFocus", "Ready to focus?" },
            { "titleDurationCard", "How long shall we focus?" }, { "bubbleDuration", "Feeling ambitious? You can tweak the timer." },
            { "labelUntimedMode", "Untimed mode:" }, { "labelSuperFocus", "Super Focus:" }, { "unitSuperFocusSessions", "sessions" },
            { "unitHour", "hr" }, { "unitMinute", "min" }, { "labelSliderMinimum", "0 HRS" }, { "labelSliderMaximum", "8 HRS" },
            { "titleGoalCard", "What would you like to achieve?" }, { "bubbleGoal", "Give this session a job to do" },
            { "placeholderGoal", "Add a new goal or search for a project" }, { "headingProjects", "Projects" },
            { "labelTasks", "Add tasks to focus on:" }, { "placeholderTask", "Search for tasks" },
            { "summaryNoGoal", "No Goal" }, { "summaryTasks", "<COUNT> tasks" }, { "summaryTaskSingular", "<COUNT> task" },
            { "titleBlockingCard", "What distractions shall we block?" }, { "bubbleBlocking", "Choose what to pause during this session" },
            { "labelChooseBlockList", "Choose a blocklist:" }, { "badgeAiEnabled", "AI Enabled" }, { "badgeAiDisabled", "AI Off" },
            { "countAppsAndWebsites", "<COUNT> apps/websites" },
            { "labelFocusAlignmentScore", "Show the distracting-site popup only below a focus alignment score of" },
            { "summaryMinutes", "<COUNT> mins" }, { "summaryUntimed", "Untimed" },
            { "summaryBreaks", "<COUNT> breaks" }, { "summaryBreakSingular", "<COUNT> break" },
            { "titleBreakSettings", "Break settings" },
            { "explainerMicroBreaks", "Micro-breaks are short pauses that run during your focus session." },
            { "summaryMicroBreaksOn", "Micro-breaks on" }, { "summaryMicroBreaksOff", "Micro-breaks off" },
            { "placeholderBlockList", "Type to search block lists" }, { "labelEnableMicroBreaks", "Enable micro-breaks:" },
            { "buttonUseGoal", "Use this goal" }, { "buttonRemoveTask", "Remove task from this session" },
            { "buttonClearSearch", "Clear search" }, { "buttonMoreTasks", "Show more tasks" },
            { "noMatchingTasks", "No matching unfinished tasks" },
            { "buttonAddNewTask", "Add “<NAME>” as a new task" }, { "addingNewTask", "Adding “<NAME>”…" },
            { "couldNotAddNewTask", "“<NAME>” couldn’t be added as a task. Please try again." },
            { "workChoicesUnavailable", "Projects or tasks could not be loaded. Please try again." },
            { "buttonRetryWorkChoices", "Try again" }, { "buttonChangeGoal", "Change goal" }, { "buttonClearGoal", "Clear goal" },
            { "buttonAddMoreTasks", "Add more tasks" }, { "noMatchingBlockLists", "No matching block lists" },
            { "noticeActiveBlockingSchedule", "“<NAME>” is blocking until <TIME>. You can still start a focus session." },
            { "editBlockLists", "Edit block lists" },
        };

        private readonly Dictionary<string, string> _values;

        public FocusStartStrings(JObject config = null)
        {
            _values = new Dictionary<string, string>(Defaults);
            if (config == null) return;
            foreach (var property in config.Properties())
                if (property.Value.Type == JTokenType.String && ((string)property.Value).Length > 0) _values[property.Name] = (string)property.Value;
        }

        public string this[string key] => _values.TryGetValue(key, out var value) ? value : "";

        public string WithCount(string key, int count) => this[key].Replace(Count, count.ToString(CultureInfo.CurrentCulture));

        /// <summary>"25 mins · 1 break" — the collapsed duration card.</summary>
        public string DurationSummary(FocusStartDuration duration, int breakCount) =>
            Join(duration.IsUntimed ? this["summaryUntimed"] : WithCount("summaryMinutes", duration.Minutes),
                 WithCount(breakCount == 1 ? "summaryBreakSingular" : "summaryBreaks", breakCount));

        /// <summary>"No Goal · 0 tasks" / "Ship the release · 1 task".</summary>
        public string GoalSummary(string goal, int taskCount)
        {
            string trimmed = (goal ?? "").Trim();
            return Join(trimmed.Length == 0 ? this["summaryNoGoal"] : trimmed, WithCount(taskCount == 1 ? "summaryTaskSingular" : "summaryTasks", taskCount));
        }

        /// <summary>"Block super distracting apps/websites · AI Enabled".</summary>
        public string BlockingSummary(string blockListName, bool aiEnabled) =>
            Join((blockListName ?? "").Trim(), aiEnabled ? this["badgeAiEnabled"] : this["badgeAiDisabled"]);

        /// <summary>"Start a 25 min session" / "Start a 1 hr 30 min session" / "Start an untimed session".</summary>
        public string StartButtonTitle(FocusStartDuration duration)
        {
            if (duration.IsUntimed) return this["buttonStartUntimed"];
            string length;
            if (duration.HoursComponent > 0 && duration.MinutesComponent > 0)
                length = duration.HoursComponent + " " + this["unitHour"] + " " + duration.MinutesComponent + " " + this["unitMinute"];
            else if (duration.HoursComponent > 0) length = duration.HoursComponent + " " + this["unitHour"];
            else length = duration.MinutesComponent + " " + this["unitMinute"];
            return this["buttonStartTimed"].Replace(DurationToken, length);
        }

        /// <summary>Two-digit hours and minutes for the big readout; null when untimed (the screen draws ∞).</summary>
        public static Tuple<string, string> ClockComponents(FocusStartDuration duration) =>
            duration.IsUntimed ? null : Tuple.Create(duration.HoursComponent.ToString("00", CultureInfo.InvariantCulture), duration.MinutesComponent.ToString("00", CultureInfo.InvariantCulture));

        public const string Separator = "·";

        private static string Join(string left, string right)
        {
            if (string.IsNullOrEmpty(right)) return left;
            if (string.IsNullOrEmpty(left)) return right;
            return left + " " + Separator + " " + right;
        }
    }

    /// <summary>
    /// What the goal card offers and what a session starts with (the Mac's <c>FocusStartWorkSelection</c>).
    /// A session's task queue is only what was picked on the start screen — never the block list's
    /// <c>lastTaskUsed</c>, which is the previous session's queue (Mac report 2026-09-15_16-46-10).
    /// </summary>
    public static class FocusStartWorkSelection
    {
        public const string EmptyQueue = "[]";
        public const int DefaultTaskSeconds = 600;
        public const int MaxProjectChips = 12;

        /// <summary>Micro-breaks a session would take: one per full <paramref name="frequencyMinutes"/> before the end.</summary>
        public static int BreakCount(FocusStartDuration duration, bool microBreaksOn, int frequencyMinutes)
        {
            if (!microBreaksOn || frequencyMinutes <= 0) return 0;
            return Math.Max(0, (duration.EffectiveMinutes - 1) / frequencyMinutes);
        }

        /// <summary>The queue the player and the server read: to-do rows with the player's extra fields.</summary>
        public static string SessionQueue(IList<ToDoTask> tasks, IList<Project> projects)
        {
            if (tasks == null || tasks.Count == 0) return EmptyQueue;
            var rows = new JArray();
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                var project = projects?.FirstOrDefault(p => p.Id == task.ProjectId);
                rows.Add(new JObject
                {
                    ["id"] = task.Id,
                    ["title"] = task.Title ?? "",
                    ["status"] = task.Status,
                    ["duration"] = task.DurationSeconds > 0 ? task.DurationSeconds : DefaultTaskSeconds,
                    ["details"] = task.Details ?? "",
                    ["isPlaying"] = i == 0,
                    ["icon"] = task.Icon,
                    ["project_id"] = task.ProjectId,
                    ["project_name"] = project?.Name,
                    ["top_score"] = task.TopScore,
                    ["due_date"] = task.DueDate,
                });
            }
            return rows.ToString(Formatting.None);
        }

        /// <summary>The first project named by an unfinished Day Plan task that matches exactly one project.</summary>
        public static Project TopDayPlanProject(IList<Project> projects, IEnumerable<string> unfinishedDayPlanProjectNames)
        {
            foreach (string name in unfinishedDayPlanProjectNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var matches = projects.Where(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) return matches[0];
            }
            return null;
        }

        /// <summary>Projects on today's Day Plan first (in plan order), then the rest A–Z.</summary>
        public static IList<Project> Projects(IList<Project> projects, IEnumerable<string> dayPlanProjectNames)
        {
            var result = new List<Project>();
            foreach (string name in dayPlanProjectNames ?? Enumerable.Empty<string>())
            {
                var project = TopDayPlanProject(projects, new[] { name });
                if (project != null && result.All(p => p.Id != project.Id)) result.Add(project);
            }
            var seen = new HashSet<string>(result.Select(p => p.Id));
            return result.Concat(projects.Where(p => !seen.Contains(p.Id)).OrderBy(p => p.Name ?? "", StringComparer.CurrentCultureIgnoreCase)).ToList();
        }

        /// <summary>
        /// The title a typed task search would create, or null when there is nothing to offer: an empty box,
        /// or a query that already names a task in the list (same title, ignoring case and outer spaces) —
        /// picking that task is the answer then, not a duplicate.
        /// </summary>
        public static string NewTaskTitle(string query, IEnumerable<ToDoTask> candidates)
        {
            string title = (query ?? "").Trim();
            if (title.Length == 0) return null;
            bool exists = (candidates ?? Enumerable.Empty<ToDoTask>())
                .Any(t => string.Equals((t.Title ?? "").Trim(), title, StringComparison.CurrentCultureIgnoreCase));
            return exists ? null : title;
        }

        /// <summary>
        /// The task Enter picks for a typed search: the one whose title is exactly what was typed (ignoring
        /// case and outer spaces), or null when none is — Enter then adds the text as a new task.
        /// </summary>
        public static ToDoTask ExactMatch(string query, IEnumerable<ToDoTask> candidates)
        {
            string title = (query ?? "").Trim();
            if (title.Length == 0) return null;
            return (candidates ?? Enumerable.Empty<ToDoTask>())
                .FirstOrDefault(t => string.Equals((t.Title ?? "").Trim(), title, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// A to-do created from the start screen's task search: due today (the Mac's "a task added to the
        /// current focus session is due today"), in the session's project when the goal is one.
        /// </summary>
        public static ToDoTask NewSessionTask(string title, DateTime now, Project project) =>
            ToDoDraft.NewTask(title, now, project, now.Date);

        /// <summary>Unfinished tasks, the chosen project's first, each group by TOP score (highest first).</summary>
        public static IList<ToDoTask> Tasks(IEnumerable<ToDoTask> tasks, Project project)
        {
            return tasks.Where(t => !t.IsCompleted)
                .Select((t, index) => new { t, index })
                .OrderBy(x => project != null && x.t.ProjectId == project.Id ? 0 : 1)
                .ThenByDescending(x => x.t.TopScore ?? double.MinValue)
                .ThenBy(x => x.index)
                .Select(x => x.t)
                .ToList();
        }
    }
}
