using Focus_Bear.Utils;
using FocusBear.Core.Forecast;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Copy for the reprioritise plan, the To-do list's prompt and the Day Plan's buffer / Fix — the Mac's
    /// <c>ForecastText</c> (config block <c>localForecastViewInfo</c>) and its Day Fix strings
    /// (<c>scheduleViewInfo</c>), with the Mac's English as the fallback.
    /// </summary>
    public static class ForecastText
    {
        private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>
        {
            { "reviewPlan", "Reschedule plan" },
            { "reviewExplanationWindows", "Tick the date changes you want to apply. Hard deadlines stay where they are, and nothing is pushed past its project’s end date." },
            { "applySelected", "Apply selected changes" },
            { "selectAll", "Select all editable changes" },
            { "conflictsHeading", "Couldn’t pick a date — choose one yourself" },
            { "conflictsExplanation", "The plan wouldn’t move these on its own. Type a date on any of them and it becomes a change you can apply." },
            { "projectEndDate", "Can’t be finished until <DATE>, after <PROJECT> ends on <ENDDATE>." },
            { "projectEndDateNoProject", "Can’t be finished until <DATE>, after its project ends." },
            { "keepPlannedDays", "Don't change tasks scheduled for today or tomorrow" },
            { "plannedDaysLocked", "Today and tomorrow are taken as already planned, so no work is moved into them." },
            { "dayPlanFollowsTheDates", "Tasks you have put on a day in your Day Plan move to their new day as well." },
            { "dayFocusHeading", "Freeing up <DAY> (<COUNT>)" },
            { "dayFocusRelief", "Moving these frees <FREED> of the <SHORTFALL> <DAY> is over." },
            { "dayFocusClears", "Moving these frees <FREED>, which is enough to make <DAY> fit." },
            { "dayFocusRemainder", "<DAY> would still be <REMAINING> over." },
            { "dayFocusPinnedRemainder", "<DAY> would still be <REMAINING> over, because <COUNT> task(s) on it are rated “hard deadline — can’t shift” and stay put. Shorten the routine, or lower a rating." },
            { "dayFocusNothingToMove", "Nothing on <DAY> can be moved to a later day, so this screen can’t make it fit. Try shortening the routine or a meeting instead." },
            { "postponementLimitNoDate", "Can’t be finished inside the time “<BAND>” allows." },
            { "postponementLimit", "Can’t be finished until <DATE>, which is further past its deadline than “<BAND>” allows." },
            { "hardDeadline", "The hard deadline cannot be met with the available time. Its date has not been moved." },
            { "outsideHorizon", "This work does not fit within the selected planning horizon." },
            { "missingEstimate", "Add estimates to the remaining tasks to calculate completion." },
            { "outcomeMissing", "Choose an outcome before proposing an extension for this project." },
            { "typeADateWindows", "Type your own date — 2y, 14d, 3wk, fri, 28 sep — then press Enter" },
            { "reasonYouChoseIt", "You chose this date." },
            { "reasonBroughtForward", "Brought forward: the work is finished by this day once everything ahead of it is done." },
            { "reasonPushedBack", "Pushed back: the days before this are already committed." },
            { "projectDeadlineHeading", "Project deadlines" },
            { "projectDeadlineExplanation", "These change a whole PROJECT’s deadline, not one task’s." },
            { "projectDeadlineRow", "Set <PROJECT>’s project deadline to <DATE>" },
            { "projectDeadlineHeadingCountWindows", "<HEADING> (<COUNT>)" },
            { "selectAllInProject", "Select all <COUNT> changes in this project" },
            { "majorChanges", "Major changes — moved 5 days or more (<COUNT>)" },
            { "minorChanges", "Minor changes — moved less than 5 days (<COUNT>)" },
            { "reprioritiseMenuItem", "Re-prioritise todo list" },
            { "reprioritiseLink", "Help me re-prioritise to-do list" },
            { "reprioritiseOverdue", "You have <COUNT> overdue tasks." },
            { "reprioritiseOverCapacity", "One of the next <DAYS> days is <DURATION> over what you can fit." },
            { "reprioritiseDismiss", "Dismiss this suggestion" },
            { "noChanges", "No date changes to propose." },
            { "noPermission", "This item must be synced or edited by someone with permission." },
            { "planChanged", "The plan changed while you reviewed it. Review the updated proposal before applying changes." },
            { "planSaved", "Saved <COUNT> date changes." },
            { "planPartiallySaved", "Saved <COUNT> date changes, then stopped because an item changed or could not be saved. Refresh and review the remaining changes." },
            { "unprojected", "No project" },
            { "close", "Close" },
            { "refresh", "Refresh" },
            { "loading", "Calculating forecast…" },
            { "loadFailed", "The complete forecast could not be loaded. Refresh to try again." },
            { "noDeadline", "No due date" },
            { "plannedTotal", "<DURATION> of tasks planned" },
            { "bufferTotal", "<BUFFER> buffer" },
            { "notEnoughTimeForTasks", "Not enough time for all tasks (over by <DURATION>)" },
            { "noEstimate", "No estimate" },
            { "editDuration", "Click to change how long this will take" },
            { "durationNotSaved", "Couldn’t save that duration." },
            { "taskOutcomeNotSavedWindows", "Couldn’t save that outcome, so it is back where it was." },
            { "outcome", "Outcome · 1–5" },
            { "inheritedFromProject", "<PROJECT>’s rating is stricter than these tasks’ own, so they are planned as <OUTCOME>." },
            { "durationFieldHintWindows", "45m, 1h30 or 45 — then press Enter" },
            { "dateNotRecognisedWindows", "Not a date I recognise" },
        };

        private static readonly Dictionary<string, string> DayFixFallback = new Dictionary<string, string>
        {
            { "dayFixLink", "Fix" },
            { "dayFixTitle", "Make today fit" },
            { "dayFixTitleDayWindows", "Make <DAY> fit" },
            { "dayFixGap", "Today is <DURATION> over what you can fit." },
            { "dayFixGapDayWindows", "<DAY> is <DURATION> over what you can fit." },
            { "dayFixNothingToFree", "Nothing left on today can be shortened or moved from here." },
            { "dayFixMeetingsOptionWindows", "Skip or shorten meetings" },
            { "dayFixRoutineOption", "Shorten the routine" },
            { "dayFixTasksOption", "Postpone tasks" },
            { "dayFixMeetingsDetail", "<DURATION> of meetings still to come" },
            { "dayFixRoutineDetail", "<DURATION> of habits before the evening routine" },
            { "dayFixTasksDetail", "<DURATION> of tasks planned today" },
            { "dayFixBack", "Back" },
            { "dayFixDone", "Done" },
            { "dayFixSkip", "Skip" },
            { "dayFixUnskip", "Un-skip" },
            { "dayFixShorten", "Shorten" },
            { "dayFixMeetingShortenHintWindows", "Applies on %@ only, and only on this computer. The meeting on your calendar is untouched, so nobody else sees a change." },
            { "dayFixMeetingShortenNoOptions", "This meeting is already as short as it goes." },
        };

        public static string Get(string key)
        {
            return Read("localForecastViewInfo", key, Fallback);
        }

        public static string DayFix(string key)
        {
            return Read("scheduleViewInfo", key, DayFixFallback);
        }

        private static string Read(string block, string key, Dictionary<string, string> fallback)
        {
            try
            {
                string v = (AppSettings.GetInstance.ConfigFileStrings?[block] as JObject)?[key]?.ToString();
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch { }
            string f;
            return fallback.TryGetValue(key, out f) ? f : key;
        }

        public static string Format(string template, params string[] pairs)
        {
            string s = template ?? "";
            for (int i = 0; i + 1 < pairs.Length; i += 2) s = s.Replace(pairs[i], pairs[i + 1]);
            return s;
        }

        /// <summary>"1h 5m" / "45m" — the Mac's abbreviated hours+minutes.</summary>
        public static string Duration(double minutes)
        {
            int m = (int)Math.Round(Math.Max(0, minutes));
            int h = m / 60;
            m %= 60;
            if (h > 0) return m > 0 ? h + "h " + m + "m" : h + "h";
            return m + "m";
        }

        /// <summary>A buffer can be negative (the day is over) — the sign goes back on explicitly.</summary>
        public static string SignedDuration(double minutes)
        {
            return (minutes < 0 ? "-" : "") + Duration(Math.Abs(minutes));
        }

        public static string Date(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("d MMM yyyy", CultureInfo.CurrentCulture) : Get("noDeadline");
        }

        public static string DayLabel(DateTime day)
        {
            return day.ToString(day.Year == DateTime.Today.Year ? "ddd d MMM" : "ddd d MMM yyyy", CultureInfo.CurrentCulture);
        }

        /// <summary>The /5 band wording a rating is judged by (the To-do list's outcome words).</summary>
        public static string OutcomeBand(int outcome)
        {
            return Focus_Bear.ToDoScreens.ToDoStrings.Load().OutcomeLabel(Math.Max(1, outcome));
        }

        /// <summary>Why the plan refused a row, in words the user can check against their own data.</summary>
        public static string Conflict(LocalTimeForecast.ConflictReason reason, DateTime? earliest, int? outcome,
                                      string projectName, DateTime? projectEnd)
        {
            switch (reason)
            {
                case LocalTimeForecast.ConflictReason.HardDeadline: return Get("hardDeadline");
                case LocalTimeForecast.ConflictReason.PostponementLimit:
                    if (!earliest.HasValue) return Format(Get("postponementLimitNoDate"), "<BAND>", OutcomeBand(outcome ?? 0));
                    return Format(Get("postponementLimit"), "<DATE>", Date(earliest), "<BAND>", OutcomeBand(outcome ?? 0));
                case LocalTimeForecast.ConflictReason.OutsideHorizon: return Get("outsideHorizon");
                case LocalTimeForecast.ConflictReason.MissingEstimate: return Get("missingEstimate");
                case LocalTimeForecast.ConflictReason.MissingOutcome: return Get("outcomeMissing");
                default:
                    if (!earliest.HasValue || projectName == null || !projectEnd.HasValue)
                        return Format(Get("projectEndDateNoProject"), "<DATE>", earliest.HasValue ? Date(earliest) : Get("noDeadline"));
                    return Format(Get("projectEndDate"), "<DATE>", Date(earliest), "<PROJECT>", projectName, "<ENDDATE>", Date(projectEnd));
            }
        }

        public static string SuggestionText(ReprioritiseSuggestion.Reason reason)
        {
            if (reason.Kind == ReprioritiseSuggestion.ReasonKind.Overdue)
                return Format(Get("reprioritiseOverdue"), "<COUNT>", reason.OverdueCount.ToString(CultureInfo.CurrentCulture));
            return Format(Get("reprioritiseOverCapacity"), "<DURATION>", Duration(reason.OverMinutes),
                          "<DAYS>", ReprioritiseSuggestion.HorizonDays.ToString(CultureInfo.CurrentCulture));
        }
    }
}
