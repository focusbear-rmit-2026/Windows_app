using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// Paging constants shared with the web (<c>PAGE.TAKE</c>) and the Mac's <c>NotesPaging</c>.
    /// </summary>
    public static class NotesPaging
    {
        public const int PageSize = 10;
        public const int FirstPage = 1;
        public const int SearchTake = 20;
        /// <summary>
        /// One-off scan size used to find the brain dump when it has fallen off page 1 (<c>GET /note</c>
        /// orders by created_at, and a brain dump is usually old). The backend's maximum take.
        /// </summary>
        public const int BrainDumpScanTake = 50;
    }

    /// <summary>
    /// The backend's length limits (<c>CreateNoteDto</c>, <c>CreateNoteTagDto</c>, <c>Create/UpdateProjectNoteDto</c>).
    /// Past them the save is refused with a generic "could not save"; the fields stop the user at the limit instead.
    /// </summary>
    public static class NoteLimits
    {
        public const int NoteTitle = 500;
        public const int NoteBody = 50000;
        public const int TagText = 255;
        public const int ProjectNoteTitle = 255;

        /// <summary>At most <paramref name="max"/> UTF-16 units, never splitting an emoji's surrogate pair.</summary>
        public static string Clip(string text, int max)
        {
            if (text == null || text.Length <= max) return text;
            int cut = max;
            if (cut > 0 && char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut);
        }
    }

    /// <summary>Timings the web and the Mac use for the notes screen.</summary>
    public static class NotesTiming
    {
        /// <summary>Web <c>DEBOUNCE_DELAY.NOTE_SEARCH</c>.</summary>
        public static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(500);
        /// <summary>Web <c>TIMEOUT_DELAY.BRAIN_DUMP_SAVE</c>.</summary>
        public static readonly TimeSpan BrainDumpSaveDelay = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan StatusMessage = TimeSpan.FromSeconds(3);
    }

    /// <summary>The list rules from the Mac's <c>NotesStore</c>, without the store.</summary>
    public static class PersonalNotesList
    {
        /// <summary>
        /// Notes the list shows: search results while searching, else the paged list — never the brain
        /// dump, which has its own editor above the list. Search results come back without the server's
        /// tag filter, so the same tag is applied here.
        /// </summary>
        public static List<Note> Visible(IEnumerable<Note> paged, IEnumerable<Note> searchResults, bool isSearchActive, string selectedTagId)
        {
            var source = isSearchActive ? (searchResults ?? Enumerable.Empty<Note>()) : (paged ?? Enumerable.Empty<Note>());
            return source.Where(n => !n.IsBrainDump && n.CarriesTag(selectedTagId)).ToList();
        }

        /// <summary>
        /// Folds a fetched page into the list. Page 1 replaces it; a later page appends what is not
        /// already there, since a note created meanwhile shifts every page by one.
        /// </summary>
        public static List<Note> Merge(IList<Note> current, IEnumerable<Note> incoming, bool append)
        {
            if (!append) return incoming.ToList();
            var known = new HashSet<string>(current.Select(n => n.Id));
            return current.Concat(incoming.Where(n => known.Add(n.Id))).ToList();
        }

        public static bool IsSearchActive(string query) => !string.IsNullOrWhiteSpace(query);
    }

    /// <summary>
    /// Page loads of the personal-notes list, one in flight at a time. A reload of page 1 asked for while
    /// another load is out — after a save or a delete, a tag picked, the search cleared — is kept and run
    /// the moment that load lands. It used to be dropped: the in-flight page (often an infinite-scroll page
    /// 2) landed, and the note just saved never appeared, or the list kept the previous tag's notes. A later
    /// page asked for meanwhile is dropped; the next scroll asks again.
    /// </summary>
    public sealed class NotesPageLoadGate
    {
        private bool _reloadPending;

        public bool IsFetching { get; private set; }

        /// <summary>True when this load should start now; false when it waits (a reload) or is dropped (a later page).</summary>
        public bool TryStart(bool append)
        {
            if (IsFetching)
            {
                if (!append) _reloadPending = true;
                return false;
            }
            IsFetching = true;
            return true;
        }

        /// <summary>The load landed. True when a page-1 reload was asked for meanwhile and should start now.</summary>
        public bool Finish()
        {
            IsFetching = false;
            bool reload = _reloadPending;
            _reloadPending = false;
            return reload;
        }
    }

    /// <summary>
    /// Which note the brain-dump editor is bound to. Web <c>BrainDumpNote</c>: the brain dump is whichever
    /// listed note has <c>is_brain_dump</c>. The list is ordered by created_at, so an old one can fall off
    /// page 1 — the web then creates a duplicate, and accounts hit by that carry several. Every one seen
    /// this session is remembered and the editor follows the most recently updated.
    /// </summary>
    public sealed class BrainDumpTracker
    {
        private readonly Dictionary<string, Note> _seen = new Dictionary<string, Note>();

        /// <summary>The note the editor is bound to; null until one is found or the first save creates it.</summary>
        public Note Adopted { get; private set; }

        /// <summary>
        /// Remembers the brain dumps in <paramref name="listed"/>. Returns the note to show when that
        /// changes which note (or which version of it) the editor should follow, else null.
        /// </summary>
        public Note Observe(IEnumerable<Note> listed)
        {
            foreach (var note in listed.Where(n => n.IsBrainDump)) _seen[note.Id] = note;
            // ISO-8601 timestamps sort chronologically as strings.
            var latest = _seen.Values.OrderByDescending(n => n.UpdatedAt ?? "", StringComparer.Ordinal).FirstOrDefault();
            if (latest == null) return null;
            if (Adopted != null && Adopted.Id == latest.Id && Adopted.UpdatedAt == latest.UpdatedAt) return null;
            Adopted = latest;
            return latest;
        }

        /// <summary>What <c>PUT /note</c> returned for a brain-dump save.</summary>
        public void Saved(Note saved)
        {
            if (saved == null) return;
            _seen[saved.Id] = saved;
            Adopted = saved;
            _newId = null;
        }

        private string _newId;

        /// <summary>
        /// The id a save sends: the adopted note's, or — before there is one — an id minted here and kept
        /// until a save lands. <c>PUT /note</c> creates a note under the id it is given, so a retry of a
        /// create whose answer never arrived, or a second save sent before the first answered, upserts the
        /// same note instead of creating another brain dump (the server's own guard admits that two id-less
        /// saves can both create).
        /// </summary>
        public string IdForSave()
        {
            if (Adopted != null) return Adopted.Id;
            return _newId ?? (_newId = Guid.NewGuid().ToString());
        }
    }

    /// <summary>When a brain-dump edit is saved. Port of the two rules on the Mac's <c>NotesStore</c>.</summary>
    public static class BrainDumpSaveRules
    {
        /// <summary>
        /// A body equal to what the server already has (just loaded, or a cursor move that produced the
        /// same HTML) is not an edit and must not start the debounce, or every screen showing the brain
        /// dump would re-save it on open.
        /// </summary>
        public static bool ShouldSchedule(string body, string lastSavedBody) => !string.Equals(body ?? "", lastSavedBody ?? "", StringComparison.Ordinal);

        /// <summary>
        /// Unchanged content is a no-op. Empty content may overwrite an existing brain dump — clearing it
        /// should stick across a restart — but must never create a brand-new empty one.
        /// </summary>
        public static bool ShouldCommit(string body, string lastSavedBody, bool hasExistingNote)
        {
            if (!ShouldSchedule(body, lastSavedBody)) return false;
            return hasExistingNote || NoteHtml.PlainText(body).Length > 0;
        }

        /// <summary>How long a refused or unanswered brain-dump save waits before it tries again.</summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// When the next save should go, once one has come back: a retry after a failure — the edit exists
        /// only in this editor, and leaving the screen only flushes a save that is scheduled — the usual
        /// debounce when typing carried on while the request was out, else nothing.
        /// </summary>
        public static TimeSpan? NextSave(bool saved, string sentBody, string currentBody) => NextSave(saved, sentBody, currentBody, false);

        /// <summary>
        /// <paramref name="flushRequested"/>: the screen was left (or the app is closing) while this save was out; typing
        /// that carried on goes at once instead of after the debounce, which a quit would not wait for.
        /// </summary>
        public static TimeSpan? NextSave(bool saved, string sentBody, string currentBody, bool flushRequested)
        {
            if (!saved) return RetryDelay;
            if (!ShouldSchedule(currentBody, sentBody)) return null;
            return flushRequested ? TimeSpan.Zero : NotesTiming.BrainDumpSaveDelay;
        }
    }

    /// <summary>The tag picker in the note editor. Web <c>NotesDrawer.handleAddTag</c>.</summary>
    public static class NoteTagPicker
    {
        /// <summary>The web's palette for a newly typed tag.</summary>
        public static readonly string[] Palette = { "#ef4444", "#f97316", "#eab308", "#22c55e", "#14b8a6", "#8b5cf6", "#ec4899" };

        /// <summary>
        /// Adds the typed tag to <paramref name="selected"/>: an existing tag with the same text
        /// (case-insensitively) is reused, so it keeps its id and colour; otherwise a new, unsaved one gets
        /// a palette colour. Adding a tag already on the note does nothing. Returns whether anything changed.
        /// </summary>
        public static bool Add(string typed, IList<NoteTag> userTags, IList<NoteTag> selected, Func<int, int> pickIndex)
        {
            string text = (typed ?? "").Trim();
            if (text.Length == 0) return false;
            if (selected.Any(t => string.Equals(t.Text, text, StringComparison.OrdinalIgnoreCase))) return false;
            var existing = userTags.FirstOrDefault(t => string.Equals(t.Text, text, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (selected.Any(t => !string.IsNullOrEmpty(t.Id) && t.Id == existing.Id)) return false;
                selected.Add(existing);
                return true;
            }
            selected.Add(new NoteTag { Id = "", Text = text, Color = Palette[pickIndex(Palette.Length)] });
            return true;
        }

        /// <summary>Tags offered as one-click suggestions: the user's, less those already on the note.</summary>
        public static List<NoteTag> Suggestions(IEnumerable<NoteTag> userTags, IEnumerable<NoteTag> selected, int limit = 10)
        {
            var chosen = new HashSet<string>(selected.Select(t => t.Text.ToLowerInvariant()));
            return userTags.Where(t => !chosen.Contains(t.Text.ToLowerInvariant())).Take(limit).ToList();
        }
    }

    /// <summary>
    /// The body of <c>PUT /note</c> (<c>CreateNoteDto</c>). Optional fields are left out rather than sent
    /// as null: the backend only touches a field whose key is present (<c>hasDtoProperty</c>), so an
    /// update without <c>tags</c> keeps the note's tags, and a brain-dump save without them keeps its own.
    /// </summary>
    public static class NoteUpsertBody
    {
        public static JObject ForNote(string id, string title, string body, IEnumerable<NoteTag> tags, string completedActivityId)
        {
            var payload = new JObject { ["title"] = (title ?? "").Trim(), ["body"] = body ?? "" };
            if (!string.IsNullOrEmpty(id)) payload["id"] = id;
            if (tags != null)
            {
                payload["tags"] = new JArray(tags.Select(t =>
                {
                    var tag = new JObject { ["text"] = t.Text };
                    // An id lets the backend reuse the tag instead of find-or-creating it by text.
                    if (!string.IsNullOrEmpty(t.Id)) tag["id"] = t.Id;
                    if (!string.IsNullOrEmpty(t.Color)) tag["color"] = t.Color;
                    return tag;
                }));
            }
            if (!string.IsNullOrEmpty(completedActivityId)) payload["completed_activity_id"] = completedActivityId;
            return payload;
        }

        /// <summary>
        /// The body a note save sends. The editor reads the stored HTML into its own document model, which
        /// holds only what the native editor can edit — tables, images, highlights and the web's other markup
        /// do not survive the trip. So a body the user never touched goes back exactly as it came; only an
        /// edit replaces it with the editor's export. <paramref name="openingExport"/> is the editor's own
        /// export straight after loading, the only fair comparison: the stored HTML rarely matches it byte for
        /// byte even when nothing was typed.
        /// </summary>
        public static string BodyToSave(string editorExport, string openingExport, string originalHtml)
        {
            if (originalHtml != null && string.Equals(editorExport ?? "", openingExport ?? "", System.StringComparison.Ordinal)) return originalHtml;
            return editorExport ?? "";
        }

        public static JObject ForBrainDump(string existingId, string title, string body)
        {
            var payload = new JObject { ["title"] = title ?? "", ["body"] = body ?? "", ["is_brain_dump"] = true };
            if (!string.IsNullOrEmpty(existingId)) payload["id"] = existingId;
            return payload;
        }
    }
}
