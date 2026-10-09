using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Focus_Bear.API;
using Focus_Bear.ToDoScreens;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// "What matters" reads its lanes from the local reprioritise forecast when there is one (the Mac's
    /// <c>ProjectsStore.localPortfolio</c>), and the server's portfolio rows only until then.
    /// </summary>
    [TestClass]
    public class ProjectsStoreLocalLanesSeamTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 10, 9, 0, 0);

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

        private sealed class ServerApi : IProjectsApi
        {
            /// <summary>The server calls this project critical; the local forecast knows better.</summary>
            public Task<PortfolioSnapshot> GetPortfolio() => Task.FromResult(new PortfolioSnapshot(new List<PortfolioProject>
            {
                new PortfolioProject { Id = "noat", Name = "NOAT", BucketRaw = PortfolioBucketWire.Critical, DueDate = "2026-09-13" },
            }, true));

            public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status) =>
                Task.FromResult(new ToDoProjectsPage(new List<Project>(), false));
            public Task<Project> GetProject(string id) => Task.FromResult<Project>(null);
            public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page) => Task.FromResult<ToDoPage>(null);
            public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Task.FromResult<IList<ToDoTask>>(new List<ToDoTask>());
            public Task<ApiResponse> UpdateProject(string id, JObject body) => Task.FromResult(new ApiResponse(200, ""));
            public Task<bool> DeleteProject(string id) => Task.FromResult(true);
            public Task<ApiResponse> CreateProject(string name, int? outcome) => Task.FromResult(new ApiResponse(500, ""));
            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body) => Task.FromResult(new ApiResponse(200, ""));
        }

        private sealed class FakeForecast : IPortfolioForecast
        {
            public event Action Changed;
            public F.Result Result { get; set; }
            public IList<Project> Projects { get; set; } = new List<Project>();
            public IList<ToDoTask> Tasks { get; set; } = new List<ToDoTask>();
            public IList<F.TaskInput> TaskInputs { get; set; } = new List<F.TaskInput>();
            public int StaleChecks;
            public int Refreshes;
            public void RefreshIfStale() => StaleChecks++;
            public void Refresh() => Refreshes++;
            public void Calculated() => Changed?.Invoke();
        }

        /// <summary>NOAT, due in three days with 41 minutes of work, on two-hour days: on track, and due this week.</summary>
        private static FakeForecast OnTrackNoat()
        {
            var projects = new List<Project> { new Project("noat", "NOAT") { DueDate = "2026-09-13", Outcome = 6 } };
            var inputs = new List<F.TaskInput> { new F.TaskInput("slides", "noat", 41) };
            var days = Enumerable.Range(0, 30).Select(offset =>
            {
                DateTime date = Now.Date.AddDays(offset);
                return F.DayInput.FromIntervals(date, new[] { new F.Interval(date.AddHours(6), date.AddHours(20)) }, null);
            });
            return new FakeForecast
            {
                Result = F.Calculate(days, ForecastSnapshot.Projects(projects), inputs),
                Projects = projects,
                Tasks = new List<ToDoTask> { new ToDoTask { Id = "slides", Title = "Slides", ProjectId = "noat" } },
                TaskInputs = inputs,
            };
        }

        [TestMethod]
        public void WithAForecast_TheLanesAndTheBannerComeFromIt()
        {
            OnSta(() =>
            {
                var forecast = OnTrackNoat();
                var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", new ServerApi(), () => Now, forecast);
                store.ScreenAppearedAsync().GetAwaiter().GetResult();

                Assert.AreEqual(1, forecast.StaleChecks, "showing the tab recalculates an old forecast");
                CollectionAssert.AreEqual(new[] { "noat" }, store.ActionGroups.StartNow.Select(p => p.Id).ToList());
                Assert.AreEqual(0, store.ActionGroups.AskForExtension.Count, "the server's critical bucket is not used");
                Assert.IsFalse(store.IsOvercommitted);
                Assert.AreEqual("slides", store.NextTask("noat").Id, "the card's NEXT task is the forecast's");
                Assert.IsFalse(store.IsNextTaskPending("noat"));
            });
        }

        [TestMethod]
        public void WithoutAForecast_TheServerRowsAreGrouped()
        {
            OnSta(() =>
            {
                var forecast = new FakeForecast();
                var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", new ServerApi(), () => Now, forecast);
                store.ScreenAppearedAsync().GetAwaiter().GetResult();

                Assert.IsNull(store.LocalPortfolio);
                Assert.IsTrue(store.HasPortfolio);
                CollectionAssert.AreEqual(new[] { "noat" }, store.ActionGroups.AskForExtension.Select(p => p.Id).ToList());
                Assert.IsTrue(store.IsOvercommitted);
                Assert.IsTrue(store.IsAssessed("noat"), "server rows keep their bucket badge");
            });
        }

        [TestMethod]
        public void ANewCalculation_RedrawsAndAWriteRecalculates()
        {
            OnSta(() =>
            {
                var forecast = new FakeForecast();
                var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", new ServerApi(), () => Now, forecast);
                int redraws = 0;
                store.Changed += () => redraws++;
                forecast.Calculated();
                Assert.AreEqual(1, redraws);

                store.RefreshAsync().GetAwaiter().GetResult();
                Assert.AreEqual(1, forecast.Refreshes, "a write changes what the forecast was built from");
            });
        }

        [TestMethod]
        public void TheOverloadedBannerLink_AsksTheHostForTheReschedulePlan()
        {
            OnSta(() =>
            {
                var store = new ProjectsStore(ToDoStrings.Fallbacks(), "u1", new ServerApi(), () => Now, new FakeForecast());
                int requests = 0;
                store.ReprioritiseRequested += () => requests++;
                store.OpenReprioritisePlan();
                Assert.AreEqual(1, requests);
            });
        }
    }
}
