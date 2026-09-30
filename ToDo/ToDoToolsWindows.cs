using Focus_Bear.StatsScreens;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>The To-Do screen's whole-list tools, each a window of its own — the Mac's <c>ToDoToolsViews</c>.</summary>
    internal static class ToDoToolsWindows
    {
        private static Window Dialog(Window owner, string title, double width, double height)
        {
            var window = new Window
            {
                Title = title, Width = width, Height = height, MinWidth = 480, MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, ShowInTaskbar = false,
            };
            window.SetResourceReference(Control.BackgroundProperty, "BackgroundColor");
            DialogFit.Apply(window);
            return window;
        }

        private static DockPanel Header(string title, params UIElement[] trailing)
        {
            var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var text = StatsUi.Text(title, 18, FontWeights.Bold, wrap: false);
            text.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(text, Dock.Left);
            header.Children.Add(text);
            foreach (var element in trailing.Reverse())
            {
                DockPanel.SetDock(element, Dock.Right);
                header.Children.Add(element);
            }
            return header;
        }

        private static TextBlock ErrorText()
        {
            var text = StatsUi.Text("", 12);
            text.Foreground = Overdue;
            text.Margin = new Thickness(0, 8, 0, 0);
            return text;
        }

        // ------------------------------------------------------------------ brain dump → tasks

        /// <summary>
        /// Web <c>STANDALONE_BRAIN_DUMP</c>: free text in, the AI's suggested tasks out for review, then each one
        /// written with its own <c>PUT /to-do</c> — one at a time, so a failure leaves exactly the unsaved ones.
        /// </summary>
        internal static void ShowBrainDump(Window owner, ToDoStore store)
        {
            var s = store.Strings;
            var window = Dialog(owner, s.BrainDumpTitle, 680, 560);
            AutomationProperties.SetAutomationId(window, "ToDoBrainDump");
            var root = new DockPanel { Margin = new Thickness(20) };
            window.Content = root;
            var suggestions = new List<BrainDumpTask>();
            bool converting = false, creating = false;
            string account = AppSettings.GetInstance.userAccount?.userID;
            string today = ToDoDates.Ymd(DateTime.Now);
            var error = ErrorText();

            // The input page is built once, so the typed text survives a failed conversion.
            var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            AutomationProperties.SetAutomationId(input, "txtBrainDumpInput");
            var inputPage = new StackPanel();
            inputPage.Children.Add(Field(input, s.BrainDumpPlaceholder, 200));
            var inputRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 8, 0, 0) };
            var count = StatsUi.Text("0 / " + ToDoTools.BrainDumpCharLimit, 11, secondary: true, wrap: false);
            count.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(count, Dock.Left);
            inputRow.Children.Add(count);
            Button go = null;
            var vague = new Border { Background = StatsUi.Tint(TodayTint, 0.12), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
            Action render = null;

            Func<bool, Task> convert = async force =>
            {
                string contents = input.Text.Trim();
                if (contents.Length == 0 || converting) return;
                error.Text = "";
                if (contents.Length > ToDoTools.BrainDumpCharLimit)
                {
                    error.Text = ToDoStrings.Format(s.BrainDumpLimit, ToDoStrings.CountToken, ToDoTools.BrainDumpCharLimit.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                if (!force && ToDoTools.IsBrainDumpVague(contents)) { vague.Visibility = Visibility.Visible; return; }
                vague.Visibility = Visibility.Collapsed;
                converting = true;
                render();
                var result = await new API.FocusBearAPIClient().ConvertBrainDump(contents);
                converting = false;
                // A sign-out, or an edit to the text while the model worked, makes the answer stale.
                if (AppSettings.GetInstance.userAccount?.userID != account || input.Text.Trim() != contents) { render(); return; }
                if (result == null || result.Count == 0) error.Text = s.BrainDumpFailed;
                else suggestions = result.ToList();
                render();
            };

            go = StatsUi.Button("\u2728  " + s.BrainDumpToTasks, async (o, e) => await convert(false), primary: true);
            go.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(go, "btnBrainDumpConvert");
            DockPanel.SetDock(go, Dock.Right);
            inputRow.Children.Add(go);
            inputPage.Children.Add(inputRow);
            var prompt = new StackPanel();
            prompt.Children.Add(StatsUi.Text(s.VagueInputPrompt, 12));
            var anyway = StatsUi.Button(s.ConvertAnyway, async (o, e) => await convert(true), height: 28);
            anyway.HorizontalAlignment = HorizontalAlignment.Left;
            anyway.Margin = new Thickness(0, 8, 0, 0);
            AutomationProperties.SetAutomationId(anyway, "btnBrainDumpConvertAnyway");
            prompt.Children.Add(anyway);
            vague.Child = prompt;
            inputPage.Children.Add(vague);
            input.TextChanged += (o, e) =>
            {
                count.Text = input.Text.Length + " / " + ToDoTools.BrainDumpCharLimit;
                go.IsEnabled = !converting && input.Text.Trim().Length > 0;
                vague.Visibility = Visibility.Collapsed;
            };

            Func<Task> create = async () =>
            {
                var valid = suggestions.Where(t => BrainDumpTaskPayload.Body(t, today) != null).ToList();
                if (creating || valid.Count == 0 || valid.Count != suggestions.Count) return;
                creating = true;
                error.Text = "";
                render();
                var done = new TaskCompletionSource<BrainDumpWriteResult>();
                var writer = new BrainDumpTaskWriter(valid,
                    isCurrent: () => AppSettings.GetInstance.userAccount?.userID == account && window.IsVisible,
                    write: async (task, finished) =>
                    {
                        var response = await store.UpsertRaw(BrainDumpTaskPayload.Body(task, today));
                        var saved = response.IsSuccess ? ToDoJson.ReadTask(response.Body) : null;
                        finished(response.IsSuccess && saved != null && string.Equals(saved.Id, task.Id, StringComparison.OrdinalIgnoreCase));
                    },
                    didSave: id => { suggestions.RemoveAll(t => t.Id == id); render(); },
                    completion: result => done.TrySetResult(result));
                writer.Start();
                var outcome = await done.Task;
                creating = false;
                _ = store.RefreshAllAsync();
                if (outcome.Outcome == BrainDumpWriteOutcome.Complete)
                {
                    window.Close();
                    return;
                }
                if (outcome.Outcome == BrainDumpWriteOutcome.Failed)
                    error.Text = ToDoStrings.Format(s.BrainDumpPartialSave, ToDoStrings.CountToken, outcome.ConfirmedCount.ToString(CultureInfo.InvariantCulture));
                render();
            };

            render = () =>
            {
                root.Children.Clear();
                bool reviewing = suggestions.Count > 0;
                var close = StatsUi.Button(s.Close, (o, e) => window.Close());
                close.IsEnabled = !creating;
                AutomationProperties.SetAutomationId(close, "btnBrainDumpClose");
                int valid = suggestions.Count(t => BrainDumpTaskPayload.Body(t, today) != null);
                UIElement[] trailing = new UIElement[] { close };
                if (reviewing)
                {
                    var createButton = StatsUi.Button(creating ? s.Saving : s.CreateTasks, async (o, e) => await create(), primary: true);
                    createButton.Margin = new Thickness(0);
                    createButton.IsEnabled = !creating && valid > 0 && valid == suggestions.Count;
                    AutomationProperties.SetAutomationId(createButton, "btnBrainDumpCreate");
                    trailing = new UIElement[] { close, createButton };
                }
                var header = Header(reviewing ? s.TasksFromBrainDump : s.BrainDumpTitle, trailing);
                DockPanel.SetDock(header, Dock.Top);
                root.Children.Add(header);
                DockPanel.SetDock(error, Dock.Bottom);
                root.Children.Add(error);
                if (!reviewing)
                {
                    ((TextBlock)go.Content).Text = converting ? s.Converting : "\u2728  " + s.BrainDumpToTasks;
                    go.IsEnabled = !converting && input.Text.Trim().Length > 0;
                    root.Children.Add(inputPage);
                    return;
                }
                var list = new StackPanel();
                var willCreate = StatsUi.Text(ToDoStrings.Format(s.TasksWillBeCreated, ToDoStrings.CountToken, valid.ToString(CultureInfo.InvariantCulture)), 12, secondary: true);
                willCreate.Margin = new Thickness(0, 0, 0, 8);
                list.Children.Add(willCreate);
                if (valid != suggestions.Count)
                {
                    var invalid = StatsUi.Text(s.BrainDumpInvalidDraft, 12);
                    invalid.Foreground = Overdue;
                    invalid.Margin = new Thickness(0, 0, 0, 8);
                    list.Children.Add(invalid);
                }
                foreach (var suggestion in suggestions.ToList())
                    list.Children.Add(SuggestionRow(s, suggestion, creating, () => { suggestions.Remove(suggestion); render(); }, render));
                root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            };
            render();
            window.Loaded += (o, e) => input.Focus();
            window.ShowDialog();
        }

        private static FrameworkElement SuggestionRow(ToDoStrings s, BrainDumpTask task, bool locked, Action remove, Action changed)
        {
            var stack = new StackPanel();
            var row = new DockPanel();
            var delete = new Button { Content = Glyph(0xE74D), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Focusable = false, IsEnabled = !locked };
            delete.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
            delete.Click += (o, e) => remove();
            DockPanel.SetDock(delete, Dock.Right);
            row.Children.Add(delete);
            var unit = StatsUi.Text(s.Minutes, 11, secondary: true, wrap: false);
            unit.VerticalAlignment = VerticalAlignment.Center;
            unit.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(unit, Dock.Right);
            row.Children.Add(unit);
            var minutes = new TextBox { Text = task.EstimatedMinutes.ToString(CultureInfo.InvariantCulture), IsEnabled = !locked };
            minutes.TextChanged += (o, e) =>
            {
                long.TryParse(minutes.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value);
                bool wasValid = BrainDumpTaskPayload.Body(task, "2000-01-01") != null;
                task.EstimatedMinutes = value;
                if (wasValid != (BrainDumpTaskPayload.Body(task, "2000-01-01") != null)) changed();
            };
            var minutesField = Field(minutes, "");
            minutesField.Width = 64;
            minutesField.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(minutesField, Dock.Right);
            row.Children.Add(minutesField);
            var name = new TextBox { Text = task.TaskName, IsEnabled = !locked };
            AutomationProperties.SetAutomationId(name, "txtBrainDumpTaskName");
            name.TextChanged += (o, e) =>
            {
                bool wasValid = !string.IsNullOrWhiteSpace(task.TaskName);
                task.TaskName = name.Text;
                if (wasValid != !string.IsNullOrWhiteSpace(task.TaskName)) changed();
            };
            row.Children.Add(Field(name, ""));
            stack.Children.Add(row);
            if (task.SubtaskList.Count > 0)
            {
                var subtasks = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                var caption = StatsUi.Text(s.Subtasks + ":", 11, secondary: true, wrap: false);
                caption.Margin = new Thickness(0, 0, 8, 0);
                subtasks.Children.Add(caption);
                foreach (var subtask in task.SubtaskList.ToList())
                {
                    var chip = Chip(subtask, glyph: "•");
                    var removeSubtask = new Button { Content = chip, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = s.Delete, Focusable = false, IsEnabled = !locked, Margin = new Thickness(0, 0, 0, 4) };
                    removeSubtask.Click += (o, e) => { task.Subtasks.Remove(subtask); changed(); };
                    subtasks.Children.Add(removeSubtask);
                }
                stack.Children.Add(subtasks);
            }
            var card = new Border { Child = stack, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetName(card, task.TaskName);
            return card;
        }

        // ------------------------------------------------------------------ Eisenhower matrix

        /// <summary>The open loaded tasks by quadrant; a task opens the editor.</summary>
        internal static void ShowMatrix(Window owner, ToDoStore store, Action<ToDoTask> openEditor)
        {
            var s = store.Strings;
            var window = Dialog(owner, s.EisenhowerMatrix, 900, 640);
            AutomationProperties.SetAutomationId(window, "ToDoMatrix");
            var root = new DockPanel { Margin = new Thickness(20) };
            var close = StatsUi.Button(s.Close, (o, e) => window.Close(), primary: true);
            close.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(close, "btnMatrixClose");
            var header = Header(s.EisenhowerMatrix, close);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Action<string, int, int> axis = (text, row, column) =>
            {
                var label = StatsUi.Text(text, 12, FontWeights.SemiBold, secondary: true, wrap: false);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.Margin = new Thickness(0, 0, 0, 6);
                if (column == 0) label.LayoutTransform = new RotateTransform(-90);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, column);
                grid.Children.Add(label);
            };
            axis(s.Urgent, 0, 1);
            axis(s.NotUrgent, 0, 2);
            axis(s.Important, 1, 0);
            axis(s.NotImportant, 2, 0);
            var open = store.LoadedTasks.Where(task => !task.IsCompleted).ToList();
            var cells = new[]
            {
                Tuple.Create(EisenhowerQuadrant.DoIt, 1, 1, Overdue), Tuple.Create(EisenhowerQuadrant.Decide, 1, 2, TodayTint),
                Tuple.Create(EisenhowerQuadrant.Delegate, 2, 1, Info), Tuple.Create(EisenhowerQuadrant.Delete, 2, 2, Neutral),
            };
            foreach (var cell in cells)
            {
                var tasks = open.Where(task => (EisenhowerQuadrant)(task.EisenhowerQuadrant ?? 1) == cell.Item1).ToList();
                var stack = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(StatsUi.Text(s.QuadrantLabel(cell.Item1), 14, FontWeights.Bold, wrap: false));
                var count = Chip(tasks.Count.ToString(CultureInfo.InvariantCulture), cell.Item4);
                count.Margin = new Thickness(8, 0, 0, 0);
                head.Children.Add(count);
                stack.Children.Add(head);
                var subtitle = StatsUi.Text(s.QuadrantSubtitle(cell.Item1), 11, secondary: true);
                subtitle.Margin = new Thickness(0, 2, 0, 8);
                stack.Children.Add(subtitle);
                foreach (var task in tasks)
                {
                    var line = new DockPanel();
                    if (!string.IsNullOrEmpty(task.DueDate))
                    {
                        var due = StatsUi.Text(s.DueLabel(task.DueDate, DateTime.Now), 10, secondary: true, wrap: false);
                        due.VerticalAlignment = VerticalAlignment.Center;
                        DockPanel.SetDock(due, Dock.Right);
                        line.Children.Add(due);
                    }
                    line.Children.Add(StatsUi.Text(task.Title, 12, wrap: false));
                    var item = new Border { Child = line, CornerRadius = new CornerRadius(5), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 4) };
                    item.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
                    var button = new Button { Content = item, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false };
                    var captured = task;
                    button.Click += (o, e) => { window.Close(); openEditor(captured); };
                    stack.Children.Add(button);
                }
                var box = new Border
                {
                    Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                    Background = StatsUi.Tint(cell.Item4, 0.12), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(4),
                };
                AutomationProperties.SetAutomationId(box, "cellMatrix" + cell.Item1);
                Grid.SetRow(box, cell.Item2);
                Grid.SetColumn(box, cell.Item3);
                grid.Children.Add(box);
            }
            root.Children.Add(grid);
            window.Content = root;
            window.ShowDialog();
        }

        // ------------------------------------------------------------------ Procrastinate

        /// <summary>Park a distracting link for later and get back to the task; the list opens, deletes, or clears them.</summary>
        internal static void ShowProcrastinate(Window owner, ToDoStore store)
        {
            var s = store.Strings;
            var window = Dialog(owner, s.Procrastinate, 680, 600);
            AutomationProperties.SetAutomationId(window, "ToDoProcrastinate");
            var api = new API.FocusBearAPIClient();
            var sites = new List<SavedWebsite>();
            var selected = new HashSet<string>();
            bool loading = true, adding = false;
            var root = new DockPanel { Margin = new Thickness(20) };
            var url = new TextBox();
            var note = new TextBox { MaxLength = ToDoTools.SavedWebsiteNoteMaxLength };
            AutomationProperties.SetAutomationId(url, "txtProcrastinateUrl");
            AutomationProperties.SetAutomationId(note, "txtProcrastinateNote");
            var urlField = Field(url, "https://");
            var noteField = Field(note, s.Note);
            var urlError = ErrorText();
            var status = StatsUi.Text("", 12, secondary: true);
            var list = new StackPanel();
            Button addButton = null, deleteSelected = null;

            Action refreshForm = () =>
            {
                string value = url.Text.Trim();
                string problem = value.Length == 0 ? null : !ToDoTools.IsValidUrl(value) ? s.InvalidUrl : ToDoTools.IsDuplicateUrl(value, sites) ? s.DuplicateUrl : null;
                urlError.Text = problem ?? "";
                urlError.Visibility = problem == null ? Visibility.Collapsed : Visibility.Visible;
                addButton.IsEnabled = value.Length > 0 && problem == null && !adding;
                deleteSelected.Visibility = selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            Action drawList = null;
            Func<Task> load = async () =>
            {
                loading = true;
                drawList();
                var result = await api.GetSavedWebsites();
                loading = false;
                if (result == null) status.Text = s.RequestFailed;
                else sites = result.ToList();
                selected.IntersectWith(sites.Select(site => site.Id));
                drawList();
                refreshForm();
            };

            drawList = () =>
            {
                list.Children.Clear();
                if (loading && sites.Count == 0) { list.Children.Add(StatsUi.Loading()); return; }
                if (sites.Count == 0) { list.Children.Add(StatsUi.Empty("\U0001F516", s.NoUrlsFound)); return; }
                foreach (var site in sites)
                {
                    var row = new DockPanel();
                    var check = new CheckBox { IsChecked = selected.Contains(site.Id), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 8, 0) };
                    var captured = site;
                    // Checked/Unchecked rather than Click, which UI Automation (a screen reader's toggle) never raises.
                    RoutedEventHandler changed = (o, e) => { if (check.IsChecked == true) selected.Add(captured.Id); else selected.Remove(captured.Id); refreshForm(); };
                    check.Checked += changed;
                    check.Unchecked += changed;
                    DockPanel.SetDock(check, Dock.Left);
                    row.Children.Add(check);
                    var trash = new Button { Content = Glyph(0xE74D), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = s.Delete, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 2, 0, 0), Focusable = false };
                    trash.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                    AutomationProperties.SetAutomationId(trash, "btnProcrastinateDelete_" + site.Id);
                    trash.Click += async (o, e) =>
                    {
                        if (await api.DeleteSavedWebsite(captured.Id)) { sites.Remove(captured); selected.Remove(captured.Id); drawList(); refreshForm(); }
                        else status.Text = s.CouldNotDeleteWebsites;
                    };
                    DockPanel.SetDock(trash, Dock.Right);
                    row.Children.Add(trash);
                    var text = new StackPanel();
                    if (!string.IsNullOrWhiteSpace(site.Title)) text.Children.Add(StatsUi.Text(site.Title, 12, FontWeights.SemiBold));
                    var link = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
                    var hyperlink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(site.Url)) { Foreground = Info };
                    hyperlink.Click += (o, e) => { string target = ToDoTools.OpenableUrl(captured.Url); if (target != null) StatsUi.OpenExternal(target); };
                    link.Inlines.Add(hyperlink);
                    text.Children.Add(link);
                    if (!string.IsNullOrWhiteSpace(site.Note)) text.Children.Add(StatsUi.Text(s.Note + ": " + site.Note, 11, secondary: true));
                    row.Children.Add(text);
                    var card = new Border { Child = row, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1) };
                    card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                    card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                    AutomationProperties.SetName(card, site.Url);
                    list.Children.Add(card);
                }
            };

            var close = StatsUi.Button(s.Close, (o, e) => window.Close(), primary: true);
            close.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(close, "btnProcrastinateClose");
            deleteSelected = StatsUi.Button(s.DeleteAll, async (o, e) =>
            {
                var ids = selected.ToList();
                if (ids.Count == 0) return;
                if (await api.DeleteSavedWebsites(ids)) { status.Text = s.WebsitesDeleted; selected.Clear(); await load(); }
                else status.Text = s.CouldNotDeleteWebsites;
            });
            AutomationProperties.SetAutomationId(deleteSelected, "btnProcrastinateDeleteSelected");
            var header = Header(s.Procrastinate, deleteSelected, close);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var form = new StackPanel();
            var info = StatsUi.Text(s.ProcrastinateInfo, 12, secondary: true);
            info.Margin = new Thickness(0, 0, 0, 10);
            form.Children.Add(info);
            form.Children.Add(urlField);
            form.Children.Add(urlError);
            var noteRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            addButton = StatsUi.Button(s.Add, async (o, e) =>
            {
                string value = url.Text.Trim();
                if (!ToDoTools.IsValidUrl(value) || ToDoTools.IsDuplicateUrl(value, sites) || adding) return;
                adding = true;
                refreshForm();
                bool ok = await api.AddSavedWebsite(value, note.Text.Trim());
                adding = false;
                if (ok) { url.Text = ""; note.Text = ""; status.Text = ""; await load(); }
                else { status.Text = s.RequestFailed; refreshForm(); }
            }, primary: true);
            addButton.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetAutomationId(addButton, "btnProcrastinateAdd");
            DockPanel.SetDock(addButton, Dock.Right);
            noteRow.Children.Add(addButton);
            noteRow.Children.Add(noteField);
            form.Children.Add(noteRow);
            status.Margin = new Thickness(0, 6, 0, 6);
            form.Children.Add(status);
            var divider = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 10) };
            divider.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            form.Children.Add(divider);
            DockPanel.SetDock(form, Dock.Top);
            root.Children.Add(form);
            root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            url.TextChanged += (o, e) => refreshForm();
            url.KeyDown += (o, e) => { if (e.Key == Key.Enter && addButton.IsEnabled) addButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); };
            window.Content = root;
            refreshForm();
            window.Loaded += async (o, e) => { url.Focus(); await load(); };
            window.ShowDialog();
        }
    }
}
