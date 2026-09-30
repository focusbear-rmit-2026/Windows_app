using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using HabitDurationText = FocusBear.Core.Schedule.HabitDurationText;
using HabitEditValues = FocusBear.Core.Schedule.HabitEditValues;
using RoutineSettingsSection = FocusBear.Core.Schedule.RoutineSettingsSection;
using RoutineTiming = FocusBear.Core.Schedule.RoutineTiming;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// The Day Plan's extra habit actions and routine options (report 2026-09-25_09-05-05: "add the same
    /// additional actions for day plan (edit, shorten) and ability to edit routine timing that we have on
    /// mac"). Ports of the Mac's action-bar Shorten / Edit columns and the routine header's "…" menu
    /// ("Edit routine timing"), from ScheduleViewController.swift.
    /// </summary>
    public partial class HabitsDayPlan
    {
        // A preview / future-day habit selected to reveal its Shorten + Edit bar (Mac selectedEditableHabitId).
        private string _selectedEditableHabitId;

        private Window OwnerWindow => Window.GetWindow(this) ?? Application.Current?.MainWindow;

        /// <summary>Whether this routine was force-cancelled for today (its card shows the Uncancel body).</summary>
        private bool IsRoutineCancelledToday(string name)
        {
            return IsViewingToday &&
                   ((name == DayPlanText.MorningRoutineHabits && _morningRoutineCancelled)
                    || (name == DayPlanText.EveningRoutineHabits && _eveningRoutineCancelled && !appSettings.DidCutOffTimePassed()));
        }

        /// <summary>A trimmed habit reads "(shortened)" in the list, like the Mac.</summary>
        private string TitleWithShortenedTag(ScheduleBlock block, string title)
        {
            return DayPlanHabitTrims.IsShortened(block.HabitId, _selectedDate)
                ? title + "  " + DayPlanText.HabitShortenedTag
                : title;
        }

        /// <summary>
        /// The time column for a habit row. An overflow habit has no slot in the day (it is parked
        /// zero-length after the habits that fit), so show how long it needs instead of "9:04–9:04".
        /// </summary>
        private static string HabitTimeText(ScheduleBlock block)
        {
            if (block.DidOverflow)
            {
                int seconds = block.OverflowDurationSeconds ?? block.DurationSeconds;
                if (seconds > 0) return HabitDurationText.Short(seconds);
            }
            return string.Format("{0}–{1}", TimeString(block.Start), TimeString(block.End));
        }

        /// <summary>Shorten is about the plan for the day being viewed — offered today and ahead, not on a past day.</summary>
        private bool CanShorten(string habitId) => !string.IsNullOrEmpty(habitId) && _selectedDate.Date >= DateTime.Today;

        /// <summary>The Shorten + Edit columns that follow Play / Done / Skip (or stand alone for a preview habit).</summary>
        private void AddHabitSettingsColumns(Panel bar, string habitId, bool afterLiveActions)
        {
            if (string.IsNullOrEmpty(habitId)) return;
            if (afterLiveActions)
            {
                // Edit/Shorten are about the habit itself rather than today's row: a hairline sets them apart.
                bar.Children.Add(new Border
                {
                    Width = 1,
                    Height = 34,
                    Background = new SolidColorBrush(WithAlpha(CGray, 0.4)),
                    Margin = new Thickness(0, 3, 28, 0),
                    VerticalAlignment = VerticalAlignment.Top
                });
            }
            Brush idle = new SolidColorBrush(WithAlpha(CGray, 0.3));
            if (CanShorten(habitId))
            {
                bool shortened = DayPlanHabitTrims.IsShortened(habitId, _selectedDate);
                var col = ActionColumn(MakeActionIcon("timer", FgBrush()),
                                       shortened ? DayPlanText.HabitShortened : DayPlanText.HabitShorten,
                                       shortened ? new SolidColorBrush(WithAlpha(COrange, 0.35)) : idle,
                                       () => OnShortenHabit(habitId));
                AutomationProperties.SetAutomationId(col.Children[0], "DayPlanShorten");
                bar.Children.Add(col);
            }
            var edit = ActionColumn(MakeActionIcon("edit", FgBrush()), DayPlanText.EditHabit, idle, () => OnEditHabit(habitId));
            AutomationProperties.SetAutomationId(edit.Children[0], "DayPlanEditHabit");
            bar.Children.Add(edit);
        }

        /// <summary>
        /// A preview habit (evening preview, a future day) selected for editing: the orange header with an
        /// Edit-only bar (plus Shorten) — there's nothing to Play/Done/Skip on a routine that isn't running.
        /// </summary>
        private Border BuildSelectedEditableUnit(ScheduleBlock block)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(COrange),
                Child = LayoutColumns(HabitTimeText(block), TitleWithShortenedTag(block, block.Title), Brushes.White, Brushes.White, vPad: 8)
            };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 14, 0, 10) };
            AddHabitSettingsColumns(bar, block.HabitId, afterLiveActions: false);
            var unit = new StackPanel { Orientation = Orientation.Vertical };
            unit.Children.Add(header);
            unit.Children.Add(bar);
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(COrange),
                Background = (Application.Current.TryFindResource("BackgroundColor") as Brush) ?? Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(0, 2, 0, 4),
                Child = unit
            };
        }

        // MARK: - Shorten

        private bool TryFindAnyHabit(string id, out habit found)
        {
            foreach (var list in new[] { appSettings.arrAllMorningHabits, appSettings.arrGLOBALAllMorningHabits,
                                         appSettings.arrAllEveningHabits, appSettings.arrGLOBALAllEveningHabits })
            {
                if (list == null) continue;
                foreach (habit h in list)
                {
                    if (h.id == id) { found = h; return true; }
                }
            }
            found = default(habit);
            return false;
        }

        private void OnShortenHabit(string habitId)
        {
            try
            {
                // Only its own rule, "Shortening habits on the Day Plan" (off by default): tying it to the change-settings
                // rules too made Shorten ask after cutoff for someone who never turned this on (report 09-05-05 rework).
                // 2-minute unlock grace as elsewhere.
                var settings = AppSettings.GetInstance;
                bool needsPassword = settings.isPasswordSet && settings.bPasswordRequireToShortenHabits
                    && (DateTime.Now - settings.SettingsUnlockedAt).TotalSeconds >= 120;
                if (needsPassword)
                {
                    var pwd = new Focus_Bear.Preferences.AskPassword { Owner = OwnerWindow };
                    if (pwd.ShowDialog() != true) return;
                    settings.SettingsUnlockedAt = DateTime.Now;
                }
                if (!TryFindAnyHabit(habitId, out habit model))
                {
                    new FBMessageBox(DayPlanText.HabitEditUnavailable) { Owner = OwnerWindow }.ShowDialog();
                    return;
                }
                int actual = DayPlanDataSource.EstimatedDuration(model);
                DateTime day = _selectedDate.Date;
                string dayLabel = day.ToString(day.Year == DateTime.Today.Year ? "ddd d MMM" : "ddd d MMM yyyy", CultureInfo.CurrentCulture);
                appSettings.CapturePostHog("day-plan-habit-shorten-opened");
                var dlg = new DayPlanShortenDialog(model.name, dayLabel, actual, DayPlanHabitTrims.OverrideSeconds(habitId, day), OwnerWindow);
                if (dlg.ShowDialog() != true) return;
                if (dlg.RestoreFullLength)
                {
                    DayPlanHabitTrims.ClearOverride(habitId, day);
                    appSettings.CapturePostHog("day-plan-habit-shorten-cleared");
                }
                else if (dlg.PickedSeconds.HasValue)
                {
                    int? applied = DayPlanHabitTrims.SetOverride(dlg.PickedSeconds.Value, habitId, day, actual);
                    appSettings.CapturePostHog(applied.HasValue ? "day-plan-habit-shortened" : "day-plan-habit-shorten-cleared");
                }
                RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan OnShortenHabit ex - " + ex.Message); }
        }

        // MARK: - Edit habit

        private async void OnEditHabit(string habitId)
        {
            try
            {
                if (!DayPlanSettingsEdits.TryReadHabit(habitId, out RoutineSettingsSection section, out string icon, out HabitEditValues values))
                {
                    new FBMessageBox(DayPlanText.HabitEditUnavailable) { Owner = OwnerWindow }.ShowDialog();
                    return;
                }
                if (!DayPlanSettingsEdits.Authorize(OwnerWindow)) return;
                appSettings.CapturePostHog("day-plan-habit-edit-opened");
                // The habit that is playing can't be deleted — the routine player is indexing into it (Mac canDelete).
                bool canDelete = appSettings.currentHabitPlaying.id != habitId;
                var dlg = new DayPlanHabitEditDialog(section, icon, values, OwnerWindow, DayPlanSettingsEdits.FocusModes(), canDelete);
                dlg.ShowDialog();
                if (dlg.Outcome == HabitEditOutcome.OpenFullEditor)
                {
                    try { ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.EditHabits); }
                    catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan open Edit habits ex - " + ex.Message); }
                    return;
                }
                if (dlg.Outcome != HabitEditOutcome.Saved && dlg.Outcome != HabitEditOutcome.Deleted) return;
                bool deleting = dlg.Outcome == HabitEditOutcome.Deleted;
                SetBusy(true);
                bool saved = deleting
                    ? await DayPlanSettingsEdits.DeleteHabitAsync(habitId)
                    : await DayPlanSettingsEdits.SaveHabitAsync(habitId, dlg.HabitIcon, dlg.Values);
                SetBusy(false);
                if (saved && deleting)
                {
                    appSettings.CapturePostHog("day-plan-habit-deleted");
                    _selectedHabitIndex = null;   // the selected row no longer exists
                    _selectedEditableHabitId = null;
                }
                else if (saved) appSettings.CapturePostHog("day-plan-habit-edited");
                else new FBMessageBox(DayPlanText.HabitSaveFailed) { Owner = OwnerWindow }.ShowDialog();
                RenderDayPlan();
            }
            catch (Exception ex)
            {
                SetBusy(false);
                LogWriter.LogWrite("HabitsDayPlan OnEditHabit ex - " + ex.Message);
            }
        }

        // MARK: - Routine options ("…" on the routine header)

        /// <summary>Which settings routine a Day Plan routine card is, or null for one Windows can't edit.</summary>
        private static RoutineSettingsSection? SectionForRoutine(string routineName)
        {
            if (routineName == DayPlanText.MorningRoutineHabits) return RoutineSettingsSection.Morning;
            if (routineName == DayPlanText.EveningRoutineHabits) return RoutineSettingsSection.Evening;
            return null;
        }

        /// <summary>The routine header's "…" button, opening the routine options menu.</summary>
        private FrameworkElement BuildRoutineMenuButton(RoutineSettingsSection section)
        {
            var dots = new TextBlock
            {
                Text = "•••",
                FontSize = 11,
                Foreground = SecBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var circle = new Border
            {
                Width = 26,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(WithAlpha(CGray, 0.6)),
                BorderThickness = new Thickness(1),
                Child = dots,
            };
            var btn = new Button
            {
                Content = circle,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanText.RoutineMoreActions,
            };
            AutomationProperties.SetAutomationId(btn, section == RoutineSettingsSection.Morning ? "DayPlanRoutineMenuMorning" : "DayPlanRoutineMenuEvening");
            AutomationProperties.SetName(btn, DayPlanText.RoutineMoreActions);

            var menu = new ContextMenu { PlacementTarget = btn, Placement = PlacementMode.Bottom };
            var timing = new MenuItem { Header = DayPlanText.EditRoutineTiming };
            AutomationProperties.SetAutomationId(timing, "DayPlanEditRoutineTiming");
            timing.Click += (s, e) => OnEditRoutineTiming(section);
            menu.Items.Add(timing);
            btn.ContextMenu = menu;
            btn.Click += (s, e) => { menu.IsOpen = true; };
            // The header toggles its accordion on mouse-up; the menu button must not.
            btn.MouseLeftButtonUp += (s, e) => e.Handled = true;
            return btn;
        }

        private async void OnEditRoutineTiming(RoutineSettingsSection section)
        {
            try
            {
                if (!DayPlanSettingsEdits.Authorize(OwnerWindow)) return;
                var root = DayPlanSettingsEdits.CurrentSettings();
                if (root == null)
                {
                    new FBMessageBox(DayPlanText.HabitEditUnavailable) { Owner = OwnerWindow }.ShowDialog();
                    return;
                }
                bool evening = section == RoutineSettingsSection.Evening;
                int? start = RoutineTiming.StartMinutes(root, section);
                int? cutoff = RoutineTiming.EveningCutoffMinutes(root);
                int? morning = RoutineTiming.StartMinutes(root, RoutineSettingsSection.Morning);
                var dlg = new DayPlanRoutineTimingDialog(
                    evening ? DayPlanText.RoutineNameEvening : DayPlanText.RoutineNameMorning,
                    start ?? (evening ? 21 * 60 : 7 * 60),
                    showsCutoff: evening,
                    cutoffMinutes: evening ? cutoff : null,
                    morningStartMinutes: morning,
                    owner: OwnerWindow);
                if (dlg.ShowDialog() != true) return;
                SetBusy(true);
                bool saved = await DayPlanSettingsEdits.SaveRoutineTimingAsync(section, dlg.Minutes, evening ? dlg.CutoffMinutes : null);
                SetBusy(false);
                if (saved) appSettings.CapturePostHog("day-plan-routine-timing-edited");
                else new FBMessageBox(DayPlanText.HabitSaveFailed) { Owner = OwnerWindow }.ShowDialog();
                RenderDayPlan();
            }
            catch (Exception ex)
            {
                SetBusy(false);
                LogWriter.LogWrite("HabitsDayPlan OnEditRoutineTiming ex - " + ex.Message);
            }
        }
    }
}
