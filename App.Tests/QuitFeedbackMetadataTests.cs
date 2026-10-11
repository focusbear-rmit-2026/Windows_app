using System;
using FocusBear.Core.Analytics;
using Focus_Bear.Preferences;
using Focus_Bear.Quit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The quit and uninstall events spliced the typed feedback into their JSON fragment, so feedback with a quote,
    /// a backslash or a line break ("it kept blocking \"Slack\"") made the fragment invalid and the whole event
    /// went out without its reason and feedback - the one thing it exists to collect.
    /// </summary>
    [TestClass]
    public class QuitFeedbackMetadataTests
    {
        private static readonly DateTime Installed = new DateTime(2026, 8, 1, 9, 30, 0, DateTimeKind.Local);

        [TestMethod]
        public void QuitFeedbackWithQuotesAndLineBreaksSurvives()
        {
            string feedback = "it kept blocking \"Slack\"\nand C:\\work";
            JObject p = EventPayload.Properties(QuitWindow.QuitMetadata("Too strict", Installed, feedback));
            Assert.IsNotNull(p, "a valid fragment");
            Assert.AreEqual(feedback, (string)p["feedback"]);
            Assert.AreEqual("Too strict", (string)p["quitReason"]);
            Assert.AreEqual(Installed.ToString("o"), (string)p["usageStartDate"]);
        }

        [TestMethod]
        public void UninstallFeedbackWithQuotesSurvives()
        {
            JObject p = EventPayload.Properties(UninstallFeedback.UninstallMetadata("o\"brien@example.com", "Other", "\"why\"", Installed));
            Assert.IsNotNull(p);
            Assert.AreEqual("\"why\"", (string)p["uninstallDescription"]);
            Assert.AreEqual("o\"brien@example.com", (string)p["emailAddress"]);
            Assert.AreEqual("Other", (string)p["uninstallReason"]);
        }
    }
}
