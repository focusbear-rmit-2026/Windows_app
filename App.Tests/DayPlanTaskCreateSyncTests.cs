using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.Habits.DayPlan;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Report 2026-09-28_21-59-10: "this Review Vidal work task isn't visible on my other computer". A task typed into
    /// the Day Plan (not imported from the to-do list) had no backing to-do, and the write-sync only ever wrote to-do
    /// backed tasks, so it never left this computer. Renames, projects, durations and importance of synced tasks
    /// weren't sent either: the store is edited in place and only compared the done flag. The Mac mints a to-do
    /// when the name is committed (mintTodoIfNeeded) and pushes each edit (setTodoTitle / Project / Outcome / Subtasks).
    /// </summary>
    [TestClass]
    public class DayPlanTaskCreateSyncTests
    {
        private static readonly DateTime Today = DateTime.Today;

        private sealed class FakeApi : IDayPlanSyncApi
        {
            public readonly List<JObject> Creates = new List<JObject>();
            public readonly List<JObject> Upserts = new List<JObject>();
            public readonly List<string> Writes = new List<string>();
            public readonly List<string> ProjectsCreated = new List<string>();
            public string NextId = "new-1";
            public bool CreateFails;
            public TaskCompletionSource<string> PendingCreate;   // set to hold the create open

            public Task<string> GetToDosByIds(IEnumerable<string> ids) => Task.FromResult<string>(null);

            public Task<bool> SetTodoStatus(string todoId, string title, int quadrant, bool completed)
            {
                Writes.Add("status " + todoId + " " + completed);
                return Task.FromResult(true);
            }

            public Task<bool> SetTodoPlanned(string todoId, string title, int quadrant, DateTime? plannedDate, string blockKey, int? order)
            {
                Writes.Add("planned " + todoId + " " + (plannedDate.HasValue ? plannedDate.Value.ToString("yyyy-MM-dd") : "none"));
                return Task.FromResult(true);
            }

            public Task<string> CreateTodo(JObject body)
            {
                Creates.Add(body);
                if (PendingCreate != null) return PendingCreate.Task;
                return Task.FromResult(CreateFails ? null : NextId);
            }

            public Task<bool> UpsertTodo(JObject body)
            {
                Upserts.Add(body);
                return Task.FromResult(true);
            }

            public Task<string> CreateProject(string name)
            {
                ProjectsCreated.Add(name);
                return Task.FromResult("proj-new");
            }

            public readonly List<string> Deletes = new List<string>();

            public Task<bool> DeleteTodo(string todoId)
            {
                Deletes.Add(todoId);
                return Task.FromResult(true);
            }
        }

        private FakeApi _api;
        private Func<IDayPlanSyncApi> _savedApi;
        private Func<string> _savedPlaceholder;
        private Action _savedCreated;

        [TestInitialize]
        public void Swap()
        {
            _savedApi = DayPlanSync.ApiFactory;
            _savedPlaceholder = DayPlanSync.PlaceholderTitle;
            _savedCreated = DayPlanSync.TodoCreated;
            _api = new FakeApi();
            DayPlanSync.ApiFactory = () => _api;
            DayPlanSync.PlaceholderTitle = () => "Task name";
            DayPlanSync.TodoCreated = () => { };
        }

        [TestCleanup]
        public void Restore()
        {
            DayPlanSync.ApiFactory = _savedApi;
            DayPlanSync.PlaceholderTitle = _savedPlaceholder;
            DayPlanSync.TodoCreated = _savedCreated;
        }

        private static DayPlanTaskStore Store() => new DayPlanTaskStore(null, _ => { }, Today.ToString("yyyy-MM-dd"));

        /// <summary>What the "+ Add task" button and the composer do: a placeholder row, then the typed name committed in place.</summary>
        private static DayPlanTask TypeNewTask(DayPlanTaskStore store, string name, string project = null)
        {
            var task = new DayPlanTask("Task name", 15);
            store.Add(task, Today);
            DayPlanTask t = store.TaskWithId(task.Id, Today);
            t.Title = name;
            t.Project = project;
            store.Update(t, Today);
            return t;
        }

        [TestMethod]
        public void ATaskTypedIntoTheDayPlanCreatesAToDo()
        {
            var store = Store();
            DayPlanTask t = TypeNewTask(store, "Review Vidal work");

            Assert.AreEqual(1, _api.Creates.Count, "one to-do created");
            JObject body = _api.Creates[0];
            Assert.IsNull(body["id"], "a create, not an update");
            Assert.AreEqual("Review Vidal work", (string)body["title"]);
            Assert.AreEqual(1, (int)body["eisenhower_quadrant"], "the neutral quadrant, as the Mac mints");
            Assert.AreEqual(Today.ToString("yyyy-MM-dd"), (string)body["due_date"], "due on the day it was planned, so other devices file it there");
            Assert.AreEqual(Today.ToString("yyyy-MM-dd"), (string)body["planned_date"]);
            Assert.AreEqual(15 * 60, (int)body["duration"], "duration goes over the wire in seconds");
            Assert.AreEqual("new-1", t.SourceTaskId, "the task is now backed by the to-do");
            Assert.AreEqual(1, t.EisenhowerQuadrant);
        }

        [TestMethod]
        public void APlaceholderBlankOrProjectOnlyDraftIsNotCreated()
        {
            var store = Store();
            store.Add(new DayPlanTask("Task name", 15), Today);
            TypeNewTask(store, "   ");
            TypeNewTask(store, "#phd");
            Assert.AreEqual(0, _api.Creates.Count);
        }

        [TestMethod]
        public void TheTasksProjectIsLinkedByIdAndAnUnknownOneIsCreated()
        {
            var store = Store();
            store.SetRemoteProjects(new Dictionary<string, string> { ["proj-phd"] = "phd" });

            TypeNewTask(store, "Review Vidal work", "PhD");
            Assert.AreEqual("proj-phd", (string)_api.Creates[0]["project_id"], "matched by name, ignoring case");
            Assert.AreEqual(0, _api.ProjectsCreated.Count);

            _api.NextId = "new-2";
            DayPlanTask t = TypeNewTask(store, "Draft grant", "Grants");
            CollectionAssert.AreEqual(new[] { "Grants" }, _api.ProjectsCreated);
            Assert.AreEqual("proj-new", (string)_api.Creates[1]["project_id"]);
            Assert.AreEqual("proj-new", t.ProjectId);
        }

        [TestMethod]
        public void AFailedCreateStaysLocalAndIsRetriedOnTheNextEdit()
        {
            var store = Store();
            _api.CreateFails = true;
            DayPlanTask t = TypeNewTask(store, "Review Vidal work");
            Assert.IsNull(t.SourceTaskId);

            _api.CreateFails = false;
            t.DurationMinutes = 30;
            store.Update(t, Today);

            Assert.AreEqual(2, _api.Creates.Count);
            Assert.AreEqual("new-1", t.SourceTaskId);
            Assert.AreEqual(30 * 60, (int)_api.Creates[1]["duration"]);
        }

        [TestMethod]
        public void LocalOnlyTasksFromBeforeTheFixAreCreatedByTheSweep_TodayAndLaterOnly()
        {
            var store = Store();
            _api.CreateFails = true;   // as if they were made by the old build
            TypeNewTask(store, "Review Vidal work");
            var tomorrow = new DayPlanTask("Plan the week", 30);
            store.Add(tomorrow, Today.AddDays(1));
            var done = new DayPlanTask("Old thing", done: true);
            store.Add(done, Today);
            store.Add(new DayPlanTask("Task name"), Today);
            _api.Creates.Clear();
            _api.CreateFails = false;

            int started = store.SyncLocalOnlyTasks(Today);

            Assert.AreEqual(2, started);
            CollectionAssert.AreEquivalent(new[] { "Review Vidal work", "Plan the week" }, _api.Creates.Select(c => (string)c["title"]).ToList());
            Assert.AreEqual(Today.AddDays(1).ToString("yyyy-MM-dd"), (string)_api.Creates.Single(c => (string)c["title"] == "Plan the week")["due_date"]);
            Assert.IsNull(done.SourceTaskId, "finished history is left alone");
        }

        [TestMethod]
        public void AnEditMadeWhileTheCreateIsInFlightIsSentAfterIt()
        {
            var store = Store();
            _api.PendingCreate = new TaskCompletionSource<string>();
            DayPlanTask t = TypeNewTask(store, "Review Vidal work");
            t.Title = "Review Vidal's work";
            store.Update(t, Today);
            Assert.AreEqual(1, _api.Creates.Count, "no second create while the first is in flight");

            _api.PendingCreate.SetResult("new-1");

            Assert.AreEqual("new-1", t.SourceTaskId);
            JObject rename = _api.Upserts.Single();
            Assert.AreEqual("new-1", (string)rename["id"]);
            Assert.AreEqual("Review Vidal's work", (string)rename["title"]);
        }

        [TestMethod]
        public void ATaskDeletedBeforeItsCreateLandsTakesTheNewToDoBackOut()
        {
            var store = Store();
            _api.PendingCreate = new TaskCompletionSource<string>();
            DayPlanTask t = TypeNewTask(store, "Review Vidal work");
            store.Delete(t.Id, Today);

            _api.PendingCreate.SetResult("new-1");

            CollectionAssert.AreEqual(new[] { "new-1" }, _api.Deletes);
        }

        [TestMethod]
        public void RenamingASyncedTaskInPlaceSendsTheNewTitle()
        {
            var store = Store();
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1", eisenhowerQuadrant: 2), Today);
            DayPlanTask t = store.Tasks(Today).Single();

            t.Title = "Write the Q3 report";
            store.Update(t, Today);

            JObject body = _api.Upserts.Single();
            Assert.AreEqual("t1", (string)body["id"]);
            Assert.AreEqual("Write the Q3 report", (string)body["title"]);
            Assert.AreEqual(2, (int)body["eisenhower_quadrant"]);
            Assert.IsNull(body["duration"], "an unchanged field is not sent, so it can't clobber the server's");
            Assert.IsNull(body["subtasks"]);
            Assert.IsNull(body["outcome"]);
        }

        [TestMethod]
        public void DetailEditsOfASyncedTaskAreSent()
        {
            var store = Store();
            store.SetRemoteProjects(new Dictionary<string, string> { ["proj-phd"] = "phd" });
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1", eisenhowerQuadrant: 1), Today);
            DayPlanTask t = store.Tasks(Today).Single();

            t.DurationMinutes = 45;
            t.Outcome = 8;
            t.Project = "phd";
            t.Subtasks = new List<DayPlanSubtask> { new DayPlanSubtask("Outline", true) };
            store.Update(t, Today);

            JObject body = _api.Upserts.Single();
            Assert.AreEqual(45 * 60, (int)body["duration"]);
            Assert.AreEqual(8, (int)body["outcome"]);
            Assert.AreEqual("proj-phd", (string)body["project_id"]);
            Assert.AreEqual("Outline", (string)body["subtasks"][0]["name"]);
            Assert.AreEqual(true, (bool)body["subtasks"][0]["is_completed"]);
        }

        [TestMethod]
        public void AnUnchangedUpdateWritesNothing()
        {
            var store = Store();
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1", eisenhowerQuadrant: 1), Today);
            store.Update(store.Tasks(Today).Single(), Today);
            Assert.AreEqual(0, _api.Upserts.Count);
            Assert.AreEqual(0, _api.Writes.Count(w => w.StartsWith("status")));
        }

        [TestMethod]
        public void TickingDoneInPlaceThenUpdatingSendsTheStatus()
        {
            // The store hands out its own objects; comparing list[i].Done with task.Done after an in-place edit never
            // saw a change.
            var store = Store();
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1", eisenhowerQuadrant: 1), Today);
            DayPlanTask t = store.Tasks(Today).Single();
            t.Done = true;
            store.Update(t, Today);
            CollectionAssert.Contains(_api.Writes, "status t1 True");
        }

        [TestMethod]
        public void AFreshlyCreatedTaskSurvivesACatalogueThatPredatesIt()
        {
            var store = Store();
            DayPlanTask t = TypeNewTask(store, "Review Vidal work");
            Assert.AreEqual("new-1", t.SourceTaskId);

            // The unfinished catalogue was downloaded (or cached) before the create landed.
            store.MergeDueTodos(new List<ToDoTask>(), DateTime.Now, "me", persist: false);

            Assert.AreEqual(1, store.Tasks(Today).Count(x => x.Title == "Review Vidal work"), "the new task must not vanish");
        }
    }
}
