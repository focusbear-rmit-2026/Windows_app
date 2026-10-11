using FocusBear.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests
{
    /// <summary>
    /// The bug these cover: the API replaces `metadata` wholesale, and Windows used to rebuild the
    /// blob from a fixed list of keys, so saving a focus mode deleted whatever another client had
    /// stored. The Mac reads an absent `is_ai_enabled` as "AI on", so the reported symptom was a
    /// per-block-list AI toggle switching itself back on after a Windows edit.
    /// </summary>
    [TestClass]
    public class FocusModeMetadataBuilderTests
    {
        private static FocusModeMetadataInput Input(JObject server = null)
        {
            return new FocusModeMetadataInput
            {
                ServerMetadata = server,
                BlockedUrls = new[] { "reddit.com" },
                AllowedAppsWin = new JArray(),
                Color = "#A3FFA1",
                LastTaskUsed = "task-1",
                AlignmentScoreThreshold = 80,
                IsAllowedListApproach = true,
            };
        }

        [TestMethod]
        public void KeepsKeysThisClientDoesNotUnderstand()
        {
            JObject server = JObject.Parse(
                "{\"is_ai_enabled\":false,\"blockAppsArray\":[\"/Applications/Slack.app\"],\"color\":\"FFFFFF\"}");

            JObject result = FocusModeMetadataBuilder.Build(Input(server));

            Assert.IsFalse((bool)result["is_ai_enabled"], "the Mac's AI veto must survive a Windows save");
            Assert.AreEqual(1, ((JArray)result["blockAppsArray"]).Count);
        }

        [TestMethod]
        public void OverwritesOnlyTheKeysThisClientOwns()
        {
            JObject server = JObject.Parse("{\"is_ai_enabled\":false,\"color\":\"FFFFFF\"}");

            JObject result = FocusModeMetadataBuilder.Build(Input(server));

            Assert.AreEqual("A3FFA1", (string)result["color"], "a key we own is replaced, and loses its '#'");
            Assert.IsFalse((bool)result["is_ai_enabled"], "a key we do not own is left alone");
        }

        [TestMethod]
        public void DoesNotMutateWhatTheServerSent()
        {
            JObject server = JObject.Parse("{\"is_ai_enabled\":false,\"color\":\"FFFFFF\"}");

            FocusModeMetadataBuilder.Build(Input(server));

            Assert.AreEqual("FFFFFF", (string)server["color"], "the cached server copy must not be edited in place");
        }

        [TestMethod]
        public void AModeThisClientJustCreatedStartsEmpty()
        {
            JObject result = FocusModeMetadataBuilder.Build(Input(null));

            Assert.IsNull(result["is_ai_enabled"]);
            Assert.AreEqual("A3FFA1", (string)result["color"]);
        }

        /// The add path used to omit `lastTaskUsed` while the edit path wrote it, so creating a mode
        /// dropped a value the read path expects to find. One builder, one key set.
        [TestMethod]
        public void WritesEveryKeyTheReadPathLooksFor()
        {
            JObject result = FocusModeMetadataBuilder.Build(Input(null));

            foreach (string key in new[]
                     {
                         "blockUrlsArray", "allowed_apps_win", "color", "lastTaskUsed",
                         "focusAlignmentScoreThreshold", "isAllowedAllApps", "isAllowedListApproach",
                         "isDefault", "isMeetings", "isLockedFocus", "isRelaxFocus", "isDeepWorkFocus",
                         "isWorkFocus", "autoSuggestFocusModeOnAllowedURL",
                     })
            {
                Assert.IsNotNull(result[key], key + " is read back by AppSettings and must be written");
            }
        }

        [TestMethod]
        public void SerializesCompactly()
        {
            string json = FocusModeMetadataBuilder.Serialize(Input(null));

            Assert.IsFalse(json.Contains("\n"));
            Assert.IsTrue(json.StartsWith("{") && json.EndsWith("}"));
        }
    }
}
