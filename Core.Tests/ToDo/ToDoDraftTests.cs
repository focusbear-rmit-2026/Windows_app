using System;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The task an editor starts from, ported from the Mac's
    /// `testNewTaskDraftPreselectsTheProjectFromTheObject`, `testEditableDraftNormalisesServerFields`
    /// and the "next free day" cases.
    /// </summary>
    [TestClass]
    public class ToDoDraftTests
    {
        [TestMethod]
        public void ANewTaskDraftPreselectsTheProjectFromTheObject()
        {
            Project project = Project(columns: new[]
            {
                Column("review", "Review", 1),
                Column("todo", "To Do", 0),
            });

            ToDoTask draft = ToDoDraft.NewTask("T", Now, project);

            Assert.AreEqual(1, draft.TagList.Count);
            Assert.AreEqual("p", draft.TagList[0].Id, "the chip comes from the object, not a catalogue lookup");
            Assert.AreEqual("P", draft.TagList[0].Text);
            Assert.AreEqual("p", draft.ProjectId);
            Assert.AreEqual("todo", draft.CustomStatusId, "starts in the project's first column, by order");
            Assert.AreEqual(ToDoDates.Ymd(Day(1)), draft.DueDate);
            Assert.AreEqual((int)EisenhowerQuadrant.DoIt, draft.EisenhowerQuadrant);
        }

        [TestMethod]
        public void AProjectWithoutColumnsLeavesTheDraftOnTheDefaultStatuses()
        {
            ToDoTask draft = ToDoDraft.NewTask("T", Now, Project());

            Assert.IsNull(draft.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.NotStarted, draft.Status);
        }

        [TestMethod]
        public void ASyncedProjectsColumnsAreNotOursToStartIn()
        {
            Project synced = Project(syncedProjectId: "zoho-1", columns: new[] { Column("todo", "To Do", 0) });

            Assert.IsNull(ToDoDraft.NewTask("T", Now, synced).CustomStatusId);
        }

        [TestMethod]
        public void ADraftWithNoProjectCarriesNoLink()
        {
            ToDoTask draft = ToDoDraft.NewTask("T", Now);

            Assert.AreEqual(0, draft.TagList.Count);
            Assert.IsNull(draft.ProjectId);
        }

        [TestMethod]
        public void ANewTaskTakesTheNextFreeDayWhenOneIsKnown()
        {
            Assert.AreEqual(ToDoDates.Ymd(Day(4)), ToDoDraft.NewTask("T", Now, dueDate: Day(4)).DueDate);
        }

        [TestMethod]
        public void WithoutAForecastANewTaskIsStillDueTomorrow()
        {
            Assert.AreEqual(ToDoDates.Ymd(Day(1)), ToDoDraft.NewTask("T", Now).DueDate);
        }

        [TestMethod]
        public void EveryDraftGetsAnIdentityOfItsOwn()
        {
            string first = ToDoDraft.NewTask("T", Now).Id;
            string second = ToDoDraft.NewTask("T", Now).Id;

            Assert.AreNotEqual(first, second);
            Assert.IsTrue(Guid.TryParse(first, out _), "the server expects a UUID");
            Assert.AreEqual(first.ToLowerInvariant(), first, "lower-case, as every other client writes it");
        }

        [TestMethod]
        public void AnEditableDraftNormalisesWhatTheServerSent()
        {
            ToDoTask task = Task("a", dueOffset: 2);
            task.DueDate = "2026-09-09T00:00:00.000Z";
            task.Details = "<p>Hello&nbsp;<b>world</b></p>";

            ToDoTask draft = ToDoDraft.Editable(task);

            Assert.AreEqual(ToDoDates.Ymd(ToDoDates.Parse("2026-09-09T00:00:00.000Z").Value), draft.DueDate);
            Assert.IsNull(draft.Outcome, "opening an unrated task must not assign it an outcome");
            Assert.AreEqual(ToDoScoring.DefaultPerspiration, draft.PerspirationLevel);
            Assert.AreEqual("Hello world", draft.Details);
        }

        [TestMethod]
        public void AnEditableDraftResolvesTheAssigneeFromTheRelation()
        {
            // Report 2026-09-16_15-14-36: reading only the flat field showed every assigned task as
            // Unassigned, and the next Save then wiped the assignment server-side.
            ToDoTask fromWire = Task("a");
            fromWire.Assignee = new ToDoAssignee("user-7", "kayla");

            Assert.AreEqual("user-7", ToDoDraft.Editable(fromWire).AssigneeId);
        }

        [TestMethod]
        public void AnEditableDraftLeavesAGenuinelyUnassignedTaskUnassigned()
        {
            Assert.IsNull(ToDoDraft.Editable(Task("a")).AssigneeId);
        }

        [TestMethod]
        public void MakingADraftEditableDoesNotTouchTheTaskItCameFrom()
        {
            // The Mac copies the struct on assignment; here the copy has to be made on purpose, or the
            // editor would be writing through to the list's own row before the user pressed Save.
            ToDoTask task = Task("a");
            task.DueDate = "2026-09-09T00:00:00.000Z";
            task.Details = "<p>Hello</p>";

            ToDoDraft.Editable(task);

            Assert.AreEqual("2026-09-09T00:00:00.000Z", task.DueDate);
            Assert.AreEqual("<p>Hello</p>", task.Details);
            Assert.IsNull(task.PerspirationLevel);
        }
    }
}
