using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The `PUT /to-do` body, ported from the Mac's `testUpsertBodySendsNullToClearProject`,
    /// `testCreateBodyWithProjectMatchesWeb` and the assignee/MCP-token cases.
    /// </summary>
    [TestClass]
    public class ToDoUpsertBodyTests
    {
        /// <summary>Present with a null value: the key IS sent, and the backend clears the field.</summary>
        private static void AssertSendsNull(IDictionary<string, object> body, string key)
        {
            Assert.IsTrue(body.ContainsKey(key), key + " must be sent so the server clears it");
            Assert.IsNull(body[key]);
        }

        private static void AssertOmitted(IDictionary<string, object> body, string key)
        {
            Assert.IsFalse(body.ContainsKey(key), key + " must be left out so the server keeps what it has");
        }

        [TestMethod]
        public void SendsNullToClearTheProjectTheColumnAndTheDueDate()
        {
            ToDoTask task = Task("a", dueOffset: 1, outcome: 5, perspiration: 2);
            task.Subtasks = new List<ToDoSubtask> { new ToDoSubtask("s", true) };

            IDictionary<string, object> body = ToDoUpsertBody.Make(task);

            Assert.AreEqual("a", body[ToDoUpsertBody.Keys.Id]);
            AssertSendsNull(body, ToDoUpsertBody.Keys.ProjectId);
            AssertSendsNull(body, ToDoUpsertBody.Keys.CustomStatusId);
            Assert.AreEqual(ToDoDates.Ymd(Day(1)), body[ToDoUpsertBody.Keys.DueDate]);

            var subtasks = (IList<IDictionary<string, object>>)body[ToDoUpsertBody.Keys.Subtasks];
            Assert.AreEqual("s", subtasks[0][ToDoUpsertBody.SubtaskKeys.Name]);
            Assert.AreEqual(true, subtasks[0][ToDoUpsertBody.SubtaskKeys.IsCompleted]);
            CollectionAssert.AreEquivalent(new[] { "name", "is_completed" }, subtasks[0].Keys.ToArray(),
                                           "the backend's canonical shape: an id only ever comes back on old rows");

            task.DueDate = null;
            AssertSendsNull(ToDoUpsertBody.Make(task), ToDoUpsertBody.Keys.DueDate);
        }

        [TestMethod]
        public void AnEmptyDueDateIsSentAsNullRatherThanAnEmptyString()
        {
            // The DTO's @IsDate rejects "", so an empty string would fail the whole write.
            ToDoTask task = Task("a");
            task.DueDate = string.Empty;

            AssertSendsNull(ToDoUpsertBody.Make(task), ToDoUpsertBody.Keys.DueDate);
        }

        [TestMethod]
        public void ReadOnlyFieldsAreNeverSent()
        {
            ToDoTask task = Task("a", dueOffset: 1, topScore: 42);

            IDictionary<string, object> body = ToDoUpsertBody.Make(task);

            AssertOmitted(body, "top_score");
            AssertOmitted(body, "assignee");
            AssertOmitted(body, "external_task_status");
        }

        [TestMethod]
        public void TheCreateBodyWithAProjectMatchesTheWeb()
        {
            Project project = Project(columns: null);
            ToDoTask draft = ToDoDraft.NewTask("Paint the room", Now, project);

            IDictionary<string, object> body = ToDoUpsertBody.Make(draft);

            var tags = (IList<IDictionary<string, string>>)body[ToDoUpsertBody.Keys.Tags];
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("p", tags[0][ToDoUpsertBody.TagKeys.Id]);
            Assert.AreEqual("P", tags[0][ToDoUpsertBody.TagKeys.Text]);
            CollectionAssert.AreEquivalent(new[] { "id", "text" }, tags[0].Keys.ToArray(),
                                           "a tag is exactly { id, text }, like the web's Tag");

            Assert.AreEqual("p", body[ToDoUpsertBody.Keys.ProjectId]);
            AssertOmitted(body, ToDoUpsertBody.Keys.SyncedProjectId);
            Assert.AreEqual(ToDoDates.Ymd(Day(1)), body[ToDoUpsertBody.Keys.DueDate]);
            Assert.AreEqual((int)EisenhowerQuadrant.DoIt, body[ToDoUpsertBody.Keys.EisenhowerQuadrant],
                            "the hidden quadrant still defaults to Do");
            Assert.AreEqual(ToDoStatusWire.NotStarted, body[ToDoUpsertBody.Keys.Status]);
            AssertSendsNull(body, ToDoUpsertBody.Keys.CustomStatusId);

            foreach (string key in new[]
                     {
                         ToDoUpsertBody.Keys.Id, ToDoUpsertBody.Keys.Title, ToDoUpsertBody.Keys.Details,
                         ToDoUpsertBody.Keys.Objective, ToDoUpsertBody.Keys.Tags, ToDoUpsertBody.Keys.Subtasks,
                         ToDoUpsertBody.Keys.Duration, ToDoUpsertBody.Keys.Outcome,
                         ToDoUpsertBody.Keys.PerspirationLevel,
                     })
            {
                Assert.IsTrue(body.ContainsKey(key), key + " is part of every write");
            }
        }

        [TestMethod]
        public void AnUnratedOutcomeIsOmittedUnlessTheUserClearedIt()
        {
            ToDoTask unrated = Task("a");

            AssertOmitted(ToDoUpsertBody.Make(unrated), ToDoUpsertBody.Keys.Outcome);
            AssertSendsNull(ToDoUpsertBody.Make(unrated, clearOutcome: true), ToDoUpsertBody.Keys.Outcome);
        }

        [TestMethod]
        public void ClearingTheAssigneeStillSendsAnExplicitNull()
        {
            // The picker has an "Unassigned" option, so an absent assignee really is the user's choice.
            AssertSendsNull(ToDoUpsertBody.Make(Task("a")), ToDoUpsertBody.Keys.AssigneeId);

            ToDoTask assigned = Task("a");
            assigned.AssigneeId = "user-7";
            Assert.AreEqual("user-7", ToDoUpsertBody.Make(assigned)[ToDoUpsertBody.Keys.AssigneeId]);
        }

        [TestMethod]
        public void TheAssigneeRelationAloneIsNotEnoughToKeepAnAssignment()
        {
            // `GET /to-do` sends the assignee as a relation and no flat id (backend#2414). This method
            // deliberately does NOT resolve the two shapes — doing so would make null ambiguous and
            // break Unassigned — so the caller must (ToDoDraft.Editable). Pinning that here keeps the
            // division of labour honest rather than accidental.
            ToDoTask fromWire = Task("a");
            fromWire.Assignee = new ToDoAssignee("user-7");

            AssertSendsNull(ToDoUpsertBody.Make(fromWire), ToDoUpsertBody.Keys.AssigneeId);
            Assert.AreEqual("user-7",
                            ToDoUpsertBody.Make(ToDoDraft.Editable(fromWire))[ToDoUpsertBody.Keys.AssigneeId]);
        }

        [TestMethod]
        public void AnUnknownMcpTokenIsLeftAloneRatherThanNulled()
        {
            // Nothing here can read or set it and `GET /to-do` never returns it, so an absent value only
            // means "this client never knew". Sending null wiped an agent's claim on every human save.
            AssertOmitted(ToDoUpsertBody.Make(Task("a")), ToDoUpsertBody.Keys.AssignedMcpTokenId);

            ToDoTask claimed = Task("a");
            claimed.AssignedMcpTokenId = "token-1";
            Assert.AreEqual("token-1", ToDoUpsertBody.Make(claimed)[ToDoUpsertBody.Keys.AssignedMcpTokenId]);
        }

        [TestMethod]
        public void OptionalStringsAreOmittedWhenEmptyAndSentWhenSet()
        {
            ToDoTask task = Task("a");
            task.Icon = string.Empty;
            task.FocusType = "deep";
            task.ExternalTaskId = "zoho-9";

            IDictionary<string, object> body = ToDoUpsertBody.Make(task);

            AssertOmitted(body, ToDoUpsertBody.Keys.Icon);
            Assert.AreEqual("deep", body[ToDoUpsertBody.Keys.FocusType]);
            Assert.AreEqual("zoho-9", body[ToDoUpsertBody.Keys.ExternalTaskId]);
        }

        [TestMethod]
        public void AnAbsentDurationIsSentAsZero()
        {
            Assert.AreEqual(0, ToDoUpsertBody.Make(Task("a"))[ToDoUpsertBody.Keys.Duration]);

            ToDoTask estimated = Task("a");
            estimated.Duration = 300;
            Assert.AreEqual(300, ToDoUpsertBody.Make(estimated)[ToDoUpsertBody.Keys.Duration]);
        }

        [TestMethod]
        public void ANullValuedKeySerialisesAsJsonNullRatherThanDisappearing()
        {
            // The whole rule rests on the difference between a key sent as null and a key left out. A
            // serialiser configured to drop nulls would erase it silently, so it is worth one assertion.
            string json = JsonConvert.SerializeObject(ToDoUpsertBody.Make(Task("a")));

            StringAssert.Contains(json, "\"project_id\":null");
            Assert.IsFalse(json.Contains("assigned_mcp_token_id"));
        }

        [TestMethod]
        public void BuildingABodyDoesNotMutateTheTask()
        {
            // The Mac's ToDoTask is a struct, so this is free there and has to be asserted here.
            ToDoTask task = Task("a", dueOffset: 1, tags: new[] { new ToDoTag("p", "P") });
            int tagCount = task.TagList.Count;

            ToDoUpsertBody.Make(task);

            Assert.AreEqual(tagCount, task.TagList.Count);
            Assert.AreEqual(ToDoDates.Ymd(Day(1)), task.DueDate);
        }
    }
}
