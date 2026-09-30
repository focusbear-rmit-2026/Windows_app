using FocusBear.Core.ToDo;
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
using System.Windows.Media.Effects;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// The Day Plan task-name composer: the Windows port of the macOS <c>DayPlanTasksPanel</c> name-field
    /// grammar (report 2026-09-25_20-13-11 — "Start outreach bot !3" was saved literally).
    ///
    /// <list type="bullet">
    /// <item><c>#project</c> files the task under a project, <c>!1</c>-<c>!5</c> sets its importance and
    /// <c>*15</c> its duration — parsed by <see cref="TaskNameCommit.ForDayPlanRow"/> in FocusBear.Core,
    /// the same rule the Mac commits by.</item>
    /// <item>While a <c>#token</c> is typed, a list of matching projects opens under the field: ↑/↓ move,
    /// Tab/Enter or a click accepts, Esc closes.</item>
    /// <item>Accepting a project for a line that is ONLY the token (typing "#sal" then Tab, the case the
    /// Mac had to fix) must not lose the task: the project is applied, the row regroups under it, and
    /// the caret stays in an empty name field showing "#project" until a real name is typed.</item>
    /// <item>A name made only of tokens is refused rather than saved blank, with a note saying why.</item>
    /// <item>A dismissible card under the row being edited lists the shortcuts (Mac TaskShortcutCard).</item>
    /// </list>
    ///
    /// Every entry point that names a task goes through the row's name field — "Add task", a project
    /// header's "＋" and renaming an existing row — so wiring the field wires all three.
    /// </summary>
    public partial class HabitsDayPlan
    {
        /// <summary>The Mac panel's own limits: suggestions after two typed characters, at most six rows.</summary>
        private const int SuggestMinTokenLength = 2;
        private const int SuggestMaxShown = 6;

        /// <summary>
        /// A project accepted for a line that was nothing but the <c>#token</c>, per task id: the name field
        /// is emptied, so this is what keeps a blur from reading the row as abandoned (Mac
        /// pendingProjectSelections).
        /// </summary>
        private readonly Dictionary<string, string> _pendingProjectSelections = new Dictionary<string, string>();

        /// <summary>A name field to re-open after the panel is rebuilt, with the text it should hold.</summary>
        private string _composerRestoreId;
        private string _composerRestoreText;

        // The `#project` suggestion list (one at a time, under the field being typed in).
        private Popup _suggestPopup;
        private TextBox _suggestField;
        private List<string> _suggestMatches = new List<string>();
        private readonly List<Border> _suggestRows = new List<Border>();
        private int _suggestIndex;
        private int _suggestTokenStart = -1;
        private int _suggestTokenLength;

        private Border _shortcutCard;

        private enum NameCommitOutcome
        {
            /// <summary>Nothing about the task changed.</summary>
            Unchanged,
            /// <summary>Only the title changed, and the field already shows it.</summary>
            PlainRename,
            /// <summary>Project, importance, duration or the row's text changed (or the row went) — rebuild.</summary>
            Structural,
            /// <summary>The name was only tokens: not committed, the text stays in the field.</summary>
            RefusedTokensOnly,
        }

        /// <summary>Per-field state shared by one name field's handlers.</summary>
        private sealed class ComposerField
        {
            public string TaskId;
            public TextBox Box;
            public Panel NameColumn;
            public Border Row;
            public TextBox Duration;
            public TextBlock Watermark;
            public TextBlock NameNeededHint;
            /// <summary>Set once this field's edit has been committed (or the field is being replaced), so the
            /// blur that follows does not commit it a second time — or commit stale text.</summary>
            public bool Finished;
        }

        /// <summary>
        /// Wire a panel row's name field to the composer. Called from <c>BuildPanelTaskRow</c> once the row
        /// exists; replaces the old plain commit-on-blur/Enter handlers.
        /// </summary>
        private void WireTaskNameComposer(DayPlanTask task, TextBox name, Panel nameColumn, Border row, TextBox duration)
        {
            var field = new ComposerField { TaskId = task.Id, Box = name, NameColumn = nameColumn, Row = row, Duration = duration };
            name.Tag = task.Id;
            System.Windows.Automation.AutomationProperties.SetAutomationId(name, "DayPlanTaskName");
            if (string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(name)))
                System.Windows.Automation.AutomationProperties.SetName(name, "Task name");

            // "#project" placeholder, shown while an accepted project waits for its task's name. A TextBox has
            // no placeholder, so it's a hit-test-transparent label laid over the field.
            int index = nameColumn.Children.IndexOf(name);
            if (index >= 0)
            {
                nameColumn.Children.RemoveAt(index);
                var host = new Grid();
                host.Children.Add(name);
                field.Watermark = new TextBlock
                {
                    FontSize = name.FontSize,
                    Foreground = SecBrush(),
                    IsHitTestVisible = false,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                host.Children.Add(field.Watermark);
                nameColumn.Children.Insert(index, host);
            }

            // Re-open the field the user was in before the panel rebuilt under them (a project accepted, or
            // a blur that regrouped the row) — with their text and the caret at its end. Set before the
            // TextChanged handler is attached so restoring doesn't reopen the suggestion list.
            if (_composerRestoreId == task.Id)
            {
                string restoreText = _composerRestoreText ?? "";
                _composerRestoreId = null;
                _composerRestoreText = null;
                name.Text = restoreText;
                name.Loaded += (s, e) =>
                {
                    name.Focus();
                    name.CaretIndex = name.Text.Length;
                    name.BringIntoView();
                };
            }
            UpdateWatermark(field);

            name.GotKeyboardFocus += (s, e) =>
            {
                field.Finished = false;   // a new edit of this field begins
                ShowShortcutCard(row);
            };
            name.LostKeyboardFocus += (s, e) => OnComposerBlur(field);
            name.TextChanged += (s, e) => OnComposerTextChanged(field);
            name.PreviewKeyDown += (s, e) => OnComposerKeyDown(field, e);
        }

        private void UpdateWatermark(ComposerField field)
        {
            if (field.Watermark == null) return;
            bool pending = _pendingProjectSelections.TryGetValue(field.TaskId, out string project);
            field.Watermark.Text = pending ? "#" + project : "";
            field.Watermark.Visibility = pending && string.IsNullOrEmpty(field.Box.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // MARK: - Keys / typing / blur --------------------------------------------------------------

        private void OnComposerKeyDown(ComposerField field, KeyEventArgs e)
        {
            bool listOpen = _suggestPopup != null && _suggestPopup.IsOpen && _suggestField == field.Box && _suggestMatches.Count > 0;
            if (listOpen)
            {
                if (e.Key == Key.Down) { MoveSuggestSelection(1); e.Handled = true; return; }
                if (e.Key == Key.Up) { MoveSuggestSelection(-1); e.Handled = true; return; }
                if ((e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None) || e.Key == Key.Enter)
                {
                    AcceptProjectSuggestion(field, _suggestMatches[_suggestIndex]);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Escape) { CloseProjectSuggestions(); e.Handled = true; return; }
            }

            if (e.Key == Key.Tab)
            {
                // Tab is a commit like any blur — EXCEPT that an unfinished (tokens-only) name keeps the caret
                // here, so Tab can't walk away from a row that isn't saved yet (Mac: saveName on insertTab).
                if (Decide(field).Kind == TaskNameCommitKind.RefuseTokensOnly)
                {
                    CommitComposedName(field);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                NameCommitOutcome outcome = CommitComposedName(field);
                if (outcome == NameCommitOutcome.RefusedTokensOnly) return;   // keep typing: the name isn't done
                field.Finished = true;
                Keyboard.ClearFocus();
                if (outcome == NameCommitOutcome.Structural) RefreshTasksAndAgenda();
                else if (outcome == NameCommitOutcome.PlainRename) RenderAgendaIfNested(field.TaskId);
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(field.TaskId, _selectedDate);
                field.Finished = true;
                if (t != null) field.Box.Text = t.Title;
                Keyboard.ClearFocus();
            }
        }

        private void OnComposerTextChanged(ComposerField field)
        {
            ClearNameNeededHint(field);   // the note goes as soon as the user starts giving it a name
            UpdateWatermark(field);
            PreviewNameTokens(field);
            UpdateProjectSuggestions(field);
        }

        private void OnComposerBlur(ComposerField field)
        {
            if (_suggestField == field.Box) CloseProjectSuggestions();
            RemoveShortcutCard();
            field.Box.Background = Brushes.Transparent;
            field.Box.BorderBrush = Brushes.Transparent;
            if (field.Finished) return;

            NameCommitOutcome outcome = CommitComposedName(field);
            if (outcome == NameCommitOutcome.Structural)
            {
                field.Finished = true;
                // After the focus change has finished routing: rebuilding now would destroy the field the
                // user just clicked into. Re-open that field (with its text) once the panel is rebuilt.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RememberPanelFocusForRebuild();   // the name or duration field the user moved to
                    RefreshTasksAndAgenda();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (outcome == NameCommitOutcome.PlainRename)
            {
                field.Finished = true;
                RenderAgendaIfNested(field.TaskId);
            }
        }

        /// <summary>A task nested under a habit/sprint/meeting also shows its title in the agenda.</summary>
        private void RenderAgendaIfNested(string taskId)
        {
            DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(taskId, _selectedDate);
            if (t != null && t.BlockKey != null) RenderDayPlan();
        }

        // MARK: - Commit ---------------------------------------------------------------------------------

        private TaskNameCommit Decide(ComposerField field)
        {
            _pendingProjectSelections.TryGetValue(field.TaskId, out string pending);
            return TaskNameCommit.ForDayPlanRow((field.Box.Text ?? "").Trim(),
                                                DayPlanTaskStore.Shared.AllProjectNames(), pending);
        }

        /// <summary>
        /// Persist a name edit by the Mac's <c>saveName</c> rule (see <see cref="TaskNameCommit"/>): an empty
        /// name discards the row, a name of only tokens is refused and kept as typed, and a real name is
        /// saved with its <c>#project</c>, <c>!priority</c> and <c>*minutes</c> applied and stripped.
        /// </summary>
        private NameCommitOutcome CommitComposedName(ComposerField field)
        {
            try
            {
                DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(field.TaskId, _selectedDate);
                if (t == null) return NameCommitOutcome.Unchanged;
                string trimmed = (field.Box.Text ?? "").Trim();
                _pendingProjectSelections.TryGetValue(field.TaskId, out string pending);
                TaskNameCommit commit = Decide(field);

                switch (commit.Kind)
                {
                    case TaskNameCommitKind.Discard:
                        _pendingProjectSelections.Remove(field.TaskId);
                        DayPlanTaskStore.Shared.Delete(field.TaskId, _selectedDate);   // empty name = discard
                        return NameCommitOutcome.Structural;

                    case TaskNameCommitKind.RefuseTokensOnly:
                        // Keep the raw text, so the tokens survive as a findable task literally called
                        // "#class" instead of the row vanishing. An emptied field after accepting a project
                        // keeps the "#project" title a from-scratch draft already carries.
                        string keep = trimmed.Length > 0 ? trimmed
                            : (string.IsNullOrEmpty(t.SourceTaskId) ? "#" + (pending ?? "") : t.Title);
                        if (keep != t.Title)
                        {
                            t.Title = keep;
                            DayPlanTaskStore.Shared.Update(t, _selectedDate);
                        }
                        ShowNameNeededHint(field);
                        return NameCommitOutcome.RefusedTokensOnly;

                    default:
                        ParsedQuickAddTask parsed = commit.Task;
                        _pendingProjectSelections.Remove(field.TaskId);
                        UpdateWatermark(field);
                        bool shape = false;
                        if (parsed.Project != null && !string.Equals(parsed.Project, t.Project, StringComparison.Ordinal))
                        {
                            t.Project = parsed.Project;
                            shape = true;
                        }
                        if (parsed.Outcome.HasValue && parsed.Outcome != t.Outcome) { t.Outcome = parsed.Outcome; shape = true; }
                        if (parsed.DurationMinutes.HasValue && parsed.DurationMinutes.Value != t.DurationMinutes)
                        {
                            t.DurationMinutes = parsed.DurationMinutes.Value;
                            shape = true;
                        }
                        bool renamed = parsed.Title != t.Title;
                        ClearNameNeededHint(field);
                        if (!renamed && !shape)
                        {
                            // Tokens that restated what the task already had still need stripping from the field.
                            return parsed.Title != trimmed ? NameCommitOutcome.Structural : NameCommitOutcome.Unchanged;
                        }
                        t.Title = parsed.Title;
                        DayPlanTaskStore.Shared.Update(t, _selectedDate);
                        if (shape) appSettings.CapturePostHog("day-plan-task-name-tokens-applied");
                        return (shape || parsed.Title != trimmed) ? NameCommitOutcome.Structural : NameCommitOutcome.PlainRename;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsDayPlan CommitComposedName - " + ex.Message);
                return NameCommitOutcome.Unchanged;
            }
        }

        /// <summary>
        /// Show what <c>!N</c> and <c>*N</c> will do WHILE they are typed (Mac previewNameTokens): the row's
        /// duration and importance band update in place. Nothing is written to the store here.
        /// </summary>
        private void PreviewNameTokens(ComposerField field)
        {
            TaskNameCommit commit = Decide(field);
            if (commit.Kind != TaskNameCommitKind.Save) return;
            ParsedQuickAddTask parsed = commit.Task;
            if (parsed.DurationMinutes.HasValue && field.Duration != null && !field.Duration.IsKeyboardFocusWithin)
                field.Duration.Text = DayPlanDurations.RowLabel(parsed.DurationMinutes.Value);
            if (parsed.Outcome.HasValue && !_selectedTaskIds.Contains(field.TaskId))
            {
                Color? band = DayPlanTaskVisuals.OutcomeColor(parsed.Outcome);
                Color? fg = DayPlanTaskVisuals.OutcomeForeground(parsed.Outcome);
                if (band.HasValue) field.Row.Background = new SolidColorBrush(band.Value);
                if (fg.HasValue)
                {
                    field.Box.Foreground = new SolidColorBrush(fg.Value);
                    field.Box.CaretBrush = new SolidColorBrush(fg.Value);
                    if (field.Duration != null) field.Duration.Foreground = new SolidColorBrush(WithAlpha(fg.Value, 0.85));
                }
            }
        }

        // MARK: - Name-needed note -------------------------------------------------------------------

        private void ShowNameNeededHint(ComposerField field)
        {
            if (field.NameNeededHint != null && field.NameNeededHint.Parent != null) return;   // don't stack them
            var hint = new TextBlock
            {
                Text = DayPlanComposerText.NameNeeded,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 2, 0, 2),
                Foreground = new SolidColorBrush(IsDarkTheme() ? Color.FromRgb(0xFF, 0x6B, 0x61) : Color.FromRgb(0xC4, 0x1E, 0x14))
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(hint, "DayPlanTaskNameNeeded");
            int index = 1;   // right under the name (its host grid is the column's first child)
            field.NameColumn.Children.Insert(Math.Min(index, field.NameColumn.Children.Count), hint);
            field.NameNeededHint = hint;
        }

        private static void ClearNameNeededHint(ComposerField field)
        {
            if (field.NameNeededHint == null) return;
            (field.NameNeededHint.Parent as Panel)?.Children.Remove(field.NameNeededHint);
            field.NameNeededHint = null;
        }

        // MARK: - #project suggestions ---------------------------------------------------------------

        /// <summary>
        /// Open (or refresh) the suggestion list for the <c>#token</c> immediately before the caret — spaces
        /// allowed, so "#Focus B" still finds "Focus Bear"; underscores stand for spaces. Names that start
        /// with the token come first, then names merely containing it (Mac updateProjectSuggestions).
        /// </summary>
        private void UpdateProjectSuggestions(ComposerField field)
        {
            TextBox box = field.Box;
            string text = box.Text ?? "";
            int caret = Math.Max(0, Math.Min(box.CaretIndex, text.Length));
            string upToCaret = text.Substring(0, caret);
            int hash = upToCaret.LastIndexOf('#');
            if (hash < 0) { CloseProjectSuggestions(); return; }
            string fragment = upToCaret.Substring(hash + 1);
            if (fragment.Trim().Length < SuggestMinTokenLength) { CloseProjectSuggestions(); return; }

            List<string> matches = PanelProjectMatches(fragment.Replace('_', ' ').Trim(),
                                                       DayPlanTaskStore.Shared.AllProjectNames());
            if (matches.Count == 0) { CloseProjectSuggestions(); return; }

            _suggestTokenStart = hash;
            _suggestTokenLength = caret - hash;
            ShowProjectSuggestions(field, matches);
        }

        private static List<string> PanelProjectMatches(string token, List<string> projects)
        {
            CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
            const CompareOptions loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            List<string> named = projects.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            List<string> starting = named.Where(p => compare.IsPrefix(p, token, loose)).ToList();
            IEnumerable<string> containing = named.Where(p => !starting.Contains(p) && compare.IndexOf(p, token, loose) >= 0);
            return starting.Concat(containing).Take(SuggestMaxShown).ToList();
        }

        private void ShowProjectSuggestions(ComposerField field, List<string> matches)
        {
            bool sameList = _suggestPopup != null && _suggestPopup.IsOpen && _suggestField == field.Box
                            && _suggestMatches.SequenceEqual(matches);
            if (sameList) return;   // keep the highlight where the user moved it

            CloseProjectSuggestions();
            _suggestField = field.Box;
            _suggestMatches = matches;
            _suggestIndex = 0;
            _suggestRows.Clear();

            var list = new StackPanel { Orientation = Orientation.Vertical };
            for (int i = 0; i < matches.Count; i++)
            {
                string project = matches[i];
                var rowLabel = new TextBlock
                {
                    Text = project,
                    FontSize = 13,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                rowLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                var suggestion = new Border
                {
                    Child = rowLabel,
                    Padding = new Thickness(10, 5, 10, 5),
                    CornerRadius = new CornerRadius(5),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };
                System.Windows.Automation.AutomationProperties.SetAutomationId(suggestion, "DayPlanProjectSuggestion");
                System.Windows.Automation.AutomationProperties.SetName(suggestion, project);
                // Mouse-DOWN, handled: the field keeps keyboard focus, so accepting isn't preceded by a blur
                // that would commit the half-typed "#partial" as the name first.
                suggestion.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    AcceptProjectSuggestion(field, project);
                };
                _suggestRows.Add(suggestion);
                list.Children.Add(suggestion);
            }

            var card = new Border
            {
                Child = list,
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 2, 12, 12),   // room for the shadow
                MinWidth = Math.Max(160, field.Box.ActualWidth),
                Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = 0.28, Direction = 270 }
            };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");

            _suggestPopup = new Popup
            {
                Child = card,
                PlacementTarget = field.Box,
                Placement = PlacementMode.Bottom,
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false
            };
            UpdateSuggestHighlight();
            _suggestPopup.IsOpen = true;
        }

        private void MoveSuggestSelection(int delta)
        {
            if (_suggestMatches.Count == 0) return;
            _suggestIndex = Math.Max(0, Math.Min(_suggestMatches.Count - 1, _suggestIndex + delta));
            UpdateSuggestHighlight();
        }

        private void UpdateSuggestHighlight()
        {
            for (int i = 0; i < _suggestRows.Count; i++)
                _suggestRows[i].Background = i == _suggestIndex
                    ? new SolidColorBrush(WithAlpha(Color.FromRgb(0xDE, 0x74, 0x00), 0.28))
                    : Brushes.Transparent;
        }

        private void CloseProjectSuggestions()
        {
            if (_suggestPopup != null) _suggestPopup.IsOpen = false;
            _suggestPopup = null;
            _suggestField = null;
            _suggestMatches = new List<string>();
            _suggestRows.Clear();
            _suggestTokenStart = -1;
            _suggestTokenLength = 0;
        }

        /// <summary>
        /// Assign the picked project and strip the typed <c>#partial</c> — ONLY that token (from its <c>#</c>
        /// to the caret), so a <c>#</c> typed mid-name doesn't truncate the rest of the title. Then the row
        /// regroups under its project and the caret goes straight back into its name (Mac
        /// acceptProjectSuggestion + regroupProjectAndContinueEditing).
        /// </summary>
        private void AcceptProjectSuggestion(ComposerField field, string project)
        {
            try
            {
                string text = field.Box.Text ?? "";
                if (_suggestTokenStart >= 0 && _suggestTokenStart + _suggestTokenLength <= text.Length)
                    text = text.Remove(_suggestTokenStart, _suggestTokenLength);
                else
                {
                    int hash = text.LastIndexOf('#');
                    if (hash >= 0) text = text.Substring(0, hash);
                }
                string cleanTitle = text.Replace("  ", " ").Trim();
                CloseProjectSuggestions();

                DayPlanTask t = DayPlanTaskStore.Shared.TaskWithId(field.TaskId, _selectedDate);
                if (t == null) return;
                t.Project = project;
                if (cleanTitle.Length == 0)
                {
                    // "#sal" then Tab leaves nothing to call the task. Take the project, keep the draft as
                    // "#project" so it can't be pruned as blank, and wait for a real name (Mac report
                    // 2026-09-09_20-38-22 follow-up: "If I type #v2 and press tab, the task vanishes").
                    _pendingProjectSelections[field.TaskId] = project;
                    if (string.IsNullOrEmpty(t.SourceTaskId)) t.Title = "#" + project;
                }
                else
                {
                    t.Title = cleanTitle;
                }
                DayPlanTaskStore.Shared.Update(t, _selectedDate);
                appSettings.CapturePostHog("day-plan-task-project-suggestion-accepted");

                // The field is about to be rebuilt under its project header; its blur must not commit the
                // stale "#partial" text over what was just saved.
                field.Finished = true;
                _composerRestoreId = field.TaskId;
                _composerRestoreText = cleanTitle;
                RefreshTasksAndAgenda();
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan AcceptProjectSuggestion - " + ex.Message); }
        }

        // MARK: - Shortcut card ----------------------------------------------------------------------

        /// <summary>
        /// The card explaining what can be typed into a task name, directly under the task being edited
        /// (Mac showShortcutCard). One at a time, following focus; its ✕ retires it for good, per account.
        /// </summary>
        private void ShowShortcutCard(Border row)
        {
            RemoveShortcutCard();
            if (RegistryManager.LoadDayPlanShortcutCardDismissed()) return;
            int index = TasksListStack.Children.IndexOf(row);
            if (index < 0) return;

            var column = new StackPanel { Orientation = Orientation.Vertical };
            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock
            {
                Text = DayPlanComposerText.ShortcutsTitle,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            heading.Children.Add(title);

            var closeGlyph = new TextBlock { Text = "✕", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            closeGlyph.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var close = new Button
            {
                Content = closeGlyph,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 2, 0),
                Cursor = Cursors.Hand,
                Focusable = false,   // clicking it must not end the edit in progress
                Template = BorderlessButtonTemplate(),
                ToolTip = DayPlanComposerText.ShortcutsDismiss,
                VerticalAlignment = VerticalAlignment.Top
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(close, "DayPlanShortcutCardDismiss");
            System.Windows.Automation.AutomationProperties.SetName(close, DayPlanComposerText.ShortcutsDismiss);
            close.Click += (s, e) =>
            {
                RegistryManager.SaveDayPlanShortcutCardDismissed(true);
                appSettings.CapturePostHog("day-plan-task-shortcut-card-dismissed");
                RemoveShortcutCard();
            };
            Grid.SetColumn(close, 1);
            heading.Children.Add(close);
            column.Children.Add(heading);

            foreach (string hint in DayPlanComposerText.DayPlanRowHints)
            {
                var line = new TextBlock { Text = hint, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0), Opacity = 0.82 };
                line.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                column.Children.Add(line);
            }

            var card = new Border
            {
                Child = column,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 6, 9, 7),
                Margin = new Thickness(28, 2, 4, 6)   // under the name, past the grip + checkbox
            };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            System.Windows.Automation.AutomationProperties.SetAutomationId(card, "DayPlanShortcutCard");
            TasksListStack.Children.Insert(index + 1, card);
            _shortcutCard = card;
        }

        private void RemoveShortcutCard()
        {
            if (_shortcutCard == null) return;
            (_shortcutCard.Parent as Panel)?.Children.Remove(_shortcutCard);
            _shortcutCard = null;
        }
    }
}
