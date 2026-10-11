using System.Collections.ObjectModel;
using Focus_Bear.Utils;
using FocusBear.Core.Postpone;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using AppDetails = Focus_Bear.Models.AppDetails;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The bulk PUT /focus-mode body. The accountability buddy's temporary mode lives only on this computer
    /// (its id is not a UUID), so sending it made the server reject the whole PUT and every block-list edit
    /// after a buddy-approved postpone failed to sync until the next buddy session replaced it.
    /// </summary>
    [TestClass]
    public class FocusModesBulkUpdateSeamTests
    {
        private static FocusMode Mode(string id, string name) => new FocusMode
        {
            id = id,
            name = name,
            arrAllowedUrls = new ObservableCollection<string> { "docs.google.com" },
            arrBlockedUrls = new ObservableCollection<string>(),
            arrAllowedAppsWin = new ObservableCollection<AppDetails>(),
        };

        [TestMethod]
        public void TheBuddysTemporaryModeIsNotSentToTheServer()
        {
            var body = JArray.Parse(FocusModeMetadata.BulkUpdateBody(new[]
            {
                Mode("11111111-1111-1111-1111-111111111111", "Work"),
                Mode(PostponeFocusSession.ModeIdPrefix + "abc", "Accountability buddy"),
            }));

            Assert.AreEqual(1, body.Count);
            Assert.AreEqual("Work", (string)body[0]["name"]);
        }

        [TestMethod]
        public void AQuoteInAModeNameStillMakesValidJson()
        {
            var body = JArray.Parse(FocusModeMetadata.BulkUpdateBody(new[] { Mode("m1", "Jeremy's \"deep\" work\\") }));

            Assert.AreEqual("Jeremy's \"deep\" work\\", (string)body[0]["name"]);
            Assert.AreEqual("docs.google.com", (string)body[0]["allowed_urls"][0]);
            Assert.IsInstanceOfType(body[0]["metadata"], typeof(JObject));
        }

        [TestMethod]
        public void NoModesToSyncGivesNoBody()
        {
            Assert.IsNull(FocusModeMetadata.BulkUpdateBody(new[] { Mode(PostponeFocusSession.ModeIdPrefix + "x", "b") }));
        }
    }
}
