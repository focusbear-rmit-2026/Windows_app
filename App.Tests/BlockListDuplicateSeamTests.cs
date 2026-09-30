using System.Collections.ObjectModel;
using System.Linq;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>"Duplicate" on a block list: the copy must be a list of its own.</summary>
    [TestClass]
    public class BlockListDuplicateSeamTests
    {
        private static FocusMode Original() => new FocusMode("m1", "Writing",
            new ObservableCollection<string> { "docs.google.com" }, new ObservableCollection<string> { "youtube.com" },
            new ObservableCollection<AppDetails> { new AppDetails("Word", "winword.exe") }, "#FFAA00", false, false, true, false, false, false, false, false, false,
            AlignmentScoreThreshold: 65) { serverMetadata = JObject.Parse("{\"is_ai_enabled\":true}") };

        [TestMethod]
        public void EditingTheDuplicate_LeavesTheOriginalAlone()
        {
            var original = Original();
            var copy = BlockListsView.DuplicateOf(original, "m2", "Writing copy");

            copy.arrAllowedUrls.Add("overleaf.com");
            copy.arrBlockedUrls.Clear();
            copy.arrAllowedAppsWin.Add(new AppDetails("Code", "code.exe"));
            copy.arrAllowedAppsWin[0].ExeNames.Add("extra.exe");
            copy.serverMetadata["is_ai_enabled"] = false;

            CollectionAssert.AreEqual(new[] { "docs.google.com" }, original.arrAllowedUrls.ToList());
            CollectionAssert.AreEqual(new[] { "youtube.com" }, original.arrBlockedUrls.ToList());
            Assert.AreEqual(1, original.arrAllowedAppsWin.Count);
            CollectionAssert.AreEqual(new[] { "winword.exe" }, original.arrAllowedAppsWin[0].ExeNames);
            Assert.IsTrue((bool)original.serverMetadata["is_ai_enabled"]);
        }

        [TestMethod]
        public void TheDuplicate_CarriesTheOriginalsRules()
        {
            var copy = BlockListsView.DuplicateOf(Original(), "m2", "Writing copy");

            Assert.AreEqual("m2", copy.id);
            Assert.AreEqual("Writing copy", copy.name);
            CollectionAssert.AreEqual(new[] { "docs.google.com" }, copy.arrAllowedUrls.ToList());
            Assert.AreEqual("Word", copy.arrAllowedAppsWin.Single().AppName);
            Assert.IsTrue(copy.isAllowedListApproach);
            Assert.AreEqual(65, copy.AlignmentScoreThreshold);
            Assert.IsTrue((bool)copy.serverMetadata["is_ai_enabled"], "AI blocking comes with it");
            Assert.IsFalse(copy.isOn);
            Assert.IsFalse(copy.isDefault);
        }
    }
}
