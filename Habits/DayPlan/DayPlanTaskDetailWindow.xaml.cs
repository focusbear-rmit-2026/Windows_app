using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Interaction logic for DayPlanTaskDetailWindow.xaml — the task detail modal (M7 phase 3), the
    /// Windows port of the macOS <c>openTaskModal</c>. Edits a task's title / project / duration /
    /// importance / notes / sub-tasks, moves it to another day, or removes it. All edits are applied
    /// to <see cref="DayPlanTaskStore"/> on Save/Move/Remove; the caller re-renders when the dialog
    /// returns <c>true</c>.
    /// </summary>
    public partial class DayPlanTaskDetailWindow : Window
    {
        private readonly string _taskId;
        private readonly DateTime _date;
        private DayPlanTask _task;
        private bool _loaded;   // Loaded can fire more than once; populate the form only the first time
        private readonly List<DayPlanSubtask> _subtasks = new List<DayPlanSubtask>();

        public DayPlanTaskDetailWindow(string taskId, DateTime date)
        {
            InitializeComponent();
            _taskId = taskId;
            _date = date;

            Heading.Text = DayPlanText.TasksDetailsTitle;
            LblProject.Text = DayPlanText.TasksProjectLabel;
            LblDuration.Text = DayPlanText.TasksDurationLabel;
            LblImportance.Text = DayPlanText.TasksImportanceLabel;
            LblNotes.Text = DayPlanText.TasksDetailsLabel;
            LblSubtasks.Text = DayPlanText.TasksSubtasksLabel;
            LblMove.Text = DayPlanText.TasksSnoozeTitle;
            BtnAddSubtaskText.Text = "＋ " + DayPlanText.TasksAddSubtask;
            BtnMoveText.Text = DayPlanText.TasksSnoozeConfirm;
            MoveField.ToolTip = DayPlanText.TasksSnoozeHelp;
            MoveHelp.Text = DayPlanText.TasksSnoozeHelp;
            btnRemoveText.Text = DayPlanText.TasksRemove;
            btnCancelText.Text = DayPlanText.Cancel;
            btnSaveText.Text = DayPlanText.Save;

            Loaded += (s, e) => LoadTask();
        }

        private void LoadTask()
        {
            if (_loaded) return;   // guard: Loaded can fire repeatedly and would double-populate the combo
            _loaded = true;

            _task = DayPlanTaskStore.Shared.TaskWithId(_taskId, _date);
            if (_task == null) { DialogResult = false; return; }

            TitleField.Text = _task.Title;
            ProjectField.Text = _task.Project ?? "";
            BuildProjectSuggestions();
            DurationField.Text = _task.DurationMinutes.ToString(CultureInfo.InvariantCulture);

            // Importance: index 0 = unset, 1..10 = outcome.
            ImportanceField.Items.Add(DayPlanText.TasksImportanceUnset);
            for (int o = 1; o <= 10; o++)
                ImportanceField.Items.Add(o + " — " + DayPlanText.OutcomeBandLabel(o));
            ImportanceField.SelectedIndex = (_task.Outcome.HasValue && _task.Outcome.Value >= 1 && _task.Outcome.Value <= 10)
                ? _task.Outcome.Value : 0;
            UpdateSwatch();

            NotesField.Text = _task.Notes ?? "";

            _subtasks.Clear();
            foreach (DayPlanSubtask st in _task.SubtaskList)
                _subtasks.Add(new DayPlanSubtask { Id = st.Id, Title = st.Title, Done = st.Done });
            RebuildSubtaskRows();

            TitleField.Focus();
            TitleField.SelectAll();

            RefreshRemoteProjects();   // pull web/mobile projects, then re-chip
        }

        private async void RefreshRemoteProjects()
        {
            try
            {
                System.Collections.Generic.List<string> names = await new Focus_Bear.API.FocusBearAPIClient().GetProjects();
                if (names != null)
                {
                    DayPlanTaskStore.Shared.RemoteProjectNames = names;
                    BuildProjectSuggestions();
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanTaskDetailWindow.RefreshRemoteProjects - " + ex.Message); }
        }

        /// <summary>Show existing project names as clickable chips that fill the Project field.</summary>
        private void BuildProjectSuggestions()
        {
            ProjectSuggestions.Children.Clear();
            List<string> names = DayPlanTaskStore.Shared.AllProjectNames();
            if (names.Count == 0)
            {
                ProjectSuggestions.Children.Add(new TextBlock
                {
                    Text = DayPlanText.TasksNoProjectsYet,
                    FontSize = 10.5,
                    Foreground = (Brush)(TryFindResource("ForegroundGrey") ?? Brushes.Gray)
                });
                return;
            }
            foreach (string p in names)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x80, 0x80, 0x80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = p,
                        FontSize = 11,
                        Foreground = (Brush)(TryFindResource("ForegroundColor") ?? Brushes.Black)
                    }
                };
                string captured = p;
                chip.MouseLeftButtonUp += (s, e) => ProjectField.Text = captured;
                ProjectSuggestions.Children.Add(chip);
            }
        }

        // ---- importance swatch -------------------------------------------------------------------

        private int SelectedOutcome => ImportanceField.SelectedIndex >= 1 ? ImportanceField.SelectedIndex : 0;

        private void ImportanceField_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSwatch();

        private void UpdateSwatch()
        {
            Color? c = DayPlanTaskVisuals.OutcomeColor(SelectedOutcome >= 1 ? SelectedOutcome : (int?)null);
            ImportanceSwatch.Background = c.HasValue ? new SolidColorBrush(c.Value) : Brushes.Transparent;
        }

        // ---- sub-tasks ---------------------------------------------------------------------------

        private void RebuildSubtaskRows()
        {
            SubtasksStack.Children.Clear();
            for (int i = 0; i < _subtasks.Count; i++)
            {
                DayPlanSubtask sub = _subtasks[i];
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox { IsChecked = sub.Done, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                check.Checked += (s, e) => sub.Done = true;
                check.Unchecked += (s, e) => sub.Done = false;
                Grid.SetColumn(check, 0);
                grid.Children.Add(check);

                var field = new TextBox
                {
                    Text = sub.Title,
                    Height = 26,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontSize = 13,
                    Background = Brushes.White,          // explicit so the text is legible in both themes
                    Foreground = Brushes.Black,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x7F, 0x7F)),
                    Padding = new Thickness(4, 0, 4, 0)
                };
                field.TextChanged += (s, e) => sub.Title = field.Text;
                Grid.SetColumn(field, 1);
                grid.Children.Add(field);

                var del = new Button
                {
                    Content = new TextBlock { Text = "✕", Margin = new Thickness(0, -1, 0, 1), Foreground = Brushes.Black },
                    Width = 28,
                    Height = 26,
                    Margin = new Thickness(6, 0, 0, 0),
                    Style = (Style)(TryFindResource("BtnStyleClearOrangeBordered")),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                int captured = i;
                del.Click += (s, e) =>
                {
                    if (captured >= 0 && captured < _subtasks.Count) { _subtasks.RemoveAt(captured); RebuildSubtaskRows(); }
                };
                Grid.SetColumn(del, 2);
                grid.Children.Add(del);

                SubtasksStack.Children.Add(grid);
            }
        }

        private void BtnAddSubtask_Click(object sender, RoutedEventArgs e)
        {
            _subtasks.Add(new DayPlanSubtask(""));
            RebuildSubtaskRows();
        }

        // ---- move to another day -----------------------------------------------------------------

        private void MoveField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MoveField.Text)) { MovePreview.Text = ""; return; }
            // Anchor relative terms ("today", "tom", "2d"…) to the real current date, NOT the viewed day,
            // so on a future day "today" moves the task back to the actual today.
            DateTime? target = DayPlanSnooze.Parse(MoveField.Text, DateTime.Today);
            MovePreview.Text = target.HasValue
                ? DayPlanText.Fmt(DayPlanText.TasksMovesTo, target.Value.ToString("dddd, d MMM", CultureInfo.CurrentCulture))
                : DayPlanText.TasksMoveUnrecognised;
        }

        private void BtnMove_Click(object sender, RoutedEventArgs e)
        {
            DateTime? target = DayPlanSnooze.Parse(MoveField.Text, DateTime.Today);
            if (!target.HasValue) { System.Media.SystemSounds.Beep.Play(); return; }
            ApplyEdits();   // keep field edits when moving
            DayPlanTaskStore.Shared.MoveTaskToDay(_taskId, _date, target.Value);
            DialogResult = true;
        }

        // ---- save / remove / cancel --------------------------------------------------------------

        private void ApplyEdits()
        {
            if (_task == null) return;
            string newTitle = (TitleField.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(newTitle)) _task.Title = newTitle;

            string proj = (ProjectField.Text ?? "").Trim();
            _task.Project = string.IsNullOrEmpty(proj) ? null : proj;

            string digits = new string((DurationField.Text ?? "").Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int mins) && mins > 0) _task.DurationMinutes = Math.Min(mins, 24 * 60);

            string notes = (NotesField.Text ?? "").Trim();
            _task.Notes = string.IsNullOrEmpty(notes) ? null : notes;

            _task.Outcome = SelectedOutcome >= 1 ? SelectedOutcome : (int?)null;

            List<DayPlanSubtask> cleaned = _subtasks
                .Select(s => new DayPlanSubtask { Id = s.Id, Title = (s.Title ?? "").Trim(), Done = s.Done })
                .Where(s => !string.IsNullOrEmpty(s.Title))
                .ToList();
            _task.Subtasks = cleaned.Count == 0 ? null : cleaned;

            DayPlanTaskStore.Shared.Update(_task, _date);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            ApplyEdits();
            DialogResult = true;
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            DayPlanTaskStore.Shared.Delete(_taskId, _date);
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
