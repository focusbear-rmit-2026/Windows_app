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
    /// The To-Do store when answers arrive out of order: every request here is held until the
    /// test releases it, so the orderings a slow network produces can be replayed exactly. With no
    /// synchronization context on the test thread, releasing a request runs the store's continuation inline.
    /// </summary>
    [TestClass]
    public class ToDoStoreRaceSeamTests
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

        private static ToDoTask Row(string id, string status = ToDoStatusWire.NotStarted) =>
            new ToDoTask { Id = id, Title = "Task " + id, Status = status, CreatedAt = "2026-09-01T00:00:00Z" };


        private sealed class HeldToDoApi : IToDoApi
        {
            public bool Hold;
            public readonly List<Tuple<int, ToDoStatus?, TaskCompletionSource<ToDoPage>>> Held = new List<Tuple<int, ToDoStatus?, TaskCompletionSource<ToDoPage>>>();
            public readonly List<Tuple<int, ToDoStatus?>> Requests = new List<Tuple<int, ToDoStatus?>>();
            public readonly List<IDictionary<string, object>> Upserts = new List<IDictionary<string, object>>();
            public readonly List<TaskCompletionSource<ApiResponse>> HeldUpserts = new List<TaskCompletionSource<ApiResponse>>();
            public bool HoldUpserts;

            public void FailEveryUpsert()
            {
                foreach (var held in HeldUpserts.ToList()) held.TrySetResult(new ApiResponse(500, "{\"message\":\"nope\"}"));
            }

            /// <summary>Five pages of To do, one task each.</summary>
            public static ToDoPage PageOf(int page, ToDoStatus? status) =>
                status == ToDoStatus.NotStarted
                    ? new ToDoPage(new List<ToDoTask> { Row("n" + page) }, 1, page < 5, 5)
                    : new ToDoPage(new List<ToDoTask>(), 0, false, 0);

            public Task<ToDoPage> GetPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId)
            {
                Requests.Add(Tuple.Create(page, status));
                if (!Hold) return Task.FromResult(PageOf(page, status));
                var held = new TaskCompletionSource<ToDoPage>();
                Held.Add(Tuple.Create(page, status, held));
                return held.Task;
            }

            public void Release(int page, ToDoStatus? status)
            {
                var held = Held.First(h => h.Item1 == page && h.Item2 == status);
                Held.Remove(held);
                held.Item3.SetResult(PageOf(page, status));
            }

            public void ReleaseAll(int page)
            {
                foreach (var held in Held.Where(h => h.Item1 == page).ToList()) Release(held.Item1, held.Item2);
            }

            public Task<IList<ToDoTask>> Search(string title) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());

            public Task<ApiResponse> Upsert(IDictionary<string, object> body)
            {
                Upserts.Add(body);
                if (!HoldUpserts) return Task.FromResult(new ApiResponse(200, ""));
                var held = new TaskCompletionSource<ApiResponse>();
                HeldUpserts.Add(held);
                return held.Task;
            }

            public Task<bool> Delete(string id) => Task.FromResult(true);
            public Task<IList<Project>> GetProjects() => Task.FromResult<IList<Project>>(new List<Project>());
            public Task<ApiResponse> CreateProject(string name) => Task.FromResult(new ApiResponse(500, ""));
        }

        private static ToDoStore ToDoStoreOn(HeldToDoApi api) =>
            new ToDoStore(ToDoStrings.Fallbacks(), api, () => new DateTime(2026, 9, 24, 12, 0, 0), ToDoSortMode.Chronological);

        private static ToDoSection ToDoSection(ToDoStore store) => store.Sections.Single(s => s.Id == ToDoStatusWire.NotStarted);

        [TestMethod]
        public void LoadMore_WhileARefreshIsReplacingPageOne_AsksForNothing()
        {
            OnSta(() =>
            {
                var api = new HeldToDoApi();
                var store = ToDoStoreOn(api);
                store.RefreshAllAsync().GetAwaiter().GetResult();
                store.LoadNextPageAsync(ToDoSection(store)).GetAwaiter().GetResult();
                store.LoadNextPageAsync(ToDoSection(store)).GetAwaiter().GetResult();
                CollectionAssert.AreEqual(new[] { "n1", "n2", "n3" }, ToDoSection(store).Tasks.Select(t => t.Id).ToList());

                api.Hold = true;
                var refresh = store.RefreshAllAsync();       // a save, a sort change, the screen reappearing…
                api.Requests.Clear();
                _ = store.LoadNextPageAsync(ToDoSection(store));   // …while the list is scrolled to the bottom

                Assert.IsFalse(api.Requests.Any(r => r.Item2 == ToDoStatus.NotStarted && r.Item1 > 1),
                               "page 4 of the old paging would land after the new page 1 and skip pages 2 and 3");
                api.ReleaseAll(1);
                refresh.GetAwaiter().GetResult();

                api.Hold = false;
                store.LoadNextPageAsync(ToDoSection(store)).GetAwaiter().GetResult();
                CollectionAssert.AreEqual(new[] { "n1", "n2" }, ToDoSection(store).Tasks.Select(t => t.Id).ToList(),
                                          "the list carries on from the page it now holds");
            });
        }

        [TestMethod]
        public void ANextPage_ThatLandsAfterPageOneWasRefetched_IsNotStitchedOn()
        {
            OnSta(() =>
            {
                var api = new HeldToDoApi();
                var store = ToDoStoreOn(api);
                store.RefreshAllAsync().GetAwaiter().GetResult();
                store.LoadNextPageAsync(ToDoSection(store)).GetAwaiter().GetResult();
                store.LoadNextPageAsync(ToDoSection(store)).GetAwaiter().GetResult();

                api.Hold = true;
                var more = store.LoadNextPageAsync(ToDoSection(store));   // page 4 goes out first…
                var refresh = store.RefreshAllAsync();                    // …then a refresh overtakes it
                api.ReleaseAll(1);
                refresh.GetAwaiter().GetResult();
                api.Release(4, ToDoStatus.NotStarted);
                more.GetAwaiter().GetResult();

                CollectionAssert.AreEqual(new[] { "n1" }, ToDoSection(store).Tasks.Select(t => t.Id).ToList());
                Assert.AreEqual(5, store.TotalCount(ToDoSection(store)));
            });
        }

        [TestMethod]
        public void DueDate_WhileTheRowsCompletionIsSaving_IsSentOnceThatWriteSettles()
        {
            OnSta(() =>
            {
                var api = new HeldToDoApi();
                var store = ToDoStoreOn(api);
                store.RefreshAllAsync().GetAwaiter().GetResult();
                var task = ToDoSection(store).Tasks.Single();

                api.HoldUpserts = true;
                var completing = store.ToggleCompleteAsync(task);
                var optimistic = store.LoadedTasks.Single(t => t.Id == task.Id);
                var dating = store.SetDueDateAsync(optimistic, new DateTime(2026, 10, 2));

                Assert.AreEqual(1, api.Upserts.Count, "a PUT built from the optimistic row would re-send COMPLETED whatever became of the first");
                api.FailEveryUpsert();          // the completion is refused and rolled back
                completing.GetAwaiter().GetResult();
                dating.GetAwaiter().GetResult();

                // The due date was not dropped: it goes now, built from the row as it stands (not completed).
                Assert.AreEqual(2, api.Upserts.Count, "the queued due date is sent once the first write settled");
                string sentStatus = (string)api.Upserts[1]["status"];
                Assert.AreNotEqual(ToDoStatusWire.Completed, sentStatus, "not the refused completion");
                foreach (var held in api.HeldUpserts.ToList()) held.TrySetResult(new ApiResponse(200, ""));
                Assert.AreEqual("2026-10-02", ToDoDates.YmdFromIso(store.LoadedTasks.Single(t => t.Id == task.Id).DueDate));
            });
        }

        [TestMethod]
        public void Delete_WhileTheRowsCompletionIsSaving_HappensOnceThatWriteLands()
        {
            OnSta(() =>
            {
                var api = new HeldToDoApi();
                var store = ToDoStoreOn(api);
                store.RefreshAllAsync().GetAwaiter().GetResult();
                var task = ToDoSection(store).Tasks.Single();

                api.HoldUpserts = true;
                var completing = store.ToggleCompleteAsync(task);
                bool accepted = store.DeleteAsync(store.LoadedTasks.Single(t => t.Id == task.Id)).GetAwaiter().GetResult();
                Assert.AreEqual(1, store.LoadedTasks.Count(t => t.Id == task.Id), "not yet: the write's answer would put it back");
                foreach (var held in api.HeldUpserts.ToList()) held.SetResult(new ApiResponse(200, ""));
                completing.GetAwaiter().GetResult();

                Assert.IsTrue(accepted, "the delete was taken, not silently dropped");
                Assert.AreEqual(0, store.LoadedTasks.Count(t => t.Id == task.Id), "deleted once the write landed");
            });
        }

        private sealed class ShiftingExportApi : IToDoApi
        {
            public int Requests;

            /// <summary>Two rows a page; a task created after page 1 pushes "b" onto page 2 as well. Page 3 repeats page 2 for ever.</summary>
            public Task<ToDoPage> GetPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId)
            {
                Requests++;
                var ids = page == 1 ? new[] { "a", "b" } : new[] { "b", "c" };
                return Task.FromResult(new ToDoPage(ids.Select(id => Row(id)).ToList(), 2, true, 3));
            }

            public Task<IList<ToDoTask>> Search(string title) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());
            public Task<ApiResponse> Upsert(IDictionary<string, object> body) => Task.FromResult(new ApiResponse(200, ""));
            public Task<bool> Delete(string id) => Task.FromResult(true);
            public Task<IList<Project>> GetProjects() => Task.FromResult<IList<Project>>(new List<Project>());
            public Task<ApiResponse> CreateProject(string name) => Task.FromResult(new ApiResponse(500, ""));
        }

        [TestMethod]
        public void TheCsvExport_WritesEachTaskOnce_AndStopsWhenPagesRepeat()
        {
            OnSta(() =>
            {
                var api = new ShiftingExportApi();
                var store = new ToDoStore(ToDoStrings.Fallbacks(), api, () => new DateTime(2026, 9, 24, 12, 0, 0), ToDoSortMode.Chronological);

                string csv = store.BuildExportAsync().GetAwaiter().GetResult();

                string[] lines = csv.Split('\n');
                Assert.AreEqual(1 + 3, lines.Length, csv);
                Assert.AreEqual(1, lines.Count(l => l.StartsWith("\"Task b\"")));
                Assert.IsTrue(api.Requests <= 3, "asked " + api.Requests + " pages");
            });
        }
    }
}
