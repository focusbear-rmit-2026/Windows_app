using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    /// <summary>
    /// The to-do endpoints the native To-Do screen uses — the same calls as the web dashboard's
    /// <c>store/reducer/to-do/extra.ts</c> and the Mac's <c>ToDoApi.swift</c>, against the backend
    /// <c>TodoController</c> and <c>ProjectController</c>. Bodies come back raw for
    /// <see cref="ToDoJson"/> to read.
    /// </summary>
    partial class FocusBearAPIClient
    {
        public static string ToDo { get { return ApiBase + "/to-do"; } }

        /// <summary>`GET /to-do` rejects take &gt; 50.</summary>
        internal const int ToDoTasksPerPage = 50;

        /// <summary>`SearchToDosDto.take` max.</summary>
        internal const int ToDoSearchTake = 20;

        internal const int ToDoProjectsTake = 50;
        internal const int ToDoProjectsMaxPages = 100;

        /// <summary>
        /// <c>GET /to-do?page=&amp;take=&amp;order=DESC&amp;sort_mode=[&amp;status=][&amp;project_id=]</c> — one page of
        /// one status bucket (web <c>getToDos</c>).
        /// </summary>
        internal async Task<ToDoPage> GetToDoPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId)
        {
            var items = new List<string> { "page=" + page, "take=" + ToDoTasksPerPage, "order=DESC", "sort_mode=" + ToDoSortModeWire.Of(sortMode) };
            if (status.HasValue) items.Add("status=" + ToDoStatusWire.Of(status.Value));
            if (!string.IsNullOrEmpty(projectId)) items.Add("project_id=" + Uri.EscapeDataString(projectId));
            var response = await SendJsonRequest("GET", ToDo + "?" + string.Join("&", items), null, "GetToDoPage");
            return response.IsSuccess ? ToDoJson.ReadPage(response.Body) : null;
        }

        /// <summary>
        /// <c>GET /to-do/search?title=&amp;take=</c>. The backend ranks by title and ignores every other filter;
        /// the screen filters the results itself, as the web does.
        /// </summary>
        internal async Task<IList<ToDoTask>> SearchToDos(string title)
        {
            string url = ToDo + "/search?title=" + Uri.EscapeDataString(title ?? "") + "&take=" + ToDoSearchTake;
            var response = await SendJsonRequest("GET", url, null, "SearchToDos");
            return response.IsSuccess ? ToDoJson.ReadTasks(response.Body) : null;
        }

        /// <summary>
        /// <c>PUT /to-do</c> — the one to-do write (upsert). The response carries the saved task, or on a
        /// failure the server's reason (<see cref="ToDoServerError"/>) for the toast.
        /// </summary>
        internal async Task<ApiResponse> UpsertToDo(IDictionary<string, object> body)
        {
            return await SendJsonRequest("PUT", ToDo, JObject.FromObject(body), "UpsertToDo");
        }

        /// <summary><c>DELETE /to-do?todo_id=</c>.</summary>
        internal async Task<bool> DeleteToDo(string id)
        {
            return (await SendJsonRequest("DELETE", ToDo + "?todo_id=" + Uri.EscapeDataString(id ?? ""), null, "DeleteToDo")).IsSuccess;
        }

        /// <summary>
        /// Every unfinished to-do: <c>GET /to-do?page=&amp;take=50&amp;order=DESC&amp;sort_mode=DUE_DATE&amp;exclude_completed=true</c>,
        /// page by page until the server says there is no next page — the Mac's forecast catalogue
        /// (<c>ToDoListQuery.forecast</c>). Null when any page fails or the pages don't add up: a partial
        /// catalogue would tell the Day Plan that tasks it simply didn't download are gone.
        /// </summary>
        internal async Task<IList<ToDoTask>> GetUnfinishedToDoCatalogue(int maxPages = 400)
        {
            var all = new List<ToDoTask>();
            var seen = new HashSet<string>();
            for (int page = 1; page <= maxPages; page++)
            {
                string url = ToDo + "?page=" + page + "&take=" + ToDoTasksPerPage + "&order=DESC&sort_mode="
                             + ToDoSortModeWire.Of(ToDoSortMode.DueDate) + "&exclude_completed=true";
                var response = await SendJsonRequest("GET", url, null, "GetUnfinishedToDoCatalogue", checkInternet: page == 1);
                ToDoPage result = response.IsSuccess ? ToDoJson.ReadPage(response.Body) : null;
                if (result == null) return null;
                int added = 0;
                foreach (ToDoTask task in result.Tasks)
                {
                    if (!string.IsNullOrEmpty(task.Id) && seen.Add(task.Id)) { all.Add(task); added++; }
                }
                if (!result.HasNextPage) return all;
                if (added == 0) return null;   // "next page" that repeats itself: stop rather than loop
            }
            return null;
        }

        /// <summary>Web <c>fetchAllProjects</c>: every active project, paging until <c>hasNextPage</c> is false. Null when the first page fails.</summary>
        internal async Task<IList<Project>> GetAllActiveToDoProjects()
        {
            var all = new List<Project>();
            var known = new HashSet<string>();
            for (int page = 1; page <= ToDoProjectsMaxPages; page++)
            {
                string url = ApiBase + "/projects?page=" + page + "&take=" + ToDoProjectsTake + "&order=DESC";
                var response = await SendJsonRequest("GET", url, null, "GetAllActiveToDoProjects", checkInternet: page == 1);
                var result = response.IsSuccess ? ToDoJson.ReadProjectsPage(response.Body) : null;
                if (result == null) return all.Count == 0 ? null : all;
                foreach (var project in result.Projects)
                {
                    if (known.Add(project.Id)) all.Add(project);
                }
                if (!result.HasNextPage || result.Projects.Count == 0) break;
            }
            return all;
        }

        /// <summary><c>POST /projects { name, description }</c> — from the editor's "Create “name”" row.</summary>
        internal Task<ApiResponse> CreateToDoProject(string name) => CreateRatedProject(name, null);

        /// <summary>
        /// <c>POST /projects { name, description[, outcome] }</c> — <c>outcome</c> on the server's /10 scale
        /// (a real column since backend#2385, stored as sent); omitted means unrated.
        /// </summary>
        internal async Task<ApiResponse> CreateRatedProject(string name, int? outcome)
        {
            var body = new JObject { ["name"] = name, ["description"] = "" };
            if (outcome.HasValue) body["outcome"] = outcome.Value;
            return await SendJsonRequest("POST", ApiBase + "/projects", body, "CreateToDoProject");
        }

        /// <summary>`ToDoPaging.projectsPerPage` / `projectTasksTake` on the Mac.</summary>
        internal const int ProjectsPerPage = 9;
        internal const int ProjectTasksTake = 20;

        /// <summary><c>GET /portfolio</c> — the server's per-project risk, which the "What matters" view groups.</summary>
        internal async Task<PortfolioSnapshot> GetPortfolio()
        {
            var response = await SendJsonRequest("GET", ApiBase + "/portfolio", null, "GetPortfolio");
            return response.IsSuccess ? ToDoJson.ReadPortfolio(response.Body) : null;
        }

        /// <summary>
        /// <c>GET /projects?page=&amp;take=&amp;order=DESC[&amp;search=][&amp;status=]</c>. Status is omitted for active,
        /// the server's default.
        /// </summary>
        internal async Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status)
        {
            string url = ApiBase + "/projects?page=" + page + "&take=" + ProjectsPerPage + "&order=DESC";
            if (!string.IsNullOrWhiteSpace(search)) url += "&search=" + Uri.EscapeDataString(search.Trim());
            if (status != ProjectLifecycleStatus.Active) url += "&status=" + ProjectLifecycleStatusWire.Of(status);
            var response = await SendJsonRequest("GET", url, null, "GetProjectsPage");
            return response.IsSuccess ? ToDoJson.ReadProjectsPage(response.Body) : null;
        }

        /// <summary><c>GET /projects/:id</c> — the full project: members, columns, counts.</summary>
        internal async Task<Project> GetProject(string id)
        {
            var response = await SendJsonRequest("GET", ApiBase + "/projects/" + Uri.EscapeDataString(id), null, "GetProject");
            return response.IsSuccess ? ToDoJson.ReadProject(response.Body) : null;
        }

        /// <summary>
        /// A project's tasks: <c>GET /projects/:id/todos</c>, or for a synced project the
        /// <c>GET /to-do?synced_project_id=</c> variant (web <c>fetchProjectDetailsTasks</c>).
        /// </summary>
        internal async Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int take = ProjectTasksTake, int page = 1)
        {
            string url = project.IsSynced
                ? ToDo + "?page=" + page + "&take=" + take + "&order=DESC&sort_mode=" + ToDoSortModeWire.Of(sortMode) + "&synced_project_id=" + Uri.EscapeDataString(project.SyncedProjectId)
                : ApiBase + "/projects/" + Uri.EscapeDataString(project.Id) + "/todos?page=" + page + "&take=" + take + "&sort_mode=" + ToDoSortModeWire.Of(sortMode);
            var response = await SendJsonRequest("GET", url, null, "GetProjectTasks");
            return response.IsSuccess ? ToDoJson.ReadPage(response.Body) : null;
        }

        /// <summary>Web <c>fetchProjectTodos</c>: the candidates a card's NEXT task is picked from.</summary>
        internal async Task<IList<ToDoTask>> GetProjectNextTaskCandidates(PortfolioFetchTarget target)
        {
            string url = ToDo + "?page=1&take=" + PortfolioNextTask.FetchTake + "&order=DESC&sort_mode=" + ToDoSortModeWire.Smart
                + (string.IsNullOrEmpty(target.SyncedProjectId)
                    ? "&project_id=" + Uri.EscapeDataString(target.ProjectId)
                    : "&synced_project_id=" + Uri.EscapeDataString(target.SyncedProjectId));
            var response = await SendJsonRequest("GET", url, null, "GetProjectNextTaskCandidates", checkInternet: false);
            return response.IsSuccess ? ToDoJson.ReadPage(response.Body)?.Tasks : null;
        }

        /// <summary><c>PUT /projects/:id</c> with a partial body (<c>{ status }</c>, <c>{ due_date }</c>…).</summary>
        internal async Task<ApiResponse> UpdateProject(string id, JObject body)
        {
            return await SendJsonRequest("PUT", ApiBase + "/projects/" + Uri.EscapeDataString(id), body, "UpdateProject");
        }

        /// <summary><c>DELETE /projects/:id</c>.</summary>
        internal async Task<bool> DeleteProject(string id)
        {
            return (await SendJsonRequest("DELETE", ApiBase + "/projects/" + Uri.EscapeDataString(id), null, "DeleteProject")).IsSuccess;
        }

        /// <summary>
        /// <c>POST /to-do/convert-brain-dump { contents }</c> — the AI turns free text into suggested tasks. Null
        /// when the call fails or the answer is unreadable.
        /// </summary>
        internal async Task<IList<BrainDumpTask>> ConvertBrainDump(string contents)
        {
            var response = await SendJsonRequest("POST", ToDo + "/convert-brain-dump", new JObject { ["contents"] = contents }, "ConvertBrainDump");
            return response.IsSuccess ? ToDoTools.ReadBrainDumpTasks(response.Body) : null;
        }

        private static string SavedWebsites { get { return ApiBase + "/saved-website"; } }

        internal async Task<IList<SavedWebsite>> GetSavedWebsites()
        {
            var response = await SendJsonRequest("GET", SavedWebsites, null, "GetSavedWebsites");
            return response.IsSuccess ? ToDoTools.ReadSavedWebsites(response.Body) : null;
        }

        /// <summary><c>POST /saved-website [{ url, title, note }]</c> — the body is an array.</summary>
        internal async Task<bool> AddSavedWebsite(string url, string note)
        {
            var body = new JArray { new JObject { ["url"] = url, ["title"] = "", ["note"] = note ?? "" } };
            return (await SendJsonRequest("POST", SavedWebsites, body, "AddSavedWebsite")).IsSuccess;
        }

        internal async Task<bool> DeleteSavedWebsite(string id)
        {
            return (await SendJsonRequest("DELETE", SavedWebsites + "?website_id=" + Uri.EscapeDataString(id ?? ""), null, "DeleteSavedWebsite")).IsSuccess;
        }

        /// <summary><c>PUT /saved-website/delete [ids]</c>.</summary>
        internal async Task<bool> DeleteSavedWebsites(IEnumerable<string> ids)
        {
            return (await SendJsonRequest("PUT", SavedWebsites + "/delete", new JArray(ids.Cast<object>().ToArray()), "DeleteSavedWebsites")).IsSuccess;
        }

        private static string TaskUrl(string taskId) => ToDo + "/" + Uri.EscapeDataString(taskId ?? "");

        /// <summary><c>GET /to-do/:id/comments?page=1&amp;take=50</c>.</summary>
        internal async Task<IList<ToDoComment>> GetToDoComments(string taskId)
        {
            var response = await SendJsonRequest("GET", TaskUrl(taskId) + "/comments?page=1&take=" + ToDoComments.CommentsTake, null, "GetToDoComments");
            return response.IsSuccess ? ToDoComments.ReadComments(response.Body) : null;
        }

        internal async Task<bool> AddToDoComment(string taskId, string content)
        {
            return (await SendJsonRequest("POST", TaskUrl(taskId) + "/comments", new JObject { ["content"] = content }, "AddToDoComment")).IsSuccess;
        }

        internal async Task<bool> UpdateToDoComment(string taskId, string commentId, string content)
        {
            return (await SendJsonRequest("PUT", TaskUrl(taskId) + "/comments/" + Uri.EscapeDataString(commentId), new JObject { ["content"] = content }, "UpdateToDoComment")).IsSuccess;
        }

        internal async Task<bool> DeleteToDoComment(string taskId, string commentId)
        {
            return (await SendJsonRequest("DELETE", TaskUrl(taskId) + "/comments/" + Uri.EscapeDataString(commentId), null, "DeleteToDoComment")).IsSuccess;
        }

        /// <summary>Reactions on the task itself (<paramref name="commentId"/> null) or on one of its comments.</summary>
        internal async Task<IList<ToDoReaction>> GetToDoReactions(string taskId, string commentId = null)
        {
            string url = commentId == null ? TaskUrl(taskId) + "/reactions" : TaskUrl(taskId) + "/comments/" + Uri.EscapeDataString(commentId) + "/reactions";
            var response = await SendJsonRequest("GET", url, null, "GetToDoReactions", checkInternet: false);
            return response.IsSuccess ? ToDoComments.ReadReactions(response.Body) : null;
        }

        internal async Task<bool> AddToDoReaction(string taskId, string emoji, string commentId = null)
        {
            string url = commentId == null ? TaskUrl(taskId) + "/reactions" : TaskUrl(taskId) + "/comments/" + Uri.EscapeDataString(commentId) + "/reactions";
            var response = await SendJsonRequest("POST", url, new JObject { ["emoji"] = emoji }, "AddToDoReaction");
            // 409 is "you have already reacted with this emoji": the reaction is there, which is what was asked.
            return response.IsSuccess || response.Status == 409;
        }

        internal async Task<bool> RemoveToDoReaction(string taskId, string emoji, string commentId = null)
        {
            string url = (commentId == null ? TaskUrl(taskId) + "/reactions" : TaskUrl(taskId) + "/comments/" + Uri.EscapeDataString(commentId) + "/reactions")
                       + "?emoji=" + Uri.EscapeDataString(emoji);
            var response = await SendJsonRequest("DELETE", url, null, "RemoveToDoReaction");
            // 404 is a reaction that is already gone.
            return response.IsSuccess || response.Status == 404;
        }

        /// <summary><c>GET /to-do/:id/attachments</c> — each row carries a short-lived <c>download_url</c>.</summary>
        internal async Task<IList<ToDoAttachment>> GetToDoAttachments(string taskId)
        {
            var response = await SendJsonRequest("GET", TaskUrl(taskId) + "/attachments", null, "GetToDoAttachments", checkInternet: false);
            return response.IsSuccess ? ToDoAttachments.ReadAttachments(response.Body) : null;
        }

        private const int AttachmentUploadTimeoutMinutes = 5;
        private const int ClientErrorStatusStart = 400;
        private const int ServerErrorStatusStart = 500;
        private const int RequestTimeoutStatus = 408;
        private const string AttachmentPath = "/attachments";
        private const string AttachmentUploadUrlPath = "/attachments/generate-upload-url";

        private const string AttachmentFileNameKey = "file_name";
        private const string AttachmentFileKey = "file_key";
        private const string AttachmentContentTypeKey = "content_type";
        private const string AttachmentFileSizeKey = "file_size";

        /// <summary>
        /// The three-step upload: a presigned URL, a PUT of the bytes to R2 (no Authorization header — the
        /// signature is the auth, and R2 rejects a second one), then the record. Returns the new attachment,
        /// or null when any step fails.
        /// </summary>
        internal Task<ToDoAttachment> UploadToDoAttachment(string taskId, string fileName, string contentType, byte[] bytes) =>
            UploadToDoAttachment(taskId, new ToDoDraftAttachment(fileName, contentType, bytes));

        internal Task<ToDoAttachment> UploadToDoAttachment(string taskId, ToDoDraftAttachment file) =>
            ToDoDraftAttachmentUpload.UploadAsync(file,
                async () =>
                {
                    var response = await SendJsonRequest("POST", TaskUrl(taskId) + AttachmentUploadUrlPath,
                        new JObject { [AttachmentFileNameKey] = file.FileName, [AttachmentContentTypeKey] = file.ContentType }, "GenerateAttachmentUploadUrl");
                    return response.IsSuccess ? ToDoAttachments.ReadTicket(response.Body) : null;
                },
                ticket => UploadAttachmentBytes(ticket, file),
                async () =>
                {
                    var response = await SendJsonRequest("POST", TaskUrl(taskId) + AttachmentPath, new JObject
                    {
                        [AttachmentFileNameKey] = file.FileName,
                        [AttachmentFileKey] = file.FileKey,
                        [AttachmentContentTypeKey] = file.ContentType,
                        [AttachmentFileSizeKey] = file.Bytes.LongLength,
                    }, "CreateToDoAttachment");
                    var created = response.IsSuccess ? ToDoAttachments.ReadAttachment(response.Body) : null;
                    bool rejected = response.Status >= ClientErrorStatusStart && response.Status < ServerErrorStatusStart
                                    && response.Status != RequestTimeoutStatus;
                    return new ToDoAttachmentRegistrationResult(created, rejected);
                },
                () => GetToDoAttachments(taskId));

        private static async Task<bool> UploadAttachmentBytes(ToDoAttachmentUploadTicket ticket, ToDoDraftAttachment file)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(AttachmentUploadTimeoutMinutes) })
                using (var content = new System.Net.Http.ByteArrayContent(file.Bytes))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    using (var put = await client.PutAsync(ticket.UploadUrl, content)) return put.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Focus_Bear.Utils.LogWriter.LogWrite("UploadToDoAttachment PUT Exception - " + ex.Message);
                return false;
            }
        }

        internal async Task<bool> DeleteToDoAttachment(string taskId, string attachmentId)
        {
            return (await SendJsonRequest("DELETE", TaskUrl(taskId) + "/attachments/" + Uri.EscapeDataString(attachmentId ?? ""), null, "DeleteToDoAttachment")).IsSuccess;
        }

        /// <summary><c>POST /to-do/generate-subtasks { task, language }</c> → <c>{ subtasks: [..] }</c>.</summary>
        internal async Task<IList<string>> GenerateSubtasks(string title, string language)
        {
            var response = await SendJsonRequest("POST", ToDo + "/generate-subtasks",
                                                 new JObject { ["task"] = title, ["language"] = language ?? "en" }, "GenerateSubtasks");
            if (!response.IsSuccess) return null;
            var subtasks = ToDoJson.Parse(response.Body)?["subtasks"] as JArray;
            if (subtasks == null) return null;
            var names = new List<string>();
            foreach (var item in subtasks)
            {
                if (item.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)item)) names.Add(((string)item).Trim());
            }
            return names;
        }
    }
}
