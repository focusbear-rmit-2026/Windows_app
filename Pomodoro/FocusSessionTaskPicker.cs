using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// "Select Tasks" (the Mac's <c>FocusSessionTaskPickerSheet</c>, Figma 1.2): tick tasks to add to the running
    /// session. It offers today's Day Plan in its own order, else the to-do list in SMART order, and searches the
    /// whole to-do list once something is typed. Text that names no task can be created as one (<c>PUT /to-do</c>,
    /// due today, as the Mac's "Create new task" does) and is queued straight away.
    /// Not ported: the Mac sheet's filter page (spoons / project / due date).
    /// </summary>
    internal sealed class FocusSessionTaskPicker : Window
    {
        private enum Source { DayPlan, ToDoList, Search }

        private sealed class Row
        {
            internal FocusSessionTask Task;
            internal bool Selected;
        }

        private readonly FocusSessionStrings _strings;
        private readonly ISet<string> _queued;
        private readonly TextBox _search = new TextBox();
        private readonly DispatcherTimer _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBlock _caption;
        private readonly ContentControl _footer = new ContentControl();
        private readonly FocusStartPalette _p = FocusStartPalette.Current();
        private readonly FocusStartScale _s = new FocusStartScale(1400);
        private List<Row> _rows = new List<Row>();
        private readonly Dictionary<string, Row> _picked = new Dictionary<string, Row>();
        private Source _source = Source.DayPlan;
        private bool _loading, _creating, _createFailed;
        private int _request;

        internal FocusSessionTaskPicker(FocusSessionStrings strings, ISet<string> queuedIds)
        {
            _strings = strings;
            _queued = queuedIds ?? new HashSet<string>();
            Title = strings["titleSelectTasks"];
            Width = 600;
            Height = 660;
            MinWidth = 420;
            MinHeight = 360;
            ShowInTaskbar = false;
            // No taller than the owner's work area (a 1080p screen at 175% leaves ~570 DIP): the header, search
            // and the Create / Queue buttons stay put and only the list between them scrolls.
            ToDoScreens.DialogFit.Apply(this);
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            AutomationProperties.SetAutomationId(this, "FocusSessionTaskPicker");

            _caption = FocusStartUi.Text("", 12, _p.Secondary);
            _caption.Margin = new Thickness(0, 10, 0, 6);
            var root = new DockPanel();
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var close = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE711, 14, _p.Secondary), Close, "btnFocusSessionPickerClose", strings["closeText"]);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            header.Children.Add(FocusStartUi.Text(strings["titleSelectTasks"], 22, _p.Primary, FontWeights.SemiBold));
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var search = FocusStartUi.SearchField(_search, strings["placeholderSearchTask"], _p, _s, "txtFocusSessionPickerSearch");
            DockPanel.SetDock(search, Dock.Top);
            root.Children.Add(search);
            DockPanel.SetDock(_caption, Dock.Top);
            root.Children.Add(_caption);
            DockPanel.SetDock(_footer, Dock.Bottom);
            root.Children.Add(_footer);
            root.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = new Border { Child = root, Padding = new Thickness(20) };

            _search.TextChanged += (o, e) => { _createFailed = false; _debounce.Stop(); _debounce.Start(); RenderFooter(); };
            _search.KeyDown += (o, e) => { if (e.Key == Key.Enter) { e.Handled = true; _debounce.Stop(); _ = Load(); } };
            _debounce.Tick += (o, e) => { _debounce.Stop(); _ = Load(); };
            PreviewKeyDown += (o, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Loaded += (o, e) => { _search.Focus(); _ = Load(); };
        }

        /// <summary>What to add to the queue, in the order it was ticked; empty when the sheet was closed.</summary>
        internal IList<FocusSessionTask> Chosen { get; private set; } = new List<FocusSessionTask>();

        private async Task Load()
        {
            int request = ++_request;
            string query = _search.Text.Trim();
            _loading = true;
            Render();
            List<FocusSessionTask> tasks;
            try
            {
                if (query.Length > 0)
                {
                    _source = Source.Search;
                    // The search endpoint ranks by title and ignores every other filter.
                    var found = await new FocusBearAPIClient().SearchToDos(query);
                    tasks = found == null ? null : found.Where(t => !t.IsCompleted).Select(FromToDo).ToList();
                }
                else
                {
                    tasks = DayPlanTasks();
                    _source = Source.DayPlan;
                    if (tasks.Count == 0)
                    {
                        _source = Source.ToDoList;
                        var page = await new FocusBearAPIClient().GetToDoPage(1, ToDoStatus.NotStarted, ToDoSortMode.Smart, null);
                        tasks = page?.Tasks.Where(t => !t.IsCompleted).Select(FromToDo).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusSessionTaskPicker Load ex - " + ex.Message);
                tasks = null;
            }
            if (request != _request) return;
            _loading = false;
            _rows = (tasks ?? new List<FocusSessionTask>()).Where(t => !_queued.Contains(t.Id)).GroupBy(t => t.Id).Select(g => g.First())
                .Select(t => _picked.TryGetValue(t.Id, out Row picked) ? picked : new Row { Task = t }).ToList();
            Render();
        }

        /// <summary>Today's Day Plan, unfinished, in the plan's own (draggable) order — the user's order, not a score's.</summary>
        private static List<FocusSessionTask> DayPlanTasks()
        {
            try
            {
                return DayPlanTaskStore.Shared.Tasks(DateTime.Today).Where(t => !t.Done)
                    .Select(t => new FocusSessionTask(t.SourceTaskId ?? t.Id, t.Title, t.Notes ?? "", Math.Max(60, t.DurationMinutes * 60),
                        projectName: string.IsNullOrWhiteSpace(t.Project) ? null : t.Project, projectId: t.ProjectId))
                    .ToList();
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSessionTaskPicker DayPlanTasks ex - " + ex.Message); return new List<FocusSessionTask>(); }
        }

        private static FocusSessionTask FromToDo(ToDoTask task) =>
            FocusSessionQueueWriter.FromToDo(task, task.TagList.FirstOrDefault(tag => tag.Id == task.ProjectId)?.Text);

        private void Render()
        {
            _caption.Text = _source == Source.Search ? _strings["labelPickerFromSearch"] : _source == Source.DayPlan ? _strings["labelPickerFromDayPlan"] : _strings["labelPickerFromToDoList"];
            _list.Children.Clear();
            if (_loading) _list.Children.Add(Message(_strings["fetchingTasks"]));
            else if (_rows.Count == 0) _list.Children.Add(Message(_source == Source.Search ? _strings["labelNoMatchingTasks"] : _strings["labelNoTasksToPick"]));
            else foreach (var row in _rows) _list.Children.Add(RowView(row));
            RenderFooter();
        }

        private TextBlock Message(string text)
        {
            var message = FocusStartUi.Text(text, 13, _p.Secondary);
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.Margin = new Thickness(0, 30, 0, 0);
            AutomationProperties.SetAutomationId(message, "txtFocusSessionPickerMessage");
            return message;
        }

        private FrameworkElement RowView(Row row)
        {
            var task = row.Task;
            var line = new DockPanel();
            var check = FocusStartUi.Glyph(row.Selected ? 0xE73A : 0xE739, 16, row.Selected ? _p.Accent : _p.Secondary);
            check.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(check, Dock.Left);
            line.Children.Add(check);
            if (!string.IsNullOrEmpty(task.Icon))
            {
                var emoji = FocusStartUi.Text(task.Icon, 18, _p.Primary, wrap: false);
                emoji.FontFamily = FocusStartUi.Emoji;
                emoji.Margin = new Thickness(0, 0, 10, 0);
                emoji.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(emoji, Dock.Left);
                line.Children.Add(emoji);
            }
            var text = new StackPanel();
            text.Children.Add(FocusStartUi.Text(task.Title, 14, _p.Primary, FontWeights.SemiBold, wrap: false));
            var chips = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            if (!string.IsNullOrWhiteSpace(task.ProjectName)) chips.Children.Add(Chip("📁 " + task.ProjectName, _p.Field, _p.Primary));
            if (task.TopScore.HasValue && !double.IsNaN(task.TopScore.Value)) chips.Children.Add(Chip("⚡ " + _strings.TopScoreBadge(task.TopScore.Value), _p.SecondaryAccent, Brushes.White));
            chips.Children.Add(Chip("🕒 " + _strings.DurationBadge(task.DurationMinutes), _p.Metadata, _p.Accent));
            text.Children.Add(chips);
            line.Children.Add(text);
            var face = new Border
            {
                Child = line,
                Background = row.Selected ? _p.Selection : _p.Surface,
                BorderBrush = row.Selected ? _p.Accent : _p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
            };
            var button = FocusStartUi.Clickable(face, () =>
            {
                row.Selected = !row.Selected;
                if (row.Selected) _picked[task.Id] = row; else _picked.Remove(task.Id);
                Render();
            }, "btnFocusSessionPickerRow", task.Title);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        private Border Chip(string text, Brush background, Brush foreground)
        {
            var chip = FocusStartUi.Badge(text, background, foreground, _s);
            ((TextBlock)chip.Child).FontSize = 11;
            ((TextBlock)chip.Child).FontFamily = FocusStartUi.Emoji;
            chip.Margin = new Thickness(0, 0, 6, 2);
            return chip;
        }

        private void RenderFooter()
        {
            var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            string typed = FocusStartWorkSelection.NewTaskTitle(_search.Text, _rows.Select(r => new ToDoTask { Title = r.Task.Title }));
            if (_createFailed)
            {
                var failed = FocusStartUi.Text(_strings["errorCouldNotSaveTask"], 12, _p.Warning);
                failed.Margin = new Thickness(0, 0, 0, 8);
                footer.Children.Add(failed);
            }
            // "Create new task" carries the text typed in the search, as the Mac's #2571 (17) does.
            string createLabel = typed == null ? _strings["buttonCreateNewTask"] : _strings["buttonCreateTypedTask"].Replace("<NAME>", typed);
            var createRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            createRow.Children.Add(new TextBlock { Text = ((char)0xE710).ToString(), FontFamily = FocusStartUi.Symbols, FontSize = 12, Foreground = _p.Primary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            createRow.Children.Add(FocusStartUi.Text(_creating ? _strings["fetchingTasks"] : createLabel, 13, _p.Primary, wrap: false));
            var createFace = new Border { Child = createRow, Background = _p.Field, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Height = 42 };
            var create = FocusStartUi.Clickable(createFace, () => { if (typed == null) _search.Focus(); else _ = CreateTyped(typed); }, "btnFocusSessionPickerCreate", createLabel);
            create.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            create.IsEnabled = !_creating;
            create.ToolTip = typed == null ? _strings["placeholderSearchTask"] : null;
            footer.Children.Add(create);
            var queue = FocusStartUi.Primary(_strings.QueueButton(_picked.Count), _p, new FocusStartScale(1100), QueueChosen, "btnFocusSessionPickerQueue");
            queue.Margin = new Thickness(0, 10, 0, 0);
            queue.IsEnabled = _picked.Count > 0 && !_creating;
            footer.Children.Add(queue);
            _footer.Content = footer;
        }

        private void QueueChosen()
        {
            if (_picked.Count == 0) return;
            Chosen = _picked.Values.Select(r => r.Task).ToList();
            DialogResult = true;
        }

        private async Task CreateTyped(string title)
        {
            if (_creating) return;
            _creating = true;
            _createFailed = false;
            RenderFooter();
            ToDoTask saved = null;
            try
            {
                var draft = FocusStartWorkSelection.NewSessionTask(title, DateTime.Now, null);
                var response = await new FocusBearAPIClient().UpsertToDo(ToDoUpsertBody.Make(draft));
                if (response != null && response.IsSuccess) saved = ToDoJson.ReadTask(response.Body) ?? draft;
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSessionTaskPicker CreateTyped ex - " + ex.Message); }
            _creating = false;
            if (saved == null) { _createFailed = true; RenderFooter(); return; }
            LogWriter.LogWrite("FocusSessionTaskPicker - created a task for the session");
            Chosen = _picked.Values.Select(r => r.Task).Concat(new[] { FromToDo(saved) }).ToList();
            DialogResult = true;
        }
    }
}
