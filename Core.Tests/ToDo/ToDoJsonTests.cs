using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The decoding rules the Mac's Codable models carry (<c>LenientRow</c>, <c>LenientInt</c>,
    /// <c>@DefaultEmpty</c>), applied by the Windows reader of <c>GET /to-do</c> and <c>GET /projects</c>.
    /// </summary>
    [TestClass]
    public class ToDoJsonTests
    {
        private const string Page = @"{
          ""data"": [
            { ""id"": ""t1"", ""title"": ""Write report"", ""status"": ""IN_PROGRESS"",
              ""due_date"": ""2026-09-25T00:00:00.000Z"", ""created_at"": ""2026-09-01T10:11:12.345Z"",
              ""outcome"": 8, ""perspiration_level"": 3, ""top_score"": 4.25, ""duration"": ""300"",
              ""tags"": [{ ""id"": ""p1"", ""text"": ""Work"" }],
              ""subtasks"": [{ ""name"": ""Outline"", ""is_completed"": true }, { ""name"": ""Draft"", ""is_completed"": false }],
              ""assignee"": { ""id"": ""u9"", ""username"": ""sam"" },
              ""eisenhower_quadrant"": 2, ""project_id"": ""p1"", ""custom_status_id"": ""c1"" },
            { ""id"": ""t2"", ""status"": ""COMPLETED"", ""duration"": 600 },
            { ""title"": ""no id — unreadable"" },
            42
          ],
          ""meta"": { ""page"": 1, ""take"": 50, ""itemCount"": 7, ""hasNextPage"": true }
        }";

        [TestMethod]
        public void ReadPage_KeepsReadableRowsAndCountsEveryRowReceived()
        {
            var page = ToDoJson.ReadPage(Page);

            Assert.IsNotNull(page);
            CollectionAssert.AreEqual(new[] { "t1", "t2" }, page.Tasks.Select(t => t.Id).ToArray());
            Assert.AreEqual(4, page.ReceivedRowCount);
            Assert.AreEqual(7, page.ItemCount);
            Assert.IsTrue(page.HasNextPage);
        }

        [TestMethod]
        public void ReadPage_ReadsEveryField()
        {
            var task = ToDoJson.ReadPage(Page).Tasks[0];

            Assert.AreEqual("Write report", task.Title);
            Assert.AreEqual(ToDoStatus.InProgress, task.DefaultStatus);
            Assert.AreEqual("2026-09-25T00:00:00.000Z", task.DueDate, "timestamps stay exactly as sent");
            Assert.AreEqual("2026-09-01T10:11:12.345Z", task.CreatedAt);
            Assert.AreEqual(8, task.Outcome);
            Assert.AreEqual(3, task.PerspirationLevel);
            Assert.AreEqual(4.25, task.TopScore);
            Assert.AreEqual(300, task.Duration, "a string duration still reads");
            Assert.AreEqual(new ToDoTag("p1", "Work"), task.Tags.Single());
            Assert.AreEqual(2, task.Subtasks.Count);
            Assert.IsTrue(task.Subtasks[0].IsCompleted);
            Assert.AreEqual("u9", task.Assignee.Id);
            Assert.AreEqual(2, task.EisenhowerQuadrant);
            Assert.AreEqual("p1", task.ProjectId);
            Assert.AreEqual("c1", task.CustomStatusId);
        }

        [TestMethod]
        public void ReadPage_MissingTitleAndCreatedAtReadAsEmpty()
        {
            var task = ToDoJson.ReadPage(Page).Tasks[1];

            Assert.AreEqual("", task.Title);
            Assert.AreEqual("", task.CreatedAt);
            Assert.AreEqual(600, task.Duration);
            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(0, task.Tags.Count);
        }

        [TestMethod]
        public void ReadPage_UnreadableBodyIsNull()
        {
            Assert.IsNull(ToDoJson.ReadPage("not json"));
            Assert.IsNull(ToDoJson.ReadPage("{\"meta\":{}}"));
            Assert.IsNull(ToDoJson.ReadPage(null));
        }

        [TestMethod]
        public void ReadTasks_AcceptsABareArrayOrAnEnvelope()
        {
            Assert.AreEqual("a", ToDoJson.ReadTasks("[{\"id\":\"a\",\"status\":\"NOT_STARTED\"}]").Single().Id);
            Assert.AreEqual("b", ToDoJson.ReadTasks("{\"data\":[{\"id\":\"b\"}]}").Single().Id);
        }

        [TestMethod]
        public void ReadTask_WithoutStatusIsNotStarted()
        {
            Assert.AreEqual(ToDoStatus.NotStarted, ToDoJson.ReadTask("{\"id\":\"a\"}").DefaultStatus);
        }

        [TestMethod]
        public void ReadProjectsPage_PrefersTheLongerListAndReadsColumns()
        {
            var page = ToDoJson.ReadProjectsPage(@"{
              ""data"": [],
              ""projects"": [
                { ""id"": ""p1"", ""name"": ""Work"", ""custom_statuses"": [
                    { ""id"": ""c2"", ""label"": ""Review"", ""order"": 2 },
                    { ""id"": ""c1"", ""label"": ""Backlog"", ""order"": 1, ""should_complete_task"": false },
                    { ""id"": ""c3"", ""label"": ""Shipped"", ""order"": 3, ""should_complete_task"": true } ],
                  ""members"": [{ ""id"": ""m1"", ""user_id"": ""u1"", ""email"": ""a@b.c"", ""invitation_status"": ""accepted"" }] },
                { ""name"": ""no id"" }
              ],
              ""meta"": { ""hasNextPage"": false }
            }");

            var project = page.Projects.Single();
            Assert.AreEqual("Work", project.Name);
            CollectionAssert.AreEqual(new[] { "c1", "c2", "c3" }, project.OrderedStatuses.Select(s => s.Id).ToArray());
            Assert.IsTrue(project.UsesCustomStatuses);
            Assert.IsTrue(project.OrderedStatuses.Last().Completes);
            Assert.AreEqual("u1", project.MemberList.Single().UserId);
            Assert.IsFalse(page.HasNextPage);
        }

        [TestMethod]
        public void ReadProject_ReadsTheCardFields()
        {
            var project = ToDoJson.ReadProject(@"{ ""id"": ""p1"", ""name"": ""Thesis"", ""description"": ""PhD"",
                ""due_date"": ""2026-12-01T00:00:00.000Z"", ""icon"": ""1f393"", ""outcome"": 8,
                ""task_count"": 12, ""uncompleted_task_count"": 5, ""status"": ""completed"",
                ""metadata"": { ""tags"": [""phd"", 3] } }");

            Assert.AreEqual("PhD", project.Description);
            Assert.AreEqual("2026-12-01T00:00:00.000Z", project.DueDate);
            Assert.AreEqual("1f393", project.Icon);
            Assert.AreEqual(8, project.Outcome);
            Assert.AreEqual(12, project.TaskCount);
            Assert.AreEqual(5, project.UncompletedTaskCount);
            Assert.AreEqual(ProjectLifecycleStatus.Completed, project.LifecycleStatus);
            CollectionAssert.AreEqual(new[] { "phd" }, project.Tags.ToArray());
        }

        [TestMethod]
        public void ReadPortfolio_ReadsRiskRowsAndGroupsThem()
        {
            var portfolio = ToDoJson.ReadPortfolio(@"{
              ""projects"": [
                { ""id"": ""a"", ""name"": ""Late"", ""bucket"": ""critical"", ""risk_score"": 9.5, ""required_hours"": 12,
                  ""days_until_due"": -2, ""due_date"": ""2026-09-22"", ""badges"": [""overcommitted"", ""brand_new""] },
                { ""id"": ""b"", ""name"": ""Soon"", ""bucket"": ""at_risk"", ""risk_score"": 4 },
                { ""id"": ""c"", ""name"": ""Someday"", ""bucket"": ""no_deadline"" },
                { ""name"": ""no id"" }
              ],
              ""overcommitted"": true
            }");

            Assert.IsTrue(portfolio.IsOvercommitted);
            Assert.AreEqual(3, portfolio.Projects.Count);
            var late = portfolio.Projects[0];
            Assert.AreEqual(PortfolioBucket.Critical, late.Bucket);
            Assert.IsTrue(late.IsOverdue);
            Assert.AreEqual(12, late.Hours);
            Assert.AreEqual(PortfolioBadge.Overcommitted, late.BadgeList[0]);

            var groups = PortfolioGrouping.Group(portfolio.Projects);
            Assert.AreEqual("a", groups.AskForExtension.Single().Id);
            Assert.AreEqual("b", groups.StartNow.Single().Id);
        }

        [TestMethod]
        public void ReadProject_ToleratesANullMetadata()
        {
            // Real rows send "metadata": null; indexing into it threw and emptied the whole page.
            var page = ToDoJson.ReadProjectsPage("{\"data\":[{\"id\":\"p1\",\"name\":\"A\",\"metadata\":null},{\"id\":\"p2\",\"name\":\"B\",\"metadata\":{}}]}");
            Assert.AreEqual(2, page.Projects.Count);
            Assert.AreEqual(0, page.Projects[0].Tags.Count);
        }

        [TestMethod]
        public void ReadPortfolio_WithoutProjectsIsNull()
        {
            Assert.IsNull(ToDoJson.ReadPortfolio("{}"));
        }
    }
}
