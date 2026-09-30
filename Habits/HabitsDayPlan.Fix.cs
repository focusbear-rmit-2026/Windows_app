using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.Forecast;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DayFixPlan = FocusBear.Core.Schedule.DayFixPlan;
using DayFixOption = FocusBear.Core.Schedule.DayFixOption;
using DayPlanBlockKey = FocusBear.Core.Schedule.DayPlanBlockKey;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// The tasks panel's buffer line, the red "Not enough time for all tasks" warning and its "Fix" — the
    /// Mac's <c>DayPlanTasksPanel.refreshTotalLabel</c> + <c>openDayFixModal</c> (report 2026-09-25_14-33-34).
    /// The day's capacity and the absorption of tasks pinned to a habit or meeting come from one single-day
    /// forecast (Core <see cref="LocalTimeForecast"/>), the same arithmetic the reprioritise plan uses.
    /// </summary>
    public partial class HabitsDayPlan
    {
        private static readonly Color CBufferRed = Color.FromRgb(0xFF, 0x3B, 0x30);

        /// <summary>The viewed day's load, or null for a day that has already gone (nothing to fix there).</summary>
        private F.DayLoad? ViewedDayLoad(out List<ScheduleBlock> blocks) => DayLoadOn(_selectedDate, out blocks);

        /// <summary>
        /// A day's load from a single-day forecast over its Day Plan blocks and planned tasks, or null for a day
        /// that has already gone. Shared by the tasks panel's buffer line and the date picker's per-day colours
        /// (the Mac's <c>netFreeMinutes(on:)</c> is "the same remaining-day capacity and task absorption as the
        /// header"): the buffer is the net free time.
        /// </summary>
        internal static F.DayLoad? DayLoadOn(DateTime date, out List<ScheduleBlock> blocks)
        {
            blocks = null;
            if (date.Date < DateTime.Today) return null;
            try
            {
                bool isToday = date.Date == DateTime.Today;
                blocks = DayPlanDataSource.BuildBlocks(date);
                DateTime? from = isToday ? DateTime.Now : (DateTime?)null;
                F.DayInput day = ReprioritiseForecast.DayInputFrom(date, blocks, from, false);
                var planned = DayPlanTaskStore.Shared.Tasks(date);
                var tasks = planned.Select(t => new F.TaskInput(
                    t.Id, estimatedMinutes: t.DurationMinutes, outcome: t.Outcome ?? F.TaskInput.UnratedOutcome,
                    blockAssignment: DayPlanBlockKey.Absorbs(t.BlockKey) ? new F.BlockAssignment(date.Date, t.BlockKey) : (F.BlockAssignment?)null,
                    completed: t.Done)).ToList();
                F.Result result = F.Calculate(new[] { day }, new F.ProjectInput[0], tasks);
                F.Day row = result.Days.FirstOrDefault();
                if (row == null) return null;
                double remaining = planned.Where(t => !t.Done).Sum(t => t.DurationMinutes);
                return F.DayLoadFor(date.Date, row.Capacity.AvailableWorkMinutes, remaining, row.AbsorbedBlockMinutes);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsDayPlan.DayLoadOn ex - " + ex.Message);
                return null;
            }
        }

        /// <summary>"≈ 2h of tasks planned · 35m buffer", the buffer red once it's negative; plus the banner + Fix.</summary>
        private void RenderBufferAndBanner(int remainingMins)
        {
            TasksTotal.Inlines.Clear();
            TasksBanner.Children.Clear();
            TasksBanner.Visibility = Visibility.Collapsed;
            List<ScheduleBlock> blocks;
            F.DayLoad? load = ViewedDayLoad(out blocks);
            if (remainingMins > 0)
                TasksTotal.Inlines.Add(new Run(ForecastText.Format(ForecastText.Get("plannedTotal"), "<DURATION>", ForecastText.Duration(remainingMins))));
            if (load.HasValue)
            {
                if (TasksTotal.Inlines.Count > 0) TasksTotal.Inlines.Add(new Run(" · "));
                var buffer = new Run(ForecastText.Format(ForecastText.Get("bufferTotal"), "<BUFFER>", ForecastText.SignedDuration(load.Value.BufferMinutes)));
                if (load.Value.IsOvercommitted) buffer.Foreground = new SolidColorBrush(CBufferRed);
                TasksTotal.Inlines.Add(buffer);
            }
            if (!load.HasValue || !load.Value.IsOvercommitted) return;

            // Hidden entirely on a day that fits, so it costs no height. The action beside it is "Fix".
            var text = new TextBlock
            {
                Text = ForecastText.Format(ForecastText.Get("notEnoughTimeForTasks"), "<DURATION>", ForecastText.Duration(load.Value.OvercommittedMinutes)),
                FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(CBufferRed),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetAutomationId(text, "DayPlanOverCapacity");
            var fix = new TextBlock
            {
                Text = ForecastText.DayFix("dayFixLink"), FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(COrange), TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetAutomationId(fix, "DayPlanFix");
            fix.MouseLeftButtonUp += (s, e) => { e.Handled = true; Dispatcher.BeginInvoke(new Action(OpenDayFix)); };
            TasksBanner.Children.Add(text);
            TasksBanner.Children.Add(fix);
            TasksBanner.Visibility = Visibility.Visible;
        }

        // MARK: - Fix

        private void OpenDayFix()
        {
            try
            {
                appSettings.CapturePostHog("day-plan-fix-opened");
                DateTime day = _selectedDate.Date;
                string dayLabel = ForecastText.DayLabel(day);
                DayPlanFixDialog dlg = null;
                dlg = new DayPlanFixDialog(dayLabel, IsViewingToday,
                    () => { List<ScheduleBlock> b; return ViewedDayLoad(out b)?.OvercommittedMinutes ?? 0; },
                    FixOptions, FixMeetings, FixHabits,
                    m =>
                    {
                        DayPlanCalendarManager.Shared.SetDismissed(m.EventId, day, !m.IsSkipped);
                        RefreshTasksAndAgenda();
                    },
                    m => ShortenMeeting(m, day, dlg),
                    h => { OnShortenHabit(h.HabitId); RefreshTasksAndAgenda(); },
                    () =>
                    {
                        List<ScheduleBlock> b;
                        double over = ViewedDayLoad(out b)?.OvercommittedMinutes ?? 0;
                        ReprioritiseWindow.Open(Window.GetWindow(this), new ForecastDayFocus.Target(day, over));
                    },
                    Window.GetWindow(this));
                dlg.ShowDialog();
                RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan.OpenDayFix ex - " + ex.Message); }
        }

        /// <summary>Routes biggest first, only those that could free a minute (Core DayFixPlan.Options).</summary>
        private IList<DayFixOption> FixOptions()
        {
            double meetings = FixMeetings().Where(m => !m.IsSkipped).Sum(m => m.Seconds) / 60.0;
            double habits = FixHabits().Sum(h => h.Seconds) / 60.0;
            double tasks = DayPlanTaskStore.Shared.Tasks(_selectedDate).Where(t => !t.Done && !string.IsNullOrEmpty(t.SourceTaskId)).Sum(t => t.DurationMinutes);
            return DayFixPlan.Options(meetings, habits, tasks);
        }

        /// <summary>The day's meetings still to come, longest first (hidden ones listed as skipped).</summary>
        private IList<DayFixMeetingRow> FixMeetings()
        {
            DateTime now = DateTime.Now;
            var rows = DayPlanCalendarManager.Shared.MeetingsFor(_selectedDate, includeDismissed: true)
                .Where(m => m.End > (IsViewingToday ? now : _selectedDate.Date))
                .Select(m => new DayFixMeetingRow
                {
                    EventId = m.Id,
                    Title = m.Title,
                    TimeLabel = TimeString(m.Start),
                    Seconds = (int)(m.End - (m.Start < _selectedDate.Date ? _selectedDate.Date : m.Start)).TotalSeconds,   // the part inside this day (a meeting from the night before)
                    IsSkipped = m.IsDismissed,
                    IsShortened = DayPlanMeetingTrims.OverrideSeconds(m.Id, _selectedDate).HasValue,
                });
            return DayFixPlan.LongestFirst(rows, r => r.Seconds, r => r.Title);
        }

        /// <summary>
        /// The habits the routine route offers: the ones inside the working day — not the evening routine, and
        /// starting before it (Core DayFixPlan.HabitsInsideWorkingDay), not already done, longest first.
        /// </summary>
        private IList<DayFixHabitRow> FixHabits()
        {
            DateTime? eveningStart = DayPlanDataSource.TimeToday(appSettings.sDayShutOffTime, _selectedDate);
            var blocks = DayPlanDataSource.BuildBlocks(_selectedDate)
                .Where(b => b.Kind == ScheduleBlockKind.Habit && !b.IsDone && !b.IsEarlier && !string.IsNullOrEmpty(b.HabitId)).ToList();
            var inside = DayFixPlan.HabitsInsideWorkingDay(blocks, b => new DateTimeOffset(b.Start),
                b => b.RoutineName == DayPlanText.EveningRoutineHabits,
                eveningStart.HasValue ? new DateTimeOffset(eveningStart.Value) : (DateTimeOffset?)null);
            var rows = inside.Select(b => new DayFixHabitRow
            {
                HabitId = b.HabitId,
                Title = b.Title,
                Seconds = b.DidOverflow ? (b.OverflowDurationSeconds ?? 0) : b.DurationSeconds,
                IsShortened = DayPlanHabitTrims.IsShortened(b.HabitId, _selectedDate),
            }).Where(r => r.Seconds > 0);
            return DayFixPlan.LongestFirst(rows, r => r.Seconds, r => r.Title);
        }

        /// <summary>A local, one-day trim of the slot the Day Plan keeps for a meeting — never a calendar write.</summary>
        private void ShortenMeeting(DayFixMeetingRow row, DateTime day, Window owner)
        {
            var untrimmed = DayPlanCalendarManager.Shared.MeetingsFor(day, includeDismissed: true, untrimmed: true).FirstOrDefault(m => m.Id == row.EventId);
            if (untrimmed == null) return;
            int actual = (int)(untrimmed.End - untrimmed.Start).TotalSeconds;
            string dayLabel = ForecastText.DayLabel(day);
            var dlg = new DayPlanShortenDialog(row.Title, dayLabel, actual, DayPlanMeetingTrims.OverrideSeconds(row.EventId, day), owner,
                hint: DayPlanText.Fmt(ForecastText.DayFix("dayFixMeetingShortenHintWindows"), dayLabel),
                noOptions: ForecastText.DayFix("dayFixMeetingShortenNoOptions"));
            appSettings.CapturePostHog("day-plan-event-shorten-opened");
            if (dlg.ShowDialog() != true) return;
            if (dlg.RestoreFullLength) DayPlanMeetingTrims.ClearOverride(row.EventId, day);
            else if (dlg.PickedSeconds.HasValue) DayPlanMeetingTrims.SetOverride(dlg.PickedSeconds.Value, row.EventId, day, actual);
            RefreshTasksAndAgenda();
        }
    }
}
