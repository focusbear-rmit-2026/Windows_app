using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>One page of <c>GET /to-do</c> (<c>PaginationDto&lt;ToDoResponse&gt;</c>).</summary>
    public sealed class ToDoPage
    {
        public ToDoPage(IList<ToDoTask> tasks, int receivedRowCount, bool hasNextPage, int? itemCount)
        {
            Tasks = tasks;
            ReceivedRowCount = receivedRowCount;
            HasNextPage = hasNextPage;
            ItemCount = itemCount;
        }

        public IList<ToDoTask> Tasks { get; }

        /// <summary>
        /// Rows the server sent, unreadable ones included. <see cref="ItemCount"/> counts rows on the
        /// server and <see cref="Tasks"/> counts rows we can render; this sits between them, which is what
        /// <see cref="ToDoSectionCount.HeaderCount"/> needs.
        /// </summary>
        public int ReceivedRowCount { get; }

        public bool HasNextPage { get; }
        public int? ItemCount { get; }
    }

    /// <summary><c>GET /portfolio</c> as the Projects tab reads it.</summary>
    public sealed class PortfolioSnapshot
    {
        public PortfolioSnapshot(IList<PortfolioProject> projects, bool isOvercommitted)
        {
            Projects = projects;
            IsOvercommitted = isOvercommitted;
        }

        public IList<PortfolioProject> Projects { get; }
        public bool IsOvercommitted { get; }
    }

    /// <summary>One page of <c>GET /projects</c> with the fields the task list reads.</summary>
    public sealed class ToDoProjectsPage
    {
        public ToDoProjectsPage(IList<Project> projects, bool hasNextPage)
        {
            Projects = projects;
            HasNextPage = hasNextPage;
        }

        public IList<Project> Projects { get; }
        public bool HasNextPage { get; }
    }

    /// <summary>
    /// Reads the to-do API's JSON into <see cref="ToDoTask"/> and <see cref="Project"/>, applying the Mac's
    /// decoding rules: a row that can't be read is dropped rather than failing the page, <c>duration</c>
    /// may be a number or a string, and a missing <c>title</c> / <c>created_at</c> reads as "".
    /// Timestamps stay as the strings the server sent — never re-formatted through DateTime.
    /// </summary>
    public static class ToDoJson
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

        /// <summary><c>{ data: [...], meta: { hasNextPage, itemCount } }</c>; null when the body is unreadable.</summary>
        public static ToDoPage ReadPage(string json)
        {
            if (!(Parse(json) is JObject page) || !(page["data"] is JArray rows)) return null;
            var tasks = ReadRows(rows);
            var meta = page["meta"] as JObject;
            return new ToDoPage(tasks, rows.Count, Bool(meta?["hasNextPage"]) ?? false, Int(meta?["itemCount"]));
        }

        /// <summary>A bare array of tasks (<c>GET /to-do/search</c>, <c>GET /to-do/by-ids</c>).</summary>
        public static IList<ToDoTask> ReadTasks(string json)
        {
            var token = Parse(json);
            if (token is JArray rows) return ReadRows(rows);
            // Tolerate a paged envelope here too; the search route has answered with both.
            if (token is JObject page && page["data"] is JArray data) return ReadRows(data);
            return null;
        }

        /// <summary>The saved task a <c>PUT /to-do</c> answers with; null when it doesn't decode.</summary>
        public static ToDoTask ReadTask(string json) => ReadTask(Parse(json) as JObject);

        public static ToDoTask ReadTask(JObject row)
        {
            string id = Text(row?["id"]);
            if (string.IsNullOrEmpty(id)) return null;
            var task = new ToDoTask
            {
                Id = id,
                Title = Text(row["title"]) ?? "",
                Details = Text(row["details"]),
                Objective = Text(row["objective"]),
                Status = Text(row["status"]) ?? ToDoStatusWire.NotStarted,
                DueDate = Text(row["due_date"]),
                CreatedAt = Text(row["created_at"]) ?? "",
                EisenhowerQuadrant = Int(row["eisenhower_quadrant"]),
                Outcome = Int(row["outcome"]),
                PerspirationLevel = Int(row["perspiration_level"]),
                TopScore = Double(row["top_score"]),
                Duration = Duration(row["duration"]),
                Icon = Text(row["icon"]),
                ProjectId = Text(row["project_id"]),
                SyncedProjectId = Text(row["synced_project_id"]),
                CustomStatusId = Text(row["custom_status_id"]),
                FocusType = Text(row["focus_type"]),
                ExternalTaskId = Text(row["external_task_id"]),
                AssigneeId = Text(row["assignee_id"]),
                AssignedMcpTokenId = Text(row["assigned_mcp_token_id"]),
            };
            if (row["assignee"] is JObject assignee && !string.IsNullOrEmpty(Text(assignee["id"])))
            {
                task.Assignee = new ToDoAssignee(Text(assignee["id"]), Text(assignee["username"]), Text(assignee["email"]));
            }
            if (row["tags"] is JArray tags)
            {
                task.Tags = tags.OfType<JObject>()
                    .Where(tag => Text(tag["id"]) != null)
                    .Select(tag => new ToDoTag(Text(tag["id"]), Text(tag["text"]) ?? ""))
                    .ToList();
            }
            if (row["subtasks"] is JArray subtasks)
            {
                task.Subtasks = subtasks.OfType<JObject>()
                    .Select(subtask => new ToDoSubtask(Text(subtask["name"]) ?? "", Bool(subtask["is_completed"]) ?? false, Text(subtask["id"])))
                    .ToList();
            }
            return task;
        }

        /// <summary>
        /// <c>GET /projects</c>. The list arrives as <c>data</c> or as the deprecated <c>projects</c>
        /// alias; the longer one wins, as on the Mac.
        /// </summary>
        public static ToDoProjectsPage ReadProjectsPage(string json)
        {
            if (!(Parse(json) is JObject page)) return null;
            var data = page["data"] as JArray ?? new JArray();
            var alias = page["projects"] as JArray ?? new JArray();
            var rows = alias.Count > data.Count ? alias : data;
            var projects = rows.OfType<JObject>().Select(ReadProject).Where(project => project != null).ToList();
            return new ToDoProjectsPage(projects, Bool((page["meta"] as JObject)?["hasNextPage"]) ?? false);
        }

        public static Project ReadProject(string json) => ReadProject(Parse(json) as JObject);

        public static Project ReadProject(JObject row)
        {
            string id = Text(row?["id"]);
            if (string.IsNullOrEmpty(id)) return null;
            var statuses = (row["custom_statuses"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(status => Text(status["id"]) != null)
                .Select(status => new ProjectCustomStatus(Text(status["id"]), Text(status["label"]) ?? "",
                                                          Int(status["order"]), Bool(status["should_complete_task"]),
                                                          Text(status["color"])));
            return new Project(id, Text(row["name"]) ?? "", statuses, Text(row["synced_project_id"]))
            {
                OwnerId = Text(row["owner_id"]),
                TaskCount = Int(row["task_count"]),
                UncompletedTaskCount = Int(row["uncompleted_task_count"]),
                Status = Text(row["status"]),
                Description = Text(row["description"]),
                DueDate = Text(row["due_date"]),
                Icon = Text(row["icon"]),
                Outcome = Int(row["outcome"]),
                Tags = ((row["metadata"] as JObject)?["tags"] as JArray ?? new JArray())
                    .Where(tag => tag.Type == JTokenType.String).Select(tag => (string)tag).ToList(),
                Members = (row["members"] as JArray ?? new JArray()).OfType<JObject>()
                    .Select(member => new ProjectMember
                    {
                        Id = Text(member["id"]),
                        UserId = Text(member["user_id"]),
                        Email = Text(member["email"]),
                        Role = Text(member["role"]),
                        InvitationStatusRaw = Text(member["invitation_status"]),
                        InvitationSentAt = Text(member["invitation_sent_at"]),
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// <c>GET /portfolio</c>: the server's risk rows (bucket, risk score, remaining hours, badges) and
        /// whether the plan is overcommitted. Null when the body is unreadable.
        /// </summary>
        public static PortfolioSnapshot ReadPortfolio(string json)
        {
            if (!(Parse(json) is JObject body) || !(body["projects"] is JArray rows)) return null;
            var projects = rows.OfType<JObject>()
                .Where(row => !string.IsNullOrEmpty(Text(row["id"])))
                .Select(row => new PortfolioProject
                {
                    Id = Text(row["id"]),
                    Name = Text(row["name"]) ?? "",
                    DueDate = Text(row["due_date"]),
                    Importance = Int(row["importance"]),
                    RequiredHours = Double(row["required_hours"]),
                    RiskScore = Double(row["risk_score"]),
                    BucketRaw = Text(row["bucket"]),
                    DaysUntilDue = Int(row["days_until_due"]),
                    Badges = (row["badges"] as JArray ?? new JArray()).Where(b => b.Type == JTokenType.String).Select(b => (string)b).ToList(),
                })
                .ToList();
            return new PortfolioSnapshot(projects, Bool(body["overcommitted"]) ?? false);
        }

        private static IList<ToDoTask> ReadRows(JArray rows) =>
            rows.Select(row => row as JObject).Select(ReadTask).Where(task => task != null).ToList();

        private static string Text(JToken token) =>
            token != null && token.Type == JTokenType.String ? (string)token : null;

        private static bool? Bool(JToken token) =>
            token != null && token.Type == JTokenType.Boolean ? (bool)token : (bool?)null;

        private static int? Int(JToken token)
        {
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.Integer: return (int)(long)token;
                case JTokenType.Float: return (int)(double)token;
                default: return null;
            }
        }

        private static double? Double(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return (double)token;
            if (token.Type == JTokenType.String
                && double.TryParse((string)token, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out double parsed)) return parsed;
            return null;
        }

        /// <summary><see cref="ToDoWire.LenientInt"/> over the token, absent staying absent.</summary>
        private static int? Duration(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) return null;
            object value = token.Type == JTokenType.String ? (object)(string)token
                         : token.Type == JTokenType.Integer ? (object)(long)token
                         : token.Type == JTokenType.Float ? (object)(double)token
                         : null;
            return ToDoWire.LenientInt(value);
        }
    }
}
