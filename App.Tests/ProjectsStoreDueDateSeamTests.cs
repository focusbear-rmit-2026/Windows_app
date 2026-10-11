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
    /// The project cards' due-date chip against a fake API: a pick shows on every card at once and is sent as
    /// <c>PUT /projects/:id { due_date }</c>, Clear sends null, a refused save puts the old date back and says
    /// why, and picking the date it already has costs no request.
    /// </summary>
    [TestClass]
    public class ProjectsStoreDueDateSeamTests
    {
        private sealed class FakeApi : IProjectsApi
        {
            public string ServerDue = "2026-09-23T00:00:00.000Z";
            public int? ServerOutcome = 7;
            public readonly List<string> CreateBodies = new List<string>();
            public readonly List<Tuple<string, JObject>> Updates = new List<Tuple<string, JObject>>();
            public TaskCompletionSource<ApiResponse> Pending;
            public ApiResponse Reply = new ApiResponse(200, "{}");

            public Task<PortfolioSnapshot> GetPortfolio() => Task.FromResult(new PortfolioSnapshot(new List<PortfolioProject>
            {
                new PortfolioProject { Id = "p1", Name = "[TEST] Thesis", DueDate = ServerDue, BucketRaw = "start_now", DaysUntilDue = -2 },
            }, false));

            public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status) =>
                Task.FromResult(new ToDoProjectsPage(new List<Project> { new Project("p1", "[TEST] Thesis") { DueDate = ServerDue, Outcome = ServerOutcome, OwnerId = "u1", TaskCount = 2 } }, false));

            public Task<Project> GetProject(string id) => Task.FromResult(new Project(id, "[TEST] Thesis") { DueDate = ServerDue, Outcome = ServerOutcome, OwnerId = "u1" });
            public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page) => Task.FromResult(new ToDoPage(new List<ToDoTask>(), 0, false, 0));
            public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());

            public async Task<ApiResponse> UpdateProject(string id, JObject body)
            {
                Updates.Add(Tuple.Create(id, body));
                var reply = Pending != null ? await Pending.Task : Reply;
                if (reply.IsSuccess && body["due_date"] != null) ServerDue = (string)body["due_date"];
                if (reply.IsSuccess && body["outcome"] != null) ServerOutcome = (int?)body["outcome"];
                return reply;
            }

            public Task<bool> DeleteProject(string id) => Task.FromResult(true);
            public Task<ApiResponse> CreateProject(string name, int? outcome)
            {
                CreateBodies.Add(name + "|" + (outcome.HasValue ? outcome.Value.ToString() : "null"));
                return Task.FromResult(new ApiResponse(201, "{\"id\":\"p2\",\"name\":\"" + name + "\",\"outcome\":" + (outcome.HasValue ? outcome.Value.ToString() : "null") + "}"));
            }
            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body) => Task.FromResult(new ApiResponse(200, ""));
        }

        private static readonly DateTime Now = new DateTime(2026, 9, 25, 12, 0, 0);

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

        private static ProjectsStore Loaded(FakeApi api)
        {
            var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", api, () => Now);
            store.ScreenAppearedAsync().GetAwaiter().GetResult();
            return store;
        }

        [TestMethod]
        public void Pick_ShowsAtOnce_ThenSavesTheDay()
        {
            OnSta(() =>
            {
                var api = new FakeApi { Pending = new TaskCompletionSource<ApiResponse>() };
                var store = Loaded(api);
                var saving = store.SetDueDateAsync("p1", new DateTime(2026, 10, 2));

                Assert.AreEqual("2026-10-02", store.Project("p1").DueDate, "the All projects card changes before the reply");
                var row = store.Portfolio.Projects.Single();
                Assert.AreEqual("2026-10-02", row.DueDate, "and so does the What matters card");
                Assert.AreEqual(7, row.DaysUntilDue);
                Assert.IsTrue(store.IsUpdatingDueDate("p1"));

                api.Pending.SetResult(new ApiResponse(200, "{}"));
                Assert.IsTrue(saving.GetAwaiter().GetResult());
                var update = api.Updates.Single();
                Assert.AreEqual("p1", update.Item1);
                Assert.AreEqual("2026-10-02", (string)update.Item2["due_date"]);
                Assert.AreEqual(1, update.Item2.Count, "only the due date is sent");
                Assert.IsFalse(store.IsUpdatingDueDate("p1"));
                Assert.AreEqual("2026-10-02", store.DueDateOf("p1"), "the reload reads it back");
                Assert.AreEqual(ToDoStrings.Fallbacks().ProjectUpdated, store.StatusMessage);
            });
        }

        [TestMethod]
        public void Clear_SendsNull()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = Loaded(api);
                Assert.IsTrue(store.SetDueDateAsync("p1", null).GetAwaiter().GetResult());
                var sent = api.Updates.Single().Item2["due_date"];
                Assert.AreEqual(JTokenType.Null, sent.Type);
                Assert.IsNull(store.Project("p1").DueDate);
                Assert.IsNull(store.Portfolio.Projects.Single().DueDate);
            });
        }

        [TestMethod]
        public void RefusedSave_PutsTheOldDateBack_AndSaysWhy()
        {
            OnSta(() =>
            {
                var api = new FakeApi { Reply = new ApiResponse(403, "{\"message\":\"Not allowed\"}") };
                var store = Loaded(api);
                Assert.IsFalse(store.SetDueDateAsync("p1", new DateTime(2026, 9, 26)).GetAwaiter().GetResult());

                Assert.AreEqual("2026-09-23T00:00:00.000Z", store.Project("p1").DueDate);
                var row = store.Portfolio.Projects.Single();
                Assert.AreEqual("2026-09-23T00:00:00.000Z", row.DueDate);
                Assert.AreEqual(-2, row.DaysUntilDue);
                StringAssert.Contains(store.StatusMessage, "Could not update project");
                StringAssert.Contains(store.StatusMessage, "Not allowed");
                Assert.IsFalse(store.IsUpdatingDueDate("p1"));
            });
        }

        [TestMethod]
        public void PickingTheSameDay_IsNotAWrite()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = Loaded(api);
                // The server's instant means the local day it falls on.
                string sameDay = ToDoDates.YmdFromIso(api.ServerDue);
                Assert.IsTrue(store.SetDueDateAsync("p1", DateTime.ParseExact(sameDay, "yyyy-MM-dd", null)).GetAwaiter().GetResult());
                Assert.AreEqual(0, api.Updates.Count);
            });
        }

        [TestMethod]
        public void OpenProjectPage_ChangesToo()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = Loaded(api);
                store.Open(store.Project("p1"));
                store.Detail.LoadAsync().GetAwaiter().GetResult();
                Assert.IsTrue(store.SetDueDateAsync("p1", new DateTime(2026, 9, 30)).GetAwaiter().GetResult());
                Assert.AreEqual("2026-09-30", store.Detail.Project.DueDate);
                Assert.AreEqual("2026-09-30", store.DueDateOf("p1"));
            });
        }

        [TestMethod]
        public void Outcome_ShowsAtOnce_ThenSavesTheTenPointValue()
        {
            OnSta(() =>
            {
                var api = new FakeApi { Pending = new TaskCompletionSource<ApiResponse>() };
                var store = Loaded(api);
                var saving = store.SetOutcomeAsync("p1", 8);
                Assert.AreEqual(8, store.Project("p1").Outcome);
                Assert.IsTrue(store.IsUpdatingOutcome("p1"));
                api.Pending.SetResult(new ApiResponse(200, "{}"));
                Assert.IsTrue(saving.GetAwaiter().GetResult());
                var body = api.Updates.Single().Item2;
                Assert.AreEqual(8, (int)body["outcome"]);
                Assert.AreEqual(1, body.Count, "only the outcome is sent, never importance");
                Assert.AreEqual(8, store.Project("p1").Outcome, "the reload reads it back");
            });
        }

        [TestMethod]
        public void Outcome_NotSet_SendsNull_AndARefusalPutsTheOldRatingBack()
        {
            OnSta(() =>
            {
                var api = new FakeApi { Reply = new ApiResponse(400, "{\"message\":\"outcome must be 1-10\"}") };
                var store = Loaded(api);
                store.Open(store.Project("p1"));
                store.Detail.LoadAsync().GetAwaiter().GetResult();
                Assert.IsFalse(store.SetOutcomeAsync("p1", null).GetAwaiter().GetResult());
                Assert.AreEqual(JTokenType.Null, api.Updates.Single().Item2["outcome"].Type);
                Assert.AreEqual(7, store.Project("p1").Outcome, "the legacy odd value is back");
                Assert.AreEqual(7, store.Detail.Project.Outcome);
                StringAssert.Contains(store.StatusMessage, "outcome must be 1-10");
                Assert.IsFalse(store.IsUpdatingOutcome("p1"));
            });
        }

        [TestMethod]
        public void Outcome_Unchanged_IsNotAWrite()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = Loaded(api);
                Assert.IsTrue(store.SetOutcomeAsync("p1", 7).GetAwaiter().GetResult());
                Assert.AreEqual(0, api.Updates.Count);
            });
        }

        [TestMethod]
        public void Create_SendsThePickedOutcome()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = Loaded(api);
                var created = store.CreateProjectAsync("  [TEST] New  ", 6).GetAwaiter().GetResult();
                Assert.AreEqual("[TEST] New|6", api.CreateBodies.Single());
                Assert.AreEqual(6, created.Outcome);
            });
        }

        [TestMethod]
        public void Header_IsEditableByTheOwnerOnly()
        {
            OnSta(() =>
            {
                var api = new FakeApi();
                var mine = Loaded(api);
                mine.Open(mine.Project("p1"));
                mine.Detail.LoadAsync().GetAwaiter().GetResult();
                Assert.IsTrue(mine.Detail.CanEditHeader);
                var theirs = new ProjectsStore(ToDoStrings.Fallbacks(), "u2", api, () => Now);
                theirs.ScreenAppearedAsync().GetAwaiter().GetResult();
                theirs.Open(theirs.Project("p1"));
                theirs.Detail.LoadAsync().GetAwaiter().GetResult();
                Assert.IsFalse(theirs.Detail.CanEditHeader);
            });
        }

        [TestMethod]
        public void OutcomeLabels_MatchTheMac()
        {
            var s = ToDoStrings.Fallbacks();
            Assert.AreEqual("Set outcome", s.OutcomeChipLabel(null));
            Assert.AreEqual("4 / 5 · Can postpone a few days", s.OutcomeChipLabel(8));
            Assert.AreEqual("3 / 5 · Can postpone a few weeks", s.OutcomeChipLabel(7));
            var rows = ProjectOutcomeEdit.Options(7).Select(s.OutcomePickerRow).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "Not set",
                "1 / 5 · Can postpone a year or more",
                "2 / 5 · Can postpone a few months",
                "3 / 5 · Can postpone a few weeks",
                "4 / 5 · Can postpone a few days",
                "5 / 5 · Hard deadline — can't shift",
                "Current: 3.5 / 5 · Can postpone a few weeks",
            }, rows);
        }

        [TestMethod]
        public void NextTasksAndStatus_DoNotRedrawTheWholeScreen()
        {
            // Every NEXT answer used to raise Changed, so a big portfolio rebuilt every card ten times a
            // second while they arrived, and a click landing mid-rebuild was lost.
            OnSta(() =>
            {
                var api = new FakeApi();
                var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", api, () => Now);
                int changed = 0, status = 0;
                var nextFor = new List<string>();
                store.Changed += () => changed++;
                store.StatusChanged += () => status++;
                store.NextTaskChanged += id => nextFor.Add(id);
                store.ScreenAppearedAsync().GetAwaiter().GetResult();
                SpinWait.SpinUntil(() => nextFor.Count > 0, 2000);
                CollectionAssert.Contains(nextFor, "p1");
                int afterLoad = changed;
                store.ShowStatus("hello");
                Assert.AreEqual(afterLoad, changed, "a status line is not a redraw");
                Assert.AreEqual(1, status);
            });
        }
    }
}
