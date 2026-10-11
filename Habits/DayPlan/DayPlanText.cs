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
        // Cancel-routine confirmation (the ✕ on an active routine card). <BUTTON> → the uncancel link label.
        public static string CancelRoutineTitle => T("cancelRoutineTitle", "Cancel Routine?");
        public static string CancelRoutineMessage => T("cancelRoutineMessage", "If you change your mind later, you can click the \"<BUTTON>\" button to restore the routine.");
        public static string CancelRoutineOk => T("cancelRoutineOk", "OK");
        public static string CancelRoutineCancel => T("cancelRoutineCancel", "Cancel");
        public static string SkippedTag => T("habitSkippedTag", "skipped");
        public static string PastHabitComplete => T("pastHabitComplete", "Complete");
        public static string PastHabitCompleteHint => T("pastHabitCompleteHint", "I actually did it but forgot to log it 😅");

        // Shown on an active routine card when its habits don't fit around today's meetings (#1371):
        // the routine stays startable, but we tell the user there isn't enough free time. The short
        // heading replaces the time range in the (collapsed-by-default) card header; the longer detail
        // shows in the body once the card is expanded.
        public static string NotEnoughTimeHeading => T("dayPlanNotEnoughTimeHeading",
            "⏰ Not enough free time today");
        public static string NotEnoughTimeForRoutine => T("dayPlanNotEnoughTime",
            "Your meetings fill the rest of today, so there isn't enough free time for your whole routine. You can still start it below, or postpone it.");

        // Empty / misc states
        public static string NoHabitsLeft => T("noHabitsLeft", "No habits left for now — enjoy your free time! 🎉");
        public static string NothingScheduled => T("nothingScheduled", "Nothing scheduled for this day.");
        public static string Today => T("today", "Today");
        public static string BackToToday => T("backToToday", "Back to today");
        // The month calendar behind the date label (the Mac's date picker popover; Windows-only keys).
        public static string DatePickerOpen => T("datePickerOpen", "Choose a date");
        public static string DatePickerPreviousMonth => T("datePickerPreviousMonth", "Previous month");
        public static string DatePickerNextMonth => T("datePickerNextMonth", "Next month");
        public static string DatePickerLoading => T("datePickerLoading", "Loading calendar…");
        public static string DatePickerFree => T("datePickerFree", "<DURATION> free");
        public static string DatePickerOver => T("datePickerOver", "<DURATION> over");
        public static string Meeting => T("meeting", "meeting");

        // Habit actions + routine options (report 2026-09-25_09-05-05 — the Mac's Edit / Shorten / "Edit
        // routine timing"). Keys mirror macOS ScheduleText so the shared config block drives both apps;
        // strings that name the Mac in the shared copy get their own Windows key.
        public static string NotEnoughTimeThisDay => T("notEnoughTimeThisDay", "Not enough time on this day");
        public static string NotEnoughTimeThisDayHeading => "⏰ " + NotEnoughTimeThisDay;
        public static string EditHabit => T("editHabit", "Edit");
        public static string EditHabitTitle => T("editHabitTitle", "Edit habit");
        public static string HabitFieldIcon => T("habitFieldIconWindows", "Icon");
        public static string HabitFieldName => T("habitFieldName", "Habit name");
        public static string HabitFieldUseTimer => T("habitFieldUseTimer", "Use timer");
        public static string HabitFieldDurationLabel => T("habitFieldDurationLabel", "Duration");
        public static string HabitDurationMinutesUnit => T("habitDurationMinutesUnit", "min");
        public static string HabitFieldCompletion => T("habitFieldCompletion", "Habit finishes when…");
        public static string HabitFieldPriority => T("habitFieldPriority", "Priority");
        public static string HabitPriorityHigh => T("habitPriorityHigh", "Must do");
        public static string HabitPriorityStandard => T("habitPriorityStandard", "Optional");
        public static string HabitFieldDays => T("habitFieldDays", "Weekly schedule");
        public static string HabitDaysWeekdays => T("habitDaysWeekdays", "Weekdays");
        public static string HabitDaysWeekend => T("habitDaysWeekend", "Weekend");
        public static string HabitDaysAll => T("habitDaysAll", "Every day");
        public static string HabitMoreOptions => T("habitMoreOptionsWindows", "More options in Edit habits…");
        public static string HabitSave => T("habitSave", "Save");
        // The full habit editor (Mac HabitEditView tabs). Same keys as the Mac's ScheduleText.
        public static string HabitPickEmoji => T("habitPickEmojiWindows", "Emoji (Win + . opens the emoji panel)");
        public static string HabitDurationSecondsUnit => T("habitDurationSecondsUnit", "sec");
        public static string HabitTabTiming => T("habitTabTiming", "Timing");
        public static string HabitTabVideos => T("habitTabVideos", "Add Videos");
        public static string HabitTabAllowApps => T("habitTabAllowApps", "Allow Apps");
        public static string HabitTabLogData => T("habitTabLogData", "Log Data");
        public static string HabitTabSubHabits => T("habitTabSubHabits", "Sub habits");
        public static string HabitTabInstructions => T("habitTabInstructions", "Instructions");
        public static string HabitTabAdvanced => T("habitTabAdvanced", "Advanced");
        public static string HabitTimingNothing => T("habitTimingNothingWindows", "Morning habits run every day they're scheduled — pick those days here.");
        public static string HabitFieldCutoff => T("habitFieldCutoff", "Late cutoff — skip this habit after");
        public static string HabitFieldVideoUrls => T("habitFieldVideoUrls", "YouTube or Vimeo video links (one is played at random)");
        public static string HabitFieldAllowedApps => T("habitFieldAllowedApps", "Allowed apps");
        public static string HabitFieldAllowedAppsHint => T("habitFieldAllowedAppsHintWindows", "App names as they show in Task Manager, e.g. notepad or Kindle.");
        public static string HabitFieldAllowedUrls => T("habitFieldAllowedUrls", "Allowed websites");
        public static string HabitFieldRunMicroBreaks => T("habitFieldRunMicroBreaks", "Run micro-breaks during this habit");
        public static string HabitFieldUseFocusMode => T("habitFieldUseFocusMode", "Use a focus mode (block list) instead of specific apps");
        public static string HabitFieldFocusMode => T("habitFieldFocusMode", "Focus mode");
        public static string HabitFocusModeUnknown => T("habitFocusModeUnknownWindows", "(the focus mode already set)");
        public static string HabitFieldShowSavedSites => T("habitFieldShowSavedSites", "Show the guilt-free play popup");
        public static string HabitFieldLogQuantity => T("habitFieldLogQuantity", "Ask me to log data after this habit");
        public static string HabitFieldQuestion => T("habitFieldQuestion", "Question");
        public static string HabitFieldMin => T("habitFieldMin", "Min");
        public static string HabitFieldMax => T("habitFieldMax", "Max");
        public static string HabitAddQuestion => T("habitAddQuestion", "Add question");
        public static string HabitFieldLogType => T("habitFieldLogType", "Report as");
        public static string HabitLogTypeSum => T("habitLogTypeSum", "Total");
        public static string HabitLogTypeAverage => T("habitLogTypeAverage", "Average");
        public static string HabitFieldTakeNotes => T("habitFieldTakeNotes", "Take notes");
        public static string HabitNotesNone => T("habitNotesNone", "No notes");
        public static string HabitNotesDuring => T("habitNotesDuring", "During the habit");
        public static string HabitNotesEnd => T("habitNotesEnd", "At the end of the habit");
        public static string HabitFieldChecklist => T("habitFieldChecklist", "Steps to tick off");
        public static string HabitRandomChoicesNote(int count) => Fmt(T("habitRandomChoicesNote", "This habit has %d random sub-habits. They're kept as they are — edit them on the web."), count);
        public static string HabitFieldInstructions => T("habitFieldInstructions", "Text instructions");
        public static string HabitFieldImages => T("habitFieldImages", "Image links");
        public static string HabitFieldTags => T("habitFieldTags", "Tags");
        public static string HabitAddItem => T("habitAddItem", "Add");
        public static string HabitDelete => T("habitDelete", "Delete habit");
        public static string HabitDeleteConfirm(string name) => Fmt(T("habitDeleteConfirmWindows", "Remove “%@” from this routine? This is saved to your account straight away."), name);
        public static string HabitDeleteConfirmButton => T("habitDeleteConfirmButton", "Delete");
        public static string HabitInvalidVideo => T("habitInvalidVideo", "Video links must be YouTube or Vimeo URLs.");
        public static string HabitInvalidQuestions => T("habitInvalidQuestions", "Every question needs text, and min must be less than max.");
        public static string HabitInvalidTags => T("habitInvalidTags", "Tags can only contain letters, spaces, underscores, brackets, commas, & and -.");
        public static string HabitCancel => T("habitCancel", "Cancel");
        public static string HabitInvalidName => T("habitInvalidName", "Give the habit a name.");
        public static string HabitInvalidDurationRange => T("habitInvalidDurationRange", "Duration must be between 1 second and 999 minutes.");
        public static string HabitInvalidCompletion => T("habitInvalidCompletion", "Say what has to happen for the habit to count as done.");
        public static string HabitInvalidDays => T("habitInvalidDays", "Pick at least one day.");
        public static string HabitEditUnavailable => T("habitEditUnavailable", "This habit couldn't be found in your settings. Edit it from Settings › Edit habits instead.");
        public static string HabitSaveFailed => T("habitSaveFailedWindows", "Couldn't save to your account. Check your connection and try again.");
        public static string HabitShorten => T("habitShorten", "Shorten");
        public static string HabitShortened => T("habitShortened", "Shortened");
        public static string HabitShortenTitle => T("habitShortenTitle", "Shorten just for this day");
        public static string HabitShortenMinutes(int minutes) => Fmt(T("habitShortenMinutes", "%d min"), minutes);
        public static string HabitShortenFullLength(string length) => Fmt(T("habitShortenFullLength", "Full length: %@"), length);
        public static string HabitShortenFullLengthAction => T("habitShortenFullLengthAction", "Full length");
        public static string HabitShortenHint(string day) => Fmt(T("habitShortenHintWindows", "Applies on %@ only, and only on this computer. The habit keeps its real length everywhere else."), day);
        public static string HabitShortenNoOptions => T("habitShortenNoOptions", "This habit is already as short as it goes.");
        public static string HabitShortenTypePlaceholder => T("habitShortenTypePlaceholder", "e.g. 25min, 1h30");
        public static string HabitShortenUseTyped => T("habitShortenUseTyped", "Use this length");
        public static string HabitShortenTypeInvalid => T("habitShortenTypeInvalid", "Type a length like 25min, 1h30 or 90.");
        public static string HabitShortenTypeNotShorter(string length) => Fmt(T("habitShortenTypeNotShorter", "That isn't shorter than %@. Pick a smaller length, or use Full length to clear the trim."), length);
        public static string HabitShortenedTag => T("habitShortenedTag", "(shortened)");
        public static string RoutineMoreActions => T("routineMoreActions", "Routine options");
        public static string EditRoutineTiming => T("editRoutineTiming", "Edit routine timing");
        public static string RoutineTimingQuestion(string routine) => Fmt(T("routineTimingQuestion", "When do you want %@ to start?"), routine);
        public static string RoutineTimingHint => T("routineTimingHint", "This moves the routine on every day it's scheduled.");
        public static string RoutineCutoffLabel => T("routineCutoffLabel", "Evening cutoff time");
        public static string RoutineCutoffHint => T("routineCutoffHint", "Optional evening habits stop at this time; must-do habits keep their own late cutoffs.");
        public static string RoutineCutoffInvalid => T("routineCutoffInvalid", "Choose a cutoff between the evening routine start and the next morning routine start.");
        public static string RoutineTimeInvalid => T("routineTimeInvalidWindows", "Type a time like 7:30, 07:30 or 7:30pm.");
        public static string RoutineNameMorning => T("routineNameMorning", "your morning routine");
        public static string RoutineNameEvening => T("routineNameEvening", "your evening routine");

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
        // Short labels on the meeting's hide/show button (Mac hideEventButton / showEventButton).
        public static string HideEventButton => T("hideEventButton", "🙈 Hide");
        public static string ShowEventButton => T("showEventButton", "👀 Show");
        public static string MeetingsShowDismissedCount => T("meetingsShowDismissedCount", "Show hidden meetings ({0})");
        public static string MeetingColour => T("meetingColour", "Meeting colour");
        public static string MeetingColourDefault => T("meetingColourDefault", "Default");
        public static string MeetingSettingsTitle => T("eventSettingsTitleWindows", "Meeting settings");
        public static string MeetingSettingsHint => T("eventSettingsHintWindows", "Click for this meeting's settings");
        public static string MeetingColourLabel => T("eventColorLabel", "Colour");
        public static string MeetingFromCalendar => T("eventFromCalendarWindows", "This meeting comes from your synced calendar — change its time or guests there.");
        public static string MeetingHideForDay => T("hideEvent", "Hide this event");
        public static string MeetingShowAgain => T("showEventAgain", "Show this event again");
        public static string DialogDone => T("dayFixDone", "Done");

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
        public static string TasksDurationHint => T("tasksDurationHint", "Duration: type minutes (30), 45m or 1h, then Enter");
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
