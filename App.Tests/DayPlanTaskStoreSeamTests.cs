using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan task store over in-memory storage (never the registry), fed the backend's wire shapes.
    /// </summary>
    [TestClass]
    public class DayPlanTaskStoreSeamTests
    {
        private static readonly DateTime Yesterday = DateTime.Today.AddDays(-1);
        private static readonly DateTime Today = DateTime.Today;
        private string _saved;
        private Func<IDayPlanSyncApi> _savedApi;

        private sealed class NoNetwork : IDayPlanSyncApi
        {
            public Task<string> GetToDosByIds(IEnumerable<string> ids) => Task.FromResult<string>(null);
            public Task<bool> SetTodoStatus(string a, string b, int c, bool d) => Task.FromResult(false);
            public Task<bool> SetTodoPlanned(string a, string b, int c, DateTime? d, string e, int? f) => Task.FromResult(false);
            public Task<string> CreateTodo(Newtonsoft.Json.Linq.JObject body) => Task.FromResult<string>(null);
            public Task<bool> UpsertTodo(Newtonsoft.Json.Linq.JObject body) => Task.FromResult(false);
            public Task<string> CreateProject(string name) => Task.FromResult<string>(null);
            public Task<bool> DeleteTodo(string todoId) => Task.FromResult(false);
        }

        [TestInitialize]
        public void Isolate() { _savedApi = DayPlanSync.ApiFactory; DayPlanSync.ApiFactory = () => new NoNetwork(); }

        [TestCleanup]
        public void Restore() => DayPlanSync.ApiFactory = _savedApi;

        /// <summary>A store whose rollover already ran today, so nothing moves unless the test says so.</summary>
        private DayPlanTaskStore Store() => new DayPlanTaskStore(null, json => _saved = json, Today.ToString("yyyy-MM-dd"));

        private static string Day(params string[] todos) => "{\"tasks\":[" + string.Join(",", todos) + "]}";

        private static string Todo(string id, string title = "Write report", string status = "NOT_STARTED", string quadrant = null) =>
            "{\"id\":\"" + id + "\",\"title\":\"" + title + "\",\"status\":\"" + status + "\",\"duration\":1800"
            + (quadrant == null ? "" : ",\"eisenhower_quadrant\":" + quadrant) + "}";

        [TestMethod]
        public void ADayPayloadNeverAddsATaskThatIsLiveOnAnotherDay()
        {
            // Snoozed to today here; the backend's planned_date still says yesterday.
            var store = Store();
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1"), Today);

            bool changed = store.HydrateFromDay(Day(Todo("t1")), Yesterday);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, store.Tasks(Yesterday).Count, "a copy was stranded on the old day");
            Assert.AreEqual(1, store.Tasks(Today).Count);
        }

        [TestMethod]
        public void AFinishedCopyElsewhereDoesNotStopTheDayGettingItsTask()
        {
            var store = Store();
            store.Add(new DayPlanTask("Write report", sourceTaskId: "t1", done: true), Today.AddDays(-3));

            Assert.IsTrue(store.HydrateFromDay(Day(Todo("t1", status: "COMPLETED")), Yesterday));
            Assert.AreEqual(1, store.Tasks(Yesterday).Count);
        }

        [TestMethod]
        public void TheBackendsTitleAndQuadrantReachATaskAlreadyOnTheDay()
        {
            var store = Store();
            store.Add(new DayPlanTask("Old name", sourceTaskId: "t1"), Yesterday);

            Assert.IsTrue(store.HydrateFromDay(Day(Todo("t1", title: "New name", quadrant: "3")), Yesterday));

            DayPlanTask task = store.Tasks(Yesterday).Single();
            Assert.AreEqual("New name", task.Title);
            Assert.AreEqual(3, task.EisenhowerQuadrant);
            Assert.IsNotNull(_saved, "persisted");
        }

        [TestMethod]
        public void ALocalTickIsNeverRevertedByTheDayPayload()
        {
            var store = Store();
            var task = new DayPlanTask("Write report", sourceTaskId: "t1", done: true);
            store.Add(task, Yesterday);

            store.HydrateFromDay(Day(Todo("t1", status: "NOT_STARTED")), Yesterday);

            Assert.IsTrue(store.Tasks(Yesterday).Single().Done);
        }

        [TestMethod]
        public void RolloverCarriesUnfinishedWorkToTodayOnceAndKeepsHistory()
        {
            var seed = new Dictionary<string, List<DayPlanTask>>
            {
                [Yesterday.ToString("yyyy-MM-dd")] = new List<DayPlanTask>
                {
                    new DayPlanTask("Done yesterday", done: true),
                    new DayPlanTask("Still to do", blockKey: "habit:Yoga"),
                },
            };
            var store = new DayPlanTaskStore(Newtonsoft.Json.JsonConvert.SerializeObject(seed), json => _saved = json, Yesterday.ToString("yyyy-MM-dd"));

            DayPlanTask carried = store.Tasks(Today).Single();
            Assert.AreEqual("Still to do", carried.Title);
            Assert.IsNull(carried.BlockKey, "yesterday's block is gone");
            Assert.AreEqual("Done yesterday", store.Tasks(Yesterday).Single().Title);

            store.Add(new DayPlanTask("Left undone"), Yesterday);
            Assert.AreEqual(1, store.Tasks(Today).Count, "the rollover runs once a day");
        }

        private static FocusBear.Core.ToDo.ToDoTask Due(string id, DateTime due, string assignee = null) => new FocusBear.Core.ToDo.ToDoTask
        {
            Id = id, Title = "Task " + id, Status = "NOT_STARTED", EisenhowerQuadrant = 2, Duration = 1800,
            DueDate = due.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), AssigneeId = assignee,
        };

        [TestMethod]
        public void DueWorkIsFiledOnItsDueDayAndOverdueWorkOnToday()
        {
            var store = Store();
            Assert.IsTrue(store.MergeDueTodos(new[] { Due("a", Today.AddDays(2).AddHours(9)), Due("b", Today.AddDays(-4).AddHours(9)) }, DateTime.Now, "me", persist: false));

            Assert.AreEqual("a", store.Tasks(Today.AddDays(2)).Single().SourceTaskId);
            Assert.AreEqual("b", store.Tasks(Today).Single().SourceTaskId);
            Assert.AreEqual(30, store.Tasks(Today).Single().DurationMinutes);
        }

        [TestMethod]
        public void SomebodyElsesWorkAndATaskTickedTodayStayOff()
        {
            var store = Store();
            store.Add(new DayPlanTask("Task t", sourceTaskId: "t", done: true, eisenhowerQuadrant: 2), Today);

            store.MergeDueTodos(new[] { Due("t", Today.AddHours(9)), Due("x", Today.AddHours(9), assignee: "someone-else") }, DateTime.Now, "me", persist: false);

            var today = store.Tasks(Today);
            Assert.AreEqual(1, today.Count, "the catalogue hasn't caught up with the tick, and x isn't mine");
            Assert.IsTrue(today[0].Done);
        }

        [TestMethod]
        public void AMovedDueDateTakesTheTaskOffItsOldDayAndDropsItsBlock()
        {
            var store = Store();
            store.MergeDueTodos(new[] { Due("a", Today.AddHours(9)) }, DateTime.Now, "me", persist: false);
            store.SetBlockKey("habit:Yoga", store.Tasks(Today).Single().Id, Today);

            store.MergeDueTodos(new[] { Due("a", Today.AddDays(1).AddHours(9)) }, DateTime.Now, "me", persist: false);

            Assert.AreEqual(0, store.Tasks(Today).Count);
            Assert.IsNull(store.Tasks(Today.AddDays(1)).Single().BlockKey);
        }

        [TestMethod]
        public void ATaskThatStaysOnItsDayKeepsItsBlockAndNothingIsRewritten()
        {
            var store = Store();
            store.MergeDueTodos(new[] { Due("a", Today.AddHours(9)) }, DateTime.Now, "me", persist: false);
            store.SetBlockKey("habit:Yoga", store.Tasks(Today).Single().Id, Today);

            Assert.IsFalse(store.MergeDueTodos(new[] { Due("a", Today.AddHours(9)) }, DateTime.Now, "me", persist: false));
            Assert.AreEqual("habit:Yoga", store.Tasks(Today).Single().BlockKey);
        }

        [TestMethod]
        public void AStatusReadDoesNotUndoATickMadeSecondsAgo()
        {
            var store = Store();
            store.Add(new DayPlanTask("Task t", sourceTaskId: "t", eisenhowerQuadrant: 2), Today);
            store.SetDone(store.Tasks(Today).Single().Id, true, Today);

            Assert.IsFalse(store.ReconcileStatusFrom("[" + Todo("t", status: "NOT_STARTED") + "]"), "the write may still be in flight");
            Assert.IsTrue(store.Tasks(Today).Single().Done);
        }
    }
}
