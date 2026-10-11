using FocusBear.Core.Ui;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Ui
{
    /// <summary>
    /// The webview hand-offs (onboarding routine suggestions, tasks, settings, help, survey, subscription) were
    /// scripts spliced with string.Format. A custom goal with a quote or a newline broke the onboarding call, and
    /// the requestId echoed back from the page was spliced in unescaped.
    /// </summary>
    [TestClass]
    public class WebviewPayloadTests
    {
        private static JObject ArgumentOf(string script, string function)
        {
            string call = function + "(";
            int start = script.IndexOf(call) + call.Length;
            int end = script.IndexOf("); clearInterval", start);
            Assert.IsTrue(start >= call.Length && end > start, script);
            return JObject.Parse(script.Substring(start, end - start));
        }

        [TestMethod]
        public void ACustomGoalWithAQuoteAndANewlineStaysOneGoal()
        {
            string goal = "Finish my \"thesis\"\nand rest";
            string script = new WebviewPayload()
                .Add("access_token", "eyJ.a.b")
                .Add("lang", "en")
                .Add("long_term_goals", new JArray(goal))
                .WithRequestId(null)
                .WaitAndCall("loadMetaDataForRoutineSuggestion");

            JObject arg = ArgumentOf(script, "loadMetaDataForRoutineSuggestion");
            Assert.AreEqual(goal, (string)arg["long_term_goals"][0]);
            Assert.AreEqual("en", (string)arg["lang"]);
            Assert.IsNull(arg["requestId"], "no requestId on the initial load");
            StringAssert.StartsWith(script, "const waitForAppLoaded = setInterval(() => { if (window.loadMetaDataForRoutineSuggestion) {");
            Assert.IsFalse(script.Contains("\n"), "a newline in the goal is escaped, not raw in the script");
        }

        [TestMethod]
        public void TheEchoedRequestIdIsAString()
        {
            string id = "abc\"); alert(1); (\"";
            JObject arg = ArgumentOf(new WebviewPayload().Add("access_token", "t").WithRequestId(id).WaitAndCall("loadSettingsData"), "loadSettingsData");
            Assert.AreEqual(id, (string)arg["requestId"]);
        }

        [TestMethod]
        public void RawJsonGoesInAsJson_OrAsAnEmptyArrayWhenUnreadable()
        {
            JObject arg = ArgumentOf(new WebviewPayload().AddJson("tasks", "[{\"id\":\"1\"}]").Add("total_duration", 12.5).WaitAndCall("loadTasks"), "loadTasks");
            Assert.AreEqual("1", (string)arg["tasks"][0]["id"]);
            Assert.AreEqual(12.5, (double)arg["total_duration"]);

            arg = ArgumentOf(new WebviewPayload().AddJson("tasks", "not json").AddJson("more", null).WaitAndCall("loadTasks"), "loadTasks");
            Assert.AreEqual(0, ((JArray)arg["tasks"]).Count);
            Assert.AreEqual(0, ((JArray)arg["more"]).Count);
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentException))]
        public void TheFunctionNameMustBeAnIdentifier()
        {
            new WebviewPayload().WaitAndCall("x); alert(1");
        }
    }
}
