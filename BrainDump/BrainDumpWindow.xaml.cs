using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.Notes;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// Native Notes / Brain Dump screen, replacing the dashboard.focusbear.io/webview/notes webview. Talks to
    /// the same <c>/note</c> and <c>/projects/:id/notes</c> API the web dashboard and the Mac use, so notes
    /// stay in sync everywhere. Port of the Mac's <c>Notes/NotesView.swift</c> + <c>NotesStore.swift</c>; the
    /// rules themselves live in <c>FocusBear.Core.Notes</c> and are tested there.
    ///
    /// Behaviour: first page + tags on open, 500 ms debounced search, infinite scroll, a brain dump saved 2 s
    /// after the last keystroke, tag filter via <c>GET /note?tag_id=</c>, and shared (project) notes on the
    /// same timeline — read here, edited on the web, where several people can type in one at once.
    /// </summary>
    public partial class BrainDumpWindow : UserControl
    {
        private const double EditorFontSize = 14;

        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private readonly Random _random = new Random();

        // Strings (notesViewInfo, and the project-note copy the To-Do screen already translates).
        private string _sBrainDumpTitle, _sShowBrainDump, _sHideBrainDump, _sSaving, _sSave, _sCreateNoteTitle, _sEditNoteTitle,
            _sNoNotesFound, _sNoNotesMatchSearch, _sNoArchivedNotes, _sDeleteNoteTitle, _sDeleteConfirmation, _sDelete, _sCancel,
            _sNoteCreated, _sNoteUpdated, _sNoteDeleted, _sCouldNotCreateNote, _sCouldNotUpdateNote, _sRequestFailed, _sAllTags,
            _sLinkedToHabit, _sProjectFilterAll, _sArchiveNote, _sRestoreNote, _sArchiveNoteHelp, _sRestoreNoteHelp, _sNoteArchived,
            _sNoteRestored, _sCouldNotArchiveNote, _sCouldNotRestoreNote, _sLastEdited, _sCreateNote, _sUntitledNote, _sNoteRenamed,
            _sCouldNotRenameNote, _sDeleteProjectNoteConfirm, _sCouldNotDeleteProjectNote, _sCouldNotCreateProjectNote, _sProjectNoteDeleted,
            _sLoading, _sNoExistingProjects;

        // Personal notes
        private readonly ObservableCollection<NoteRow> _rows = new ObservableCollection<NoteRow>();
        private List<Note> _notes = new List<Note>();
        private List<Note> _searchResults;
        private int _currentPage = NotesPaging.FirstPage;
        private bool _hasNextPage;
        private bool _isFetching;
        private readonly NotesPageLoadGate _pageGate = new NotesPageLoadGate();
        private bool _isSearching;
        private string _selectedTagId;
        private List<NoteTag> _userTags = new List<NoteTag>();
        private readonly ObservableCollection<NoteTag> _tagFilterItems = new ObservableCollection<NoteTag>();
        private readonly DispatcherTimer _searchTimer = new DispatcherTimer { Interval = NotesTiming.SearchDebounce };

        // Brain dump
        private readonly BrainDumpTracker _brainDump = new BrainDumpTracker();
        private string _lastSavedBrainDumpBody = "";
        private bool _suppressBrainDumpChange;
        private bool _hasScannedForBrainDump;
        private bool _isSavingBrainDump;
        private bool _flushWhenSaved;   // left the screen while a save was out: send the rest the moment it lands
        private bool _retryingFailedSave;  // the scheduled save is a retry after a failure (offline)

        /// <summary>
        /// The text not yet confirmed saved, per signed-in user, kept on disk: a failed save used to retry for as long
        /// as the app ran, even with the screen closed, and a quit lost the text. Restored into the editor next time.
        /// </summary>
        private static BrainDumpDraft Draft
        {
            get
            {
                string user = AppSettings.GetInstance.userAccount?.userID;
                string safe = new string((string.IsNullOrEmpty(user) ? "signed-out" : user)
                    .Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                return new BrainDumpDraft(System.IO.Path.Combine(AppSettings.AppDataDirPath, "BrainDump", "draft-" + safe + ".html"));
            }
        }
        private readonly DispatcherTimer _brainDumpSaveTimer = new DispatcherTimer { Interval = NotesTiming.BrainDumpSaveDelay };

        // Personal note editor
        private Note _editingNote;
        private string _editingOpeningBodyHtml;
        private readonly ObservableCollection<NoteTag> _selectedTags = new ObservableCollection<NoteTag>();
        private readonly ObservableCollection<NoteTag> _existingTagSuggestions = new ObservableCollection<NoteTag>();
        private bool _isSavingNote;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = NotesTiming.StatusMessage };

        // Shared (project) notes
        private NotesScope _scope;
        private bool _suppressScopeChange;
        private string _projectFilter;
        private bool _isCreatingProjectNote;
        private bool _isRenamingProjectNote;
        private List<Project> _projects = new List<Project>();
        private List<ProjectNoteEntry> _projectNotes = new List<ProjectNoteEntry>();
        private List<ProjectNoteEntry> _archivedProjectNotes = new List<ProjectNoteEntry>();
        private bool _isLoadingProjectNotes, _isLoadingArchivedProjectNotes, _hasLoadedProjectNotes, _hasLoadedArchivedProjectNotes, _isArchivingNote;
        private int _projectNotesGeneration, _archivedNotesGeneration;
        private ProjectNoteEntry _openProjectNote;

        public BrainDumpWindow()
        {
            InitializeComponent();
            rtbBrainDump.PreviewKeyDown += NoteEditor_PreviewKeyDown;
            rtbNoteBody.PreviewKeyDown += NoteEditor_PreviewKeyDown;
            Utils.IdleProgressBars.Watch(LoadingIndicator, ProjectNoteLoading);   // collapsed "loading" bars: no 60 fps storyboard while hidden

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            LoadStrings();
            // The server's limits: past them a save is refused with nothing but "could not save".
            txtNoteTitle.MaxLength = NoteLimits.NoteTitle;
            txtNewTag.MaxLength = NoteLimits.TagText;
            txtNewProjectNoteTitle.MaxLength = NoteLimits.ProjectNoteTitle;
            txtProjectNoteTitle.MaxLength = NoteLimits.ProjectNoteTitle;

            lvNotes.ItemsSource = _rows;
            cmbTagFilter.ItemsSource = _tagFilterItems;
            icSelectedTags.ItemsSource = _selectedTags;
            icExistingTags.ItemsSource = _existingTagSuggestions;

            _searchTimer.Tick += SearchTimer_Tick;
            _brainDumpSaveTimer.Tick += BrainDumpSaveTimer_Tick;
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); lblStatus.Visibility = Visibility.Collapsed; lblProjectNoteStatus.Visibility = Visibility.Collapsed; };

            SetBrainDumpVisible(RegistryManager.GetNotesBrainDumpVisible());
            SetScopeSilently(NotesScopeRules.Restore(RegistryManager.GetNotesScope()));
            NoteFlowDocument.Load(rtbBrainDump, "", EditorFontSize);
            ProjectFilterPanel.SelectionCommitted += (s, e) => ApplyProjectFilter(ProjectFilterPanel.SelectedId);
            NewProjectNoteProject.SelectionCommitted += (s, e) => UpdateCreateProjectNoteButton();
            IsVisibleChanged += BrainDumpWindow_IsVisibleChanged;
        }

        private string S(string fallback, string key)
        {
            return appSettings.GetConfigStringSafe(fallback, "notesViewInfo", key);
        }

        private string ToDoS(string fallback, string key)
        {
            return appSettings.GetConfigStringSafe(fallback, "toDoListViewInfo", key);
        }

        private void LoadStrings()
        {
            try
            {
                _sBrainDumpTitle = S("Brain Dump", "brainDumpTitle");
                lblBrainDumpTitle.Text = _sBrainDumpTitle;
                lblBrainDumpInfo.Text = S("Use Brain Dump to capture ideas fast. Saved notes you've added or edited appear in the list below.", "brainDumpInfoText");
                lblBrainDumpPlaceholder.Text = S("Start typing your thoughts...", "brainDumpPlaceholder");
                _sShowBrainDump = S("Show brain dump", "showBrainDumpText");
                _sHideBrainDump = S("Hide brain dump", "hideBrainDumpText");
                _sSaving = S("Saving…", "savingText");
                _sSave = S("Save", "saveText");
                _sCancel = S("Cancel", "cancelText");
                lblCancelNote.Text = _sCancel;
                lblSearchPlaceholder.Text = S("Search notes...", "searchPlaceholder");
                lblNewNote.Text = "+  " + S("New note", "newNoteText");
                _sCreateNoteTitle = S("Create Note", "createNoteTitle");
                _sEditNoteTitle = S("Edit Note", "editNoteTitle");
                lblNoteTitlePlaceholder.Text = S("Title", "noteTitlePlaceholder");
                lblNoteBodyPlaceholder.Text = S("Write your note here...", "noteBodyPlaceholder");
                lblTagsLabel.Text = S("Tags", "tagsLabel");
                lblNewTagPlaceholder.Text = S("Add a tag...", "addTagPlaceholder");
                lblAddTag.Text = S("Add", "addTagText");
                lblExistingTags.Text = S("Existing tags", "existingTagsText") + ":";
                _sLinkedToHabit = S("Linked to habit", "linkedToHabitText");
                _sNoNotesFound = S("No notes found", "noNotesFoundText");
                _sNoNotesMatchSearch = S("No notes found matching your search", "noNotesMatchSearchText");
                _sNoArchivedNotes = S("No archived notes", "noArchivedNotesText");
                _sDeleteNoteTitle = S("Delete Note", "deleteNoteTitle");
                _sDeleteConfirmation = S("Are you sure you want to delete this note?", "deleteConfirmationText");
                _sDelete = S("Delete", "deleteText");
                _sNoteCreated = S("Note created successfully", "noteCreatedText");
                _sNoteUpdated = S("Note updated successfully", "noteUpdatedText");
                _sNoteDeleted = S("Note deleted successfully", "noteDeletedText");
                _sCouldNotCreateNote = S("Could not create note", "couldNotCreateNoteText");
                _sCouldNotUpdateNote = S("Could not update note", "couldNotUpdateNoteText");
                _sRequestFailed = S("Could not process the request", "requestFailedText");
                _sAllTags = S("All tags", "allTagsText");
                cmbTagFilter.ToolTip = S("Filter by tag", "filterByTagText");

                btnBrainDumpBold.ToolTip = btnNoteBold.ToolTip = S("Bold", "boldText");
                btnBrainDumpItalic.ToolTip = btnNoteItalic.ToolTip = S("Italic", "italicText");
                btnBrainDumpUnderline.ToolTip = btnNoteUnderline.ToolTip = S("Underline", "underlineText");
                btnBrainDumpBullets.ToolTip = btnNoteBullets.ToolTip = S("Bulleted list", "bulletListText");
                btnBrainDumpNumbering.ToolTip = btnNoteNumbering.ToolTip = S("Numbered list", "numberedListText");

                rbScopeAll.Content = S("All notes", "scopeAllText");
                rbScopePersonal.Content = S("Personal Notes", "scopePersonalText");
                rbScopeProject.Content = S("Shared Notes", "scopeProjectNotesText");
                rbScopeArchived.Content = S("Archived", "scopeArchivedText");
                _sProjectFilterAll = S("All projects", "projectFilterAll");
                ProjectFilterPanel.ToolTip = S("Show shared notes from one project only", "projectFilterHelp");
                ProjectFilterPanel.ClearedTitle = _sProjectFilterAll;
                NewProjectNoteProject.Placeholder = ToDoS("Project", "filterProjectText");
                lblBackToNotes.Text = "←  " + S("Back to notes", "backToNotesText");
                _sArchiveNote = S("Archive", "archiveNoteText");
                _sArchiveNoteHelp = S("File this note away — it leaves the list but keeps everything in it", "archiveNoteHelpText");
                _sRestoreNote = S("Restore", "restoreNoteText");
                _sRestoreNoteHelp = S("Put this note back in the list", "restoreNoteHelpText");
                _sNoteArchived = S("Note archived", "noteArchivedText");
                _sNoteRestored = S("Note restored", "noteRestoredText");
                _sCouldNotArchiveNote = S("Could not archive the note", "couldNotArchiveNoteText");
                _sCouldNotRestoreNote = S("Could not restore the note", "couldNotRestoreNoteText");

                lblEditOnWeb.Text = ToDoS("Edit on the web", "editOnWebText") + "  ↗";
                lblNewProjectNote.Text = "👥  " + S("New shared note", "newProjectNoteText");
                lblNewProjectNoteTitle.Text = S("New shared note", "newProjectNoteText");
                lblCancelNewProjectNote.Text = _sCancel;
                _sCreateNote = ToDoS("Create note", "createNoteText");
                lblCreateProjectNote.Text = _sCreateNote;
                lblNewProjectNoteTitlePlaceholder.Text = S("Title", "noteTitlePlaceholder");
                _sUntitledNote = ToDoS("Untitled note", "untitledNoteText");
                txtProjectNoteTitle.ToolTip = ToDoS("Click to rename this note", "renameNoteHintText");
                _sNoteRenamed = ToDoS("Note renamed", "noteRenamedText");
                _sCouldNotRenameNote = ToDoS("Could not rename the note", "couldNotRenameNoteText");
                _sDeleteProjectNoteConfirm = ToDoS("Delete this note? This cannot be undone.", "deleteNoteConfirmText");
                _sCouldNotDeleteProjectNote = ToDoS("Could not delete the note", "couldNotDeleteNoteText");
                _sCouldNotCreateProjectNote = ToDoS("Could not create the note", "couldNotCreateNoteText");
                _sProjectNoteDeleted = ToDoS("Note deleted", "noteDeletedText");
                _sLoading = ToDoS("Loading…", "loadingText");
                _sNoExistingProjects = ToDoS("No existing projects", "noExistingProjectsText");
                lblDeleteProjectNote.Text = "🗑  " + ToDoS("Delete", "deleteText");
                lblProjectNoteWebOnly.Text = ToDoS("The note body is edited on the web, where several people can type in it at once. You can rename, create and delete notes here.", "noteBodyWebOnlyText");
                _sLastEdited = ToDoS("Last edited <DATE>", "lastEditedText");

                lblBrainDumpSaving.Text = _sSaving;
                lblSaveNote.Text = _sSave;
                lblShowBrainDump.Text = "🧠  " + _sShowBrainDump;
                btnHideBrainDump.ToolTip = _sHideBrainDump;
                btnShowBrainDump.ToolTip = _sShowBrainDump;
                lblEmptyTitle.Text = _sNoNotesFound;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BrainDumpWindow LoadStrings() ex - " + ex.Message);
            }
        }

        /// <summary>Kept for existing callers (App.ShowBrainDumpWindow, the tray menu, the global hotkey).</summary>
        public void ShowBrainDumpWindowAndStartTimer()
        {
            LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer()");
            if (IsVisible) _ = RefreshAsync();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = RefreshAsync();
        }

        private void BrainDumpWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                _ = RefreshAsync();
            }
            else
            {
                // Leaving the screen: save what the debounce is still holding, and step out of an open note.
                FlushPendingBrainDumpSave();
                // A save that already failed stops retrying here: its text is kept as the local draft and restored
                // (and sent) the next time the screen opens.
                if (_retryingFailedSave)
                {
                    _brainDumpSaveTimer.Stop();
                    _retryingFailedSave = false;
                }
                CloseProjectNote();
            }
        }

        // ================================================================== loading

        /// <summary>Web <c>WebviewNotes</c> mount: tags + first page (which also carries the brain dump).</summary>
        private async Task RefreshAsync()
        {
            _ = LoadTagsAsync();
            _ = ScanForBrainDumpOnceAsync();
            _ = LoadProjectNotesAsync(false);
            if (_scope.ShowsArchivedNotes()) _ = LoadArchivedProjectNotesAsync(false);
            if (IsSearchActive) await RunSearchAsync();
            else await LoadPageAsync(NotesPaging.FirstPage, false);
        }

        private bool IsSearchActive { get { return PersonalNotesList.IsSearchActive(txtSearch.Text); } }

        private async Task LoadPageAsync(int page, bool append)
        {
            // A page-1 reload asked for while a load is out waits for it rather than being dropped.
            if (!_pageGate.TryStart(append)) return;
            _isFetching = true;
            string tagId = _selectedTagId;
            UpdateLoading();
            try
            {
                string body = await new API.FocusBearAPIClient().GetNotesPage(page, NotesPaging.PageSize, tagId);
                // A stale page for a filter the user has since changed must not replace the list.
                if (tagId != _selectedTagId) return;
                NotesPage result = NotesWire.ReadPage(body);
                if (result == null) { ShowStatus(_sRequestFailed); return; }
                _notes = PersonalNotesList.Merge(_notes, result.Data, append);
                _hasNextPage = result.HasNextPage;
                _currentPage = page;
                AdoptBrainDump(result.Data);
            }
            finally
            {
                _isFetching = false;
                UpdateLoading();
                RenderList();
                if (_pageGate.Finish()) _ = LoadPageAsync(NotesPaging.FirstPage, false);
            }
        }

        private async Task LoadTagsAsync()
        {
            var tags = NotesWire.ReadTags(await new API.FocusBearAPIClient().GetNoteTags());
            if (tags == null) return;
            _userTags = tags;
            RebuildTagFilter();
        }

        private void RebuildTagFilter()
        {
            string previous = _selectedTagId ?? "";
            _tagFilterItems.Clear();
            _tagFilterItems.Add(new NoteTag { Id = "", Text = _sAllTags });
            foreach (var tag in _userTags) _tagFilterItems.Add(tag);
            cmbTagFilter.SelectedItem = _tagFilterItems.FirstOrDefault(t => t.Id == previous) ?? _tagFilterItems[0];
            cmbTagFilter.Visibility = _userTags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Web <c>createOrUpdateNote</c> / <c>deleteNote</c> re-fetch the list, and re-run an active search.</summary>
        private async Task RefreshAfterWriteAsync()
        {
            await LoadPageAsync(NotesPaging.FirstPage, false);
            if (IsSearchActive) await RunSearchAsync();
            _ = LoadTagsAsync();
        }

        private void RenderList()
        {
            var personal = PersonalNotesList.Visible(_notes, _searchResults, IsSearchActive, _selectedTagId);
            var shared = NotesFeed.ProjectNotes(_projectNotes, _archivedProjectNotes, _scope);
            // The choice is kept, but applied only while its control is on screen to show and clear it.
            string projectFilter = NotesFeed.EffectiveProjectFilter(_projectFilter, _scope, NotesFeed.FilterableProjects(shared));
            var items = NotesFeed.Items(personal, shared, _scope, txtSearch.Text, _selectedTagId != null, projectFilter);

            _rows.Clear();
            foreach (var item in items) _rows.Add(NoteRow.For(item, _sLinkedToHabit, _sUntitledNote));

            RebuildProjectFilter(shared);

            bool loading = _isFetching || _isSearching || (_scope.IncludesProjectNotes() && (_isLoadingProjectNotes || _isLoadingArchivedProjectNotes));
            EmptyState.Visibility = _rows.Count == 0 && !loading ? Visibility.Visible : Visibility.Collapsed;
            if (_scope.ShowsArchivedNotes()) { lblEmptyTitle.Text = _sNoArchivedNotes; lblEmptyDescription.Text = ""; }
            else { lblEmptyTitle.Text = _sNoNotesFound; lblEmptyDescription.Text = IsSearchActive ? _sNoNotesMatchSearch : ""; }
            // A new note is an active note: offering one from the archive would file it where the user is not looking.
            btnNewNote.Visibility = _scope.ShowsArchivedNotes() ? Visibility.Collapsed : Visibility.Visible;
            btnNewProjectNote.Visibility = btnNewNote.Visibility;
            if (_scope.ShowsArchivedNotes()) NewProjectNoteCard.Visibility = Visibility.Collapsed;
        }

        private void UpdateLoading()
        {
            bool loading = _isFetching || _isSearching;
            LoadingIndicator.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            lblSearching.Visibility = _isSearching ? Visibility.Visible : Visibility.Collapsed;
            lblProjectNotesLoading.Visibility = (_isLoadingProjectNotes || _isLoadingArchivedProjectNotes) && _scope.IncludesProjectNotes() ? Visibility.Visible : Visibility.Collapsed;
            if (loading) EmptyState.Visibility = Visibility.Collapsed;
        }

        // Infinite scroll: the next page when the list is scrolled near the bottom.
        private void lvNotes_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange <= 0 || IsSearchActive || !_hasNextPage || _isFetching || !_scope.IncludesPersonalNotes()) return;
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 40)
                _ = LoadPageAsync(_currentPage + 1, true);
        }

        // ================================================================== search + tag filter

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            _searchTimer.Stop();
            _searchTimer.Start();
            // Project notes are filtered locally, so their half of the list can follow the text at once.
            RenderList();
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            if (IsSearchActive)
            {
                await RunSearchAsync();
            }
            else
            {
                _searchResults = null;
                await LoadPageAsync(NotesPaging.FirstPage, false);
            }
        }

        private async Task RunSearchAsync()
        {
            string query = txtSearch.Text.Trim();
            if (query.Length == 0) return;
            _isSearching = true;
            UpdateLoading();
            string body = await new API.FocusBearAPIClient().SearchNotes(query, NotesPaging.SearchTake);
            _isSearching = false;
            UpdateLoading();
            // Ignore a stale response if the query moved on meanwhile.
            if (query != txtSearch.Text.Trim()) return;
            List<Note> found = NotesWire.ReadNotes(body);
            _searchResults = found ?? new List<Note>();
            // Offline, "No notes found matching your search" alone reads as "you have no such note".
            if (found == null) ShowStatus(_sRequestFailed);
            RenderList();
        }

        private void cmbTagFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbTagFilter.SelectedItem as NoteTag;
            string tagId = selected == null || string.IsNullOrEmpty(selected.Id) ? null : selected.Id;
            if (tagId == _selectedTagId) return;
            _selectedTagId = tagId;
            if (IsSearchActive) RenderList();
            else _ = LoadPageAsync(NotesPaging.FirstPage, false);
        }

        // ================================================================== scope + project filter

        private void SetScopeSilently(NotesScope scope)
        {
            _suppressScopeChange = true;
            _scope = scope;
            rbScopeAll.IsChecked = scope == NotesScope.All;
            rbScopePersonal.IsChecked = scope == NotesScope.Personal;
            rbScopeProject.IsChecked = scope == NotesScope.Project;
            rbScopeArchived.IsChecked = scope == NotesScope.Archived;
            _suppressScopeChange = false;
        }

        private void Scope_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressScopeChange) return;
            NotesScope scope = sender == rbScopePersonal ? NotesScope.Personal
                : sender == rbScopeProject ? NotesScope.Project
                : sender == rbScopeArchived ? NotesScope.Archived
                : NotesScope.All;
            if (scope == _scope) return;
            _scope = scope;
            // The archive is a place you visit, not a place you live.
            if (scope.IsWorthPersisting()) RegistryManager.SetNotesScope(scope.RawValue());
            if (scope.ShowsArchivedNotes()) _ = LoadArchivedProjectNotesAsync(false);
            UpdateLoading();
            RenderList();
        }

        /// <summary>
        /// Only worth showing once shared notes are in view and there is more than one project to choose
        /// between — otherwise it is a control with a single option.
        /// </summary>
        private void RebuildProjectFilter(List<ProjectNoteEntry> shared)
        {
            var options = NotesFeed.FilterableProjects(shared);
            ProjectFilterPanel.Visibility = _scope.IncludesProjectNotes() && options.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            ProjectFilterPanel.SetOptions(options);
            ProjectFilterPanel.SelectedId = _projectFilter;
        }

        private void ApplyProjectFilter(string projectId)
        {
            if (projectId == _projectFilter) return;
            // Deliberately not persisted: narrowing to one project is a momentary act, and coming back to a
            // silently filtered list later reads as missing notes.
            _projectFilter = projectId;
            RenderList();
        }

        // ================================================================== shared notes

        /// <summary>
        /// Every active project's notes. <c>GET /projects/:id/notes</c> is per project, so this is a
        /// catalogue walk plus one call per project, a few at a time — hence once per session.
        /// </summary>
        private async Task LoadProjectNotesAsync(bool force)
        {
            if (_isLoadingProjectNotes || (!force && _hasLoadedProjectNotes)) return;
            _isLoadingProjectNotes = true;
            int generation = ++_projectNotesGeneration;
            UpdateLoading();
            var client = new API.FocusBearAPIClient();
            List<Project> projects = await LoadActiveProjectsAsync(client);
            if (generation != _projectNotesGeneration) return;
            if (projects == null)
            {
                _isLoadingProjectNotes = false;
                UpdateLoading();
                RenderList();
                if (_scope.IncludesProjectNotes()) ShowStatus(_sRequestFailed);
                return;
            }
            ProjectNotesSweepResult result = await SweepAsync(client, projects, false);
            if (generation != _projectNotesGeneration) return;
            _isLoadingProjectNotes = false;
            // A sweep that lost projects has not really loaded; leaving the gate open means simply
            // reopening the screen tries them again.
            _hasLoadedProjectNotes = result.IsComplete;
            _projects = projects;
            _projectNotes = result.Entries.ToList();
            ReportIncompleteSweep(result);
            UpdateLoading();
            RenderList();
            // The archive can be opened before the catalogue lands; pick it up now it has.
            if (_scope.ShowsArchivedNotes()) _ = LoadArchivedProjectNotesAsync(false);
        }

        /// <summary>The archive, fetched the first time it is opened, from whatever catalogue the active sweep has.</summary>
        private async Task LoadArchivedProjectNotesAsync(bool force)
        {
            if (_isLoadingArchivedProjectNotes || _projects.Count == 0 || (!force && _hasLoadedArchivedProjectNotes)) return;
            _isLoadingArchivedProjectNotes = true;
            int generation = ++_archivedNotesGeneration;
            UpdateLoading();
            ProjectNotesSweepResult result = await SweepAsync(new API.FocusBearAPIClient(), _projects, true);
            if (generation != _archivedNotesGeneration) return;
            _isLoadingArchivedProjectNotes = false;
            _hasLoadedArchivedProjectNotes = result.IsComplete;
            _archivedProjectNotes = result.Entries.ToList();
            ReportIncompleteSweep(result);
            UpdateLoading();
            RenderList();
        }

        private static async Task<List<Project>> LoadActiveProjectsAsync(API.FocusBearAPIClient client)
        {
            var all = new List<Project>();
            var known = new HashSet<string>();
            for (int page = 1; page <= FocusBear.Core.Notes.ProjectsPage.MaxPages; page++)
            {
                var result = NotesWire.ReadProjectsPage(await client.GetActiveProjectsPage(page, FocusBear.Core.Notes.ProjectsPage.Take));
                if (result == null) return all.Count == 0 ? null : all;
                all.AddRange(result.Items.Where(p => known.Add(p.Id)));
                if (!result.HasNextPage || result.Items.Count == 0) break;
            }
            return all;
        }

        /// <summary>Runs Core's rate-limited sweep on the UI thread: fetches are awaited, retries use a dispatcher timer.</summary>
        private Task<ProjectNotesSweepResult> SweepAsync(API.FocusBearAPIClient client, List<Project> projects, bool archived)
        {
            var done = new TaskCompletionSource<ProjectNotesSweepResult>();
            var runner = new ProjectNotesSweepRunner(
                (projectId, completion) => FetchProjectNotes(client, projectId, archived, completion),
                (delay, work) =>
                {
                    var timer = new DispatcherTimer { Interval = delay };
                    timer.Tick += (s, e) => { timer.Stop(); work(); };
                    timer.Start();
                });
            runner.Run(projects, r => done.TrySetResult(r));
            return done.Task;
        }

        private static async void FetchProjectNotes(API.FocusBearAPIClient client, string projectId, bool archived, Action<IList<ProjectNote>, int> completion)
        {
            // Every page: the endpoint pages at 20 by default, and page 1 alone hid a project's later notes.
            ProjectNotesFetch fetched = await ProjectNotesPaging.FetchAll(async page =>
            {
                API.ApiResponse response = await client.GetProjectNotes(projectId, archived, page);
                return Tuple.Create(response.Status, response.Body);
            });
            completion(fetched.Notes, fetched.Status);
        }

        /// <summary>A quietly short list is indistinguishable from having no notes — the bug this replaces — so say so.</summary>
        private void ReportIncompleteSweep(ProjectNotesSweepResult result)
        {
            if (result.IsComplete) return;
            LogWriter.LogWrite("Notes: project notes sweep incomplete - " + result.FailedProjectIds.Count + " project(s) unanswered");
            if (_scope.IncludesProjectNotes()) ShowStatus(_sRequestFailed);
        }

        private async void OpenProjectNote(ProjectNoteEntry entry)
        {
            _openProjectNote = entry;
            ShowProjectNote(entry);
            ProjectNotePanel.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Collapsed;
            ProjectNoteLoading.Visibility = Visibility.Visible;
            StartProjectNoteEditor(entry);

            // The list rows carry no body; fetch the note itself.
            ProjectNote full = NotesWire.ReadProjectNote(await new API.FocusBearAPIClient().GetProjectNote(entry.ProjectId, entry.Id));
            if (_openProjectNote == null || _openProjectNote.Id != entry.Id) return;
            ProjectNoteLoading.Visibility = Visibility.Collapsed;
            if (full == null) { ShowStatus(_sRequestFailed); return; }
            _openProjectNote = entry.With(full);
            _projectNotes = ProjectNoteEntry.Applying(full, _projectNotes);
            _archivedProjectNotes = ProjectNoteEntry.Applying(full, _archivedProjectNotes);
            ShowProjectNote(_openProjectNote);
        }

        private void ShowProjectNote(ProjectNoteEntry entry)
        {
            if (!txtProjectNoteTitle.IsKeyboardFocusWithin) txtProjectNoteTitle.Text = string.IsNullOrWhiteSpace(entry.Title) ? _sUntitledNote : entry.Title;
            lblProjectNoteProject.Text = "👥  " + entry.ProjectName;
            DateTime? edited = ToDoDates.Parse(entry.SortDateString);
            lblProjectNoteEdited.Text = edited.HasValue ? _sLastEdited.Replace("<DATE>", NoteRow.FormatDate(edited)) : "";
            fdProjectNote.Document = NoteFlowDocument.FromDocument(ProseMirrorNote.ToDocument(entry.Note.Content), EditorFontSize);
            fdProjectNote.Document.Foreground = (Brush)FindResource("ForegroundColor");
            fdProjectNote.Document.FontFamily = FontFamily;

            bool archived = NotesNavigation.FooterAction(entry.Note) == NotesFooterAction.RestoreNote;
            lblArchiveNote.Text = archived ? "↩  " + _sRestoreNote : "🗄  " + _sArchiveNote;
            btnArchiveNote.ToolTip = archived ? _sRestoreNoteHelp : _sArchiveNoteHelp;
            btnArchiveNote.IsEnabled = !_isArchivingNote;
        }

        private ProjectNoteEditor _projectNoteEditor;

        /// <summary>
        /// Shared notes are edited live, as on the Mac and the web: the editor loads the note's Yjs state and
        /// syncs with everyone else who has it open. The read-only copy stays up until the editor holds the note,
        /// and comes back if the editor cannot (no WebView2, unsafe content, lost access).
        /// </summary>
        private void StartProjectNoteEditor(ProjectNoteEntry entry)
        {
            StopProjectNoteEditor();
            var identity = new ProjectNoteIdentity(AppSettings.GetInstance.userAccount?.userID, entry.ProjectId, entry.Id);
            if (!identity.IsValid || !identity.IsSafeForPath) return;
            bool dark = string.Equals(AppSettings.GetInstance.sTheme, "Dark", StringComparison.OrdinalIgnoreCase);
            var card = TryFindResource("TrayCardBg") as SolidColorBrush;
            var editor = new ProjectNoteEditor(identity, () => AppSettings.GetInstance.userAccount?.userID, dark, card?.Color);
            editor.ShowingNoteChanged += (s, e) => { if (editor == _projectNoteEditor) ShowEditorOrCopy(); };
            _projectNoteEditor = editor;
            ProjectNoteEditorHost.Content = editor;
            ShowEditorOrCopy();
        }

        private void ShowEditorOrCopy()
        {
            // The editor sits in the tree from the start (WebView2 needs a window to initialize); it is just hidden.
            bool live = _projectNoteEditor != null && _projectNoteEditor.IsShowingNote;
            ProjectNoteEditorHost.Visibility = _projectNoteEditor != null ? Visibility.Visible : Visibility.Collapsed;
            ProjectNoteEditorHost.Opacity = live ? 1 : 0;
            ProjectNoteEditorHost.IsHitTestVisible = live;
            fdProjectNote.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
            lblProjectNoteWebOnly.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
        }

        private void StopProjectNoteEditor()
        {
            var editor = _projectNoteEditor;
            _projectNoteEditor = null;
            if (editor == null) return;
            ProjectNoteEditorHost.Content = null;
            editor.Close();   // finishes its final save on its own
            ShowEditorOrCopy();
        }

        private void CloseProjectNote()
        {
            if (_openProjectNote == null) return;
            StopProjectNoteEditor();
            _openProjectNote = null;
            ProjectNotePanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            fdProjectNote.Document = null;
            RenderList();
        }

        private void btnBackToNotes_Click(object sender, RoutedEventArgs e) { CloseProjectNote(); }

        private void btnEditOnWeb_Click(object sender, RoutedEventArgs e)
        {
            if (_openProjectNote == null) return;
            try { Process.Start(new ProcessStartInfo(ProjectNoteLinks.WebUrl(_openProjectNote.ProjectId, _openProjectNote.Id)) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("Notes edit on web ex - " + ex.Message); }
        }

        /// <summary>
        /// Archive or restore the open note. Nothing is decided locally: the note moves between the two lists
        /// only once the server confirms, so Windows never shows a state the web and the Mac disagree with.
        /// </summary>
        private async void btnArchiveNote_Click(object sender, RoutedEventArgs e)
        {
            var entry = _openProjectNote;
            if (entry == null || _isArchivingNote) return;
            bool archive = !entry.Note.IsArchived;
            _isArchivingNote = true;
            btnArchiveNote.IsEnabled = false;
            ProjectNote saved = NotesWire.ReadProjectNote(await new API.FocusBearAPIClient().SetProjectNoteArchived(entry.ProjectId, entry.Id, archive));
            _isArchivingNote = false;
            btnArchiveNote.IsEnabled = true;
            if (saved == null) { ShowStatus(archive ? _sCouldNotArchiveNote : _sCouldNotRestoreNote); return; }
            var lists = NotesFeed.Moved(entry, saved, archive, _projectNotes, _archivedProjectNotes);
            _projectNotes = lists.Active;
            _archivedProjectNotes = lists.Archive;
            ShowStatus(archive ? _sNoteArchived : _sNoteRestored);
            if (NotesNavigation.AfterArchiving(archive) == ArchivedNoteDisposition.StepBackToList)
            {
                CloseProjectNote();
            }
            else if (_openProjectNote != null && _openProjectNote.Id == saved.Id)
            {
                _openProjectNote = entry.With(saved);
                ShowProjectNote(_openProjectNote);
            }
        }

        // ================================================================== shared notes: create, rename, delete

        private void btnNewProjectNote_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadProjectNotesAsync(false);
            NewProjectNoteProject.SetOptions(_projects.Select(p => (p.Id, p.Name)));
            NewProjectNoteProject.SelectedId = null;
            txtNewProjectNoteTitle.Text = "";
            bool hasProjects = _projects.Count > 0;
            lblNoProjects.Text = _isLoadingProjectNotes ? _sLoading : _sNoExistingProjects;
            lblNoProjects.Visibility = hasProjects ? Visibility.Collapsed : Visibility.Visible;
            NewProjectNoteFields.Visibility = hasProjects ? Visibility.Visible : Visibility.Collapsed;
            NewProjectNoteCard.Visibility = Visibility.Visible;
            btnNewProjectNote.IsEnabled = false;
            UpdateCreateProjectNoteButton();
            if (hasProjects) NewProjectNoteProject.Focus();
        }

        private void btnCancelNewProjectNote_Click(object sender, RoutedEventArgs e) { CloseNewProjectNoteCard(); }

        private void CloseNewProjectNoteCard()
        {
            NewProjectNoteCard.Visibility = Visibility.Collapsed;
            btnNewProjectNote.IsEnabled = true;
        }

        private void txtNewProjectNoteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblNewProjectNoteTitlePlaceholder.Visibility = string.IsNullOrEmpty(txtNewProjectNoteTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtNewProjectNoteTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnCreateProjectNote.IsEnabled) { e.Handled = true; btnCreateProjectNote_Click(sender, e); }
        }

        /// <summary>Create stays disabled until a project is actually chosen: nothing is preselected on purpose.</summary>
        private void UpdateCreateProjectNoteButton()
        {
            btnCreateProjectNote.IsEnabled = NewProjectNote.CanCreate(NewProjectNoteProject.SelectedId) && !_isCreatingProjectNote;
            lblCreateProjectNote.Text = _isCreatingProjectNote ? _sSaving : _sCreateNote;
        }

        /// <summary><c>POST /projects/:id/notes { title }</c>, then open the new note, attached to the project the user picked.</summary>
        private async void btnCreateProjectNote_Click(object sender, RoutedEventArgs e)
        {
            string projectId = NewProjectNoteProject.SelectedId;
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (!NewProjectNote.CanCreate(projectId) || project == null || _isCreatingProjectNote) return;
            _isCreatingProjectNote = true;
            UpdateCreateProjectNoteButton();
            string title = NewProjectNote.ResolvedTitle(txtNewProjectNoteTitle.Text, _sUntitledNote);
            ProjectNote created = NotesWire.ReadProjectNote(await new API.FocusBearAPIClient().CreateProjectNote(projectId, title));
            _isCreatingProjectNote = false;
            UpdateCreateProjectNoteButton();
            if (created == null) { ShowStatus(_sCouldNotCreateProjectNote); return; }
            var entry = ProjectNoteEntry.Created(created, project);
            _projectNotes.Insert(0, entry);
            CloseNewProjectNoteCard();
            ShowStatus(_sNoteCreated);
            RenderList();
            OpenProjectNote(entry);
        }

        private void txtProjectNoteTitle_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _isRenamingProjectNote = true;
            txtProjectNoteTitle.BorderBrush = (Brush)FindResource("TrayCardHoverBorder");
        }

        /// <summary>
        /// Tab in a note editor moves focus on, so the editor is not a keyboard trap (AcceptsTab is off). In a bulleted
        /// or numbered list, Tab and Shift+Tab indent and outdent the item instead, as they do in the Mac editor.
        /// </summary>
        private static void NoteEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab || !(sender is RichTextBox box)) return;
            if ((Keyboard.Modifiers & ~ModifierKeys.Shift) != ModifierKeys.None) return;
            var paragraph = box.CaretPosition?.Paragraph;
            if (paragraph == null || !(paragraph.Parent is System.Windows.Documents.ListItem)) return;
            var command = (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                ? System.Windows.Documents.EditingCommands.DecreaseIndentation
                : System.Windows.Documents.EditingCommands.IncreaseIndentation;
            if (command.CanExecute(null, box))
            {
                command.Execute(null, box);
                e.Handled = true;
            }
        }

        private void txtProjectNoteTitle_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Keyboard.ClearFocus();   // leaving saves
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(txtProjectNoteTitle), btnBackToNotes);
            }
            else if (e.Key == Key.Escape)
            {
                // Escape is the discard route: edit mode ends first, so leaving afterwards saves nothing.
                e.Handled = true;
                _isRenamingProjectNote = false;
                if (_openProjectNote != null) txtProjectNoteTitle.Text = string.IsNullOrWhiteSpace(_openProjectNote.Title) ? _sUntitledNote : _openProjectNote.Title;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(txtProjectNoteTitle), btnBackToNotes);
                Keyboard.ClearFocus();
            }
        }

        /// <summary>
        /// Leaving the title saves what was typed (Back used to drop a typed-but-unentered title). Only the
        /// title: the body is a collaborative document written through the web editor, so this screen and
        /// that editor never write the same field. Blank or unchanged is not a rename.
        /// </summary>
        private async void txtProjectNoteTitle_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            txtProjectNoteTitle.BorderBrush = Brushes.Transparent;
            var entry = _openProjectNote;
            string title = InlineRenameDismissal.OnDismiss(txtProjectNoteTitle.Text, entry != null ? entry.Title : null, _isRenamingProjectNote);
            _isRenamingProjectNote = false;
            if (entry == null || title == null) return;
            ProjectNote saved = NotesWire.ReadProjectNote(await new API.FocusBearAPIClient().RenameProjectNote(entry.ProjectId, entry.Id, title));
            if (saved == null)
            {
                ShowStatus(_sCouldNotRenameNote);
                if (_openProjectNote != null && _openProjectNote.Id == entry.Id) txtProjectNoteTitle.Text = entry.Title;
                return;
            }
            // Patch the title in place: the rename response carries no body, and the list rows never had one.
            var renamed = new ProjectNote
            {
                Id = entry.Note.Id, ProjectId = entry.Note.ProjectId, CreatedBy = entry.Note.CreatedBy, Title = saved.Title,
                Content = entry.Note.Content, CreatedAt = entry.Note.CreatedAt, UpdatedAt = saved.UpdatedAt ?? entry.Note.UpdatedAt, ArchivedAt = entry.Note.ArchivedAt,
            };
            _projectNotes = ProjectNoteEntry.Applying(renamed, _projectNotes);
            _archivedProjectNotes = ProjectNoteEntry.Applying(renamed, _archivedProjectNotes);
            if (_openProjectNote != null && _openProjectNote.Id == entry.Id)
            {
                _openProjectNote = entry.With(renamed);
                ShowProjectNote(_openProjectNote);
            }
            ShowStatus(_sNoteRenamed);
        }

        private async void btnDeleteProjectNote_Click(object sender, RoutedEventArgs e)
        {
            var entry = _openProjectNote;
            if (entry == null) return;
            var confirm = new FBMessageBox(_sDeleteProjectNoteConfirm, _sDeleteNoteTitle, MessageBoxButton.YesNo, _sDelete, _sCancel);
            confirm.Owner = Window.GetWindow(this);
            confirm.ShowDialog();
            if (confirm.btnClicked != MessageBoxResult.Yes) return;
            bool deleted = await new API.FocusBearAPIClient().DeleteProjectNote(entry.ProjectId, entry.Id);
            if (!deleted) { ShowStatus(_sCouldNotDeleteProjectNote); return; }
            _projectNotes = _projectNotes.Where(n => n.Id != entry.Id).ToList();
            _archivedProjectNotes = _archivedProjectNotes.Where(n => n.Id != entry.Id).ToList();
            CloseProjectNote();
            ShowStatus(_sProjectNoteDeleted);
        }

        // ================================================================== brain dump

        private void SetBrainDumpVisible(bool visible)
        {
            BrainDumpCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            btnShowBrainDump.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnHideBrainDump_Click(object sender, RoutedEventArgs e)
        {
            FlushPendingBrainDumpSave();
            SetBrainDumpVisible(false);
            RegistryManager.SetNotesBrainDumpVisible(false);
        }

        private void btnShowBrainDump_Click(object sender, RoutedEventArgs e)
        {
            SetBrainDumpVisible(true);
            RegistryManager.SetNotesBrainDumpVisible(true);
        }

        private void AdoptBrainDump(IEnumerable<Note> listed)
        {
            Note latest = _brainDump.Observe(listed);
            if (latest != null) ApplyBrainDump(latest);
        }

        /// <summary>One larger page, once per session, so the live brain dump is found even when it has fallen off page 1.</summary>
        private async Task ScanForBrainDumpOnceAsync()
        {
            if (_hasScannedForBrainDump) return;
            _hasScannedForBrainDump = true;
            NotesPage result = NotesWire.ReadPage(await new API.FocusBearAPIClient().GetNotesPage(NotesPaging.FirstPage, NotesPaging.BrainDumpScanTake));
            if (result != null) AdoptBrainDump(result.Data);
        }

        private void ApplyBrainDump(Note note)
        {
            string body = note.Body ?? "";
            // Unsaved local edits win: never clobber text the user is still typing.
            bool hasUnsavedEdits = _brainDumpSaveTimer.IsEnabled || _isSavingBrainDump || CurrentBrainDumpHtml() != _lastSavedBrainDumpBody;
            _lastSavedBrainDumpBody = body;
            if (hasUnsavedEdits) return;
            _suppressBrainDumpChange = true;
            try { NoteFlowDocument.Load(rtbBrainDump, body, EditorFontSize); }
            finally { _suppressBrainDumpChange = false; }
            // The editor's own export is the baseline: HTML from the web editor rarely round-trips byte for
            // byte, and a difference nobody typed must not count as an edit.
            _lastSavedBrainDumpBody = CurrentBrainDumpHtml();
            UpdatePlaceholder(rtbBrainDump, lblBrainDumpPlaceholder);

            // Text that never reached the server (a failed save, then a quit or a closed screen) comes back, and goes.
            string draft = Draft.Read();
            if (BrainDumpDraft.ShouldRestore(draft, _lastSavedBrainDumpBody))
            {
                LogWriter.LogWrite("BrainDump: restoring unsent text from the local draft");
                _suppressBrainDumpChange = true;
                try { NoteFlowDocument.Load(rtbBrainDump, draft, EditorFontSize); }
                finally { _suppressBrainDumpChange = false; }
                UpdatePlaceholder(rtbBrainDump, lblBrainDumpPlaceholder);
                _brainDumpSaveTimer.Stop();
                _brainDumpSaveTimer.Interval = NotesTiming.BrainDumpSaveDelay;
                _brainDumpSaveTimer.Start();
            }
            else if (draft != null)
            {
                Draft.Clear();   // the server already has it
            }
        }

        private string CurrentBrainDumpHtml()
        {
            return NoteFlowDocument.ToHtml(rtbBrainDump.Document);
        }

        private void rtbBrainDump_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder(rtbBrainDump, lblBrainDumpPlaceholder);
            if (_suppressBrainDumpChange) return;
            _brainDumpSaveTimer.Stop();
            _brainDumpSaveTimer.Interval = NotesTiming.BrainDumpSaveDelay;
            if (BrainDumpSaveRules.ShouldSchedule(CurrentBrainDumpHtml(), _lastSavedBrainDumpBody)) _brainDumpSaveTimer.Start();
        }

        private void rtbBrainDump_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            FlushPendingBrainDumpSave();
        }

        private async void BrainDumpSaveTimer_Tick(object sender, EventArgs e)
        {
            _brainDumpSaveTimer.Stop();
            await SaveBrainDumpAsync();
        }

        /// <summary>
        /// Raised once the server has a brain dump saved from this screen. The focus session screen shows the same
        /// note in its side panel and reads it again; a save flushed on leaving this screen lands after that
        /// screen has already appeared, so re-reading on appearance alone would miss it.
        /// </summary>
        internal static event Action BrainDumpSaved;

        private void FlushPendingBrainDumpSave()
        {
            if (_isSavingBrainDump) { _flushWhenSaved = true; return; }
            if (!_brainDumpSaveTimer.IsEnabled) return;
            _brainDumpSaveTimer.Stop();
            _ = SaveBrainDumpAsync();
        }

        /// <summary>Web <c>BrainDumpNote.saveBrainDump</c>: upsert by id when known; clearing sticks; never create an empty one.</summary>
        private async Task SaveBrainDumpAsync()
        {
            // One save at a time. A second one sent before the first brain dump's create had answered
            // created a second brain dump; this one's turn comes when that save lands (NextSave below).
            if (_isSavingBrainDump) return;
            string html = CurrentBrainDumpHtml();
            Note existing = _brainDump.Adopted;
            if (!BrainDumpSaveRules.ShouldCommit(html, _lastSavedBrainDumpBody, existing != null)) return;
            _isSavingBrainDump = true;
            Draft.Write(html);   // kept until the server confirms it
            lblBrainDumpSaving.Visibility = Visibility.Visible;
            string body = await new API.FocusBearAPIClient().UpsertNote(NoteUpsertBody.ForBrainDump(_brainDump.IdForSave(), _sBrainDumpTitle, html));
            _isSavingBrainDump = false;
            lblBrainDumpSaving.Visibility = Visibility.Collapsed;
            Note saved = NotesWire.ReadNote(body);
            if (saved == null) ShowStatus(_sRequestFailed);
            else
            {
                _brainDump.Saved(saved);
                _lastSavedBrainDumpBody = html;
                if (CurrentBrainDumpHtml() == html) Draft.Clear();   // typing that carried on is still unsent
                try { BrainDumpSaved?.Invoke(); }
                catch (Exception ex) { LogWriter.LogWrite("BrainDumpSaved handler ex - " + ex.Message); }
            }
            // A failure is retried — the text exists only in this editor, and leaving the screen flushes only a
            // save that is scheduled; typing that carried on while the request was out is a new edit.
            _retryingFailedSave = saved == null;
            bool flush = _flushWhenSaved;
            _flushWhenSaved = false;
            TimeSpan? next = BrainDumpSaveRules.NextSave(saved != null, html, CurrentBrainDumpHtml(), flush);
            if (next == TimeSpan.Zero)
            {
                _brainDumpSaveTimer.Stop();
                _ = SaveBrainDumpAsync();
            }
            else if (next.HasValue && !_brainDumpSaveTimer.IsEnabled)
            {
                _brainDumpSaveTimer.Interval = next.Value;
                _brainDumpSaveTimer.Start();
            }
        }

        // ================================================================== list clicks

        private void lvNotes_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Clicks on the card's own buttons are theirs.
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source != lvNotes)
            {
                if (source is ButtonBase) return;
                source = source is Visual || source is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            var container = ItemsControl.ContainerFromElement(lvNotes, e.OriginalSource as DependencyObject) as ListBoxItem;
            var row = container != null ? container.DataContext as NoteRow : null;
            if (row == null) return;
            if (row.IsProject) OpenProjectNote(row.Item.Project);
            else OpenEditor(row.Item.Personal);
            lvNotes.SelectedItem = null;
        }

        private void btnEditNote_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) != null ? ((FrameworkElement)sender).DataContext as NoteRow : null;
            if (row != null && row.IsPersonal) OpenEditor(row.Item.Personal);
        }

        // ================================================================== create / edit

        private void btnNewNote_Click(object sender, RoutedEventArgs e) { OpenEditor(null); }

        private void OpenEditor(Note note)
        {
            _editingNote = note;
            lblEditorTitle.Text = note == null ? _sCreateNoteTitle : _sEditNoteTitle;
            txtNoteTitle.Text = note != null ? note.Title : "";
            NoteFlowDocument.Load(rtbNoteBody, note != null ? note.Body : "", EditorFontSize);
            // The baseline an untouched body is recognised by: the editor's own export, not the stored HTML.
            _editingOpeningBodyHtml = NoteFlowDocument.ToHtml(rtbNoteBody.Document);
            UpdatePlaceholder(rtbNoteBody, lblNoteBodyPlaceholder);
            _selectedTags.Clear();
            if (note != null) foreach (var tag in note.TagList) _selectedTags.Add(tag);
            txtNewTag.Text = "";
            RefreshExistingTagSuggestions();
            if (note != null && !string.IsNullOrEmpty(note.ActivityName))
            {
                lblLinkedHabit.Text = (string.IsNullOrEmpty(note.ActivityEmoji) ? "" : note.ActivityEmoji + " ") + _sLinkedToHabit + ": " + note.ActivityName;
                lblLinkedHabit.Visibility = Visibility.Visible;
            }
            else
            {
                lblLinkedHabit.Visibility = Visibility.Collapsed;
            }
            UpdateSaveButton();
            EditorPanel.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Collapsed;
            txtNoteTitle.Focus();
        }

        private void CloseEditor()
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            _editingNote = null;
        }

        private void btnCancelNote_Click(object sender, RoutedEventArgs e) { CloseEditor(); }

        private void txtNoteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblNoteTitlePlaceholder.Visibility = string.IsNullOrEmpty(txtNoteTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
            UpdateSaveButton();
        }

        private void rtbNoteBody_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholder(rtbNoteBody, lblNoteBodyPlaceholder); }

        private void UpdateSaveButton()
        {
            btnSaveNote.IsEnabled = !string.IsNullOrWhiteSpace(txtNoteTitle.Text) && !_isSavingNote;
            lblSaveNote.Text = _isSavingNote ? _sSaving : _sSave;
        }

        /// <summary>Web <c>NotesDrawer.handleSave</c> → <c>createOrUpdateNote</c>: PUT, then refresh and close on success.</summary>
        private async void btnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNoteTitle.Text) || _isSavingNote) return;
            Note editing = _editingNote;
            bool isUpdate = editing != null;
            _isSavingNote = true;
            UpdateSaveButton();
            // An untouched body goes back as stored: the editor cannot hold the web's tables, images or highlights.
            string body = NoteUpsertBody.BodyToSave(NoteFlowDocument.ToHtml(rtbNoteBody.Document), _editingOpeningBodyHtml, isUpdate ? editing.Body : null);
            var payload = NoteUpsertBody.ForNote(isUpdate ? editing.Id : null, txtNoteTitle.Text, body,
                                                 _selectedTags.ToList(), isUpdate ? editing.CompletedActivityId : null);
            Note saved = NotesWire.ReadNote(await new API.FocusBearAPIClient().UpsertNote(payload));
            _isSavingNote = false;
            UpdateSaveButton();
            if (saved == null) { ShowStatus(isUpdate ? _sCouldNotUpdateNote : _sCouldNotCreateNote); return; }
            CloseEditor();
            ShowStatus(isUpdate ? _sNoteUpdated : _sNoteCreated);
            await RefreshAfterWriteAsync();
        }

        // ================================================================== tags in the editor

        private void RefreshExistingTagSuggestions()
        {
            _existingTagSuggestions.Clear();
            foreach (var tag in NoteTagPicker.Suggestions(_userTags, _selectedTags)) _existingTagSuggestions.Add(tag);
            ExistingTagsPanel.Visibility = _existingTagSuggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddTagFromInput()
        {
            string typed = txtNewTag.Text;
            txtNewTag.Text = "";
            var selected = _selectedTags.ToList();
            if (NoteTagPicker.Add(typed, _userTags, selected, n => _random.Next(n)))
            {
                _selectedTags.Clear();
                foreach (var tag in selected) _selectedTags.Add(tag);
            }
            RefreshExistingTagSuggestions();
        }

        private void btnAddTag_Click(object sender, RoutedEventArgs e) { AddTagFromInput(); }

        private void txtNewTag_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AddTagFromInput();
        }

        private void txtNewTag_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblNewTagPlaceholder.Visibility = string.IsNullOrEmpty(txtNewTag.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnRemoveTag_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement) != null ? ((FrameworkElement)sender).DataContext as NoteTag : null;
            if (tag == null) return;
            var match = _selectedTags.FirstOrDefault(t => t.Text == tag.Text);
            if (match != null) _selectedTags.Remove(match);
            RefreshExistingTagSuggestions();
        }

        private void btnExistingTag_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement) != null ? ((FrameworkElement)sender).DataContext as NoteTag : null;
            if (tag == null) return;
            var selected = _selectedTags.ToList();
            if (NoteTagPicker.Add(tag.Text, _userTags, selected, n => _random.Next(n)))
            {
                _selectedTags.Clear();
                foreach (var t in selected) _selectedTags.Add(t);
            }
            RefreshExistingTagSuggestions();
        }

        // ================================================================== delete

        private async void btnDeleteNote_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) != null ? ((FrameworkElement)sender).DataContext as NoteRow : null;
            if (row == null || !row.IsPersonal) return;
            Note note = row.Item.Personal;
            var confirm = new FBMessageBox(_sDeleteConfirmation, _sDeleteNoteTitle, MessageBoxButton.YesNo, _sDelete, _sCancel);
            confirm.Owner = Window.GetWindow(this);
            confirm.ShowDialog();
            if (confirm.btnClicked != MessageBoxResult.Yes) return;
            bool success = await new API.FocusBearAPIClient().DeleteNote(note.Id);
            if (!success) { ShowStatus(_sRequestFailed); return; }
            _notes.RemoveAll(n => n.Id == note.Id);
            if (_searchResults != null) _searchResults.RemoveAll(n => n.Id == note.Id);
            RenderList();
            ShowStatus(_sNoteDeleted);
            await RefreshAfterWriteAsync();
        }

        // ================================================================== helpers

        private static void UpdatePlaceholder(RichTextBox editor, TextBlock placeholder)
        {
            placeholder.Visibility = NoteFlowDocument.IsEmpty(editor.Document) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowStatus(string message)
        {
            lblStatus.Text = message;
            lblStatus.Visibility = Visibility.Visible;
            lblProjectNoteStatus.Text = message;
            lblProjectNoteStatus.Visibility = Visibility.Visible;
            _statusTimer.Stop();
            _statusTimer.Start();
        }
    }
}
