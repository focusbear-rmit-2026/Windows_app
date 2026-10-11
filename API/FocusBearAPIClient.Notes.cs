using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    /// <summary>What a notes request came back with: the status (0 when it never reached the server) and the body.</summary>
    internal sealed class ApiResponse
    {
        internal ApiResponse(int status, string body)
        {
            Status = status;
            Body = body;
        }

        internal int Status { get; }
        internal string Body { get; }
        internal bool IsSuccess { get { return Status >= 200 && Status < 300; } }

        internal static readonly ApiResponse Unreachable = new ApiResponse(0, null);
    }

    /// <summary>
    /// The notes endpoints the native Notes screen uses — the same ones the web dashboard's
    /// <c>store/reducer/note/extra.ts</c> and the Mac's <c>NotesApi.swift</c> call, against the backend
    /// <c>NoteController</c> (<c>/note</c>, <c>/note/search</c>, <c>/note/tags</c>) and the project notes
    /// under <c>/projects/:id/notes</c>. Bodies are returned raw: <c>FocusBear.Core.Notes.NotesWire</c>
    /// parses them, deliberately without turning timestamps into dates.
    /// </summary>
    partial class FocusBearAPIClient
    {
        private const string ApiBase = "https://api.focusbear.io";

        public static string Notes { get { return ApiBase + "/note"; } }
        public static string NoteTags { get { return ApiBase + "/note/tags"; } }
        public static string NoteSearch { get { return ApiBase + "/note/search"; } }

        /// <summary>
        /// One authenticated request. <paramref name="checkInternet"/> costs an extra round trip to the API,
        /// so the project-notes sweep checks once up front rather than once per project.
        /// </summary>
        internal async Task<ApiResponse> SendJsonRequest(string method, string url, JToken body, string caller, bool checkInternet = true)
        {
            try
            {
                if (checkInternet && !await IsInternetOn())
                {
                    LogWriter.LogWrite(caller + ": no internet");
                    return ApiResponse.Unreachable;
                }
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    if (await RenewToken() == 0) return ApiResponse.Unreachable;
                }
                using (HttpClient client = GetClientWithHeaders())
                using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                {
                    if (body != null)
                        request.Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    using (var response = await client.SendAsync(request))
                    {
                        string text = response.Content != null ? await response.Content.ReadAsStringAsync() : "";
                        int status = (int)response.StatusCode;
                        if (status < 200 || status >= 300)
                            LogWriter.LogWrite(caller + " " + method + " FAILED - status " + status + " " + Truncate(text));
                        return new ApiResponse(status, text);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(caller + " Exception - " + ex.Message);
                return ApiResponse.Unreachable;
            }
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= 500 ? text : text.Substring(0, 500) + "…";
        }

        private static string BodyOrNull(ApiResponse response)
        {
            return response.IsSuccess ? response.Body : null;
        }

        /// <summary><c>GET /note?page=&amp;take=&amp;order=DESC[&amp;tag_id=]</c> — one page, newest first.</summary>
        internal async Task<string> GetNotesPage(int page, int take, string tagId = null)
        {
            string url = Notes + "?page=" + page + "&take=" + take + "&order=DESC";
            if (!string.IsNullOrEmpty(tagId)) url += "&tag_id=" + Uri.EscapeDataString(tagId);
            return BodyOrNull(await SendJsonRequest("GET", url, null, "GetNotesPage"));
        }

        /// <summary><c>GET /note/search?query=&amp;take=</c> — a plain array of matching notes (bodies too).</summary>
        internal async Task<string> SearchNotes(string query, int take)
        {
            string url = NoteSearch + "?query=" + Uri.EscapeDataString(query ?? "") + "&take=" + take;
            return BodyOrNull(await SendJsonRequest("GET", url, null, "SearchNotes"));
        }

        /// <summary><c>GET /note/tags</c> — every tag the user has put on a note.</summary>
        internal async Task<string> GetNoteTags()
        {
            return BodyOrNull(await SendJsonRequest("GET", NoteTags, null, "GetNoteTags"));
        }

        /// <summary><c>PUT /note</c> — create (no id) or update (id). Returns the saved note, or null.</summary>
        internal async Task<string> UpsertNote(JObject payload)
        {
            return BodyOrNull(await SendJsonRequest("PUT", Notes, payload, "UpsertNote"));
        }

        /// <summary><c>DELETE /note?note_id=</c>.</summary>
        internal async Task<bool> DeleteNote(string noteId)
        {
            return (await SendJsonRequest("DELETE", Notes + "?note_id=" + Uri.EscapeDataString(noteId ?? ""), null, "DeleteNote")).IsSuccess;
        }

        /// <summary><c>GET /projects?page=&amp;take=&amp;order=DESC</c> — active projects (the server's default status).</summary>
        internal async Task<string> GetActiveProjectsPage(int page, int take)
        {
            string url = ApiBase + "/projects?page=" + page + "&take=" + take + "&order=DESC";
            return BodyOrNull(await SendJsonRequest("GET", url, null, "GetActiveProjectsPage"));
        }

        /// <summary>
        /// <c>GET /projects/:id/notes?page=&amp;take=&amp;order=DESC[&amp;archived=true]</c> — one page. The status is
        /// returned whole: the sweep retries a 429 or 5xx and believes a 404, so it has to see which one it got.
        /// </summary>
        internal async Task<ApiResponse> GetProjectNotes(string projectId, bool archived, int page = 1)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes?page=" + page
                         + "&take=" + FocusBear.Core.Notes.ProjectNotesPaging.Take + "&order=DESC";
            if (archived) url += "&archived=true";
            return await SendJsonRequest("GET", url, null, "GetProjectNotes", checkInternet: false);
        }

        /// <summary><c>GET /projects/:id/notes/:noteId</c> — the note with its body, which list rows lack.</summary>
        internal async Task<string> GetProjectNote(string projectId, string noteId)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes/" + Uri.EscapeDataString(noteId);
            return BodyOrNull(await SendJsonRequest("GET", url, null, "GetProjectNote"));
        }

        /// <summary><c>POST /projects/:id/notes { title }</c> — a new shared note in that project.</summary>
        internal async Task<string> CreateProjectNote(string projectId, string title)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes";
            return BodyOrNull(await SendJsonRequest("POST", url, new JObject { ["title"] = title }, "CreateProjectNote"));
        }

        /// <summary>
        /// <c>PATCH /projects/:id/notes/:noteId { title }</c>. The endpoint takes <c>content</c> too, but the body
        /// is a Yjs document: written from here it would be invisible to the web editor and clobbered by it.
        /// </summary>
        internal async Task<string> RenameProjectNote(string projectId, string noteId, string title)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes/" + Uri.EscapeDataString(noteId);
            return BodyOrNull(await SendJsonRequest("PATCH", url, new JObject { ["title"] = title }, "RenameProjectNote"));
        }

        private static string ProjectNoteUrl(string projectId, string noteId) =>
            ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes/" + Uri.EscapeDataString(noteId);

        /// <summary>
        /// <c>GET /projects/:id/notes/:noteId/doc</c> — the shared note's editor state
        /// <c>{yjs_state, content}</c>. The status comes back whole: 401/403/404 mean access is gone.
        /// </summary>
        internal Task<ApiResponse> GetProjectNoteDoc(string projectId, string noteId) =>
            SendJsonRequest("GET", ProjectNoteUrl(projectId, noteId) + "/doc", null, "GetProjectNoteDoc");

        /// <summary><c>PUT …/doc {yjs_state, content}</c> — the server merges the update and returns the merged state.</summary>
        internal Task<ApiResponse> SaveProjectNoteDoc(string projectId, string noteId, JObject body) =>
            SendJsonRequest("PUT", ProjectNoteUrl(projectId, noteId) + "/doc", body, "SaveProjectNoteDoc", checkInternet: false);

        /// <summary><c>POST …/realtime-auth {socket_id}</c> — Pusher auth for <c>presence-project-note-:noteId</c>.</summary>
        internal Task<ApiResponse> AuthorizeProjectNoteRealtime(string projectId, string noteId, string socketId) =>
            SendJsonRequest("POST", ProjectNoteUrl(projectId, noteId) + "/realtime-auth", new JObject { ["socket_id"] = socketId }, "AuthorizeProjectNoteRealtime", checkInternet: false);

        /// <summary><c>DELETE /projects/:id/notes/:noteId</c>.</summary>
        internal async Task<bool> DeleteProjectNote(string projectId, string noteId)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes/" + Uri.EscapeDataString(noteId);
            return (await SendJsonRequest("DELETE", url, null, "DeleteProjectNote")).IsSuccess;
        }

        /// <summary><c>PATCH /projects/:id/notes/:noteId { archived }</c> — file a note away, or bring it back.</summary>
        internal async Task<string> SetProjectNoteArchived(string projectId, string noteId, bool archived)
        {
            string url = ApiBase + "/projects/" + Uri.EscapeDataString(projectId) + "/notes/" + Uri.EscapeDataString(noteId);
            return BodyOrNull(await SendJsonRequest("PATCH", url, new JObject { ["archived"] = archived }, "SetProjectNoteArchived"));
        }
    }
}
