using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// The Day Plan's top-left "Schedule settings" cog — the Windows port of the macOS
    /// <c>scheduleCog</c> / <c>openScheduleSettings</c> (report 2026-09-25_20-15-02). Level with the date
    /// line, it opens a small popover with the Mac's four switches:
    /// <list type="bullet">
    /// <item>Show events from earlier today — today only, and only when a meeting already finished
    /// (not persisted, like the Mac).</item>
    /// <item>Hide deep work blocks — the sprints and their breaks aren't drawn (persisted).</item>
    /// <item>Hide habits — the routine cards aren't drawn, leaving just the calendar (persisted).</item>
    /// <item>Show dismissed events — meetings hidden with "Hide" come back faded (persisted; this used to
    /// sit in the Tasks panel's cog on Windows only).</item>
    /// </list>
    /// </summary>
    public partial class HabitsDayPlan
    {
        private bool _showEarlierMeetings;
        /// <summary>Whether today has a meeting that already ended — the earlier-events switch means nothing otherwise.</summary>
        private bool _canShowEarlierMeetings;
        private bool _hideDeepWorkBlocks = RegistryManager.LoadDayPlanHideDeepWork();
        private bool _hideHabitBlocks = RegistryManager.LoadDayPlanHideHabits();
        private Button _scheduleCog;

        /// <summary>The finished-today meetings that "Show events from earlier today" would add back.</summary>
        private bool TodayHasEarlierMeetings()
        {
            if (!IsViewingToday) return false;
            DateTime now = DateTime.Now;
            return DayPlanCalendarManager.Shared.MeetingsFor(_selectedDate, _showDismissedMeetings)
                                         .Any(m => m.End <= now);
        }

        /// <summary>Recompute the earlier-events switch's availability before a render (Mac canShowEarlierToggle).</summary>
        private bool ShowEarlierMeetingsForRender()
        {
            try { _canShowEarlierMeetings = TodayHasEarlierMeetings(); }
            catch (Exception ex) { LogWriter.LogWrite("ShowEarlierMeetingsForRender - " + ex.Message); _canShowEarlierMeetings = false; }
            if (!_canShowEarlierMeetings) _showEarlierMeetings = false;
            return _showEarlierMeetings;
        }

        /// <summary>The date bar with the settings cog pinned top-left, level with the date line.</summary>
        private UIElement WithScheduleCog(UIElement dateBar)
        {
            var grid = new Grid();
            grid.Children.Add(dateBar);
            // One cog for the page's life, moved onto each rebuilt date bar: an open settings popover is
            // anchored to it, and each switch re-renders the agenda while the popover stays open.
            if (_scheduleCog == null) _scheduleCog = BuildScheduleCog();
            (_scheduleCog.Parent as Panel)?.Children.Remove(_scheduleCog);
            grid.Children.Add(_scheduleCog);
            return grid;
        }

        private Button BuildScheduleCog()
        {
            var glyph = new TextBlock
            {
                Text = "⚙",
                FontSize = 17,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var chrome = new Border
            {
                Child = glyph,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 3),
                Background = Brushes.Transparent
            };
            var cog = new Button
            {
                Content = chrome,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanComposerText.ScheduleSettings,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0)
            };
            // Only a soft rounded rectangle on hover, like the Tasks panel's cog.
            cog.MouseEnter += (s, e) => chrome.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));
            cog.MouseLeave += (s, e) => chrome.Background = Brushes.Transparent;
            System.Windows.Automation.AutomationProperties.SetAutomationId(cog, "DayPlanScheduleSettings");
            System.Windows.Automation.AutomationProperties.SetName(cog, DayPlanComposerText.ScheduleSettings);
            cog.Click += (s, e) => OpenScheduleSettings(cog);
            return cog;
        }

        private void OpenScheduleSettings(Button cog)
        {
            try
            {
                var form = new StackPanel { Orientation = Orientation.Vertical };
                var heading = new TextBlock
                {
                    Text = DayPlanComposerText.ScheduleSettings,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                form.Children.Add(heading);

                ShowEarlierMeetingsForRender();
                form.Children.Add(SettingsCheck(DayPlanComposerText.ShowEarlierEvents, "DayPlanShowEarlierEvents",
                    _showEarlierMeetings, _canShowEarlierMeetings, on =>
                    {
                        _showEarlierMeetings = on;
                        appSettings.CapturePostHog("day-plan-show-earlier-events-toggled");
                    }));
                form.Children.Add(SettingsCheck(DayPlanComposerText.HideDeepWork, "DayPlanHideDeepWork",
                    _hideDeepWorkBlocks, true, on =>
                    {
                        _hideDeepWorkBlocks = on;
                        RegistryManager.SaveDayPlanHideDeepWork(on);
                        appSettings.CapturePostHog("day-plan-hide-deep-work-toggled");
                    }));
                form.Children.Add(SettingsCheck(DayPlanComposerText.HideHabitBlocks, "DayPlanHideHabits",
                    _hideHabitBlocks, true, on =>
                    {
                        _hideHabitBlocks = on;
                        RegistryManager.SaveDayPlanHideHabits(on);
                        appSettings.CapturePostHog("day-plan-hide-habits-toggled");
                    }));
                form.Children.Add(SettingsCheck(DayPlanComposerText.ShowDismissedEvents, "DayPlanShowDismissedEvents",
                    _showDismissedMeetings, true, on =>
                    {
                        _showDismissedMeetings = on;
                        RegistryManager.SaveDayPlanShowHiddenMeetings(on);
                    }));

                var card = new Border
                {
                    Child = form,
                    Padding = new Thickness(14, 12, 14, 12),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 4, 14, 14),   // room for the shadow
                    Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.28, Direction = 270 }
                };
                card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                System.Windows.Automation.AutomationProperties.SetAutomationId(card, "DayPlanScheduleSettingsPopover");

                // Transient, like the Mac's NSPopover: a click anywhere else closes it.
                var popup = new Popup
                {
                    Child = card,
                    PlacementTarget = cog,
                    Placement = PlacementMode.Bottom,
                    AllowsTransparency = true,
                    StaysOpen = false
                };
                popup.IsOpen = true;
                appSettings.CapturePostHog("day-plan-schedule-settings-opened");
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan OpenScheduleSettings - " + ex.Message); }
        }

        /// <summary>One switch in the popover. Toggling applies at once and re-renders the agenda.</summary>
        private CheckBox SettingsCheck(string label, string automationId, bool isChecked, bool enabled, Action<bool> apply)
        {
            var text = new TextBlock { Text = label, FontSize = 13, TextWrapping = TextWrapping.Wrap };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var check = new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                IsEnabled = enabled,
                Opacity = enabled ? 1.0 : 0.45,
                Margin = new Thickness(0, 8, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(check, automationId);
            RoutedEventHandler changed = (s, e) =>
            {
                try
                {
                    apply(check.IsChecked == true);
                    RenderDayPlan();
                }
                catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan schedule setting - " + ex.Message); }
            };
            check.Checked += changed;
            check.Unchecked += changed;
            return check;
        }

        /// <summary>Deep-work sprints and their paired breaks are skipped while "Hide deep work blocks" is on.</summary>
        private bool IsHiddenByScheduleSettings(ScheduleBlock block)
        {
            if (block == null) return false;
            if (_hideDeepWorkBlocks && (block.Kind == ScheduleBlockKind.DeepWork || block.Kind == ScheduleBlockKind.BreakSprint)) return true;
            return false;
        }
    }
}
