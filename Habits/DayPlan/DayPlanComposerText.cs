namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Strings for the Day Plan task-name composer (the <c>#project</c> / <c>!priority</c> / <c>*minutes</c>
    /// shortcuts) and the Day Plan's top-left schedule settings cog. Same <c>scheduleViewInfo</c> keys and
    /// English fallbacks as the macOS <c>ScheduleText</c>, read through <see cref="DayPlanText.T"/>. Kept
    /// apart from <see cref="DayPlanText"/> so the composer port stays in its own files.
    /// </summary>
    public static class DayPlanComposerText
    {
        // The shortcut card shown under a task while its name is being edited (Mac TaskShortcutCard).
        public static string ShortcutsTitle => DayPlanText.T("tasksShortcutsTitle", "Shortcuts you can type in the name");
        public static string ShortcutsDismiss => DayPlanText.T("tasksShortcutsDismiss", "Don't show this again");
        public static string ShortcutProject => DayPlanText.T("tasksShortcutProject", "#project — file the task under a project");
        public static string ShortcutPriority => DayPlanText.T("tasksShortcutPriority", "!1 to !5 — set importance (!5 is the most important)");
        public static string ShortcutMinutes => DayPlanText.T("tasksShortcutMinutes", "*15 — set the duration to 15 minutes");

        /// <summary>
        /// The Day Plan ROW grammar's hints. A spelled "30m" is part of the name on a row (it has its own
        /// duration), so — exactly as on the Mac — the card does not offer it.
        /// </summary>
        public static string[] DayPlanRowHints => new[] { ShortcutProject, ShortcutPriority, ShortcutMinutes };

        /// <summary>Why a name made only of tokens was not saved (Mac tasksNameNeeded).</summary>
        public static string NameNeeded => DayPlanText.T("tasksNameNeeded",
            "Add a name for this task. Tags like #project, !priority and *minutes go after the name.");

        // Schedule settings cog (Mac scheduleCog / openScheduleSettings).
        public static string ScheduleSettings => DayPlanText.T("scheduleSettings", "Schedule settings");
        public static string ShowEarlierEvents => DayPlanText.T("showEarlierEvents", "Show events from earlier today");
        public static string HideDeepWork => DayPlanText.T("hideDeepWork", "Hide deep work blocks");
        public static string HideHabitBlocks => DayPlanText.T("hideHabitBlocks", "Hide habits");
        public static string ShowDismissedEvents => DayPlanText.T("showDismissedEvents", "Show dismissed events");
    }
}
