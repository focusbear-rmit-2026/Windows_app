using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// One accordion section of the to-do list. <see cref="Status"/> is null for a section that came
    /// from a project's own board column rather than from a default status.
    /// </summary>
    public sealed class ToDoSection
    {
        public ToDoSection(string id, string title, ToDoStatus? status, IList<ToDoTask> tasks)
        {
            Id = id;
            Title = title;
            Status = status;
            Tasks = tasks;
        }

        public string Id { get; }
        public string Title { get; }
        public ToDoStatus? Status { get; }
        public IList<ToDoTask> Tasks { get; }
    }

    /// <summary>
    /// Web `partitionCustomTasksForTodosPage` + `mergeCustomStatusSectionsByLabel` +
    /// `buildTodosPageDisplayTasksByStatus`: which section of the list a task is drawn in.
    ///
    /// A task sitting in a project column NAMED like a default status ("To Do", "Doing", "Done") stays
    /// in that default section; any other project column becomes a section of its own, merged by label
    /// across every project that has one, so two projects' "Review" columns are one "Review" section.
    /// A task completed elsewhere always reads as done wherever its stale column points — the same
    /// rule <see cref="ProjectKanban.ColumnId"/> draws the board with.
    /// </summary>
    public static class ToDoSectioning
    {
        /// <summary>Web default order: Doing, To do, Done.</summary>
        public static readonly IList<ToDoStatus> DefaultOrder =
            new[] { ToDoStatus.InProgress, ToDoStatus.NotStarted, ToDoStatus.Completed };

        /// <summary>Prefix of a merged custom column's section id, keeping it out of the wire statuses' namespace.</summary>
        public const string CustomSectionIdPrefix = "custom__";

        /// <summary>
        /// Web alias map: a project column spelled any of these ways folds into that default section
        /// instead of earning a section of its own.
        /// </summary>
        private static readonly IDictionary<ToDoStatus, string[]> Aliases = new Dictionary<ToDoStatus, string[]>
        {
            { ToDoStatus.NotStarted, new[] { "to do", "todo", "not started", "not-started" } },
            { ToDoStatus.InProgress, new[] { "doing", "in progress", "in-progress", "inprogress" } },
            { ToDoStatus.Completed, new[] { "done", "completed", "finished" } },
        };

        /// <summary>The form two labels are compared in: trimmed and case-folded.</summary>
        public static string Normalized(string label)
        {
            return (label ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static IList<ToDoSection> Build(IEnumerable<ToDoTask> tasks, IEnumerable<Project> projects,
                                               ToDoSortMode sortMode, IDictionary<ToDoStatus, string> sectionTitles,
                                               DateTime now)
        {
            IDictionary<string, string> labelsByStatusId = new Dictionary<string, string>(StringComparer.Ordinal);
            var completingStatusIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Project project in projects ?? Enumerable.Empty<Project>())
            {
                foreach (ProjectCustomStatus status in project.CustomStatuses)
                {
                    labelsByStatusId[status.Id] = status.Label;
                    if (status.Completes) completingStatusIds.Add(status.Id);
                }
            }

            IDictionary<string, ToDoStatus> aliasLookup = BuildAliasLookup(sectionTitles);

            var byStatus = new Dictionary<ToDoStatus, List<ToDoTask>>();
            var custom = new List<CustomBucket>();

            foreach (ToDoTask task in tasks ?? Enumerable.Empty<ToDoTask>())
            {
                string columnId = task.CustomStatusId;
                string label;
                if (string.IsNullOrEmpty(columnId) || !labelsByStatusId.TryGetValue(columnId, out label))
                {
                    Bucket(byStatus, task.DefaultStatus).Add(task);
                    continue;
                }

                // Completed elsewhere while the custom id still names an open column: Done, not the column.
                if (task.IsCompleted && !completingStatusIds.Contains(columnId))
                {
                    Bucket(byStatus, ToDoStatus.Completed).Add(task);
                    continue;
                }

                string key = Normalized(label);
                ToDoStatus aliased;
                if (aliasLookup.TryGetValue(key, out aliased))
                {
                    Bucket(byStatus, aliased).Add(task);
                    continue;
                }

                CustomBucket bucket = custom.FirstOrDefault(entry => entry.Key == key);
                if (bucket == null)
                {
                    bucket = new CustomBucket(key, label.Trim());
                    custom.Add(bucket);
                }
                bucket.Tasks.Add(task);
            }

            var sections = DefaultOrder
                .Select(status => new ToDoSection(ToDoStatusWire.Of(status), SectionTitle(sectionTitles, status), status,
                                                  ToDoSorting.Sort(Unique(Bucket(byStatus, status)), sortMode, now)))
                .ToList();

            IEnumerable<CustomBucket> orderedCustom = custom
                .OrderBy(entry => entry.Label, StringComparer.InvariantCultureIgnoreCase);
            foreach (CustomBucket entry in orderedCustom)
            {
                sections.Add(new ToDoSection(CustomSectionIdPrefix + entry.Key, entry.Label, null,
                                             ToDoSorting.Sort(Unique(entry.Tasks), sortMode, now)));
            }
            return sections;
        }

        /// <summary>
        /// The caller's own section headings alias too, so a column named exactly what the heading says
        /// folds into it whatever language the app is running in.
        /// </summary>
        private static IDictionary<string, ToDoStatus> BuildAliasLookup(IDictionary<ToDoStatus, string> sectionTitles)
        {
            var lookup = new Dictionary<string, ToDoStatus>(StringComparer.Ordinal);
            foreach (KeyValuePair<ToDoStatus, string[]> entry in Aliases)
            {
                foreach (string name in entry.Value) lookup[name] = entry.Key;
            }
            foreach (KeyValuePair<ToDoStatus, string> entry in sectionTitles ?? EmptyTitles)
            {
                lookup[Normalized(entry.Value)] = entry.Key;
            }
            return lookup;
        }

        private static readonly IDictionary<ToDoStatus, string> EmptyTitles = new Dictionary<ToDoStatus, string>();

        private static string SectionTitle(IDictionary<ToDoStatus, string> titles, ToDoStatus status)
        {
            string title;
            if (titles != null && titles.TryGetValue(status, out title)) return title;
            return ToDoStatusWire.Of(status);
        }

        private static List<ToDoTask> Bucket(IDictionary<ToDoStatus, List<ToDoTask>> buckets, ToDoStatus status)
        {
            List<ToDoTask> bucket;
            if (!buckets.TryGetValue(status, out bucket))
            {
                bucket = new List<ToDoTask>();
                buckets[status] = bucket;
            }
            return bucket;
        }

        /// <summary>
        /// The same to-do can reach one section from two fetches (its status bucket and its project),
        /// and a duplicated row would be counted as well as drawn twice.
        /// </summary>
        private static IList<ToDoTask> Unique(IEnumerable<ToDoTask> tasks)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return tasks.Where(task => seen.Add(task.Id)).ToList();
        }

        private sealed class CustomBucket
        {
            internal CustomBucket(string key, string label)
            {
                Key = key;
                Label = label;
            }

            internal string Key { get; }
            internal string Label { get; }
            internal List<ToDoTask> Tasks { get; } = new List<ToDoTask>();
        }
    }

    /// <summary>
    /// The number beside a section heading.
    ///
    /// It used to be the server's `meta.itemCount` for that status bucket, which made the heading and
    /// the rows two different answers to the same question: "Doing 1" could sit above "No tasks in
    /// this section". They disagree because a fetched row does not have to end up in the section it
    /// was fetched for — <see cref="ToDoSectioning.Build"/> re-partitions by the project column a task
    /// carries, so an IN_PROGRESS task in a column named "Blocked" leaves Doing for a section of its
    /// own — and because a row the client could not decode never becomes a row at all.
    ///
    /// So the server total is trusted only for what it alone knows: the rows still unfetched behind
    /// paging. Everything already delivered is counted by what the section renders. "To do 145" with
    /// 50 loaded still reads 145; a Doing bucket whose only task moved elsewhere now reads 0, and the
    /// heading can no longer promise a task the user cannot reach.
    /// </summary>
    public static class ToDoSectionCount
    {
        /// <param name="rendered">Rows this section actually draws.</param>
        /// <param name="serverItemCount">`meta.itemCount` for the bucket, or null when unpaged.</param>
        /// <param name="rowsReceived">Rows the server sent for the bucket, undecodable ones included.</param>
        public static int HeaderCount(int rendered, int? serverItemCount, int rowsReceived)
        {
            if (!serverItemCount.HasValue) return rendered;
            return rendered + Math.Max(0, serverItemCount.Value - rowsReceived);
        }
    }
}
