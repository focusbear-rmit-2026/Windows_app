using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Pure rules behind the Day Plan's project repair pass, ported from the Mac's
    /// <c>DayPlanProjectRepair.swift</c>.
    ///
    /// The repair exists because a project typed in the Day Plan used to stay in local storage — no
    /// backend project was created and the backing to-do's <c>project_id</c> was never set — so it was
    /// invisible on web and mobile. The same happens going forward whenever a project push fails
    /// (offline, token not ready).
    /// </summary>
    public static class DayPlanProjectSyncRules
    {
        /// <summary>Minimum gap between two repair passes.</summary>
        public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(10);

        /// <summary>A task needing repair, with the <c>yyyy-MM-dd</c> day it is stored under.</summary>
        public sealed class Candidate : IEquatable<Candidate>
        {
            public Candidate(string dayKey, DayPlanTask task)
            {
                DayKey = dayKey;
                Task = task;
            }

            public string DayKey { get; }
            public DayPlanTask Task { get; }

            public bool Equals(Candidate other)
            {
                return !ReferenceEquals(other, null) && DayKey == other.DayKey && Equals(Task, other.Task);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Candidate);
            }

            public override int GetHashCode()
            {
                unchecked { return ((DayKey == null ? 0 : DayKey.GetHashCode()) * 397) ^ Task.GetHashCode(); }
            }
        }

        /// <summary>
        /// A task needs repair when it names a project but its backing to-do is not known to point at a
        /// project that exists on the backend. A task without a to-do cannot be repaired here (the mint
        /// path pushes its project once it has one); an externally synced to-do's project belongs to
        /// ClickUp / Trello, and the backend nulls <c>project_id</c> for those anyway.
        /// </summary>
        public static bool NeedsRepair(DayPlanTask task, ISet<string> remoteProjectIds)
        {
            if (task.SourceTaskId == null) return false;
            if (string.IsNullOrWhiteSpace(task.Project)) return false;
            if (task.HasSyncedExternalProject == true) return false;
            if (task.ProjectId == null) return true;
            return !(remoteProjectIds ?? new HashSet<string>()).Contains(task.ProjectId);
        }

        /// <summary>
        /// Follows a finished project merge into the Day Plan's own copy of the data.
        ///
        /// The merge moves the backing to-dos on the server and then archives the emptied source, but a
        /// Day Plan task stores its project as a NAME, not an id. <c>GET /projects</c> defaults to
        /// active only, so a task still naming the archived source misses the name lookup on its next
        /// sync and <c>POST /projects</c> creates that project again — which is how merging one
        /// duplicate produces an endless supply of archived ones (report 2026-09-18_21-46-48: three
        /// archived "Classroom mode"). Rewriting the name is what stops the next sync asking for it.
        ///
        /// A to-do whose project belongs to ClickUp/Trello is left alone; the merge did not move it
        /// either. Matching is case-insensitive on the trimmed name, the same comparison the name lookup
        /// uses to decide the project already exists.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> FollowingMerge(
            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> tasksByDay,
            string source, string target, string targetId)
        {
            string wanted = (source ?? string.Empty).Trim();
            if (wanted.Length == 0) return tasksByDay;
            if (string.Equals(wanted, (target ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return tasksByDay;
            }

            return tasksByDay.ToDictionary(
                day => day.Key,
                day => (IReadOnlyList<DayPlanTask>)day.Value.Select(task =>
                {
                    if (task.HasSyncedExternalProject == true) return task;
                    string name = task.Project == null ? null : task.Project.Trim();
                    if (name == null || !string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) return task;

                    IReadOnlyList<DayPlanTaskTag> tags = task.Tags;
                    if (tags != null)
                    {
                        // EnsureProjectTag can only drop a tag matching the id it is given, so the
                        // retired project's own tag has to go explicitly — otherwise the web keeps
                        // showing the merged-away project beside the one it was merged into.
                        IReadOnlyList<DayPlanTaskTag> withoutSource =
                            tags.Where(t => t.Id != task.ProjectId).ToList();
                        tags = EnsureProjectTag(withoutSource, targetId, target);
                    }
                    return task.WithProject(target, targetId, tags);
                }).ToList());
        }

        /// <summary>Every task needing repair, by day then stored order, so passes are predictable.</summary>
        public static IReadOnlyList<Candidate> Candidates(
            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> tasksByDay, ISet<string> remoteProjectIds)
        {
            return tasksByDay.Keys
                .OrderBy(day => day, StringComparer.Ordinal)
                .SelectMany(day => tasksByDay[day]
                    .Where(task => NeedsRepair(task, remoteProjectIds))
                    .Select(task => new Candidate(day, task)))
                .ToList();
        }

        /// <summary>
        /// Candidates whose to-do can be PUT (quadrant known — the upsert requires it and would
        /// otherwise clobber it) versus those whose project can only be created, not linked, until a
        /// hydrate backfills the quadrant.
        /// </summary>
        public static SplitCandidates Split(IEnumerable<Candidate> candidates)
        {
            List<Candidate> all = candidates.ToList();
            return new SplitCandidates(
                all.Where(c => c.Task.EisenhowerQuadrant.HasValue).ToList(),
                all.Where(c => !c.Task.EisenhowerQuadrant.HasValue).ToList());
        }

        public sealed class SplitCandidates
        {
            public SplitCandidates(IReadOnlyList<Candidate> linkable, IReadOnlyList<Candidate> missingQuadrant)
            {
                Linkable = linkable;
                MissingQuadrant = missingQuadrant;
            }

            public IReadOnlyList<Candidate> Linkable { get; }
            public IReadOnlyList<Candidate> MissingQuadrant { get; }
        }

        /// <summary>
        /// Distinct project names across the candidates, case-insensitively, keeping the first-seen
        /// casing and order — so ten tasks tagged <c>#thesis</c> / <c>#Thesis</c> create ONE project.
        /// </summary>
        public static IReadOnlyList<string> DistinctProjectNames(IEnumerable<Candidate> candidates)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> names = new List<string>();
            foreach (Candidate candidate in candidates)
            {
                string name = candidate.Task.Project == null ? null : candidate.Task.Project.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (seen.Add(name.ToLowerInvariant())) names.Add(name);
            }
            return names;
        }

        /// <summary>Whether a pass may run now: never more than once per interval (null = never ran).</summary>
        public static bool IsDue(DateTimeOffset now, DateTimeOffset? lastRun, TimeSpan? minimumInterval = null)
        {
            if (!lastRun.HasValue) return true;
            return now - lastRun.Value >= (minimumInterval ?? MinimumInterval);
        }

        /// <summary>
        /// The tag list a project link sends, mirroring the web dashboard's <c>ensureProjectTaskTags</c>:
        /// the to-do's label tags plus exactly one <c>{ id: projectId, text: projectName }</c> tag. With
        /// no project id (clearing) the labels are returned as they are.
        /// </summary>
        public static IReadOnlyList<DayPlanTaskTag> EnsureProjectTag(IReadOnlyList<DayPlanTaskTag> tags,
                                                                     string projectId, string projectName)
        {
            if (projectId == null) return tags;
            List<DayPlanTaskTag> labels = tags.Where(t => t.Id != projectId).ToList();
            labels.Add(new DayPlanTaskTag(projectId, projectName ?? string.Empty));
            return labels;
        }
    }
}
