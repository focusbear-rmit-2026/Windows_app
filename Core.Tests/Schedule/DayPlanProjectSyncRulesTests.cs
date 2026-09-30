using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>DayPlanProjectRepairTests.swift</c>: which tasks need their project
    /// pushed to the backend, de-duplicating project names so one project is created per name, the
    /// once-per-10-minutes throttle, and the tag list mirrored to the web.
    /// </summary>
    [TestClass]
    public class DayPlanProjectSyncRulesTests
    {
        private static readonly ISet<string> KnownProjects =
            new HashSet<string> { "proj-thesis", "proj-house" };

        private static DayPlanTask Task(string title = "Task", string project = null,
                                        string sourceTaskId = "todo-1", string projectId = null,
                                        int? quadrant = 4, bool? hasSyncedExternalProject = null)
        {
            return new DayPlanTask(title, sourceTaskId: sourceTaskId, project: project, projectId: projectId,
                                   eisenhowerQuadrant: quadrant,
                                   hasSyncedExternalProject: hasSyncedExternalProject);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> Days(
            params KeyValuePair<string, DayPlanTask[]>[] days)
        {
            return days.ToDictionary(d => d.Key, d => (IReadOnlyList<DayPlanTask>)d.Value.ToList());
        }

        private static KeyValuePair<string, DayPlanTask[]> Day(string key, params DayPlanTask[] tasks)
        {
            return new KeyValuePair<string, DayPlanTask[]>(key, tasks);
        }

        // MARK: - Selector: which tasks need repair

        [TestMethod]
        public void LocalOnlyProjectNeedsRepair()
        {
            Assert.IsTrue(DayPlanProjectSyncRules.NeedsRepair(Task(project: "Thesis"), KnownProjects));
        }

        [TestMethod]
        public void TaskLinkedToKnownProjectDoesNotNeedRepair()
        {
            DayPlanTask linked = Task(project: "Thesis", projectId: "proj-thesis");
            Assert.IsFalse(DayPlanProjectSyncRules.NeedsRepair(linked, KnownProjects));
        }

        [TestMethod]
        public void TaskLinkedToProjectMissingFromRemoteListNeedsRepair()
        {
            DayPlanTask stale = Task(project: "Thesis", projectId: "proj-deleted");
            Assert.IsTrue(DayPlanProjectSyncRules.NeedsRepair(stale, KnownProjects));
        }

        [TestMethod]
        public void TaskWithoutProjectDoesNotNeedRepair()
        {
            Assert.IsFalse(DayPlanProjectSyncRules.NeedsRepair(Task(project: null), KnownProjects));
            Assert.IsFalse(DayPlanProjectSyncRules.NeedsRepair(Task(project: "   "), KnownProjects));
        }

        [TestMethod]
        public void PureLocalTaskWithoutTodoIsOutOfScope()
        {
            DayPlanTask unminted = Task(project: "Thesis", sourceTaskId: null);
            Assert.IsFalse(DayPlanProjectSyncRules.NeedsRepair(unminted, KnownProjects));
        }

        [TestMethod]
        public void ExternallySyncedTodoIsLeftToTheIntegration()
        {
            DayPlanTask clickUp = Task(project: "ClickUp List", hasSyncedExternalProject: true);
            Assert.IsFalse(DayPlanProjectSyncRules.NeedsRepair(clickUp, KnownProjects));
        }

        [TestMethod]
        public void CandidatesScanEveryDayInDateOrder()
        {
            var byDay = Days(
                Day("2026-09-08", Task("today linked", project: "House", projectId: "proj-house"),
                                  Task("today local", project: "Thesis")),
                Day("2026-08-30", Task("old local", project: "Garden"), Task("old no project")),
                Day("2026-09-10", Task("future local", project: "thesis")));

            IReadOnlyList<DayPlanProjectSyncRules.Candidate> found =
                DayPlanProjectSyncRules.Candidates(byDay, KnownProjects);

            CollectionAssert.AreEqual(new List<string> { "old local", "today local", "future local" },
                                      found.Select(c => c.Task.Title).ToList());
            CollectionAssert.AreEqual(new List<string> { "2026-08-30", "2026-09-08", "2026-09-10" },
                                      found.Select(c => c.DayKey).ToList());
        }

        [TestMethod]
        public void SplitSeparatesTasksThatCannotBeLinkedYet()
        {
            var byDay = Days(Day("2026-09-08",
                                 Task("has quadrant", project: "Thesis"),
                                 Task("imported before quadrant", project: "Thesis", quadrant: null)));

            DayPlanProjectSyncRules.SplitCandidates split = DayPlanProjectSyncRules.Split(
                DayPlanProjectSyncRules.Candidates(byDay, new HashSet<string>()));

            CollectionAssert.AreEqual(new List<string> { "has quadrant" },
                                      split.Linkable.Select(c => c.Task.Title).ToList());
            CollectionAssert.AreEqual(new List<string> { "imported before quadrant" },
                                      split.MissingQuadrant.Select(c => c.Task.Title).ToList());
        }

        // MARK: - Dedupe by project name

        [TestMethod]
        public void TenTasksWithTheSameProjectYieldOneName()
        {
            DayPlanTask[] tasks = Enumerable.Range(1, 10)
                .Select(i => Task("t" + i, project: i % 2 == 0 ? "thesis" : "Thesis"))
                .ToArray();

            IReadOnlyList<string> names = DayPlanProjectSyncRules.DistinctProjectNames(
                DayPlanProjectSyncRules.Candidates(Days(Day("2026-09-08", tasks)), new HashSet<string>()));

            CollectionAssert.AreEqual(new List<string> { "Thesis" }, names.ToList(), "first-seen casing wins");
        }

        [TestMethod]
        public void DistinctNamesKeepFirstSeenOrderAndTrimWhitespace()
        {
            DayPlanTask[] tasks = { Task(project: " House "), Task(project: "Thesis"), Task(project: "house") };

            IReadOnlyList<string> names = DayPlanProjectSyncRules.DistinctProjectNames(
                DayPlanProjectSyncRules.Candidates(Days(Day("2026-09-08", tasks)), new HashSet<string>()));

            CollectionAssert.AreEqual(new List<string> { "House", "Thesis" }, names.ToList());
        }

        // MARK: - Throttling

        [TestMethod]
        public void FirstEverPassIsDue()
        {
            Assert.IsTrue(DayPlanProjectSyncRules.IsDue(DateTimeOffset.Now, null));
        }

        [TestMethod]
        public void PassWithinTenMinutesIsNotDue()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            Assert.IsFalse(DayPlanProjectSyncRules.IsDue(now, now.AddMinutes(-9)));
            Assert.IsFalse(DayPlanProjectSyncRules.IsDue(now, now));
        }

        [TestMethod]
        public void PassAfterTenMinutesIsDue()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            Assert.IsTrue(DayPlanProjectSyncRules.IsDue(now, now.AddMinutes(-10)));
            Assert.IsTrue(DayPlanProjectSyncRules.IsDue(now, now.AddHours(-3)));
        }

        [TestMethod]
        public void MinimumIntervalIsTenMinutes()
        {
            Assert.AreEqual(600, DayPlanProjectSyncRules.MinimumInterval.TotalSeconds);
        }

        // MARK: - Tag mirroring (web `ensureProjectTaskTags`)

        [TestMethod]
        public void EnsureProjectTagAppendsExactlyOneProjectTagAfterLabels()
        {
            var labels = new List<DayPlanTaskTag>
            {
                new DayPlanTaskTag("tag-urgent", "urgent"),
                new DayPlanTaskTag("proj-thesis", "Old name"),
            };

            IReadOnlyList<DayPlanTaskTag> tags =
                DayPlanProjectSyncRules.EnsureProjectTag(labels, "proj-thesis", "Thesis");

            CollectionAssert.AreEqual(
                new List<DayPlanTaskTag>
                {
                    new DayPlanTaskTag("tag-urgent", "urgent"),
                    new DayPlanTaskTag("proj-thesis", "Thesis"),
                },
                tags.ToList());
        }

        [TestMethod]
        public void EnsureProjectTagWithoutProjectKeepsLabelsUntouched()
        {
            var labels = new List<DayPlanTaskTag> { new DayPlanTaskTag("tag-urgent", "urgent") };

            CollectionAssert.AreEqual(labels,
                                      DayPlanProjectSyncRules.EnsureProjectTag(labels, null, null).ToList());
        }
    }

    /// <summary>
    /// Merging a duplicate project archived the source, and the Day Plan kept naming it. Because
    /// <c>GET /projects</c> hides archived projects, the next sync could not find the name and created
    /// the project again — so the user merged "Classroom mode" three times and collected three archived
    /// copies of it (report 2026-09-18_21-46-48).
    /// </summary>
    [TestClass]
    public class DayPlanProjectMergeFollowTests
    {
        private static DayPlanTask Task(string title, string project, string projectId = null,
                                        IReadOnlyList<DayPlanTaskTag> tags = null,
                                        bool? hasSyncedExternalProject = null)
        {
            return new DayPlanTask(title, sourceTaskId: "todo-" + title, project: project, projectId: projectId,
                                   tags: tags, eisenhowerQuadrant: 4,
                                   hasSyncedExternalProject: hasSyncedExternalProject);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> Day(string key,
                                                                                   params DayPlanTask[] tasks)
        {
            return new Dictionary<string, IReadOnlyList<DayPlanTask>> { { key, tasks.ToList() } };
        }

        [TestMethod]
        public void TasksNamingTheMergedAwayProjectFollowItToTheTarget()
        {
            var before = Day("2026-09-18",
                             Task("A", "Classroom mode", "old-id"),
                             Task("B", "Unrelated", "other-id"));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", "new-id");

            Assert.AreEqual("classroom-mode", after["2026-09-18"][0].Project);
            Assert.AreEqual("new-id", after["2026-09-18"][0].ProjectId);
            Assert.AreEqual("Unrelated", after["2026-09-18"][1].Project, "An unrelated project is untouched");
            Assert.AreEqual("other-id", after["2026-09-18"][1].ProjectId);
        }

        /// <summary>
        /// The name lookup that would recreate the project compares case-insensitively, so this must
        /// too, or the very casing difference that made the duplicate survives the merge that removed it.
        /// </summary>
        [TestMethod]
        public void MatchingIgnoresCaseAndSurroundingSpace()
        {
            var before = Day("d", Task("A", "  CLASSROOM MODE "));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "classroom mode", "classroom-mode", "new-id");

            Assert.AreEqual("classroom-mode", after["d"][0].Project);
        }

        [TestMethod]
        public void AnExternallySyncedProjectIsLeftWhereTheMergeLeftIt()
        {
            var before = Day("d", Task("A", "Classroom mode", "old-id", hasSyncedExternalProject: true));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", "new-id");

            Assert.AreEqual("Classroom mode", after["d"][0].Project);
            Assert.AreEqual("old-id", after["d"][0].ProjectId);
        }

        [TestMethod]
        public void MergingAProjectIntoItselfChangesNothing()
        {
            var before = Day("d", Task("A", "Classroom mode", "id"));

            CollectionAssert.AreEqual(
                before["d"].ToList(),
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom mode", "id")["d"].ToList());
            CollectionAssert.AreEqual(
                before["d"].ToList(),
                DayPlanProjectSyncRules.FollowingMerge(before, "   ", "classroom-mode", "id")["d"].ToList());
        }

        [TestMethod]
        public void TheProjectTagFollowsSoTheWebSeesTheSameProject()
        {
            var tags = new List<DayPlanTaskTag>
            {
                new DayPlanTaskTag("old-id", "Classroom mode"),
                new DayPlanTaskTag("label-1", "urgent"),
            };
            var before = Day("d", Task("A", "Classroom mode", "old-id", tags));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", "new-id");

            IReadOnlyList<DayPlanTaskTag> moved = after["d"][0].Tags;
            Assert.AreEqual("classroom-mode", moved.Single(t => t.Id == "new-id").Text);
            Assert.IsFalse(moved.Any(t => t.Id == "old-id"), "The retired project must not stay on the task");
            Assert.IsTrue(moved.Any(t => t.Id == "label-1"), "Ordinary labels survive");
        }

        /// <summary>
        /// The Mac's tasks are value-type structs, so a merge there cannot possibly disturb the caller's
        /// copy. Here they are objects, so "leave the input alone" is a real property to hold — a
        /// merge that mutated in place would rewrite whatever else is holding those tasks.
        /// </summary>
        [TestMethod]
        public void TheInputIsNeverMutated()
        {
            DayPlanTask original = Task("A", "Classroom mode", "old-id");
            var before = Day("d", original);

            DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", "new-id");

            Assert.AreEqual("Classroom mode", original.Project);
            Assert.AreEqual("old-id", original.ProjectId);
            Assert.AreSame(original, before["d"][0]);
        }

        /// <summary>
        /// The merge target may have no id yet (the project is known by name only until the next fetch).
        /// Writing null is deliberate — it is what makes the next sync link the name rather than trust a
        /// stale id.
        /// </summary>
        [TestMethod]
        public void AMergeWithNoTargetIdClearsTheStaleLink()
        {
            var before = Day("d", Task("A", "Classroom mode", "old-id"));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", null);

            Assert.AreEqual("classroom-mode", after["d"][0].Project);
            Assert.IsNull(after["d"][0].ProjectId);
        }

        /// <summary>
        /// The whole point: after the merge has been followed, the task no longer names a project the
        /// backend hides, so the repair scan stops proposing to create it again.
        /// </summary>
        [TestMethod]
        public void AFollowedMergeStopsTheNextScanRecreatingTheArchivedProject()
        {
            var remote = new HashSet<string> { "new-id" };
            var before = Day("d", Task("A", "Classroom mode", "old-id"));
            Assert.AreEqual(1, DayPlanProjectSyncRules.Candidates(before, remote).Count,
                            "before the merge is followed, the scan wants to push the archived name");

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> after =
                DayPlanProjectSyncRules.FollowingMerge(before, "Classroom mode", "classroom-mode", "new-id");

            Assert.AreEqual(0, DayPlanProjectSyncRules.Candidates(after, remote).Count);
        }
    }
}
