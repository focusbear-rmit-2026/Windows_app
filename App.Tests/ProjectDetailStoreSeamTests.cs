using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Focus_Bear.API;
using Focus_Bear.ToDoScreens;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The project board against a fake API: a card moved between custom columns writes the column and the
    /// status together, a refused move goes back where it came from, a drop on its own column costs nothing,
    /// and a move never unassigns the task.
    /// </summary>
    [TestClass]
    public class ProjectDetailStoreSeamTests
    {
        private sealed class FakeApi : IProjectsApi
        {
            public Project Project;
            public List<ToDoTask> Tasks = new List<ToDoTask>();
            public readonly List<IDictionary<string, object>> Upserts = new List<IDictionary<string, object>>();
            public ApiResponse NextUpsert = new ApiResponse(200, "");

            public Task<PortfolioSnapshot> GetPortfolio() => Task.FromResult<PortfolioSnapshot>(null);
            public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status) => Task.FromResult<ToDoProjectsPage>(null);
            public Task<Project> GetProject(string id) => Task.FromResult(Project);
            public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page) =>
                Task.FromResult(new ToDoPage(Tasks.Select(t => t.Copy()).ToList(), Tasks.Count, false, Tasks.Count));
            public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());
            public Task<ApiResponse> UpdateProject(string id, JObject body) => Task.FromResult(new ApiResponse(200, ""));
            public Task<bool> DeleteProject(string id) => Task.FromResult(true);
            public Task<ApiResponse> CreateProject(string name, int? outcome) => Task.FromResult(new ApiResponse(500, ""));

            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body)
            {
                Upserts.Add(body);
                return Task.FromResult(NextUpsert);
            }
        }

        private static void OnSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new AssertFailedException(failure.Message, failure);
        }

        private const string Me = "u1";

        private static Tuple<ProjectDetailStore, FakeApi> Board()
        {
            var project = new Project("p1", "Thesis", new[]
            {
                new ProjectCustomStatus("backlog", "Backlog", 1),
                new ProjectCustomStatus("writing", "Writing", 2),
                new ProjectCustomStatus("shipped", "Shipped", 3, shouldCompleteTask: true),
            })
            { OwnerId = Me };
            var api = new FakeApi { Project = project };
            api.Tasks.Add(new ToDoTask
            {
                Id = "t1", Title = "Chapter 1", Status = ToDoStatusWire.NotStarted, CustomStatusId = "backlog", ProjectId = "p1",
                Tags = new List<ToDoTag> { new ToDoTag("p1", "Thesis") }, Assignee = new ToDoAssignee("u9", "sam"), CreatedAt = "2026-09-01T00:00:00Z",
            });
            var store = new ProjectDetailStore(ToDoStrings.Fallbacks(), Me, new Project("p1", "Thesis"), api, () => new DateTime(2026, 9, 24, 12, 0, 0));
            store.LoadAsync().GetAwaiter().GetResult();
            return Tuple.Create(store, api);
        }

        [TestMethod]
        public void Load_DrawsTheProjectsOwnColumns()
        {
            OnSta(() =>
            {
                var store = Board().Item1;
                CollectionAssert.AreEqual(new[] { "backlog", "writing", "shipped" }, store.Columns.Select(c => c.Id).ToArray());
                Assert.AreEqual("t1", store.TasksIn(store.Columns[0]).Single().Id);
            });
        }

        [TestMethod]
        public void Move_ToACompletingColumn_WritesColumnAndStatusTogether_AndKeepsTheAssignee()
        {
            OnSta(() =>
            {
                var board = Board();
                var store = board.Item1;
                store.MoveAsync(store.Tasks.Single(), "shipped").GetAwaiter().GetResult();

                var body = board.Item2.Upserts.Single();
                Assert.AreEqual("shipped", body[ToDoUpsertBody.Keys.CustomStatusId]);
                Assert.AreEqual(ToDoStatusWire.Completed, body[ToDoUpsertBody.Keys.Status]);
                Assert.AreEqual("u9", body[ToDoUpsertBody.Keys.AssigneeId]);
                Assert.AreEqual("shipped", store.ColumnIdOf(store.Tasks.Single()));
                Assert.IsTrue(store.HasChanges);
            });
        }

        [TestMethod]
        public void Move_ThatTheServerRefuses_GoesBackToItsColumn()
        {
            OnSta(() =>
            {
                var board = Board();
                var store = board.Item1;
                board.Item2.NextUpsert = new ApiResponse(403, "{\"message\":\"Not allowed\"}");
                store.MoveAsync(store.Tasks.Single(), "writing").GetAwaiter().GetResult();

                Assert.AreEqual("backlog", store.ColumnIdOf(store.Tasks.Single()));
                StringAssert.Contains(store.StatusMessage, "Not allowed");
                Assert.IsFalse(store.HasChanges);
                Assert.AreEqual(0, store.UpdatingTaskIds.Count);
            });
        }

        [TestMethod]
        public void Move_OntoItsOwnColumn_IsNotAWrite()
        {
            OnSta(() =>
            {
                var board = Board();
                board.Item1.MoveAsync(board.Item1.Tasks.Single(), "backlog").GetAwaiter().GetResult();
                Assert.AreEqual(0, board.Item2.Upserts.Count);
            });
        }
    }
}
