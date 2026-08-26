using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Windows data-mapping layer for the Day Plan — turns the app's routines/habits/settings into
    /// the engine's routine segments, then runs <see cref="DayPlanScheduleBuilder"/> to get the day's
    /// blocks. The C# equivalent of the macOS ScheduleTab helpers
    /// (Mac-App: FocusBear/Schedule/ScheduleViewController.swift — habitInputs / estimatedDuration /
    /// standardSegments), adapted to <see cref="AppSettings"/>.
    ///
    /// Windows differences from macOS (intentional gaps for now):
    ///  • No on-demand custom routines (arrAllCustomRoutine) — morning + evening only.
    ///  • No non-timer duration history — non-timer habits use a sensible default.
    ///  • No break-frequency setting — deep work stays one block (no work/break sprint split).
    ///  • No per-day completed-id set — the live to-do arrays already exclude finished habits.
    /// Calendar meetings are deferred, so meetings are always empty here.
    /// </summary>
    public static class DayPlanDataSource
    {
        // "No duration set" sentinel for timer habits (matches AppSettings.nTimerValForNonTimerActivities).
        private const int NoDurationSentinel = 86400;

        // Config-driven display names (scheduleViewInfo), with English fallbacks via DayPlanText.
        public static string MorningRoutineHabits => DayPlanText.MorningRoutineHabits;
        public static string EveningRoutineHabits => DayPlanText.EveningRoutineHabits;

        private static AppSettings AppSettings => AppSettings.GetInstance;

        /// <summary>
        /// Whether a habit id has been completed or skipped in today's routine. Combines the in-session
        /// Day Plan set with the backend-sourced arrayCompletedActivities / arraySkipped — the latter
        /// are re-fetched on init, so done-state survives app restarts and habit edits.
        /// </summary>
        public static bool IsHabitDoneOrSkipped(string id, string dayKey)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var s = AppSettings;
            // Backend per-day completions / skips (works for today's earlier accordion and past days).
            if (s.dayPlanCompletedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> daySet) && daySet.Contains(id)) return true;
            if (s.dayPlanSkippedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> skipSet) && skipSet.Contains(id)) return true;
            // Today also has the in-session set + the live activity-props.
            if (dayKey == DateTime.Now.ToString("yyyy-MM-dd"))
            {
                if (s.dayPlanDoneOrSkippedHabitIds.Contains(id)) return true;
                if (JArrayContainsId(s.arrayCompletedActivities, id)) return true;
                if (JArrayContainsId(s.arraySkipped, id)) return true;
            }
            return false;
        }

        /// <summary>
        /// True if the habit was SKIPPED (not completed) on <paramref name="dayKey"/> — from the backend
        /// routine-progress skip cache or the live skipped array. Lets the Day Plan tag skipped habits
        /// distinctly from done ones. (The mixed in-session done-or-skipped set can't tell the two apart,
        /// so it's intentionally not consulted here — those default to the "done" appearance.)
        /// </summary>
        public static bool IsHabitSkipped(string id, string dayKey)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var s = AppSettings;
            if (s.dayPlanSkippedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> skipSet) && skipSet.Contains(id)) return true;
            if (dayKey == DateTime.Now.ToString("yyyy-MM-dd") && JArrayContainsId(s.arraySkipped, id)) return true;
            return false;
        }

        private static bool JArrayContainsId(JArray arr, string id)
        {
            if (arr == null) return false;
            foreach (JToken t in arr) if (t != null && t.ToString() == id) return true;
            return false;
        }

        /// <summary>
        /// Build the day's timetable for <paramref name="selectedDate"/>. For today this uses the live
        /// routine state (active routine + evening preview); for any other day it's a read-only preview
        /// of that day's scheduled habits (with done-state from the backend routine-progress cache).
        /// </summary>
        public static List<ScheduleBlock> BuildBlocks(DateTime selectedDate, ScheduleLabels labels = null, bool includeDismissedMeetings = false)
        {
            labels = labels ?? DayPlanText.EngineLabels();
            var settings = AppSettings;
            bool isToday = selectedDate.Date == DateTime.Today;
            DateTime endOfDay = EndOfDay(selectedDate);
            DateTime? eveningStart = TimeToday(settings.sDayShutOffTime, selectedDate);

            List<ScheduleRoutineSegment> segments;
            DateTime now;
            if (isToday)
            {
                now = DateTime.Now;
                segments = StandardSegments(now);
            }
            else
            {
                // Anchor a non-today plan at that day's morning start so it lays out from the top.
                now = TimeToday(settings.sDayStartTime, selectedDate) ?? selectedDate.Date;
                segments = SegmentsForDate(selectedDate);
            }

            bool isEveningActive = isToday && settings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS;
            // Show the "🌙 Evening routine" transition marker at the shutdown time whenever the evening
            // routine is still ahead. No deep-work filler after the shutdown time.
            DateTime? eveningRoutineStart = (!isEveningActive && eveningStart.HasValue && eveningStart.Value > now)
                ? eveningStart : (DateTime?)null;
            DateTime? deepWorkCutoff = eveningStart;

            return DayPlanScheduleBuilder.Build(
                segments,
                DayPlanCalendarManager.Shared.MeetingsFor(selectedDate, includeDismissedMeetings),   // backend calendar sync (Google/Microsoft)
                now,
                endOfDay,
                leaveBufferSeconds: 0,
                outOfOrderAllowed: false,
                eveningRoutineStart: eveningRoutineStart,
                deepWorkCutoff: deepWorkCutoff,
                sprintWorkSeconds: 0,                // no break-frequency source on Windows yet
                sprintBreakSeconds: 0,
                // Meetings floor: for today, start from `now` so already-finished meetings drop off the
                // agenda (matching how habits pack from `now`); an in-progress meeting (End > now) still
                // shows. For other days, surface the whole day.
                meetingsFrom: isToday ? DateTime.Now : selectedDate.Date,
                labels: labels);
        }

        /// <summary>
        /// Read-only routine segments for an arbitrary day (past or future): the morning + evening
        /// habits scheduled for that weekday, non-actionable, with done-state from the per-day cache.
        /// Mirrors macOS segmentsForDate (custom routines omitted — none on Windows).
        /// </summary>
        public static List<ScheduleRoutineSegment> SegmentsForDate(DateTime date)
        {
            var settings = AppSettings;
            var segs = new List<ScheduleRoutineSegment>();
            string dayKey = date.ToString("yyyy-MM-dd");
            string weekday = date.DayOfWeek.ToString();
            DateTime dayStart = date.Date;
            DateTime endOfDay = EndOfDay(date);
            DateTime morningStart = TimeToday(settings.sDayStartTime, date) ?? dayStart;
            DateTime? eveningStart = TimeToday(settings.sDayShutOffTime, date);

            var morning = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllMorningHabits, weekday), isEvening: false), morningStart, dayKey, asEarlier: true);
            if (morning.Count > 0)
            {
                segs.Add(new ScheduleRoutineSegment(MorningRoutineHabits, morning,
                                                    morningStart, eveningStart ?? endOfDay, actionable: false));
            }

            if (eveningStart.HasValue)
            {
                var evening = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllEveningHabits, weekday), isEvening: true), eveningStart.Value, dayKey, asEarlier: true);
                if (evening.Count > 0)
                {
                    segs.Add(new ScheduleRoutineSegment(EveningRoutineHabits, evening,
                                                        eveningStart.Value, endOfDay, actionable: false));
                }
            }
            return segs;
        }

        /// <summary>
        /// The day's routine segments: the active routine (actionable), plus a read-only preview of
        /// the evening routine once the morning/day routine hands off at the shutdown time.
        /// Mirrors macOS standardSegments (custom routines omitted — none on Windows).
        /// </summary>
        public static List<ScheduleRoutineSegment> StandardSegments(DateTime now)
        {
            var settings = AppSettings;
            string dayKey = now.ToString("yyyy-MM-dd");
            string weekday = now.DayOfWeek.ToString();   // only show habits scheduled for today's weekday
            var segs = new List<ScheduleRoutineSegment>();
            DateTime endOfDay = EndOfDay(now);
            DateTime? eveningStart = TimeToday(settings.sDayShutOffTime, now);
            bool isMorningActive = settings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS;
            bool isEveningActive = settings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS;

            // Between routines (e.g. the morning routine is done and the evening one hasn't started) —
            // show the evening routine as a read-only preview so the Day Plan still shows the rest of the
            // day (deep-work fill + the evening routine), instead of a static "well done" screen.
            if (!isMorningActive && !isEveningActive)
            {
                // "Earlier today": the finished morning routine, shown as a read-only, struck-through
                // accordion (collapsed by default) so completed habits stay visible with their times.
                DateTime? morningStart = TimeToday(settings.sDayStartTime, now);
                if (morningStart.HasValue)
                {
                    var pastMorning = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllMorningHabits, weekday), isEvening: false), morningStart.Value, dayKey, asEarlier: true);
                    if (pastMorning.Count > 0)
                    {
                        segs.Add(new ScheduleRoutineSegment(MorningRoutineHabits, pastMorning,
                                                            morningStart.Value, eveningStart ?? endOfDay,
                                                            actionable: false, isEarlier: true));
                    }
                }

                if (eveningStart.HasValue)
                {
                    if (eveningStart.Value > now)
                    {
                        // Evening routine still ahead → read-only preview.
                        var eveningPreview = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllEveningHabits, weekday), isEvening: true), now, dayKey, dropNonHighPriorityPastCutoff: true);
                        if (eveningPreview.Count > 0)
                            segs.Add(new ScheduleRoutineSegment(EveningRoutineHabits, eveningPreview,
                                                                eveningStart.Value, endOfDay, actionable: false));
                    }
                    else
                    {
                        // Evening window has started but the routine isn't active (finished/skipped — including
                        // on another device) → show it as a read-only, struck-through "earlier" section, like
                        // the morning one, instead of dropping it entirely.
                        var pastEvening = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllEveningHabits, weekday), isEvening: true), eveningStart.Value, dayKey, asEarlier: true);
                        if (pastEvening.Count > 0)
                            segs.Add(new ScheduleRoutineSegment(EveningRoutineHabits, pastEvening,
                                                                eveningStart.Value, endOfDay, actionable: false, isEarlier: true));
                    }
                }
                return segs;
            }

            // Active routine (the live to-do array — indices must match Play/Done/Skip in later milestones).
            // ShowMorningRoutine/ShowEveningRoutine normally apply the expired-trial top-3 trim right
            // after populating this array - but ShowEveningRoutine sets nCurrentActivityType = EVENING_HABITS
            // and can then early-return into the Focus Summary screen (not shown yet today) BEFORE ever
            // reaching that trim. The Day Plan then renders "active" with the untrimmed array. Apply the
            // trim here too (idempotent - a no-op once already trimmed) and reassign the actual field, not
            // a local copy, so Play/Done/Skip (which index into settings.arrAll*Habits directly) stay in
            // sync with what's displayed.
            List<habit> activeSource;
            string activeName;
            if (isMorningActive)
            {
                settings.arrAllMorningHabits = settings.ApplyExpiredHabitLimit(settings.arrAllMorningHabits, isEvening: false);
                activeSource = settings.arrAllMorningHabits;
                activeName = MorningRoutineHabits;
            }
            else
            {
                settings.arrAllEveningHabits = settings.ApplyExpiredHabitLimit(settings.arrAllEveningHabits, isEvening: true);
                activeSource = settings.arrAllEveningHabits;
                activeName = EveningRoutineHabits;
            }

            // For the active evening routine, drop non-high-priority habits once the cutoff has passed
            // (they can no longer be done) so the plan matches the routine and can fall to the empty state.
            var activeHabits = HabitInputs(activeSource, now, dayKey, dropNonHighPriorityPastCutoff: !isMorningActive);
            // Cap the morning/day routine at the evening start so it never bleeds into the evening.
            DateTime activeEnd = (isMorningActive && eveningStart.HasValue && eveningStart.Value > now)
                ? eveningStart.Value : endOfDay;
            segs.Add(new ScheduleRoutineSegment(activeName, activeHabits, now, activeEnd, actionable: true));

            // Evening routine preview, once the morning/day routine hands off at the shutdown time.
            if (isMorningActive && eveningStart.HasValue && eveningStart.Value > now)
            {
                var eveningHabits = HabitInputs(settings.ApplyExpiredHabitLimit(FilterByWeekday(settings.arrGLOBALAllEveningHabits, weekday), isEvening: true), now, dayKey, dropNonHighPriorityPastCutoff: true);
                if (eveningHabits.Count > 0)
                {
                    segs.Add(new ScheduleRoutineSegment(EveningRoutineHabits, eveningHabits,
                                                        eveningStart.Value, endOfDay, actionable: false));
                }
            }

            return segs;
        }

        /// <summary>
        /// Maps a routine's habits to engine inputs (index = position in the passed array, so
        /// Play/Done/Skip target the right row). Mirrors macOS habitInputs.
        /// </summary>
        public static List<ScheduleHabitInput> HabitInputs(List<habit> habits, DateTime now, string dayKey, bool asEarlier = false, bool dropNonHighPriorityPastCutoff = false)
        {
            var settings = AppSettings;
            var result = new List<ScheduleHabitInput>();
            if (habits == null) return result;
            string playingId = settings.currentHabitPlaying.id;
            for (int idx = 0; idx < habits.Count; idx++)
            {
                habit h = habits[idx];
                bool inDoneSet = IsHabitDoneOrSkipped(h.id, dayKey);
                // Normal segments drop habits already completed/skipped (so a mid-routine return shows
                // only the remaining ones). The "earlier today" section keeps them all, marking only the
                // actually-completed ones done (struck through) — a not-done past habit (e.g. one added
                // after the routine) stays un-struck and selectable so it can be marked done retroactively.
                if (!asEarlier && inDoneSet) continue;
                // Past the non-high-priority cutoff time, only high-priority evening habits can still be
                // done — the routine build drops the rest (App.ShowEveningRoutine). Mirror that here so the
                // Day Plan doesn't list habits that are no longer actionable; when nothing is left the
                // "No habits left — enjoy your free time" empty state shows instead (#1315-followup).
                if (dropNonHighPriorityPastCutoff && !asEarlier && settings.DidCutOffTimePassed() && !h.bHighPriorityActivity)
                    continue;
                bool playing = !string.IsNullOrEmpty(playingId) && playingId == h.id;
                result.Add(new ScheduleHabitInput(
                    index: idx,
                    title: string.IsNullOrEmpty(h.name) ? "Habit" : h.name,
                    durationSeconds: EstimatedDuration(h),
                    isPlaying: playing,
                    elapsedSeconds: 0,
                    cutoff: TimeToday(h.sCutOffTime, now),
                    isDone: asEarlier && inDoneSet,
                    id: h.id));
            }
            return result;
        }

        /// <summary>
        /// Duration to schedule a habit for. Timer habits use their set duration (5 min if unset);
        /// non-timer habits have no fixed duration and no history on Windows, so use a small default.
        /// Mirrors macOS estimatedDuration.
        /// </summary>
        public static int EstimatedDuration(habit h)
        {
            if (h.useTimer)
            {
                int d = h.durationSeconds;
                return (d > 0 && d < NoDurationSentinel) ? d : 5 * 60;
            }
            return 60;   // no history on Windows — a sensible default for a quick log-style habit
        }

        // MARK: - Time / day-of-week helpers

        /// <summary>A DateTime at the given "HH:mm"(":ss") time on <paramref name="now"/>'s date, or null.</summary>
        public static DateTime? TimeToday(string time, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(time)) return null;
            if (TimeSpan.TryParse(time.Trim(), CultureInfo.InvariantCulture, out TimeSpan ts))
            {
                return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).Add(ts);
            }
            return null;
        }

        private static DateTime EndOfDay(DateTime now)
        {
            return new DateTime(now.Year, now.Month, now.Day, 23, 59, 0);
        }

        /// <summary>
        /// Whether a days-of-week list ("all"/"weekdays"/"weekend"/named days) includes the weekday.
        /// Mirrors macOS isScheduledOn (empty list = every day).
        /// </summary>
        public static bool IsScheduledOn(List<string> days, string weekday)
        {
            if (days == null || days.Count == 0) return true;
            string wd = weekday.ToLowerInvariant();
            bool isWeekend = wd.StartsWith("sat") || wd.StartsWith("sun");
            foreach (var d in days)
            {
                string dl = (d ?? "").ToLowerInvariant();
                if (dl == "all") return true;
                if (dl == "weekdays" && !isWeekend) return true;
                if (dl == "weekend" && isWeekend) return true;
                if (dl.Length >= 3 && wd.StartsWith(dl.Substring(0, 3))) return true;
            }
            return false;
        }

        public static List<habit> FilterByWeekday(List<habit> habits, string weekday)
        {
            var result = new List<habit>();
            if (habits == null) return result;
            foreach (var h in habits)
            {
                if (IsScheduledOn(h.DaysOfWeek, weekday)) result.Add(h);
            }
            return result;
        }
    }
}
