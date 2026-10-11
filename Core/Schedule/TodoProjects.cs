using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Which project a backend to-do belongs to, the way the Mac resolves it (DayPlanTasks.swift
    /// <c>TodoProjectFields</c> + <c>DayPlanTaskStore.projectName(for:)</c>): the to-do's
    /// <c>project_id</c> looked up in the user's <c>/projects</c>, else its first tag's text (the
    /// backend mirrors a native project as a tag, and ClickUp/Trello-synced to-dos only carry the tag).
    ///
    /// The bug it exists for (report 2026-09-25_09-44-27, "why do most of the tasks in the day plan not
    /// have projects associated"): Windows only ever read <c>tags[0].text</c>, so every to-do linked to a
    /// project by <c>project_id</c> alone showed under "No project", and a task already on the day never
    /// picked its project up afterwards.
    /// </summary>
    public static class TodoProjects
    {
        /// <summary>The to-do's native project id, or null.</summary>
        public static string ProjectId(JObject todo)
        {
            string id = todo == null ? null : (string)todo["project_id"];
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }

        /// <summary>An externally synced project (ClickUp / Trello). The backend nulls <c>project_id</c>
        /// for those, and the tag is the only name there is.</summary>
        public static bool HasSyncedExternalProject(JObject todo)
        {
            return todo != null && !string.IsNullOrWhiteSpace((string)todo["synced_project_id"]);
        }

        /// <summary>The first tag's text (<c>{ id, text }</c>; older payloads used <c>name</c>).</summary>
        public static string FirstTagText(JObject todo)
        {
            JArray tags = todo == null ? null : todo["tags"] as JArray;
            if (tags == null) return null;
            foreach (JToken tag in tags)
            {
                JObject t = tag as JObject;
                if (t == null) continue;
                string text = (string)t["text"] ?? (string)t["name"];
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            return null;
        }

        /// <summary>The project name to show for a to-do, or null when it has none we can name.</summary>
        public static string ProjectName(JObject todo, IReadOnlyDictionary<string, string> projectsById)
        {
            string id = ProjectId(todo);
            string name;
            if (id != null && projectsById != null && projectsById.TryGetValue(id, out name) && !string.IsNullOrWhiteSpace(name))
                return name;
            return FirstTagText(todo);
        }

        /// <summary>
        /// A task already on the day, refreshed from its backing to-do. The backend is the truth for what
        /// the to-do is linked to, so whenever it (or the task's recorded link) says there is a project, the
        /// name follows the backend — including a project renamed or relinked elsewhere. A name typed on
        /// this computer that the backend knows nothing about yet is left alone. A project id that can't be
        /// resolved yet (projects not loaded) never blanks a name.
        /// </summary>
        /// <returns>The name the task should now carry (null = no project), or <paramref name="currentName"/> unchanged.</returns>
        public static string RefreshedName(string currentName, string storedProjectId, JObject todo,
                                           IReadOnlyDictionary<string, string> projectsById)
        {
            string backendId = ProjectId(todo);
            bool backendLinked = backendId != null || HasSyncedExternalProject(todo);
            string resolved = ProjectName(todo, projectsById);
            if (resolved != null)
            {
                if (backendLinked || storedProjectId != null || string.IsNullOrWhiteSpace(currentName)) return resolved;
                return currentName;
            }
            // Nothing to name. The link was undone elsewhere only if we had recorded one and the backend now
            // has neither an id nor a tag; an unresolvable id keeps whatever the task shows.
            if (!backendLinked && storedProjectId != null) return null;
            return currentName;
        }

        /// <summary>One page of <c>/projects</c> (a bare array or <c>{ data | projects: [...] }</c>) as
        /// (id, name) pairs, plus the total the server reports (0 when it doesn't).</summary>
        public static List<KeyValuePair<string, string>> ParseProjectsPage(JToken root, out int totalCount)
        {
            totalCount = 0;
            JArray arr = root as JArray;
            JObject obj = root as JObject;
            if (arr == null && obj != null)
            {
                JArray data = obj["data"] as JArray ?? new JArray();
                JArray projects = obj["projects"] as JArray ?? new JArray();
                arr = projects.Count > data.Count ? projects : data;
                JToken total = obj["total_count"] ?? (obj["meta"] as JObject)?["total"];
                if (total != null && total.Type == JTokenType.Integer) totalCount = (int)total;
            }
            var result = new List<KeyValuePair<string, string>>();
            if (arr == null) return result;
            foreach (JObject p in arr.OfType<JObject>())
            {
                string id = (string)p["id"];
                string name = (string)p["name"] ?? (string)p["title"];
                if (string.IsNullOrWhiteSpace(name)) continue;
                result.Add(new KeyValuePair<string, string>(id ?? "", name));
            }
            return result;
        }
    }
}
