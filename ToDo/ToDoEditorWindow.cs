using Focus_Bear.StatsScreens;
using Focus_Bear.Utils;
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
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The task editor — the Mac's <c>ToDoEditorSheet</c>: title and completion, status (or the owning
    /// project's own columns), the TOP card (due date, outcome, perspiration and minutes), projects,
    /// description and subtasks. Save sends one <c>PUT /to-do</c>; closing with edits asks first.
    /// </summary>
    internal sealed class ToDoEditorWindow : Window
    {
        private const int MaxVisibleProjectChips = 12;
        private const int MaxSegmentedStatuses = 4;

        private readonly ToDoStore _store;
        private readonly ToDoStrings _strings;
        private readonly ToDoTask _original;
        private readonly ToDoTask _draft;
        private readonly bool _isNew;
        private readonly string _originalDetailsHtml;
        private readonly string _openingDetailsText;
        private readonly string _openingSnapshot;
        private readonly List<string> _selectedProjectIds;
        private string _statusOwnerProjectId;
        private bool _saving;
        private bool _closeConfirmed;

        private readonly TextBox _title = new TextBox();
        private readonly TextBox _dueInput = new TextBox();
        private readonly TextBox _duration = new TextBox();
        private readonly TextBox _projectSearch = new TextBox();
        private readonly TextBox _details = new TextBox();
        private readonly TextBox _pendingSubtask = new TextBox();
        private readonly Slider _perspiration = new Slider { Minimum = ToDoScoring.MinLevel, Maximum = ToDoScoring.MaxLevel, IsSnapToTickEnabled = true, TickFrequency = 1, Width = 180 };
        private readonly ContentControl _check = new ContentControl();
        private readonly ContentControl _statusArea = new ContentControl();
        private readonly ContentControl _projectsArea = new ContentControl();
        private readonly ContentControl _subtasksArea = new ContentControl();
        private readonly ContentControl _outcomeArea = new ContentControl();
        private readonly TextBlock _topScore = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        private readonly TextBlock _duePreview;
        private readonly TextBlock _perspirationLabel;
        private readonly TextBlock _titleWarning;
        private readonly TextBlock _error;
        private Button _saveButton;
        private Button _generateButton;
        private bool _syncingPerspiration;

        public ToDoEditorWindow(ToDoStore store, ToDoTask task, string initialTitle, Project initialProject = null)
        {
            _store = store;
            _strings = store.Strings;
            _isNew = task == null;
            // A new task opened from a project page is linked to it, even before the catalogue lists it.
            store.EnsureProject(initialProject);
            _original = task != null ? ToDoDraft.Editable(task) : ToDoDraft.NewTask(initialTitle ?? "", DateTime.Now, initialProject);
            _draft = _original.Copy();
            _originalDetailsHtml = task?.Details;
            _openingDetailsText = _original.Details ?? "";
            _selectedProjectIds = _original.TagList.Select(tag => tag.Id).ToList();
            _statusOwnerProjectId = _original.ProjectId;

            Title = _isNew ? _strings.CreateTaskTitle : _strings.EditTaskTitle;
            Width = 720;
            Height = 720;
            MinWidth = 560;
            MinHeight = 480;
            ShowInTaskbar = false;
            // Header (Save/Cancel) docked on top, body in a ScrollViewer: capped to the screen, the body scrolls.
            DialogFit.Apply(this);
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards
            AutomationProperties.SetAutomationId(this, "ToDoEditor");

            _duePreview = StatsUi.Text("", 11, secondary: true);
            _perspirationLabel = StatsUi.Text("", 11, secondary: true);
            _titleWarning = StatsUi.Text(_strings.TitleIsNeeded, 11);
            _titleWarning.Foreground = TodayTint;
            _error = StatsUi.Text("", 12);
            _error.Foreground = Overdue;

            _title.Text = _draft.Title;
            _dueInput.Text = DueFieldText(ToDoDates.Parse(_draft.DueDate));
            _duration.Text = DurationText(_draft);
            _details.Text = _openingDetailsText;
            _perspiration.Value = _draft.EffectivePerspiration;
            _openingSnapshot = Snapshot();

            Content = BuildLayout();
            RefreshAll();

            _title.TextChanged += (s, e) => { _draft.Title = _title.Text; RefreshHeader(); };
            _title.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = SaveAsync(); } };
            _dueInput.TextChanged += (s, e) => RefreshTop();
            _duration.TextChanged += (s, e) => DurationChanged();
            _perspiration.ValueChanged += (s, e) => PerspirationChanged();
            _projectSearch.TextChanged += (s, e) => RefreshProjects();
            _projectSearch.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = CreateProjectFromSearchAsync(); } };
            _pendingSubtask.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; AddSubtask(); } };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; _ = SaveAsync(); }
                else if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            };
            Closing += (s, e) =>
            {
                if (_closeConfirmed || _saving || !HasChanges) return;
                var confirm = new FBMessageBox(_strings.UnsavedChanges, Title, MessageBoxButton.YesNo, _strings.Discard, _strings.Cancel) { Owner = this };
                confirm.ShowDialog();
                if (confirm.btnClicked != MessageBoxResult.Yes) e.Cancel = true;
            };
            Loaded += (s, e) => { _title.Focus(); _title.CaretIndex = _title.Text.Length; };
        }

        private static string DueFieldText(DateTime? day) => ToDoDueDateFieldText.Text(day, ToDoStrings.Culture);

        /// <summary>What the duration field starts with: the explicit minutes, else the perspiration's.</summary>
        private static string DurationText(ToDoTask task) =>
            (ToDoScoring.DurationSeconds(task.DurationSeconds, task.EffectivePerspiration) / 60).ToString(CultureInfo.InvariantCulture);

        internal bool HasChanges => Snapshot() != _openingSnapshot;

        private string Snapshot()
        {
            return string.Join("\u001f", new[]
            {
                _title.Text.Trim(), _draft.Status, _draft.CustomStatusId ?? "", _dueInput.Text.Trim(), _draft.Outcome?.ToString() ?? "",
                _draft.PerspirationLevel?.ToString() ?? "", string.Join(",", _selectedProjectIds), _statusOwnerProjectId ?? "",
                _duration.Text.Trim(), _details.Text, string.Join("|", _draft.SubtaskList.Select(st => st.Name + (st.IsCompleted ? "1" : "0"))),
                _pendingSubtask.Text.Trim(),
            });
        }

        /// <summary>
        /// The catalogue lists active projects only; a tag it does not know (a completed or archived project, or
        /// a catalogue that failed to load) still shows, and still goes back on Save.
        /// </summary>
        private IList<Project> SelectedProjects => ToDoEditorProjects.Selected(_selectedProjectIds, _original.TagList, _store.Project);

        /// <summary>Web <c>resolveStatusOwnerProjectId</c>.</summary>
        private string ResolvedOwnerProjectId
        {
            get
            {
                if (_statusOwnerProjectId != null && _selectedProjectIds.Contains(_statusOwnerProjectId)) return _statusOwnerProjectId;
                var selected = SelectedProjects;
                if (!string.IsNullOrEmpty(_draft.CustomStatusId))
                {
                    var owner = selected.FirstOrDefault(project => project.CustomStatuses.Any(status => status.Id == _draft.CustomStatusId));
                    if (owner != null) return owner.Id;
                }
                var custom = selected.FirstOrDefault(project => project.UsesCustomStatuses);
                if (custom != null) return custom.Id;
                return _selectedProjectIds.FirstOrDefault();
            }
        }

        private Project OwnerProject => _store.Project(ResolvedOwnerProjectId);

        /// <summary>The draft as the toggle should see it: owned by the resolved project, even before Save sets it.</summary>
        private ToDoTask WithOwner(ToDoTask draft)
        {
            var copy = draft.Copy();
            copy.ProjectId = ResolvedOwnerProjectId;
            return copy;
        }

        private DateTime? ParsedDue => string.IsNullOrWhiteSpace(_dueInput.Text) ? (DateTime?)null : ToDoDueDateParser.Parse(_dueInput.Text, DateTime.Now, ToDoStrings.Culture);

        private bool DueIsInvalid => !string.IsNullOrWhiteSpace(_dueInput.Text) && !ParsedDue.HasValue;

        // ------------------------------------------------------------------ layout

        private UIElement BuildLayout()
        {
            var header = new DockPanel { Margin = new Thickness(20, 16, 20, 10), LastChildFill = false };
            var heading = StatsUi.Text(Title, 18, FontWeights.Bold, wrap: false);
            heading.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(heading, Dock.Left);
            header.Children.Add(heading);
            if (!_isNew && !string.IsNullOrEmpty(_original.CreatedAt))
            {
                var created = StatsUi.Text(ToDoStrings.Format(_strings.Created, ToDoStrings.DateToken, _strings.Abbreviated(_original.CreatedAt)), 11, secondary: true, wrap: false);
                created.VerticalAlignment = VerticalAlignment.Center;
                created.Margin = new Thickness(10, 3, 0, 0);
                DockPanel.SetDock(created, Dock.Left);
                header.Children.Add(created);
            }
            _saveButton = StatsUi.Button(_strings.Save, async (s, e) => await SaveAsync(), primary: true);
            _saveButton.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(_saveButton, "btnToDoEditorSave");
            DockPanel.SetDock(_saveButton, Dock.Right);
            header.Children.Add(_saveButton);
            var cancel = StatsUi.Button(_strings.Cancel, (s, e) => Close());
            AutomationProperties.SetAutomationId(cancel, "btnToDoEditorCancel");
            DockPanel.SetDock(cancel, Dock.Right);
            header.Children.Add(cancel);

            var body = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };

            var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            _check.Margin = new Thickness(0, 6, 10, 0);
            DockPanel.SetDock(_check, Dock.Left);
            titleRow.Children.Add(_check);
            AutomationProperties.SetAutomationId(_title, "txtToDoEditorTitle");
            titleRow.Children.Add(Field(_title, _strings.AddATask, 36));
            _title.FontSize = 15;
            body.Children.Add(titleRow);
            _titleWarning.Margin = new Thickness(30, 0, 0, 6);
            body.Children.Add(_titleWarning);

            body.Children.Add(SectionTitle(_strings.Status));
            body.Children.Add(_statusArea);

            body.Children.Add(TopCard());

            body.Children.Add(_projectsArea);

            body.Children.Add(SectionTitle(_strings.Description));
            _details.AcceptsReturn = true;
            _details.TextWrapping = TextWrapping.Wrap;
            _details.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            AutomationProperties.SetAutomationId(_details, "txtToDoEditorDetails");
            var detailsField = Field(_details, _strings.Description, 90);
            _details.VerticalContentAlignment = VerticalAlignment.Top;
            body.Children.Add(detailsField);

            var subtasksHeader = new DockPanel { Margin = new Thickness(0, 16, 0, 6), LastChildFill = false };
            var subtasksTitle = StatsUi.Text(_strings.CreateSubtasks, 12, FontWeights.SemiBold, wrap: false);
            subtasksTitle.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(subtasksTitle, Dock.Left);
            subtasksHeader.Children.Add(subtasksTitle);
            _generateButton = SmallButton(_strings.GenerateSubtasksViaAI, async (s, e) => await GenerateSubtasksAsync(), glyph: Glyph(0xE945));
            _generateButton.Margin = new Thickness(0);
            DockPanel.SetDock(_generateButton, Dock.Right);
            subtasksHeader.Children.Add(_generateButton);
            body.Children.Add(subtasksHeader);
            body.Children.Add(_subtasksArea);
            var addRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var add = SmallButton(_strings.AddSubtask, (s, e) => AddSubtask(), glyph: Glyph(0xE710));
            add.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(add, Dock.Right);
            addRow.Children.Add(add);
            AutomationProperties.SetAutomationId(_pendingSubtask, "txtToDoEditorSubtask");
            addRow.Children.Add(Field(_pendingSubtask, _strings.AddASubtask));
            body.Children.Add(addRow);

            if (!_isNew)
            {
                var discussionStatus = StatsUi.Text("", 11, secondary: true);
                AutomationProperties.SetAutomationId(discussionStatus, "txtToDoDiscussionStatus");
                body.Children.Add(new ToDoTaskDiscussion(_original.Id, _strings, AppSettings.GetInstance.userAccount?.userID, message => discussionStatus.Text = message));
                body.Children.Add(discussionStatus);
                body.Children.Add(new ToDoTaskAttachments(_original.Id, _strings));
                var delete = StatsUi.Button(_strings.DeleteTask, async (s, e) => await DeleteAsync());
                delete.HorizontalAlignment = HorizontalAlignment.Left;
                delete.Margin = new Thickness(0, 22, 0, 0);
                AutomationProperties.SetAutomationId(delete, "btnToDoEditorDelete");
                body.Children.Add(delete);
            }
            _error.Margin = new Thickness(0, 10, 0, 0);
            body.Children.Add(_error);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
            var root = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            root.Children.Add(scroll);
            return root;
        }

        private static TextBlock SectionTitle(string text)
        {
            var title = StatsUi.Text(text, 12, FontWeights.SemiBold, wrap: false);
            title.Margin = new Thickness(0, 14, 0, 6);
            return title;
        }

        private FrameworkElement TopCard()
        {
            var stack = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var bolt = StatsUi.Text(Glyph(0xE945), 13, wrap: false);
            bolt.FontFamily = new FontFamily("Segoe MDL2 Assets");
            bolt.Foreground = Accent;
            bolt.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(bolt);
            var title = StatsUi.Text(_strings.TopScore, 12, FontWeights.SemiBold, wrap: false);
            title.Margin = new Thickness(6, 0, 8, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(title);
            AutomationProperties.SetAutomationId(_topScore, "txtToDoEditorTop");
            head.Children.Add(new Border { Child = _topScore, Background = Accent, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(head);

            var columns = new Grid();
            for (int i = 0; i < 3; i++) columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var due = TopColumn("T", _strings.TimelineDueDate);
            AutomationProperties.SetAutomationId(_dueInput, "txtToDoEditorDue");
            _dueInput.ToolTip = _strings.DueDateGrammarHelp;
            due.Children.Add(Field(_dueInput, _strings.DueDatePlaceholder));
            _duePreview.Margin = new Thickness(2, 4, 0, 0);
            due.Children.Add(_duePreview);
            columns.Children.Add(due);

            var outcome = TopColumn("O", _strings.Outcome);
            outcome.Children.Add(_outcomeArea);
            Grid.SetColumn(outcome, 1);
            columns.Children.Add(outcome);

            var perspiration = TopColumn("P", _strings.Perspiration);
            AutomationProperties.SetAutomationId(_perspiration, "sldToDoEditorPerspiration");
            perspiration.Children.Add(_perspiration);
            perspiration.Children.Add(_perspirationLabel);
            var minutes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            AutomationProperties.SetAutomationId(_duration, "txtToDoEditorMinutes");
            var durationField = Field(_duration, "");
            durationField.Width = 70;
            minutes.Children.Add(durationField);
            var unit = StatsUi.Text(_strings.Minutes, 11, secondary: true, wrap: false);
            unit.VerticalAlignment = VerticalAlignment.Center;
            unit.Margin = new Thickness(6, 0, 0, 0);
            minutes.Children.Add(unit);
            perspiration.Children.Add(minutes);
            Grid.SetColumn(perspiration, 2);
            columns.Children.Add(perspiration);
            stack.Children.Add(columns);

            var card = StatsUi.Card(stack, padding: new Thickness(14));
            card.Margin = new Thickness(0, 16, 0, 4);
            return card;
        }

        private static StackPanel TopColumn(string letter, string title)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            var head = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
            head.Inlines.Add(new System.Windows.Documents.Run(letter + "  ") { FontWeight = FontWeights.Bold, Foreground = Accent, FontSize = 13 });
            var run = new System.Windows.Documents.Run(title) { FontSize = 12, FontWeight = FontWeights.Medium };
            head.Inlines.Add(run);
            head.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            stack.Children.Add(head);
            return stack;
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshStatus();
            RefreshTop();
            RefreshProjects();
            RefreshSubtasks();
        }

        private void RefreshHeader()
        {
            bool blank = string.IsNullOrWhiteSpace(_title.Text);
            _titleWarning.Visibility = blank ? Visibility.Visible : Visibility.Collapsed;
            if (_saveButton != null)
            {
                _saveButton.IsEnabled = !blank && !_saving && !DueIsInvalid;
                ((TextBlock)_saveButton.Content).Text = _saving ? _strings.Saving : _strings.Save;
                _saveButton.ToolTip = blank ? _strings.TitleIsNeeded : _strings.Save;
            }
            if (_generateButton != null) _generateButton.IsEnabled = !blank;
            var check = CompletionCheck(_draft.IsCompleted, _draft.IsCompleted ? _strings.MarkIncomplete : _strings.MarkCompleteShort, (s, e) =>
            {
                _draft.CopyStatusFrom(ToDoStatusToggle.ApplyWithOwner(WithOwner(_draft), !_draft.IsCompleted, OwnerProject));
                RefreshHeader();
                RefreshStatus();
                RefreshTop();
            }, 22);
            AutomationProperties.SetAutomationId(check, "btnToDoEditorCheck");
            _check.Content = check;
        }

        private void RefreshStatus()
        {
            var panel = new StackPanel();
            var selected = SelectedProjects;
            if (selected.Count > 1)
            {
                var owner = OwnerProject;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                var label = StatsUi.Text(_strings.StatusAppliesTo, 11, secondary: true, wrap: false);
                label.VerticalAlignment = VerticalAlignment.Center;
                label.Margin = new Thickness(0, 0, 8, 6);
                row.Children.Add(label);
                row.Children.Add(MenuButton(_strings.ProjectName(owner?.Name ?? ""), false, menu =>
                {
                    foreach (var project in selected)
                    {
                        var captured = project.Id;
                        menu.Add(_strings.ProjectName(project.Name), captured == ResolvedOwnerProjectId, () =>
                        {
                            _statusOwnerProjectId = captured;
                            DropStaleCustomStatus();
                            RefreshStatus();
                        });
                    }
                }));
                panel.Children.Add(row);
            }

            var ownerProject = OwnerProject;
            if (ownerProject != null && ownerProject.UsesCustomStatuses)
            {
                var ordered = ownerProject.OrderedStatuses;
                string current = _draft.CustomStatusId ?? ordered.First().Id;
                Action<string> choose = id =>
                {
                    _draft.CustomStatusId = id;
                    bool completes = ordered.FirstOrDefault(status => status.Id == id)?.Completes ?? false;
                    _draft.Status = completes ? ToDoStatusWire.Completed : (id == ordered.First().Id ? ToDoStatusWire.NotStarted : ToDoStatusWire.InProgress);
                    RefreshHeader();
                    RefreshStatus();
                    RefreshTop();
                };
                if (ordered.Count <= MaxSegmentedStatuses)
                {
                    panel.Children.Add(StatsUi.Segmented(ordered.Select(status => status.Id).ToArray(), current,
                                                         id => ordered.First(status => status.Id == id).Label, choose));
                }
                else
                {
                    var label = ordered.FirstOrDefault(status => status.Id == current)?.Label ?? "";
                    panel.Children.Add(MenuButton(label, false, menu =>
                    {
                        foreach (var status in ordered)
                        {
                            var captured = status.Id;
                            menu.Add(status.Label, captured == current, () => choose(captured));
                        }
                    }));
                }
            }
            else
            {
                var statuses = new[] { ToDoStatus.NotStarted, ToDoStatus.InProgress, ToDoStatus.Completed };
                var segmented = StatsUi.Segmented(statuses, _draft.DefaultStatus, _strings.StatusLabel, status =>
                {
                    _draft.Status = ToDoStatusWire.Of(status);
                    _draft.CustomStatusId = null;
                    RefreshHeader();
                    RefreshStatus();
                    RefreshTop();
                });
                AutomationProperties.SetAutomationId(segmented, "segToDoEditorStatus");
                panel.Children.Add(segmented);
            }
            _statusArea.Content = panel;
        }

        private void RefreshTop()
        {
            var day = ParsedDue;
            bool typing = !string.IsNullOrWhiteSpace(_dueInput.Text);
            _duePreview.Text = !typing ? _strings.NoDueDate : day.HasValue ? _strings.DueLabel(ToDoDates.Ymd(day.Value), DateTime.Now) : _strings.UnrecognisedDate;
            if (typing && !day.HasValue) _duePreview.Foreground = Overdue;
            else _duePreview.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            double score = _draft.IsCompleted ? ToDoScoring.CompletedScore
                : ToDoScoring.TopScore(_draft.EffectiveOutcome, _draft.EffectivePerspiration, day, DateTime.Now);
            _topScore.Text = ToDoScoring.Rounded(score);
            _perspirationLabel.Text = _draft.EffectivePerspiration + " · " + _strings.PerspirationLabel(_draft.EffectivePerspiration);

            int? wire = _draft.Outcome;
            int step = wire.HasValue ? Math.Min(ToDoScoring.OutcomeSteps, Math.Max(1, (int)Math.Round(wire.Value / (double)ToDoScoring.OutcomeWireFactor, MidpointRounding.AwayFromZero))) : 0;
            string outcomeText = wire.HasValue ? step + " / " + ToDoScoring.OutcomeSteps : _strings.NotSet;
            var outcomeButton = MenuButton(outcomeText, false, menu =>
            {
                menu.Add(_strings.NotSet, !wire.HasValue, () => { _draft.Outcome = null; RefreshTop(); });
                for (int option = 1; option <= ToDoScoring.OutcomeSteps; option++)
                {
                    int value = option * ToDoScoring.OutcomeWireFactor;
                    menu.Add(_strings.OutcomeOptionLabel(option), wire == value, () => { _draft.Outcome = value; RefreshTop(); }, "menuToDoOutcome" + option);
                }
            }, "btnToDoEditorOutcome");
            outcomeButton.HorizontalAlignment = HorizontalAlignment.Left;
            var outcomeStack = new StackPanel();
            outcomeStack.Children.Add(outcomeButton);
            if (wire.HasValue)
            {
                var words = StatsUi.Text(_strings.OutcomeLabel(wire.Value), 11, secondary: true);
                AutomationProperties.SetAutomationId(words, "txtToDoEditorOutcomeLabel");
                outcomeStack.Children.Add(words);
            }
            _outcomeArea.Content = outcomeStack;
            RefreshHeader();
        }

        private void PerspirationChanged()
        {
            if (_syncingPerspiration) return;
            int level = (int)Math.Round(_perspiration.Value);
            _draft.PerspirationLevel = level;
            _syncingPerspiration = true;
            _duration.Text = ToDoScoring.MinutesForPerspiration(level).ToString(CultureInfo.InvariantCulture);
            _syncingPerspiration = false;
            RefreshTop();
        }

        /// <summary>Typed minutes move the Perspiration slider to the level that covers them, and back.</summary>
        private void DurationChanged()
        {
            if (_syncingPerspiration) return;
            if (!int.TryParse(_duration.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes) || minutes <= 0) return;
            int level = ToDoScoring.PerspirationForMinutes(minutes, _draft.EffectivePerspiration);
            if (level == _draft.EffectivePerspiration) return;
            _draft.PerspirationLevel = level;
            _syncingPerspiration = true;
            _perspiration.Value = level;
            _syncingPerspiration = false;
            RefreshTop();
        }

        private void RefreshProjects()
        {
            var stack = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 6) };
            head.Children.Add(StatsUi.Text(_strings.Projects, 12, FontWeights.SemiBold, wrap: false));
            var count = StatsUi.Text(_selectedProjectIds.Count + " / " + _store.Projects.Count, 11, secondary: true, wrap: false);
            count.Margin = new Thickness(8, 1, 0, 0);
            head.Children.Add(count);
            stack.Children.Add(head);

            var selected = SelectedProjects;
            if (selected.Count > 0) stack.Children.Add(ChipRow(selected, true));

            if (_projectSearchField == null)
            {
                AutomationProperties.SetAutomationId(_projectSearch, "txtToDoEditorProjectSearch");
                _projectSearchField = Field(_projectSearch, _strings.SearchProjectsPlaceholder);
                _projectSearchField.Width = 320;
                _projectSearchField.HorizontalAlignment = HorizontalAlignment.Left;
                _projectSearchField.Margin = new Thickness(0, 0, 0, 6);
            }
            if (_projectSearchField.Parent is Panel old) old.Children.Remove(_projectSearchField);
            stack.Children.Add(_projectSearchField);

            string query = _projectSearch.Text.Trim();
            var candidates = _store.Projects
                .Where(project => !_selectedProjectIds.Contains(project.Id))
                .Where(project => query.Length == 0 || _strings.ProjectName(project.Name).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .ToList();
            if (_store.Projects.Count == 0) stack.Children.Add(StatsUi.Text(_strings.NoExistingProjects, 12, secondary: true));
            else if (candidates.Count == 0) stack.Children.Add(StatsUi.Text(_strings.NoProjectsFound, 12, secondary: true));
            else stack.Children.Add(ChipRow(candidates.Take(MaxVisibleProjectChips).ToList(), false));

            string creatable = CreatableProjectName;
            if (creatable != null)
            {
                var create = SmallButton(ToDoStrings.Format(_strings.CreateProjectNamed, ToDoStrings.ProjectToken, creatable), async (s, e) => await CreateProjectFromSearchAsync(), glyph: Glyph(0xE710));
                create.HorizontalAlignment = HorizontalAlignment.Left;
                create.Margin = new Thickness(0, 6, 0, 0);
                AutomationProperties.SetAutomationId(create, "btnToDoEditorCreateProject");
                stack.Children.Add(create);
            }
            _projectsArea.Content = stack;
        }

        private Grid _projectSearchField;
        private bool _creatingProject;

        private FrameworkElement ChipRow(IList<Project> projects, bool selected)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var project in projects)
            {
                var captured = project.Id;
                var chip = Chip(_strings.ProjectName(project.Name), Accent, selected, selected ? "✓" : "+");
                chip.Margin = new Thickness(0, 0, 6, 6);
                var button = new Button { Content = chip, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, Focusable = false };
                button.Click += (s, e) => ToggleProject(captured);
                AutomationProperties.SetAutomationId(button, (selected ? "chipToDoEditorSelected_" : "chipToDoEditorProject_") + captured);
                wrap.Children.Add(button);
            }
            return wrap;
        }

        /// <summary>The typed search names a project nobody has yet, so an inline "Create" row is offered.</summary>
        private string CreatableProjectName
        {
            get
            {
                string typed = _projectSearch.Text.Trim();
                if (typed.Length == 0 || _creatingProject) return null;
                bool exists = _store.Projects.Any(project =>
                    string.Equals(_strings.ProjectName(project.Name), typed, StringComparison.CurrentCultureIgnoreCase)
                    || string.Equals(project.Name, typed, StringComparison.CurrentCultureIgnoreCase));
                return exists ? null : typed;
            }
        }

        private void ToggleProject(string id)
        {
            if (!_selectedProjectIds.Remove(id)) _selectedProjectIds.Add(id);
            DropStaleCustomStatus();
            RefreshProjects();
            RefreshStatus();
        }

        /// <summary>Web: the status-owner project follows the selection, and a column it doesn't have is dropped.</summary>
        private void DropStaleCustomStatus()
        {
            var owner = OwnerProject;
            // An owner the catalogue does not know has no columns loaded: that is no reason to move the card.
            if (owner == null) { if (ResolvedOwnerProjectId == null) _draft.CustomStatusId = null; return; }
            if (!owner.CustomStatuses.Any(status => status.Id == _draft.CustomStatusId)) _draft.CustomStatusId = null;
        }

        private async System.Threading.Tasks.Task CreateProjectFromSearchAsync()
        {
            string name = CreatableProjectName;
            if (name == null) return;
            _creatingProject = true;
            RefreshProjects();
            var project = await _store.CreateProjectAsync(name);
            _creatingProject = false;
            if (project != null)
            {
                if (!_selectedProjectIds.Contains(project.Id)) _selectedProjectIds.Add(project.Id);
                _projectSearch.Text = "";
            }
            else _error.Text = _store.StatusMessage ?? _strings.CouldNotCreateProject;
            RefreshProjects();
            RefreshStatus();
        }

        private void RefreshSubtasks()
        {
            var stack = new StackPanel();
            var subtasks = _draft.SubtaskList.ToList();
            for (int i = 0; i < subtasks.Count; i++)
            {
                int index = i;
                var subtask = subtasks[i];
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var toggle = new CheckBox { IsChecked = subtask.IsCompleted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                // Checked/Unchecked, not Click: a screen reader or UI Automation toggles a CheckBox without
                // raising Click, so a Click handler silently dropped their tick.
                RoutedEventHandler changed = (s, e) =>
                {
                    ReplaceSubtask(index, new ToDoSubtask(_draft.SubtaskList[index].Name, toggle.IsChecked == true));
                    RefreshSubtasks();
                };
                toggle.Checked += changed;
                toggle.Unchecked += changed;
                AutomationProperties.SetName(toggle, subtask.Name);
                DockPanel.SetDock(toggle, Dock.Left);
                row.Children.Add(toggle);
                var remove = new Button { Content = Glyph(0xE74D), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
                // Named and reachable with Tab: it was a mouse-only glyph read as "".
                AutomationProperties.SetName(remove, _strings.Delete + " " + subtask.Name);
                remove.ToolTip = _strings.Delete;
                remove.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                remove.Click += (s, e) =>
                {
                    var list = _draft.SubtaskList.ToList();
                    list.RemoveAt(index);
                    _draft.Subtasks = list;
                    RefreshSubtasks();
                };
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);
                var name = new TextBox { Text = subtask.Name };
                name.TextChanged += (s, e) => ReplaceSubtask(index, new ToDoSubtask(name.Text, _draft.SubtaskList[index].IsCompleted));
                var field = Field(name, _strings.AddASubtask, 30);
                if (subtask.IsCompleted) name.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                row.Children.Add(field);
                stack.Children.Add(row);
            }
            _subtasksArea.Content = stack;
        }

        private void ReplaceSubtask(int index, ToDoSubtask subtask)
        {
            var list = _draft.SubtaskList.ToList();
            if (index < 0 || index >= list.Count) return;
            list[index] = subtask;
            _draft.Subtasks = list;
        }

        private void AddSubtask()
        {
            string name = _pendingSubtask.Text.Trim();
            if (name.Length == 0) return;
            _draft.Subtasks = _draft.SubtaskList.Concat(new[] { new ToDoSubtask(name, false) }).ToList();
            _pendingSubtask.Text = "";
            RefreshSubtasks();
        }

        private async System.Threading.Tasks.Task GenerateSubtasksAsync()
        {
            string title = _title.Text.Trim();
            if (title.Length == 0) return;
            _generateButton.IsEnabled = false;
            var generated = await new API.FocusBearAPIClient().GenerateSubtasks(title, AppSettings.GetInstance.sLangCode);
            _generateButton.IsEnabled = true;
            if (generated == null)
            {
                _error.Text = _strings.CouldNotGenerateSubtasks;
                return;
            }
            _draft.Subtasks = _draft.SubtaskList.Concat(generated.Select(name => new ToDoSubtask(name, false))).ToList();
            _error.Text = "";
            RefreshSubtasks();
        }

        // ------------------------------------------------------------------ save / delete

        /// <summary>
        /// Web <c>TaskForm.handleSubmit</c>: tags from the selected projects, <c>project_id</c> the status
        /// owner, the pending subtask appended, the duration explicit or the perspiration's.
        /// </summary>
        internal ToDoTask BuildTaskToSave()
        {
            var toSave = _draft.Copy();
            toSave.Title = _title.Text.Trim();
            var day = ParsedDue;
            toSave.DueDate = day.HasValue ? ToDoDates.Ymd(day.Value) : null;
            toSave.Details = DetailsToSave(_details.Text, _openingDetailsText, _originalDetailsHtml);
            var selected = SelectedProjects;
            toSave.Tags = selected.Select(project => new ToDoTag(project.Id, project.Name)).ToList();
            toSave.ProjectId = ResolvedOwnerProjectId;
            toSave.CustomStatusId = ToDoEditorProjects.CustomStatusToSave(_draft.CustomStatusId, toSave.ProjectId, OwnerProject);
            var subtasks = _draft.SubtaskList.Where(subtask => !string.IsNullOrWhiteSpace(subtask.Name))
                                             .Select(subtask => new ToDoSubtask(subtask.Name.Trim(), subtask.IsCompleted)).ToList();
            string pending = _pendingSubtask.Text.Trim();
            if (pending.Length > 0) subtasks.Add(new ToDoSubtask(pending, false));
            toSave.Subtasks = subtasks;
            int.TryParse(_duration.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int explicitMinutes);
            toSave.Duration = ToDoScoring.DurationSeconds(Math.Max(0, explicitMinutes) * 60, _draft.EffectivePerspiration);
            return toSave;
        }

        /// <summary>
        /// The editor shows details as plain text. Unchanged, the web's rich text goes back exactly as it came;
        /// only an edit replaces it with what was typed.
        /// </summary>
        internal static string DetailsToSave(string typed, string openingText, string originalHtml)
        {
            string details = (typed ?? "").Trim();
            return details == (openingText ?? "").Trim() && originalHtml != null ? originalHtml : details;
        }

        /// <summary>The editor saved or deleted the task.</summary>
        internal bool Committed => _closeConfirmed;

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (_saving || string.IsNullOrWhiteSpace(_title.Text) || DueIsInvalid) return;
            var toSave = BuildTaskToSave();
            _saving = true;
            _error.Text = "";
            RefreshHeader();
            bool saved = await _store.SaveTaskAsync(toSave, _isNew, clearOutcome: _draft.Outcome == null && _original.Outcome != null);
            _saving = false;
            if (saved)
            {
                _closeConfirmed = true;
                Close();
                return;
            }
            _error.Text = _store.StatusMessage ?? _strings.RequestFailed;
            RefreshHeader();
        }

        private async System.Threading.Tasks.Task DeleteAsync()
        {
            var confirm = new FBMessageBox(_strings.DeleteTaskConfirmation, _strings.DeleteTask, MessageBoxButton.YesNo, _strings.Delete, _strings.Cancel) { Owner = this };
            confirm.ShowDialog();
            if (confirm.btnClicked != MessageBoxResult.Yes) return;
            if (await _store.DeleteAsync(_original))
            {
                _closeConfirmed = true;
                Close();
            }
            else _error.Text = _strings.CouldNotDeleteTask;
        }
    }

    internal static class ToDoTaskEditing
    {
        /// <summary>Take the status fields of a toggled copy onto the draft the editor holds.</summary>
        internal static void CopyStatusFrom(this ToDoTask draft, ToDoTask toggled)
        {
            draft.Status = toggled.Status;
            draft.CustomStatusId = toggled.CustomStatusId;
        }
    }
}
