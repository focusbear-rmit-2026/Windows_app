using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>Why one task could not be moved into the merge target.</summary>
    public enum ProjectMergeSkip
    {
        /// <summary>
        /// The task belongs to an external provider (Zoho, Jira). Its project membership is owned by
        /// that provider; re-pointing it here would be undone on the next sync.
        /// </summary>
        ExternallySynced,
    }

    /// <summary>
    /// One task's re-assignment: the `PUT /to-do` body that moves it, or the reason it stays put.
    ///
    /// The Mac returns Swift's <c>Result</c>; C# has no such type in netstandard2.0, so the two cases
    /// are one value a caller must ask before reading — a plain null body would let a caller move a
    /// task it was told not to.
    /// </summary>
    public sealed class ProjectMergeReassignment
    {
        private ProjectMergeReassignment(IDictionary<string, object> body, ProjectMergeSkip? skip)
        {
            Body = body;
            Skip = skip;
        }

        /// <summary>The write to send, or null when the task was skipped.</summary>
        public IDictionary<string, object> Body { get; }

        /// <summary>Why the task was left behind, or null when it moves.</summary>
        public ProjectMergeSkip? Skip { get; }

        public bool Moves => Body != null;

        internal static ProjectMergeReassignment Moving(IDictionary<string, object> body)
        {
            return new ProjectMergeReassignment(body, null);
        }

        internal static ProjectMergeReassignment Skipped(ProjectMergeSkip skip)
        {
            return new ProjectMergeReassignment(null, skip);
        }
    }

    /// <summary>
    /// What a finished merge tells the user: how many tasks moved, how many were left behind, and
    /// whether the source may now be archived.
    /// </summary>
    public struct ProjectMergeOutcome : IEquatable<ProjectMergeOutcome>
    {
        public ProjectMergeOutcome(int moved, int skipped, int failed)
        {
            Moved = moved;
            Skipped = skipped;
            Failed = failed;
        }

        public int Moved { get; }
        public int Skipped { get; }
        public int Failed { get; }

        /// <summary>Only a fully emptied project is archived — one still holding tasks is not empty.</summary>
        public bool ArchivesSource => Failed == 0 && Skipped == 0;

        public bool Succeeded => Failed == 0;

        public bool Equals(ProjectMergeOutcome other)
        {
            return Moved == other.Moved && Skipped == other.Skipped && Failed == other.Failed;
        }

        public override bool Equals(object obj) => obj is ProjectMergeOutcome other && Equals(other);

        public override int GetHashCode() => (Moved * 397 ^ Skipped) * 397 ^ Failed;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "moved={0} skipped={1} failed={2}",
                                 Moved, Skipped, Failed);
        }
    }

    /// <summary>
    /// Merging one project into another (Mac report 2026-09-11_07-31-36: "Should also be able to merge
    /// a project with another one — I have several with similar names. That project's tasks would get
    /// transferred to this one.").
    ///
    /// There is no server-side merge, so a merge is a task-by-task `PUT /to-do` that re-points each of
    /// the source's tasks at the target, followed by archiving the emptied source. Everything deciding
    /// WHAT is sent lives here, so the rules are tested without the API.
    /// </summary>
    public static class ProjectMerge
    {
        /// <summary>
        /// Projects offered as a destination: active, writable, never the source itself, and never a
        /// synced project — its tasks live in the external system. <paramref name="query"/> filters by
        /// name, ignoring case and accents, as the picker's search box does.
        /// </summary>
        public static IList<Project> Destinations(IEnumerable<Project> projects, Project source, string userId,
                                                  string query = "")
        {
            string trimmed = (query ?? string.Empty).Trim();
            return (projects ?? Enumerable.Empty<Project>())
                .Where(project => IsWritableNativeProject(project, userId)
                               && project.Id != source.Id
                               && ProjectNames.Matches(project.Name, trimmed))
                .OrderBy(project => project.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        /// <summary>A merge may only be started between two native projects the user can write to.</summary>
        public static bool CanMerge(Project source, Project target, string userId)
        {
            if (source.Id == target.Id || source.IsSynced || target.IsSynced) return false;

            return ProjectPermissions.CanManageMembers(ProjectPermissions.ViewerRole(source, userId))
                && ProjectPermissions.CanAddTask(ProjectPermissions.ViewerRole(target, userId));
        }

        /// <summary>
        /// The `PUT /to-do` body that moves one task into <paramref name="target"/>, or the reason it
        /// was left behind.
        ///
        /// Three fields have to change together. `project_id` is the link itself; the project TAG is
        /// what the to-do list and Day Plan actually display, so a stale source tag would keep showing
        /// the old project; and `custom_status_id` names a column on the SOURCE's board, which does not
        /// exist on the target — carrying it over would leave the task in a column the target cannot
        /// show.
        ///
        /// <paramref name="source"/> may be null so a task in no project yet can be filed into one;
        /// there is then simply no source tag to strip.
        /// </summary>
        public static ProjectMergeReassignment Reassignment(ToDoTask task, Project source, Project target)
        {
            if (task.IsExternallySynced) return ProjectMergeReassignment.Skipped(ProjectMergeSkip.ExternallySynced);

            // A task off the wire carries its assignee only as a relation (backend#2414), and the body sends
            // assignee_id always — null clears it. Resolve it first, or the merge unassigns the task.
            ToDoTask outgoing = task.Copy();
            outgoing.AssigneeId = ToDoAssigneeChoices.AssigneeId(task.AssigneeId, task.Assignee?.Id);
            IDictionary<string, object> body = ToDoUpsertBody.Make(outgoing);
            body[ToDoUpsertBody.Keys.ProjectId] = target.Id;
            body[ToDoUpsertBody.Keys.Tags] = ToDoUpsertBody.TagBodies(RepointedTags(task, source, target));
            body[ToDoUpsertBody.Keys.CustomStatusId] = target.UsesCustomStatuses
                ? target.OrderedStatuses.Select(status => status.Id).FirstOrDefault()
                : null;
            return ProjectMergeReassignment.Moving(body);
        }

        /// <summary>The source's project tag replaced by the target's; every other label survives.</summary>
        private static IList<ToDoTag> RepointedTags(ToDoTask task, Project source, Project target)
        {
            List<ToDoTag> tags = task.TagList
                .Where(tag => source == null || tag.Id != source.Id)
                .ToList();
            if (!tags.Any(tag => tag.Id == target.Id)) tags.Add(new ToDoTag(target.Id, target.Name));
            return tags;
        }

        private static bool IsWritableNativeProject(Project project, string userId)
        {
            return !project.IsSynced
                && project.LifecycleStatus == ProjectLifecycleStatus.Active
                && ProjectPermissions.CanAddTask(ProjectPermissions.ViewerRole(project, userId));
        }
    }

    /// <summary>
    /// How a project's name is searched, in one place: the merge picker and the portfolio's search box
    /// must not come to disagree about whether "Grant" finds "Grànt Application".
    /// </summary>
    internal static class ProjectNames
    {
        /// <summary>Case- and accent-insensitive, mirroring the Mac's diacritic-insensitive search.</summary>
        private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        /// <summary>A blank query matches everything, so the list behaves as before until the user types.</summary>
        internal static bool Matches(string name, string trimmedQuery)
        {
            if (string.IsNullOrEmpty(trimmedQuery)) return true;
            if (name == null) return false;

            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(name, trimmedQuery, SearchOptions) >= 0;
        }
    }
}
