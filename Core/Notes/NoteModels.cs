using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// A note tag as <c>GET /note</c> and <c>GET /note/tags</c> return it. An empty id means "typed by the
    /// user, not saved yet": the backend assigns one on upsert. Mirrors the Mac's <c>NoteTag</c>.
    /// </summary>
    public sealed class NoteTag
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("text")] public string Text { get; set; } = "";
        [JsonProperty("color")] public string Color { get; set; }

        /// <summary>Colour to draw a tag the server gave none: the web's neutral grey.</summary>
        public const string DefaultColor = "#6b7280";

        [JsonIgnore] public string DisplayColor => string.IsNullOrEmpty(Color) ? DefaultColor : Color;
    }

    /// <summary>
    /// A personal note — the backend <c>NoteResponseDto</c>, as the web's <c>note.interface.ts</c> and the
    /// Mac's <c>Note</c> read it. <see cref="Body"/> is the web editor's HTML.
    /// </summary>
    public sealed class Note
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("title")] public string Title { get; set; } = "";
        [JsonProperty("body")] public string Body { get; set; }
        [JsonProperty("completed_activity_id")] public string CompletedActivityId { get; set; }
        [JsonProperty("tags")] public List<NoteTag> Tags { get; set; }
        [JsonProperty("embedded_todo_ids")] public List<string> EmbeddedTodoIds { get; set; }
        [JsonProperty("activity_name")] public string ActivityName { get; set; }
        [JsonProperty("activity_emoji")] public string ActivityEmoji { get; set; }
        [JsonProperty("logged_at")] public string LoggedAt { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; } = "";
        [JsonProperty("is_brain_dump")] public bool? IsBrainDumpFlag { get; set; }

        [JsonIgnore] public bool IsBrainDump => IsBrainDumpFlag == true;
        [JsonIgnore] public IList<NoteTag> TagList => Tags ?? new List<NoteTag>();

        /// <summary>The web list shows <c>logged_at ?? created_at</c>: a back-dated log shows when it happened.</summary>
        [JsonIgnore] public string DisplayDateString => string.IsNullOrEmpty(LoggedAt) ? CreatedAt : LoggedAt;

        public bool CarriesTag(string tagId) => string.IsNullOrEmpty(tagId) || TagList.Any(t => t.Id == tagId);
    }

    /// <summary>
    /// A note inside a project (<c>GET /projects/:id/notes</c>). Its body is a ProseMirror document
    /// written through a collaborative (Yjs) editor, not HTML; list rows arrive without it.
    /// </summary>
    public sealed class ProjectNote
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("project_id")] public string ProjectId { get; set; }
        [JsonProperty("created_by")] public string CreatedBy { get; set; }
        [JsonProperty("title")] public string Title { get; set; } = "";
        [JsonProperty("content")] public JToken Content { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; }
        /// <summary>When the note was filed away; null while active, and absent from an older API.</summary>
        [JsonProperty("archived_at")] public string ArchivedAt { get; set; }

        [JsonIgnore] public bool IsArchived => ArchivedAt != null;

        [JsonIgnore] public string PlainText => ProseMirrorNote.PlainText(Content);
    }

    /// <summary>
    /// Wire parsing for the note endpoints. Every reader takes the raw response body and tolerates a
    /// missing or malformed one by returning null, so the caller's "request failed" path is the only
    /// failure path.
    ///
    /// Raw text rather than a parsed JToken on purpose: Newtonsoft's default parse turns every ISO
    /// timestamp into a DateTime, and reading that back as a string gives the running culture's format
    /// ("09/01/2026 00:00:00"). The list sorts and the brain dump picks its note by comparing those
    /// strings as ISO-8601, so they must arrive exactly as the server sent them.
    /// </summary>
    public static class NotesWire
    {
        public static JToken Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    return JToken.ReadFrom(reader);
            }
            catch (JsonException) { return null; }
        }

        public static NotesPage ReadPage(string json) => ReadPage(Parse(json));
        public static List<Note> ReadNotes(string json) => ReadNotes(Parse(json));
        public static Note ReadNote(string json) => ReadNote(Parse(json));
        public static List<NoteTag> ReadTags(string json) => ReadTags(Parse(json));
        public static List<ProjectNote> ReadProjectNotes(string json) => ReadProjectNotes(Parse(json));
        public static ProjectNote ReadProjectNote(string json) => ReadProjectNote(Parse(json));

        /// <summary>
        /// One page of <c>GET /projects</c>, reduced to what the notes sweep needs (id and name). The list
        /// arrives as <c>data</c> or as <c>projects</c> depending on the API version; the longer one wins, as
        /// on the Mac. Null when the body is unreadable.
        /// </summary>
        public static ProjectsPage ReadProjectsPage(string json)
        {
            if (!(Parse(json) is JObject page)) return null;
            var data = page["data"] as JArray ?? new JArray();
            var alias = page["projects"] as JArray ?? new JArray();
            var items = alias.Count > data.Count ? alias : data;
            var projects = new List<FocusBear.Core.ToDo.Project>();
            foreach (var item in items.OfType<JObject>())
            {
                string id = item["id"]?.Type == JTokenType.String ? (string)item["id"] : null;
                if (string.IsNullOrEmpty(id)) continue;
                string name = item["name"]?.Type == JTokenType.String ? (string)item["name"] : (item["title"]?.Type == JTokenType.String ? (string)item["title"] : "");
                projects.Add(new FocusBear.Core.ToDo.Project(id, name));
            }
            bool hasNext = (page["meta"] as JObject)?["hasNextPage"]?.Type == JTokenType.Boolean && (bool)page["meta"]["hasNextPage"];
            return new ProjectsPage(projects, hasNext);
        }

        /// <summary><c>GET /note</c>: <c>{ data: [...], meta: { hasNextPage } }</c>. Takes a token from <see cref="Parse"/>.</summary>
        public static NotesPage ReadPage(JToken json)
        {
            if (!(json is JObject page) || !(page["data"] is JArray data)) return null;
            return new NotesPage(ReadNotes(data) ?? new List<Note>(), (page["meta"] as JObject)?["hasNextPage"]?.Type == JTokenType.Boolean && page["meta"].Value<bool>("hasNextPage"));
        }

        public static List<Note> ReadNotes(JToken json) => Read<List<Note>>(json as JArray);

        public static Note ReadNote(JToken json) => Read<Note>(json as JObject);

        public static List<NoteTag> ReadTags(JToken json) => Read<List<NoteTag>>(json as JArray);

        /// <summary><c>GET /projects/:id/notes</c>: <c>{ data: [...] }</c>.</summary>
        public static List<ProjectNote> ReadProjectNotes(JToken json) => Read<List<ProjectNote>>(json?["data"] as JArray);

        public static ProjectNote ReadProjectNote(JToken json) => Read<ProjectNote>(json as JObject);

        private static T Read<T>(JToken json) where T : class
        {
            if (json == null) return null;
            try { return json.ToObject<T>(); }
            catch (JsonException) { return null; }
        }
    }

    public sealed class ProjectsPage
    {
        /// <summary>Mac <c>ToDoPaging.allProjectsTake</c> / <c>allProjectsMaxPages</c>.</summary>
        public const int Take = 50;
        public const int MaxPages = 100;

        public ProjectsPage(IList<FocusBear.Core.ToDo.Project> items, bool hasNextPage)
        {
            Items = items;
            HasNextPage = hasNextPage;
        }

        public IList<FocusBear.Core.ToDo.Project> Items { get; }
        public bool HasNextPage { get; }
    }

    public sealed class NotesPage
    {
        public NotesPage(IList<Note> data, bool hasNextPage)
        {
            Data = data;
            HasNextPage = hasNextPage;
        }

        public IList<Note> Data { get; }
        public bool HasNextPage { get; }
    }
}
