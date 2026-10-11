using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>What one project's notes fetch came to: every note, or null with the status that stopped it.</summary>
    public sealed class ProjectNotesFetch
    {
        public ProjectNotesFetch(IList<ProjectNote> notes, int status)
        {
            Notes = notes;
            Status = status;
        }

        /// <summary>Null when a page failed — a list missing its later pages must not pass for the whole list.</summary>
        public IList<ProjectNote> Notes { get; }

        /// <summary>The failing page's HTTP status (0 = never arrived, or arrived unreadable), for the sweep's retry rule.</summary>
        public int Status { get; }
    }

    /// <summary>
    /// Every note of one project, page by page. <c>GET /projects/:id/notes</c> is paged (<c>take</c> defaults
    /// to 20), and the screen asked for page 1 alone: a project's 21st note never reached the list, the
    /// search, or the project filter. Now pages of the backend's maximum are read until the server says
    /// there is no next page.
    /// </summary>
    public static class ProjectNotesPaging
    {
        /// <summary>The backend's MAX_PAGINATION_TAKE.</summary>
        public const int Take = 50;

        /// <summary>A cap, so a server that always answers "there is more" cannot keep the sweep forever.</summary>
        public const int MaxPages = 40;

        /// <param name="fetchPage">(page) → (status, body) for <c>?page=&amp;take=</c><see cref="Take"/>.</param>
        public static async Task<ProjectNotesFetch> FetchAll(Func<int, Task<Tuple<int, string>>> fetchPage)
        {
            var all = new List<ProjectNote>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int page = 1; page <= MaxPages; page++)
            {
                Tuple<int, string> response = await fetchPage(page).ConfigureAwait(true);
                int status = response?.Item1 ?? 0;
                bool success = status >= 200 && status < 300;
                var read = success ? ReadPage(response.Item2) : null;
                if (read == null) return new ProjectNotesFetch(null, success ? 0 : status);
                int added = 0;
                foreach (var note in read.Item1)
                {
                    if (note == null || string.IsNullOrEmpty(note.Id) || !seen.Add(note.Id)) continue;
                    all.Add(note);
                    added++;
                }
                if (!read.Item2) return new ProjectNotesFetch(all, status);
                // A "next page" that repeats itself: stop rather than loop.
                if (added == 0) return new ProjectNotesFetch(all, status);
            }
            return new ProjectNotesFetch(all, 200);
        }

        /// <summary><c>{ data: [...], meta: { hasNextPage } }</c> → (notes, hasNextPage); null when unreadable.</summary>
        public static Tuple<List<ProjectNote>, bool> ReadPage(string json)
        {
            if (!(NotesWire.Parse(json) is JObject page)) return null;
            List<ProjectNote> notes = NotesWire.ReadProjectNotes(page);
            if (notes == null) return null;
            var hasNext = (page["meta"] as JObject)?["hasNextPage"];
            return Tuple.Create(notes, hasNext != null && hasNext.Type == JTokenType.Boolean && (bool)hasNext);
        }
    }
}
