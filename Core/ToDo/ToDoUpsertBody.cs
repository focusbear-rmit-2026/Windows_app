using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// The body of `PUT /to-do`.
    ///
    /// Web `persistToDoTask` PUTs the whole task; this sends the writable subset of `CreateToDoDto`,
    /// leaving out the read-only fields (`top_score`, `assignee`, `external_task_status`) the server
    /// owns. The difference between a key sent as JSON null and a key left out entirely is the whole
    /// point of this class: the backend's `hasDtoProperty` CLEARS a field sent as null and leaves it
    /// alone when the key is absent.
    ///
    /// The Mac builds a <c>[String: Any]</c> and has to spell null as <c>NSNull()</c>, because a Swift
    /// dictionary cannot hold nil. A C# dictionary can, so here a present key with a null value IS the
    /// JSON null and <c>ContainsKey</c> is what tells "clear this" from "don't touch this". Serialise
    /// it with a serialiser that writes null values (Newtonsoft's default) or the distinction is lost
    /// on the way out.
    /// </summary>
    public static class ToDoUpsertBody
    {
        /// <summary>Every key this body can carry, so no call site spells one as a literal.</summary>
        public static class Keys
        {
            public const string Id = "id";
            public const string Title = "title";
            public const string Details = "details";
            public const string Objective = "objective";
            public const string Status = "status";
            public const string EisenhowerQuadrant = "eisenhower_quadrant";
            public const string Tags = "tags";
            public const string Subtasks = "subtasks";
            public const string Duration = "duration";
            public const string CustomStatusId = "custom_status_id";
            public const string ProjectId = "project_id";
            public const string DueDate = "due_date";
            public const string Outcome = "outcome";
            public const string PerspirationLevel = "perspiration_level";
            public const string Icon = "icon";
            public const string FocusType = "focus_type";
            public const string ExternalTaskId = "external_task_id";
            public const string SyncedProjectId = "synced_project_id";
            public const string AssigneeId = "assignee_id";
            public const string AssignedMcpTokenId = "assigned_mcp_token_id";
        }

        /// <summary>The keys of one `{ id, text }` tag, which is exactly the web's `Tag`.</summary>
        public static class TagKeys
        {
            public const string Id = "id";
            public const string Text = "text";
        }

        /// <summary>The keys of one subtask — the backend's canonical shape, with no `id`.</summary>
        public static class SubtaskKeys
        {
            public const string Name = "name";
            public const string IsCompleted = "is_completed";
        }

        /// <param name="clearOutcome">
        /// Send an explicit null outcome. An unrated task carries no outcome and normally omits the
        /// key; this is how a user who DELETES a rating gets it cleared server-side.
        /// </param>
        public static IDictionary<string, object> Make(ToDoTask task, bool clearOutcome = false)
        {
            var body = new Dictionary<string, object>
            {
                { Keys.Id, task.Id },
                { Keys.Title, task.Title },
                { Keys.Details, task.Details ?? string.Empty },
                { Keys.Objective, task.Objective ?? string.Empty },
                { Keys.Status, task.Status },
                { Keys.EisenhowerQuadrant, task.EisenhowerQuadrant ?? (int)ToDo.EisenhowerQuadrant.DoIt },
                { Keys.Tags, TagBodies(task.TagList) },
                { Keys.Subtasks, SubtaskBodies(task.SubtaskList) },
                { Keys.Duration, task.DurationSeconds },
                { Keys.CustomStatusId, NullIfEmpty(task.CustomStatusId) },
                { Keys.ProjectId, NullIfEmpty(task.ProjectId) },

                // An explicit null clears the date; the DTO's @IsDate would reject an empty string.
                { Keys.DueDate, NullIfEmpty(task.DueDate) },

                // Explicit null clears the assignment, and nothing else. The picker has an "Unassigned"
                // option, so null here really is the user's choice — which is why a caller holding a
                // task straight off the wire must resolve the flat field and the `assignee` relation
                // BEFORE it gets here (see ToDoDraft.Editable). Doing that resolution inside this
                // method would make null ambiguous and break Unassigned.
                { Keys.AssigneeId, NullIfEmpty(task.AssigneeId) },
            };

            if (task.Outcome.HasValue) body[Keys.Outcome] = task.Outcome.Value;
            else if (clearOutcome) body[Keys.Outcome] = null;

            if (task.PerspirationLevel.HasValue) body[Keys.PerspirationLevel] = task.PerspirationLevel.Value;

            AddIfPresent(body, Keys.Icon, task.Icon);
            AddIfPresent(body, Keys.FocusType, task.FocusType);
            AddIfPresent(body, Keys.ExternalTaskId, task.ExternalTaskId);
            AddIfPresent(body, Keys.SyncedProjectId, task.SyncedProjectId);

            // Not so for the MCP token: nothing in the desktop apps can read or set it and `GET /to-do`
            // never returns it, so an absent value only ever means "this client never knew". Sending
            // null unconditionally wiped an agent's claim on the task every time a human pressed Save.
            AddIfPresent(body, Keys.AssignedMcpTokenId, task.AssignedMcpTokenId);

            return body;
        }

        /// <summary>A tag is exactly <c>{ id, text }</c> — anything else is not the web's `Tag`.</summary>
        public static IList<IDictionary<string, string>> TagBodies(IEnumerable<ToDoTag> tags)
        {
            return tags
                .Select(tag => (IDictionary<string, string>)new Dictionary<string, string>
                {
                    { TagKeys.Id, tag.Id },
                    { TagKeys.Text, tag.Text },
                })
                .ToList();
        }

        private static IList<IDictionary<string, object>> SubtaskBodies(IEnumerable<ToDoSubtask> subtasks)
        {
            return subtasks
                .Select(subtask => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    { SubtaskKeys.Name, subtask.Name },
                    { SubtaskKeys.IsCompleted, subtask.IsCompleted },
                })
                .ToList();
        }

        private static void AddIfPresent(IDictionary<string, object> body, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) body[key] = value;
        }

        private static object NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
