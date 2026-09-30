using Focus_Bear.StatsScreens;
using FocusBear.Core.ToDo;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The Mac's <c>ToDoDueDatePopover</c> (web <c>DueDateSnoozePopover</c>): free text ("2d", "30th", "fri")
    /// with a live preview, or a preset — Today · Tomorrow · Next week, and Clear when there is a date. The
    /// project cards add a month calendar under the presets. Esc closes it; Enter saves what is typed, or the
    /// calendar's highlighted day.
    /// </summary>
    internal static class ToDoDueDatePopup
    {
        /// <param name="idPrefix">Automation ids: txt{prefix}Input, txt{prefix}Preview, btn{prefix}Save, cal{prefix}.</param>
        internal static Popup Show(FrameworkElement anchor, ToDoStrings strings, string currentDue, Action<DateTime?> onCommit,
                                   string idPrefix, bool withCalendar)
        {
            var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
            var stack = new StackPanel { Width = 250 };
            var heading = StatsUi.Text(strings.EditDueDate, 12, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(heading);
            var input = new TextBox();
            AutomationProperties.SetAutomationId(input, "txt" + idPrefix + "Input");
            AutomationProperties.SetName(input, strings.EditDueDate);
            input.ToolTip = strings.DueDateGrammarHelp;
            stack.Children.Add(Field(input, strings.DueDatePlaceholder));
            var preview = StatsUi.Text("", 11, secondary: true);
            preview.Margin = new Thickness(2, 6, 0, 0);
            AutomationProperties.SetAutomationId(preview, "txt" + idPrefix + "Preview");
            stack.Children.Add(preview);
            var presets = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            stack.Children.Add(presets);
            var save = SmallButton(strings.Save, null, primary: true);
            save.HorizontalAlignment = HorizontalAlignment.Right;
            save.Margin = new Thickness(0, 8, 0, 0);
            AutomationProperties.SetAutomationId(save, "btn" + idPrefix + "Save");
            stack.Children.Add(save);

            bool committed = false;
            Action<DateTime?> commit = day =>
            {
                if (committed) return;
                committed = true;
                popup.IsOpen = false;
                onCommit(day);
            };
            DateTime now = DateTime.Now;
            foreach (var option in ProjectDueDateEdit.QuickOptions(currentDue))
            {
                var day = ProjectDueDateEdit.DateFor(option, now);
                var button = SmallButton(Label(strings, option), (s, e) => commit(day));
                button.Margin = new Thickness(0, 0, 6, 6);
                AutomationProperties.SetAutomationId(button, "btn" + idPrefix + option);
                presets.Children.Add(button);
            }

            Calendar calendar = null;
            if (withCalendar)
            {
                calendar = ThemedCalendar.Create();
                calendar.SelectionMode = CalendarSelectionMode.SingleDate;
                calendar.DisplayDate = ProjectDueDateEdit.CalendarStart(currentDue, now);
                calendar.SelectedDate = ToDoDates.Parse(currentDue)?.Date;
                calendar.IsTodayHighlighted = true;
                calendar.HorizontalAlignment = HorizontalAlignment.Center;
                calendar.Margin = new Thickness(0, 2, 0, 0);
                AutomationProperties.SetAutomationId(calendar, "cal" + idPrefix);
                // A click on a day picks it. The Calendar selects on mouse-down and captures the mouse, so
                // the pick waits for the release and hands the capture back before the popup goes.
                calendar.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    var dayButton = Ancestor<CalendarDayButton>(e.OriginalSource as DependencyObject, calendar);
                    if (dayButton == null || !(dayButton.DataContext is DateTime picked)) return;
                    calendar.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (Mouse.Captured != null) Mouse.Capture(null);
                        commit(picked.Date);
                    }));
                };
                calendar.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key != Key.Enter) return;
                    e.Handled = true;
                    commit((calendar.SelectedDate ?? calendar.DisplayDate).Date);
                };
                stack.Children.Insert(stack.Children.IndexOf(save), calendar);
            }

            Func<DateTime?> parsed = () => ToDoDueDateParser.Parse(input.Text, DateTime.Now, ToDoStrings.Culture);
            Action sync = () =>
            {
                bool typing = input.Text.Trim().Length > 0;
                var day = parsed();
                preview.Visibility = typing ? Visibility.Visible : Visibility.Collapsed;
                preview.Text = day.HasValue ? strings.DueLabel(ToDoDates.Ymd(day.Value), DateTime.Now) : strings.UnrecognisedDate;
                if (day.HasValue) preview.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
                else preview.Foreground = Overdue;
                presets.Visibility = typing ? Visibility.Collapsed : Visibility.Visible;
                save.Visibility = typing ? Visibility.Visible : Visibility.Collapsed;
                save.IsEnabled = day.HasValue;
                // What is typed shows on the calendar too, so the two never disagree.
                if (calendar != null && day.HasValue)
                {
                    calendar.SelectedDate = day.Value.Date;
                    calendar.DisplayDate = day.Value.Date;
                }
            };
            input.TextChanged += (s, e) => sync();
            input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { var day = parsed(); if (day.HasValue) commit(day.Value.Date); e.Handled = true; }
            };
            save.Click += (s, e) => { var day = parsed(); if (day.HasValue) commit(day.Value.Date); };
            sync();

            var card = new Border { Child = stack, Padding = new Thickness(12), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 8, 8) };
            card.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.25 };
            KeyboardNavigation.SetTabNavigation(card, KeyboardNavigationMode.Cycle);
            // Esc closes from anywhere in the popup, the calendar included.
            card.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                popup.IsOpen = false;
                (anchor as IInputElement)?.Focus();
            };
            popup.Child = card;
            popup.Opened += (s, e) => input.Focus();
            popup.IsOpen = true;
            return popup;
        }

        private static string Label(ToDoStrings strings, DueDateQuickOption option)
        {
            switch (option)
            {
                case DueDateQuickOption.Today: return strings.Today;
                case DueDateQuickOption.Tomorrow: return strings.Tomorrow;
                case DueDateQuickOption.NextWeek: return strings.NextWeek;
                default: return strings.Clear;
            }
        }

        private static T Ancestor<T>(DependencyObject source, DependencyObject stopAt) where T : DependencyObject
        {
            for (var p = source; p != null && p != stopAt; p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
                if (p is T found) return found;
            return null;
        }
    }
}
