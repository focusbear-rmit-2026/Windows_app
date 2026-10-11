using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan's write-sync against a fake API. A task without a stored Eisenhower quadrant was never synced:
    /// ticking it, deleting it or snoozing it stayed on this computer, so the due-date merge put it back on the next
    /// open and the status reconcile un-ticked it.
    /// </summary>
    [TestClass]
    public class DayPlanSyncSeamTests
    {
        private sealed class FakeApi : IDayPlanSyncApi
        {
            public string Row;   // the server's row, or null for "gone"
            public bool ReadFails;
            public readonly List<string> Writes = new List<string>();
            public readonly List<string> Titles = new List<string>();

            public Task<string> GetToDosByIds(IEnumerable<string> ids) =>
                Task.FromResult(ReadFails ? null : "[" + (Row ?? "") + "]");

            public Task<bool> SetTodoStatus(string todoId, string title, int quadrant, bool completed)
            {
                Writes.Add("status " + todoId + " q" + quadrant + " " + completed);
                Titles.Add(title);
                return Task.FromResult(true);
            }

            public Task<bool> SetTodoPlanned(string todoId, string title, int quadrant, DateTime? plannedDate, string blockKey, int? order)
            {
                Titles.Add(title);
                Writes.Add("planned " + todoId + " q" + quadrant + " " + (plannedDate.HasValue ? plannedDate.Value.ToString("yyyy-MM-dd") : "none"));
                return Task.FromResult(true);
            }

            public Task<string> CreateTodo(Newtonsoft.Json.Linq.JObject body) => Task.FromResult<string>(null);
            public Task<bool> UpsertTodo(Newtonsoft.Json.Linq.JObject body) => Task.FromResult(true);
            public Task<string> CreateProject(string name) => Task.FromResult<string>(null);
            public Task<bool> DeleteTodo(string todoId) => Task.FromResult(true);
        }

        private FakeApi _api;
        private Func<IDayPlanSyncApi> _saved;

        [TestInitialize]
        public void Swap() { _saved = DayPlanSync.ApiFactory; _api = new FakeApi(); DayPlanSync.ApiFactory = () => _api; }

        [TestCleanup]
        public void Restore() => DayPlanSync.ApiFactory = _saved;

        private static string Row(string quadrant) =>
            "{\"id\":\"t1\",\"title\":\"Write report\",\"status\":\"NOT_STARTED\"" + (quadrant == null ? "" : ",\"eisenhower_quadrant\":" + quadrant) + "}";

        [TestMethod]
        public async Task TickingATask_KeepsARenameMadeOnTheWeb()
        {
            // The upsert sends a title. The Day Plan's copy can be older than the server's (renamed on the web since),
            // and more tasks now sync (no quadrant needed), so ticking one put the old name back.
            _api.Row = "{\"id\":\"t1\",\"title\":\"Write the Q3 report\",\"status\":\"NOT_STARTED\"}";
            var task = new DayPlanTask("Write report", sourceTaskId: "t1");

            Assert.IsTrue(await DayPlanSync.SendStatusAsync(task, true));
            CollectionAssert.AreEqual(new[] { "Write the Q3 report" }, _api.Titles);

            Assert.IsTrue(await DayPlanSync.SendPlannedAsync(task, new DateTime(2026, 9, 28), "morning", null));
            Assert.AreEqual("Write the Q3 report", _api.Titles.Last(), "the task keeps the name it read");
        }

        [TestMethod]
        public async Task ATaskWithoutAQuadrantIsSyncedWithTheServersOwn()
        {
            _api.Row = Row("2");
            var task = new DayPlanTask("Write report", sourceTaskId: "t1");

            Assert.IsTrue(await DayPlanSync.SendStatusAsync(task, true));

            CollectionAssert.AreEqual(new[] { "status t1 q2 True" }, _api.Writes, "the real quadrant, never a guess over it");
            Assert.AreEqual(2, task.EisenhowerQuadrant, "kept for the next write");
        }

        [TestMethod]
        public async Task AToDoWithNoQuadrantAtAllIsSentAsDoIt()
        {
            _api.Row = Row(null);
            var task = new DayPlanTask("Write report", sourceTaskId: "t1");

            await DayPlanSync.SendPlannedAsync(task, null, null, null);

            CollectionAssert.AreEqual(new[] { "planned t1 q1 none" }, _api.Writes, "a missing quadrant reads as 1 (Do it), as on the web; removing it from the plan clears its due date");
        }

        [TestMethod]
        public async Task NothingIsWrittenWhenTheRowCannotBeRead()
        {
            _api.ReadFails = true;
            Assert.IsFalse(await DayPlanSync.SendStatusAsync(new DayPlanTask("x", sourceTaskId: "t1"), true));
            _api.ReadFails = false;
            _api.Row = null;
            Assert.IsFalse(await DayPlanSync.SendStatusAsync(new DayPlanTask("x", sourceTaskId: "t1"), true), "a deleted to-do");
            Assert.AreEqual(0, _api.Writes.Count);
        }

        [TestMethod]
        public async Task AKnownQuadrantNeedsNoReadAndALocalTaskNoWrite()
        {
            _api.ReadFails = true;
            Assert.IsTrue(await DayPlanSync.SendStatusAsync(new DayPlanTask("x", sourceTaskId: "t1", eisenhowerQuadrant: 1), false));
            Assert.IsFalse(await DayPlanSync.SendStatusAsync(new DayPlanTask("from scratch"), true));
            CollectionAssert.AreEqual(new[] { "status t1 q1 False" }, _api.Writes);
        }
    }
}
