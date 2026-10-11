using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// A task's project tags survive the editor even when the active-project catalogue does not list the
    /// project — completed, archived, or simply not loaded. Saving used to rebuild the tags from the
    /// catalogue alone and silently took the task out of every such project.
    /// </summary>
    [TestClass]
    public class ToDoEditorProjectsTests
    {
        private static readonly Project Active = new Project("p-active", "Website");

        private static Project Catalogue(string id) => id == Active.Id ? Active : null;

        [TestMethod]
        public void Selected_KeepsATagTheCatalogueDoesNotKnow()
        {
            var tags = new[] { new ToDoTag("p-archived", "Old thesis"), new ToDoTag("p-active", "Website (stale name)") };

            var selected = ToDoEditorProjects.Selected(new[] { "p-archived", "p-active" }, tags, Catalogue);

            CollectionAssert.AreEqual(new[] { "p-archived", "p-active" }, selected.Select(p => p.Id).ToList());
            Assert.AreEqual("Old thesis", selected[0].Name, "the task's own tag text stands in for the missing project");
            Assert.AreSame(Active, selected[1], "a catalogued project is the catalogue's copy, current name and columns");
        }

        [TestMethod]
        public void Selected_WithAFailedCatalogue_StillHoldsEveryTag()
        {
            var tags = new[] { new ToDoTag("a", "Work"), new ToDoTag("b", "Home") };

            var selected = ToDoEditorProjects.Selected(new[] { "a", "b" }, tags, id => null);

            CollectionAssert.AreEqual(new[] { "Work", "Home" }, selected.Select(p => p.Name).ToList());
        }

        [TestMethod]
        public void Selected_DropsAnIdNeitherKnows_AndNeverListsOneTwice()
        {
            var selected = ToDoEditorProjects.Selected(new[] { "ghost", "p-active", "p-active" }, new ToDoTag[0], Catalogue);

            CollectionAssert.AreEqual(new[] { "p-active" }, selected.Select(p => p.Id).ToList());
        }

        [TestMethod]
        public void Selected_ANewlyPickedCatalogueProjectNeedsNoTag()
        {
            var selected = ToDoEditorProjects.Selected(new[] { "p-active" }, null, Catalogue);

            Assert.AreSame(Active, selected.Single());
        }

        [TestMethod]
        public void CustomStatus_StaysPutWhenTheOwnerIsNotCatalogued()
        {
            Assert.AreEqual("col-review", ToDoEditorProjects.CustomStatusToSave("col-review", "p-archived", null),
                            "an unknown board's columns are not loaded; that is no reason to move the card");
        }

        [TestMethod]
        public void CustomStatus_FollowsACataloguedOwnersBoard()
        {
            var board = new Project("p-board", "Board", new[] { new ProjectCustomStatus("col-1", "Todo", 1) });

            Assert.AreEqual("col-1", ToDoEditorProjects.CustomStatusToSave("col-1", board.Id, board));
            Assert.IsNull(ToDoEditorProjects.CustomStatusToSave("col-1", Active.Id, Active), "a board with no columns has no column to keep");
            Assert.IsNull(ToDoEditorProjects.CustomStatusToSave("col-1", null, null), "no project, no board");
        }
    }
}
