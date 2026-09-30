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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The Projects tab — the Mac's <c>ProjectsTabView</c> / <c>ProjectDetailView</c>: "What matters" (the
    /// portfolio's three groups with the overloaded warning), "All projects" (paged, searched, by status), and
    /// a project's page with its tasks as a list or a board whose cards drag between columns.
    /// </summary>
    internal sealed class ProjectsView : UserControl
    {
        private const double CardWidth = 320;
        private const double ColumnWidth = 280;
        private const string DragFormat = "FocusBearToDoTaskId";

        private readonly ProjectsStore _store;
        private readonly ToDoStore _todo;
        private readonly Action<string> _showInTaskList;
        private readonly Action<ToDoTask, Project> _openEditor;
        private readonly TextBox _search = new TextBox();
        private readonly Grid _searchField;
        private readonly ContentControl _body = new ContentControl();
        private readonly TextBlock _status;
        private bool _redrawQueued;

        internal ProjectsView(ProjectsStore store, ToDoStore todo, Action<string> showInTaskList, Action<ToDoTask, Project> openEditor)
        {
            _store = store;
            _todo = todo;
            _showInTaskList = showInTaskList;
            _openEditor = openEditor;
            AutomationProperties.SetAutomationId(_search, "txtProjectsSearch");
            _search.TextChanged += (s, e) => _store.SetSearchQuery(_search.Text);
            _searchField = Field(_search, store.Strings.ProjectSearchPlaceholder);
            _searchField.Width = 260;
            _status = StatsUi.Text("", 12, secondary: true);
            _status.Margin = new Thickness(0, 8, 0, 0);
            AutomationProperties.SetAutomationId(_status, "txtProjectsStatus");
            var root = new DockPanel();
            var footer = new Border { Child = _status, Padding = new Thickness(24, 0, 24, 12) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            root.Children.Add(_body);
            Content = root;
            _store.Changed += QueueRedraw;
            _store.StatusChanged += DrawStatus;
            _store.NextTaskChanged += DrawNextTask;
            // A redraw replaces every button on the page. One that lands between a press and its release
            // swaps the chip under the mouse for a new one, which never sees the release, so the click is
            // lost ("nothing happened when I clicked it"). Held redraws run once the click is done.
            PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_redrawHeld) Dispatcher.BeginInvoke(new Action(() => { if (_redrawHeld) { _redrawHeld = false; Redraw(); } }), System.Windows.Threading.DispatcherPriority.Background);
            };
            Redraw();
        }

        private bool _redrawHeld;

        /// <summary>The NEXT lines on screen, by project, so a task arriving redraws its own line only.</summary>
        private readonly Dictionary<string, List<TextBlock>> _nextLines = new Dictionary<string, List<TextBlock>>();

        private void DrawStatus()
        {
            var detail = _store.Detail;
            _status.Text = (detail != null ? detail.StatusMessage : null) ?? _store.StatusMessage ?? "";
        }

        private void DrawNextTask(string projectId)
        {
            if (_nextLines.TryGetValue(projectId, out var lines))
                foreach (var line in lines) FillNextTaskLine(line, projectId);
        }

        private ToDoStrings S => _store.Strings;

        private void QueueRedraw()
        {
            if (_redrawQueued) return;
            _redrawQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _redrawQueued = false;
                if (Mouse.LeftButton == MouseButtonState.Pressed && IsMouseOver) _redrawHeld = true;
                else Redraw();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void Redraw()
        {
            _redrawHeld = false;
            _nextLines.Clear();
            DrawStatus();
            var detail = _store.Detail;
            if (_searchField.Parent is Panel parent) parent.Children.Remove(_searchField);
            _body.Content = detail != null ? DetailPage(detail) : ListPage();
        }

        // ------------------------------------------------------------------ list page

        private FrameworkElement ListPage()
        {
            var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(24, 0, 24, 12) };
            var modes = new[] { ProjectsViewMode.Focus, ProjectsViewMode.All };
            var segmented = StatsUi.Segmented(modes, _store.ViewMode, mode => mode == ProjectsViewMode.Focus ? S.ViewWhatMatters : S.ViewAllProjects, mode => _store.SetViewMode(mode));
            AutomationProperties.SetAutomationId(segmented, "segProjectsView");
            segmented.VerticalAlignment = VerticalAlignment.Center;
            segmented.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(segmented, Dock.Left);
            toolbar.Children.Add(segmented);
            DockPanel.SetDock(_searchField, Dock.Left);
            _searchField.VerticalAlignment = VerticalAlignment.Center;
            toolbar.Children.Add(_searchField);
            if (_store.ViewMode == ProjectsViewMode.All)
            {
                var status = MenuButton(S.ProjectStatusLabel(_store.StatusFilter), _store.StatusFilter != ProjectLifecycleStatus.Active, menu =>
                {
                    foreach (ProjectLifecycleStatus option in Enum.GetValues(typeof(ProjectLifecycleStatus)))
                    {
                        var captured = option;
                        menu.Add(S.ProjectStatusLabel(option), option == _store.StatusFilter, () => _store.SetStatusFilter(captured), "menuProjectsStatus" + option);
                    }
                }, "btnProjectsStatus");
                status.Margin = new Thickness(8, 6, 0, 0);
                DockPanel.SetDock(status, Dock.Left);
                toolbar.Children.Add(status);
            }
            var refresh = IconButton(Glyph(0xE72C), S.Refresh, false, async (s, e) => await _store.RefreshAsync(), "btnProjectsRefresh");
            DockPanel.SetDock(refresh, Dock.Right);
            toolbar.Children.Add(refresh);
            var create = StatsUi.Button("+  " + S.NewProject, (s, e) => ShowCreateProject(), primary: true);
            create.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(create, "btnProjectsNew");
            DockPanel.SetDock(create, Dock.Right);
            toolbar.Children.Add(create);

            var content = new StackPanel();
            if (_store.ViewMode == ProjectsViewMode.Focus) FillFocus(content);
            else FillAll(content);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new Border { Padding = new Thickness(24, 0, 24, 16), Child = content } };
            if (_store.ViewMode == ProjectsViewMode.All)
            {
                scroll.ScrollChanged += (s, e) =>
                {
                    if (e.VerticalChange > 0 && scroll.ScrollableHeight - scroll.VerticalOffset < 160) _ = _store.LoadMoreAsync();
                };
            }
            var page = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            page.Children.Add(toolbar);
            page.Children.Add(scroll);
            return page;
        }

        private void FillFocus(StackPanel content)
        {
            if (_store.Portfolio == null)
            {
                content.Children.Add(_store.IsPortfolioLoading ? StatsUi.Loading() : StatsUi.Empty("⚠", S.CouldNotLoadProjects));
                return;
            }
            var overloaded = _store.OvercommittedProjects;
            if (_store.Portfolio.IsOvercommitted) content.Children.Add(OvercommittedBanner(overloaded));
            var groups = _store.ActionGroups;
            if (groups.IsEmpty)
            {
                content.Children.Add(StatsUi.Empty("✓", S.FocusEmptyTitle, S.FocusEmptyDescription));
                return;
            }
            AddSection(content, PortfolioActionSection.AskForExtension, groups.AskForExtension, Overdue);
            AddSection(content, PortfolioActionSection.StartNow, groups.StartNow, TodayTint);
            AddSection(content, PortfolioActionSection.BackBurner, groups.BackBurner, Neutral);
        }

        private FrameworkElement OvercommittedBanner(IList<PortfolioProject> projects)
        {
            string names = PortfolioGrouping.OvercommittedNames(projects.Select(p => new PortfolioProject { Id = p.Id, Name = S.ProjectName(p.Name) }),
                                                               count => ToDoStrings.Format(S.OvercommittedAndMore, ToDoStrings.CountToken, count.ToString(CultureInfo.InvariantCulture)));
            var stack = new StackPanel();
            stack.Children.Add(StatsUi.Text("⚠  " + S.OvercommittedTitle, 13, FontWeights.SemiBold));
            var body = StatsUi.Text(names.Length == 0 ? S.OvercommittedDescriptionFallback : S.OvercommittedDescription.Replace("<PROJECTS>", names), 12, secondary: true);
            body.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(body);
            var banner = new Border { Child = stack, Background = StatsUi.Tint(Overdue, 0.1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 14) };
            AutomationProperties.SetAutomationId(banner, "bannerProjectsOvercommitted");
            return banner;
        }

        private void AddSection(StackPanel content, PortfolioActionSection section, IList<PortfolioProject> projects, Brush accent)
        {
            if (projects.Count == 0) return;
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 10) };
            head.Children.Add(new Border { Width = 5, Height = 20, CornerRadius = new CornerRadius(2), Background = accent, Margin = new Thickness(0, 0, 8, 0) });
            var title = StatsUi.Text(S.SectionTitle(section), 15, FontWeights.SemiBold, wrap: false);
            title.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(title);
            var count = StatsUi.Text(projects.Count.ToString(CultureInfo.InvariantCulture), 13, secondary: true, wrap: false);
            count.Margin = new Thickness(8, 0, 0, 0);
            count.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(count);
            content.Children.Add(head);
            var wrap = new WrapPanel();
            foreach (var project in projects) wrap.Children.Add(RiskCard(project, section));
            content.Children.Add(wrap);
        }

        private FrameworkElement RiskCard(PortfolioProject risk, PortfolioActionSection section)
        {
            var stack = new StackPanel();
            var head = new DockPanel { LastChildFill = true };
            var bucket = Chip(S.BucketLabel(risk.Bucket), BucketTint(risk.Bucket));
            bucket.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(bucket, Dock.Right);
            head.Children.Add(bucket);
            head.Children.Add(CardTitle(_store.Project(risk.Id)?.Icon, risk.Name));
            stack.Children.Add(head);
            stack.Children.Add(NextTaskLine(risk.Id));
            var footer = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            footer.Children.Add(DueChip(risk.Id, risk.DueDate));
            if (risk.Hours > 0)
            {
                footer.Children.Add(Chip(ToDoStrings.Format(S.RequiredHours, "<DURATION>", HoursLabel(risk.Hours)), glyph: "⏱"));
                var pace = PortfolioGrouping.RecommendedDailyHours(risk);
                if (pace.HasValue) footer.Children.Add(Chip(ToDoStrings.Format(S.RecommendedDaily, "<DURATION>", HoursLabel(pace.Value))));
            }
            foreach (var badge in PortfolioGrouping.Badges(risk.BadgeList, section))
            {
                string label = S.BadgeLabel(badge);
                if (label != null) footer.Children.Add(Chip(label, badge == PortfolioBadge.Sacrifice ? Neutral : Overdue));
            }
            stack.Children.Add(footer);
            var card = Card(stack, BucketTint(risk.Bucket));
            AutomationProperties.SetAutomationId(card, "cardRisk_" + risk.Id);
            AutomationProperties.SetName(card, risk.Name);
            card.MouseLeftButtonUp += (s, e) => { if (!e.Handled) _store.Open(risk); };
            card.Select = card.Open = () => _store.Open(risk);
            return card;
        }

        private void FillAll(StackPanel content)
        {
            if (_store.IsProjectsLoading && _store.Projects.Count == 0)
            {
                content.Children.Add(StatsUi.Loading());
                return;
            }
            switch (_store.EmptyVariant)
            {
                case ProjectsEmptyVariant.ActiveZero: content.Children.Add(StatsUi.Empty("\U0001F4C1", S.NoProjectsYet, S.CreateFirstProject)); return;
                case ProjectsEmptyVariant.Search: content.Children.Add(StatsUi.Empty("\U0001F50D", S.NoMatchingProjects, S.TryDifferentSearch)); return;
                case ProjectsEmptyVariant.Completed: content.Children.Add(StatsUi.Empty("✓", S.NoCompletedProjects)); return;
                case ProjectsEmptyVariant.Archived: content.Children.Add(StatsUi.Empty("\U0001F5C4", S.NoArchivedProjects)); return;
                case ProjectsEmptyVariant.Failed: content.Children.Add(StatsUi.Empty("⚠", S.CouldNotLoadProjects)); return;
            }
            var wrap = new WrapPanel();
            foreach (var project in _store.SortedProjects) wrap.Children.Add(ProjectCard(project));
            content.Children.Add(wrap);
            if (_store.IsLoadingMore) content.Children.Add(StatsUi.Loading());
            else if (_store.HasNextPage)
            {
                var more = StatsUi.Button(S.LoadMore, async (s, e) => await _store.LoadMoreAsync(), height: 28);
                more.HorizontalAlignment = HorizontalAlignment.Center;
                AutomationProperties.SetAutomationId(more, "btnProjectsLoadMore");
                content.Children.Add(more);
            }
        }

        private FrameworkElement ProjectCard(Project project)
        {
            var stack = new StackPanel();
            var head = new DockPanel { LastChildFill = true };
            var dots = new TextBlock { Text = Glyph(0xE712), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            dots.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var actions = new Button { Content = new Border { Child = dots, Width = 26, Height = 22, Background = Brushes.Transparent }, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = S.MoreOptions, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetAutomationId(actions, "btnProjectMenu_" + project.Id);
            // Was mouse-only (Focusable = false) and read as "": now "More options for <project>".
            AutomationProperties.SetName(actions, S.MoreOptions + ": " + project.Name);
            actions.Click += (s, e) => { e.Handled = true; ProjectMenu(actions, project).Open(); };
            DockPanel.SetDock(actions, Dock.Right);
            head.Children.Add(actions);
            if (project.LifecycleStatus != ProjectLifecycleStatus.Active)
            {
                var lifecycle = Chip(S.ProjectStatusLabel(project.LifecycleStatus));
                DockPanel.SetDock(lifecycle, Dock.Right);
                head.Children.Add(lifecycle);
            }
            if (_store.IsMemberOnly(project))
            {
                var member = Chip(S.MemberBadge, Info);
                DockPanel.SetDock(member, Dock.Right);
                head.Children.Add(member);
            }
            head.Children.Add(CardTitle(project.Icon, project.Name));
            stack.Children.Add(head);
            if (!string.IsNullOrWhiteSpace(project.Description))
            {
                var description = StatsUi.Text(project.Description, 12, secondary: true);
                description.MaxHeight = 34;
                description.TextTrimming = TextTrimming.CharacterEllipsis;
                description.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(description);
            }
            stack.Children.Add(NextTaskLine(project.Id));
            var footer = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            footer.Children.Add(DueChip(project.Id, project.DueDate));
            int members = project.MemberList.Count;
            if (members > 1) footer.Children.Add(Chip(ToDoStrings.Format(S.MembersCount, ToDoStrings.CountToken, members.ToString(CultureInfo.InvariantCulture)), glyph: "\U0001F465"));
            int? open = project.UncompletedTaskCount;
            footer.Children.Add(Chip(open.HasValue
                ? ToDoStrings.Format(S.TasksToDoCount, ToDoStrings.CountToken, open.Value.ToString(CultureInfo.InvariantCulture))
                : ToDoStrings.Format(S.TasksCount, ToDoStrings.CountToken, (project.TaskCount ?? 0).ToString(CultureInfo.InvariantCulture)), glyph: "☑"));
            if (_store.RisksById.TryGetValue(project.Id, out var risk) && risk.Bucket != PortfolioBucket.NoDeadline)
            {
                footer.Children.Add(Chip(S.BucketLabel(risk.Bucket), BucketTint(risk.Bucket)));
            }
            stack.Children.Add(footer);
            var card = Card(stack, null);
            AutomationProperties.SetAutomationId(card, "cardProject_" + project.Id);
            AutomationProperties.SetName(card, project.Name);
            card.MouseLeftButtonUp += (s, e) => { if (!e.Handled) _store.Open(project); };
            card.Select = card.Open = () => _store.Open(project);
            return card;
        }

        private ToDoMenu ProjectMenu(UIElement anchor, Project project)
        {
            var menu = new ToDoMenu(anchor);
            menu.Add(S.Edit, false, () => _store.Open(project), "menuProjectOpen");
            menu.Add(S.ShowInTaskList, false, () => _showInTaskList(project.Id), "menuProjectShowTasks");
            if (_store.CanManage(project))
            {
                menu.AddSeparator();
                if (project.LifecycleStatus != ProjectLifecycleStatus.Completed)
                    menu.Add(S.MarkCompleteProject, false, () => _ = _store.SetLifecycleAsync(project, ProjectLifecycleStatus.Completed), "menuProjectComplete");
                if (project.LifecycleStatus != ProjectLifecycleStatus.Archived)
                    menu.Add(S.ArchiveProject, false, () => _ = _store.SetLifecycleAsync(project, ProjectLifecycleStatus.Archived), "menuProjectArchive");
                if (project.LifecycleStatus != ProjectLifecycleStatus.Active)
                    menu.Add(S.RestoreProject, false, () => _ = _store.SetLifecycleAsync(project, ProjectLifecycleStatus.Active), "menuProjectRestore");
            }
            return menu;
        }

        // ------------------------------------------------------------------ shared card parts

        /// <summary>A project card: focusable and announced as a list item (ToDoRowCard), Space / Enter open it.</summary>
        private static ToDoRowCard Card(UIElement content, Brush accent)
        {
            var card = new ToDoRowCard
            {
                Child = content,
                Width = CardWidth,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 12, 12),
                Cursor = Cursors.Hand,
            };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            if (accent != null) card.BorderBrush = StatsUi.Tint(accent, 0.55);
            else card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return card;
        }

        private FrameworkElement CardTitle(string icon, string name)
        {
            var title = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            string emoji = ToDoScreen.EmojiIcon(icon);
            if (emoji != null) title.Inlines.Add(new System.Windows.Documents.Run(emoji + "  ") { FontFamily = EmojiFont });
            title.Inlines.Add(new System.Windows.Documents.Run(S.ProjectName(name)));
            title.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            return title;
        }

        private FrameworkElement NextTaskLine(string projectId)
        {
            var line = new TextBlock { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            if (!_nextLines.TryGetValue(projectId, out var lines)) _nextLines[projectId] = lines = new List<TextBlock>();
            lines.Add(line);
            FillNextTaskLine(line, projectId);
            return line;
        }

        private void FillNextTaskLine(TextBlock line, string projectId)
        {
            line.Inlines.Clear();
            line.Inlines.Add(new System.Windows.Documents.Run(S.NextTask.ToUpper(ToDoStrings.Culture) + "  ") { FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = Accent });
            var next = _store.NextTask(projectId);
            string text = next != null ? next.Title : _store.IsNextTaskPending(projectId) ? "…" : S.NextTaskEmpty;
            var run = new System.Windows.Documents.Run(text);
            line.Inlines.Add(run);
            if (next != null && next.TopScore > 0) line.Inlines.Add(new System.Windows.Documents.Run("   TOP " + ToDoScoring.Rounded(next.TopScore)) { Foreground = Accent, FontSize = 11 });
            line.SetResourceReference(TextBlock.ForegroundProperty, next != null ? "ForegroundColor" : "TraySubtitleColor");
        }

        private const string CalendarGlyph = "\U0001F4C5";
        private const string FlagGlyph = "\U0001F6A9";

        /// <summary>
        /// "📅 Due: 23 Sep" (red overdue, orange today) or "Set due date" — a button, as on the Mac: it opens the
        /// due-date popover and saves straight to the project. On a project's page only its owner gets the button.
        /// </summary>
        private FrameworkElement DueChip(string projectId, string dueDate, bool editable = true)
        {
            Border chip;
            if (string.IsNullOrEmpty(dueDate)) chip = Chip(S.SetDueDate, glyph: CalendarGlyph);
            else
            {
                var state = ToDoDates.DueState(dueDate, false, DateTime.Now);
                Brush tint = state == ToDoDueState.Overdue ? Overdue : state == ToDoDueState.Today ? TodayTint : Neutral;
                chip = Chip(ToDoStrings.Format(S.DueWithDate, ToDoStrings.DateToken, S.DueLabel(dueDate, DateTime.Now)), tint, glyph: CalendarGlyph);
            }
            if (!editable) return chip;
            return ChipButton(chip, "btnProjectDue_" + projectId, S.EditDueDate, _store.IsUpdatingDueDate(projectId), button =>
                _duePopup = ToDoDueDatePopup.Show(button, S, _store.DueDateOf(projectId) ?? dueDate, day => _ = _store.SetDueDateAsync(projectId, day), "ProjectDue", withCalendar: true));
        }

        /// <summary>
        /// Mac <c>ProjectDetailView.outcomeChip</c>: "🚩 4 / 5 · Can postpone a few days", or "Set outcome". The owner
        /// clicks it for the one outcome picker — Not set, 1–5, and a legacy odd value kept as its own row.
        /// </summary>
        private FrameworkElement OutcomeChip(Project project, bool editable)
        {
            var chip = Chip(S.OutcomeChipLabel(project.Outcome), Neutral, glyph: FlagGlyph);
            chip.MaxWidth = 300;
            if (!editable) return chip;
            string projectId = project.Id;
            return ChipButton(chip, "btnProjectOutcome_" + projectId, S.EditOutcome, _store.IsUpdatingOutcome(projectId), button =>
            {
                var menu = new ToDoMenu(button);
                foreach (var option in ProjectOutcomeEdit.Options(project.Outcome))
                {
                    int? wire = option.Wire;
                    string id = "menuProjectOutcome" + (option.IsLegacy ? "Legacy" : option.Step.HasValue ? option.Step.Value.ToString(CultureInfo.InvariantCulture) : "None");
                    menu.Add(S.OutcomePickerRow(option), project.Outcome == wire, () => _ = _store.SetOutcomeAsync(projectId, wire), id);
                }
                menu.Open();
            });
        }

        /// <summary>A chip that is a button: its click stays off the card beneath, and keyboard focus rings it.</summary>
        private static Button ChipButton(Border chip, string automationId, string tooltip, bool saving, Action<Button> onClick)
        {
            var button = new Button
            {
                Content = chip, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = tooltip,
                Background = Brushes.Transparent, Margin = chip.Margin, Opacity = saving ? 0.6 : 1, IsEnabled = !saving,
                FocusVisualStyle = null,
            };
            chip.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(button, automationId);
            AutomationProperties.SetName(button, tooltip);
            // Inside a card: the chip's click must not also open the project.
            button.Click += (s, e) => { e.Handled = true; onClick(button); };
            // The plain template draws no focus rectangle of its own.
            chip.BorderThickness = new Thickness(1);
            chip.BorderBrush = Brushes.Transparent;
            button.GotKeyboardFocus += (s, e) => { if (InputManager.Current.MostRecentInputDevice is KeyboardDevice) chip.BorderBrush = Accent; };
            button.LostKeyboardFocus += (s, e) => chip.BorderBrush = Brushes.Transparent;
            return button;
        }

        /// <summary>The open due-date popup, for tests.</summary>
        internal Popup _duePopup;

        private static string HoursLabel(double hours)
        {
            var parts = PortfolioGrouping.DurationParts(hours);
            return ToDoDates.DurationLabel(parts.Hours, parts.Minutes);
        }

        private static Brush BucketTint(PortfolioBucket bucket)
        {
            switch (bucket)
            {
                case PortfolioBucket.OnTrack: return Good;
                case PortfolioBucket.AtRisk: return TodayTint;
                case PortfolioBucket.Critical: return Overdue;
                default: return Neutral;
            }
        }

        private void ShowCreateProject()
        {
            var window = new Window
            {
                Title = S.NewProject, Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this), ShowInTaskbar = false,
            };
            window.SetResourceReference(BackgroundProperty, "BackgroundColor");
            DialogFit.Apply(window);
            AutomationProperties.SetAutomationId(window, "ProjectCreate");
            var stack = new StackPanel { Margin = new Thickness(20) };
            var heading = StatsUi.Text(S.NewProject, 16, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(heading);
            var name = new TextBox();
            AutomationProperties.SetAutomationId(name, "txtProjectCreateName");
            stack.Children.Add(Field(name, S.ProjectNamePlaceholder));
            // Mac ProjectCreateSheet: a new project is rated with the same outcome picker as its page's chip.
            int? outcome = null;
            var outcomeLabel = StatsUi.Text(S.Outcome, 12, FontWeights.SemiBold);
            outcomeLabel.Margin = new Thickness(0, 12, 0, 4);
            stack.Children.Add(outcomeLabel);
            var outcomeHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left };
            Action drawOutcome = null;
            drawOutcome = () =>
            {
                var picker = MenuButton(S.OutcomePickerRow(new OutcomeOption(outcome, ProjectOutcomeEdit.ChipStep(outcome), false)), false, menu =>
                {
                    foreach (var option in ProjectOutcomeEdit.Options(outcome))
                    {
                        int? wire = option.Wire;
                        menu.Add(S.OutcomePickerRow(option), outcome == wire, () => { outcome = wire; drawOutcome(); },
                                 "menuProjectCreateOutcome" + (option.Step.HasValue ? option.Step.Value.ToString(CultureInfo.InvariantCulture) : "None"));
                    }
                }, "btnProjectCreateOutcome");
                picker.Margin = new Thickness(0);
                outcomeHost.Content = picker;
            };
            drawOutcome();
            stack.Children.Add(outcomeHost);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            buttons.Children.Add(StatsUi.Button(S.Cancel, (s, e) => window.Close()));
            Button save = null;
            save = StatsUi.Button(S.Save, async (s, e) =>
            {
                save.IsEnabled = false;
                var project = await _store.CreateProjectAsync(name.Text, outcome);
                if (project != null)
                {
                    window.Close();
                    _store.Open(project);
                }
                else save.IsEnabled = true;
            }, primary: true);
            save.Margin = new Thickness(0);
            save.IsEnabled = false;
            AutomationProperties.SetAutomationId(save, "btnProjectCreateSave");
            name.TextChanged += (s, e) => save.IsEnabled = !string.IsNullOrWhiteSpace(name.Text) && !_store.IsCreating;
            name.KeyDown += (s, e) => { if (e.Key == Key.Enter && save.IsEnabled) save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); };
            buttons.Children.Add(save);
            stack.Children.Add(buttons);
            window.Content = stack;
            window.Loaded += (s, e) => name.Focus();
            window.ShowDialog();
        }

        // ------------------------------------------------------------------ project page

        private FrameworkElement DetailPage(ProjectDetailStore detail)
        {
            var project = detail.Project;
            var nav = new DockPanel { LastChildFill = false, Margin = new Thickness(24, 0, 24, 10) };
            var back = new Button { Content = "‹  " + S.BackToProjects, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, Foreground = Accent, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(back, S.BackToProjects);
            back.Click += (s, e) => _store.CloseDetail();
            AutomationProperties.SetAutomationId(back, "btnProjectBack");
            DockPanel.SetDock(back, Dock.Left);
            nav.Children.Add(back);
            if (detail.CanManage || detail.CanDelete)
            {
                var more = IconButton(Glyph(0xE712), S.MoreOptions, false, null, "btnProjectDetailMenu");
                more.Click += (s, e) => DetailMenu(more, detail).Open();
                DockPanel.SetDock(more, Dock.Right);
                nav.Children.Add(more);
            }
            var showTasks = StatsUi.Button(S.ShowInTaskList, (s, e) => _showInTaskList(project.Id), height: 30);
            showTasks.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(showTasks, "btnProjectShowTasks");
            DockPanel.SetDock(showTasks, Dock.Right);
            nav.Children.Add(showTasks);

            var header = new StackPanel { Margin = new Thickness(24, 0, 24, 12) };
            if (detail.NotFound)
            {
                header.Children.Add(StatsUi.Empty("?", S.ProjectNotFound));
                var missing = new DockPanel();
                DockPanel.SetDock(nav, Dock.Top);
                missing.Children.Add(nav);
                missing.Children.Add(header);
                return missing;
            }
            if (project.LifecycleStatus != ProjectLifecycleStatus.Active)
            {
                var banner = StatsUi.Text(project.LifecycleStatus == ProjectLifecycleStatus.Completed ? S.ProjectCompletedBanner : S.ProjectArchivedBanner, 12);
                header.Children.Add(new Border { Child = banner, Background = StatsUi.Tint(Info, 0.12), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 10) });
            }
            var titleRow = new DockPanel { LastChildFill = true };
            var modes = StatsUi.Segmented(new[] { ProjectTasksMode.List, ProjectTasksMode.Board }, detail.TasksMode, mode => mode == ProjectTasksMode.List ? S.ListView : S.BoardView, mode => detail.SetTasksMode(mode));
            AutomationProperties.SetAutomationId(modes, "segProjectTasksMode");
            DockPanel.SetDock(modes, Dock.Right);
            modes.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(modes);
            var name = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            string emoji = ToDoScreen.EmojiIcon(project.Icon);
            if (emoji != null) name.Inlines.Add(new System.Windows.Documents.Run(emoji + "  ") { FontFamily = EmojiFont });
            name.Inlines.Add(new System.Windows.Documents.Run(S.ProjectName(project.Name)));
            name.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(name, "txtProjectName");
            AutomationProperties.SetName(name, project.Name);
            titleRow.Children.Add(name);
            header.Children.Add(titleRow);
            if (!string.IsNullOrWhiteSpace(project.Description))
            {
                var description = StatsUi.Text(project.Description, 12, secondary: true);
                description.Margin = new Thickness(0, 4, 0, 0);
                header.Children.Add(description);
            }
            var meta = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            meta.Children.Add(DueChip(project.Id, project.DueDate, detail.CanEditHeader));
            meta.Children.Add(OutcomeChip(project, detail.CanEditHeader));
            foreach (var tag in project.Tags ?? new List<string>()) meta.Children.Add(Chip("#" + tag, Accent));
            if (project.MemberList.Count > 0)
            {
                var members = Chip(ToDoStrings.Format(S.MembersWithCount, ToDoStrings.CountToken, project.MemberList.Count.ToString(CultureInfo.InvariantCulture)), Info, glyph: "\U0001F465");
                members.ToolTip = string.Join("\n", project.MemberList.Select(MemberLine));
                meta.Children.Add(members);
            }
            header.Children.Add(meta);

            FrameworkElement tasks;
            if (detail.IsTasksLoading && detail.Tasks.Count == 0) tasks = StatsUi.Loading();
            else if (detail.Tasks.Count == 0) tasks = EmptyTasks(detail);
            else tasks = detail.TasksMode == ProjectTasksMode.Board ? Board(detail) : TaskList(detail);

            var page = new DockPanel();
            DockPanel.SetDock(nav, Dock.Top);
            DockPanel.SetDock(header, Dock.Top);
            page.Children.Add(nav);
            page.Children.Add(header);
            if (detail.TasksMode == ProjectTasksMode.List || detail.Tasks.Count == 0)
            {
                var addRow = new Border { Padding = new Thickness(24, 0, 24, 8) };
                var add = StatsUi.Button("+  " + S.AddTask, (s, e) => _openEditor(null, project), height: 30);
                add.HorizontalAlignment = HorizontalAlignment.Left;
                AutomationProperties.SetAutomationId(add, "btnProjectAddTask");
                addRow.Child = add;
                DockPanel.SetDock(addRow, Dock.Top);
                page.Children.Add(addRow);
            }
            page.Children.Add(tasks);
            return page;
        }

        private string MemberLine(ProjectMember member)
        {
            string who = member.UserId == _store.CurrentUserId && _store.CurrentUserId.Length > 0 ? S.You
                       : !string.IsNullOrEmpty(member.Email) ? member.Email
                       : member.MemberRole == ProjectMemberRole.Owner ? S.ProjectOwner : S.UnknownUser;
            string line = who + " · " + S.RoleLabel(member.MemberRole);
            if (member.InvitationStatus == ProjectInvitationStatus.Pending) line += " · " + S.InvitationPendingStatus;
            return line;
        }

        private ToDoMenu DetailMenu(UIElement anchor, ProjectDetailStore detail)
        {
            var project = detail.Project;
            var menu = new ToDoMenu(anchor);
            if (detail.CanManage)
            {
                if (project.LifecycleStatus != ProjectLifecycleStatus.Completed)
                    menu.Add(S.MarkCompleteProject, false, () => _ = LifecycleFromDetail(project, ProjectLifecycleStatus.Completed), "menuProjectDetailComplete");
                if (project.LifecycleStatus != ProjectLifecycleStatus.Archived)
                    menu.Add(S.ArchiveProject, false, () => _ = LifecycleFromDetail(project, ProjectLifecycleStatus.Archived), "menuProjectDetailArchive");
                if (project.LifecycleStatus != ProjectLifecycleStatus.Active)
                    menu.Add(S.RestoreProject, false, () => _ = LifecycleFromDetail(project, ProjectLifecycleStatus.Active), "menuProjectDetailRestore");
            }
            menu.Add(S.EditOnWeb, false, () => StatsUi.OpenExternal("https://dashboard.focusbear.io/projects/" + Uri.EscapeDataString(project.Id)), "menuProjectDetailWeb");
            if (detail.CanDelete)
            {
                menu.AddSeparator();
                menu.Add(S.DeleteProject, false, () => ConfirmDelete(project), "menuProjectDetailDelete");
            }
            return menu;
        }

        private async System.Threading.Tasks.Task LifecycleFromDetail(Project project, ProjectLifecycleStatus status)
        {
            if (await _store.SetLifecycleAsync(project, status) && _store.Detail != null) await _store.Detail.LoadAsync();
        }

        private void ConfirmDelete(Project project)
        {
            var confirm = new FBMessageBox(S.DeleteProjectConfirm, S.DeleteProject, MessageBoxButton.YesNo, S.Delete, S.Cancel) { Owner = Window.GetWindow(this) };
            confirm.ShowDialog();
            if (confirm.btnClicked == MessageBoxResult.Yes) _ = _store.DeleteProjectAsync(project);
        }

        private FrameworkElement EmptyTasks(ProjectDetailStore detail)
        {
            return StatsUi.Empty("☑", S.NoTasksYet, S.CreateFirstTask);
        }

        private FrameworkElement TaskList(ProjectDetailStore detail)
        {
            var list = new StackPanel();
            foreach (var task in detail.SortedTasks) list.Children.Add(TaskCard(detail, task, compact: false));
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new Border { Padding = new Thickness(24, 0, 24, 16), Child = list } };
        }

        /// <summary>
        /// Web <c>ProjectDetailsKanban</c>: one column per custom status (or To Do / In Progress / Done). A card
        /// drags to another column, or moves through its "Move to" menu; both write the same way.
        /// </summary>
        private FrameworkElement Board(ProjectDetailStore detail)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var column in detail.Columns)
            {
                var captured = column;
                var cards = new StackPanel { MinHeight = 80 };
                var tasks = detail.TasksIn(column);
                foreach (var task in tasks) cards.Children.Add(TaskCard(detail, task, compact: true));
                if (tasks.Count == 0)
                {
                    var none = StatsUi.Text(S.NoTasksInColumn, 12, secondary: true);
                    none.Margin = new Thickness(4, 6, 0, 6);
                    cards.Children.Add(none);
                }
                var head = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };
                var count = StatsUi.Text(tasks.Count.ToString(CultureInfo.InvariantCulture), 12, FontWeights.SemiBold, secondary: true, wrap: false);
                DockPanel.SetDock(count, Dock.Right);
                head.Children.Add(count);
                var label = StatsUi.Text((column.Completes ? "✓ " : "") + column.Label, 13, FontWeights.SemiBold, wrap: false);
                head.Children.Add(label);
                var stack = new StackPanel();
                stack.Children.Add(head);
                stack.Children.Add(cards);
                var lane = new Border
                {
                    Child = stack, Width = ColumnWidth, Padding = new Thickness(10), Margin = new Thickness(0, 0, 12, 0),
                    CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), AllowDrop = true, VerticalAlignment = VerticalAlignment.Top,
                };
                lane.SetResourceReference(Border.BackgroundProperty, "TrayCircleBtnBg");
                lane.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                AutomationProperties.SetAutomationId(lane, "laneProject_" + column.Id);
                lane.DragOver += (s, e) =>
                {
                    e.Effects = e.Data.GetDataPresent(DragFormat) && detail.CanMoveTask ? DragDropEffects.Move : DragDropEffects.None;
                    lane.BorderBrush = e.Effects == DragDropEffects.Move ? Accent : lane.BorderBrush;
                    e.Handled = true;
                };
                lane.DragLeave += (s, e) => lane.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                lane.Drop += async (s, e) =>
                {
                    lane.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                    string id = e.Data.GetData(DragFormat) as string;
                    var task = detail.Tasks.FirstOrDefault(t => t.Id == id);
                    if (task != null) await detail.MoveAsync(task, captured.Id);
                };
                row.Children.Add(lane);
            }
            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new Border { Padding = new Thickness(24, 0, 24, 16), Child = row },
            };
        }

        private FrameworkElement TaskCard(ProjectDetailStore detail, ToDoTask task, bool compact)
        {
            bool updating = detail.UpdatingTaskIds.Contains(task.Id);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            FrameworkElement check;
            if (updating) check = new ProgressBar { IsIndeterminate = true, Width = 18, Height = 3, Foreground = Accent, Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            else
            {
                var button = CompletionCheck(task.IsCompleted, task.IsCompleted ? S.MarkIncomplete : S.MarkCompleteShort, async (s, e) => await detail.ToggleCompleteAsync(task), 18);
                AutomationProperties.SetAutomationId(button, "btnProjectTaskCheck_" + task.Id);
                check = button;
            }
            check.Margin = new Thickness(0, 1, 8, 0);
            grid.Children.Add(check);
            var body = new StackPanel();
            Grid.SetColumn(body, 1);
            var title = new TextBlock { Text = task.Title, FontSize = 13, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap, MaxHeight = 38, TextTrimming = TextTrimming.CharacterEllipsis };
            if (task.IsCompleted) title.TextDecorations = TextDecorations.Strikethrough;
            title.SetResourceReference(TextBlock.ForegroundProperty, task.IsCompleted ? "TraySubtitleColor" : "ForegroundColor");
            body.Children.Add(title);
            var chips = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            if (!string.IsNullOrEmpty(task.DueDate))
            {
                var state = ToDoDates.DueState(task.DueDate, task.IsCompleted, DateTime.Now);
                chips.Children.Add(Chip(S.DueLabel(task.DueDate, DateTime.Now), state == ToDoDueState.Overdue ? Overdue : state == ToDoDueState.Today ? TodayTint : Neutral, glyph: "\U0001F4C5"));
            }
            if (task.DurationSeconds > 0) chips.Children.Add(Chip(ToDoDates.DurationLabelFromSeconds(task.DurationSeconds), glyph: "⏱"));
            if (task.SubtaskList.Count > 0) chips.Children.Add(Chip(task.SubtaskList.Count(st => st.IsCompleted) + "/" + task.SubtaskList.Count, glyph: "☑"));
            if (!compact)
            {
                string column = detail.Columns.FirstOrDefault(c => c.Id == detail.ColumnIdOf(task))?.Label;
                if (column != null) chips.Children.Add(Chip(column));
            }
            if (chips.Children.Count > 0) body.Children.Add(chips);
            grid.Children.Add(body);
            if (!task.IsCompleted)
            {
                double score = ToDoScoring.TopScoreOf(task, DateTime.Now);
                if (score > 0)
                {
                    var top = StatsUi.Text(ToDoScoring.Rounded(score), 11, FontWeights.SemiBold, wrap: false);
                    top.Foreground = Accent;
                    top.ToolTip = S.TopScoreTooltip;
                    top.Margin = new Thickness(8, 1, 0, 0);
                    Grid.SetColumn(top, 2);
                    grid.Children.Add(top);
                }
            }
            var card = new ToDoRowCard
            {
                Child = grid, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand, Opacity = task.IsCompleted ? 0.7 : 1,
            };
            card.SetResourceReference(Border.BackgroundProperty, compact ? "BackgroundColor" : "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetAutomationId(card, "cardProjectTask_" + task.Id);
            AutomationProperties.SetName(card, task.Title);

            Point? pressedAt = null;
            card.PreviewMouseLeftButtonDown += (s, e) => { if (!(e.OriginalSource is DependencyObject d) || !IsInButton(d, card)) pressedAt = e.GetPosition(card); };
            card.MouseLeftButtonUp += (s, e) =>
            {
                if (pressedAt.HasValue && !e.Handled) { pressedAt = null; _openEditor(task, detail.Project); }
            };
            // Keyboard: Tab reaches the task, Space or Enter opens it (the mouse click does the same).
            card.Select = card.Open = () => _openEditor(task, detail.Project);
            if (compact && detail.CanMoveTask)
            {
                card.MouseMove += (s, e) =>
                {
                    if (!pressedAt.HasValue || e.LeftButton != MouseButtonState.Pressed) return;
                    var delta = e.GetPosition(card) - pressedAt.Value;
                    if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                    pressedAt = null;
                    card.Opacity = 0.5;
                    DragDrop.DoDragDrop(card, new DataObject(DragFormat, task.Id), DragDropEffects.Move);
                    card.Opacity = task.IsCompleted ? 0.7 : 1;
                };
            }
            card.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                var menu = new ToDoMenu(card, PlacementMode.MousePoint);
                menu.Add(S.Edit, false, () => _openEditor(task, detail.Project), "menuProjectTaskEdit");
                if (detail.CanMoveTask)
                {
                    string current = detail.ColumnIdOf(task);
                    foreach (var column in detail.Columns.Where(c => c.Id != current))
                    {
                        var captured = column.Id;
                        menu.Add(S.MoveTo + " " + column.Label, false, () => _ = detail.MoveAsync(task, captured), "menuProjectMove_" + column.Id);
                    }
                }
                menu.Open();
            };
            return card;
        }

        private static bool IsInButton(DependencyObject source, DependencyObject stopAt)
        {
            for (var p = source; p != null && p != stopAt; p = VisualTreeHelper.GetParent(p) ?? LogicalTreeHelper.GetParent(p))
                if (p is ButtonBase) return true;
            return false;
        }
    }
}
