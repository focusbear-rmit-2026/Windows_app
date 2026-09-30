using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// A to-do tag as the backend stores it (<c>{ id, text }</c>). Snapshotted from the to-do payload on
    /// import so a project link can echo the to-do's label tags back — the <c>PUT /to-do</c> upsert
    /// replaces the whole tag list, so sending only the project tag would drop them.
    /// </summary>
    public sealed class DayPlanTaskTag : IEquatable<DayPlanTaskTag>
    {
        public DayPlanTaskTag(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public string Id { get; }
        public string Text { get; }

        public bool Equals(DayPlanTaskTag other)
        {
            return !ReferenceEquals(other, null) && Id == other.Id && Text == other.Text;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DayPlanTaskTag);
        }

        public override int GetHashCode()
        {
            unchecked { return ((Id == null ? 0 : Id.GetHashCode()) * 397) ^ (Text == null ? 0 : Text.GetHashCode()); }
        }

        public override string ToString()
        {
            return "{" + Id + ", " + Text + "}";
        }
    }

    /// <summary>
    /// One planned task for a given day.
    ///
    /// Only the fields the ported rules actually read are here; the Mac's struct carries a good deal
    /// more (notes, subtasks, attachment counts, assignee) that belongs with the UI that shows it.
    ///
    /// The Mac's version is a STRUCT, so every Swift assignment is a copy. This is a class, so the rules
    /// that "modify" a task (the merge follow-through) return <see cref="WithProject"/> copies and never
    /// touch the caller's objects — a difference the tests pin explicitly.
    /// </summary>
    public sealed class DayPlanTask : IEquatable<DayPlanTask>
    {
        /// <summary>
        /// Eisenhower quadrant sent when a Day Plan task has none stored. The product does not use the
        /// quadrant anywhere, so this is a filler the upsert requires rather than a meaningful value. It is 1
        /// (Do it), what the web and the Mac's To-do model read a missing quadrant as; 4 would be the Delete box.
        /// </summary>
        public const int NeutralQuadrant = 1;

        public const int DefaultDurationMinutes = 15;

        public DayPlanTask(string title, string id = null, int durationMinutes = DefaultDurationMinutes,
                           bool done = false, string sourceTaskId = null, string blockKey = null,
                           string project = null, string projectId = null,
                           IReadOnlyList<DayPlanTaskTag> tags = null, bool? hasSyncedExternalProject = null,
                           int? eisenhowerQuadrant = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
            Title = title;
            DurationMinutes = durationMinutes;
            Done = done;
            SourceTaskId = sourceTaskId;
            BlockKey = blockKey;
            Project = project;
            ProjectId = projectId;
            Tags = tags;
            HasSyncedExternalProject = hasSyncedExternalProject;
            EisenhowerQuadrant = eisenhowerQuadrant;
        }

        public string Id { get; }
        public string Title { get; }
        public int DurationMinutes { get; }
        public bool Done { get; }

        /// <summary>Links back to a backend to-do; null for from-scratch tasks.</summary>
        public string SourceTaskId { get; }

        /// <summary>Schedule block this task is nested under; null = unassigned.</summary>
        public string BlockKey { get; }

        /// <summary>Project NAME (from the to-do's tag, or typed as <c>#name</c>); null = no project.</summary>
        public string Project { get; }

        /// <summary>
        /// The backend project the backing to-do is KNOWN to point at (<c>to-do.project_id</c>), recorded
        /// on import and after a successful project link. null = never linked / unknown — with a
        /// <see cref="Project"/> name set, that is exactly what the repair pass scans for.
        /// </summary>
        public string ProjectId { get; }

        /// <summary>The to-do's tags as last seen from the backend; null = unknown (tags are then
        /// omitted so the backend keeps what it has).</summary>
        public IReadOnlyList<DayPlanTaskTag> Tags { get; }

        /// <summary>The to-do belongs to an externally synced project (ClickUp / Trello). The backend
        /// nulls <c>project_id</c> for those, so its tag must never become a native project.</summary>
        public bool? HasSyncedExternalProject { get; }

        /// <summary>Captured at import; required to echo back on the upsert without clobbering it. null
        /// for from-scratch tasks and tasks imported before the field existed.</summary>
        public int? EisenhowerQuadrant { get; }

        /// <summary>
        /// A copy pointed at another project — the Swift <c>var moved = task; moved.project = …</c>
        /// idiom, made explicit because this is a reference type. Every argument is SET, not merged:
        /// a null <paramref name="projectId"/> means "no known backend project", which is a real state
        /// the merge follow-through has to be able to write.
        /// </summary>
        public DayPlanTask WithProject(string project, string projectId, IReadOnlyList<DayPlanTaskTag> tags)
        {
            return new DayPlanTask(Title, Id, DurationMinutes, Done, SourceTaskId, BlockKey,
                                   project, projectId, tags, HasSyncedExternalProject,
                                   EisenhowerQuadrant);
        }

        public bool Equals(DayPlanTask other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;
            return Id == other.Id && Title == other.Title && DurationMinutes == other.DurationMinutes
                   && Done == other.Done && SourceTaskId == other.SourceTaskId && BlockKey == other.BlockKey
                   && Project == other.Project && ProjectId == other.ProjectId
                   && HasSyncedExternalProject == other.HasSyncedExternalProject
                   && EisenhowerQuadrant == other.EisenhowerQuadrant && TagsEqual(Tags, other.Tags);
        }

        private static bool TagsEqual(IReadOnlyList<DayPlanTaskTag> a, IReadOnlyList<DayPlanTaskTag> b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            return a.SequenceEqual(b);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DayPlanTask);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id == null ? 0 : Id.GetHashCode();
                hash = (hash * 397) ^ (Title == null ? 0 : Title.GetHashCode());
                hash = (hash * 397) ^ Done.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return Title + (Project == null ? string.Empty : " #" + Project);
        }
    }
}
