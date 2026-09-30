using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Focus
{
    /// <summary>Backend to-do status, as the session queue carries it.</summary>
    public static class FocusSessionTaskStatus
    {
        public const string NotStarted = "NOT_STARTED";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";

        public static bool IsCompleted(string status) => string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One task in the running session's queue (the Mac's <c>FocusSessionTask</c>). The queue is not a new
    /// store: it is the same JSON array the start screens write to <c>sRawTasks</c> and the focus mode keeps as
    /// <c>lastTaskUsed</c>. <see cref="Raw"/> keeps every field of the row, so a rewrite never drops one this
    /// screen does not know about.
    /// </summary>
    public sealed class FocusSessionTask
    {
        /// <summary>The To Do Player's default for a task with no duration, which the start screens use too.</summary>
        public const int DefaultDurationSeconds = 600;

        public FocusSessionTask(string id, string title, string details = "", int durationSeconds = DefaultDurationSeconds,
            string status = FocusSessionTaskStatus.NotStarted, bool isActive = false, double? topScore = null,
            string icon = null, string projectName = null, string projectId = null, JObject raw = null)
        {
            Id = id ?? "";
            Title = title ?? "";
            Details = details ?? "";
            DurationSeconds = durationSeconds;
            Status = string.IsNullOrEmpty(status) ? FocusSessionTaskStatus.NotStarted : status.ToUpperInvariant();
            IsActive = isActive;
            TopScore = topScore;
            Icon = icon;
            ProjectName = projectName;
            ProjectId = projectId;
            Raw = raw ?? new JObject();
        }

        public string Id { get; }
        public string Title { get; }
        public string Details { get; }
        public int DurationSeconds { get; }
        public string Status { get; }
        /// <summary>The task the session is working on — the To Do Player's <c>isPlaying</c>.</summary>
        public bool IsActive { get; }
        public double? TopScore { get; }
        public string Icon { get; }
        public string ProjectName { get; }
        public string ProjectId { get; }
        public JObject Raw { get; }

        public bool IsCompleted => FocusSessionTaskStatus.IsCompleted(Status);

        public int DurationMinutes => Math.Max(1, (int)Math.Round(DurationSeconds / 60.0, MidpointRounding.AwayFromZero));

        public FocusSessionTask With(string title = null, string details = null, int? durationSeconds = null, string status = null,
            bool? isActive = null, string projectName = null, string projectId = null, bool clearProject = false) =>
            new FocusSessionTask(Id, title ?? Title, details ?? Details, durationSeconds ?? DurationSeconds, status ?? Status,
                isActive ?? IsActive, TopScore, Icon, clearProject ? null : projectName ?? ProjectName,
                clearProject ? null : projectId ?? ProjectId, Raw);

        public override bool Equals(object obj) =>
            obj is FocusSessionTask other && other.Id == Id && other.Title == Title && other.Details == Details
            && other.DurationSeconds == DurationSeconds && other.Status == Status && other.IsActive == IsActive
            && other.TopScore == TopScore && other.Icon == Icon && other.ProjectName == ProjectName && other.ProjectId == ProjectId;

        public override int GetHashCode() => (Id ?? "").GetHashCode() ^ (Title ?? "").GetHashCode();
    }

    /// <summary>
    /// Reads and rewrites the session queue (the Mac's <c>FocusSessionTaskQueue</c>). Every change returns a new
    /// list, so the caller can compare, persist and redraw in one place.
    /// </summary>
    public static class FocusSessionQueue
    {
        private static class Key
        {
            public const string Id = "id";
            public const string Title = "title";
            public const string Details = "details";
            public const string Duration = "duration";
            public const string Status = "status";
            public const string IsPlaying = "isPlaying";
            public const string TopScore = "top_score";
            public const string Icon = "icon";
            public const string ProjectName = "project_name";
            public const string ProjectId = "project_id";
        }

        /// <summary>An empty, "[]" or unreadable string is an empty queue — the screen must always draw.</summary>
        public static IList<FocusSessionTask> Decode(string json)
        {
            string trimmed = (json ?? "").Trim();
            if (trimmed.Length == 0 || trimmed == "[]") return new List<FocusSessionTask>();
            JArray rows;
            try { rows = JArray.Parse(trimmed); }
            catch (JsonException) { return new List<FocusSessionTask>(); }
            var tasks = new List<FocusSessionTask>();
            foreach (var row in rows.OfType<JObject>())
            {
                string id = Text(row[Key.Id]);
                string title = Text(row[Key.Title]);
                // A row with neither an id nor a title is nothing the user can act on.
                if (id.Length == 0 && title.Length == 0) continue;
                tasks.Add(new FocusSessionTask(id, title, Text(row[Key.Details]), DurationSeconds(row[Key.Duration]),
                    Text(row[Key.Status]), Bool(row[Key.IsPlaying]), Number(row[Key.TopScore]), NullIfEmpty(Text(row[Key.Icon])),
                    NullIfEmpty(Text(row[Key.ProjectName])), NullIfEmpty(Text(row[Key.ProjectId])), (JObject)row.DeepClone()));
            }
            return tasks;
        }

        /// <summary>Back into the shape <c>sRawTasks</c> holds, keeping each row's other fields.</summary>
        public static string Encode(IEnumerable<FocusSessionTask> tasks)
        {
            var rows = new JArray();
            foreach (var task in tasks ?? Enumerable.Empty<FocusSessionTask>())
            {
                var row = (JObject)task.Raw.DeepClone();
                row[Key.Id] = task.Id;
                row[Key.Title] = task.Title;
                row[Key.Details] = task.Details;
                row[Key.Duration] = task.DurationSeconds;
                row[Key.Status] = task.Status;
                row[Key.IsPlaying] = task.IsActive;
                row[Key.TopScore] = task.TopScore.HasValue ? new JValue(task.TopScore.Value) : JValue.CreateNull();
                row[Key.Icon] = task.Icon;
                row[Key.ProjectName] = task.ProjectName;
                row[Key.ProjectId] = task.ProjectId;
                rows.Add(row);
            }
            return rows.ToString(Formatting.None);
        }

        /// <summary>The flagged task, else the first one still to do.</summary>
        public static FocusSessionTask Active(IEnumerable<FocusSessionTask> tasks)
        {
            var list = (tasks ?? Enumerable.Empty<FocusSessionTask>()).ToList();
            return list.FirstOrDefault(t => t.IsActive && !t.IsCompleted) ?? list.FirstOrDefault(t => !t.IsCompleted);
        }

        /// <summary>Everything below the active card, completed rows included (they are struck through, not hidden).</summary>
        public static IList<FocusSessionTask> Queued(IEnumerable<FocusSessionTask> tasks)
        {
            var list = (tasks ?? Enumerable.Empty<FocusSessionTask>()).ToList();
            var active = Active(list);
            return active == null ? list : list.Where(t => !ReferenceEquals(t, active)).ToList();
        }

        /// <summary>What the queue still owes — the heading counts these, not the struck-through rows.</summary>
        public static IList<FocusSessionTask> Outstanding(IEnumerable<FocusSessionTask> tasks) =>
            (tasks ?? Enumerable.Empty<FocusSessionTask>()).Where(t => !t.IsCompleted).ToList();

        /// <summary>Makes one task the active one; every other task loses the flag.</summary>
        public static IList<FocusSessionTask> Focusing(IEnumerable<FocusSessionTask> tasks, string id) =>
            tasks.Select(t => t.With(isActive: t.Id == id)).ToList();

        /// <summary>Completes a task and hands the session to the next one still to do.</summary>
        public static IList<FocusSessionTask> Completing(IEnumerable<FocusSessionTask> tasks, string id)
        {
            var updated = tasks.Select(t => t.Id == id ? t.With(status: FocusSessionTaskStatus.Completed, isActive: false) : t).ToList();
            var next = updated.FirstOrDefault(t => !t.IsCompleted);
            return next == null ? updated : Focusing(updated, next.Id);
        }

        /// <summary>Drops a task, promoting the next one when the removed task was active.</summary>
        public static IList<FocusSessionTask> Removing(IEnumerable<FocusSessionTask> tasks, string id)
        {
            var list = tasks.ToList();
            var active = Active(list);
            bool wasActive = active != null && active.Id == id;
            var remaining = list.Where(t => t.Id != id).ToList();
            var next = remaining.FirstOrDefault(t => !t.IsCompleted);
            return wasActive && next != null ? Focusing(remaining, next.Id) : remaining;
        }

        /// <summary>Adds tasks the queue does not hold yet. Adding to a queue with nothing to do makes the first new one active.</summary>
        public static IList<FocusSessionTask> Appending(IEnumerable<FocusSessionTask> tasks, IEnumerable<FocusSessionTask> additions)
        {
            var list = tasks.ToList();
            var ids = new HashSet<string>(list.Select(t => t.Id));
            var fresh = additions.Where(t => ids.Add(t.Id)).ToList();
            if (fresh.Count == 0) return list;
            var combined = list.Concat(fresh).ToList();
            if (Active(list) == null)
            {
                var first = combined.FirstOrDefault(t => !t.IsCompleted);
                if (first != null) combined = Focusing(combined, first.Id).ToList();
            }
            return combined;
        }

        /// <summary>
        /// The block popup's "add and play": the task becomes the active one, added if it is new. With
        /// <paramref name="completeCurrent"/> the task that was active is completed first.
        /// </summary>
        public static IList<FocusSessionTask> Playing(IEnumerable<FocusSessionTask> tasks, FocusSessionTask task, bool completeCurrent)
        {
            var list = tasks.ToList();
            var active = Active(list);
            if (completeCurrent && active != null && active.Id != task.Id)
                list = list.Select(t => t.Id == active.Id ? t.With(status: FocusSessionTaskStatus.Completed, isActive: false) : t).ToList();
            if (list.All(t => t.Id != task.Id)) list.Add(task);
            return Focusing(list, task.Id);
        }

        /// <summary>Moves a task one place up or down — the menu's stand-in for the design's drag handle.</summary>
        public static IList<FocusSessionTask> Moving(IEnumerable<FocusSessionTask> tasks, string id, int offset)
        {
            var list = tasks.ToList();
            int index = list.FindIndex(t => t.Id == id);
            int destination = index + offset;
            if (offset == 0 || index < 0 || destination < 0 || destination >= list.Count) return list;
            var moved = list[index];
            list[index] = list[destination];
            list[destination] = moved;
            return list;
        }

        public static bool SameQueue(IList<FocusSessionTask> a, IList<FocusSessionTask> b) =>
            a != null && b != null && a.Count == b.Count && a.Zip(b, (x, y) => x.Equals(y)).All(same => same);

        /// <summary>The player and the to-do API have written durations as numbers and as strings.</summary>
        public static int DurationSeconds(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return FocusSessionTask.DefaultDurationSeconds;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                int seconds = (int)value.Value<double>();
                return seconds > 0 ? seconds : FocusSessionTask.DefaultDurationSeconds;
            }
            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed : FocusSessionTask.DefaultDurationSeconds;
        }

        private static string Text(JToken token) => token == null || token.Type == JTokenType.Null ? "" : token.ToString();

        private static bool Bool(JToken token) => token != null && token.Type == JTokenType.Boolean && token.Value<bool>();

        private static double? Number(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return token.Value<double>();
            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : (double?)null;
        }

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The rail's plain-text brain dump, as the web editor stores a note: each line a paragraph.</summary>
    public static class FocusSessionBrainDumpText
    {
        public static string ToHtml(string plainText)
        {
            string text = (plainText ?? "").Replace("\r\n", "\n").TrimEnd('\n');
            if (text.Trim().Length == 0) return "";
            return string.Concat(text.Split('\n').Select(line => "<p>" + System.Net.WebUtility.HtmlEncode(line) + "</p>"));
        }
    }

    /// <summary>Which of the in-session states the screen shows (the Mac's <c>FocusSessionPhase</c>).</summary>
    public enum FocusSessionPhase
    {
        Running,
        Paused,
        /// <summary>Nothing is running — only on screen for the moment a session is being torn down.</summary>
        Idle,
    }

    /// <summary>
    /// What the in-session screen offers, from the app's session state — kept apart from WPF so every rule is
    /// testable. <c>isSuperStrict</c> is the Windows equivalent of the Mac's super-strict session: a Super Focus
    /// run (its stop is password-gated and it cannot be paused) or a super strict ("iron focus") blocking
    /// schedule. <c>isClassSession</c> is a teacher-run class session, where the teacher owns the checklist.
    /// </summary>
    public sealed class FocusSessionRules
    {
        public FocusSessionRules(bool isRunning, bool isPaused, bool isSuperFocus, bool isSuperStrict, bool isClassSession)
        {
            Phase = !isRunning ? FocusSessionPhase.Idle : isPaused ? FocusSessionPhase.Paused : FocusSessionPhase.Running;
            IsSuperFocus = isSuperFocus;
            IsSuperStrict = isSuperStrict;
            IsClassSession = isClassSession;
        }

        public FocusSessionPhase Phase { get; }
        public bool IsSuperFocus { get; }
        public bool IsSuperStrict { get; }
        public bool IsClassSession { get; }

        /// <summary>A Super Focus run cannot be paused (the tray refuses), so it offers Stop instead, as the Mac's super focus does.</summary>
        public bool CanPause => Phase == FocusSessionPhase.Running && !IsSuperFocus && !IsClassSession;

        public bool ShowsDurationControls => Phase == FocusSessionPhase.Running && !IsClassSession;

        /// <summary>Taking time off is what super strict exists to prevent; adding time never is.</summary>
        public bool CanReduce => ShowsDurationControls && !IsSuperStrict;

        public bool IsGoalLocked => IsSuperStrict || IsClassSession;

        /// <summary>During a class session the teacher's checklist must not be reshuffled from here.</summary>
        public bool IsTaskEditingLocked => IsClassSession;

        public string Headline(FocusSessionStrings strings)
        {
            if (Phase == FocusSessionPhase.Paused) return strings["titleSessionPaused"];
            if (IsSuperFocus) return strings["titleSuperFocusRunning"];
            return strings["titleOngoingSession"];
        }
    }

    /// <summary>
    /// The session length the progress bar measures against, and the work the session is for. Pure so the rules
    /// that went wrong on the Mac (a stale total making the bar stand still) are pinned by tests.
    /// </summary>
    public static class FocusSessionProgress
    {
        /// <summary>Never less than what is left, or elapsed would clamp to zero and the bar never move.</summary>
        public static int TotalSeconds(int plannedMinutes, int adjustedSeconds, int remainingSeconds) =>
            Math.Max(Math.Max(0, plannedMinutes * FocusSessionClock.SecondsPerMinute + adjustedSeconds), Math.Max(0, remainingSeconds));

        /// <summary>
        /// A new end for a running session moved by <paramref name="minutes"/>: never earlier than a minute from
        /// now, and null when the move would change nothing. Taking time off never adds any: a session with under
        /// a minute left stays as it is (-5 with 40 seconds to go used to put it back to a full minute).
        /// </summary>
        public static DateTime? AdjustedEnd(DateTime end, DateTime now, int minutes)
        {
            if (minutes == 0) return null;
            DateTime moved = (end < now ? now : end).AddMinutes(minutes);
            DateTime floor = now.AddMinutes(1);
            if (moved < floor) moved = floor;
            if (minutes < 0 && moved >= end) return null;
            return Math.Abs((moved - end).TotalSeconds) < 1 ? (DateTime?)null : moved;
        }

        /// <summary>
        /// The session screen's -5 / +5 (the Mac's <c>extendFocusModeFor</c> / <c>reduceFocusModeFor</c>): the new end, or
        /// null when nothing moves. Adding time to a session that stands between the user and a routine (a postponed
        /// routine, or the buddy's postpone session) is limited by the cutoff exactly as the Mac limits it
        /// (<see cref="Stats.FocusExtensionCutoffLimit"/>): +5 at 16:58 with a 17:00 cutoff grants 2 minutes, and none
        /// once the cutoff has passed when the user made it strict.
        /// </summary>
        public static DateTime? AdjustedEndWithinCutoff(DateTime end, DateTime now, int minutes, DateTime? cutoff,
            bool isAfterCutoff, bool isCutoffStrict, bool protectsRoutine)
        {
            if (minutes > 0)
            {
                minutes = Stats.FocusExtensionCutoffLimit.GrantedMinutes(minutes, now, end, cutoff, isAfterCutoff, isCutoffStrict, protectsRoutine);
                if (minutes <= 0) return null;
            }
            return AdjustedEnd(end, now, minutes);
        }

        /// <summary>The big title: the active task's project, else the goal, else the block list's name.</summary>
        public static string WorkTitle(FocusSessionTask active, string goal, string blockListName)
        {
            string project = (active?.ProjectName ?? "").Trim();
            if (project.Length > 0) return project;
            string trimmed = (goal ?? "").Trim();
            return trimmed.Length > 0 ? trimmed : blockListName ?? "";
        }
    }

    /// <summary>
    /// The screen's copy: the config's <c>focusStateVcInfo</c> block (shared with the Mac, whose keys these are),
    /// falling back to the Mac's English. Placeholders are <c>&lt;COUNT&gt;</c>, <c>&lt;MINUTES&gt;</c>,
    /// <c>&lt;SCORE&gt;</c>, <c>&lt;TIME&gt;</c>, <c>&lt;ELAPSED&gt;</c>, <c>&lt;TOTAL&gt;</c>, <c>&lt;NAME&gt;</c>.
    /// </summary>
    public sealed class FocusSessionStrings
    {
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "titleOngoingSession", "Ongoing Focus Session" }, { "titleSessionPaused", "Focus Session Paused" },
            { "titleSuperFocusRunning", "Super Focus session running" },
            { "unitHourText", "hr" }, { "unitMinuteText", "min" }, { "unitSecondText", "sec" }, { "labelMinutesShort", "MIN" },
            { "toolTipExtendDuration", "Add time to this session" }, { "toolTipReduceDuration", "Take time off this session" },
            { "pauseText", "Pause" }, { "resumeText", "Resume" }, { "endFocusSessionText", "End Session" },
            { "noticeDurationLockedInSuperStrict", "The duration cannot be reduced in super strict mode" },
            { "titleUntitledFocusMode", "Focus session" }, { "titleIntention", "Focus Goal:" },
            { "placeholderFocusGoal", "What do you want to get done in this session?" },
            { "noticeGoalLocked", "Focus goal cannot be changed in super strict mode" },
            { "labelActiveTask", "ACTIVE TASK" }, { "labelTaskQueue", "Task Queue (<COUNT>):" },
            { "titleNoActiveTask", "No task chosen yet" }, { "bodyNoActiveTask", "Add a task so this session has a job to do." },
            { "buttonAddTask", "Add a task" }, { "labelEmptyQueue", "Nothing else queued." },
            { "buttonMarkComplete", "Mark as complete" }, { "labelTimeOnTask", "Working for <TIME>" },
            { "labelSessionProgress", "<ELAPSED> of <TOTAL>" }, { "toolTipAddTasks", "Add tasks to this session" },
            { "labelDurationBadge", "DURATION: <MINUTES> MINS" }, { "labelTopScoreBadge", "TOP: <SCORE>" },
            { "menuMarkComplete", "Mark as Complete" }, { "menuFocusOnThisInstead", "Focus on this instead" },
            { "menuMoveTaskUp", "Move up" }, { "menuMoveTaskDown", "Move down" }, { "menuRemoveFromQueue", "Remove from queue" },
            { "menuEditTask", "Edit task" },
            { "titleSelectTasks", "Select Tasks" }, { "placeholderSearchTask", "Search for a task" },
            { "buttonCreateNewTask", "Create new task" }, { "buttonQueueTasks", "Queue <COUNT> tasks" },
            { "buttonQueueSingleTask", "Queue 1 task" }, { "labelNoMatchingTasks", "No matching tasks" },
            { "labelNoTasksToPick", "Nothing planned for today yet" }, { "labelPickerFromDayPlan", "From today's Day Plan" },
            { "labelPickerFromToDoList", "From your to-do list" }, { "labelPickerFromSearch", "Searching all your to-dos" },
            { "fetchingTasks", "Fetching tasks..." }, { "closeText", "Close" }, { "buttonCancelText", "Cancel" },
            { "buttonCreateTypedTask", "Create “<NAME>”" }, { "errorCouldNotSaveTask", "Could not save the task" },
            { "errorCouldNotLoadTask", "Could not load this task for editing. Reconnect and try again." },
            { "labelFocusMusic", "FOCUS MUSIC" }, { "labelNotes", "NOTES" }, { "labelBrainDump", "BRAINDUMP" },
            { "placeholderBrainDump", "Make a quick braindump" }, { "buttonSeeOtherNotes", "See other notes" },
            { "buttonOpenFocusMusic", "Open Focus Music" },
            { "blockListLabel", "Block list:" },
            { "titleEndSessionConfirm", "End session early?" },
            { "bodyEndSessionConfirm", "A few more minutes could be the difference. Stick with it?" },
            { "labelFocusAlignmentScore", "Focus alignment score: <SCORE>%" },
        };

        private readonly Dictionary<string, string> _values;

        /// <param name="info">The config's <c>focusStateVcInfo</c>.</param>
        /// <param name="progress">The config's <c>focusProgressInfo</c> — the shared Pause / Resume / End Session words.</param>
        public FocusSessionStrings(JObject info = null, JObject progress = null)
        {
            _values = new Dictionary<string, string>(Defaults);
            Overlay(progress);
            Overlay(info);
        }

        private void Overlay(JObject config)
        {
            if (config == null) return;
            foreach (var property in config.Properties())
                if (property.Value.Type == JTokenType.String && ((string)property.Value).Length > 0) _values[property.Name] = (string)property.Value;
        }

        public string this[string key] => _values.TryGetValue(key, out var value) ? value : "";

        public FocusSessionDurationUnits Units => new FocusSessionDurationUnits(this["unitHourText"], this["unitMinuteText"], this["unitSecondText"]);

        public string QueueHeading(int count) => this["labelTaskQueue"].Replace("<COUNT>", count.ToString(CultureInfo.CurrentCulture));

        public string QueueButton(int count) => count == 1 ? this["buttonQueueSingleTask"] : this["buttonQueueTasks"].Replace("<COUNT>", count.ToString(CultureInfo.CurrentCulture));

        public string TimeOnTask(int seconds) => this["labelTimeOnTask"].Replace("<TIME>", FocusSessionClock.CompactElapsed(seconds, Units));

        /// <summary>"5 min of 30 min" — the bar in words.</summary>
        public string Progress(int elapsedSeconds, int totalSeconds) =>
            this["labelSessionProgress"].Replace("<ELAPSED>", FocusSessionClock.CompactElapsed(elapsedSeconds, Units))
                .Replace("<TOTAL>", FocusSessionClock.CompactElapsed(totalSeconds, Units));

        public string DurationBadge(int minutes) => this["labelDurationBadge"].Replace("<MINUTES>", minutes.ToString(CultureInfo.CurrentCulture));

        public string TopScoreBadge(double score) => this["labelTopScoreBadge"].Replace("<SCORE>", score.ToString("0.0", CultureInfo.CurrentCulture));

        public string AlignmentScore(double score) => this["labelFocusAlignmentScore"].Replace("<SCORE>", Math.Round(score).ToString(CultureInfo.CurrentCulture));
    }
}
