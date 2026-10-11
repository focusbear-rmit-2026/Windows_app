using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace FocusBear.Core.Tests.Forecast
{
    /// <summary>
    /// The "What matters" view built from the local forecast — ported from the Mac's
    /// LocalForecastPortfolioTests (reports 2026-10-04_21-05-20 and 2026-10-05_08-24-02).
    /// </summary>
    [TestClass]
    public class LocalForecastPortfolioTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 10);

        private static DateTime Day(int offset) => Today.AddDays(offset);

        /// <summary>Fourteen awake hours busy leaves two work hours a day with default sleep and no extra care.</summary>
        private static F.DayInput TwoHourDay(int offset)
        {
            DateTime date = Day(offset);
            return F.DayInput.FromIntervals(date, new[] { new F.Interval(date.AddHours(6), date.AddHours(20)) }, null);
        }

        private static Project ProjectRow(string id, int? due = null, int outcome = 6, int? total = null, int? open = null)
        {
            return new Project(id, id)
            {
                DueDate = due.HasValue ? ToDoDates.Ymd(Day(due.Value)) : null,
                Outcome = outcome,
                TaskCount = total,
                UncompletedTaskCount = open,
            };
        }

        private static F.TaskInput Task(string id, string project, double? minutes, int? notBefore = null, bool completed = false)
        {
            return new F.TaskInput(id, project, minutes, notBefore: notBefore.HasValue ? Day(notBefore.Value) : (DateTime?)null,
                                   completed: completed);
        }

        private static F.Result Forecast(IList<Project> projects, IEnumerable<F.TaskInput> tasks, int days)
        {
            return F.Calculate(Enumerable.Range(0, days).Select(TwoHourDay), ForecastSnapshot.Projects(projects), tasks);
        }

        private static LocalForecastPortfolio Portfolio(IList<Project> projects, IEnumerable<F.TaskInput> tasks, int days = 3,
                                                        IDictionary<string, double> topScores = null)
        {
            return LocalForecastPortfolio.Make(projects, Forecast(projects, tasks, days), topScores);
        }

        private static string[] Ids(IEnumerable<PortfolioProject> rows) => rows.Select(r => r.Id).ToArray();

        // MARK: - Buckets and evidence

        [TestMethod]
        public void KnownWorkThatCannotFitBeforeTheDueDateAsksForAnExtension()
        {
            var noat = ProjectRow("noat", due: 3);
            var result = Portfolio(new[] { noat }, new[] { Task("slides", "noat", 1000) }, days: 30,
                                   topScores: new Dictionary<string, double> { { "noat", 13.9 } });
            CollectionAssert.AreEqual(new[] { "noat" }, Ids(result.Groups.AskForExtension));
            Assert.AreEqual(0, result.Groups.StartNow.Count);
            PortfolioProject row = result.Rows.Single();
            Assert.AreEqual(PortfolioBucket.Critical, row.Bucket);
            CollectionAssert.Contains(row.BadgeList.ToList(), PortfolioBadge.Overcommitted);
            Assert.IsTrue(result.IsOvercommitted);
            Assert.IsTrue(result.AssessedProjectIds.Contains("noat"));
        }

        [TestMethod]
        public void FinishingEveryTaskIsAnAssessedOnTrackProject()
        {
            // The server still counts five of fifteen open; what the forecast downloaded is the honest number.
            var result = Portfolio(new[] { ProjectRow("mrff", due: 0, total: 15, open: 5) }, new F.TaskInput[0]);
            PortfolioProject row = result.Rows.Single();
            Assert.AreEqual(PortfolioBucket.OnTrack, row.Bucket);
            Assert.IsTrue(result.AssessedProjectIds.Contains("mrff"));
            CollectionAssert.AreEqual(new[] { "mrff" }, Ids(result.Groups.BackBurner));
            Assert.IsFalse(result.IsOvercommitted);
            Assert.AreEqual(0, row.BadgeList.Count);
        }

        [TestMethod]
        public void MissingEstimatesStayVisibleWithoutInventedRiskOrTotalEffort()
        {
            var result = Portfolio(new[] { ProjectRow("unknown", due: 0) }, new[] { Task("a", "unknown", null) });
            CollectionAssert.AreEqual(new[] { "unknown" }, Ids(result.Groups.StartNow));
            Assert.AreEqual(0, result.Groups.AskForExtension.Count);
            Assert.IsFalse(result.AssessedProjectIds.Contains("unknown"));
            Assert.IsNull(result.Rows.Single().RequiredHours);
            Assert.IsFalse(result.IsOvercommitted);
        }

        [TestMethod]
        public void ADeadlineBeyondTheHorizonIsNotAMissedDeadline()
        {
            var result = Portfolio(new[] { ProjectRow("distant", due: 20) }, new[] { Task("a", "distant", 1000) }, days: 1);
            Assert.AreEqual(0, result.Groups.AskForExtension.Count);
            Assert.IsFalse(result.AssessedProjectIds.Contains("distant"));
            CollectionAssert.AreEqual(new[] { "distant" }, Ids(result.Groups.BackBurner));
            Assert.IsFalse(result.IsOvercommitted);
        }

        [TestMethod]
        public void RemainingEffortAndDaysUntilDueComeFromTheForecast()
        {
            var result = Portfolio(new[] { ProjectRow("work", due: 2) }, new[] { Task("a", "work", 90) });
            PortfolioProject row = result.Rows.Single();
            Assert.AreEqual(1.5, row.RequiredHours.Value, 0.0001);
            Assert.AreEqual(2, row.DaysUntilDue);
            Assert.AreEqual(ToDoDates.Ymd(Day(2)), row.DueDate);
            CollectionAssert.Contains(row.BadgeList.ToList(), PortfolioBadge.StartNow, "work placed today earns the badge");
        }

        [TestMethod]
        public void AProjectOutsideTheCatalogueGetsNoCard()
        {
            var catalogue = new[] { ProjectRow("known", due: 2) };
            F.Result forecast = Forecast(new[] { ProjectRow("known", due: 2), ProjectRow("gone", due: 2) },
                                         new[] { Task("a", "known", 30), Task("b", "gone", 30) }, 3);
            CollectionAssert.AreEqual(new[] { "known" }, Ids(LocalForecastPortfolio.Make(catalogue, forecast).Rows));
        }

        // MARK: - Lanes (report 2026-10-04_21-05-20)

        /// <summary>The user's board, in miniature: thirty planned days, two work hours a day.</summary>
        [TestMethod]
        public void TheReportedBoardLandsInPredictableLanes()
        {
            var projects = new[]
            {
                ProjectRow("noat", due: 4), ProjectRow("review", due: 2), ProjectRow("focus", due: 24),
                ProjectRow("sean", due: 90), ProjectRow("steady", due: 20), ProjectRow("undated"),
                ProjectRow("reduce", due: 1), ProjectRow("stale", due: -30),
                ProjectRow("geelong", due: -16, total: 1, open: 0), ProjectRow("dmg", due: -11, total: 1, open: 0),
            };
            var tasks = new[]
            {
                Task("slides", "noat", 41), Task("insar", "review", null), Task("glenn", "focus", null),
                Task("bed", "sean", 15, notBefore: 40), Task("chip", "steady", 60), Task("someday", "undated", 30),
            };
            var result = Portfolio(projects, tasks, days: 30, topScores: new Dictionary<string, double>
            {
                { "noat", 13.9 }, { "review", 0.9 }, { "focus", 0.6 }, { "sean", 0.3 },
            });

            Assert.AreEqual(0, result.Groups.AskForExtension.Count);
            CollectionAssert.AreEqual(new[] { "noat", "review", "focus" }, Ids(result.Groups.StartNow),
                                      "due this week or at risk — highest TOP score first");
            CollectionAssert.AreEqual(new[] { "steady" }, Ids(result.Groups.SteadyProgress));
            CollectionAssert.AreEqual(new[] { "sean", "stale", "reduce", "undated", "geelong", "dmg" }, Ids(result.Groups.BackBurner),
                                      "beyond the horizon, undated and empty projects are backburner; finished ones sink to the bottom");
            Assert.AreEqual(PortfolioBucket.OnTrack, result.Rows.Single(r => r.Id == "noat").Bucket);
        }

        /// <summary>Report 2026-10-05_08-24-02: undated projects with stale overdue tasks outranked NOAT.</summary>
        [TestMethod]
        public void UndatedProjectsGoToBackburnerAndADueSoonProjectLeadsStartNow()
        {
            string[] undated = { "product-management", "phd", "finance", "fundraising", "hr", "meta" };
            double[] scores = { 110, 90, 89, 88, 72, 27 };
            var projects = undated.Select(id => ProjectRow(id)).Concat(new[] { ProjectRow("noat", due: 3), ProjectRow("review", due: 23) }).ToList();
            var tasks = undated.Select(id => Task(id + "-next", id, 30))
                .Concat(new[] { Task("slides", "noat", 41), Task("insar", "review", null) });
            var top = undated.Select((id, i) => new { id, score = scores[i] }).ToDictionary(x => x.id, x => x.score);
            top["noat"] = 13.9;
            top["review"] = 0.9;

            var result = Portfolio(projects, tasks, days: 30, topScores: top);

            CollectionAssert.AreEqual(new[] { "noat", "review" }, Ids(result.Groups.StartNow));
            CollectionAssert.AreEqual(undated, Ids(result.Groups.BackBurner), "undated, ordered by TOP score");
        }

        [TestMethod]
        public void AnEmptyProjectDueTodayGoesToBackburnerAndKeepsItsDate()
        {
            var result = Portfolio(new[] { ProjectRow("reduce", due: 0), ProjectRow("noat", due: 3) },
                                   new[] { Task("slides", "noat", 41) }, days: 30);
            CollectionAssert.AreEqual(new[] { "noat" }, Ids(result.Groups.StartNow));
            CollectionAssert.AreEqual(new[] { "reduce" }, Ids(result.Groups.BackBurner));
            PortfolioProject reduce = result.Rows.Single(r => r.Id == "reduce");
            Assert.IsTrue(reduce.HasDueDate);
            Assert.AreEqual(0, reduce.DaysUntilDue);
        }

        [TestMethod]
        public void AnUndatedEmptyProjectHasNoInventedDate()
        {
            var result = Portfolio(new[] { ProjectRow("reduce") }, new F.TaskInput[0], days: 30);
            CollectionAssert.AreEqual(new[] { "reduce" }, Ids(result.Groups.BackBurner));
            Assert.IsFalse(result.Rows.Single().HasDueDate);
            Assert.IsNull(result.Rows.Single().DaysUntilDue);
        }

        // MARK: - Next tasks

        [TestMethod]
        public void NextTasksComeFromThePlannedOpenTasksOnly()
        {
            var planned = new[] { Task("mine", "p", 30), Task("done", "p", 30, completed: true), Task("other", "q", 30) };
            var tasks = new[]
            {
                new ToDoTask { Id = "mine", Title = "Mine", ProjectId = "p" },
                new ToDoTask { Id = "done", Title = "Done", ProjectId = "p" },
                new ToDoTask { Id = "unplanned", Title = "Somebody else's", ProjectId = "p" },
                new ToDoTask { Id = "other", Title = "Other", ProjectId = "q" },
            };
            var next = LocalForecastPortfolio.NextTasks(planned, tasks, Today);
            Assert.AreEqual("mine", next["p"].Id);
            Assert.AreEqual("other", next["q"].Id);
            Assert.AreEqual(2, next.Count);
        }
    }
}
