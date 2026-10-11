using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// `TO_DO_STATUS` on the wire. A task's <c>status</c> may also be a free-form external label
    /// (Zoho, Jira), so callers read <see cref="ToDoTask.DefaultStatus"/> rather than comparing the
    /// raw string.
    /// </summary>
    public enum ToDoStatus
    {
        NotStarted,
        InProgress,
        Completed,
    }

    /// <summary>The wire spellings of <see cref="ToDoStatus"/>, in one place.</summary>
    public static class ToDoStatusWire
    {
        public const string NotStarted = "NOT_STARTED";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";

        public static string Of(ToDoStatus status)
        {
            switch (status)
            {
                case ToDoStatus.NotStarted: return NotStarted;
                case ToDoStatus.InProgress: return InProgress;
                case ToDoStatus.Completed: return Completed;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        /// <summary>The status this wire value names, or null when it is an external/custom label.</summary>
        public static ToDoStatus? Parse(string raw)
        {
            switch (raw)
            {
                case NotStarted: return ToDoStatus.NotStarted;
                case InProgress: return ToDoStatus.InProgress;
                case Completed: return ToDoStatus.Completed;
                default: return null;
            }
        }
    }

    /// <summary>One label chip on a task. A project links to its tasks through a tag of its own id.</summary>
    public sealed class ToDoTag : IEquatable<ToDoTag>
    {
        public ToDoTag(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public string Id { get; }
        public string Text { get; }

        public bool Equals(ToDoTag other) => other != null && Id == other.Id && Text == other.Text;
        public override bool Equals(object obj) => Equals(obj as ToDoTag);
        public override int GetHashCode() => (Id?.GetHashCode() ?? 0) * 397 ^ (Text?.GetHashCode() ?? 0);
        public override string ToString() => "#" + Text;
    }

    /// <summary>
    /// The portable subset of the API's `ToDoResponse` — the fields the rules in this namespace read.
    /// The Windows app's own task model fills one of these; nothing here knows about the transport.
    /// </summary>
    public sealed class ToDoTask
    {
        public string Id { get; set; }
        public string Title { get; set; }

        /// <summary>Raw wire status: a <see cref="ToDoStatusWire"/> value, or an external label.</summary>
        public string Status { get; set; }

        /// <summary>ISO-8601 instant or plain <c>yyyy-MM-dd</c>; see <see cref="ToDoDates.Parse"/>.</summary>
        public string DueDate { get; set; }

        public string CreatedAt { get; set; }
        public int? Outcome { get; set; }
        public int? PerspirationLevel { get; set; }

        /// <summary>
        /// What the server last materialised. Only a fallback — the server scores on write and has no
        /// cron, so this goes stale the moment a due date passes. See <see cref="ToDoScoring.TopScoreOf"/>.
        /// </summary>
        public double? TopScore { get; set; }

        public string ProjectId { get; set; }

        /// <summary>The project board column this task sits in, when its project has custom columns.</summary>
        public string CustomStatusId { get; set; }

        public IList<ToDoTag> Tags { get; set; } = new List<ToDoTag>();

        /// <summary>Rich text from the web editor; the native screens show <see cref="ToDoHtml"/>'s plain text.</summary>
        public string Details { get; set; }

        public string Objective { get; set; }

        /// <summary>1-4; absent rows default to <see cref="ToDo.EisenhowerQuadrant.DoIt"/>.</summary>
        public int? EisenhowerQuadrant { get; set; }

        /// <summary>Estimated work, in seconds. The wire sends it as a number OR a string — see <see cref="ToDoWire"/>.</summary>
        public int? Duration { get; set; }

        public IList<ToDoSubtask> Subtasks { get; set; } = new List<ToDoSubtask>();

        /// <summary>Either an emoji glyph or the hex codepoints the web's picker writes.</summary>
        public string Icon { get; set; }

        public string FocusType { get; set; }

        /// <summary>Set when the task is owned by an external provider (Zoho, Jira).</summary>
        public string ExternalTaskId { get; set; }

        /// <summary>Set when the task's project mirrors an external provider's project.</summary>
        public string SyncedProjectId { get; set; }

        /// <summary>
        /// The flat assignee id. <c>GET /to-do</c> does not send this key at all (backend#2414) — read it
        /// through <see cref="ToDoAssigneeChoices.AssigneeId"/>, never on its own.
        /// </summary>
        public string AssigneeId { get; set; }

        /// <summary>An agent's claim on the task. Nothing in the desktop apps can read or set it.</summary>
        public string AssignedMcpTokenId { get; set; }

        /// <summary>The assignee as a relation, which is the shape <c>GET /to-do</c> actually sends.</summary>
        public ToDoAssignee Assignee { get; set; }

        /// <summary>
        /// The default-status bucket this task belongs to. An unknown or external status counts as
        /// "doing", mirroring the web's <c>getEffectiveTaskStatus</c> fallback.
        /// </summary>
        public ToDoStatus DefaultStatus => ToDoStatusWire.Parse(Status) ?? ToDoStatus.InProgress;

        public bool IsCompleted => DefaultStatus == ToDoStatus.Completed;

        public IList<ToDoTag> TagList => Tags ?? new List<ToDoTag>();

        public IList<ToDoSubtask> SubtaskList => Subtasks ?? new List<ToDoSubtask>();

        /// <summary>The estimate a write sends: an absent duration is zero, as the Mac's `durationSeconds` is.</summary>
        public int DurationSeconds => Duration ?? 0;

        /// <summary>A task the external provider owns; its project membership is not ours to change.</summary>
        public bool IsExternallySynced =>
            !string.IsNullOrEmpty(SyncedProjectId) || !string.IsNullOrEmpty(ExternalTaskId);

        /// <summary>Web `refineTaskTopMetrics`: clamped into the level range, defaulting to the floor.</summary>
        public int EffectiveOutcome => ToDoScoring.ClampLevel(Outcome ?? ToDoScoring.MinLevel);

        public int EffectivePerspiration => ToDoScoring.ClampLevel(PerspirationLevel ?? ToDoScoring.MinLevel);

        /// <summary>
        /// A value-semantics copy. The Mac's rules are written against Swift structs, where
        /// <c>var updated = task</c> already copies; the ports keep that by copying explicitly, so a
        /// caller's task is never mutated behind its back.
        /// </summary>
        public ToDoTask Copy()
        {
            var copy = (ToDoTask)MemberwiseClone();
            copy.Tags = new List<ToDoTag>(TagList);
            copy.Subtasks = new List<ToDoSubtask>(SubtaskList);
            return copy;
        }
    }

    /// <summary>`ProjectStatus` — one column of a project's kanban board.</summary>
    public sealed class ProjectCustomStatus
    {
        public ProjectCustomStatus(string id, string label, int? order = null,
                                   bool? shouldCompleteTask = null, string color = null)
        {
            Id = id;
            Label = label;
            Order = order;
            ShouldCompleteTask = shouldCompleteTask;
            Color = color;
        }

        public string Id { get; }
        public string Label { get; }
        public string Color { get; }
        public int? Order { get; }

        /// <summary>Nullable on the wire; an absent flag means "this column does not finish tasks".</summary>
        public bool? ShouldCompleteTask { get; }

        public bool Completes => ShouldCompleteTask ?? false;
    }

    /// <summary>The portable subset of a project: enough to reason about its board and its tasks.</summary>
    public sealed class Project
    {
        public Project(string id, string name, IEnumerable<ProjectCustomStatus> customStatuses = null,
                       string syncedProjectId = null)
        {
            Id = id;
            Name = name;
            CustomStatuses = (customStatuses ?? Enumerable.Empty<ProjectCustomStatus>()).ToList();
            SyncedProjectId = syncedProjectId;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Set when the project mirrors an external provider's (Zoho, Jira) project.</summary>
        public string SyncedProjectId { get; }

        public IList<ProjectCustomStatus> CustomStatuses { get; }

        /// <summary>
        /// The fields below arrive on some payloads and not others, and no rule needs all of them at
        /// once, so they are set through an object initialiser rather than swelling the constructor
        /// every caller already uses.
        /// </summary>
        public string OwnerId { get; set; }

        public IList<ProjectMember> Members { get; set; } = new List<ProjectMember>();

        /// <summary>How many to-dos the project holds, as `GET /projects` reports it.</summary>
        public int? TaskCount { get; set; }

        /// <summary>How many of them are still open, when the server says.</summary>
        public int? UncompletedTaskCount { get; set; }

        public string Description { get; set; }

        /// <summary>ISO-8601 or <c>yyyy-MM-dd</c>, as sent; see <see cref="ToDoDates.Parse"/>.</summary>
        public string DueDate { get; set; }

        /// <summary>An emoji, or the hex codepoints the web's picker stores.</summary>
        public string Icon { get; set; }

        /// <summary>The project's rating on the 1-10 wire scale; null when unrated.</summary>
        public int? Outcome { get; set; }

        /// <summary>`metadata.tags`, already folded by the server.</summary>
        public IList<string> Tags { get; set; } = new List<string>();

        /// <summary>Raw `status`; absent on legacy payloads, which means active.</summary>
        public string Status { get; set; }

        public IList<ProjectMember> MemberList => Members ?? new List<ProjectMember>();

        public ProjectLifecycleStatus LifecycleStatus => ProjectLifecycleStatusWire.Parse(Status);

        public bool IsSynced => !string.IsNullOrEmpty(SyncedProjectId);

        /// <summary>A synced project's columns belong to the provider, so its board is never custom.</summary>
        public bool UsesCustomStatuses => !IsSynced && CustomStatuses.Count > 0;

        public IList<ProjectCustomStatus> OrderedStatuses => ToDoOrdering.ByOrder(CustomStatuses);
    }

    /// <summary>
    /// The one place columns are put in board order. Several rules sort by <c>order</c> and must agree
    /// about a column with none — an absent order sorts as if it were the first.
    /// </summary>
    internal static class ToDoOrdering
    {
        internal const int MissingOrder = 0;

        internal static IList<ProjectCustomStatus> ByOrder(IEnumerable<ProjectCustomStatus> statuses)
        {
            return (statuses ?? Enumerable.Empty<ProjectCustomStatus>())
                .OrderBy(status => status.Order ?? MissingOrder)
                .ToList();
        }
    }
}
