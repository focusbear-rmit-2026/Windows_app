using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The reschedule plan's writes against a fake API that answers in the real wire shapes: the catalogue
    /// row from <c>GET /to-do</c>, the re-read from <c>GET /to-do/by-ids</c> (through the app's own merge), and
    /// the saved task from <c>PUT /to-do</c>. "Saved 0 date changes, then stopped…" (2026-09-25) was the
    /// re-read's due date coming back re-spelled and every dated task being refused before its PUT.
    /// </summary>
    [TestClass]
    public class ReprioritiseWritesSeamTests
    {
        private const string TaskId = "0b7d3a52-9c1e-4d8f-a6b2-3f4e5d6c7b8a";

        private static string Row(string due, string title = "[TEST] UX pass", string status = "NOT_STARTED", int outcome = 6, bool withUpdatedAt = false)
        {
            return "{\"id\":\"" + TaskId + "\",\"title\":\"" + title + "\",\"status\":\"" + status + "\",\"due_date\":\"" + due + "\"," +
                   "\"created_at\":\"2026-09-01T03:04:05.678Z\"," + (withUpdatedAt ? "\"updated_at\":\"2026-09-25T11:57:10.835Z\"," : "") +
                   "\"eisenhower_quadrant\":2,\"outcome\":" + outcome + ",\"duration\":\"300\",\"perspiration_level\":2," +
                   "\"project_id\":\"p-ux\",\"tags\":[{\"id\":\"p-ux\",\"text\":\"UX\"}],\"assignee\":{\"id\":\"me\",\"username\":\"jeremy\"}}";
        }

        private sealed class FakeApi : IReprioritiseApi
        {
            public string ServerRow = Row("2026-09-25T00:00:00.000Z", withUpdatedAt: true);
            public readonly List<IDictionary<string, object>> Upserts = new List<IDictionary<string, object>>();
            public int UpsertStatus = 200;

            public Task<string> GetToDosByIds(IEnumerable<string> ids)
            {
                // The app's own merge of the by-ids pages, over the body the server sends.
                var merged = new JArray();
                FocusBearAPIClient.AppendByIdsRows(merged, "[" + ServerRow + "]");
                return Task.FromResult(merged.ToString(Newtonsoft.Json.Formatting.None));
            }

            public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body)
            {
                Upserts.Add(body);
                if (UpsertStatus != 200) return Task.FromResult(new ApiResponse(UpsertStatus, "{\"message\":\"nope\"}"));
                // The server stores a plain day as midnight UTC and answers with the saved row.
                var saved = JObject.Parse(ServerRow);
                if (body.ContainsKey("due_date")) saved["due_date"] = body["due_date"] + "T00:00:00.000Z";
                if (body.ContainsKey("outcome")) saved["outcome"] = JToken.FromObject(body["outcome"]);
                if (body.ContainsKey("duration")) saved["duration"] = JToken.FromObject(body["duration"]);
                ServerRow = saved.ToString(Newtonsoft.Json.Formatting.None);
                return Task.FromResult(new ApiResponse(200, ServerRow));
            }

            public Task<Project> GetProject(string id) => Task.FromResult<Project>(null);
            public Task<ApiResponse> UpdateProject(string id, JObject body) => Task.FromResult(new ApiResponse(200, "{}"));
        }

        /// <summary>The row the plan was built from, as the catalogue reads it (<c>GET /to-do</c> page).</summary>
        private static ToDoTask CatalogueRow(string due = "2026-09-25T00:00:00.000Z") =>
            ToDoJson.ReadPage("{\"data\":[" + Row(due) + "],\"meta\":{\"hasNextPage\":false,\"itemCount\":1}}").Tasks.Single();

        private static F.DueDateChange Change() =>
            new F.DueDateChange(F.ChangeKind.Task, TaskId, new DateTime(2026, 9, 25), new DateTime(2026, 9, 27));

        [TestMethod]
        public void TheByIdsMergeKeepsTheServersSpelling()
        {
            var merged = new JArray();
            FocusBearAPIClient.AppendByIdsRows(merged, "[" + Row("2026-09-25T00:00:00.000Z") + "]");
            Assert.AreEqual("2026-09-25T00:00:00.000Z", ToDoJson.ReadTasks(merged.ToString()).Single().DueDate);
        }

        [TestMethod]
        public async Task AnUnchangedDatedTaskIsWritten()
        {
            var api = new FakeApi();
            var saved = new List<F.DueDateChange>();
            var log = new List<string>();
            var result = await ReprioritiseWrites.ApplyDueDatesAsync(api, new[] { Change() }, new ReprioritiseWrites.DueDateContext
            {
                PreviousTask = id => id == TaskId ? CatalogueRow() : null,
                TaskSaved = saved.Add,
                Log = log.Add,
            });
            Assert.AreEqual(1, result.Item1, string.Join("\n", log));
            Assert.IsTrue(result.Item2);
            Assert.AreEqual(1, api.Upserts.Count);
            var body = api.Upserts[0];
            CollectionAssert.AreEquivalent(new[] { "id", "title", "eisenhower_quadrant", "due_date" }, body.Keys.ToArray());
            Assert.AreEqual("2026-09-27", body["due_date"]);
            Assert.AreEqual(2, body["eisenhower_quadrant"]);
            Assert.AreEqual(1, saved.Count);
            Assert.IsTrue(log.Any(l => l.StartsWith("[user-action] reprioritise")));
        }

        [TestMethod]
        public async Task ATaskEditedElsewhereIsRefusedBeforeAnyWrite()
        {
            var api = new FakeApi { ServerRow = Row("2026-09-30T00:00:00.000Z") };
            var log = new List<string>();
            var result = await ReprioritiseWrites.ApplyDueDatesAsync(api, new[] { Change() }, new ReprioritiseWrites.DueDateContext
            {
                PreviousTask = id => CatalogueRow(),
                Log = log.Add,
            });
            Assert.AreEqual(0, result.Item1);
            Assert.IsFalse(result.Item2);
            Assert.AreEqual(0, api.Upserts.Count);
            Assert.IsTrue(log.Any(l => l.Contains("reason=EditedElsewhere")), string.Join("\n", log));
        }

        [TestMethod]
        public async Task AFailedWriteStopsAndSaysSo()
        {
            var api = new FakeApi { UpsertStatus = 400 };
            var result = await ReprioritiseWrites.ApplyDueDatesAsync(api, new[] { Change() }, new ReprioritiseWrites.DueDateContext { PreviousTask = id => CatalogueRow() });
            Assert.AreEqual(0, result.Item1);
            Assert.IsFalse(result.Item2);
        }

        [TestMethod]
        public async Task AnOutcomeIsWrittenBesideTheIdentityFieldsOnly()
        {
            var api = new FakeApi();
            Assert.IsTrue(await ReprioritiseWrites.WriteOutcomeAsync(api, TaskId, 8));
            CollectionAssert.AreEquivalent(new[] { "id", "title", "eisenhower_quadrant", "outcome" }, api.Upserts.Single().Keys.ToArray());
            Assert.AreEqual(8, api.Upserts.Single()["outcome"]);
            Assert.AreEqual("[TEST] UX pass", api.Upserts.Single()["title"]);
        }

        [TestMethod]
        public async Task AnOutcomeTheServerRefusedIsNotSaved()
        {
            Assert.IsFalse(await ReprioritiseWrites.WriteOutcomeAsync(new FakeApi { UpsertStatus = 400 }, TaskId, 8));
            Assert.IsFalse(await ReprioritiseWrites.WriteOutcomeAsync(new FakeApi(), "not-there", 8));
        }

        [TestMethod]
        public async Task ADurationIsSentInSeconds()
        {
            var api = new FakeApi();
            Assert.IsTrue(await ReprioritiseWrites.WriteDurationAsync(api, TaskId, 90));
            CollectionAssert.AreEquivalent(new[] { "id", "title", "eisenhower_quadrant", "duration" }, api.Upserts.Single().Keys.ToArray());
            Assert.AreEqual(5400, api.Upserts.Single()["duration"]);
        }

        [TestMethod]
        public async Task AnAccountSwitchStopsTheLoop()
        {
            var api = new FakeApi();
            var result = await ReprioritiseWrites.ApplyDueDatesAsync(api, new[] { Change() }, new ReprioritiseWrites.DueDateContext
            {
                PreviousTask = id => CatalogueRow(),
                AccountUnchanged = () => false,
            });
            Assert.AreEqual(0, api.Upserts.Count);
            Assert.IsFalse(result.Item2);
        }
    }
}
