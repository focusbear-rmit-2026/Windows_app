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
    /// The Projects tab's stores when answers arrive out of order: every request here is held until the
    /// test releases it, so the orderings a slow network produces can be replayed exactly. With no
    /// synchronization context on the test thread, releasing a request runs the store's continuation inline.
    /// </summary>
    [TestClass]
    public class ProjectsStoreRaceSeamTests
    {
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

        private sealed class HeldProjectsApi : IProjectsApi
        {
            public Project Project = new Project("p1", "Thesis", new[]
            {
                new ProjectCustomStatus("backlog", "Backlog", 1),
                new ProjectCustomStatus("writing", "Writing", 2),
                new ProjectCustomStatus("shipped", "Shipped", 3, shouldCompleteTask: true),
            }) { OwnerId = "u1" };

            public readonly List<IDictionary<string, object>> Upserts = new List<IDictionary<string, object>>();
            public readonly List<TaskCompletionSource<ApiResponse>> HeldUpserts = new List<TaskCompletionSource<ApiResponse>>();
            public readonly List<Tuple<int, string>> PageRequests = new List<Tuple<int, string>>();
            public bool FailPages;
            public readonly Queue<TaskCompletionSource<PortfolioSnapshot>> HeldPortfolios = new Queue<TaskCompletionSource<PortfolioSnapshot>>();
            public bool HoldPortfolio;

            public Task<PortfolioSnapshot> GetPortfolio()
            {
                if (!HoldPortfolio) return Task.FromResult(new PortfolioSnapshot(new List<PortfolioProject>(), false));
                var held = new TaskCompletionSource<PortfolioSnapshot>();
                HeldPortfolios.Enqueue(held);
                return held.Task;
            }

            public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status)
            {
                PageRequests.Add(Tuple.Create(page, search));
                if (FailPages) return Task.FromResult<ToDoProjectsPage>(null);
                var all = new[] { new Project("a", "Grant"), new Project("b", "House") };
                var rows = all.Where(p => string.IsNullOrWhiteSpace(search) || p.Name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                return Task.FromResult(new ToDoProjectsPage(rows, false));
            }

            public Task<Project> GetProject(string id) => Task.FromResult(Project);

            public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page) =>
                Task.FromResult(new ToDoPage(new List<ToDoTask>
                {
                    new ToDoTask { Id = "t1", Title = "Chapter 1", Status = ToDoStatusWire.NotStarted, CustomStatusId = "backlog", ProjectId = "p1",
                                   Tags = new List<ToDoTag> { new ToDoTag("p1", "Thesis") }, CreatedAt = "2026-09-01T00:00:00Z" },
                }, 1, false, 1));

            public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());
            public Task<ApiResponse> UpdateProject(string id, JObject body) => Task.FromResult(new ApiResponse(200, ""));
            public Task<bool> DeleteProject(string id) => Task.FromResult(true);
            public Task<ApiResponse> CreateProject(string name, int? outcome) => Task.FromResult(new ApiResponse(500, ""));

            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body)
            {
                Upserts.Add(body);
                var held = new TaskCompletionSource<ApiResponse>();
                HeldUpserts.Add(held);
                return held.Task;
            }
        }

        [TestMethod]
        public void BoardMove_WhileTheCardIsStillSaving_IsMadeOnceThatSaveSettles()
        {
            // A second drag (or "Move to") while the card saved was dropped without a word. It is now one write at a
            // time still, but the later move goes once the first settles, planned from where the card then is.
            OnSta(() =>
            {
                var api = new HeldProjectsApi();
                var detail = new ProjectDetailStore(ToDoStrings.Fallbacks(), "u1", new Project("p1", "Thesis"), api, () => new DateTime(2026, 9, 24, 12, 0, 0));
                detail.LoadAsync().GetAwaiter().GetResult();
                var card = detail.Tasks.Single();

                var first = detail.MoveAsync(card, "writing");
                var moved = detail.Tasks.Single();
                var second = detail.MoveAsync(moved, "shipped");      // dragged on again before the first answered

                Assert.AreEqual(1, api.Upserts.Count, "never two writes for one card at once");
                api.HeldUpserts[0].TrySetResult(new ApiResponse(500, ""));   // the first move is refused
                first.GetAwaiter().GetResult();
                second.GetAwaiter().GetResult();

                Assert.AreEqual(2, api.Upserts.Count, "the later move was sent, not dropped");
                api.HeldUpserts[1].TrySetResult(new ApiResponse(200, ""));
                Assert.AreEqual("shipped", detail.Tasks.Single().CustomStatusId, "the card ends where it was last dragged");
                Assert.AreEqual(0, detail.UpdatingTaskIds.Count);
            });
        }

        private static ProjectsStore ProjectsStoreOn(HeldProjectsApi api) =>
            new ProjectsStore(ToDoStrings.Fallbacks(), "u1", api, () => new DateTime(2026, 9, 24, 12, 0, 0));

        [TestMethod]
        public void ASearchTypedInWhatMatters_FiltersAllProjectsOnTheWayIn()
        {
            OnSta(() =>
            {
                var api = new HeldProjectsApi();
                var store = ProjectsStoreOn(api);
                store.ScreenAppearedAsync().GetAwaiter().GetResult();
                Assert.AreEqual(2, store.Projects.Count);

                store.SetSearchQuery("gra");                  // "What matters" filters locally; the server is not asked
                store.SetViewMode(ProjectsViewMode.All);

                Assert.AreEqual("gra", api.PageRequests.Last().Item2);
                CollectionAssert.AreEqual(new[] { "a" }, store.Projects.Select(p => p.Id).ToList(),
                                          "the list under the search box matches what it says");
            });
        }

        [TestMethod]
        public void AProjectsListThatFailedToLoad_DoesNotClaimThereAreNoProjects()
        {
            OnSta(() =>
            {
                var api = new HeldProjectsApi { FailPages = true };
                var store = ProjectsStoreOn(api);
                store.SetViewMode(ProjectsViewMode.All);
                store.LoadProjectsAsync().GetAwaiter().GetResult();

                Assert.AreEqual(ProjectsEmptyVariant.Failed, store.EmptyVariant);
            });
        }

        [TestMethod]
        public void AnOlderPortfolioAnswer_DoesNotReplaceANewerOne()
        {
            OnSta(() =>
            {
                var api = new HeldProjectsApi { HoldPortfolio = true };
                var store = ProjectsStoreOn(api);
                var older = store.LoadPortfolioAsync();
                var newer = store.LoadPortfolioAsync();         // a due-date save reloads while the first is out
                var olderHeld = api.HeldPortfolios.Dequeue();
                var newerHeld = api.HeldPortfolios.Dequeue();

                var fresh = new PortfolioSnapshot(new List<PortfolioProject> { new PortfolioProject { Id = "a", Name = "Grant", DueDate = "2026-10-01" } }, false);
                var stale = new PortfolioSnapshot(new List<PortfolioProject> { new PortfolioProject { Id = "a", Name = "Grant", DueDate = "2026-09-20" } }, true);
                newerHeld.SetResult(fresh);
                olderHeld.SetResult(stale);
                older.GetAwaiter().GetResult();
                newer.GetAwaiter().GetResult();

                Assert.AreSame(fresh, store.Portfolio);
                Assert.IsFalse(store.IsPortfolioLoading);
            });
        }

        /// <summary>A project of 120 tasks served 50 a page, each page held until released when asked to.</summary>
        private sealed class PagedProjectApi : IProjectsApi
        {
            public readonly List<int> Asked = new List<int>();
            public bool Hold;
            public int FailPage;
            public readonly List<Tuple<int, TaskCompletionSource<ToDoPage>>> Held = new List<Tuple<int, TaskCompletionSource<ToDoPage>>>();
            public string Prefix = "t";

            public ToDoPage PageOf(int page)
            {
                if (page == FailPage) return null;
                var rows = Enumerable.Range((page - 1) * 50 + 1, 50).Where(i => i <= 120)
                    .Select(i => new ToDoTask { Id = Prefix + i, Title = "T" + i, Status = ToDoStatusWire.NotStarted, CreatedAt = "2026-09-01T00:00:00Z" }).ToList();
                return new ToDoPage(rows, rows.Count, page * 50 < 120, 120);
            }

            public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page)
            {
                Asked.Add(page);
                if (!Hold) return Task.FromResult(PageOf(page));
                var held = new TaskCompletionSource<ToDoPage>();
                Held.Add(Tuple.Create(page, held));
                return held.Task;
            }

            public Task<PortfolioSnapshot> GetPortfolio() => Task.FromResult<PortfolioSnapshot>(null);
            public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status) => Task.FromResult<ToDoProjectsPage>(null);
            public Task<Project> GetProject(string id) => Task.FromResult(new Project("p1", "Big") { OwnerId = "u1" });
            public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());
            public Task<ApiResponse> UpdateProject(string id, JObject body) => Task.FromResult(new ApiResponse(200, ""));
            public Task<bool> DeleteProject(string id) => Task.FromResult(true);
            public Task<ApiResponse> CreateProject(string name, int? outcome) => Task.FromResult(new ApiResponse(500, ""));
            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body) => Task.FromResult(new ApiResponse(200, ""));
        }

        private static ProjectDetailStore DetailOn(IProjectsApi api) =>
            new ProjectDetailStore(ToDoStrings.Fallbacks(), "u1", new Project("p1", "Big"), api, () => new DateTime(2026, 9, 24, 12, 0, 0));

        [TestMethod]
        public void AProjectPage_HoldsEveryTask_NotJustTheFirstFifty()
        {
            OnSta(() =>
            {
                var api = new PagedProjectApi();
                var detail = DetailOn(api);
                detail.LoadTasksAsync().GetAwaiter().GetResult();

                Assert.AreEqual(120, detail.Tasks.Count);
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, api.Asked);
                Assert.AreEqual(120, detail.Columns.Sum(column => detail.TasksIn(column).Count), "the board's columns count every task");
            });
        }

        [TestMethod]
        public void AProjectPageThatFailsPartWay_KeepsTheListItHad()
        {
            OnSta(() =>
            {
                var api = new PagedProjectApi();
                var detail = DetailOn(api);
                detail.LoadTasksAsync().GetAwaiter().GetResult();
                api.FailPage = 2;
                detail.LoadTasksAsync().GetAwaiter().GetResult();

                Assert.AreEqual(120, detail.Tasks.Count, "50 rows must not pass for the whole project");
                Assert.AreEqual(detail.Strings.RequestFailed, detail.StatusMessage);
            });
        }

        [TestMethod]
        public void AnOlderTasksLoad_DoesNotLandOverANewerOne()
        {
            OnSta(() =>
            {
                var api = new PagedProjectApi { Hold = true, Prefix = "old" };
                var detail = DetailOn(api);
                var older = detail.LoadTasksAsync();
                api.Prefix = "new";
                var newer = detail.LoadTasksAsync();       // the editor saved while the page was loading
                api.Hold = false;
                var olderPage = api.Held[0];
                var newerPage = api.Held[1];
                api.Prefix = "new";
                newerPage.Item2.SetResult(api.PageOf(1));
                newer.GetAwaiter().GetResult();
                api.Prefix = "old";
                olderPage.Item2.SetResult(api.PageOf(1));
                older.GetAwaiter().GetResult();

                Assert.IsTrue(detail.Tasks.All(t => t.Id.StartsWith("new")), "the stale answer is dropped");
                Assert.AreEqual(120, detail.Tasks.Count);
                Assert.IsFalse(detail.IsTasksLoading);
            });
        }
    }
}
