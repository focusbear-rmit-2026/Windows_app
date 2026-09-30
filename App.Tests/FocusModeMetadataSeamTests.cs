using System.Collections.ObjectModel;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using AppDetails = Focus_Bear.Models.AppDetails;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Core's FocusModeMetadataBuilderTests prove the merge rule. These prove the app feeds it the
    /// WPF fields it owns — a field dropped from the adapter would be written back as its default and
    /// overwrite what the user set, which Core's tests cannot see.
    /// </summary>
    [TestClass]
    public class FocusModeMetadataSeamTests
    {
        private static FocusMode EditedMode(string serverMetadataJson)
        {
            return new FocusMode
            {
                id = "mode-1",
                color = "#A3FFA1",
                isAllowedListApproach = true,
                isDeepWorkFocus = true,
                lastTaskUsed = "write the report",
                AlignmentScoreThreshold = 65,
                arrBlockedUrls = new ObservableCollection<string> { "reddit.com", "*.youtube.com" },
                arrAllowedAppsWin = new ObservableCollection<AppDetails> { new AppDetails { AppName = "Code" } },
                serverMetadata = serverMetadataJson == null ? null : JObject.Parse(serverMetadataJson),
            };
        }

        [TestMethod]
        public void AWindowsSaveKeepsWhatTheMacWrote()
        {
            JObject saved = JObject.Parse(FocusModeMetadata.Serialize(EditedMode(
                "{\"is_ai_enabled\":false,\"blockAppsArray\":[\"/Applications/Slack.app\"],\"isAllowedListApproach\":false}")));

            Assert.IsFalse((bool)saved["is_ai_enabled"]);
            Assert.AreEqual("/Applications/Slack.app", (string)saved["blockAppsArray"][0]);
            Assert.IsTrue((bool)saved["isAllowedListApproach"], "a key Windows owns takes the value the user just set");
        }

        [TestMethod]
        public void EveryFieldTheUserEditsReachesTheBlob()
        {
            JObject saved = FocusModeMetadata.Build(EditedMode(null));

            Assert.AreEqual("A3FFA1", (string)saved["color"]);
            Assert.IsTrue((bool)saved["isDeepWorkFocus"]);
            Assert.AreEqual("write the report", (string)saved["lastTaskUsed"]);
            Assert.AreEqual(65, (int)saved["focusAlignmentScoreThreshold"]);
            CollectionAssert.AreEqual(new[] { "reddit.com", "*.youtube.com" }, saved["blockUrlsArray"].ToObject<string[]>());
            Assert.AreEqual("Code", (string)saved["allowed_apps_win"][0]["AppName"]);
        }

        [TestMethod]
        public void TheCachedServerCopyIsNotEditedInPlace()
        {
            FocusMode mode = EditedMode("{\"color\":\"FFFFFF\"}");

            FocusModeMetadata.Build(mode);

            Assert.AreEqual("FFFFFF", (string)mode.serverMetadata["color"],
                "the next save must still start from what the server sent");
        }
    }
}
