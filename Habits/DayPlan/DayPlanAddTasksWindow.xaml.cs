using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Interaction logic for DayPlanAddTasksWindow.xaml — the "From to-do list" picker for the Day
    /// Plan tasks panel (M7). Fetches the user's recent to-dos (GET <c>to-do/recent</c>, cached in
    /// <see cref="AppSettings.sAllTasks"/>), lists the incomplete ones not already planned for the
    /// day, and adds the checked ones as <see cref="DayPlanTask"/>s. The Windows equivalent of the
    /// macOS <c>addFromList</c> picker.
    /// </summary>
    public partial class DayPlanAddTasksWindow : Window
    {
        private readonly DateTime _date;
        private readonly List<PickerTodo> _todos = new List<PickerTodo>();
        private readonly AppSettings appSettings = AppSettings.GetInstance;

        /// <summary>A to-do candidate + its row checkbox, so filtering/collecting is simple.</summary>
        private class PickerTodo
        {
            public string Id;
            public string Title;
            public int DurationMinutes;
            public string Project;
            public int? Outcome;
            public int? EisenhowerQuadrant;
            public CheckBox Row;
        }

        public DayPlanAddTasksWindow(DateTime date)
        {
            InitializeComponent();
            _date = date;

            // All user-facing text from config (scheduleViewInfo), with English fallbacks.
            Heading.Text = DayPlanText.TasksAddFromList;
            StatusText.Text = DayPlanText.TasksLoading;
            btnCancelText.Text = DayPlanText.Cancel;
            btnAddText.Text = DayPlanText.TasksAddSelected;

            Loaded += async (s, e) => await LoadTodos();
        }

        // Count of items in the fetched payload before the "already added" filter, so BuildRows can
        // tell "nothing fetched" apart from "everything already planned".
        private int _fetchedCount;

        private async System.Threading.Tasks.Task LoadTodos()
        {
            try
            {
                var client = new FocusBearAPIClient();
                // Same endpoint/order as the web to-do list and the macOS picker: SMART priority,
                // server-side status=NOT_STARTED (so we don't need to filter completed ourselves).
                string json = await client.GetSmartTasks();
                ParseTodos(json);
                BuildRows();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanAddTasksWindow.LoadTodos - " + ex.Message);
                StatusText.Text = DayPlanText.TasksEmpty;
            }
        }

        /// <summary>
        /// Parse the smart-tasks payload. Accepts either a bare array or a <c>{ data: [...] }</c> wrapper
        /// (both shapes the backend can return). The endpoint already returns only NOT_STARTED tasks, so
        /// the only client-side filter is "not already planned for this day". Each item is read
        /// defensively so one malformed field can't blank the whole list.
        /// </summary>
        private void ParseTodos(string json)
        {
            _todos.Clear();
            _fetchedCount = 0;
            if (string.IsNullOrWhiteSpace(json)) return;

            JArray arr = null;
            try
            {
                JToken root = JToken.Parse(json);
                if (root is JArray a) arr = a;
                else if (root is JObject o)
                {
                    foreach (string k in new[] { "data", "tasks", "items", "results", "todos" })
                    {
                        if (o[k] is JArray inner) { arr = inner; break; }
                    }
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanAddTasksWindow.ParseTodos parse - " + ex.Message); }
            if (arr == null) return;

            _fetchedCount = arr.Count;

            foreach (JToken t in arr)
            {
                try
                {
                    if (!(t is JObject o)) continue;
                    string id = o.Value<string>("id");
                    if (string.IsNullOrEmpty(id)) continue;
                    if (DayPlanTaskStore.Shared.Contains(id, _date)) continue;   // already on this day

                    // duration is seconds; be tolerant of number/string/missing.
                    int secs = 0;
                    JToken durTok = o["duration"];
                    if (durTok != null && durTok.Type != JTokenType.Null)
                        int.TryParse(durTok.ToString(), out secs);
                    int minutes = secs > 0 ? Math.Max(1, secs / 60) : 15;

                    string project = null;
                    if (o["tags"] is JArray tags && tags.Count > 0 && tags[0] is JObject tag0)
                        project = tag0.Value<string>("text") ?? tag0.Value<string>("name");

                    _todos.Add(new PickerTodo
                    {
                        Id = id,
                        Title = o.Value<string>("title") ?? "Task",
                        DurationMinutes = minutes,
                        Project = project,
                        Outcome = TryInt(o["outcome"]),
                        EisenhowerQuadrant = TryInt(o["eisenhower_quadrant"])
                    });
                }
                catch (Exception ex) { LogWriter.LogWrite("DayPlanAddTasksWindow.ParseTodos item - " + ex.Message); }
            }
        }

        private static int? TryInt(JToken tok)
        {
            if (tok == null || tok.Type == JTokenType.Null) return null;
            return int.TryParse(tok.ToString(), out int v) ? v : (int?)null;
        }

        private void BuildRows()
        {
            TasksList.Children.Clear();
            if (_todos.Count == 0)
            {
                // Distinguish "nothing came back" from "everything is already on the day".
                StatusText.Text = _fetchedCount > 0
                    ? DayPlanText.TasksPickerAllAdded
                    : DayPlanText.TasksPickerNone;
                StatusText.Visibility = Visibility.Visible;
                ListScroll.Visibility = Visibility.Collapsed;
                return;
            }

            StatusText.Visibility = Visibility.Collapsed;
            ListScroll.Visibility = Visibility.Visible;

            foreach (PickerTodo todo in _todos)
            {
                var cb = new CheckBox
                {
                    Margin = new Thickness(2, 4, 2, 4),
                    Foreground = (Brush)(Application.Current.TryFindResource("ForegroundColor") ?? Brushes.Black),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = new TextBlock
                    {
                        Text = todo.DurationMinutes > 0 ? $"{todo.Title}  ·  {todo.DurationMinutes}m" : todo.Title,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    }
                };
                todo.Row = cb;
                TasksList.Children.Add(cb);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = (SearchBox.Text ?? "").Trim();
            foreach (PickerTodo todo in _todos)
            {
                if (todo.Row == null) continue;
                bool match = string.IsNullOrEmpty(q) ||
                             (todo.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                todo.Row.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (PickerTodo todo in _todos)
                {
                    if (todo.Row?.IsChecked != true) continue;
                    DayPlanTaskStore.Shared.Add(new DayPlanTask(
                        todo.Title, todo.DurationMinutes, done: false,
                        sourceTaskId: todo.Id, project: todo.Project,
                        outcome: todo.Outcome, eisenhowerQuadrant: todo.EisenhowerQuadrant), _date);
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanAddTasksWindow.btnAdd_Click - " + ex.Message); }
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
