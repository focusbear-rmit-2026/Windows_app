using System.Collections.ObjectModel;
using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Bodies and script arguments that used to be built by pasting user text between quotes. A name with a
    /// quote or backslash made them unreadable (a block list or schedule that silently never saved; every block
    /// list's sync failing at once) or, in a web view, ran as script.
    /// </summary>
    [TestClass]
    public class SplicedJsonSeamTests
    {
        private const string Awkward = "Mum's \"work\" \\ stuff";

        private static FocusMode Mode(string id, string name) => new FocusMode(id, name,
            new ObservableCollection<string> { "a.com" }, new ObservableCollection<string>(), new ObservableCollection<AppDetails>(),
            "#FFAA00", false, true, false, false, false, false, false, false, false);

        [TestMethod]
        public void ABlockListNamedWithQuotes_StillMakesAReadableBody()
        {
            JObject body = JObject.Parse(FocusModeMetadata.RequestBody(Mode("m1", Awkward)).ToString());

            Assert.AreEqual(Awkward, (string)body["name"]);
            Assert.AreEqual("a.com", (string)body["allowed_urls"][0]);
            Assert.IsNotNull(body["metadata"] as JObject);
        }

        [TestMethod]
        public void TheSyncOfEveryBlockList_IsAValidArray_EvenWhenEmpty()
        {
            JArray all = JArray.Parse(FocusModeMetadata.RequestBody(new[] { Mode("m1", Awkward), Mode("m2", "Plain") }).ToString());
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual("[]", FocusModeMetadata.RequestBody(new FocusMode[0]).ToString(Newtonsoft.Json.Formatting.None));
        }

        [TestMethod]
        public void ABlockListName_CannotBreakOutOfTheWebViewCall()
        {
            string argument = FocusModeMetadata.FocusNamesScriptArgument(new[] { Mode("m1", "\"}]});alert(1);//") });

            var parsed = JObject.Parse(argument);   // the whole argument is one JSON value…
            Assert.AreEqual("\"}]});alert(1);//", (string)parsed["focusNames"][0]["name"], "…and the name is data inside it");
        }

        [TestMethod]
        public void ABlockingScheduleNamedWithQuotes_StillMakesAReadableBody()
        {
            var schedule = new CustomBlockingSchedule { id = "s1", name = Awkward, StartTimeValue = "09:00", EndTimeValue = "17:00", SelectedFocusModeId = "m1" };

            JObject body = JObject.Parse(BlockingScheduleManager.RequestBody(schedule).ToString());

            Assert.AreEqual(Awkward, (string)body["name"]);
            Assert.AreEqual("09:00", (string)body["start_time"]);
            Assert.AreEqual("m1", (string)body["focus_mode_id"]);
            Assert.IsNotNull(body["metadata"]["is_micro_breaks_enabled"]);
            Assert.AreEqual(JTokenType.Array, body["days_of_week"].Type);
        }

        [TestMethod]
        public void TheUrlSafetyCheck_SurvivesATabTitleWithQuotesAndAJustificationWithNewLines()
        {
            JObject body = JObject.Parse(Focus_Bear.API.FocusBearAPIClient.UrlSafetyRequest(
                "https://x.com/a?b=\"c\"", "He said \"hi\" \\ bye", "", "Deep work", "Write the \"intro\"",
                new JArray(), null, "It's for work\nreally", new[] { "a\"b" }, "english", true).ToString());

            Assert.AreEqual("He said \"hi\" \\ bye", (string)body["tab_title"]);
            Assert.AreEqual("It's for work\nreally", (string)body["extraJustificationForThisSite"]);
            Assert.AreEqual("a\"b", (string)body["lastFiveJustificationsInThisFocusSession"][0]);
            Assert.AreEqual(true, (bool)body["task_must_align_to_focus_intention"]);
            Assert.AreEqual("", (string)body["currentTaskInToDoPlayer"]);
            Assert.IsNull(Focus_Bear.API.FocusBearAPIClient.UrlSafetyRequest("u", "t", "", "", "", null, null, null, null, "english", false)["task_must_align_to_focus_intention"]);
        }

        [TestMethod]
        public void FinishingASession_WithTypedDistractions_StillMakesAReadableBody()
        {
            JObject body = JObject.Parse(Focus_Bear.API.FocusBearAPIClient.FinishFocusModeBody(
                "wrote 2 pages", "Slack \"pings\"\nand my phone", new System.DateTime(2026, 9, 27, 1, 2, 3, System.DateTimeKind.Utc),
                "{\"focus_alignment_score\":85.5}", "[{\"id\":\"t1\"}]").ToString());

            Assert.AreEqual("Slack \"pings\"\nand my phone", (string)body["distractions"]);
            Assert.AreEqual("wrote+2+pages", (string)body["achievements"], "achievements go URL-encoded, as before");
            Assert.AreEqual(85.5, (double)body["metadata"]["focus_alignment_score"]);
            Assert.AreEqual("t1", (string)body["to_dos"][0]["id"]);
            StringAssert.Contains(Focus_Bear.API.FocusBearAPIClient.FinishFocusModeBody("", "", new System.DateTime(2026, 9, 27, 1, 2, 3, System.DateTimeKind.Utc), null, null)
                                      .ToString(Newtonsoft.Json.Formatting.None), "\"finish_time\":\"2026-09-27T01:02:03");
        }

        [TestMethod]
        public void FinishingASession_KeepsBothTextsWithinTheServersLimit()
        {
            JObject body = Focus_Bear.API.FocusBearAPIClient.FinishFocusModeBody(
                string.Concat(System.Linq.Enumerable.Repeat("done, ", 100)), new string('d', 400), System.DateTime.UtcNow, null, null);

            Assert.IsTrue(((string)body["achievements"]).Length <= 255, "FinishFocusModeDto.achievements is @MaxLength(255)");
            Assert.AreEqual(255, ((string)body["distractions"]).Length);
        }

        [TestMethod]
        public void AnUnlockRequest_WithAMultiLineReason_StillMakesAReadableBody()
        {
            JObject body = JObject.Parse(Focus_Bear.API.FocusBearAPIClient.UnlockRequestBody("b1", "Need \"Docs\"\nfor the exam", 15).ToString());
            Assert.AreEqual("Need \"Docs\"\nfor the exam", (string)body["reason"]);
            Assert.AreEqual(15, (int)body["unlock_duration_minutes"]);
        }
    }
}
