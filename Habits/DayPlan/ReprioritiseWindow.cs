using Focus_Bear.Utils;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// "Reschedule plan" — the Mac's <c>ForecastReschedulePlanSheet</c> (report 2026-09-25_14-33-34: "missing
    /// the reprioritise tasks feature that we have on Mac"). Opened from the To-do list's menu or prompt, and
    /// from the Day Plan's "Fix" → Postpone tasks (then focused on that day). Every change is a proposal the
    /// user ticks; nothing is written until "Apply selected changes".
    /// </summary>
    public sealed class ReprioritiseWindow : Window
    {
        private static ReprioritiseWindow _open;

        private readonly ReprioritiseForecast _store = ReprioritiseForecast.Shared;
        private readonly ForecastDayFocus.Target? _focus;
        private readonly HashSet<string> _selected = new HashSet<string>();
        private readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool> { { "major", true }, { "minor", false }, { "projectDeadlines", false } };
        /// <summary>The section heading to give keyboard focus back to after its toggle re-renders the list.</summary>
        private string _refocusSection;
        private readonly HashSet<string> _editingDate = new HashSet<string>();
        private readonly HashSet<string> _editingDuration = new HashSet<string>();
        /// <summary>The task whose outcome slider gets keyboard focus back after the plan re-flows under it.</summary>
        private string _refocusOutcome;
        private readonly StackPanel _body = new StackPanel();
        private readonly TextBlock _message;
        private readonly CheckBox _lockDays;
        private readonly TextBlock _lockHint;
        private readonly Button _apply;
        private readonly Button _selectAll;

        /// <summary>Opens the plan (one at a time), recalculating it underneath.</summary>
        public static void Open(Window owner, ForecastDayFocus.Target? focus = null)
        {
            try
            {
                if (_open != null) { _open.Close(); _open = null; }
                var w = new ReprioritiseWindow(owner, focus);
                _open = w;
                w.Closed += (s, e) => { if (_open == w) _open = null; };
                w.Show();
                AppSettings.GetInstance.CapturePostHog(focus.HasValue ? "day-plan-fix-postpone-tasks-opened" : "reprioritise-plan-opened");
                _ = w._store.RefreshAsync(true);
            }
            catch (Exception ex) { LogWriter.LogWrite("ReprioritiseWindow.Open ex - " + ex.Message); }
        }

        private ReprioritiseWindow(Window owner, ForecastDayFocus.Target? focus)
        {
            Owner = owner;
            _focus = focus;
            Title = ForecastText.Get("reviewPlan");
            Width = 700;
            Height = 700;
            MinWidth = 520;
            MinHeight = 480;
            ShowInTaskbar = true;
            // Placed by FitToWorkArea, not CenterOwner: centring a 700-high window on an owner near the top
            // of a short screen started it above the screen, cutting off the title and Close.
            WindowStartupLocation = WindowStartupLocation.Manual;
            SourceInitialized += (s, e) => FitToWorkArea();
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            SetResourceReference(ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(this, "ReprioritiseWindow");

            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 16), LastChildFill = true };

            var top = new StackPanel();
            var titleRow = new DockPanel();
            var close = DayPlanDialogUi.Secondary(ForecastText.Get("close"), "ReprioritiseClose", Close);
            close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            titleRow.Children.Add(close);
            var title = DayPlanDialogUi.Heading(ForecastText.Get("reviewPlan"));
            title.FontSize = 20;
            title.Margin = new Thickness(0);
            title.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(title);
            top.Children.Add(titleRow);
            top.Children.Add(DayPlanDialogUi.Hint(ForecastText.Get("reviewExplanationWindows"), 8));
            _lockDays = DayPlanDialogUi.Check(ForecastText.Get("keepPlannedDays"), "ReprioritiseLockDays", _store.LocksPlannedDays);
            _lockDays.Checked += (s, e) => _store.LocksPlannedDays = true;
            _lockDays.Unchecked += (s, e) => _store.LocksPlannedDays = false;
            top.Children.Add(_lockDays);
            _lockHint = DayPlanDialogUi.Hint(ForecastText.Get("plannedDaysLocked"), 2);
            _lockHint.Margin = new Thickness(22, 2, 0, 0);
            top.Children.Add(_lockHint);
            top.Children.Add(DayPlanDialogUi.Hint(ForecastText.Get("dayPlanFollowsTheDates"), 6));
            _message = new TextBlock { FontSize = 12.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Orange), TextWrapping = TextWrapping.Wrap,
                                       Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            top.Children.Add(_message);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            _apply = DayPlanDialogUi.Primary(ForecastText.Get("applySelected"), "ReprioritiseApply", Apply);
            _apply.Margin = new Thickness(0);
            DockPanel.SetDock(_apply, Dock.Right);
            footer.Children.Add(_apply);
            _selectAll = DayPlanDialogUi.Secondary(ForecastText.Get("selectAll"), "ReprioritiseSelectAll", SelectAll);
            _selectAll.HorizontalAlignment = HorizontalAlignment.Left;
            footer.Children.Add(_selectAll);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0),
                                            Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = _body } };
            root.Children.Add(scroll);
            Content = root;

            _store.Changed += OnStoreChanged;
            Closed += (s, e) => _store.Changed -= OnStoreChanged;
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox)) Close(); };
            Render();
        }

        /// <summary>
        /// Keep the whole window on the monitor it opens on: no taller or wider than that monitor's work
        /// area (less a margin), centred on the owner, and pushed back inside the work area. The header and
        /// buttons are docked, so a shorter window scrolls only the list between them.
        /// </summary>
        private void FitToWorkArea()
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget == null) return;
                IntPtr hwnd = Owner != null && new System.Windows.Interop.WindowInteropHelper(Owner).Handle != IntPtr.Zero
                    ? new System.Windows.Interop.WindowInteropHelper(Owner).Handle
                    : new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MonitorInfo)) };
                IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                Rect work;
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
                    Point topLeft = fromDevice.Transform(new Point(info.Work.Left, info.Work.Top));
                    Point bottomRight = fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom));
                    work = new Rect(topLeft, bottomRight);
                }
                else work = SystemParameters.WorkArea;

                // A maximised owner's Left/Top are its restore bounds, not where it is: centre on the screen then.
                FocusBear.Core.Layout.Box? around = null;
                if (Owner != null && Owner.IsVisible && Owner.WindowState == WindowState.Normal && Owner.ActualWidth > 0)
                    around = new FocusBear.Core.Layout.Box(Owner.Left, Owner.Top, Owner.ActualWidth, Owner.ActualHeight);
                var box = FocusBear.Core.Layout.WindowFit.Place(new FocusBear.Core.Layout.Box(work.Left, work.Top, work.Width, work.Height),
                                                                Width, Height, around);
                MaxWidth = FocusBear.Core.Layout.WindowFit.MaxExtent(work.Width);
                MaxHeight = FocusBear.Core.Layout.WindowFit.MaxExtent(work.Height);
                MinWidth = Math.Min(MinWidth, MaxWidth);
                MinHeight = Math.Min(MinHeight, MaxHeight);
                Width = box.Width;
                Height = box.Height;
                Left = box.Left;
                Top = box.Top;
                LogWriter.LogWrite("ReprioritiseWindow placed at " + box + " in work area " + work);
            }
            catch (Exception ex) { LogWriter.LogWrite("ReprioritiseWindow.FitToWorkArea - " + ex.Message); }
        }

        private const uint MonitorDefaultToNearest = 2;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        private void OnStoreChanged()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(OnStoreChanged)); return; }
            Render();
        }

        // MARK: - Render

        private void Render()
        {
            _body.Children.Clear();
            bool busy = _store.IsLoading || _store.IsApplying;
            _lockHint.Visibility = _store.LocksPlannedDays ? Visibility.Visible : Visibility.Collapsed;
            _lockDays.IsEnabled = !busy;
            F.WorkPlan plan = _store.IsLoading ? null : _store.Plan;
            if (plan != null) _selected.IntersectWith(plan.Changes.Select(c => c.Id));
            _apply.IsEnabled = !busy && plan != null && _selected.Count > 0;
            if (!string.IsNullOrEmpty(_store.EditMessage))
            {
                _message.Text = _store.EditMessage;
                _message.Visibility = Visibility.Visible;
            }
            _apply.Opacity = _apply.IsEnabled ? 1 : 0.5;
            _selectAll.IsEnabled = !busy && plan != null;

            if (_store.IsLoading || (plan == null && _store.Error == null))
            {
                var loading = DayPlanDialogUi.Hint(_store.IsApplying ? ForecastText.Get("loading") : ForecastText.Get("loading"), 20);
                loading.FontSize = 14;
                loading.HorizontalAlignment = HorizontalAlignment.Center;
                _body.Children.Add(loading);
                return;
            }
            if (plan == null)
            {
                _body.Children.Add(new TextBlock { Text = _store.Error, Foreground = new SolidColorBrush(DayPlanDialogUi.Red), TextWrapping = TextWrapping.Wrap });
                return;
            }
            if (plan.Changes.Count == 0) _body.Children.Add(Label(ForecastText.Get("noChanges"), 13, FontWeights.Normal));

            var focused = new List<F.DueDateChange>();
            if (_focus.HasValue)
            {
                focused = _store.ChangesMovingWorkOff(_focus.Value.Day);
                DayFocusSection(focused);
            }
            var focusedIds = new HashSet<string>(focused.Select(c => c.Id));
            List<F.DueDateChange> tasks, projectDeadlines;
            ForecastGrouping.Split(plan.Changes.Where(c => !focusedIds.Contains(c.Id)), out tasks, out projectDeadlines);
            Accordion("major", ForecastText.Get("majorChanges"), tasks.Where(ForecastGrouping.IsMajor).ToList());
            Accordion("minor", ForecastText.Get("minorChanges"), tasks.Where(c => !ForecastGrouping.IsMajor(c)).ToList());
            ProjectDeadlines(projectDeadlines);
            Conflicts(plan.ReviewableConflicts);
        }

        private void DayFocusSection(List<F.DueDateChange> changes)
        {
            ForecastDayFocus.Target focus = _focus.Value;
            string day = ForecastText.DayLabel(focus.Day);
            var head = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
            var box = GroupCheck(ForecastGrouping.BulkSelectableIds(changes, _store.CanApply));
            DockPanel.SetDock(box, Dock.Left);
            head.Children.Add(box);
            head.Children.Add(Label(ForecastText.Format(ForecastText.Get("dayFocusHeading"), "<DAY>", day,
                                                        "<COUNT>", changes.Count.ToString(CultureInfo.CurrentCulture)), 14, FontWeights.SemiBold));
            _body.Children.Add(head);
            if (changes.Count == 0)
            {
                _body.Children.Add(Warning(ForecastText.Format(ForecastText.Get("dayFocusNothingToMove"), "<DAY>", day)));
                return;
            }
            ForecastDayFocus.Relief relief = _store.ReliefFor(focus);
            string freed = ForecastText.Duration(relief.FreedMinutes);
            string sentence;
            if (relief.ClearsTheDay) sentence = ForecastText.Format(ForecastText.Get("dayFocusClears"), "<FREED>", freed, "<DAY>", day);
            else
            {
                string remaining = ForecastText.Duration(relief.RemainingMinutes);
                sentence = ForecastText.Format(ForecastText.Get("dayFocusRelief"), "<FREED>", freed,
                                               "<SHORTFALL>", ForecastText.Duration(relief.ShortfallMinutes), "<DAY>", day) + " "
                           + (relief.RemainderIsPinned
                               ? ForecastText.Format(ForecastText.Get("dayFocusPinnedRemainder"), "<DAY>", day, "<REMAINING>", remaining,
                                                     "<COUNT>", relief.ImmovableCount.ToString(CultureInfo.CurrentCulture))
                               : ForecastText.Format(ForecastText.Get("dayFocusRemainder"), "<DAY>", day, "<REMAINING>", remaining));
            }
            TextBlock s = relief.ClearsTheDay ? DayPlanDialogUi.Hint(sentence, 0) : Warning(sentence);
            s.Margin = new Thickness(0, 0, 0, 6);
            _body.Children.Add(s);
            foreach (var group in ForecastGrouping.Groups(changes, _store.TaskProjectId)) _body.Children.Add(GroupCard(group));
        }

        private void Accordion(string key, string titleTemplate, List<F.DueDateChange> changes)
        {
            if (changes.Count == 0) return;
            string title = ForecastText.Format(titleTemplate, "<COUNT>", changes.Count.ToString(CultureInfo.CurrentCulture));
            if (!AccordionHeader(key, title, ForecastGrouping.BulkSelectableIds(changes, _store.CanApply))) return;
            foreach (var group in ForecastGrouping.Groups(changes, _store.TaskProjectId)) _body.Children.Add(GroupCard(group));
        }

        /// <summary>
        /// A collapsible section's heading: chevron, optional select-all box, title. The whole heading
        /// toggles - by click, or by Enter / Space when it has keyboard focus (it is a tab stop) - and tells
        /// screen readers whether it is open. Returns whether the section is open.
        /// </summary>
        private bool AccordionHeader(string key, string title, List<string> selectableIds)
        {
            bool expanded;
            if (!_expanded.TryGetValue(key, out expanded)) expanded = false;
            var head = new DockPanel { Margin = new Thickness(0, 10, 0, 6), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                                       Focusable = true, FocusVisualStyle = null };
            var chevron = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(expanded ? "M4,6 L8,10 12,6" : "M6,4 L10,8 6,12"),
                StrokeThickness = 1.6, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TraySubtitleColor");
            DockPanel.SetDock(chevron, Dock.Left);
            head.Children.Add(chevron);
            if (selectableIds != null)
            {
                var box = GroupCheck(selectableIds);
                DockPanel.SetDock(box, Dock.Left);
                head.Children.Add(box);
            }
            var label = Label(title, 14, FontWeights.SemiBold);
            head.Children.Add(label);
            AutomationProperties.SetAutomationId(head, "ReprioritiseSection_" + key);
            AutomationProperties.SetName(head, title);
            AutomationProperties.SetItemStatus(head, expanded ? "expanded" : "collapsed");
            Action<bool> toggle = byKeyboard =>
            {
                _expanded[key] = !expanded;
                // The re-render replaces the heading; hand keyboard focus to its replacement.
                _refocusSection = byKeyboard ? key : null;
                Render();
            };
            head.MouseLeftButtonUp += (s, e) => { if (e.Handled) return; e.Handled = true; toggle(false); };
            head.KeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter && e.Key != Key.Space) return;
                e.Handled = true;
                toggle(true);
            };
            // The heading draws no focus ring of its own, so underline the title while it has keyboard focus.
            head.GotKeyboardFocus += (s, e) => label.TextDecorations = TextDecorations.Underline;
            head.LostKeyboardFocus += (s, e) => label.TextDecorations = null;
            if (_refocusSection == key)
            {
                _refocusSection = null;
                head.Loaded += (s, e) => Keyboard.Focus(head);
            }
            _body.Children.Add(head);
            return expanded;
        }

        private Border GroupCard(ForecastGrouping.Group<F.DueDateChange> group)
        {
            var panel = new StackPanel();
            if (group.ProjectId != null)
            {
                var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var ids = ForecastGrouping.BulkSelectableIds(group.Rows, _store.CanApply);
                var box = GroupCheck(ids);
                box.ToolTip = ForecastText.Format(ForecastText.Get("selectAllInProject"), "<COUNT>", ids.Count.ToString(CultureInfo.CurrentCulture));
                DockPanel.SetDock(box, Dock.Left);
                head.Children.Add(box);
                head.Children.Add(Chip("#" + _store.ProjectName(group.ProjectId)));
                panel.Children.Add(head);
                int? inherited = _store.InheritedOutcome(group.ProjectId, group.Rows.Select(r => r.ItemId));
                if (inherited.HasValue)
                {
                    var strings = ToDoScreens.ToDoStrings.Load();
                    var note = DayPlanDialogUi.Hint(ForecastText.Format(ForecastText.Get("inheritedFromProject"),
                        "<PROJECT>", _store.ProjectName(group.ProjectId),
                        "<OUTCOME>", ProjectOutcomeEdit.ValueText(inherited.Value) + " · " + strings.OutcomeLabel(inherited.Value)), 0);
                    note.FontSize = 11.5;
                    note.Margin = new Thickness(0, -2, 0, 6);
                    panel.Children.Add(note);
                }
            }
            foreach (F.DueDateChange change in group.Rows) panel.Children.Add(ChangeRow(change));
            return Card(panel);
        }

        private FrameworkElement ChangeRow(F.DueDateChange change)
        {
            bool editable = _store.CanApply(change) && !_store.IsApplying;
            var row = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            var check = new CheckBox { IsChecked = _selected.Contains(change.Id), IsEnabled = editable, VerticalContentAlignment = VerticalAlignment.Top };
            var name = new Emoji.Wpf.TextBlock { Text = _store.Name(change.Kind, change.ItemId), FontSize = 13.5, FontWeight = FontWeights.Medium,
                                                 TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 0, 0) };
            name.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            check.Content = name;
            AutomationProperties.SetAutomationId(check, "ReprioritiseRow_" + change.ItemId);
            check.Checked += (s, e) => { _selected.Add(change.Id); UpdateApply(); };
            check.Unchecked += (s, e) => { _selected.Remove(change.Id); UpdateApply(); };
            row.Children.Add(check);

            var line = new WrapPanel { Margin = new Thickness(22, 3, 0, 0) };
            line.Children.Add(Small(ForecastText.Date(change.PreviousDate) + " →"));
            line.Children.Add(DateControl(change.Id, change.ProposedDate, _store.ManualDate(change.Id).HasValue, editable));
            if (change.Kind == F.ChangeKind.Task) line.Children.Add(DurationControl(change.ItemId, editable && _store.CanEditRows));
            row.Children.Add(line);

            string why;
            if (_store.ManualDate(change.Id).HasValue) why = ForecastText.Get("reasonYouChoseIt");
            else if (change.PreviousDate.HasValue && change.ProposedDate.Date < change.PreviousDate.Value.Date) why = ForecastText.Get("reasonBroughtForward");
            else why = ForecastText.Get("reasonPushedBack");
            var reason = DayPlanDialogUi.Hint(why, 2);
            reason.FontSize = 11.5;
            reason.Margin = new Thickness(22, 2, 0, 0);
            row.Children.Add(reason);
            if (change.Kind == F.ChangeKind.Task) row.Children.Add(OutcomeControl(change.ItemId, editable && _store.CanEditRows));
            if (!_store.CanApply(change))
            {
                var perm = Warning(ForecastText.Get("noPermission"));
                perm.Margin = new Thickness(22, 2, 0, 0);
                row.Children.Add(perm);
            }
            return row;
        }

        /// <summary>
        /// The row's outcome: the Mac's ForecastOutcomeSlider — the task editor's 1-5 scale and words
        /// ("4 / 5 · Can postpone a few days"), the /10 wire value. It writes when the drag ends (or on a
        /// click / arrow key), never on the steps in between: each write re-flows the list, and re-flowing
        /// mid-drag would move the row out from under the pointer.
        /// </summary>
        private FrameworkElement OutcomeControl(string taskId, bool editable)
        {
            var strings = ToDoScreens.ToDoStrings.Load();
            int step = OutcomeStep(_store.Outcome(taskId));
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 4, 0, 0) };
            var slider = new Slider
            {
                Minimum = 1, Maximum = ToDoScoring.OutcomeSteps, SmallChange = 1, LargeChange = 1, TickFrequency = 1,
                IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                Width = 150, Value = step, IsEnabled = editable, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = ForecastText.Get("outcome"),
            };
            AutomationProperties.SetAutomationId(slider, "ReprioritiseOutcome_" + taskId);
            AutomationProperties.SetName(slider, ForecastText.Get("outcome"));
            var label = new TextBlock { Text = strings.OutcomeOptionLabel(step), FontSize = 11.5, Margin = new Thickness(8, 0, 0, 0),
                                        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            AutomationProperties.SetAutomationId(label, "ReprioritiseOutcomeLabel_" + taskId);
            bool dragging = false;
            Action commit = () =>
            {
                int chosen = (int)Math.Round(slider.Value);
                if (chosen == OutcomeStep(_store.Outcome(taskId))) return;
                _refocusOutcome = slider.IsKeyboardFocusWithin ? taskId : null;
                _ = _store.SetTaskOutcomeAsync(taskId, chosen * ToDoScoring.OutcomeWireFactor);
            };
            slider.ValueChanged += (s, e) =>
            {
                label.Text = strings.OutcomeOptionLabel((int)Math.Round(slider.Value));
                if (!dragging) commit();
            };
            slider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent,
                              new System.Windows.Controls.Primitives.DragStartedEventHandler((s, e) => dragging = true));
            slider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                              new System.Windows.Controls.Primitives.DragCompletedEventHandler((s, e) => { dragging = false; commit(); }));
            if (_refocusOutcome == taskId)
            {
                _refocusOutcome = null;
                slider.Loaded += (s, e) => Keyboard.Focus(slider);
            }
            panel.Children.Add(slider);
            panel.Children.Add(label);
            return panel;
        }

        /// <summary>The 1-5 position for a /10 wire value; a legacy odd value rounds to the nearest step (Mac displayStep).</summary>
        private static int OutcomeStep(int? wire)
        {
            int effective = wire ?? F.TaskInput.UnratedOutcome;
            int step = (int)Math.Round(effective / (double)ToDoScoring.OutcomeWireFactor, MidpointRounding.AwayFromZero);
            return Math.Min(ToDoScoring.OutcomeSteps, Math.Max(1, step));
        }

        /// <summary>
        /// The estimate chip; click it to type a new one with the Day Plan's own duration grammar
        /// (QuickAddTaskParser: 45m, 1h30, a bare 45). Enter saves, Esc puts it back.
        /// </summary>
        private FrameworkElement DurationControl(string taskId, bool editable)
        {
            int? minutes = _store.DurationMinutes(taskId);
            if (_editingDuration.Contains(taskId))
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
                var field = DayPlanDialogUi.Field("ReprioritiseDurationField", minutes.HasValue ? QuickAddTaskParser.DurationLabel(minutes.Value) : "", 70);
                field.ToolTip = ForecastText.Get("durationFieldHintWindows");
                var error = new TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Red), Margin = new Thickness(8, 0, 0, 0),
                                            VerticalAlignment = VerticalAlignment.Center };
                field.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        int? typed = QuickAddTaskParser.DurationMinutesFromFieldText(field.Text);
                        if (!typed.HasValue || typed.Value <= 0) { error.Text = ForecastText.Get("durationFieldHintWindows"); return; }
                        _editingDuration.Remove(taskId);
                        _ = _store.SetDurationAsync(taskId, typed.Value);
                        Render();
                    }
                    else if (e.Key == Key.Escape) { e.Handled = true; _editingDuration.Remove(taskId); Render(); }
                };
                field.Loaded += (s, e) => { field.Focus(); field.SelectAll(); };
                panel.Children.Add(field);
                panel.Children.Add(error);
                return panel;
            }
            var chip = Chip(minutes.HasValue ? ForecastText.Duration(minutes.Value) : ForecastText.Get("noEstimate"));
            chip.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetAutomationId(chip, "ReprioritiseDuration_" + taskId);
            if (editable)
            {
                chip.Cursor = Cursors.Hand;
                chip.ToolTip = ForecastText.Get("editDuration");
                chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; _editingDuration.Add(taskId); Render(); };
            }
            return chip;
        }

        /// <summary>The proposed (or typed) date; click to type your own, with the To-do list's own date grammar.</summary>
        private FrameworkElement DateControl(string changeId, DateTime date, bool overridden, bool editable)
        {
            if (_editingDate.Contains(changeId))
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                var field = DayPlanDialogUi.Field("ReprioritiseDateField", ToDoDates.Ymd(date), 150);
                field.ToolTip = ForecastText.Get("typeADateWindows");
                var error = new TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush(DayPlanDialogUi.Red), Margin = new Thickness(8, 0, 0, 0),
                                            VerticalAlignment = VerticalAlignment.Center };
                Action commit = () =>
                {
                    DateTime? parsed = ToDoDueDateParser.Parse(field.Text, DateTime.Now, CultureInfo.CurrentCulture);
                    if (!parsed.HasValue) { error.Text = ForecastText.Get("dateNotRecognisedWindows"); return; }
                    _editingDate.Remove(changeId);
                    _store.SetManualDate(changeId, parsed.Value);
                };
                field.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { e.Handled = true; commit(); }
                    else if (e.Key == Key.Escape) { e.Handled = true; _editingDate.Remove(changeId); Render(); }
                };
                field.Loaded += (s, e) => field.Focus();
                panel.Children.Add(field);
                panel.Children.Add(error);
                return panel;
            }
            var link = new TextBlock
            {
                Text = ForecastText.Date(date), FontSize = 12.5, FontWeight = FontWeights.Medium, Margin = new Thickness(6, 0, 0, 0),
                TextDecorations = editable ? TextDecorations.Underline : null, Cursor = editable ? Cursors.Hand : Cursors.Arrow,
                ToolTip = ForecastText.Get("typeADateWindows"),
            };
            if (overridden) link.Foreground = new SolidColorBrush(DayPlanDialogUi.Orange);
            else link.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(link, "ReprioritiseDate_" + changeId);
            if (editable) link.MouseLeftButtonUp += (s, e) => { e.Handled = true; _editingDate.Add(changeId); Render(); };
            return link;
        }

        /// <summary>
        /// Changing whole PROJECTS' deadlines - kept apart from the task rows, and folded shut by default:
        /// it can list dozens of projects that need a date, and they pushed the task changes off screen.
        /// </summary>
        private void ProjectDeadlines(List<F.DueDateChange> changes)
        {
            if (changes.Count == 0) return;
            string title = ForecastText.Format(ForecastText.Get("projectDeadlineHeadingCountWindows"),
                                               "<HEADING>", ForecastText.Get("projectDeadlineHeading"),
                                               "<COUNT>", changes.Count.ToString(CultureInfo.CurrentCulture));
            // No select-all: a project's deadline is never swept up in a bulk select (the Mac's rule too).
            if (!AccordionHeader("projectDeadlines", title, null)) return;
            _body.Children.Add(DayPlanDialogUi.Hint(ForecastText.Get("projectDeadlineExplanation"), 0));
            var panel = new StackPanel();
            foreach (F.DueDateChange change in changes)
            {
                bool editable = _store.CanApply(change) && !_store.IsApplying;
                var row = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
                var check = new CheckBox { IsChecked = _selected.Contains(change.Id), IsEnabled = editable };
                var name = new TextBlock { Text = ForecastText.Format(ForecastText.Get("projectDeadlineRow"), "<PROJECT>", _store.ProjectName(change.ItemId),
                                                                      "<DATE>", ForecastText.Date(change.ProposedDate)),
                                           FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 0, 0) };
                name.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                check.Content = name;
                AutomationProperties.SetAutomationId(check, "ReprioritiseProjectRow_" + change.ItemId);
                check.Checked += (s, e) => { _selected.Add(change.Id); UpdateApply(); };
                check.Unchecked += (s, e) => { _selected.Remove(change.Id); UpdateApply(); };
                row.Children.Add(check);
                var line = new WrapPanel { Margin = new Thickness(22, 3, 0, 0) };
                line.Children.Add(Small(ForecastText.Date(change.PreviousDate) + " →"));
                line.Children.Add(DateControl(change.Id, change.ProposedDate, _store.ManualDate(change.Id).HasValue, editable));
                row.Children.Add(line);
                if (!_store.CanApply(change))
                {
                    var perm = Warning(ForecastText.Get("noPermission"));
                    perm.Margin = new Thickness(22, 2, 0, 0);
                    row.Children.Add(perm);
                }
                panel.Children.Add(row);
            }
            // The Mac's warning tint, so a whole-project change never reads as a task row.
            Border card = Card(panel);
            card.Margin = new Thickness(0, 6, 0, 10);
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0x95, 0x00));
            _body.Children.Add(card);
        }

        private void Conflicts(IReadOnlyList<F.PlanConflict> conflicts)
        {
            if (conflicts.Count == 0) return;
            _body.Children.Add(Label(ForecastText.Get("conflictsHeading"), 14, FontWeights.SemiBold, top: 14));
            _body.Children.Add(DayPlanDialogUi.Hint(ForecastText.Get("conflictsExplanation"), 2));
            foreach (var group in ForecastGrouping.ConflictGroups(conflicts, _store.TaskProjectId))
            {
                var panel = new StackPanel();
                if (group.ProjectId != null) panel.Children.Add(Chip("#" + _store.ProjectName(group.ProjectId)));
                foreach (F.PlanConflict conflict in group.Rows)
                {
                    var row = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };
                    var name = new Emoji.Wpf.TextBlock { Text = _store.Name(conflict.Kind, conflict.ItemId), FontSize = 13.5, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
                    name.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                    row.Children.Add(name);
                    var line = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
                    DateTime? current = conflict.Kind == F.ChangeKind.Task ? _store.TaskDueDate(conflict.ItemId) : _store.ProjectDueDate(conflict.ItemId);
                    line.Children.Add(Small(ForecastText.Date(current) + " →"));
                    bool canType = conflict.Reason != F.ConflictReason.MissingEstimate && !_store.IsApplying;
                    if (_editingDate.Contains(conflict.ChangeId) || !canType)
                        line.Children.Add(canType ? DateControl(conflict.ChangeId, conflict.EarliestCompletionDate ?? DateTime.Today, false, true)
                                                  : Small(" —"));
                    else
                    {
                        var edit = new TextBlock { Text = ToDoScreens.ToDoStrings.Load().EditDueDate, FontSize = 12.5, Margin = new Thickness(6, 0, 0, 0),
                                                   Foreground = new SolidColorBrush(DayPlanDialogUi.Orange), TextDecorations = TextDecorations.Underline,
                                                   Cursor = Cursors.Hand, ToolTip = ForecastText.Get("typeADateWindows") };
                        string id = conflict.ChangeId;
                        edit.MouseLeftButtonUp += (s, e) => { _editingDate.Add(id); Render(); };
                        line.Children.Add(edit);
                    }
                    row.Children.Add(line);
                    var reason = new TextBlock { Text = _store.ConflictReason(conflict), FontSize = 12, TextWrapping = TextWrapping.Wrap,
                                                 Foreground = new SolidColorBrush(DayPlanDialogUi.Red), Margin = new Thickness(0, 2, 0, 0) };
                    row.Children.Add(reason);
                    panel.Children.Add(row);
                }
                _body.Children.Add(Card(panel));
            }
        }

        // MARK: - Actions

        private void UpdateApply()
        {
            bool busy = _store.IsLoading || _store.IsApplying;
            _apply.IsEnabled = !busy && _selected.Count > 0;
            _apply.Opacity = _apply.IsEnabled ? 1 : 0.5;
        }

        private void SelectAll()
        {
            F.WorkPlan plan = _store.Plan;
            if (plan == null) return;
            foreach (string id in ForecastGrouping.BulkSelectableIds(plan.Changes, _store.CanApply)) _selected.Add(id);
            Render();
        }

        private async void Apply()
        {
            F.WorkPlan plan = _store.Plan;
            if (plan == null || _selected.Count == 0) return;
            var chosen = plan.Changes.Where(c => _selected.Contains(c.Id)).ToList();
            AppSettings.GetInstance.CapturePostHog("reprioritise-plan-applied");
            var result = await _store.ApplyAsync(chosen);
            _selected.Clear();
            _message.Text = ForecastText.Format(ForecastText.Get(result.Item2 ? "planSaved" : "planPartiallySaved"),
                                                "<COUNT>", result.Item1.ToString(CultureInfo.CurrentCulture));
            _message.Visibility = Visibility.Visible;
            Render();
        }

        // MARK: - Building blocks

        private CheckBox GroupCheck(List<string> ids)
        {
            var state = ForecastGrouping.State(ids, _selected);
            var box = new CheckBox
            {
                IsThreeState = true,
                IsChecked = state == ForecastGrouping.SelectionState.All ? true : state == ForecastGrouping.SelectionState.Some ? (bool?)null : false,
                IsEnabled = ids.Count > 0 && !_store.IsApplying,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            RoutedEventHandler toggle = (s, e) =>
            {
                e.Handled = true;
                var updated = ForecastGrouping.Toggled(ids, _selected);
                _selected.Clear();
                _selected.UnionWith(updated);
                Render();
            };
            // The state-change events, not Click: UI Automation (a screen reader) toggles without raising Click.
            box.Checked += toggle;
            box.Unchecked += toggle;
            box.Indeterminate += toggle;
            box.MouseLeftButtonUp += (s, e) => e.Handled = true;
            return box;
        }

        private static Border Card(UIElement content)
        {
            var b = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 8),
                                 Margin = new Thickness(0, 0, 0, 10), Child = content };
            b.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            b.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            return b;
        }

        private static Border Chip(string text)
        {
            var t = new Emoji.Wpf.TextBlock { Text = text, FontSize = 11.5 };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var b = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), BorderThickness = new Thickness(1),
                                 Child = t, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                                 Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0x95, 0x00)),
                                 BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x95, 0x00)) };
            return b;
        }

        private static TextBlock Label(string text, double size, FontWeight weight, double top = 0)
        {
            var t = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0),
                                    VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            return t;
        }

        private static TextBlock Small(string text)
        {
            var t = new TextBlock { Text = text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            return t;
        }

        private static TextBlock Warning(string text)
        {
            return new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(DayPlanDialogUi.Orange) };
        }
    }
}
