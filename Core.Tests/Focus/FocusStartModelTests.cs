using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Focus;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Focus
{
    [TestClass]
    public class FocusStartModelTests
    {
        private static readonly FocusStartStrings Strings = new FocusStartStrings();

        [TestMethod]
        public void TheDialStepsByFiveMinutes_WithinOneMinuteToEightHours()
        {
            Assert.AreEqual(30, new FocusStartDuration(25).Stepped(1).Minutes);
            Assert.AreEqual(1, new FocusStartDuration(3).Stepped(-1).Minutes, "never below one minute");
            Assert.AreEqual(480, new FocusStartDuration(478).Stepped(2).Minutes);
            Assert.AreEqual(90, FocusStartDuration.FromSliderFraction(0.19).Minutes, "0.19 × 480 = 91.2, snapped to 90");
            Assert.AreEqual(1, FocusStartDuration.FromSliderFraction(0).Minutes);
            Assert.AreEqual(720, new FocusStartDuration(25, isUntimed: true).EffectiveMinutes, "untimed runs 12 hours");
            Assert.AreEqual(25, FocusStartDuration.FromStored("junk").Minutes);
            Assert.AreEqual(480, FocusStartDuration.FromStored("900").Minutes);
        }

        [TestMethod]
        public void TheStartButtonAndSummariesReadLikeTheDesign()
        {
            Assert.AreEqual("Start a 25 min session", Strings.StartButtonTitle(new FocusStartDuration(25)));
            Assert.AreEqual("Start a 1 hr 30 min session", Strings.StartButtonTitle(new FocusStartDuration(90)));
            Assert.AreEqual("Start a 2 hr session", Strings.StartButtonTitle(new FocusStartDuration(120)));
            Assert.AreEqual("Start an untimed session", Strings.StartButtonTitle(new FocusStartDuration(25, true)));
            Assert.AreEqual("25 mins · 1 break", Strings.DurationSummary(new FocusStartDuration(25), 1));
            Assert.AreEqual("No Goal · 0 tasks", Strings.GoalSummary("  ", 0));
            Assert.AreEqual("Ship it · 1 task", Strings.GoalSummary("Ship it", 1));
            Assert.AreEqual("01", FocusStartStrings.ClockComponents(new FocusStartDuration(65)).Item1);
            Assert.IsNull(FocusStartStrings.ClockComponents(new FocusStartDuration(65, true)));
        }

        [TestMethod]
        public void ConfigCopyWinsOverTheDefaults()
        {
            var strings = new FocusStartStrings(JObject.Parse(@"{ ""titleSimple"": ""Empezar"", ""buttonStartTimed"": """" }"));
            Assert.AreEqual("Empezar", strings["titleSimple"]);
            Assert.AreEqual("Start a 5 min session", strings.StartButtonTitle(new FocusStartDuration(5)), "an empty override keeps the default");
        }

        [TestMethod]
        public void BreaksCountOnlyWhenMicroBreaksAreOn()
        {
            Assert.AreEqual(1, FocusStartWorkSelection.BreakCount(new FocusStartDuration(25), true, 15));
            Assert.AreEqual(0, FocusStartWorkSelection.BreakCount(new FocusStartDuration(15), true, 15), "no break right at the end");
            Assert.AreEqual(0, FocusStartWorkSelection.BreakCount(new FocusStartDuration(120), false, 15));
        }

        [TestMethod]
        public void DayPlanProjectsComeFirst_ThenAToZ()
        {
            var projects = new List<Project> { P("1", "Zeta"), P("2", "alpha"), P("3", "Mid") };
            var ordered = FocusStartWorkSelection.Projects(projects, new[] { "mid", null, "unknown" });
            CollectionAssert.AreEqual(new[] { "Mid", "alpha", "Zeta" }, ordered.Select(p => p.Name).ToArray());
            Assert.AreEqual("3", FocusStartWorkSelection.TopDayPlanProject(projects, new[] { "unknown", "MID" }).Id);
        }

        [TestMethod]
        public void TheChosenProjectsTasksLead_EachGroupByTopScore()
        {
            var tasks = new List<ToDoTask>
            {
                T("a", "p2", 20.2), T("b", "p1", 4.6), T("c", "p1", 9.1), T("d", "p2", 1), T("done", "p1", 99, "COMPLETED"),
            };
            var ordered = FocusStartWorkSelection.Tasks(tasks, P("p1", "One"));
            CollectionAssert.AreEqual(new[] { "c", "b", "a", "d" }, ordered.Select(t => t.Id).ToArray());
        }

        [TestMethod]
        public void TheQueueCarriesThePlayersFields_FirstTaskPlaying()
        {
            var queue = JArray.Parse(FocusStartWorkSelection.SessionQueue(new List<ToDoTask> { T("a", "p1", 3), T("b", null, null) }, new List<Project> { P("p1", "One") }));
            Assert.AreEqual(true, (bool)queue[0]["isPlaying"]);
            Assert.AreEqual(false, (bool)queue[1]["isPlaying"]);
            Assert.AreEqual(600, (int)queue[1]["duration"], "tasks without a duration get ten minutes");
            Assert.AreEqual("One", (string)queue[0]["project_name"]);
            Assert.AreEqual("[]", FocusStartWorkSelection.SessionQueue(new List<ToDoTask>(), null));
        }

        [TestMethod]
        public void ATypedSearchWithNoSuchTask_IsOfferedAsANewTask()
        {
            var rows = new[] { new ToDoTask { Id = "a", Title = "Windows app review" } };
            Assert.AreEqual("windows app coding", FocusStartWorkSelection.NewTaskTitle("  windows app coding ", rows));
            Assert.IsNull(FocusStartWorkSelection.NewTaskTitle("WINDOWS APP REVIEW", rows), "an existing title is picked, not duplicated");
            Assert.IsNull(FocusStartWorkSelection.NewTaskTitle("   ", rows));
            Assert.AreEqual("x", FocusStartWorkSelection.NewTaskTitle("x", null));
            Assert.AreEqual("a", FocusStartWorkSelection.ExactMatch(" windows app REVIEW", rows).Id);
            Assert.IsNull(FocusStartWorkSelection.ExactMatch("windows", rows), "a partial match is not what Enter picks");
        }

        [TestMethod]
        public void ANewSessionTask_IsDueToday_InTheGoalsProject()
        {
            var now = new System.DateTime(2026, 9, 26, 15, 30, 0);
            var task = FocusStartWorkSelection.NewSessionTask("Write it", now, P("p1", "Focus Bear"));
            Assert.AreEqual("Write it", task.Title);
            Assert.AreEqual("2026-09-26", task.DueDate);
            Assert.AreEqual("p1", task.ProjectId);
            Assert.AreEqual("NOT_STARTED", task.Status);
            Assert.IsFalse(string.IsNullOrEmpty(task.Id), "PUT /to-do inserts a row under the client's id");
            Assert.IsNull(FocusStartWorkSelection.NewSessionTask("Loose", now, null).ProjectId);
        }

        private static Project P(string id, string name) => new Project(id, name);

        private static ToDoTask T(string id, string project, double? top, string status = "NOT_STARTED") =>
            new ToDoTask { Id = id, Title = id, ProjectId = project, TopScore = top, Status = status };
    }
}
