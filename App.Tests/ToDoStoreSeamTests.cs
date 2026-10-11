using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Focus_Bear.API;
using Focus_Bear.ToDoScreens;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The native To-Do screen's store against a fake API: the optimistic completion and its rollback, the
    /// section counts that move with it, and the write a row action sends. Core proves the rules; this
    /// proves the screen applies them to what the server actually returns.
    /// </summary>
    [TestClass]
    public class ToDoStoreSeamTests
    {
        private sealed class FakeApi : IToDoApi
        {
            public readonly Dictionary<ToDoStatus, List<ToDoTask>> Buckets = new Dictionary<ToDoStatus, List<ToDoTask>>();
            public readonly List<IDictionary<string, object>> Upserts = new List<IDictionary<string, object>>();
            public ApiResponse NextUpsert = new ApiResponse(200, "");

            public Task<ToDoPage> GetPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId)
            {
                var rows = status.HasValue && Buckets.ContainsKey(status.Value) ? Buckets[status.Value] : new List<ToDoTask>();
                return System.Threading.Tasks.Task.FromResult(new ToDoPage(rows.Select(t => t.Copy()).ToList(), rows.Count, false, rows.Count));
            }

            public IList<ToDoTask> SearchAnswer = new List<ToDoTask>();
            public Task<IList<ToDoTask>> Search(string title) => System.Threading.Tasks.Task.FromResult(SearchAnswer);

            public Task<ApiResponse> Upsert(IDictionary<string, object> body)
            {
                Upserts.Add(body);
                return System.Threading.Tasks.Task.FromResult(NextUpsert);
            }

            public Task<bool> Delete(string id) => System.Threading.Tasks.Task.FromResult(true);
            public Task<IList<Project>> GetProjects() => System.Threading.Tasks.Task.FromResult<IList<Project>>(new List<Project>());
            public Task<ApiResponse> CreateProject(string name) => System.Threading.Tasks.Task.FromResult(new ApiResponse(500, ""));
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

        private static ToDoTask Row(string id, string status, ToDoAssignee assignee = null) =>
            new ToDoTask { Id = id, Title = "Task " + id, Status = status, CreatedAt = "2026-09-01T00:00:00Z", Assignee = assignee };

        private static Tuple<ToDoStore, FakeApi> Loaded()
        {
            var api = new FakeApi();
            api.Buckets[ToDoStatus.NotStarted] = new List<ToDoTask> { Row("a", ToDoStatusWire.NotStarted, new ToDoAssignee("u9", "sam")), Row("b", ToDoStatusWire.NotStarted) };
            api.Buckets[ToDoStatus.Completed] = new List<ToDoTask> { Row("c", ToDoStatusWire.Completed) };
            var store = new ToDoStore(ToDoStrings.Fallbacks(), api, () => new DateTime(2026, 9, 24, 12, 0, 0), ToDoSortMode.Chronological);
            store.RefreshAllAsync().GetAwaiter().GetResult();
            return Tuple.Create(store, api);
        }

        private static int Count(ToDoStore store, string sectionId) =>
            store.TotalCount(store.Sections.Single(section => section.Id == sectionId));

        [TestMethod]
        public void Refresh_OpensToDoWhenNothingIsInProgress()
        {
            OnSta(() =>
            {
                var store = Loaded().Item1;
                Assert.AreEqual(ToDoStatusWire.NotStarted, store.ExpandedSectionId);
                Assert.AreEqual(2, Count(store, ToDoStatusWire.NotStarted));
                Assert.AreEqual(1, Count(store, ToDoStatusWire.Completed));
            });
        }

        [TestMethod]
        public void ToggleComplete_MovesTheTaskAndTheCountsTogether_AndBannersIt()
        {
            OnSta(() =>
            {
                var loaded = Loaded();
                var store = loaded.Item1;
                store.ToggleCompleteAsync(store.LoadedTasks.Single(t => t.Id == "b")).GetAwaiter().GetResult();

                Assert.AreEqual(1, Count(store, ToDoStatusWire.NotStarted));
                Assert.AreEqual(2, Count(store, ToDoStatusWire.Completed));
                Assert.AreEqual(ToDoStatusWire.Completed, loaded.Item2.Upserts.Single()[ToDoUpsertBody.Keys.Status]);
                StringAssert.Contains(store.BannerText, "'Task b' completed!");
            });
        }

        [TestMethod]
        public void ToggleComplete_ThatTheServerRejects_PutsTheTaskBackAndSaysWhy()
        {
            OnSta(() =>
            {
                var loaded = Loaded();
                var store = loaded.Item1;
                loaded.Item2.NextUpsert = new ApiResponse(400, "{\"message\":\"Title too long\"}");
                store.ToggleCompleteAsync(store.LoadedTasks.Single(t => t.Id == "b")).GetAwaiter().GetResult();

                Assert.AreEqual(ToDoStatus.NotStarted, store.LoadedTasks.Single(t => t.Id == "b").DefaultStatus);
                Assert.AreEqual(2, Count(store, ToDoStatusWire.NotStarted));
                Assert.AreEqual(1, Count(store, ToDoStatusWire.Completed));
                StringAssert.Contains(store.StatusMessage, "Title too long");
                Assert.IsNull(store.Banner);
                Assert.AreEqual(0, store.UpdatingTaskIds.Count);
            });
        }

        [TestMethod]
        public void RowWrite_KeepsAnAssigneeTheListOnlySentAsARelation()
        {
            // GET /to-do sends `assignee: { id }` and no flat assignee_id (backend#2414); sending the flat
            // field as it arrived would unassign the task on every tick.
            OnSta(() =>
            {
                var loaded = Loaded();
                var store = loaded.Item1;
                store.ToggleCompleteAsync(store.LoadedTasks.Single(t => t.Id == "a")).GetAwaiter().GetResult();
                Assert.AreEqual("u9", loaded.Item2.Upserts.Single()[ToDoUpsertBody.Keys.AssigneeId]);
            });
        }

        [TestMethod]
        public void SetDueDate_SendsTheLocalDay_AndSkipsAnUnchangedOne()
        {
            OnSta(() =>
            {
                var loaded = Loaded();
                var store = loaded.Item1;
                store.SetDueDateAsync(store.LoadedTasks.Single(t => t.Id == "b"), new DateTime(2026, 9, 30)).GetAwaiter().GetResult();
                Assert.AreEqual("2026-09-30", loaded.Item2.Upserts.Single()[ToDoUpsertBody.Keys.DueDate]);

                store.SetDueDateAsync(store.LoadedTasks.Single(t => t.Id == "b"), new DateTime(2026, 9, 30)).GetAwaiter().GetResult();
                Assert.AreEqual(1, loaded.Item2.Upserts.Count, "the same day again is not another write");
            });
        }

        [TestMethod]
        public void Filters_NarrowTheSections_AndCountTheirOwnRows()
        {
            OnSta(() =>
            {
                var store = Loaded().Item1;
                store.SetFilters(new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Full });
                Assert.AreEqual(ToDoEmptyState.NoFilterMatch, store.EmptyState);
                store.SetFilters(new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Low });
                Assert.AreEqual(2, Count(store, ToDoStatusWire.NotStarted));
            });
        }

        [TestMethod]
        public void ASearchThatFails_SaysSo_RatherThanClaimingNoTaskMatches()
        {
            OnSta(() =>
            {
                var loaded = Loaded();
                var store = loaded.Item1;
                loaded.Item2.SearchAnswer = null;   // offline, or the server refused
                store.SetSearchQuery("report");
                store.ScreenAppearedAsync().GetAwaiter().GetResult();

                Assert.AreEqual(store.Strings.RequestFailed, store.StatusMessage);
                Assert.IsFalse(store.IsSearching);
            });
        }

        [TestMethod]
        public void TickingATaskBeforeItsProjectHasLoaded_KeepsItsBoardColumn()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                api.Buckets[ToDoStatus.InProgress] = new List<ToDoTask>
                {
                    new ToDoTask { Id = "t", Title = "Draft", Status = ToDoStatusWire.InProgress, ProjectId = "p1", CustomStatusId = "writing", CreatedAt = "2026-09-01T00:00:00Z" },
                };
                var store = new ToDoStore(ToDoStrings.Fallbacks(), api, () => new DateTime(2026, 9, 24, 12, 0, 0), ToDoSortMode.Chronological);
                store.RefreshAllAsync().GetAwaiter().GetResult();   // the project catalogue has not answered

                store.ToggleCompleteAsync(store.LoadedTasks.Single()).GetAwaiter().GetResult();

                var body = api.Upserts.Single();
                Assert.AreEqual(ToDoStatusWire.Completed, body[ToDoUpsertBody.Keys.Status]);
                Assert.AreEqual("writing", body[ToDoUpsertBody.Keys.CustomStatusId], "a null here moves the card off its column on every board");
            });
        }

        [TestMethod]
        public void TheTodayFilter_StillMeansTodayAfterMidnight()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                api.Buckets[ToDoStatus.NotStarted] = new List<ToDoTask>
                {
                    new ToDoTask { Id = "wed", Title = "Wed", Status = ToDoStatusWire.NotStarted, DueDate = "2026-09-23", CreatedAt = "2026-09-01T00:00:00Z" },
                    new ToDoTask { Id = "thu", Title = "Thu", Status = ToDoStatusWire.NotStarted, DueDate = "2026-09-24", CreatedAt = "2026-09-01T00:00:00Z" },
                };
                var clock = new DateTime(2026, 9, 23, 23, 50, 0);
                var store = new ToDoStore(ToDoStrings.Fallbacks(), api, () => clock, ToDoSortMode.Chronological);
                store.RefreshAllAsync().GetAwaiter().GetResult();

                var range = ToDoStore.DueDateRange(ToDoDueDatePreset.Today, clock);
                store.SetFilters(new ToDoFilters { DueDateStart = range.Item1, DueDateEnd = range.Item2 });
                Assert.AreEqual(ToDoDueDatePreset.Today, store.ActiveDueDatePreset);
                CollectionAssert.AreEqual(new[] { "wed" }, store.Sections.SelectMany(s => s.Tasks).Select(t => t.Id).ToList());

                clock = new DateTime(2026, 9, 24, 0, 10, 0);      // the screen left open past midnight

                Assert.AreEqual(ToDoDueDatePreset.Today, store.ActiveDueDatePreset, "the menu still says Today…");
                CollectionAssert.AreEqual(new[] { "thu" }, store.Sections.SelectMany(s => s.Tasks).Select(t => t.Id).ToList(), "…and means it");
            });
        }

        [TestMethod]
        public void EditorDetails_UnchangedRichTextGoesBackAsItCame()
        {
            const string html = "<p><strong>Plan</strong> the week</p>";
            string opening = ToDoHtml.PlainText(html);
            Assert.AreEqual(html, ToDoEditorWindow.DetailsToSave(opening + "  ", opening, html));
            Assert.AreEqual("Plan the month", ToDoEditorWindow.DetailsToSave("Plan the month", opening, html));
            Assert.AreEqual("typed", ToDoEditorWindow.DetailsToSave("typed", "", null));
        }
    }
}
