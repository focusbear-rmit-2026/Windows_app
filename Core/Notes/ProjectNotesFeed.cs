using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// Lets the Notes screen list a user's project ("shared") notes next to their personal ones. Port of
    /// the Mac's <c>Notes/ProjectNotesFeed.swift</c>: only the pure logic — which notes are listed, in
    /// what order, how every project's notes are fetched without being rate limited.
    /// </summary>
    public sealed class ProjectNoteEntry
    {
        public ProjectNoteEntry(string projectId, string projectName, ProjectNote note)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            Note = note;
        }

        public string ProjectId { get; }
        public string ProjectName { get; }
        public ProjectNote Note { get; }

        public string Id => Note.Id;
        public string Title => Note.Title;

        /// <summary>Ordered by when the note last changed, falling back to when it was created.</summary>
        public string SortDateString => Note.UpdatedAt ?? Note.CreatedAt;

        public ProjectNoteEntry With(ProjectNote note) => new ProjectNoteEntry(ProjectId, ProjectName, note);

        /// <summary>Flattens a fetch (project id → its notes) into entries. Projects with no notes drop out.</summary>
        public static List<ProjectNoteEntry> Entries(IDictionary<string, IList<ProjectNote>> notesByProjectId, IEnumerable<Project> projects)
        {
            var entries = new List<ProjectNoteEntry>();
            foreach (var project in projects)
            {
                if (!notesByProjectId.TryGetValue(project.Id, out var notes) || notes == null) continue;
                entries.AddRange(notes.Select(n => new ProjectNoteEntry(project.Id, project.Name, n)));
            }
            return entries;
        }

        /// <summary>
        /// A note just created in <paramref name="project"/>. The chosen project is authoritative — it is what
        /// the row is grouped under and what the web link points at — whether or not the server echoed
        /// <c>project_id</c> back.
        /// </summary>
        public static ProjectNoteEntry Created(ProjectNote note, Project project) => new ProjectNoteEntry(project.Id, project.Name, note);

        /// <summary>
        /// Folds a fully fetched note (list rows carry no body) into the entries. Only that note changes, and
        /// it keeps the project it was listed under.
        /// </summary>
        public static List<ProjectNoteEntry> Applying(ProjectNote full, IEnumerable<ProjectNoteEntry> entries) =>
            entries.Select(e => e.Id == full.Id ? e.With(full) : e).ToList();
    }

    /// <summary>
    /// Which notes the screen lists. <see cref="Archived"/> is a view of shared notes rather than a fourth
    /// kind of note: only project notes can be archived (a personal note is deleted), so it belongs on the
    /// control that already answers "which notes am I looking at".
    /// </summary>
    public enum NotesScope
    {
        All,
        Personal,
        Project,
        Archived,
    }

    public static class NotesScopeRules
    {
        public static bool IncludesPersonalNotes(this NotesScope scope) => scope == NotesScope.All || scope == NotesScope.Personal;
        public static bool IncludesProjectNotes(this NotesScope scope) => scope != NotesScope.Personal;
        /// <summary>Archived notes are hidden everywhere else — that is what archiving is for.</summary>
        public static bool ShowsArchivedNotes(this NotesScope scope) => scope == NotesScope.Archived;

        /// <summary>
        /// Worth reopening the screen on. Landing back in the archive later would read as every note having
        /// vanished, so it is a momentary view, not a preference.
        /// </summary>
        public static bool IsWorthPersisting(this NotesScope scope) => scope != NotesScope.Archived;

        /// <summary>The raw values the Mac persists, so a scope reads the same wherever it was saved.</summary>
        public static string RawValue(this NotesScope scope)
        {
            switch (scope)
            {
                case NotesScope.Personal: return "personal";
                case NotesScope.Project: return "project";
                case NotesScope.Archived: return "archived";
                default: return "all";
            }
        }

        public static NotesScope Parse(string raw)
        {
            switch (raw)
            {
                case "personal": return NotesScope.Personal;
                case "project": return NotesScope.Project;
                case "archived": return NotesScope.Archived;
                default: return NotesScope.All;
            }
        }

        /// <summary>The scope a saved value reopens on: anything unknown, or the archive, is All.</summary>
        public static NotesScope Restore(string raw)
        {
            var scope = Parse(raw);
            return scope.IsWorthPersisting() ? scope : NotesScope.All;
        }
    }

    /// <summary>A row: one of the user's own notes, or a note inside one of their projects.</summary>
    public sealed class NotesListItem
    {
        /// <summary>Ids come from two tables, so they are namespaced before a list sees them.</summary>
        public const string PersonalIdPrefix = "personal-";
        public const string ProjectIdPrefix = "project-";

        private NotesListItem(Note personal, ProjectNoteEntry project)
        {
            Personal = personal;
            Project = project;
        }

        public static NotesListItem ForPersonal(Note note) => new NotesListItem(note, null);
        public static NotesListItem ForProject(ProjectNoteEntry entry) => new NotesListItem(null, entry);

        public Note Personal { get; }
        public ProjectNoteEntry Project { get; }
        public bool IsProject => Project != null;

        public string Id => IsProject ? ProjectIdPrefix + Project.Id : PersonalIdPrefix + Personal.Id;

        /// <summary>
        /// Both kinds share one timeline: a personal note by the date its row shows
        /// (<c>logged_at ?? created_at</c>), a project note by when it last changed.
        /// </summary>
        public DateTime? SortDate => ToDoDates.Parse(IsProject ? Project.SortDateString : Personal.DisplayDateString);
    }

    public static class NotesFeed
    {
        /// <summary>
        /// Builds the list from the two sources. <paramref name="personal"/> is already filtered (brain dump
        /// excluded, tag filter and server-side search applied); project notes are filtered here because they
        /// are fetched whole and their rows carry no body to search.
        ///
        /// A tag filter excludes project notes entirely: tags are a personal-notes concept, so "only notes
        /// tagged phd" can never mean a project note. Filtering to one project drops personal notes for the
        /// same reason — they belong to no project.
        /// </summary>
        public static List<NotesListItem> Items(IEnumerable<Note> personal, IEnumerable<ProjectNoteEntry> projectNotes, NotesScope scope,
                                                string searchQuery, bool isTagFilterActive, string projectFilter = null)
        {
            var items = new List<NotesListItem>();
            if (scope.IncludesPersonalNotes() && projectFilter == null)
                items.AddRange(personal.Select(NotesListItem.ForPersonal));
            if (scope.IncludesProjectNotes() && !isTagFilterActive)
            {
                var entries = projectNotes.Where(e => Matches(e.Title, searchQuery));
                if (projectFilter != null) entries = entries.Where(e => e.ProjectId == projectFilter);
                items.AddRange(entries.Select(NotesListItem.ForProject));
            }
            items.Sort(NewerFirst);
            return items;
        }

        /// <summary>
        /// Which project notes the current scope draws on. Active and archived notes are two separate fetches,
        /// and each set is still checked against the note's own <c>archived_at</c>: a server that does not
        /// understand the archive filter answers both with the same active notes, and the archive must then be
        /// honestly empty rather than present live notes as filed away.
        /// </summary>
        public static List<ProjectNoteEntry> ProjectNotes(IEnumerable<ProjectNoteEntry> active, IEnumerable<ProjectNoteEntry> archived, NotesScope scope) =>
            scope.ShowsArchivedNotes()
                ? archived.Where(e => e.Note.IsArchived).ToList()
                : active.Where(e => !e.Note.IsArchived).ToList();

        /// <summary>
        /// Where a note lands once archived or restored: out of one list and into the other, never both. The
        /// position is only so it is somewhere definite — the screen sorts by last edit, and archiving is not
        /// an edit (the server leaves <c>updated_at</c> alone).
        /// </summary>
        public static (List<ProjectNoteEntry> Active, List<ProjectNoteEntry> Archive) Moved(ProjectNoteEntry entry, ProjectNote saved, bool archived,
                                                                                           IEnumerable<ProjectNoteEntry> active, IEnumerable<ProjectNoteEntry> archive)
        {
            var moved = entry.With(saved);
            var remainingActive = active.Where(e => e.Id != saved.Id).ToList();
            var remainingArchive = archive.Where(e => e.Id != saved.Id).ToList();
            if (archived) remainingArchive.Insert(0, moved);
            else remainingActive.Insert(0, moved);
            return (remainingActive, remainingArchive);
        }

        /// <summary>
        /// Projects that actually have a note in view, newest note first, for the filter control. Offering a
        /// project with nothing in it would just be a way to empty the list.
        /// </summary>
        public static List<(string Id, string Name)> FilterableProjects(IEnumerable<ProjectNoteEntry> entries)
        {
            var seen = new HashSet<string>();
            return entries
                .OrderByDescending(e => e.SortDateString ?? "", StringComparer.Ordinal)
                .Where(e => seen.Add(e.ProjectId))
                .Select(e => (e.ProjectId, e.ProjectName))
                .ToList();
        }

        /// <summary>
        /// The project filter the list may actually apply: only while the filter control is on screen (shared
        /// notes in view, more than one project to choose between) and the chosen project is still one of its
        /// options. The choice itself is kept across scope changes, but a filter the user cannot see must not
        /// narrow the list — picking a project under "All notes" and switching to "Personal" emptied the list
        /// (a project filter drops every personal note) with no control left to clear it, and a project with
        /// nothing in the archive filtered the archive to nothing while the hidden box read "All projects".
        /// </summary>
        public static string EffectiveProjectFilter(string projectFilter, NotesScope scope, IList<(string Id, string Name)> options)
        {
            if (projectFilter == null || !scope.IncludesProjectNotes() || options == null || options.Count <= 1) return null;
            return options.Any(option => option.Id == projectFilter) ? projectFilter : null;
        }

        /// <summary>Case- and accent-insensitive title match, as a person expects from a search box.</summary>
        public static bool Matches(string title, string query)
        {
            string trimmed = (query ?? "").Trim();
            if (trimmed.Length == 0) return true;
            return FoldForSearch(title).IndexOf(FoldForSearch(trimmed), StringComparison.Ordinal) >= 0;
        }

        internal static string FoldForSearch(string text)
        {
            var decomposed = (text ?? "").Normalize(NormalizationForm.FormD);
            var output = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    output.Append(char.ToLowerInvariant(c));
            }
            return output.ToString();
        }

        /// <summary>Newest first; undated rows sink to the bottom; ties fall back to the id so the order is stable.</summary>
        private static int NewerFirst(NotesListItem left, NotesListItem right)
        {
            var l = left.SortDate ?? DateTime.MinValue;
            var r = right.SortDate ?? DateTime.MinValue;
            if (l != r) return r.CompareTo(l);
            return string.CompareOrdinal(left.Id, right.Id);
        }
    }

    /// <summary>
    /// The project filter is a type-to-search box. What the typed text means once the user moves on — the
    /// rules of the Mac's <c>SearchableComboBoxField.resolve</c>, which exist because acting on a stale
    /// selection sent a "create note" to the wrong project.
    /// </summary>
    public static class ProjectFilterText
    {
        /// <summary>
        /// The project the text names: an exact name (case and surrounding space ignored) wins; otherwise a
        /// fragment that only one project's name contains. Empty, unknown or ambiguous text is no project —
        /// never the first one.
        /// </summary>
        public static string Resolve(string text, IEnumerable<(string Id, string Name)> options)
        {
            string typed = (text ?? "").Trim();
            if (typed.Length == 0) return null;
            var list = options.ToList();
            var exact = list.Where(o => string.Equals(o.Name.Trim(), typed, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0].Id;
            var fragment = list.Where(o => o.Name.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            return fragment.Count == 1 ? fragment[0].Id : null;
        }

        /// <summary>The cleared row ("All projects") first, then every project.</summary>
        public static List<string> Titles(string clearedOptionTitle, IEnumerable<(string Id, string Name)> options) =>
            new[] { clearedOptionTitle }.Concat(options.Select(o => o.Name)).ToList();

        /// <summary>A selection whose project has vanished from the options falls back to the cleared row.</summary>
        public static string DisplayName(string selectedId, IEnumerable<(string Id, string Name)> options, string clearedOptionTitle)
        {
            if (selectedId == null) return clearedOptionTitle;
            foreach (var option in options) if (option.Id == selectedId) return option.Name;
            return clearedOptionTitle;
        }
    }

    /// <summary>What one project's notes fetch came back with. Refused is not the same as empty.</summary>
    public sealed class ProjectNotesOutcome
    {
        private ProjectNotesOutcome(IList<ProjectNote> notes) { Notes = notes; }

        public static ProjectNotesOutcome Answered(IList<ProjectNote> notes) => new ProjectNotesOutcome(notes ?? new List<ProjectNote>());
        public static readonly ProjectNotesOutcome Failed = new ProjectNotesOutcome(null);

        /// <summary>Null when the project never answered.</summary>
        public IList<ProjectNote> Notes { get; }
        public bool IsFailed => Notes == null;
    }

    public sealed class ProjectNotesSweepResult
    {
        public ProjectNotesSweepResult(IList<ProjectNoteEntry> entries, ISet<string> failedProjectIds)
        {
            Entries = entries;
            FailedProjectIds = failedProjectIds;
        }

        public IList<ProjectNoteEntry> Entries { get; }
        public ISet<string> FailedProjectIds { get; }

        /// <summary>A sweep that lost projects is not finished, however complete its list looks.</summary>
        public bool IsComplete => FailedProjectIds.Count == 0;
    }

    /// <summary>
    /// Fetching every project's notes without tripping the API's rate limiter.
    ///
    /// The screen needs one <c>GET /projects/:id/notes</c> per active project. The Mac's first version sent
    /// them all at once; with 128 projects against an endpoint allowing 10 requests a second the server
    /// refused 66 of them with 429, and each refusal was recorded as a project with no notes — real notes
    /// vanished from the list, differently each run. So: a few requests in flight at a time, a rejected
    /// request retried rather than believed, and a project that still cannot be reached reported as failed.
    /// </summary>
    public static class ProjectNotesSweep
    {
        /// <summary>Four in flight keeps a sweep inside 10/s while the rest of the app also uses the API.</summary>
        public const int MaxConcurrentRequests = 4;
        /// <summary>One first try plus three retries; beyond that the server is not merely busy.</summary>
        public const int MaxAttempts = 4;
        public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Rate limiting, a 5xx and a request that never arrived (status 0) mean "ask again shortly". A
        /// 401/403/404 is the server's settled answer; retrying it spends budget the waiting projects need.
        /// </summary>
        public static bool IsTemporaryFailure(int status) => status == 429 || status == 0 || (status >= 500 && status < 600);

        /// <summary>How long before attempt <paramref name="attempt"/> + 1 (1-based), or null to stop. Doubles each time.</summary>
        public static TimeSpan? RetryDelay(int status, int attempt)
        {
            if (attempt < 1 || attempt >= MaxAttempts || !IsTemporaryFailure(status)) return null;
            return TimeSpan.FromTicks(FirstRetryDelay.Ticks * (1L << (attempt - 1)));
        }

        /// <summary>A project missing from <paramref name="outcomes"/> counts as failed: nobody heard back about it.</summary>
        public static ProjectNotesSweepResult Result(IDictionary<string, ProjectNotesOutcome> outcomes, IEnumerable<Project> projects)
        {
            var notesByProject = new Dictionary<string, IList<ProjectNote>>();
            var failed = new HashSet<string>();
            var projectList = projects.ToList();
            foreach (var project in projectList)
            {
                if (outcomes.TryGetValue(project.Id, out var outcome) && !outcome.IsFailed) notesByProject[project.Id] = outcome.Notes;
                else failed.Add(project.Id);
            }
            return new ProjectNotesSweepResult(ProjectNoteEntry.Entries(notesByProject, projectList), failed);
        }
    }

    /// <summary>
    /// Runs a <see cref="ProjectNotesSweep"/>: at most <see cref="ProjectNotesSweep.MaxConcurrentRequests"/>
    /// fetches in flight, each retried on a temporary failure, finishing with one result for every project.
    /// The fetch and the retry timer are injected, so the scheduling is testable without a network or a clock.
    /// </summary>
    public sealed class ProjectNotesSweepRunner
    {
        /// <summary>(projectId, completion(notes or null, statusCode)).</summary>
        public delegate void Fetch(string projectId, Action<IList<ProjectNote>, int> completion);

        /// <summary>(delay, work) — run <c>work</c> after <c>delay</c>.</summary>
        public delegate void Schedule(TimeSpan delay, Action work);

        private readonly Fetch _fetch;
        private readonly Schedule _schedule;
        private readonly int _maxConcurrent;
        private readonly object _lock = new object();
        private Queue<Project> _queue = new Queue<Project>();
        private Dictionary<string, ProjectNotesOutcome> _outcomes = new Dictionary<string, ProjectNotesOutcome>();
        private List<Project> _projects = new List<Project>();
        private int _remaining;
        private Action<ProjectNotesSweepResult> _completion;

        public ProjectNotesSweepRunner(Fetch fetch, Schedule schedule, int maxConcurrentRequests = ProjectNotesSweep.MaxConcurrentRequests)
        {
            _fetch = fetch;
            _schedule = schedule;
            _maxConcurrent = Math.Max(1, maxConcurrentRequests);
        }

        public void Run(IEnumerable<Project> projects, Action<ProjectNotesSweepResult> completion)
        {
            var list = projects.ToList();
            if (list.Count == 0)
            {
                completion(ProjectNotesSweep.Result(new Dictionary<string, ProjectNotesOutcome>(), list));
                return;
            }
            int slots;
            lock (_lock)
            {
                _projects = list;
                _completion = completion;
                _queue = new Queue<Project>(list);
                _remaining = list.Count;
                _outcomes = new Dictionary<string, ProjectNotesOutcome>();
                slots = Math.Min(_maxConcurrent, list.Count);
            }
            for (int i = 0; i < slots; i++) StartNext();
        }

        private void StartNext()
        {
            Project project;
            lock (_lock)
            {
                if (_queue.Count == 0) return;
                project = _queue.Dequeue();
            }
            Attempt(project, 1);
        }

        private void Attempt(Project project, int number)
        {
            _fetch(project.Id, (notes, status) =>
            {
                if (notes != null) { Finish(project, ProjectNotesOutcome.Answered(notes)); return; }
                var delay = ProjectNotesSweep.RetryDelay(status, number);
                if (delay == null) { Finish(project, ProjectNotesOutcome.Failed); return; }
                // A retry keeps its slot. Idling the slot for the backoff is the throttle: a rate-limited
                // sweep should make fewer requests per second, not hand the slot to the next project.
                _schedule(delay.Value, () => Attempt(project, number + 1));
            });
        }

        private void Finish(Project project, ProjectNotesOutcome outcome)
        {
            Action<ProjectNotesSweepResult> completion = null;
            ProjectNotesSweepResult result = null;
            lock (_lock)
            {
                _outcomes[project.Id] = outcome;
                _remaining--;
                if (_remaining == 0 && _completion != null)
                {
                    result = ProjectNotesSweep.Result(_outcomes, _projects);
                    completion = _completion;
                    _completion = null;
                }
            }
            if (completion != null) { completion(result); return; }
            StartNext();
        }
    }

    /// <summary>What the footer's main button does: archive/restore an open note, else close the screen.</summary>
    public enum NotesFooterAction { ArchiveNote, RestoreNote, CloseScreen }

    /// <summary>What happens to the open note once archived (step back — "I am done with it") or restored (stay).</summary>
    public enum ArchivedNoteDisposition { StepBackToList, StayOpen }

    /// <summary>Close steps back from an open project note before it ever dismisses the screen.</summary>
    public enum NotesCloseStep { CloseProjectNote, CloseScreen }

    public static class NotesNavigation
    {
        public static NotesFooterAction FooterAction(ProjectNote openNote) =>
            openNote == null ? NotesFooterAction.CloseScreen : openNote.IsArchived ? NotesFooterAction.RestoreNote : NotesFooterAction.ArchiveNote;

        public static ArchivedNoteDisposition AfterArchiving(bool archived) =>
            archived ? ArchivedNoteDisposition.StepBackToList : ArchivedNoteDisposition.StayOpen;

        public static NotesCloseStep CloseStep(bool hasOpenProjectNote) =>
            hasOpenProjectNote ? NotesCloseStep.CloseProjectNote : NotesCloseStep.CloseScreen;
    }

    /// <summary>Creating a project note (<c>POST /projects/:id/notes { title }</c>).</summary>
    public static class NewProjectNote
    {
        /// <summary>A project must be chosen deliberately; nothing is preselected.</summary>
        public static bool CanCreate(string projectId) => !string.IsNullOrWhiteSpace(projectId);

        public static string ResolvedTitle(string typed, string fallback)
        {
            string trimmed = (typed ?? "").Trim();
            return NoteLimits.Clip(trimmed.Length == 0 ? fallback : trimmed, NoteLimits.ProjectNoteTitle);
        }
    }

    /// <summary>What leaving an inline title edit does with the typed text.</summary>
    public static class InlineRenameDismissal
    {
        /// <summary>
        /// Leaving while still editing saves the trimmed draft (Back used to drop a typed-but-unentered title).
        /// Leaving after Escape — edit mode already ended — discards. Blank or unchanged is never a rename.
        /// Returns the title to save, or null for nothing.
        /// </summary>
        public static string OnDismiss(string draft, string value, bool isEditing)
        {
            if (!isEditing) return null;
            string trimmed = (draft ?? "").Trim();
            if (trimmed.Length == 0 || trimmed == value) return null;
            return trimmed;
        }
    }

    /// <summary>The dashboard deep link for a project note, one place so Projects and Notes cannot drift.</summary>
    public static class ProjectNoteLinks
    {
        public const string Dashboard = "https://dashboard.focusbear.io";

        public static string WebUrl(string projectId, string noteId)
        {
            string url = Dashboard + "/projects/" + Uri.EscapeDataString(projectId ?? "") + "?tab=notes";
            if (!string.IsNullOrEmpty(noteId)) url += "&note_id=" + Uri.EscapeDataString(noteId);
            return url;
        }
    }
}
