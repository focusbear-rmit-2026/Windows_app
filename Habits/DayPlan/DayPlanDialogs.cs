using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
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

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Building blocks for the Day Plan's small code-built dialogs (Shorten, Edit habit, Edit routine
    /// timing), in the house style of the other code-built screens: theme brushes by DynamicResource,
    /// the app's orange accent, and explicit black text on light buttons so they read in both themes.
    /// </summary>
    internal static class DayPlanDialogUi
    {
        internal static readonly Color Orange = Color.FromRgb(0xFF, 0x95, 0x00);
        internal static readonly Color Red = Color.FromRgb(0xFF, 0x3B, 0x30);
        internal static readonly Color Gray = Color.FromRgb(0x8E, 0x8E, 0x93);

        /// <summary>A borderless, owner-centred dialog with the app's orange hairline, holding <paramref name="body"/>.</summary>
        /// <summary>True when <paramref name="source"/> sits in a control or hand-cursor card (below <paramref name="root"/>).</summary>
        private static bool IsOverClickable(DependencyObject source, DependencyObject root)
        {
            for (DependencyObject d = source; d != null && d != root;
                 d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                if (d is ButtonBase || d is TextBoxBase || d is Selector || d is Thumb || d is ScrollBar) return true;
                if (d is FrameworkElement fe && fe.Cursor == Cursors.Hand) return true;
            }
            return false;
        }

        public static void Frame(Window w, string automationId, UIElement body, double width)
        {
            w.Title = "Focus Bear";
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            w.SizeToContent = SizeToContent.Height;
            w.Width = width;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = w.Owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            w.SetResourceReference(Control.BackgroundProperty, "BackgroundColor");
            w.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(w, automationId);
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Orange),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22, 18, 22, 18),
                Child = body
            };
            border.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
            // Borderless, so let the whole dialog be dragged by its background — but not from a clickable card:
            // DragMove captures the mouse, so the card never got its MouseLeftButtonUp and clicking the Fix
            // window's options did nothing (report 2026-09-25_21-21-30).
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState != MouseButtonState.Pressed || IsOverClickable(e.OriginalSource as DependencyObject, border)) return;
                try { w.DragMove(); } catch { }
            };
            w.Content = border;
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; w.DialogResult = false; }
            };
        }

        internal static TextBlock Heading(string text)
        {
            var t = new Emoji.Wpf.TextBlock { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            return t;
        }

        internal static TextBlock Label(string text, double top = 10)
        {
            var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 4) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            return t;
        }

        internal static TextBlock Hint(string text, double top = 6)
        {
            // Theme secondary text (TraySubtitleColor), never a hard-coded grey, so it tracks the dark theme's contrast.
            var t = new Emoji.Wpf.TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            return t;
        }

        internal static TextBlock Error()
        {
            return new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Red), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        }

        internal static void ShowError(TextBlock error, string message)
        {
            error.Text = message ?? "";
            error.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static TextBox Field(string automationId, string text = "", double width = double.NaN)
        {
            var box = new TextBox
            {
                Text = text ?? "",
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Width = width,
                HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            // Theme card colours, so the field reads in both themes.
            box.SetResourceReference(Control.BackgroundProperty, "TrayCardBg");
            box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            box.SetResourceReference(Control.BorderBrushProperty, "TraySubtitleColor");
            box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(box, automationId);
            box.GotKeyboardFocus += (s, e) => box.SelectAll();
            return box;
        }

        private static ControlTemplate RoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(ButtonBase));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            return template;
        }

        /// <summary>The filled orange action (white text).</summary>
        internal static Button Primary(string text, string automationId, Action onClick)
        {
            return MakeButton(text, automationId, onClick, new SolidColorBrush(Orange), Brushes.White, new SolidColorBrush(Orange));
        }

        /// <summary>A light outlined button — explicit black text so it reads on the dark theme too.</summary>
        internal static Button Secondary(string text, string automationId, Action onClick, bool highlighted = false)
        {
            Brush fill = highlighted ? new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xD3)) : new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5));
            Brush edge = highlighted ? new SolidColorBrush(Orange) : new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD8));
            return MakeButton(text, automationId, onClick, fill, Brushes.Black, edge);
        }

        private static Button MakeButton(string text, string automationId, Action onClick, Brush fill, Brush fg, Brush edge)
        {
            var b = new Button
            {
                Content = new TextBlock { Text = text, FontSize = 13.5, Foreground = fg, TextWrapping = TextWrapping.NoWrap },
                Background = fill,
                BorderBrush = edge,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 7, 14, 7),
                MinWidth = 84,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Template = RoundedButtonTemplate(),
                FocusVisualStyle = null,
            };
            AutomationProperties.SetAutomationId(b, automationId);
            AutomationProperties.SetName(b, text);
            b.Click += (s, e) => onClick();
            return b;
        }

        /// <summary>A right-aligned row of buttons (Cancel … Save).</summary>
        internal static StackPanel ButtonRow(params Button[] buttons)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, -8, 0) };
            foreach (var b in buttons) row.Children.Add(b);
            return row;
        }

        internal static CheckBox Check(string text, string automationId, bool isChecked)
        {
            var c = new CheckBox { IsChecked = isChecked, Margin = new Thickness(0, 10, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = text, FontSize = 13, Margin = new Thickness(4, 0, 0, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            c.Content = label;
            AutomationProperties.SetAutomationId(c, automationId);
            return c;
        }

        /// <summary>"7:30 am" — how the timing field shows a time (the same shape the Day Plan rows use).</summary>
        internal static string TimeText(int minutes)
        {
            return DateTime.Today.AddMinutes(minutes).ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
        }
    }

    /// <summary>
    /// "Shorten just for this day": pick a shorter length for the habit on the day being viewed, type
    /// one, or put it back to full length. The Mac's DayPlanShortenHabitView; the rules (which presets,
    /// what a typed length means) are FocusBear.Core's <see cref="DayPlanHabitDurations"/>.
    /// </summary>
    public sealed class DayPlanShortenDialog : Window
    {
        /// <summary>Seconds picked, when the user chose a length.</summary>
        public int? PickedSeconds { get; private set; }
        /// <summary>True when the user chose "Full length".</summary>
        public bool RestoreFullLength { get; private set; }

        public DayPlanShortenDialog(string habitTitle, string dayLabel, int actualSeconds, int? currentSeconds, Window owner,
                                    string hint = null, string noOptions = null)
        {
            Owner = owner;
            var body = new StackPanel();
            body.Children.Add(DayPlanDialogUi.Heading(DayPlanText.HabitShortenTitle));
            var title = new Emoji.Wpf.TextBlock { Text = habitTitle, FontSize = 14, TextWrapping = TextWrapping.Wrap };
            title.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            body.Children.Add(title);
            body.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitShortenFullLength(
                DayPlanText.HabitShortenMinutes(DayPlanHabitDurations.MinutesLabel(actualSeconds))), 2));

            IReadOnlyList<int> options = DayPlanHabitDurations.Options(actualSeconds);
            var presets = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            if (options.Count == 0)
            {
                body.Children.Add(DayPlanDialogUi.Hint(noOptions ?? DayPlanText.HabitShortenNoOptions, 12));
            }
            else
            {
                foreach (int seconds in options)
                {
                    int s = seconds;
                    var b = DayPlanDialogUi.Secondary(DayPlanText.HabitShortenMinutes(DayPlanHabitDurations.MinutesLabel(s)),
                                                      "ShortenOption" + (s / 60), () => Pick(s), highlighted: currentSeconds == s);
                    b.Margin = new Thickness(0, 0, 8, 8);
                    presets.Children.Add(b);
                }
                body.Children.Add(presets);
            }

            // A typed length, for anything the presets don't cover ("needs to let me type a duration").
            var typedRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = true };
            var error = DayPlanDialogUi.Error();
            var field = DayPlanDialogUi.Field("ShortenTypedField", width: 150);
            field.ToolTip = DayPlanText.HabitShortenTypePlaceholder;
            var placeholder = DayPlanDialogUi.Hint(DayPlanText.HabitShortenTypePlaceholder, 0);
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.Margin = new Thickness(10, 0, 0, 0);
            Action useTyped = () =>
            {
                var typed = DayPlanHabitDurations.TypedTrimFor(field.Text, actualSeconds);
                switch (typed.Outcome)
                {
                    case DayPlanHabitDurations.TypedTrimOutcome.Apply:
                        Pick(typed.Seconds);
                        break;
                    case DayPlanHabitDurations.TypedTrimOutcome.NotShorter:
                        DayPlanDialogUi.ShowError(error, DayPlanText.HabitShortenTypeNotShorter(
                            DayPlanText.HabitShortenMinutes(DayPlanHabitDurations.MinutesLabel(actualSeconds))));
                        break;
                    default:
                        DayPlanDialogUi.ShowError(error, DayPlanText.HabitShortenTypeInvalid);
                        break;
                }
            };
            field.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; useTyped(); } };
            var useBtn = DayPlanDialogUi.Secondary(DayPlanText.HabitShortenUseTyped, "ShortenUseTyped", useTyped);
            useBtn.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(field, Dock.Left);
            DockPanel.SetDock(useBtn, Dock.Left);
            typedRow.Children.Add(field);
            typedRow.Children.Add(useBtn);
            typedRow.Children.Add(placeholder);
            if (actualSeconds > DayPlanHabitDurations.MinimumSeconds)
            {
                body.Children.Add(typedRow);
                body.Children.Add(error);
            }

            body.Children.Add(DayPlanDialogUi.Hint(hint ?? DayPlanText.HabitShortenHint(dayLabel), 12));

            var buttons = new List<Button> { DayPlanDialogUi.Secondary(DayPlanText.HabitCancel, "ShortenCancel", () => DialogResult = false) };
            if (currentSeconds.HasValue)
                buttons.Add(DayPlanDialogUi.Secondary(DayPlanText.HabitShortenFullLengthAction, "ShortenFullLength", () => { RestoreFullLength = true; DialogResult = true; }));
            body.Children.Add(DayPlanDialogUi.ButtonRow(buttons.ToArray()));

            DayPlanDialogUi.Frame(this, "DayPlanShortenDialog", body, 440);
        }

        private void Pick(int seconds)
        {
            PickedSeconds = seconds;
            DialogResult = true;
        }
    }

    /// <summary>
    /// "When do you want your morning routine to start?" — the routine's start time and, for the
    /// evening, the global cutoff. The Mac's RoutineTimingView; validity is FocusBear.Core's
    /// <see cref="RoutineTiming.IsEveningCutoffValid"/>.
    /// </summary>
    public sealed class DayPlanRoutineTimingDialog : Window
    {
        public int Minutes { get; private set; }
        /// <summary>The evening cutoff chosen, or null for none (evening only).</summary>
        public int? CutoffMinutes { get; private set; }

        private readonly TextBox _start;
        private readonly TextBox _cutoff;
        private readonly CheckBox _cutoffOn;
        private readonly TextBlock _error;
        private readonly bool _showsCutoff;
        private readonly int? _morningStart;

        public DayPlanRoutineTimingDialog(string routineName, int minutes, bool showsCutoff, int? cutoffMinutes,
                                          int? morningStartMinutes, Window owner)
        {
            Owner = owner;
            _showsCutoff = showsCutoff;
            _morningStart = morningStartMinutes;
            var body = new StackPanel();
            body.Children.Add(DayPlanDialogUi.Heading(DayPlanText.RoutineTimingQuestion(routineName)));
            _start = DayPlanDialogUi.Field("RoutineStartField", DayPlanDialogUi.TimeText(minutes), 150);
            body.Children.Add(_start);
            body.Children.Add(DayPlanDialogUi.Hint(DayPlanText.RoutineTimingHint));

            var cutoffPanel = new StackPanel();
            _cutoffOn = DayPlanDialogUi.Check(DayPlanText.RoutineCutoffLabel, "RoutineCutoffToggle", cutoffMinutes.HasValue);
            _cutoff = DayPlanDialogUi.Field("RoutineCutoffField", DayPlanDialogUi.TimeText(cutoffMinutes ?? minutes), 150);
            _cutoff.Margin = new Thickness(0, 6, 0, 0);
            var cutoffHint = DayPlanDialogUi.Hint(DayPlanText.RoutineCutoffHint);
            if (showsCutoff)
            {
                cutoffPanel.Children.Add(_cutoffOn);
                cutoffPanel.Children.Add(_cutoff);
                cutoffPanel.Children.Add(cutoffHint);
                body.Children.Add(cutoffPanel);
                Action sync = () =>
                {
                    Visibility v = _cutoffOn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    _cutoff.Visibility = v;
                    cutoffHint.Visibility = v;
                };
                _cutoffOn.Checked += (s, e) => sync();
                _cutoffOn.Unchecked += (s, e) => sync();
                sync();
            }

            _error = DayPlanDialogUi.Error();
            body.Children.Add(_error);
            body.Children.Add(DayPlanDialogUi.ButtonRow(
                DayPlanDialogUi.Secondary(DayPlanText.HabitCancel, "RoutineTimingCancel", () => DialogResult = false),
                DayPlanDialogUi.Primary(DayPlanText.HabitSave, "RoutineTimingSave", TrySave)));

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter && !(Keyboard.FocusedElement is Button)) { e.Handled = true; TrySave(); } };
            ContentRendered += (s, e) => _start.Focus();
            DayPlanDialogUi.Frame(this, "DayPlanRoutineTimingDialog", body, 420);
        }

        private void TrySave()
        {
            int? start = RoutineTiming.ParseTypedTime(_start.Text);
            if (!start.HasValue) { DayPlanDialogUi.ShowError(_error, DayPlanText.RoutineTimeInvalid); return; }
            int? cutoff = null;
            if (_showsCutoff && _cutoffOn.IsChecked == true)
            {
                cutoff = RoutineTiming.ParseTypedTime(_cutoff.Text);
                if (!cutoff.HasValue) { DayPlanDialogUi.ShowError(_error, DayPlanText.RoutineTimeInvalid); return; }
                if (!RoutineTiming.IsEveningCutoffValid(cutoff, start.Value, _morningStart))
                {
                    DayPlanDialogUi.ShowError(_error, DayPlanText.RoutineCutoffInvalid);
                    return;
                }
            }
            Minutes = start.Value;
            CutoffMinutes = cutoff;
            DialogResult = true;
        }
    }

    /// <summary>
    /// A meeting's settings, opened by clicking its Day Plan card (report 2026-09-25_14-31-18: "the blue dot
    /// for colour shouldn't be here. Should be in settings if you click on the event"). The Mac opens its
    /// event form there, which carries the colour swatches; Windows reads meetings from the calendar sync and
    /// has no event write, so this holds only what the Day Plan owns: the colour and hide-for-the-day.
    /// </summary>
    public sealed class DayPlanMeetingSettingsDialog : Window
    {
        public string Colour { get; private set; }
        public bool ColourChanged { get; private set; }
        public bool Hidden { get; private set; }
        public bool HiddenChanged { get; private set; }

        private readonly WrapPanel _swatches = new WrapPanel();
        private readonly IReadOnlyList<string> _palette;

        public DayPlanMeetingSettingsDialog(string title, string when, IReadOnlyList<string> palette, string colour,
                                            bool hidden, Window owner)
        {
            Owner = owner;
            _palette = palette ?? new string[0];
            Colour = colour;
            Hidden = hidden;
            var body = new StackPanel();
            body.Children.Add(DayPlanDialogUi.Hint(DayPlanText.MeetingSettingsTitle, 0));
            body.Children.Add(DayPlanDialogUi.Heading(title));
            var whenText = DayPlanDialogUi.Hint(when, 0);
            whenText.FontSize = 13;
            body.Children.Add(whenText);

            body.Children.Add(DayPlanDialogUi.Label(DayPlanText.MeetingColourLabel, 16));
            body.Children.Add(_swatches);
            PaintSwatches();

            var hide = DayPlanDialogUi.Check(DayPlanText.MeetingHideForDay, "MeetingHideToggle", hidden);
            hide.Margin = new Thickness(0, 16, 0, 0);
            hide.Checked += (s, e) => { Hidden = true; HiddenChanged = Hidden != hidden; };
            hide.Unchecked += (s, e) => { Hidden = false; HiddenChanged = Hidden != hidden; };
            body.Children.Add(hide);

            body.Children.Add(DayPlanDialogUi.Hint(DayPlanText.MeetingFromCalendar, 14));
            body.Children.Add(DayPlanDialogUi.ButtonRow(
                DayPlanDialogUi.Secondary(DayPlanText.HabitCancel, "MeetingSettingsCancel", () => DialogResult = false),
                DayPlanDialogUi.Primary(DayPlanText.DialogDone, "MeetingSettingsDone", () => DialogResult = true)));
            DayPlanDialogUi.Frame(this, "DayPlanMeetingSettingsDialog", body, 440);
        }

        private void PaintSwatches()
        {
            _swatches.Children.Clear();
            for (int i = 0; i < _palette.Count; i++)
            {
                string hex = _palette[i];
                // The first swatch is the default meeting blue, which is stored as "no colour".
                string value = i == 0 ? null : hex;
                bool selected = i == 0 ? string.IsNullOrEmpty(Colour) || string.Equals(Colour, hex, StringComparison.OrdinalIgnoreCase)
                                       : string.Equals(Colour, hex, StringComparison.OrdinalIgnoreCase);
                Color c;
                try { c = (Color)ColorConverter.ConvertFromString(hex); } catch { c = DayPlanDialogUi.Orange; }
                var ring = new Border
                {
                    Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 10, 8),
                    BorderThickness = new Thickness(selected ? 2 : 0), Padding = new Thickness(3),
                    Child = new Border { CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(c) },
                    Cursor = Cursors.Hand,
                    ToolTip = i == 0 ? DayPlanText.MeetingColourDefault : hex,
                };
                ring.SetResourceReference(Border.BorderBrushProperty, "ForegroundColor");
                // A button around the swatch: a bare Border was mouse-only and invisible to screen readers.
                var swatch = new Button
                {
                    Content = ring,
                    Template = Focus_Bear.StatsScreens.StatsUi.PlainTemplate(),
                    Cursor = Cursors.Hand,
                    ToolTip = ring.ToolTip,
                };
                AutomationProperties.SetAutomationId(swatch, "MeetingColour" + i);
                AutomationProperties.SetName(swatch, (i == 0 ? DayPlanText.MeetingColourDefault : hex) + (selected ? " (selected)" : ""));
                swatch.Click += (s, e) =>
                {
                    ColourChanged = !string.Equals(value ?? "", Colour ?? "", StringComparison.OrdinalIgnoreCase) || ColourChanged;
                    Colour = value;
                    PaintSwatches();
                };
                _swatches.Children.Add(swatch);
            }
        }
    }
}
