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
                // The 45-day window is ~1.7 MB and a backend provider sync per call, and this runs every 90 s while the
                // Day Plan is open: reload it every 15 minutes, and just the next week (plus the viewed day) otherwise.
                var calendar = DayPlanCalendarManager.Shared;
                bool wide = !calendar.IsWindowFresh(DayPlanCalendarManager.DefaultWindowDays, DayPlanCalendarManager.WideWindowMaxAgeSeconds);
                if (await calendar.RefreshAsync(force || wide, viewedDay: _selectedDate,
                        windowDays: wide ? DayPlanCalendarManager.DefaultWindowDays : DayPlanCalendarManager.NearWindowDays))
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
            // Only while the Day Plan is on screen: the page is kept after it's left (and the window hides to the tray),
            // and this read is a 7-45 day calendar download. Coming back through Refresh reads at once; otherwise the next tick does.
            _calendarTimer.Tick += (s, e) => { if (IsVisible) FetchCalendar(); };   // manager self-throttles; re-renders only on change
            _calendarTimer.Start();
        }

        /// <summary>Pull the user's backend project list so projects made on web/mobile show as
        /// suggestions in the detail modal (they have no local task to derive them from).</summary>
        private async void FetchProjectNames()
        {
            try
            {
                // id -> name, so a to-do linked by project_id alone is named (report 2026-09-25_09-44-27).
                Dictionary<string, string> byId = await new FocusBearAPIClient().GetProjectsById();
                if (byId != null && DayPlanTaskStore.Shared.SetRemoteProjects(byId)) RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("FetchProjectNames - " + ex.Message); }
        }

        // The day we last hydrated from the backend, so we fetch at most once per viewed day per session.
        private string _lastTaskHydrateDay = "";
        private DateTime _lastStatusSyncUtc = DateTime.MinValue;

        /// <summary>
        /// Pull the selected day's planned to-dos from the backend and reconcile them into the local
        /// store, so a task planned/ticked on the web or mobile shows here too (Phase 4 read half).
        /// Throttled to once per viewed day; non-mutating and best-effort (a missing endpoint just no-ops).
        /// </summary>
        private async void HydrateTasksFromBackend()
        {
            try
            {
                // The day asked about, kept across the awaits: the user can page to another day before the replies
                // come back, and that day's tasks and hidden meetings must not be filed under the new one.
                DateTime day = _selectedDate.Date;
                string dayKey = FocusBear.Core.Schedule.DayPlanDayKey.For(day);
                bool firstLoadOfDay = _lastTaskHydrateDay != dayKey;
                // The done-sync below runs on every open; only a burst of opens is collapsed.
                if (!firstLoadOfDay && (DateTime.UtcNow - _lastStatusSyncUtc).TotalSeconds < 10) return;
                _lastTaskHydrateDay = dayKey;
                _lastStatusSyncUtc = DateTime.UtcNow;

                var client = new FocusBearAPIClient();
                bool changed = false;

                // 1) Pull the day-plan payload once per viewed day: tasks the backend planned onto this
                //    day (when planned_date sticks) + the dismissed meeting ids for the day.
                string raw = firstLoadOfDay ? await client.GetDayPlanForDay(dayKey) : null;
                if (!string.IsNullOrEmpty(raw))
                {
                    // Only a past day takes its tasks from the payload (Mac loadAndHydrate): today and later are filed
                    // by due date below, and the legacy planned_date pointers would bring stale copies onto them.
                    if (day < DateTime.Today && DayPlanTaskStore.Shared.HydrateFromDay(raw, day)) changed = true;
                    try
                    {
                        if (Newtonsoft.Json.Linq.JObject.Parse(raw)["dismissed_event_ids"] is Newtonsoft.Json.Linq.JArray da)
                        {
                            DayPlanCalendarManager.Shared.SetDismissed(day, da.Select(x => x.ToString()));
                            changed = true;
                        }
                    }
                    catch (Exception ex) { LogWriter.LogWrite("dismissed_event_ids parse - " + ex.Message); }
                }

                // 1b) File every synced, unfinished to-do by its due date (overdue → today), the Mac's
                //     loadAndMergeDueTodos (report 2026-09-25_19-54-42). Needs the COMPLETE unfinished
                //     catalogue; a failed or partial read changes nothing.
                var catalogue = await ToDoCatalogue.GetAsync();
                if (catalogue != null && DayPlanTaskStore.Shared.MergeDueTodos(catalogue.Tasks, DateTime.Now, catalogue.Account))
                    changed = true;
                // 1c) Tasks typed here that never got a to-do (made before creates were synced, or while offline) get one
                //     now, so they reach the user's other devices (report 2026-09-28_21-59-10).
                if (catalogue != null) DayPlanTaskStore.Shared.SyncLocalOnlyTasks(DateTime.Today);

                // 2) Reconcile done-state from the actual to-do list (/to-do/recent includes completed).
                //    Run EVERY time the Day Plan is shown/changed — NOT throttled per day — so a task
                //    done or re-opened on web/mobile reflects here on the next open. GetAllTasks itself
                //    self-throttles (≤15s), so this is cheap. The /day-plan/day read above is empty when
                //    the backend doesn't persist planned_date, so this is the reliable done sync.
                //    /to-do/recent leaves completed to-dos out, so it can never tick a task finished
                //    elsewhere; ask for this day's to-dos by id instead (completed ones included), and
                //    fall back to /recent only if that call fails.
                await client.GetAllTasks();   // refreshes appSettings.sAllTasks (debounced ≤15s)
                var sourceIds = DayPlanTaskStore.Shared.Tasks(day)
                    .Select(t => t.SourceTaskId).Where(id => !string.IsNullOrEmpty(id));
                string planned = await client.GetToDosByIds(sourceIds);
                if (DayPlanTaskStore.Shared.ReconcileStatusFrom(planned ?? appSettings.sAllTasks))
                    changed = true;

                // Only re-render if the day we reconciled is still the one on screen.
                if (changed && _selectedDate.Date == day)
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
                // "≈ Xh of tasks planned · Ym buffer" + the red over-capacity warning and its Fix (Mac).
                RenderBufferAndBanner(remainingMins);

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
            // "Show dismissed events" lives in the Day Plan's top-left schedule cog, as on the Mac.

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
            // Read as "Done: <task>", not an unnamed checkbox.
            System.Windows.Automation.AutomationProperties.SetName(check, "Done: " + (task.Title ?? ""));
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
                    // Commit / Enter / Tab / Esc and the #project suggestions: WireTaskNameComposer (below).
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

            // Duration. An active task's is an inline field, as on the Mac (DayPlanTasks "dur:" field): click it or Tab
            // from the name, type "30" / "45m" / "1h", and Enter or leaving it saves. It used to be a plain label that
            // only the ⋯ modal could change (report 2026-09-28_20-58-44).
            Brush durBrush = fgColor.HasValue ? new SolidColorBrush(WithAlpha(fgColor.Value, 0.85)) : SecBrush();
            FrameworkElement durElement;
            TextBox dur = null;
            if (task.Done)
            {
                durElement = new TextBlock
                {
                    Text = DayPlanDurations.RowLabel(task.DurationMinutes),
                    FontSize = 12,
                    Foreground = durBrush,
                    MinWidth = 34,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
            }
            else
            {
                dur = BuildTaskDurationField(task, durBrush, fgColor.HasValue ? new SolidColorBrush(fgColor.Value) : FgBrush());
                durElement = dur;
            }
            Grid.SetColumn(durElement, 4);
            grid.Children.Add(durElement);

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
            // #project / !priority / *minutes in the name, suggestions, shortcut card (HabitsDayPlan.Composer.cs).
            if (nameField is TextBox composerField) WireTaskNameComposer(task, composerField, nameColumn, row, dur);
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
            // The "🙈 Hide" / "👀 Show" control sits in the card's own last column, so the title trims before it
            // instead of running underneath (report 2026-09-25_14-31-18). A bare ✕ read as "delete this
            // event"; it only hides the row from the Day Plan, so the button says so (Mac hideEventButton).
            // The colour swatch moved off the card into the meeting's settings, opened by clicking the card.
            Button toggle = BuildMeetingHideButton(block);
            UIElement card = BuildNonHabitCard(block, toggle);
            if (dismissed) card.Opacity = 0.45;   // shown only via "Show hidden meetings"; fade it
            var overlay = new Grid { Cursor = Cursors.Hand, ToolTip = DayPlanText.MeetingSettingsHint };
            overlay.Children.Add(card);
            overlay.MouseLeftButtonUp += (s, e) =>
            {
                if (e.Handled) return;
                e.Handled = true;
                // After the click has finished routing, so the modal doesn't open inside the mouse-up.
                Dispatcher.BeginInvoke(new Action(() => ShowMeetingSettings(block)));
            };
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

        /// <summary>The meeting's "🙈 Hide" / "👀 Show" pill (Mac hideEventButton / showEventButton).</summary>
        private Button BuildMeetingHideButton(ScheduleBlock block)
        {
            bool dismissed = block.IsDismissed;
            // Emoji.Wpf renders the monkey in colour (Segoe UI Emoji); explicit black text on the light pill so
            // it reads on both themes.
            var toggleLabel = new Emoji.Wpf.TextBlock
            {
                Text = dismissed ? DayPlanText.ShowEventButton : DayPlanText.HideEventButton,
                FontSize = 11.5,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"),
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center
            };
            var togglePill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD8)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                Child = toggleLabel
            };
            var toggle = new Button
            {
                Content = togglePill,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(10, 0, 4, 0),
                Template = BorderlessButtonTemplate(),
                ToolTip = dismissed ? DayPlanText.MeetingRestore : DayPlanText.MeetingDismiss,
                VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(toggle, dismissed ? "DayPlanShowMeeting" : "DayPlanHideMeeting");
            System.Windows.Automation.AutomationProperties.SetName(toggle, dismissed ? DayPlanText.MeetingRestore : DayPlanText.MeetingDismiss);
            toggle.Click += (s, e) =>
            {
                DayPlanCalendarManager.Shared.SetDismissed(block.EventID, _selectedDate, !dismissed);
                RenderDayPlan();
            };
            return toggle;
        }

        /// <summary>
        /// The meeting's settings, opened by clicking its card (Mac: clicking a meeting opens its edit form,
        /// where its colour lives). Windows reads meetings from the calendar sync and cannot edit the event
        /// itself, so this holds what the Day Plan owns about it: its colour and whether it is hidden today.
        /// </summary>
        private void ShowMeetingSettings(ScheduleBlock block)
        {
            try
            {
                var dlg = new DayPlanMeetingSettingsDialog(block.Title,
                    string.Format("{0}, {1}–{2}", _selectedDate.ToString("ddd d MMM", CultureInfo.CurrentCulture),
                                  TimeString(block.Start), TimeString(block.End)),
                    MeetingColours, block.Colour, block.IsDismissed, Window.GetWindow(this));
                appSettings.CapturePostHog("day-plan-event-settings-opened");
                if (dlg.ShowDialog() != true) return;
                if (dlg.ColourChanged) DayPlanCalendarManager.Shared.SetColour(block.EventID, dlg.Colour);   // null = default
                if (dlg.HiddenChanged) DayPlanCalendarManager.Shared.SetDismissed(block.EventID, _selectedDate, dlg.Hidden);
                RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan ShowMeetingSettings ex - " + ex.Message); }
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
            // Read as "Done: <task>", not an unnamed checkbox.
            System.Windows.Automation.AutomationProperties.SetName(check, "Done: " + (task.Title ?? ""));
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

        // MARK: - inline duration field --------------------------------------------------------------

        /// <summary>Marks a task row's duration field (and whose it is) — not a string, so the name composer's refocus ignores it.</summary>
        private sealed class TaskDurationTag
        {
            public TaskDurationTag(string taskId) { TaskId = taskId; }
            public string TaskId { get; }
        }

        /// <summary>The task whose duration field had focus when the panel rebuilt; it gets focus back.</summary>
        private string _durationRestoreId;

        /// <summary>
        /// A panel row's editable duration: looks like the old "15m" label until it has focus, then shows the name
        /// field's focus border with the text selected so typing replaces it. Enter or leaving saves; Esc reverts.
        /// </summary>
        private TextBox BuildTaskDurationField(DayPlanTask task, Brush textBrush, Brush caretBrush)
        {
            var box = new TextBox
            {
                Text = DayPlanDurations.RowLabel(task.DurationMinutes),
                Tag = new TaskDurationTag(task.Id),
                FontSize = 12,
                Foreground = textBrush,
                CaretBrush = caretBrush,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2, 1, 2, 1),
                MinWidth = 40,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                AllowDrop = false,   // a reorder drop falls through to the row
                Cursor = Cursors.IBeam,
                ToolTip = DayPlanText.TasksDurationHint
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(box, "DayPlanTaskDuration");
            System.Windows.Automation.AutomationProperties.SetName(box, "Duration: " + (task.Title ?? ""));

            bool finished = false;
            box.GotKeyboardFocus += (s, e) =>
            {
                finished = false;
                box.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0x80));
                box.BorderBrush = new SolidColorBrush(COrange);
                box.SelectAll();
            };
            // A click focuses the field and selects its text (a plain click would drop the caret mid-text instead).
            box.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (box.IsKeyboardFocusWithin) return;
                box.Focus();
                e.Handled = true;
            };
            box.LostKeyboardFocus += (s, e) =>
            {
                box.Background = Brushes.Transparent;
                box.BorderBrush = Brushes.Transparent;
                if (finished) return;
                finished = true;
                CommitTaskDuration(task.Id, box);
            };
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    finished = true;
                    CommitTaskDuration(task.Id, box);
                    Keyboard.ClearFocus();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    finished = true;
                    DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(task.Id, _selectedDate);
                    box.Text = DayPlanDurations.RowLabel(t?.DurationMinutes ?? task.DurationMinutes);
                    Keyboard.ClearFocus();
                }
            };
            if (task.Id == _durationRestoreId)
            {
                _durationRestoreId = null;
                box.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            }
            return box;
        }

        /// <summary>
        /// Save a row's typed duration (Mac <c>commitDuration</c>). Unreadable text keeps the old length. A change
        /// moves the plan's totals and the agenda, so both rebuild — after focus has moved, keeping it where it went.
        /// </summary>
        private void CommitTaskDuration(string taskId, TextBox box)
        {
            try
            {
                DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(taskId, _selectedDate);
                if (t == null) return;
                int minutes = DayPlanDurations.CommitRowField(box.Text, t.DurationMinutes);
                box.Text = DayPlanDurations.RowLabel(minutes);
                if (minutes == t.DurationMinutes) return;
                LogWriter.LogWrite("[user-action] Day Plan task duration " + t.DurationMinutes + "m -> " + minutes + "m");
                t.DurationMinutes = minutes;
                DayPlanTaskStore.Shared.Update(t, _selectedDate);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RememberPanelFocusForRebuild();
                    RefreshTasksAndAgenda();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan CommitTaskDuration - " + ex.Message); }
        }

        /// <summary>Before the panel rebuilds under the user: note the name or duration field they are in, to refocus it.</summary>
        private void RememberPanelFocusForRebuild()
        {
            var focused = Keyboard.FocusedElement as TextBox;
            if (focused == null || !TasksListStack.IsAncestorOf(focused)) return;
            if (focused.Tag is string nameTaskId)
            {
                _composerRestoreId = nameTaskId;
                _composerRestoreText = focused.Text;
            }
            else if (focused.Tag is TaskDurationTag durationTag)
            {
                _durationRestoreId = durationTag.TaskId;
            }
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
