using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Which section of the to-do list a task is drawn in, and what the number beside the heading says.
    /// Ported from the Mac's `testAliasedCustomColumnsFoldIntoDefaultSections` and its header-count
    /// cases, which pin the reported "Doing 1" above "No tasks in this section".
    /// </summary>
    [TestClass]
    public class ToDoSectioningTests
    {
        private static IList<ToDoSection> Build(IEnumerable<ToDoTask> tasks, IEnumerable<Project> projects)
        {
            return ToDoSectioning.Build(tasks, projects, ToDoSortMode.Smart, SectionTitles, Now);
        }

        [TestMethod]
        public void AliasedCustomColumnsFoldIntoDefaultSections()
        {
            Project project = Project(columns: new[]
            {
                Column("s-todo", "To Do", 0),
                Column("s-review", "Review", 1),
            });
            var tasks = new[]
            {
                Task("a", ToDoStatus.InProgress),
                Task("b", customStatusId: "s-todo", projectId: "p"),
                Task("c", customStatusId: "s-review", projectId: "p"),
            };

            IList<ToDoSection> sections = Build(tasks, new[] { project });

            CollectionAssert.AreEqual(new[] { "IN_PROGRESS", "NOT_STARTED", "COMPLETED", "custom__review" },
                                      sections.Select(section => section.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "b" }, Ids(sections[1].Tasks),
                                      "'To Do' is an alias of the default To do section");
            CollectionAssert.AreEqual(new[] { "c" }, Ids(sections[3].Tasks));
            Assert.AreEqual("Review", sections[3].Title);
            Assert.IsNull(sections[3].Status, "a merged custom column belongs to no default status");
        }

        [TestMethod]
        public void ACallersOwnHeadingAliasesToo()
        {
            // The alias list is English; the headings are whatever the app is running in. A column named
            // exactly what the heading says must fold into it rather than doubling it.
            var titles = new Dictionary<ToDoStatus, string>
            {
                { ToDoStatus.InProgress, "En cours" },
                { ToDoStatus.NotStarted, "A faire" },
                { ToDoStatus.Completed, "Termine" },
            };
            Project project = Project(columns: new[] { Column("s", "  En Cours  ", 0) });

            IList<ToDoSection> sections = ToDoSectioning.Build(
                new[] { Task("a", customStatusId: "s", projectId: "p") }, new[] { project },
                ToDoSortMode.Smart, titles, Now);

            Assert.AreEqual(3, sections.Count, "no section of its own");
            CollectionAssert.AreEqual(new[] { "a" }, Ids(sections[0].Tasks));
        }

        [TestMethod]
        public void TheSameLabelInTwoProjectsIsOneSection()
        {
            Project first = Project(id: "p1", name: "One", columns: new[] { Column("c1", "Review", 0) });
            Project second = Project(id: "p2", name: "Two", columns: new[] { Column("c2", " review ", 0) });

            IList<ToDoSection> sections = Build(
                new[] { Task("a", customStatusId: "c1", projectId: "p1"), Task("b", customStatusId: "c2", projectId: "p2") },
                new[] { first, second });

            Assert.AreEqual(4, sections.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(sections[3].Tasks));
            Assert.AreEqual("Review", sections[3].Title, "the first spelling seen names the merged section");
        }

        [TestMethod]
        public void CustomSectionsFollowTheirLabelsAlphabetically()
        {
            Project project = Project(columns: new[]
            {
                Column("c1", "Zebra", 0),
                Column("c2", "apples", 1),
            });

            IList<ToDoSection> sections = Build(
                new[] { Task("z", customStatusId: "c1", projectId: "p"), Task("a", customStatusId: "c2", projectId: "p") },
                new[] { project });

            CollectionAssert.AreEqual(new[] { "apples", "Zebra" },
                                      sections.Skip(3).Select(section => section.Title).ToArray());
        }

        [TestMethod]
        public void ATaskCompletedElsewhereIsDrawnDoneWhateverColumnItStillPointsAt()
        {
            // Report 2026-09-15_17-41-45: ticking a task off the Day Plan or another device changes only
            // `status`, leaving `custom_status_id` on whatever open column it was in.
            Project project = Project(columns: new[]
            {
                Column("open", "Review", 0),
                Column("finished", "Shipped", 1, completes: true),
            });

            IList<ToDoSection> sections = Build(
                new[]
                {
                    Task("ticked", ToDoStatus.Completed, customStatusId: "open", projectId: "p"),
                    Task("shipped", ToDoStatus.Completed, customStatusId: "finished", projectId: "p"),
                },
                new[] { project });

            CollectionAssert.AreEqual(new[] { "ticked" }, Ids(sections[2].Tasks), "Done, not 'Review'");
            CollectionAssert.AreEqual(new[] { "shipped" }, Ids(sections[3].Tasks),
                                      "a column that finishes tasks keeps its own section");
        }

        [TestMethod]
        public void AColumnNoProjectKnowsFallsBackToTheTasksOwnStatus()
        {
            // A task can arrive carrying a column from a project this page never fetched.
            IList<ToDoSection> sections = Build(
                new[] { Task("orphan", ToDoStatus.InProgress, customStatusId: "gone", projectId: "elsewhere") },
                new Project[0]);

            CollectionAssert.AreEqual(new[] { "orphan" }, Ids(sections[0].Tasks));
        }

        [TestMethod]
        public void ATaskDeliveredTwiceIsDrawnOnce()
        {
            IList<ToDoSection> sections = Build(new[] { Task("dupe"), Task("dupe") }, new Project[0]);

            CollectionAssert.AreEqual(new[] { "dupe" }, Ids(sections[1].Tasks));
        }

        [TestMethod]
        public void SectionsAreSortedByTheChosenMode()
        {
            IList<ToDoSection> sections = ToDoSectioning.Build(
                new[] { Task("late", dueOffset: 5), Task("soon", dueOffset: 1) }, new Project[0],
                ToDoSortMode.DueDate, SectionTitles, Now);

            CollectionAssert.AreEqual(new[] { "soon", "late" }, Ids(sections[1].Tasks));
        }

        [TestMethod]
        public void HeaderCountKeepsTheServerTotalForRowsStillBehindPaging()
        {
            // "To do 145" with only the first page of 50 fetched still reads 145.
            Assert.AreEqual(145, ToDoSectionCount.HeaderCount(50, 145, 50));
        }

        [TestMethod]
        public void HeaderCountDropsATaskSectionedIntoAnotherColumn()
        {
            // The reported bug: the one IN_PROGRESS task sits in a project column called "Review", so it
            // renders under Review, not Doing. Doing must read 0, not "1" above "No tasks here".
            Assert.AreEqual(0, ToDoSectionCount.HeaderCount(0, 1, 1));
        }

        [TestMethod]
        public void HeaderCountDropsARowTheClientCouldNotRead()
        {
            // The server sent it and we could not decode it — it will never appear, so do not count it.
            Assert.AreEqual(2, ToDoSectionCount.HeaderCount(2, 3, 3));
        }

        [TestMethod]
        public void HeaderCountCountsRowsGainedFromOtherBuckets()
        {
            // A task fetched as IN_PROGRESS but sectioned into To do makes To do genuinely one longer.
            Assert.AreEqual(146, ToDoSectionCount.HeaderCount(51, 145, 50));
        }

        [TestMethod]
        public void HeaderCountFallsBackToRenderedRowsWithoutAServerTotal()
        {
            Assert.AreEqual(4, ToDoSectionCount.HeaderCount(4, null, 0));
        }

        [TestMethod]
        public void HeaderCountNeverGoesNegativeWhenMoreArrivedThanPromised()
        {
            Assert.AreEqual(3, ToDoSectionCount.HeaderCount(3, 1, 3));
        }
    }
}
