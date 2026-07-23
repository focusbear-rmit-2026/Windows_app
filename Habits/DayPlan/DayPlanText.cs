using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// All user-facing Day Plan strings, read from the <c>scheduleViewInfo</c> config block with
    /// English fallbacks — the C# equivalent of the macOS <c>ScheduleText</c> helper. Null-safe: if
    /// the (remote or bundled) config doesn't carry the key yet, the fallback is used instead of
    /// throwing. The macOS app already ships these keys in its config; the same block is added to the
    /// Windows bundled config.json so offline/local builds have them too.
    /// </summary>
    public static class DayPlanText
    {
        private static JObject Info
        {
            get { return AppSettings.GetInstance.ConfigFileStrings?["scheduleViewInfo"] as JObject; }
        }

        /// <summary>
        /// Format a config template that may use the macOS/iOS placeholders <c>%@</c> or <c>%d</c>
        /// (positional <c>%1$@</c> too) instead of .NET's <c>{0}</c>. The shared <c>scheduleViewInfo</c>
        /// block is authored for the Swift app, so its strings carry <c>%@</c>; translate before
        /// <see cref="string.Format(string, object)"/> so we don't render a literal "%@".
        /// </summary>
        public static string Fmt(string template, object arg)
        {
            if (string.IsNullOrEmpty(template)) return template;
            string t = template
                .Replace("%1$@", "{0}").Replace("%1$d", "{0}")
                .Replace("%@", "{0}").Replace("%d", "{0}");
            try { return string.Format(t, arg); }
            catch { return template; }
        }

        /// <summary>Read a key from scheduleViewInfo, falling back to <paramref name="fallback"/>.</summary>
        public static string T(string key, string fallback)
        {
            try
            {
                JToken v = Info?[key];
                string s = v?.ToString();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }

        // Routine group / headers
        public static string MorningRoutineHabits => T("morningRoutineHabits", "Morning routine habits");
        public static string EveningRoutineHabits => T("eveningRoutineHabits", "Evening routine habits");
        public static string HabitsSuffix => T("habitsSuffix", "habits");
        public static string HabitsDone => T("habitsDone", "Habits done");

        // Action bar (used in M5)
        public static string Play => T("play", "Play");
        public static string Done => T("done", "Done");
        public static string Skip => T("skip", "Skip");
        public static string SkipRoutineForToday => T("skipRoutineForToday", "Skip routine for today");
        public static string SkippedTag => T("habitSkippedTag", "skipped");
        public static string PastHabitComplete => T("pastHabitComplete", "Complete");
        public static string PastHabitCompleteHint => T("pastHabitCompleteHint", "I actually did it but forgot to log it 😅");

        // Empty / misc states
        public static string NoHabitsLeft => T("noHabitsLeft", "No habits left for now — enjoy your free time! 🎉");
        public static string NothingScheduled => T("nothingScheduled", "Nothing scheduled for this day.");
        public static string Today => T("today", "Today");
        public static string BackToToday => T("backToToday", "Back to today");
        public static string Meeting => T("meeting", "meeting");

        // Tasks panel (M7). Keys mirror the macOS ScheduleText.tasks* keys so the same config block
        // drives both apps.
        public static string TasksForToday => T("tasksForToday", "Tasks for today");
        public static string TasksForDay => T("tasksForDay", "Tasks for {0}");
        public static string TasksAddNew => T("tasksAddNew", "Add task");
        public static string TasksAddFromList => T("tasksAddFromList", "From to-do list");
        public static string TasksEmpty => T("tasksEmpty", "No tasks yet. Add one from scratch or from your to-do list.");
        public static string TasksNamePlaceholder => T("tasksNamePlaceholder", "Task name");
        public static string TasksDragHint => T("tasksDragHint", "Drag onto a block to schedule it");
        public static string TasksReturnToPanel => T("tasksReturnToPanel", "Move back to tasks");
        public static string TasksTimeBoxed => T("tasksTimeBoxed", "Time boxed");
        public static string TasksSettings => T("tasksSettings", "Task settings");
        public static string TasksSearchPlaceholder => T("tasksSearchPlaceholder", "Search tasks…");
        public static string TasksAddSelected => T("tasksAddSelected", "Add");
        public static string TasksLoading => T("tasksLoading", "Loading…");
        public static string TasksPickerAllAdded => T("tasksPickerAllAdded", "All your open to-dos are already on this day.");
        public static string TasksPickerNone => T("tasksPickerNone", "No open to-dos found. Add tasks in your to-do list first.");
        public static string TasksNoProjectsYet => T("tasksNoProjectsYet", "No projects yet — type one above.");
        public static string TasksAddToProject => T("tasksAddToProject", "Add a task to this project");
        public static string TasksMovesTo => T("tasksMovesTo", "Moves to {0}");
        public static string TasksMoveUnrecognised => T("tasksMoveUnrecognised", "Not a date I recognise");
        public static string TasksAddFromListTitle => T("tasksAddFromList", "From to-do list");
        public static string TasksShowCompleted => T("tasksShowCompleted", "Show completed");
        public static string TasksSelectedCount => T("tasksSelectedCount", "{0} selected");
        public static string TasksClearSelection => T("tasksClearSelection", "Clear");
        public static string TasksTotalToDo => T("tasksTotalToDo", "≈ {0} of tasks to do");
        public static string Cancel => T("cancel", "Cancel");
        public static string Save => T("eventSave", "Save");

        // Panel chrome (Phase 2)
        public static string TasksShowCompletedCount => T("tasksShowCompletedCount", "Show completed ({0})");
        public static string TasksNoProject => T("tasksNoProject", "No project");

        // Meetings (Phase 5 — calendar parity)
        public static string MeetingDismiss => T("meetingDismiss", "Hide this meeting for today");
        public static string MeetingRestore => T("meetingRestore", "Show this meeting again");
        public static string MeetingsShowDismissedCount => T("meetingsShowDismissedCount", "Show hidden meetings ({0})");
        public static string MeetingColour => T("meetingColour", "Meeting colour");
        public static string MeetingColourDefault => T("meetingColourDefault", "Default");

        // Detail modal (Phase 3)
        public static string TasksDetailsTitle => T("tasksDetailsTitle", "Task details");
        public static string TasksProjectLabel => T("tasksProjectLabel", "Project");
        public static string TasksProjectField => T("tasksProjectField", "Project (optional)");
        public static string TasksDurationLabel => T("tasksDurationLabel", "Duration (min)");
        public static string TasksImportanceLabel => T("tasksImportanceLabel", "Importance");
        public static string TasksImportanceUnset => T("tasksImportanceUnset", "Not set");
        public static string TasksDetailsLabel => T("tasksDetailsLabel", "Details");
        public static string TasksSubtasksLabel => T("tasksSubtasksLabel", "Sub-tasks");
        public static string TasksAddSubtask => T("tasksAddSubtask", "Add sub-task");
        public static string TasksSubtaskPlaceholder => T("tasksSubtaskPlaceholder", "Sub-task");
        public static string TasksDetails => T("tasksExpand", "Details");
        public static string TasksRemove => T("tasksRemove", "Remove");

        // Snooze / move-to-day (Phase 3)
        public static string TasksSnooze => T("tasksSnooze", "Snooze");
        public static string TasksSnoozeTitle => T("tasksSnoozeTitle", "Move task to…");
        public static string TasksSnoozePlaceholder => T("tasksSnoozePlaceholder", "e.g. 2d, 2w, tue, tom");
        public static string TasksSnoozeHelp => T("tasksSnoozeHelp", "2d = in 2 days · 2w = in 2 weeks · tue = next Tuesday · tom = tomorrow · 1 Jul = a date");
        public static string TasksSnoozeConfirm => T("tasksSnoozeConfirm", "Move");

        /// <summary>Importance band label for an outcome 1–10 (macOS outcomeBandLabel).</summary>
        public static string OutcomeBandLabel(int outcome)
        {
            if (outcome <= 3) return T("importanceLow", "Low");
            if (outcome <= 6) return T("importanceNormal", "Normal");
            if (outcome <= 8) return T("importanceHigh", "High");
            return T("importanceCritical", "Critical");
        }

        // Engine block labels (emoji prefixes match the macOS ScheduleText.engineLabels).
        public static string DeepWork => "🧠 " + T("deepWork", "Deep work");
        public static string DeepWorkNoRoom => "🧠 " + T("deepWorkNoRoom", "Deep work (no room for next habit)");
        public static string BreakSprint => "☕️ " + T("breakSprint", "Break");
        public static string Sleep => "😴 " + T("sleep", "Sleep");
        public static string EveningRoutine => "🌙 " + T("eveningRoutine", "Evening routine");
        public static string MorningRoutine => "🌅 " + T("morningRoutine", "Morning routine");

        /// <summary>Engine labels built from config, passed into <see cref="DayPlanScheduleBuilder"/>.</summary>
        public static ScheduleLabels EngineLabels()
        {
            return new ScheduleLabels
            {
                DeepWork = DeepWork,
                DeepWorkNoRoom = DeepWorkNoRoom,
                BreakSprint = BreakSprint,
                Sleep = Sleep,
                EveningRoutine = EveningRoutine,
                MorningRoutine = MorningRoutine,
                Meeting = Meeting
            };
        }
    }
}
