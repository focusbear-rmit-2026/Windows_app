using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// M7 — the Day Plan tasks panel (right column) and drag-to-associate wiring. The Windows port of
    /// the macOS <c>DayPlanTasksPanel</c> + the <c>DropTargetView</c>/<c>assignTask</c> plumbing in
    /// <c>ScheduleViewController</c>. Kept in a partial file so the large agenda code-behind stays
    /// readable. A task dragged from this panel onto a habit row nests under that habit
    /// (<c>"habit:&lt;title&gt;"</c> block key), and shows a clock badge here once time-boxed.
    /// </summary>
    public partial class HabitsDayPlan
    {
        // macOS: tasksPanelMinWidth 260 / maxWidth 600 (kTasksPanelWidth = "dayPlanPanelWidth").
        private const double MinTasksWidth = 260;
        private const double MaxTasksWidth = 600;
        private const double DefaultTasksWidth = 340;

        // Drag payload format + multi-select state.
        private const string TaskDragFormat = "FBDayPlanTaskIds";
        private readonly HashSet<string> _selectedTaskIds = new HashSet<string>();

        // Drag gesture state (drag begins only past the system threshold, from the grip).
        private Point _taskDragStart;
        private string _taskDragId;
        private bool _taskDragArmed;

        // A freshly-added task to focus + select-all so the user can type its name immediately.
        private string _focusTaskId;

        // Whether completed tasks are shown in the panel (toggled from the settings cog).
        private bool _showCompleted;

        // Whether meetings the user hid with ✕ are surfaced (struck-through) in the agenda. Persisted.
        private bool _showDismissedMeetings = RegistryManager.LoadDayPlanShowHiddenMeetings();

        private static readonly Color CTaskDropHighlight = Color.FromArgb(0x33, 0xFF, 0x95, 0x00);

        // MARK: - Panel width (resize) ----------------------------------------------------------------

        /// <summary>Restore the persisted panel width (clamped), then render the panel once on load.</summary>
        private void InitTasksPanel()
        {
            try
            {
                double w = DefaultTasksWidth;
                string saved = RegistryManager.LoadDayPlanPanelWidth();
                if (!string.IsNullOrEmpty(saved) &&
                    double.TryParse(saved, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                {
                    w = parsed;
                }
                TasksColumn.Width = new GridLength(Math.Max(MinTasksWidth, Math.Min(MaxTasksWidth, w)));
            }
            catch (Exception ex) { LogWriter.LogWrite("InitTasksPanel - " + ex.Message); }
            BtnTasksCog.ToolTip = DayPlanText.TasksSettings;   // config-driven (XAML default is a fallback)
            RenderTasksPanel();
            HydrateTasksFromBackend();   // cross-device read (Phase 4): pull tasks planned elsewhere
            FetchProjectNames();         // web/mobile projects → suggestion chips
            FetchCalendar();             // backend calendar sync → meeting blocks in the agenda
            EnsureCalendarTimer();       // keep meetings live while the Day Plan is open
        }

        /// <summary>
        /// Pull calendar meetings from the backend (Google/Microsoft sync) and re-render so they appear
        /// as meeting blocks in the agenda. Throttled inside the manager; best-effort.
        /// </summary>
        private async void FetchCalendar(bool force = false)
        {
            try
            {
                if (await DayPlanCalendarManager.Shared.RefreshAsync(force))
                    RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("FetchCalendar - " + ex.Message); }
        }

        // Polls the backend calendar sync while the Day Plan is on screen, so an event added in
        // Google/Outlook shows up on its own (backend sync lag aside) without reopening the app.
        private System.Windows.Threading.DispatcherTimer _calendarTimer;

        private void EnsureCalendarTimer()
        {
            if (_calendarTimer != null) return;
            _calendarTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(90)
            };
            _calendarTimer.Tick += (s, e) => FetchCalendar();   // manager self-throttles; re-renders only on change
            _calendarTimer.Start();
        }

        /// <summary>Pull the user's backend project list so projects made on web/mobile show as
        /// suggestions in the detail modal (they have no local task to derive them from).</summary>
        private async void FetchProjectNames()
        {
            try
            {
                List<string> names = await new FocusBearAPIClient().GetProjects();
                if (names != null) DayPlanTaskStore.Shared.RemoteProjectNames = names;
            }
            catch (Exception ex) { LogWriter.LogWrite("FetchProjectNames - " + ex.Message); }
        }

        // The day we last hydrated from the backend, so we fetch at most once per viewed day per session.
        private string _lastTaskHydrateDay = "";

        /// <summary>
        /// Pull the selected day's planned to-dos from the backend and reconcile them into the local
        /// store, so a task planned/ticked on the web or mobile shows here too (Phase 4 read half).
        /// Throttled to once per viewed day; non-mutating and best-effort (a missing endpoint just no-ops).
        /// </summary>
        private async void HydrateTasksFromBackend()
        {
            try
            {
                string dayKey = _selectedDate.ToString("yyyy-MM-dd");
                if (_lastTaskHydrateDay == dayKey) return;
                _lastTaskHydrateDay = dayKey;

                var client = new FocusBearAPIClient();
                bool changed = false;

                // 1) Pull the day-plan payload once per viewed day: tasks the backend planned onto this
                //    day (when planned_date sticks) + the dismissed meeting ids for the day.
                string raw = await client.GetDayPlanForDay(dayKey);
                if (!string.IsNullOrEmpty(raw))
                {
                    if (DayPlanTaskStore.Shared.HydrateFromDay(raw, _selectedDate)) changed = true;
                    try
                    {
                        if (Newtonsoft.Json.Linq.JObject.Parse(raw)["dismissed_event_ids"] is Newtonsoft.Json.Linq.JArray da)
                        {
                            DayPlanCalendarManager.Shared.SetDismissed(_selectedDate, da.Select(x => x.ToString()));
                            changed = true;
                        }
                    }
                    catch (Exception ex) { LogWriter.LogWrite("dismissed_event_ids parse - " + ex.Message); }
                }

                // 2) Reconcile done-state from the actual to-do list (/to-do/recent includes completed).
                //    Run EVERY time the Day Plan is shown/changed — NOT throttled per day — so a task
                //    done or re-opened on web/mobile reflects here on the next open. GetAllTasks itself
                //    self-throttles (≤15s), so this is cheap. The /day-plan/day read above is empty when
                //    the backend doesn't persist planned_date, so this is the reliable done sync.
                await client.GetAllTasks();   // refreshes appSettings.sAllTasks (debounced ≤15s)
                if (DayPlanTaskStore.Shared.ReconcileStatusFrom(appSettings.sAllTasks))
                    changed = true;

                // Only re-render if the day we reconciled is still the one on screen.
                if (changed && _selectedDate.ToString("yyyy-MM-dd") == dayKey)
                    RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("HydrateTasksFromBackend - " + ex.Message); }
        }

        /// <summary>Persist the new panel width after the user finishes dragging the splitter.</summary>
        private void TasksSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                double w = TasksColumn.ActualWidth;
                w = Math.Max(MinTasksWidth, Math.Min(MaxTasksWidth, w));
                TasksColumn.Width = new GridLength(w);
                RegistryManager.SaveDayPlanPanelWidth(w.ToString("0", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { LogWriter.LogWrite("TasksSplitter_DragCompleted - " + ex.Message); }
        }

        // MARK: - Panel render ------------------------------------------------------------------------

        /// <summary>Rebuild the tasks list (panel column) for the selected day.</summary>
        private void RenderTasksPanel()
        {
            if (TasksListStack == null) return;   // XAML not yet initialised (defensive)
            try
            {
                TasksTitle.Text = IsViewingToday
                    ? DayPlanText.TasksForToday
                    : DayPlanText.Fmt(DayPlanText.TasksForDay,
                        _selectedDate.ToString("ddd d MMM", CultureInfo.CurrentCulture));

                TasksListStack.Children.Clear();

                List<DayPlanTask> stored = DayPlanTaskStore.Shared.Tasks(_selectedDate);
                // Completed tasks hidden unless the user opts in via the cog (Phase 2).
                List<DayPlanTask> all = _showCompleted ? stored : stored.Where(t => !t.Done).ToList();

                // Drop selections that point at tasks no longer present.
                _selectedTaskIds.IntersectWith(stored.Select(t => t.Id));

                int remainingMins = all.Where(t => !t.Done).Sum(t => t.DurationMinutes);
                TasksTotal.Text = remainingMins > 0
                    ? DayPlanText.Fmt(DayPlanText.TasksTotalToDo, Hm(remainingMins))
                    : "";

                if (all.Count == 0)
                {
                    TasksListStack.Children.Add(new TextBlock
                    {
                        Text = DayPlanText.TasksEmpty,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(2, 8, 2, 0),
                        Foreground = SecBrush()
                    });
                    return;
                }

                // Group by project in FIRST-APPEARANCE order (matches macOS; the list order is also what
                // drag-reorder edits): each project (a header with total + "＋") then its tasks, and the
                // "No project" tasks last under a plain header (only shown when there are projects).
                var order = new List<string>();
                var groups = new Dictionary<string, List<DayPlanTask>>(StringComparer.OrdinalIgnoreCase);
                var noProject = new List<DayPlanTask>();
                foreach (DayPlanTask t in all)
                {
                    if (string.IsNullOrWhiteSpace(t.Project)) { noProject.Add(t); continue; }
                    string p = t.Project.Trim();
                    if (!groups.TryGetValue(p, out List<DayPlanTask> g)) { g = new List<DayPlanTask>(); groups[p] = g; order.Add(p); }
                    g.Add(t);
                }

                foreach (string p in order)
                {
                    List<DayPlanTask> g = groups[p];
                    int projMins = g.Where(t => !t.Done).Sum(t => t.DurationMinutes);
                    TasksListStack.Children.Add(BuildProjectHeader(p, projMins));
                    foreach (DayPlanTask t in g)
                        TasksListStack.Children.Add(BuildPanelTaskRow(t));
                }

                if (noProject.Count > 0)
                {
                    if (order.Count > 0)   // only label the loose tasks once there are projects to distinguish from
                        TasksListStack.Children.Add(BuildPlainHeader(DayPlanText.TasksNoProject));
                    foreach (DayPlanTask t in noProject)
                        TasksListStack.Children.Add(BuildPanelTaskRow(t));
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("RenderTasksPanel - " + ex.Message); }
        }

        /// <summary>A project grouping header: name · total minutes · an accent "＋" that adds a task
        /// straight into this project (macOS ProjectHeaderView's add button).</summary>
        private Border BuildProjectHeader(string project, int minutes)
        {
            var grid = new Grid { Margin = new Thickness(2, 10, 2, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // total
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // add button
            var name = new TextBlock
            {
                Text = project,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FgBrush(),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            var total = new TextBlock
            {
                Text = minutes > 0 ? Hm(minutes) : "",
                FontSize = 11,
                Foreground = SecBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(total, 1);
            grid.Children.Add(total);

            // Accent "＋" — add a new task already assigned to this project (after the duration, per macOS).
            var add = new Button
            {
                Content = new TextBlock { Text = "＋", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(COrange) },
                Width = 24,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanText.TasksAddToProject
            };
            add.Click += (s, e) => AddTaskInProject(project);
            Grid.SetColumn(add, 2);
            grid.Children.Add(add);

            return new Border
            {
                Child = grid,
                BorderBrush = new SolidColorBrush(WithAlpha(CGray, 0.4)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 3)
            };
        }

        /// <summary>A plain (non-actionable) sub-header, e.g. "No project" for the loose tasks.</summary>
        private TextBlock BuildPlainHeader(string text)
        {
            return new TextBlock
            {
                Text = "▾ " + text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = SecBrush(),
                Margin = new Thickness(2, 10, 2, 3)
            };
        }

        private void AddTaskInProject(string project)
        {
            DayPlanTask task = DayPlanTaskStore.Shared.AddInProject(project, _selectedDate);
            _focusTaskId = task.Id;   // focus the new row so the user can type its name immediately
            RenderTasksPanel();
        }

        private void BtnTasksCog_Click(object sender, RoutedEventArgs e)
        {
            int doneCount = DayPlanTaskStore.Shared.Tasks(_selectedDate).Count(t => t.Done);
            var menu = new ContextMenu();
            var showCompleted = new MenuItem
            {
                Header = DayPlanText.Fmt(DayPlanText.TasksShowCompletedCount, doneCount),
                IsCheckable = true,
                IsChecked = _showCompleted
            };
            showCompleted.Click += (s, ev) => { _showCompleted = !_showCompleted; RenderTasksPanel(); };
            menu.Items.Add(showCompleted);

            // "Show hidden meetings" — only when the day actually has one hidden with ✕.
            int hiddenMeetings = DayPlanCalendarManager.Shared.MeetingsFor(_selectedDate, true).Count(m => m.IsDismissed);
            if (hiddenMeetings > 0)
            {
                var showDismissed = new MenuItem
                {
                    Header = DayPlanText.Fmt(DayPlanText.MeetingsShowDismissedCount, hiddenMeetings),
                    IsCheckable = true,
                    IsChecked = _showDismissedMeetings
                };
                showDismissed.Click += (s, ev) =>
                {
                    _showDismissedMeetings = !_showDismissedMeetings;
                    RegistryManager.SaveDayPlanShowHiddenMeetings(_showDismissedMeetings);
                    RenderDayPlan();
                };
                menu.Items.Add(showDismissed);
            }

            menu.PlacementTarget = sender as UIElement;
            menu.IsOpen = true;
        }

        /// <summary>Re-render both the agenda (nested tasks) and the panel after a task change.</summary>
        private void RefreshTasksAndAgenda()
        {
            RenderDayPlan();       // rebuilds nested-under-habit rows
            RenderTasksPanel();    // rebuilds the loose panel list
        }

        /// <summary>
        /// One panel task row — the macOS <c>makeRow</c>: drag grip · checkbox · name · [clock] · Nm.
        /// The grip is the drag source (so clicking the name/checkbox never starts a drag); a selected
        /// row is tinted, and Ctrl/Shift-clicking the name toggles it into the multi-drag selection.
        /// </summary>
        private Border BuildPanelTaskRow(DayPlanTask task)
        {
            // Importance ("outcome") band tint for the whole row + a high-contrast text colour on it.
            Color? bandColor = DayPlanTaskVisuals.OutcomeColor(task.Outcome);
            Color? fgColor = DayPlanTaskVisuals.OutcomeForeground(task.Outcome);
            Brush textBrush = task.Done ? SecBrush() : (fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : FgBrush());
            Brush subBrush = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : SecBrush();

            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // grip
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // clock
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // duration
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // details

            // Drag grip (≡). A Border with a solid (transparent) fill so the WHOLE 22×26 area is
            // hit-testable — a bare glyph is only hittable on its pixels, which made grabbing a drag
            // flaky. Only the grip starts a drag, so name/checkbox interaction is unaffected.
            var grip = new Border
            {
                Width = 22,
                Height = 26,
                Background = Brushes.Transparent,   // hit-testable across the whole handle
                Cursor = Cursors.SizeAll,
                ToolTip = DayPlanText.TasksDragHint,
                Child = new TextBlock
                {
                    Text = "⣿",   // dotted grip; contrast colour so it stays visible on importance bands
                    FontSize = 13,
                    Foreground = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : FgBrush(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grip.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _taskDragStart = e.GetPosition(null);
                _taskDragId = task.Id;
                _taskDragArmed = true;
                grip.CaptureMouse();   // keep receiving MoveMove even if the pointer briefly leaves the grip
            };
            grip.PreviewMouseMove += Grip_PreviewMouseMove;
            grip.PreviewMouseLeftButtonUp += (s, e) =>
            {
                _taskDragArmed = false; _taskDragId = null;
                if (grip.IsMouseCaptured) grip.ReleaseMouseCapture();
            };
            Grid.SetColumn(grip, 0);
            grid.Children.Add(grip);

            // Done checkbox. A panel task ticked done just leaves the (completed-hidden) list, so only
            // the panel needs rebuilding — not the whole agenda.
            var check = new CheckBox
            {
                IsChecked = task.Done,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            check.Checked += (s, e) => { DayPlanTaskStore.Shared.SetDone(task.Id, true, _selectedDate); RenderTasksPanel(); };
            check.Unchecked += (s, e) => { DayPlanTaskStore.Shared.SetDone(task.Id, false, _selectedDate); RenderTasksPanel(); };
            Grid.SetColumn(check, 1);
            grid.Children.Add(check);

            // Name. Active tasks get an inline-editable field (caret + focus border). Completed tasks
            // get a read-only, struck-through label in a CONTRAST colour — the old code forced grey,
            // which vanished on the grey importance band (theme-independent, so invisible in both modes).
            FrameworkElement nameField;
            if (task.Done)
            {
                nameField = new TextBlock
                {
                    Text = task.Title,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    TextDecorations = TextDecorations.Strikethrough,
                    Foreground = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : FgBrush(),
                    Opacity = 0.8,   // dimmed to read as "done" while staying legible
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                var name = new TextBox
                {
                    Text = task.Title,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Transparent,
                    Background = Brushes.Transparent,
                    Foreground = textBrush,
                    CaretBrush = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : FgBrush(),
                    FontSize = 13,
                    Padding = new Thickness(3, 1, 3, 1),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    AllowDrop = false   // let a reorder drop fall through to the row (TextBox eats it by default)
                };
                name.GotKeyboardFocus += (s, e) =>
                {
                    name.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0x80));
                    name.BorderBrush = new SolidColorBrush(COrange);
                };
                name.LostKeyboardFocus += (s, e) =>
                {
                    name.Background = Brushes.Transparent;
                    name.BorderBrush = Brushes.Transparent;
                    CommitTaskName(task, name.Text);
                };
                name.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { CommitTaskName(task, name.Text); Keyboard.ClearFocus(); e.Handled = true; }
                    else if (e.Key == Key.Escape) { name.Text = task.Title; Keyboard.ClearFocus(); e.Handled = true; }
                };
                name.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        ToggleTaskSelection(task.Id);
                        e.Handled = true;
                    }
                };
                // A just-added task: focus its field + select the placeholder so the user types straight away.
                if (task.Id == _focusTaskId)
                {
                    _focusTaskId = null;
                    name.Loaded += (s, e) => { name.Focus(); name.SelectAll(); };
                }
                nameField = name;
            }

            // Name column = the name plus, when the task has sub-tasks, a "☑ done/total" line beneath
            // it. When all are done it goes green — but only on a default (uncoloured) row; on a coloured
            // row the green would be invisible against the fill, so it follows the row's contrast colour
            // (like the title/time do). Clicking the count opens the detail modal.
            var nameColumn = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            nameColumn.Children.Add(nameField);
            List<DayPlanSubtask> subs = task.SubtaskList;
            if (subs.Count > 0)
            {
                bool allDone = subs.All(x => x.Done);
                var count = new TextBlock
                {
                    Text = "☑ " + subs.Count(x => x.Done) + "/" + subs.Count,
                    FontSize = 10,
                    Margin = new Thickness(4, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    Foreground = (allDone && !fgColor.HasValue) ? DoneGreenBrush() : subBrush
                };
                count.MouseLeftButtonUp += (s, e) => OpenTaskModal(task.Id);
                nameColumn.Children.Add(count);
            }
            Grid.SetColumn(nameColumn, 2);
            grid.Children.Add(nameColumn);

            // Clock badge on a time-boxed (already-assigned) task. A plain TextBlock renders the emoji
            // as a monochrome black glyph (invisible in dark mode), so draw a small themed vector clock
            // whose stroke follows the row's contrast/secondary colour.
            if (task.BlockKey != null)
            {
                Brush clockBrush = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : SecBrush();
                var clock = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M8,8 m-6,0 a6,6 0 1,0 12,0 a6,6 0 1,0 -12,0 M8,4 L8,8 L11,10"),
                    Stroke = clockBrush,
                    StrokeThickness = 1.3,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Fill = Brushes.Transparent,
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = DayPlanText.TasksTimeBoxed
                };
                Grid.SetColumn(clock, 3);
                grid.Children.Add(clock);
            }

            // Duration (edit via the detail modal).
            var dur = new TextBlock
            {
                Text = task.DurationMinutes + "m",
                FontSize = 12,
                Foreground = fgColor.HasValue ? new SolidColorBrush(WithAlpha(fgColor.Value, 0.85)) : SecBrush(),
                MinWidth = 34,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(dur, 4);
            grid.Children.Add(dur);

            // Details button (⋯) — opens the task detail modal (notes, sub-tasks, importance, move,
            // remove). A larger, clearer "more options" glyph than the old tiny ⓘ.
            var details = new Button
            {
                Content = new TextBlock
                {
                    Text = "⋯",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : SecBrush(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -6, 0, 0)   // the ⋯ glyph sits low; nudge it up to centre
                },
                Width = 26,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanText.TasksDetails
            };
            details.Click += (s, e) => OpenTaskModal(task.Id);
            Grid.SetColumn(details, 5);
            grid.Children.Add(details);

            // Row background: selection highlight wins; else the importance band tint; else transparent.
            Brush rowBg = _selectedTaskIds.Contains(task.Id)
                ? new SolidColorBrush(WithAlpha(COrange, 0.15))
                : (bandColor.HasValue ? new SolidColorBrush(bandColor.Value) : Brushes.Transparent);

            var row = new Border
            {
                Padding = new Thickness(6, 6, 6, 6),
                CornerRadius = new CornerRadius(4),
                Background = rowBg,
                Child = grid
            };

            // Reorder: each row is also a drop target for a dragged task (or selection). Dropping the
            // dragged task here moves it to just before this one (adopting this row's project group).
            row.AllowDrop = true;
            row.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(TaskDragFormat))
                {
                    e.Effects = DragDropEffects.Move;
                    row.BorderBrush = new SolidColorBrush(COrange);   // insertion indicator above the row
                    row.BorderThickness = new Thickness(0, 2, 0, 0);
                }
                else e.Effects = DragDropEffects.None;
                e.Handled = true;
            };
            row.DragLeave += (s, e) => { row.BorderThickness = new Thickness(0); };
            row.Drop += (s, e) => OnTaskReorderDrop(task.Id, row, e);
            return row;
        }

        private void OnTaskReorderDrop(string targetId, Border row, DragEventArgs e)
        {
            row.BorderThickness = new Thickness(0);
            if (!e.Data.GetDataPresent(TaskDragFormat)) return;
            try
            {
                string raw = e.Data.GetData(TaskDragFormat) as string ?? "";
                foreach (string id in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    DayPlanTaskStore.Shared.MoveTaskBefore(id, targetId, _selectedDate);
                _selectedTaskIds.Clear();
                RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("OnTaskReorderDrop - " + ex.Message); }
            e.Handled = true;
        }

        /// <summary>Open the task detail modal (edit fields / sub-tasks / move / remove), then refresh.</summary>
        private void OpenTaskModal(string taskId)
        {
            try
            {
                var modal = new Focus_Bear.Habits.DayPlan.DayPlanTaskDetailWindow(taskId, _selectedDate)
                {
                    Owner = _parentWindow ?? Window.GetWindow(this)
                };
                bool? changed;
                if (_parentWindow != null) _parentWindow.GridShade.Visibility = Visibility.Visible;
                try { changed = modal.ShowDialog(); }
                finally { if (_parentWindow != null) _parentWindow.GridShade.Visibility = Visibility.Collapsed; }
                if (changed == true) RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("OpenTaskModal - " + ex.Message); }
        }

        private void CommitTaskName(DayPlanTask task, string newTitle)
        {
            newTitle = (newTitle ?? "").Trim();
            if (string.IsNullOrEmpty(newTitle) || newTitle == task.Title) return;
            task.Title = newTitle;
            DayPlanTaskStore.Shared.Update(task, _selectedDate);
        }

        private void ToggleTaskSelection(string id)
        {
            if (!_selectedTaskIds.Remove(id)) _selectedTaskIds.Add(id);
            RenderTasksPanel();
        }

        // MARK: - Drag source -------------------------------------------------------------------------

        private void Grip_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_taskDragArmed || _taskDragId == null || e.LeftButton != MouseButtonState.Pressed) return;
            Point pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _taskDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _taskDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            // Drag the whole selection if the gripped task is part of it; otherwise just this one.
            List<string> ids = _selectedTaskIds.Contains(_taskDragId)
                ? _selectedTaskIds.ToList()
                : new List<string> { _taskDragId };

            _taskDragArmed = false;
            // Release the capture we took on mouse-down; DoDragDrop must own the mouse for its own loop.
            if (sender is UIElement src && src.IsMouseCaptured) src.ReleaseMouseCapture();
            try
            {
                var data = new DataObject(TaskDragFormat, string.Join(",", ids));
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
            }
            catch (Exception ex) { LogWriter.LogWrite("Task drag - " + ex.Message); }
            finally { _taskDragId = null; }
        }

        // MARK: - Drop targets (habit rows) -----------------------------------------------------------

        /// <summary>
        /// Wrap a habit row as a drop target keyed <c>"habit:&lt;title&gt;"</c> and append any tasks
        /// already nested under it. Called from BuildGroupCard for every habit line.
        /// </summary>
        private void AddHabitRowWithTasks(StackPanel inner, UIElement rowElement, ScheduleBlock h)
        {
            string key = "habit:" + (h.Title ?? "");
            var host = new Border { Background = Brushes.Transparent, AllowDrop = true, Child = rowElement };
            host.DragEnter += (s, e) => OnHabitDragOver(host, e);
            host.DragOver += (s, e) => OnHabitDragOver(host, e);
            host.DragLeave += (s, e) => { host.Background = Brushes.Transparent; };
            host.Drop += (s, e) => OnHabitDrop(host, key, e);
            inner.Children.Add(host);

            foreach (DayPlanTask t in DayPlanTaskStore.Shared.TasksAssignedTo(key, _selectedDate))
                inner.Children.Add(BuildNestedTaskRow(t));
        }

        private void OnHabitDragOver(Border host, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(TaskDragFormat))
            {
                e.Effects = DragDropEffects.Move;
                host.Background = new SolidColorBrush(CTaskDropHighlight);
            }
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void OnHabitDrop(Border host, string key, DragEventArgs e)
        {
            host.Background = Brushes.Transparent;
            if (!e.Data.GetDataPresent(TaskDragFormat)) return;
            try
            {
                string raw = e.Data.GetData(TaskDragFormat) as string ?? "";
                foreach (string id in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    DayPlanTaskStore.Shared.SetBlockKey(key, id, _selectedDate);
                _selectedTaskIds.Clear();
                RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("OnHabitDrop - " + ex.Message); }
            e.Handled = true;
        }

        // MARK: - Drop targets (deep-work blocks) -----------------------------------------------------

        /// <summary>Unix-epoch seconds for a block's start (matches the macOS "deepwork:&lt;epoch&gt;" key).</summary>
        private static long ToEpoch(DateTime dt) =>
            new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local)).ToUnixTimeSeconds();

        /// <summary>The block key for a deep-work sprint: <c>"deepwork:&lt;start-epoch&gt;"</c>.</summary>
        private static string DeepWorkKey(ScheduleBlock b) => "deepwork:" + ToEpoch(b.Start);

        private static long? DeepWorkAnchor(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("deepwork:")) return null;
            return long.TryParse(key.Substring("deepwork:".Length), out long v) ? v : (long?)null;
        }

        /// <summary>
        /// Assign each deep-work task to the nearest sprint in the current render — the sprint whose
        /// time range holds the task's anchor epoch, else the closest by start. Mirrors macOS
        /// <c>deepWorkTasksByOrdinal</c>, so a task keeps its block even as sprint start times drift
        /// between renders (the engine recomputes them from "now").
        /// </summary>
        private Dictionary<ScheduleBlock, List<DayPlanTask>> DeepWorkTasksByBlock(List<ScheduleBlock> dwBlocks)
        {
            var map = new Dictionary<ScheduleBlock, List<DayPlanTask>>();
            foreach (ScheduleBlock b in dwBlocks) map[b] = new List<DayPlanTask>();
            if (dwBlocks.Count == 0) return map;

            foreach (DayPlanTask task in DayPlanTaskStore.Shared.Tasks(_selectedDate)
                         .Where(t => t.BlockKey != null && t.BlockKey.StartsWith("deepwork")))
            {
                long? anchor = DeepWorkAnchor(task.BlockKey);
                ScheduleBlock best = dwBlocks[0];
                double bestScore = double.MaxValue;
                foreach (ScheduleBlock b in dwBlocks)
                {
                    double score;
                    if (anchor.HasValue)
                    {
                        long s = ToEpoch(b.Start), e = ToEpoch(b.End);
                        score = (anchor.Value >= s && anchor.Value <= e) ? 0 : Math.Min(Math.Abs(anchor.Value - s), Math.Abs(anchor.Value - e));
                    }
                    else score = 0;   // legacy "deepwork" with no anchor → first sprint
                    if (score < bestScore) { bestScore = score; best = b; }
                }
                map[best].Add(task);
            }
            return map;
        }

        /// <summary>
        /// Add a deep-work card as a drop target (key <c>"deepwork:&lt;epoch&gt;"</c>) and nest its
        /// assigned tasks beneath it — the deep-work equivalent of <see cref="AddHabitRowWithTasks"/>.
        /// </summary>
        private void AddDeepWorkCardWithTasks(ScheduleBlock block, Dictionary<ScheduleBlock, List<DayPlanTask>> dwTasks)
        {
            string key = DeepWorkKey(block);
            UIElement card = BuildNonHabitCard(block);
            var host = new Border { Background = Brushes.Transparent, AllowDrop = true, Child = card };
            host.DragEnter += (s, e) => OnHabitDragOver(host, e);
            host.DragOver += (s, e) => OnHabitDragOver(host, e);
            host.DragLeave += (s, e) => { host.Background = Brushes.Transparent; };
            host.Drop += (s, e) => OnHabitDrop(host, key, e);
            DayPlanStack.Children.Add(host);

            if (dwTasks.TryGetValue(block, out List<DayPlanTask> tasks) && tasks.Count > 0)
            {
                foreach (DayPlanTask t in tasks)
                    DayPlanStack.Children.Add(BuildNestedTaskRow(t));
                // Small gap so the last nested task doesn't butt up against the next card (e.g. the
                // evening routine box) — the nested rows are added straight to the agenda stack.
                DayPlanStack.Children.Add(new Border { Height = 8 });
            }
        }

        /// <summary>The block key for a calendar meeting: <c>"event:&lt;eventID&gt;"</c> (matches macOS).</summary>
        private static string MeetingKey(ScheduleBlock b) => "event:" + (b.EventID ?? "");

        /// <summary>
        /// Add a meeting card as a drop target (key <c>"event:&lt;id&gt;"</c>) and nest its assigned tasks
        /// beneath it — time-box a task onto a meeting, like deep-work sprints and habits.
        /// </summary>
        private void AddMeetingCardWithTasks(ScheduleBlock block)
        {
            string key = MeetingKey(block);
            bool dismissed = block.IsDismissed;
            UIElement card = BuildNonHabitCard(block);
            if (dismissed) card.Opacity = 0.45;   // shown only via "Show hidden meetings"; fade it

            // Overlay a colour swatch + a hide (✕) / restore (↺) button at the card's top-right corner.
            var overlay = new Grid();
            overlay.Children.Add(card);
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 6, 0)
            };
            if (!dismissed) actions.Children.Add(BuildColourSwatch(block));   // recolour a visible meeting
            var toggle = new Button
            {
                Content = dismissed ? "↺" : "✕",
                FontSize = 12,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = SecBrush(),
                Cursor = Cursors.Hand,
                Padding = new Thickness(4, 0, 4, 0),
                Template = BorderlessButtonTemplate(),
                ToolTip = dismissed ? DayPlanText.MeetingRestore : DayPlanText.MeetingDismiss,
                VerticalAlignment = VerticalAlignment.Center
            };
            toggle.Click += (s, e) =>
            {
                DayPlanCalendarManager.Shared.SetDismissed(block.EventID, _selectedDate, !dismissed);
                RenderDayPlan();
            };
            actions.Children.Add(toggle);
            overlay.Children.Add(actions);

            var host = new Border { Background = Brushes.Transparent, AllowDrop = true, Child = overlay };
            host.DragEnter += (s, e) => OnHabitDragOver(host, e);
            host.DragOver += (s, e) => OnHabitDragOver(host, e);
            host.DragLeave += (s, e) => { host.Background = Brushes.Transparent; };
            host.Drop += (s, e) => OnHabitDrop(host, key, e);
            DayPlanStack.Children.Add(host);

            List<DayPlanTask> tasks = DayPlanTaskStore.Shared.TasksAssignedTo(key, _selectedDate);
            if (tasks.Count > 0)
            {
                foreach (DayPlanTask t in tasks)
                    DayPlanStack.Children.Add(BuildNestedTaskRow(t));
                DayPlanStack.Children.Add(new Border { Height = 8 });
            }
        }

        // The colour swatches offered for a meeting (the first is the default blue). Hex so they persist
        // to the backend unchanged; rendering parses them via ColorFromHex.
        private static readonly string[] MeetingColours =
        {
            "#007AFF", // blue (default)
            "#FF3B30", // red
            "#FF9500", // orange
            "#FFCC00", // yellow
            "#34C759", // green
            "#30B0C7", // teal
            "#5856D6", // indigo
            "#AF52DE", // purple
            "#FF2D55", // pink
        };

        /// <summary>A small colour dot on a meeting card; clicking it opens the palette popup.</summary>
        private UIElement BuildColourSwatch(ScheduleBlock block)
        {
            var dot = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(ColorFromHex(block.Colour, CBlue)),
                BorderBrush = SecBrush(),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = DayPlanText.MeetingColour
            };
            dot.MouseLeftButtonUp += (s, e) => ShowColourPicker(block, dot);
            return dot;
        }

        /// <summary>Palette popup: pick a swatch to recolour the meeting, or "Default" to reset it.</summary>
        private void ShowColourPicker(ScheduleBlock block, UIElement anchor)
        {
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = anchor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var swatches = new WrapPanel { Width = 152 };
            foreach (string hex in MeetingColours)
            {
                bool selected = string.Equals(hex, block.Colour, StringComparison.OrdinalIgnoreCase);
                var sw = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = new SolidColorBrush(ColorFromHex(hex, CBlue)),
                    BorderBrush = FgBrush(),
                    BorderThickness = new Thickness(selected ? 2 : 0),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand
                };
                string chosen = hex;
                sw.MouseLeftButtonUp += (s, e) => PickColour(block, chosen, popup);
                swatches.Children.Add(sw);
            }

            var reset = new Button
            {
                Content = new TextBlock { Text = DayPlanText.MeetingColourDefault, Margin = new Thickness(2, 3, 2, 3) },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = SecBrush(),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate(),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(4, 2, 0, 0)
            };
            reset.Click += (s, e) => PickColour(block, null, popup);

            var stack = new StackPanel { Margin = new Thickness(8) };
            stack.Children.Add(swatches);
            stack.Children.Add(reset);

            popup.Child = new Border
            {
                Background = (Application.Current.TryFindResource("BackgroundColor") as Brush) ?? Brushes.White,
                BorderBrush = SecBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = stack
            };
            popup.IsOpen = true;
        }

        private void PickColour(ScheduleBlock block, string hex, System.Windows.Controls.Primitives.Popup popup)
        {
            DayPlanCalendarManager.Shared.SetColour(block.EventID, hex);   // null hex = reset to default
            if (popup != null) popup.IsOpen = false;
            RenderDayPlan();
        }

        /// <summary>A task nested under a habit: indented, checkbox · name · Nm · ↩ (send back to panel).</summary>
        private UIElement BuildNestedTaskRow(DayPlanTask task)
        {
            var grid = new Grid { Margin = new Thickness(30, 1, 6, 1) };   // indent under the habit
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                IsChecked = task.Done,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            check.Checked += (s, e) => { DayPlanTaskStore.Shared.SetDone(task.Id, true, _selectedDate); RefreshTasksAndAgenda(); };
            check.Unchecked += (s, e) => { DayPlanTaskStore.Shared.SetDone(task.Id, false, _selectedDate); RefreshTasksAndAgenda(); };
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);

            var name = new TextBlock
            {
                Text = task.Title,
                FontSize = 13,
                Foreground = task.Done ? SecBrush() : FgBrush(),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 40,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (task.Done) name.TextDecorations = TextDecorations.Strikethrough;
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var dur = new TextBlock
            {
                Text = task.DurationMinutes + "m",
                FontSize = 12,
                Foreground = SecBrush(),
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dur, 2);
            grid.Children.Add(dur);

            var back = new Button
            {
                Content = "↩",   // ↩
                FontSize = 13,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = SecBrush(),
                Cursor = Cursors.Hand,
                Padding = new Thickness(4, 0, 4, 0),
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanText.TasksReturnToPanel
            };
            back.Click += (s, e) =>
            {
                DayPlanTaskStore.Shared.SetBlockKey(null, task.Id, _selectedDate);
                RefreshTasksAndAgenda();
            };
            Grid.SetColumn(back, 3);
            grid.Children.Add(back);

            return new Border { Child = grid };
        }

        // MARK: - Add buttons -------------------------------------------------------------------------

        private void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            var task = new DayPlanTask(DayPlanText.TasksNamePlaceholder, 15);
            DayPlanTaskStore.Shared.Add(task, _selectedDate);
            _focusTaskId = task.Id;   // focus + select the new row so the user can type immediately
            RenderTasksPanel();
        }

        private void BtnAddFromList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Focus_Bear.Habits.DayPlan.DayPlanAddTasksWindow(_selectedDate)
                {
                    Owner = _parentWindow ?? Window.GetWindow(this)
                };
                bool? ok;
                if (_parentWindow != null) _parentWindow.GridShade.Visibility = Visibility.Visible;   // grid shade behind the popup
                try { ok = picker.ShowDialog(); }
                finally { if (_parentWindow != null) _parentWindow.GridShade.Visibility = Visibility.Collapsed; }
                if (ok == true) RenderTasksPanel();
            }
            catch (Exception ex) { LogWriter.LogWrite("BtnAddFromList_Click - " + ex.Message); }
        }

        // MARK: - helpers -----------------------------------------------------------------------------

        /// <summary>"1h 30m" / "45m" — the macOS <c>hm</c> compact-duration format.</summary>
        private static string Hm(int minutes)
        {
            if (minutes < 60) return minutes + "m";
            int h = minutes / 60, m = minutes % 60;
            return m == 0 ? h + "h" : h + "h " + m + "m";
        }
    }
}
