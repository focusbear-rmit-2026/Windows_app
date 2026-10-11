using System;
using System.Collections.Generic;
using Focus_Bear.API;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// PostActivityCompleted spliced its body together. A break activity always carries a question list, empty
    /// when it has no questions, and an empty list came out as "log_quantity_answers":] - invalid JSON, so the
    /// server refused the completion (a 400, not a 500, so it was not even kept for the offline sync). The same
    /// empty list saved offline as "]" and failed every later offline batch it was in.
    /// </summary>
    [TestClass]
    public class ActivityCompletedBodyTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);

        private static JObject Body(List<QuantityQuestion> questions, bool skipped = false, string choice = "") =>
            JObject.Parse(ActivityCompletedBody.Build("act-1", 300, "dev-1", "seq-1", Start, Start.AddSeconds(300), skipped, choice, questions));

        [TestMethod]
        public void NoQuestions_NoAnswersKeyAndValidJson()
        {
            JObject body = Body(new List<QuantityQuestion>());
            Assert.IsNull(body["log_quantity_answers"]);
            Assert.AreEqual("act-1", (string)body["activity_id"]);
            Assert.AreEqual(300, (int)body["duration_logged"]);
            Assert.AreEqual(0, ((JObject)body["metadata"]).Count);
            Assert.IsNull(Body(null)["log_quantity_answers"]);
        }

        [TestMethod]
        public void AnswersAreNumbersPerQuestion()
        {
            var q = new QuantityQuestion { id = "q-1", quantity = 6.85 };
            JObject body = Body(new List<QuantityQuestion> { q }, choice: "choice-9");
            Assert.AreEqual("q-1", (string)body["log_quantity_answers"][0]["question_id"]);
            Assert.AreEqual(6.85, (double)body["log_quantity_answers"][0]["logged_value"]);
            Assert.AreEqual("choice-9", (string)body["choice_id"]);
        }

        [TestMethod]
        public void ASkipIsMarkedInTheMetadata()
        {
            Assert.IsTrue((bool)Body(null, skipped: true)["metadata"]["skipped_did_complete"]);
            Assert.IsNull(Body(null)["choice_id"], "no choice, no key");
        }

        [TestMethod]
        public void AnOfflineEntryKeepsItsStoredFinishTime()
        {
            // The sync added the duration to a finish time that already included it, doubling every offline
            // activity's length.
            var e = new OfflineEntry("act-1", "300", "seq-1", "2026-09-27T08:00:00.0000000Z", "2026-09-27T08:05:00.0000000Z", "", "", "", "", "");
            JObject body = ActivityCompletedBody.FromOffline(e, "dev-1");
            Assert.AreEqual("2026-09-27T08:05:00.0000000Z", (string)body["finish_time"]);
            Assert.AreEqual("2026-09-27T08:00:00.0000000Z", (string)body["start_time"]);
            Assert.AreEqual(300, (int)body["duration_logged"]);
            Assert.IsNull(body["log_quantity_answers"]);
            Assert.IsNull(body["choice_id"]);
        }

        [TestMethod]
        public void AnOfflineSkipWithNoDurationSyncs()
        {
            // The skip fallback stores no duration; int.Parse("") threw, and the sync's catch deleted the whole
            // offline queue.
            var e = new OfflineEntry("act-1", "", "seq-1", "2026-09-27T08:00:00Z", "2026-09-27T08:00:01Z", "true", "", "", "", "");
            JObject body = ActivityCompletedBody.FromOffline(e, "dev-1");
            Assert.AreEqual(0, (int)body["duration_logged"]);
            Assert.IsTrue((bool)body["metadata"]["skipped_did_complete"]);
        }

        [TestMethod]
        public void AnOfflineEntrySavedWithTheOldEmptyAnswersStillSyncs()
        {
            var e = new OfflineEntry("act-1", "60", "seq-1", "s", "f", "", "choice-2", "]", "", "");
            JObject body = ActivityCompletedBody.FromOffline(e, "dev-1");
            Assert.IsNull(body["log_quantity_answers"]);
            Assert.AreEqual("choice-2", (string)body["choice_id"]);
        }

        [TestMethod]
        public void OfflineAnswers_EmptyListStoresNothing_AndAStoredBrokenListSyncsAsEmpty()
        {
            Assert.AreEqual("", ActivityCompletedBody.OfflineAnswers(new List<QuantityQuestion>()));
            Assert.AreEqual("", ActivityCompletedBody.OfflineAnswers(null));

            string stored = ActivityCompletedBody.OfflineAnswers(new List<QuantityQuestion> { new QuantityQuestion { id = "q-1", quantity = 2.5 } });
            Assert.IsFalse(stored.Contains(","), "the offline store is a CSV line");
            JArray back = ActivityCompletedBody.OfflineAnswersToJson(stored);
            Assert.AreEqual(2.5, (double)back[0]["logged_value"]);

            Assert.AreEqual(0, ActivityCompletedBody.OfflineAnswersToJson("]").Count, "an entry saved by the old code");
            Assert.AreEqual(0, ActivityCompletedBody.OfflineAnswersToJson("").Count);
            Assert.AreEqual(0, ActivityCompletedBody.OfflineAnswersToJson(null).Count);
        }
    }
}
