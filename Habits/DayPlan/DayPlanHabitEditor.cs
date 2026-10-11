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
    /// <summary>What the habit editor was closed with.</summary>
    public enum HabitEditOutcome
    {
        Cancelled,
        Saved,
        Deleted,
        OpenFullEditor,
    }

    /// <summary>
    /// The Day Plan's habit editor — a port of the Mac's HabitEditView (DayPlanHabitEditor.swift), which
    /// mirrors the web dashboard's editor: the inline row (emoji, name, timer + minutes/seconds or what
    /// finishes it) plus the "More options" tabs Timing · Add Videos · Allow Apps · Log Data · Sub habits ·
    /// Instructions · Advanced, and Delete habit (report 2026-09-25_09-05-05 follow-up: "add all the edit
    /// habit options that we have in the Mac app"). Reading and writing the settings JSON is FocusBear.Core's
    /// <see cref="HabitSettingsEdit"/>; validation is <see cref="HabitEditValidator"/> (the web's rules).
    ///
    /// Colours come from the theme resources (BackgroundColor, ForegroundColor, TrayCardBg, TrayCardBorder,
    /// TraySubtitleColor) plus the app's orange accent; light buttons carry explicit black text.
    /// </summary>
    public sealed class DayPlanHabitEditDialog : Window
    {
        public HabitEditValues Values { get; }
        public string HabitIcon => Values.Icon;
        public HabitEditOutcome Outcome { get; private set; } = HabitEditOutcome.Cancelled;
        /// <summary>Kept for callers written against the earlier dialog.</summary>
        public bool OpenFullEditor => Outcome == HabitEditOutcome.OpenFullEditor;

        private enum Tab { Timing, Videos, AllowApps, LogData, SubHabits, Instructions, Advanced }

        private readonly RoutineSettingsSection _section;
        private readonly bool _canDelete;
        private readonly IReadOnlyList<KeyValuePair<string, string>> _focusModes;
        private Tab _tab = Tab.Timing;
        private readonly StackPanel _tabStrip = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly StackPanel _tabBody = new StackPanel();
        private readonly Border _footerHost = new Border();
        private readonly TextBlock _headerTitle;
        private readonly TextBlock _error;
        private TextBox _cutoffField;
        private bool _confirmingDelete;

        /// <param name="focusModes">The user's focus modes (block lists), id → name, for the Allow Apps tab.</param>
        /// <param name="canDelete">False for the habit that is playing right now (the player indexes into it).</param>
        public DayPlanHabitEditDialog(RoutineSettingsSection section, string icon, HabitEditValues values, Window owner,
                                      IReadOnlyList<KeyValuePair<string, string>> focusModes = null, bool canDelete = true)
        {
            Owner = owner;
            _section = section;
            _canDelete = canDelete;
            _focusModes = focusModes ?? new List<KeyValuePair<string, string>>();
            Values = values;
            if (!string.IsNullOrEmpty(icon)) Values.Icon = icon;
            // Editable lists must be mutable.
            Values.VideoUrls = new List<string>(Values.VideoUrls ?? new List<string>());
            Values.AllowedApps = new List<string>(Values.AllowedApps ?? new List<string>());
            Values.AllowedUrls = new List<string>(Values.AllowedUrls ?? new List<string>());
            Values.Checklist = new List<string>(Values.Checklist ?? new List<string>());
            Values.ImageUrls = new List<string>(Values.ImageUrls ?? new List<string>());
            Values.Tags = new List<string>(Values.Tags ?? new List<string>());
            Values.LogQuestions = new List<HabitLogQuestionEdit>(Values.LogQuestions ?? new List<HabitLogQuestionEdit>());

            var root = new DockPanel { LastChildFill = true };

            // Orange header bar: habit name + close, like the web modal header.
            var header = new DockPanel { Background = new SolidColorBrush(DayPlanDialogUi.Orange) };
            var close = FocusClose();
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            _headerTitle = new Emoji.Wpf.TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                                                     TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(18, 11, 8, 11),
                                                     VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(_headerTitle);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            header.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };

            var basics = BuildBasics();
            DockPanel.SetDock(basics, Dock.Top);
            root.Children.Add(basics);

            var strip = new Border { BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(10, 0, 10, 0), Child = new ScrollViewer
            {
                Content = _tabStrip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            } };
            strip.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            strip.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            DockPanel.SetDock(strip, Dock.Top);
            root.Children.Add(strip);

            _error = DayPlanDialogUi.Error();
            _error.Margin = new Thickness(20, 8, 20, 0);
            var footerPanel = new StackPanel();
            footerPanel.Children.Add(_error);
            footerPanel.Children.Add(_footerHost);
            var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = footerPanel };
            footer.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Padding = new Thickness(18, 14, 18, 14), Child = _tabBody } };
            root.Children.Add(scroll);

            var frame = new Border { BorderBrush = new SolidColorBrush(DayPlanDialogUi.Orange), BorderThickness = new Thickness(1), Child = root };
            frame.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");

            Title = "Focus Bear";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Width = 700;
            Height = 620;
            ShowInTaskbar = false;
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            // Never taller than the screen's work area (a 569-px laptop screen at 175%): the footer with Save stays on it.
            Focus_Bear.ToDoScreens.DialogFit.Apply(this);
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            SetResourceReference(ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(this, "DayPlanHabitEditDialog");
            Content = frame;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; DialogResult = false; }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
                         && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn))
                {
                    e.Handled = true;
                    TrySave();
                }
            };

            UpdateHeader();
            RenderTabs();
            RenderFooter();
        }

        /// <summary>Opens on a given tab (the harness and the "Log data" shortcut use this).</summary>
        public void ShowTab(int index)
        {
            _tab = (Tab)Math.Max(0, Math.Min(6, index));
            RenderTabs();
        }

        // MARK: - Header + inline row

        private Button FocusClose()
        {
            var x = new TextBlock { Text = "✕", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            var b = new Button { Content = x, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Template = PlainTemplate(), ToolTip = DayPlanText.HabitCancel };
            AutomationProperties.SetAutomationId(b, "HabitEditClose");
            b.Click += (s, e) => DialogResult = false;
            return b;
        }

        private void UpdateHeader()
        {
            string name = string.IsNullOrWhiteSpace(Values.TrimmedName) ? DayPlanText.EditHabitTitle : Values.TrimmedName;
            _headerTitle.Text = HabitSettingsEdit.DisplayName(Values.Icon, name);
        }

        private FrameworkElement BuildBasics()
        {
            var panel = new StackPanel { Margin = new Thickness(18, 12, 18, 12) };

            var nameRow = new DockPanel { LastChildFill = true };
            var icon = DayPlanDialogUi.Field("HabitIconField", Values.Icon, 52);
            icon.FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI");
            icon.HorizontalContentAlignment = HorizontalAlignment.Center;
            icon.ToolTip = DayPlanText.HabitPickEmoji;
            icon.TextChanged += (s, e) => { Values.Icon = icon.Text ?? ""; UpdateHeader(); };
            DockPanel.SetDock(icon, Dock.Left);
            nameRow.Children.Add(icon);
            var name = DayPlanDialogUi.Field("HabitNameField", Values.Name);
            name.Margin = new Thickness(8, 0, 0, 0);
            name.ToolTip = DayPlanText.HabitFieldName;
            name.TextChanged += (s, e) => { Values.Name = name.Text ?? ""; UpdateHeader(); };
            nameRow.Children.Add(name);
            panel.Children.Add(nameRow);
            ContentRendered += (s, e) => { name.Focus(); };

            var timerRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var useTimer = DayPlanDialogUi.Check(DayPlanText.HabitFieldUseTimer, "HabitUseTimer", Values.UsesTimer);
            useTimer.Margin = new Thickness(0, 4, 16, 0);
            timerRow.Children.Add(useTimer);

            var duration = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            duration.Children.Add(Label(DayPlanText.HabitFieldDurationLabel, new Thickness(0, 0, 6, 0)));
            var minutes = DayPlanDialogUi.Field("HabitDurationMinutes", (Values.DurationSeconds / 60).ToString(CultureInfo.InvariantCulture), 52);
            var seconds = DayPlanDialogUi.Field("HabitDurationSeconds", (Values.DurationSeconds % 60).ToString(CultureInfo.InvariantCulture), 44);
            var total = DayPlanDialogUi.Hint(HabitDurationText.Short(Values.DurationSeconds), 0);
            total.VerticalAlignment = VerticalAlignment.Center;
            total.Margin = new Thickness(8, 0, 0, 0);
            TextChangedEventHandler recompute = (s, e) =>
            {
                int m, sec;
                int.TryParse(minutes.Text, NumberStyles.None, CultureInfo.InvariantCulture, out m);
                int.TryParse(seconds.Text, NumberStyles.None, CultureInfo.InvariantCulture, out sec);
                Values.DurationSeconds = Math.Max(0, Math.Min(HabitEditValues.MaxDurationMinutes, m)) * 60 + Math.Max(0, Math.Min(59, sec));
                total.Text = HabitDurationText.Short(Values.DurationSeconds);
            };
            minutes.TextChanged += recompute;
            seconds.TextChanged += recompute;
            duration.Children.Add(minutes);
            duration.Children.Add(Unit(DayPlanText.HabitDurationMinutesUnit));
            duration.Children.Add(seconds);
            duration.Children.Add(Unit(DayPlanText.HabitDurationSecondsUnit));
            duration.Children.Add(total);
            timerRow.Children.Add(duration);

            var completion = new DockPanel { LastChildFill = true, MinWidth = 480 };
            var completionLabel = Label(DayPlanText.HabitFieldCompletion, new Thickness(0, 0, 8, 0));
            DockPanel.SetDock(completionLabel, Dock.Left);
            completion.Children.Add(completionLabel);
            var completionField = DayPlanDialogUi.Field("HabitCompletionField", Values.CompletionRequirement);
            completionField.TextChanged += (s, e) => Values.CompletionRequirement = completionField.Text ?? "";
            completion.Children.Add(completionField);
            timerRow.Children.Add(completion);

            Action sync = () =>
            {
                Values.UsesTimer = useTimer.IsChecked == true;
                duration.Visibility = Values.UsesTimer ? Visibility.Visible : Visibility.Collapsed;
                completion.Visibility = Values.UsesTimer ? Visibility.Collapsed : Visibility.Visible;
            };
            useTimer.Checked += (s, e) => sync();
            useTimer.Unchecked += (s, e) => sync();
            sync();
            panel.Children.Add(timerRow);
            return panel;
        }

        // MARK: - Tabs

        private static string TabTitle(Tab t)
        {
            switch (t)
            {
                case Tab.Timing: return DayPlanText.HabitTabTiming;
                case Tab.Videos: return DayPlanText.HabitTabVideos;
                case Tab.AllowApps: return DayPlanText.HabitTabAllowApps;
                case Tab.LogData: return DayPlanText.HabitTabLogData;
                case Tab.SubHabits: return DayPlanText.HabitTabSubHabits;
                case Tab.Instructions: return DayPlanText.HabitTabInstructions;
                default: return DayPlanText.HabitTabAdvanced;
            }
        }

        private void RenderTabs()
        {
            _tabStrip.Children.Clear();
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                bool active = t == _tab;
                var label = new TextBlock { Text = TabTitle(t), FontSize = 12.5, FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(9, 9, 9, 7) };
                if (active) label.Foreground = new SolidColorBrush(DayPlanDialogUi.Orange);
                else label.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
                var box = new Border { BorderThickness = new Thickness(0, 0, 0, 2), BorderBrush = active ? new SolidColorBrush(DayPlanDialogUi.Orange) : Brushes.Transparent, Child = label };
                var b = new Button { Content = box, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Template = PlainTemplate(), FocusVisualStyle = null };
                AutomationProperties.SetAutomationId(b, "HabitTab" + t);
                AutomationProperties.SetName(b, TabTitle(t));
                Tab captured = t;
                b.Click += (s, e) => { _tab = captured; RenderTabs(); };
                _tabStrip.Children.Add(b);
            }
            RenderBody();
        }

        private void RenderBody()
        {
            _tabBody.Children.Clear();
            _cutoffField = null;
            switch (_tab)
            {
                case Tab.Timing: TimingTab(); break;
                case Tab.Videos: VideosTab(); break;
                case Tab.AllowApps: AllowAppsTab(); break;
                case Tab.LogData: LogDataTab(); break;
                case Tab.SubHabits: SubHabitsTab(); break;
                case Tab.Instructions: InstructionsTab(); break;
                default: AdvancedTab(); break;
            }
        }

        private void TimingTab()
        {
            bool anything = false;
            if (HabitEditValidator.ShowsPriority(_section))
            {
                anything = true;
                _tabBody.Children.Add(Label(DayPlanText.HabitFieldPriority, new Thickness(0, 0, 0, 4)));
                var prio = new StackPanel { Orientation = Orientation.Horizontal };
                prio.Children.Add(Radio(DayPlanText.HabitPriorityHigh, "HabitPriorityHigh", "prio", Values.IsHighPriority, () => { Values.IsHighPriority = true; RenderBody(); }));
                prio.Children.Add(Radio(DayPlanText.HabitPriorityStandard, "HabitPriorityStandard", "prio", !Values.IsHighPriority, () => { Values.IsHighPriority = false; RenderBody(); }));
                _tabBody.Children.Add(prio);
            }
            if (HabitEditValidator.ShowsDaysOfWeek(_section))
            {
                anything = true;
                _tabBody.Children.Add(Label(DayPlanText.HabitFieldDays, new Thickness(0, 12, 0, 4)));
                var chips = new WrapPanel();
                foreach (string code in WeekdayCode.All) chips.Children.Add(DayChip(code));
                _tabBody.Children.Add(chips);
                var shortcuts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                shortcuts.Children.Add(Shortcut(DayPlanText.HabitDaysWeekdays, new[] { "MON", "TUE", "WED", "THU", "FRI" }));
                shortcuts.Children.Add(Shortcut(DayPlanText.HabitDaysWeekend, new[] { "SAT", "SUN" }));
                shortcuts.Children.Add(Shortcut(DayPlanText.HabitDaysAll, WeekdayCode.All.ToArray()));
                _tabBody.Children.Add(shortcuts);
            }
            if (HabitEditValidator.ShowsCutoff(_section, Values))
            {
                anything = true;
                var cutoff = DayPlanDialogUi.Check(DayPlanText.HabitFieldCutoff, "HabitCutoffToggle", Values.CutoffEnabled);
                cutoff.Margin = new Thickness(0, 14, 0, 0);
                cutoff.Checked += (s, e) => { Values.CutoffEnabled = true; RenderBody(); };
                cutoff.Unchecked += (s, e) => { Values.CutoffEnabled = false; RenderBody(); };
                _tabBody.Children.Add(cutoff);
                if (Values.CutoffEnabled)
                {
                    _cutoffField = DayPlanDialogUi.Field("HabitCutoffField", DayPlanDialogUi.TimeText(Values.CutoffMinutes), 130);
                    _cutoffField.Margin = new Thickness(22, 6, 0, 0);
                    _tabBody.Children.Add(_cutoffField);
                }
            }
            if (!anything) _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitTimingNothing, 0));
        }

        private void VideosTab()
        {
            _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldVideoUrls, 0));
            _tabBody.Children.Add(StringList(Values.VideoUrls, "HabitVideo", "https://youtu.be/…"));
        }

        private void AllowAppsTab()
        {
            var useFocus = DayPlanDialogUi.Check(DayPlanText.HabitFieldUseFocusMode, "HabitUseFocusMode", Values.UsesFocusMode);
            useFocus.Margin = new Thickness(0);
            useFocus.IsEnabled = _focusModes.Count > 0 || Values.UsesFocusMode;
            useFocus.Checked += (s, e) => { if (!Values.UsesFocusMode) { Values.SetUsesFocusMode(true, _focusModes.Count > 0 ? _focusModes[0].Key : ""); RenderBody(); } };
            useFocus.Unchecked += (s, e) => { Values.SetUsesFocusMode(false, null); RenderBody(); };
            _tabBody.Children.Add(useFocus);
            if (Values.UsesFocusMode)
            {
                _tabBody.Children.Add(Label(DayPlanText.HabitFieldFocusMode, new Thickness(22, 10, 0, 4)));
                var modes = new StackPanel { Margin = new Thickness(22, 0, 0, 0) };
                var list = _focusModes.ToList();
                if (!list.Any(m => m.Key == Values.AllowedFocusModeId))
                    list.Insert(0, new KeyValuePair<string, string>(Values.AllowedFocusModeId, DayPlanText.HabitFocusModeUnknown));
                foreach (var mode in list)
                {
                    string id = mode.Key;
                    modes.Children.Add(Radio(mode.Value, "HabitFocusMode", "focusmode", id == Values.AllowedFocusModeId, () => Values.AllowedFocusModeId = id));
                }
                _tabBody.Children.Add(modes);
            }
            else
            {
                _tabBody.Children.Add(Label(DayPlanText.HabitFieldAllowedApps, new Thickness(0, 12, 0, 2)));
                _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldAllowedAppsHint, 0));
                _tabBody.Children.Add(StringList(Values.AllowedApps, "HabitAllowedApp", "notepad.exe", onChange: SyncMicroBreaks));
                _tabBody.Children.Add(Label(DayPlanText.HabitFieldAllowedUrls, new Thickness(0, 12, 0, 2)));
                _tabBody.Children.Add(StringList(Values.AllowedUrls, "HabitAllowedUrl", "example.com", onChange: SyncMicroBreaks));
                _microBreaks = DayPlanDialogUi.Check(DayPlanText.HabitFieldRunMicroBreaks, "HabitRunMicroBreaks", Values.RunMicroBreaks);
                _microBreaks.Checked += (s, e) => Values.RunMicroBreaks = true;
                _microBreaks.Unchecked += (s, e) => Values.RunMicroBreaks = false;
                _tabBody.Children.Add(_microBreaks);
                SyncMicroBreaks();
            }
            var saved = DayPlanDialogUi.Check(DayPlanText.HabitFieldShowSavedSites, "HabitShowSavedSites", Values.ShowSavedDistractingWebsites);
            saved.Margin = new Thickness(0, 14, 0, 0);
            saved.Checked += (s, e) => Values.ShowSavedDistractingWebsites = true;
            saved.Unchecked += (s, e) => Values.ShowSavedDistractingWebsites = false;
            _tabBody.Children.Add(saved);
        }

        private CheckBox _microBreaks;

        private void SyncMicroBreaks()
        {
            if (_microBreaks == null) return;
            _microBreaks.IsEnabled = Values.CanRunMicroBreaks;
            _microBreaks.Opacity = Values.CanRunMicroBreaks ? 1 : 0.55;
        }

        private void LogDataTab()
        {
            if (Values.RandomChoiceCount > 0)
            {
                _tabBody.Children.Add(RandomChoicesNote());
            }
            else
            {
                var log = DayPlanDialogUi.Check(DayPlanText.HabitFieldLogQuantity, "HabitLogQuantity", Values.LogQuantity);
                log.Margin = new Thickness(0);
                log.Checked += (s, e) => { Values.LogQuantity = true; RenderBody(); };
                log.Unchecked += (s, e) => { Values.LogQuantity = false; RenderBody(); };
                _tabBody.Children.Add(log);
                if (Values.LogQuantity)
                {
                    var rows = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                    for (int i = 0; i < Values.LogQuestions.Count; i++) rows.Children.Add(QuestionRow(i));
                    _tabBody.Children.Add(rows);
                    var add = DayPlanDialogUi.Secondary("+ " + DayPlanText.HabitAddQuestion, "HabitAddQuestion", () =>
                    {
                        Values.LogQuestions.Add(new HabitLogQuestionEdit(Guid.NewGuid().ToString(), ""));
                        RenderBody();
                    });
                    add.HorizontalAlignment = HorizontalAlignment.Left;
                    add.Margin = new Thickness(0, 4, 0, 0);
                    _tabBody.Children.Add(add);
                    _tabBody.Children.Add(Label(DayPlanText.HabitFieldLogType, new Thickness(0, 12, 0, 4)));
                    var type = new StackPanel { Orientation = Orientation.Horizontal };
                    type.Children.Add(Radio(DayPlanText.HabitLogTypeSum, "HabitLogSum", "logtype", Values.LogSummaryType != HabitNotes.SummaryAverage, () => Values.LogSummaryType = HabitNotes.SummarySum));
                    type.Children.Add(Radio(DayPlanText.HabitLogTypeAverage, "HabitLogAverage", "logtype", Values.LogSummaryType == HabitNotes.SummaryAverage, () => Values.LogSummaryType = HabitNotes.SummaryAverage));
                    _tabBody.Children.Add(type);
                }
            }
            _tabBody.Children.Add(Label(DayPlanText.HabitFieldTakeNotes, new Thickness(0, 14, 0, 4)));
            var notes = new StackPanel();
            notes.Children.Add(Radio(DayPlanText.HabitNotesNone, "HabitNotesNone", "notes", Values.TakeNotes != HabitNotes.During && Values.TakeNotes != HabitNotes.End, () => Values.TakeNotes = HabitNotes.None));
            notes.Children.Add(Radio(DayPlanText.HabitNotesDuring, "HabitNotesDuring", "notes", Values.TakeNotes == HabitNotes.During, () => Values.TakeNotes = HabitNotes.During));
            notes.Children.Add(Radio(DayPlanText.HabitNotesEnd, "HabitNotesEnd", "notes", Values.TakeNotes == HabitNotes.End, () => Values.TakeNotes = HabitNotes.End));
            _tabBody.Children.Add(notes);
        }

        private FrameworkElement QuestionRow(int index)
        {
            HabitLogQuestionEdit q = Values.LogQuestions[index];
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var remove = RemoveButton(() => { Values.LogQuestions.RemoveAt(index); RenderBody(); });
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var max = DayPlanDialogUi.Field("HabitQuestionMax", q.MaxValue, 60);
            max.ToolTip = DayPlanText.HabitFieldMax;
            max.TextChanged += (s, e) => q.MaxValue = max.Text ?? "";
            max.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(max, Dock.Right);
            row.Children.Add(max);
            var min = DayPlanDialogUi.Field("HabitQuestionMin", q.MinValue, 60);
            min.ToolTip = DayPlanText.HabitFieldMin;
            min.TextChanged += (s, e) => q.MinValue = min.Text ?? "";
            min.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(min, Dock.Right);
            row.Children.Add(min);
            var text = DayPlanDialogUi.Field("HabitQuestionText", q.Question);
            text.ToolTip = DayPlanText.HabitFieldQuestion;
            text.TextChanged += (s, e) => q.Question = text.Text ?? "";
            row.Children.Add(text);
            var wrap = new StackPanel();
            if (index == 0)
            {
                var head = new DockPanel { Margin = new Thickness(0, 0, 34, 2) };
                var maxH = DayPlanDialogUi.Hint(DayPlanText.HabitFieldMax, 0); maxH.Width = 60; maxH.Margin = new Thickness(6, 0, 0, 0);
                var minH = DayPlanDialogUi.Hint(DayPlanText.HabitFieldMin, 0); minH.Width = 60; minH.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(maxH, Dock.Right); DockPanel.SetDock(minH, Dock.Right);
                head.Children.Add(maxH); head.Children.Add(minH);
                head.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldQuestion, 0));
                wrap.Children.Add(head);
            }
            wrap.Children.Add(row);
            return wrap;
        }

        private void SubHabitsTab()
        {
            if (Values.RandomChoiceCount > 0) { _tabBody.Children.Add(RandomChoicesNote()); return; }
            _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldChecklist, 0));
            _tabBody.Children.Add(StringList(Values.Checklist, "HabitChecklistItem", ""));
        }

        private void InstructionsTab()
        {
            _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldInstructions, 0));
            var text = DayPlanDialogUi.Field("HabitInstructionsField", Values.TextInstructions);
            text.AcceptsReturn = true;
            text.TextWrapping = TextWrapping.Wrap;
            text.VerticalContentAlignment = VerticalAlignment.Top;
            text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            text.Height = 110;
            text.Margin = new Thickness(0, 6, 0, 0);
            text.TextChanged += (s, e) => Values.TextInstructions = text.Text ?? "";
            _tabBody.Children.Add(text);
            _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldImages, 14));
            _tabBody.Children.Add(StringList(Values.ImageUrls, "HabitImageUrl", "https://…"));
        }

        private void AdvancedTab()
        {
            _tabBody.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitFieldTags, 0));
            _tabBody.Children.Add(StringList(Values.Tags, "HabitTag", ""));
        }

        private FrameworkElement RandomChoicesNote()
        {
            var panel = new StackPanel();
            panel.Children.Add(DayPlanDialogUi.Hint(DayPlanText.HabitRandomChoicesNote(Values.RandomChoiceCount), 0));
            var link = new TextBlock { Text = DayPlanText.HabitMoreOptions, FontSize = 12.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Orange),
                                       TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand, Margin = new Thickness(0, 8, 0, 0) };
            link.MouseLeftButtonUp += (s, e) => { Outcome = HabitEditOutcome.OpenFullEditor; DialogResult = false; };
            panel.Children.Add(link);
            return panel;
        }

        // MARK: - Footer

        private void RenderFooter()
        {
            var row = new DockPanel { Margin = new Thickness(20, 12, 20, 14), LastChildFill = false };
            if (_confirmingDelete)
            {
                var confirm = new TextBlock { Text = DayPlanText.HabitDeleteConfirm(Values.TrimmedName), FontSize = 12.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Red),
                                              TextWrapping = TextWrapping.Wrap, MaxWidth = 380, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(confirm, Dock.Left);
                row.Children.Add(confirm);
                var del = Destructive(DayPlanText.HabitDeleteConfirmButton, "HabitDeleteConfirm", () => { Outcome = HabitEditOutcome.Deleted; DialogResult = true; });
                DockPanel.SetDock(del, Dock.Right);
                row.Children.Add(del);
                var keep = DayPlanDialogUi.Secondary(DayPlanText.HabitCancel, "HabitDeleteCancel", () => { _confirmingDelete = false; RenderFooter(); });
                DockPanel.SetDock(keep, Dock.Right);
                row.Children.Add(keep);
            }
            else
            {
                if (_canDelete)
                {
                    var delete = OutlineRed(DayPlanText.HabitDelete, "HabitDelete", () => { _confirmingDelete = true; RenderFooter(); });
                    DockPanel.SetDock(delete, Dock.Left);
                    row.Children.Add(delete);
                }
                var save = DayPlanDialogUi.Primary(DayPlanText.HabitSave, "HabitEditSave", TrySave);
                save.Margin = new Thickness(0);
                DockPanel.SetDock(save, Dock.Right);
                row.Children.Add(save);
                var cancel = DayPlanDialogUi.Secondary(DayPlanText.HabitCancel, "HabitEditCancel", () => DialogResult = false);
                DockPanel.SetDock(cancel, Dock.Right);
                row.Children.Add(cancel);
            }
            _footerHost.Child = row;
        }

        private void TrySave()
        {
            if (_cutoffField != null)
            {
                int? cutoff = RoutineTiming.ParseTypedTime(_cutoffField.Text);
                if (!cutoff.HasValue) { Fail(DayPlanText.RoutineTimeInvalid, Tab.Timing); return; }
                Values.CutoffMinutes = cutoff.Value;
            }
            HabitValidationFailure? failure = HabitEditValidator.Validate(Values, _section);
            if (failure.HasValue)
            {
                Fail(FailureText(failure.Value), TabFor(failure.Value));
                return;
            }
            Outcome = HabitEditOutcome.Saved;
            DialogResult = true;
        }

        private void Fail(string message, Tab? tab)
        {
            if (tab.HasValue && tab.Value != _tab && tab.Value != Tab.Timing) { _tab = tab.Value; RenderTabs(); }
            else if (tab == Tab.Timing && _tab != Tab.Timing) { _tab = Tab.Timing; RenderTabs(); }
            DayPlanDialogUi.ShowError(_error, message);
        }

        private static Tab? TabFor(HabitValidationFailure failure)
        {
            switch (failure)
            {
                case HabitValidationFailure.Days: return Tab.Timing;
                case HabitValidationFailure.Video: return Tab.Videos;
                case HabitValidationFailure.Questions: return Tab.LogData;
                case HabitValidationFailure.Tags: return Tab.Advanced;
                default: return null;   // name / duration / completion live in the inline row
            }
        }

        private static string FailureText(HabitValidationFailure failure)
        {
            switch (failure)
            {
                case HabitValidationFailure.Name: return DayPlanText.HabitInvalidName;
                case HabitValidationFailure.DurationRange: return DayPlanText.HabitInvalidDurationRange;
                case HabitValidationFailure.Completion: return DayPlanText.HabitInvalidCompletion;
                case HabitValidationFailure.Days: return DayPlanText.HabitInvalidDays;
                case HabitValidationFailure.Video: return DayPlanText.HabitInvalidVideo;
                case HabitValidationFailure.Questions: return DayPlanText.HabitInvalidQuestions;
                case HabitValidationFailure.Tags: return DayPlanText.HabitInvalidTags;
                default: return DayPlanText.HabitEditUnavailable;
            }
        }

        // MARK: - Building blocks

        private static ControlTemplate PlainTemplate()
        {
            var t = new ControlTemplate(typeof(ButtonBase));
            t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            return t;
        }

        private static TextBlock Label(string text, Thickness margin)
        {
            var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap, Margin = margin, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            return t;
        }

        private static TextBlock Unit(string text)
        {
            var t = DayPlanDialogUi.Hint(text, 0);
            t.Margin = new Thickness(4, 0, 8, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        private RadioButton Radio(string text, string automationId, string group, bool isChecked, Action onChecked)
        {
            var label = new Emoji.Wpf.TextBlock { Text = text, FontSize = 13, Margin = new Thickness(4, 0, 0, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var r = new RadioButton { Content = label, IsChecked = isChecked, GroupName = group + "-" + _groupKey,
                                      Margin = new Thickness(0, 2, 18, 2), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(r, automationId);
            r.Checked += (s, e) => onChecked();
            return r;
        }

        // Radio groups are keyed per dialog so two editors never share a group.
        private readonly string _groupKey = Guid.NewGuid().ToString("N");

        /// <summary>A list of single-line rows with remove buttons and an "Add" link (Mac EditableStringList).</summary>
        private FrameworkElement StringList(IList<string> items, string automationId, string placeholder, Action onChange = null)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
                var remove = RemoveButton(() => { items.RemoveAt(index); onChange?.Invoke(); RenderBody(); });
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);
                var field = DayPlanDialogUi.Field(automationId + index, items[index]);
                if (!string.IsNullOrEmpty(placeholder)) field.ToolTip = placeholder;
                field.TextChanged += (s, e) => { if (index < items.Count) items[index] = field.Text ?? ""; onChange?.Invoke(); };
                row.Children.Add(field);
                panel.Children.Add(row);
            }
            var add = new TextBlock { Text = "+ " + DayPlanText.HabitAddItem, FontSize = 12.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Orange), Cursor = Cursors.Hand, Margin = new Thickness(0, 2, 0, 0) };
            AutomationProperties.SetAutomationId(add, automationId + "Add");
            add.MouseLeftButtonUp += (s, e) => { items.Add(""); RenderBody(); FocusLast(automationId); };
            panel.Children.Add(add);
            return panel;
        }

        private void FocusLast(string automationIdPrefix)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBox last = null;
                foreach (var tb in Descendants<TextBox>(_tabBody))
                    if ((AutomationProperties.GetAutomationId(tb) ?? "").StartsWith(automationIdPrefix, StringComparison.Ordinal)) last = tb;
                last?.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var d in Descendants<T>(child)) yield return d;
            }
        }

        private static Button RemoveButton(Action onClick)
        {
            var x = new TextBlock { Text = "✕", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            x.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var b = new Button { Content = x, Width = 28, Height = 26, Margin = new Thickness(6, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                                 Cursor = Cursors.Hand, Template = PlainTemplate(), ToolTip = DayPlanText.TasksRemove };
            AutomationProperties.SetAutomationId(b, "HabitRemoveRow");
            b.Click += (s, e) => onClick();
            return b;
        }

        private static Button OutlineRed(string text, string automationId, Action onClick)
        {
            var b = DayPlanDialogUi.Secondary(text, automationId, onClick);
            b.Background = Brushes.Transparent;
            b.BorderBrush = new SolidColorBrush(DayPlanDialogUi.Red);
            ((TextBlock)b.Content).Foreground = new SolidColorBrush(DayPlanDialogUi.Red);
            b.Margin = new Thickness(0);
            return b;
        }

        private static Button Destructive(string text, string automationId, Action onClick)
        {
            var b = DayPlanDialogUi.Primary(text, automationId, onClick);
            b.Background = new SolidColorBrush(DayPlanDialogUi.Red);
            b.BorderBrush = new SolidColorBrush(DayPlanDialogUi.Red);
            b.Margin = new Thickness(0);
            return b;
        }

        private ToggleButton DayChip(string code)
        {
            var label = new TextBlock { Text = ShortDayLabel(code), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            var chip = new ToggleButton { Content = label, IsChecked = Values.DaysOfWeek.Contains(code), Width = 46, Height = 28, Margin = new Thickness(0, 0, 5, 5),
                                          BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null };
            var template = new ControlTemplate(typeof(ToggleButton));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            chip.Template = template;
            Action paint = () =>
            {
                bool on = chip.IsChecked == true;
                if (on)
                {
                    chip.Background = new SolidColorBrush(DayPlanDialogUi.Orange);
                    chip.BorderBrush = new SolidColorBrush(DayPlanDialogUi.Orange);
                    label.Foreground = Brushes.White;
                }
                else
                {
                    chip.SetResourceReference(Control.BackgroundProperty, "TrayCardBg");
                    chip.SetResourceReference(Control.BorderBrushProperty, "TraySubtitleColor");
                    label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                }
            };
            chip.Checked += (s, e) => { Values.DaysOfWeek.Add(code); paint(); };
            chip.Unchecked += (s, e) => { Values.DaysOfWeek.Remove(code); paint(); };
            paint();
            AutomationProperties.SetAutomationId(chip, "HabitDay" + code);
            AutomationProperties.SetName(chip, code);
            return chip;
        }

        private static string ShortDayLabel(string code)
        {
            int index = WeekdayCode.All.ToList().IndexOf(code);   // 0 = Monday
            if (index < 0) return code;
            string[] names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;   // 0 = Sunday
            return names[(index + 1) % 7];
        }

        private TextBlock Shortcut(string text, string[] codes)
        {
            var t = new TextBlock { Text = text, FontSize = 11.5, TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 12, 0) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            t.MouseLeftButtonUp += (s, e) =>
            {
                Values.DaysOfWeek = new HashSet<string>(codes);
                RenderBody();
            };
            return t;
        }
    }
}
