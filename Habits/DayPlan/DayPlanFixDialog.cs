using FocusBear.Core.Schedule;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>One meeting row in the Fix modal.</summary>
    public sealed class DayFixMeetingRow
    {
        public string EventId;
        public string Title;
        public string TimeLabel;
        public int Seconds;
        public bool IsSkipped;
        public bool IsShortened;
    }

    /// <summary>One habit row in the Fix modal.</summary>
    public sealed class DayFixHabitRow
    {
        public string HabitId;
        public string Title;
        public int Seconds;
        public bool IsShortened;
    }

    /// <summary>
    /// "Make today fit" — the Mac's <c>DayFixView</c>, opened from the "Fix" beside the Day Plan's red
    /// "Not enough time for all tasks" (report 2026-09-25_14-33-34: "need the day plan to have the warning
    /// if there's not enough buffer time and the fix button with same UX as mac"). Routes are offered
    /// biggest first and only when they could free something (Core <see cref="DayFixPlan.Options"/>);
    /// lists inside them are longest first.
    /// </summary>
    public sealed class DayPlanFixDialog : Window
    {
        private readonly StackPanel _body = new StackPanel();
        private readonly string _dayLabel;
        private readonly bool _isToday;
        private readonly Func<double> _overMinutes;
        private readonly Func<IList<DayFixOption>> _options;
        private readonly Func<IList<DayFixMeetingRow>> _meetings;
        private readonly Func<IList<DayFixHabitRow>> _habits;
        private readonly Action<DayFixMeetingRow> _onSkipMeeting;
        private readonly Action<DayFixMeetingRow> _onShortenMeeting;
        private readonly Action<DayFixHabitRow> _onShortenHabit;
        private readonly Action _onPostponeTasks;
        private DayFixRoute? _open;

        public DayPlanFixDialog(string dayLabel, bool isToday, Func<double> overMinutes, Func<IList<DayFixOption>> options,
                                Func<IList<DayFixMeetingRow>> meetings, Func<IList<DayFixHabitRow>> habits,
                                Action<DayFixMeetingRow> onSkipMeeting, Action<DayFixMeetingRow> onShortenMeeting,
                                Action<DayFixHabitRow> onShortenHabit, Action onPostponeTasks, Window owner)
        {
            Owner = owner;
            _dayLabel = dayLabel;
            Focus_Bear.Utils.WindowTheme.FollowTheme(this);
            _isToday = isToday;
            _overMinutes = overMinutes;
            _options = options;
            _meetings = meetings;
            _habits = habits;
            _onSkipMeeting = onSkipMeeting;
            _onShortenMeeting = onShortenMeeting;
            _onShortenHabit = onShortenHabit;
            _onPostponeTasks = onPostponeTasks;
            var root = new StackPanel();
            root.Children.Add(_body);
            DayPlanDialogUi.Frame(this, "DayPlanFixDialog", root, 440);
            Render();
        }

        /// <summary>Redraw after a row's action changed the day (the host re-plans, then calls this).</summary>
        public void Render()
        {
            _body.Children.Clear();
            _body.Children.Add(DayPlanDialogUi.Heading(_isToday ? ForecastText.DayFix("dayFixTitle")
                : ForecastText.Format(ForecastText.DayFix("dayFixTitleDayWindows"), "<DAY>", _dayLabel)));
            double over = _overMinutes();
            string gap = _isToday
                ? ForecastText.Format(ForecastText.DayFix("dayFixGap"), "<DURATION>", ForecastText.Duration(over))
                : ForecastText.Format(ForecastText.DayFix("dayFixGapDayWindows"), "<DAY>", _dayLabel, "<DURATION>", ForecastText.Duration(over));
            var gapText = new TextBlock { Text = over > 0 ? gap : "", FontSize = 13, TextWrapping = TextWrapping.Wrap,
                                          Foreground = new SolidColorBrush(DayPlanDialogUi.Red), Margin = new Thickness(0, 0, 0, 10) };
            AutomationProperties.SetAutomationId(gapText, "DayFixGap");
            _body.Children.Add(gapText);

            IList<DayFixOption> options = _options();
            if (options.Count == 0)
            {
                _body.Children.Add(DayPlanDialogUi.Hint(ForecastText.DayFix("dayFixNothingToFree"), 0));
            }
            else if (_open == DayFixRoute.Meetings) MeetingList();
            else if (_open == DayFixRoute.Routine) HabitList();
            else foreach (DayFixOption option in options) _body.Children.Add(RouteButton(option));

            var buttons = new List<Button>();
            if (_open.HasValue) buttons.Add(DayPlanDialogUi.Secondary(ForecastText.DayFix("dayFixBack"), "DayFixBack", () => { _open = null; Render(); }));
            buttons.Add(DayPlanDialogUi.Primary(ForecastText.DayFix("dayFixDone"), "DayFixDone", () => DialogResult = true));
            var row = DayPlanDialogUi.ButtonRow(buttons.ToArray());
            _body.Children.Add(row);
        }

        private FrameworkElement RouteButton(DayFixOption option)
        {
            string title, detail;
            switch (option.Route)
            {
                case DayFixRoute.Meetings:
                    title = ForecastText.DayFix("dayFixMeetingsOptionWindows"); detail = ForecastText.DayFix("dayFixMeetingsDetail"); break;
                case DayFixRoute.Routine:
                    title = ForecastText.DayFix("dayFixRoutineOption"); detail = ForecastText.DayFix("dayFixRoutineDetail"); break;
                default:
                    title = ForecastText.DayFix("dayFixTasksOption"); detail = ForecastText.DayFix("dayFixTasksDetail"); break;
            }
            var text = new StackPanel();
            var t = new TextBlock { Text = title, FontSize = 13.5, FontWeight = FontWeights.SemiBold };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            text.Children.Add(t);
            var d = DayPlanDialogUi.Hint(ForecastText.Format(detail, "<DURATION>", ForecastText.Duration(option.FreeableMinutes)), 1);
            d.FontSize = 11.5;
            text.Children.Add(d);
            var chevron = new TextBlock { Text = "›", FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
            chevron.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var dock = new DockPanel();
            DockPanel.SetDock(chevron, Dock.Right);
            dock.Children.Add(chevron);
            dock.Children.Add(text);
            var card = new Border { Child = dock, Padding = new Thickness(12, 9, 12, 9), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                                    Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            // A button around the card: Tab reaches it, Enter / Space open it, and a screen reader reads its title
            // (a bare Border was mouse-only). A button also takes the mouse press, so a click never starts a window drag.
            var button = new Button
            {
                Content = card,
                Template = Focus_Bear.StatsScreens.StatsUi.PlainTemplate(),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetAutomationId(button, "DayFixRoute_" + option.Route);
            AutomationProperties.SetName(button, title);
            AutomationProperties.SetHelpText(button, d.Text);
            DayFixRoute route = option.Route;
            button.Click += (s, e) =>
            {
                if (route == DayFixRoute.Tasks) { DialogResult = true; _onPostponeTasks(); return; }
                _open = route;
                Render();
            };
            return button;
        }

        private void MeetingList()
        {
            var list = new StackPanel();
            foreach (DayFixMeetingRow m in _meetings())
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var shorten = DayPlanDialogUi.Secondary(ForecastText.DayFix("dayFixShorten"), "DayFixShortenMeeting", () => { _onShortenMeeting(m); Render(); });
                shorten.Margin = new Thickness(6, 0, 0, 0);
                shorten.MinWidth = 0;
                shorten.IsEnabled = !m.IsSkipped;
                DockPanel.SetDock(shorten, Dock.Right);
                row.Children.Add(shorten);
                var skip = DayPlanDialogUi.Secondary(m.IsSkipped ? ForecastText.DayFix("dayFixUnskip") : ForecastText.DayFix("dayFixSkip"), "DayFixSkipMeeting",
                                                     () => { _onSkipMeeting(m); Render(); });
                skip.Margin = new Thickness(6, 0, 0, 0);
                skip.MinWidth = 0;
                DockPanel.SetDock(skip, Dock.Right);
                row.Children.Add(skip);
                row.Children.Add(RowText(m.Title, m.TimeLabel + " · " + ForecastText.Duration(m.Seconds / 60.0), m.IsSkipped, m.IsShortened));
                list.Children.Add(row);
            }
            _body.Children.Add(Scroll(list));
        }

        private void HabitList()
        {
            var list = new StackPanel();
            foreach (DayFixHabitRow h in _habits())
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var shorten = DayPlanDialogUi.Secondary(ForecastText.DayFix("dayFixShorten"), "DayFixShortenHabit", () => { _onShortenHabit(h); Render(); });
                shorten.Margin = new Thickness(6, 0, 0, 0);
                shorten.MinWidth = 0;
                DockPanel.SetDock(shorten, Dock.Right);
                row.Children.Add(shorten);
                row.Children.Add(RowText(h.Title, ForecastText.Duration(h.Seconds / 60.0), false, h.IsShortened));
                list.Children.Add(row);
            }
            _body.Children.Add(Scroll(list));
        }

        private static FrameworkElement RowText(string title, string detail, bool skipped, bool shortened)
        {
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = new Emoji.Wpf.TextBlock { Text = title, FontSize = 13, TextWrapping = TextWrapping.Wrap,
                                              TextDecorations = skipped ? TextDecorations.Strikethrough : null };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            panel.Children.Add(t);
            var d = DayPlanDialogUi.Hint(detail, 1);
            d.FontSize = 11.5;
            if (shortened) d.Foreground = new SolidColorBrush(DayPlanDialogUi.Orange);
            panel.Children.Add(d);
            return panel;
        }

        private static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer { Content = content, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                      Padding = new Thickness(0, 0, 6, 0) };
        }
    }
}
