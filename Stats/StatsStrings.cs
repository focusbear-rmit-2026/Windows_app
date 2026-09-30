using Focus_Bear.Utils;
using FocusBear.Core.Stats;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Focus_Bear.StatsScreens
{
    /// <summary>
    /// User-facing strings for the native Stats screen, from the shared config's <c>statsViewInfo</c> section
    /// (the same keys the Mac's <c>StatsStrings.swift</c> reads), with its English fallbacks. Templates use
    /// <c>{name}</c> placeholders filled by <see cref="StatsText.Fill"/>.
    /// </summary>
    public sealed class StatsStrings
    {
        private readonly JObject _info;

        private StatsStrings(JObject info)
        {
            _info = info;
        }

        public static StatsStrings Load()
        {
            var config = AppSettings.GetInstance.ConfigFileStrings;
            return new StatsStrings(config != null ? config["statsViewInfo"] as JObject : null);
        }

        private string S(string key, string fallback)
        {
            var value = _info != null ? _info[key] as JValue : null;
            string text = value != null && value.Type == JTokenType.String ? (string)value : null;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        // Tabs
        public string TabGoals => S("tabGoals", "Your Goals");
        public string TabProgress => S("tabProgress", "Progress");
        public string TabStreak => S("tabStreak", "Streak");
        public string TabPepTalk => S("tabPepTalk", "Pep Talk");
        public string TabLeaderboard => S("tabLeaderboard", "Leaderboard");
        public string TabHabitStats => S("tabHabitStats", "Habit Stats");
        public string TabProductivity => S("tabProductivity", "Productivity");
        public string TabFocusLog => S("tabFocusLog", "Focus Log");

        public string TabTitle(StatsTab tab)
        {
            switch (tab)
            {
                case StatsTab.Goals: return TabGoals;
                case StatsTab.Progress: return TabProgress;
                case StatsTab.PepTalk: return TabPepTalk;
                case StatsTab.Leaderboard: return TabLeaderboard;
                case StatsTab.HabitStats: return TabHabitStats;
                case StatsTab.Productivity: return TabProductivity;
                case StatsTab.FocusLog: return TabFocusLog;
                default: return TabStreak;
            }
        }

        // Shared
        public string Title => S("title", "Stats");
        public string LoadFailed => S("loadFailedText", "Could not load stats. Check your connection and try again.");
        public string Retry => S("retryText", "Retry");
        public string Refresh => S("refreshText", "Refresh");
        public string Save => S("saveText", "Save");
        public string Cancel => S("cancelText", "Cancel");
        public string Delete => S("deleteText", "Delete");
        public string Edit => S("editText", "Edit");
        public string Close => S("closeText", "Close");
        public string Date => S("dateText", "Date");

        // Streak
        public string MorningRoutine => S("morningRoutineText", "Morning Routine");
        public string EveningRoutine => S("eveningRoutineText", "Evening Routine");
        public string FocusBlocks => S("focusBlocksText", "Focus Blocks");
        public string DaysUntilLevelUp => S("daysUntilLevelUpText", "{days} more days until you level up");
        public string AverageCompletion => S("averageCompletionText", "Average completion:");
        public string PercentValue => S("percentValueText", "{value}%");
        public string PerDayValue => S("perDayValueText", "{value}/day");
        public string DayStreak => S("dayStreakText", "Day Streak");

        // Progress
        public string Start => S("startText", "Start");
        public string End => S("endText", "End");
        public string LevelTitle => S("levelTitleText", "Level {level}");
        public string LevelGoals => S("levelGoalsText", "Level {level} goals:");
        public string NextLevelRequirements => S("nextLevelRequirementsText", "Next level requirements");
        public string MorningRoutineStreak => S("morningRoutineStreakText", "Morning Routine Streak");
        public string MicroBreakStreak => S("microBreakStreakText", "Micro Break Streak");
        public string EveningRoutineStreak => S("eveningRoutineStreakText", "Evening Routine Streak");
        public string FocusModeStreak => S("focusModeStreakText", "Focus Mode Streak");
        public string ChatWithFocusBear => S("chatWithFocusBearText", "Chat with Focus Bear");
        public string SetUpFocusMode => S("setUpFocusModeText", "Set up a Focus Mode");
        public string SetupBlockedUrls => S("setupBlockedUrlsText", "Setup always blocked URLs");
        public string InstallMobileApp => S("installMobileAppText", "Install mobile application");
        public string EditYourHabits => S("editYourHabitsText", "Edit your habits");

        private static readonly string[] DefaultLevelNames =
        {
            "Novice", "Apprentice", "Explorer", "Disciplined", "Diligent", "Strategist", "Mentor", "Scholar", "Guardian", "Achiever",
        };

        public string LevelName(int level)
        {
            var names = _info != null ? _info["levelNames"] as JArray : null;
            var list = names != null && names.Count == DefaultLevelNames.Length ? names.Select(n => (string)n).ToArray() : DefaultLevelNames;
            return level >= 0 && level < list.Length ? list[level] : "";
        }

        public string RequirementName(LevelRequirementKind kind)
        {
            switch (kind)
            {
                case LevelRequirementKind.Morning: return MorningRoutineStreak;
                case LevelRequirementKind.MicroBreak: return MicroBreakStreak;
                case LevelRequirementKind.Evening: return EveningRoutineStreak;
                case LevelRequirementKind.FocusMode: return FocusModeStreak;
                case LevelRequirementKind.Chat: return ChatWithFocusBear;
                default: return SetUpFocusMode;
            }
        }

        // Your Goals
        public string NoLongTermGoals => S("noLongTermGoalsText", "You haven't set long term goals.");
        public string AddGoal => S("addGoalText", "Add goal");
        public string GoalPlaceholder => S("goalPlaceholder", "Enter a goal");
        public string GoalsSaved => S("goalsSavedText", "Goals saved");
        public string GoalsSaveFailed => S("goalsSaveFailedText", "Could not save goals");

        // Pep Talk
        public string ToneName(string tone)
        {
            var names = _info != null ? _info["toneNames"] as JObject : null;
            var translated = names != null ? names[tone] as JValue : null;
            if (translated != null && translated.Type == JTokenType.String && !string.IsNullOrEmpty((string)translated)) return (string)translated;
            if (tone == "humorous") return "Humorous";
            if (tone == "sassy") return "Sassy";
            return tone;
        }

        public string PepTalkUnavailable => S("pepTalkUnavailableText", "We couldn't generate a personalized message today. Please try again later.");
        public string PepTalkRateLimited => S("pepTalkRateLimitedText", "Too many pep talks just now — the server is asking us to wait a moment. Retry will work again shortly.");
        public string MotivationalStyle => S("motivationalStyleText", "Motivational Style");
        public string MotivationalNotice => S("motivationalNoticeText", "Your routine streak and your routine activities will be sent to Open AI. No other details will be shared, but if anything in your habit titles would identify you, we recommend you remove it.");

        // Leaderboard
        public string SortBy => S("sortByText", "Sort by");
        public string MorningStreak => S("morningStreakText", "Morning Streak");
        public string BreakStreak => S("breakStreakText", "Break Streak");
        public string EveningStreak => S("eveningStreakText", "Evening Streak");
        public string FocusMode => S("focusModeText", "Focus Mode");
        public string Rank => S("rankText", "Rank");
        public string Name => S("nameText", "Name");
        public string NoUserFound => S("noUserFoundText", "No User Found");
        public string LeaderboardUsername => S("leaderboardUsernameText", "Leaderboard Username");
        public string Update => S("updateText", "Update");
        public string UsernameRule => S("usernameRuleText", "Letters and underscores only");
        public string StreakValue => S("streakValueText", "Streak: {streak}");
        public string DaysCompletedOutOf => S("daysCompletedOutOfText", "{completed} days completed out of last {total} days");
        public string DaysCompletedOutOfExpected => S("daysCompletedOutOfExpectedText", "Last {window} days: {completed} completed, {forgiven} weekend days forgiven");
        public string Ascending => S("ascendingText", "Ascending");
        public string Descending => S("descendingText", "Descending");

        public string SortTitle(LeaderboardSortKey key)
        {
            switch (key)
            {
                case LeaderboardSortKey.MicroBreak: return BreakStreak;
                case LeaderboardSortKey.Evening: return EveningStreak;
                case LeaderboardSortKey.FocusMode: return FocusMode;
                default: return MorningStreak;
            }
        }

        // Habit Stats
        public string NoHabitsTitle => S("noHabitsTitle", "Add a habit to see your stats");
        public string NoHabitsDescription => S("noHabitsDescription", "Go to \"{navItem}\" in the side menu, then add a habit to a routine.");
        public string EditHabitsNavItem => S("editHabitsNavItem", "Edit Habits");
        public string MostConsistentHabits => S("mostConsistentHabitsText", "Most consistent habits");
        public string LeastConsistentHabits => S("leastConsistentHabitsText", "Least consistent habits");
        public string PercentOfLastDays => S("percentOfLastDaysText", "% of last {days} days");
        public string TotalCompletions => S("totalCompletionsText", "Total completions");
        public string HoursSpent => S("hoursSpentText", "Hours spent");
        public string CurrentStreak => S("currentStreakText", "Current streak");
        public string Last30Days => S("last30DaysText", "Last 30 days");
        public string HoursValue => S("hoursValueText", "{value} hrs");
        public string DaysValue => S("daysValueText", "{value} days");
        public string MinutesValue => S("minutesValueText", "{value} min");
        public string HabitCompletionHistory => S("habitCompletionHistoryText", "Habit Completion History");
        public string Less => S("lessText", "Less");
        public string More => S("moreText", "More");
        public string SelectQuestions => S("selectQuestionsText", "Select questions");
        public string QuantityLogged => S("quantityLoggedText", "Quantity Logged");
        public string NoDataAvailable => S("noDataAvailableText", "No data available for this habit yet");
        public string LogQuantityHistory => S("logQuantityHistoryText", "Log Quantity History");
        public string DailyTotal => S("dailyTotalText", "Daily total");
        public string DailyAverage => S("dailyAverageText", "Daily average");
        public string Completion => S("completionText", "completion");
        public string Completions => S("completionsText", "completions");

        // Productivity
        public string Daily => S("dailyText", "Daily");
        public string Monthly => S("monthlyText", "Monthly");
        public string Yearly => S("yearlyText", "Yearly");
        public string FocusedMinutes => S("focusedMinutesText", "Focused Minutes");
        public string FocusedHours => S("focusedHoursText", "Focused Hours");
        public string Total => S("totalText", "Total");
        public string AverageFocusPerDay => S("averageFocusPerDayText", "Average Focus Per Day");
        public string MostFocusedDay => S("mostFocusedDayText", "Most Focused Day");
        public string AverageTabCount => S("averageTabCountText", "Average Tab Count");
        public string AverageTabDuration => S("averageTabDurationText", "Average Tab Duration");
        public string TabsValue => S("tabsValueText", "{value} tabs");
        public string SecondsShortValue => S("secondsShortValueText", "{value}s");
        public string FocusedMinutesDescription => S("focusedMinutesDescriptionText", "Time spent in focus sessions weighted by alignment score");
        public string AlignmentScoreTooltip => S("alignmentScoreTooltipText", "Alignment score measures how well you stayed focused during a session. Time is weighted by this score, so sessions with higher focus contribute more to your total.");
        public string NoProductivityData => S("noProductivityDataText", "No productivity data available yet. Complete some focus sessions to see your stats.");

        public string PeriodTitle(ProductivityPeriod period)
        {
            switch (period)
            {
                case ProductivityPeriod.Monthly: return Monthly;
                case ProductivityPeriod.Yearly: return Yearly;
                default: return Daily;
            }
        }

        // Focus Log
        public string AddManualSession => S("addManualSessionText", "Add Manual Session");
        public string FocusBlockSummary => S("focusBlockSummaryText", "Focus Block Summary");
        public string Export => S("exportText", "Export");
        public string Duration => S("durationText", "Duration");
        public string StartTime => S("startTimeText", "Start Time");
        public string FocusLogTime => S("focusLogTimeText", "Time");
        public string FinishTime => S("finishTimeText", "Finish Time");
        public string Intention => S("intentionText", "Intention");
        public string Achievements => S("achievementsText", "Achievements");
        public string Distractions => S("distractionsText", "Distractions");
        public string NoFocusSummary => S("noFocusSummaryText", "No focus summary available");
        public string StartFocusSessionHint => S("startFocusSessionHintText", "Start a focus session to see your performance summary here.");
        public string LoadMoreSessions => S("loadMoreSessionsText", "Load more sessions");
        public string ConfirmDeleteSession => S("confirmDeleteSessionText", "Are you sure you want to delete this focus session?");
        public string FinishAfterStart => S("finishAfterStartText", "Finish time must be after start time");
        public string StartInFuture => S("startInFutureText", "Start time cannot be in the future");
        public string FinishInFuture => S("finishInFutureText", "Finish time cannot be in the future");
        public string DurationHours => S("durationHoursText", "Duration (hours)");
        public string FilterPlaceholder => S("filterPlaceholder", "Filter");
        public string SessionSaveFailed => S("sessionSaveFailedText", "Could not save the focus session");

        public string ColumnTitle(FocusLogColumn column)
        {
            switch (column)
            {
                case FocusLogColumn.Duration: return Duration;
                case FocusLogColumn.Intention: return Intention;
                case FocusLogColumn.Distractions: return Distractions;
                case FocusLogColumn.FocusMode: return FocusMode;
                default: return FocusLogTime;
            }
        }

        public string Problem(ManualSessionProblem problem)
        {
            switch (problem)
            {
                case ManualSessionProblem.StartInFuture: return StartInFuture;
                case ManualSessionProblem.FinishInFuture: return FinishInFuture;
                default: return FinishAfterStart;
            }
        }

        /// <summary>"47 min", "3.2 hrs", "2 days, 5 hrs" — the Mac's <c>focusedTimeText</c>.</summary>
        public string FocusedTime(double minutes)
        {
            var humanized = HumanizedFocusTime.From(minutes);
            string value = StatsText.PlainNumber(humanized.Value);
            switch (humanized.TimeUnit)
            {
                case HumanizedFocusTime.Unit.Minutes: return StatsText.Fill(MinutesValue, ("value", value));
                case HumanizedFocusTime.Unit.Hours: return StatsText.Fill(HoursValue, ("value", value));
                default:
                    return StatsText.Fill(DaysValue, ("value", value)) + ", " + StatsText.Fill(HoursValue, ("value", humanized.RemainingHours.ToString()));
            }
        }

        public string HumanizeSeconds(int seconds) => FocusLogMath.HumanizeSeconds(seconds, DurationWords.English);
    }
}
