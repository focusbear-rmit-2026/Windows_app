using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Who a task is assigned to, ported from the Mac's `ToDoAssigneeChoicesTests` cases — the shared
    /// reader, the picker's visibility rules and "is this somebody else's work".
    /// </summary>
    [TestClass]
    public class ToDoAssigneeChoicesTests
    {
        private static ToDoAssigneeOption Option(string id) => new ToDoAssigneeOption(id, id + "@x.io");

        [TestMethod]
        public void TheSharedReaderPrefersTheFlatIdAndFallsBackToTheRelation()
        {
            // `GET /to-do` sends the assignee as a relation and carries no flat key at all
            // (backend#2414), so the fallback is the branch that actually fires in production.
            Assert.AreEqual("user-7", ToDoAssigneeChoices.AssigneeId(null, "user-7"));
            Assert.AreEqual("flat", ToDoAssigneeChoices.AssigneeId("flat", "user-7"));
            Assert.IsNull(ToDoAssigneeChoices.AssigneeId(null, null),
                          "genuinely unassigned must stay null, or Unassigned could never be chosen");
            Assert.AreEqual("user-7", ToDoAssigneeChoices.AssigneeId(string.Empty, "user-7"),
                            "an empty flat field is absent, not a choice");
        }

        [TestMethod]
        public void TheUserIsNeverListedTwice()
        {
            IList<ToDoAssigneeOption> options = ToDoAssigneeChoices.Options(
                Option("me"), new[] { Option("me"), Option("other") }, Option("me"));

            CollectionAssert.AreEqual(new[] { "me", "other" }, options.Select(option => option.Id).ToArray());
        }

        [TestMethod]
        public void AnAssigneeNeitherListKnowsIsStillOffered()
        {
            // Someone removed from the project must not vanish from the menu while the task is theirs —
            // the user would have no way to see, or change, who holds it.
            IList<ToDoAssigneeOption> options = ToDoAssigneeChoices.Options(
                Option("me"), new[] { Option("other") }, Option("stranger"));

            CollectionAssert.AreEqual(new[] { "me", "other", "stranger" },
                                      options.Select(option => option.Id).ToArray());
        }

        [TestMethod]
        public void ThereIsNoAssigneeControlForASoloProject()
        {
            // Report 2026-09-14_19-50-36: the only option would be the user themselves, so it is noise.
            IList<ToDoAssigneeOption> solo = ToDoAssigneeChoices.Options(Option("me"), null, null);

            Assert.IsFalse(ToDoAssigneeChoices.CanAssign(solo, null, "me"));
        }

        [TestMethod]
        public void TheControlAppearsOnceTheProjectHasAnotherMember()
        {
            IList<ToDoAssigneeOption> shared = ToDoAssigneeChoices.Options(
                Option("me"), new[] { Option("other") }, null);

            Assert.IsTrue(ToDoAssigneeChoices.CanAssign(shared, null, "me"));
        }

        [TestMethod]
        public void AnExistingAssignmentKeepsTheControlVisibleEvenInASoloProject()
        {
            IList<ToDoAssigneeOption> solo = ToDoAssigneeChoices.Options(Option("me"), null, null);

            Assert.IsTrue(ToDoAssigneeChoices.CanAssign(solo, "someone-else", "me"));
        }

        [TestMethod]
        public void AnAssignmentToMyselfDoesNotForceTheControlOpen()
        {
            IList<ToDoAssigneeOption> solo = ToDoAssigneeChoices.Options(Option("me"), null, null);

            Assert.IsFalse(ToDoAssigneeChoices.CanAssign(solo, "me", "me"));
        }

        [TestMethod]
        public void AnUnassignedTaskIsMine()
        {
            // Most tasks are unassigned and they are the user's own day; treating that as somebody
            // else's would empty the Day Plan.
            Assert.IsFalse(ToDoAssigneeChoices.IsAssignedToSomebodyElse(null, "me"));
            Assert.IsFalse(ToDoAssigneeChoices.IsAssignedToSomebodyElse(string.Empty, "me"));
        }

        [TestMethod]
        public void ATaskAssignedToMeIsMineAndOneAssignedToAnotherPersonIsNot()
        {
            Assert.IsFalse(ToDoAssigneeChoices.IsAssignedToSomebodyElse("me", "me"));
            Assert.IsTrue(ToDoAssigneeChoices.IsAssignedToSomebodyElse("kayla", "me"));
        }

        [TestMethod]
        public void NothingIsHiddenWhileTheSignedInUserIsUnknown()
        {
            // Guessing on a cold launch would blank the day, which is far worse than one extra row.
            Assert.IsFalse(ToDoAssigneeChoices.IsAssignedToSomebodyElse("kayla", null));
            Assert.IsFalse(ToDoAssigneeChoices.IsAssignedToSomebodyElse("kayla", string.Empty));
        }

        [TestMethod]
        public void OwnershipIsDecidedFromTheRelationWhenTheFlatFieldIsMissing()
        {
            // The two rules have to be used together: reading the flat field alone makes every task
            // look unassigned, and this then quietly answers "mine" for all of them.
            string resolved = ToDoAssigneeChoices.AssigneeId(null, "kayla");

            Assert.IsTrue(ToDoAssigneeChoices.IsAssignedToSomebodyElse(resolved, "me"));
        }
    }
}
