using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Merging one project into another, ported from the Mac's merge cases
    /// (report 2026-09-11_07-31-36: "I have several with similar names").
    /// </summary>
    [TestClass]
    public class ProjectMergeTests
    {
        private static Project Source() => Project(id: "src", name: "Grant application");

        private static IList<IDictionary<string, string>> TagsOf(IDictionary<string, object> body)
        {
            return (IList<IDictionary<string, string>>)body[ToDoUpsertBody.Keys.Tags];
        }

        [TestMethod]
        public void DestinationsExcludeTheSourceSyncedAndInactiveProjects()
        {
            Project source = Source();
            var all = new[]
            {
                source,
                Project(id: "a", name: "Grant Application (old)"),
                Project(id: "b", name: "House"),
                Project(id: "c", name: "Grant synced", syncedProjectId: "zoho-1"),
                Project(id: "d", name: "Grant archived", lifecycle: ProjectLifecycleStatus.Archived),
                Project(id: "e", name: "Grant of a stranger", ownerId: "someone-else"),
            };

            CollectionAssert.AreEqual(new[] { "a", "b" },
                                      ProjectIds(ProjectMerge.Destinations(all, source, "me")),
                                      "only active, native, writable projects other than the source");
            CollectionAssert.AreEqual(new[] { "a" },
                                      ProjectIds(ProjectMerge.Destinations(all, source, "me", query: "grant")),
                                      "the search filters by name, case-insensitively");
        }

        [TestMethod]
        public void DestinationsAreListedByName()
        {
            Project source = Source();
            var all = new[]
            {
                source,
                Project(id: "z", name: "zebra"),
                Project(id: "a", name: "Apples"),
            };

            CollectionAssert.AreEqual(new[] { "a", "z" }, ProjectIds(ProjectMerge.Destinations(all, source, "me")));
        }

        [TestMethod]
        public void CanMergeRequiresManagingTheSourceAndWritingToTheTarget()
        {
            Project source = Project(id: "src", name: "Source");

            Assert.IsTrue(ProjectMerge.CanMerge(source, Project(id: "t", name: "Target"), "me"));
            Assert.IsFalse(ProjectMerge.CanMerge(source, source, "me"), "a project cannot merge into itself");
            Assert.IsFalse(ProjectMerge.CanMerge(source, Project(id: "t", name: "T", syncedProjectId: "zoho-1"), "me"),
                           "a synced project's tasks belong to the external tool");
            Assert.IsFalse(ProjectMerge.CanMerge(Project(id: "src", name: "S", ownerId: "someone-else"),
                                                 Project(id: "t", name: "T"), "me"),
                           "only someone who can manage the source may empty it");
        }

        [TestMethod]
        public void AMemberMayNotEmptyAProjectTheyOnlyBelongTo()
        {
            Project source = Project(id: "src", name: "S", ownerId: "someone-else",
                                     members: new[] { Member("me", ProjectMemberRole.Member) });

            Assert.IsFalse(ProjectMerge.CanMerge(source, Project(id: "t", name: "T"), "me"));
        }

        [TestMethod]
        public void ReassignmentKeepsAnAssigneeTheListOnlySentAsARelation()
        {
            // GET /to-do and GET /projects/:id/todos carry `assignee: { id }` and no flat assignee_id
            // (backend#2414). The body always sends assignee_id, and null CLEARS it — so a merge that
            // read the flat field alone unassigned every assigned task it moved.
            ToDoTask task = Task("t1", projectId: "src", tags: new[] { new ToDoTag("src", "Source") });
            task.Assignee = new ToDoAssignee("u9", "sam");

            ProjectMergeReassignment result = ProjectMerge.Reassignment(task, Project(id: "src", name: "Source"),
                                                                        Project(id: "dst", name: "Target"));

            Assert.AreEqual("u9", result.Body[ToDoUpsertBody.Keys.AssigneeId]);
            Assert.IsNull(task.AssigneeId, "the task read is not mutated");
        }

        [TestMethod]
        public void ReassignmentRepointsTheProjectTagAndClearsTheOldColumn()
        {
            Project source = Project(id: "src", name: "Source");
            Project target = Project(id: "dst", name: "Target", columns: new[] { Column("col-1", "To do", 0) });

            ToDoTask task = Task("t1", projectId: "src", customStatusId: "source-column",
                                 tags: new[] { new ToDoTag("src", "Source"), new ToDoTag("label", "#deep") });
            task.Title = "Fix references";

            ProjectMergeReassignment result = ProjectMerge.Reassignment(task, source, target);

            Assert.IsTrue(result.Moves);
            Assert.AreEqual("dst", result.Body[ToDoUpsertBody.Keys.ProjectId]);
            Assert.AreEqual("col-1", result.Body[ToDoUpsertBody.Keys.CustomStatusId],
                            "the task lands in the target's first column");
            CollectionAssert.AreEqual(new[] { "label", "dst" },
                                      TagsOf(result.Body).Select(tag => tag[ToDoUpsertBody.TagKeys.Id]).ToArray(),
                                      "the source's project tag is replaced; other labels survive");
            Assert.AreEqual("Fix references", result.Body[ToDoUpsertBody.Keys.Title],
                            "the rest of the task is sent unchanged");
        }

        [TestMethod]
        public void ReassignmentClearsTheColumnWhenTheTargetHasNoBoard()
        {
            ToDoTask task = Task("t1", customStatusId: "source-column");

            ProjectMergeReassignment result = ProjectMerge.Reassignment(task, Project(id: "src", name: "Source"),
                                                                        Project(id: "dst", name: "Target"));

            Assert.IsTrue(result.Body.ContainsKey(ToDoUpsertBody.Keys.CustomStatusId));
            Assert.IsNull(result.Body[ToDoUpsertBody.Keys.CustomStatusId],
                          "a column from the source board cannot follow the task");
        }

        [TestMethod]
        public void ATaskAlreadyTaggedWithTheTargetIsNotTaggedTwice()
        {
            ToDoTask task = Task("t1", tags: new[] { new ToDoTag("dst", "Target") });

            ProjectMergeReassignment result = ProjectMerge.Reassignment(task, Project(id: "src", name: "Source"),
                                                                        Project(id: "dst", name: "Target"));

            CollectionAssert.AreEqual(new[] { "dst" },
                                      TagsOf(result.Body).Select(tag => tag[ToDoUpsertBody.TagKeys.Id]).ToArray());
        }

        [TestMethod]
        public void ATaskInNoProjectYetCanStillBeFiledIntoOne()
        {
            // The reschedule row's project picker passes no source; there is simply no tag to strip.
            ToDoTask task = Task("t1", tags: new[] { new ToDoTag("label", "#deep") });

            ProjectMergeReassignment result = ProjectMerge.Reassignment(task, null, Project(id: "dst", name: "Target"));

            CollectionAssert.AreEqual(new[] { "label", "dst" },
                                      TagsOf(result.Body).Select(tag => tag[ToDoUpsertBody.TagKeys.Id]).ToArray());
        }

        [TestMethod]
        public void ExternallySyncedTasksAreLeftAlone()
        {
            ToDoTask synced = Task("t1");
            synced.SyncedProjectId = "zoho-1";

            ProjectMergeReassignment result = ProjectMerge.Reassignment(synced, Project(id: "src", name: "Source"),
                                                                        Project(id: "dst", name: "T"));

            Assert.IsFalse(result.Moves, "a task owned by an external tool must not be re-pointed");
            Assert.AreEqual(ProjectMergeSkip.ExternallySynced, result.Skip);
            Assert.IsNull(result.Body);

            ToDoTask external = Task("t2");
            external.ExternalTaskId = "jira-4";
            Assert.IsFalse(ProjectMerge.Reassignment(external, null, Project(id: "dst", name: "T")).Moves);
        }

        [TestMethod]
        public void ReassignmentDoesNotMutateTheTaskItReads()
        {
            // The Mac's task is a struct, so its tags cannot be edited in place by building a body.
            ToDoTask task = Task("t1", tags: new[] { new ToDoTag("src", "Source") });

            ProjectMerge.Reassignment(task, Project(id: "src", name: "Source"), Project(id: "dst", name: "Target"));

            Assert.AreEqual(1, task.TagList.Count);
            Assert.AreEqual("src", task.TagList[0].Id);
            Assert.IsNull(task.ProjectId);
        }

        [TestMethod]
        public void OnlyAFullyEmptiedProjectIsArchived()
        {
            Assert.IsTrue(new ProjectMergeOutcome(3, 0, 0).ArchivesSource);
            Assert.IsFalse(new ProjectMergeOutcome(3, 1, 0).ArchivesSource,
                           "a project still holding synced tasks is not empty");
            Assert.IsFalse(new ProjectMergeOutcome(3, 0, 1).ArchivesSource);
            Assert.IsFalse(new ProjectMergeOutcome(3, 0, 1).Succeeded);
            Assert.IsTrue(new ProjectMergeOutcome(3, 1, 0).Succeeded, "a skip is not a failure");
        }
    }
}
