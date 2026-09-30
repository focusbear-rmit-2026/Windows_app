using Focus_Bear.API;
using Focus_Bear.Focus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// More request bodies spliced around user text. A tab title with quotes (very common: "\"Song\" - YouTube")
    /// or an intention with a quote made the "report a bad AI decision" body invalid JSON; an occupation typed
    /// with a quote broke the relevant-sites request; the password-reset and confirmation-email requests
    /// spliced the email.
    /// </summary>
    [TestClass]
    public class SplicedBodiesTests
    {
        [TestMethod]
        public void TheBadAiDecisionReportKeepsQuotedTitlesAndIntentions()
        {
            string title = "\"Song\" - YouTube \\ live";
            JObject url = JObject.Parse(GentleBlock.ReportBody(false, "chrome", "https://youtube.com/watch?v=1", title, "Deep \"work\"", "Write \"chapter 3\"", "en"));
            Assert.AreEqual(title, (string)url["tab_title"]);
            Assert.AreEqual("https://youtube.com/watch?v=1", (string)url["url"]);
            Assert.AreEqual("Deep \"work\"", (string)url["focus_mode"]);
            Assert.AreEqual("Write \"chapter 3\"", (string)url["intention"]);
            Assert.AreEqual("", (string)url["meta_description"]);
            Assert.IsNull(url["appName"]);

            JObject app = JObject.Parse(GentleBlock.ReportBody(true, "C:\\Games\\game.exe", "", "", "Focus", null, "en"));
            Assert.AreEqual("C:\\Games\\game.exe", (string)app["appName"]);
            Assert.AreEqual("Focus", (string)app["focusMode"]);
            Assert.AreEqual("", (string)app["intention"]);
            Assert.IsNull(app["url"]);
        }

        [TestMethod]
        public void EmailAndOccupationBodiesAreJson()
        {
            Assert.AreEqual("o\"brien@example.com", (string)JObject.Parse(FocusBearAPIClient.EmailBody("o\"brien@example.com"))["email"]);
            Assert.AreEqual("", (string)JObject.Parse(FocusBearAPIClient.EmailBody(null))["email"]);
            Assert.AreEqual("\"Head\" chef \\ baker", (string)JObject.Parse(FocusBearAPIClient.OccupationBody("\"Head\" chef \\ baker"))["user_occupation"]);
        }
    }
}
