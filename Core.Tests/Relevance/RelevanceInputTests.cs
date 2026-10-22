using System;
using FocusBear.Core.Relevance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Relevance
{
    [TestClass]
    public class RelevanceInputTests
    {
        [DataTestMethod]
        [DataRow("Python Tutorial - Google Chrome", "Python Tutorial")]
        [DataRow("Python Tutorial — Mozilla Firefox", "Python Tutorial")]
        [DataRow("Python Tutorial — Mozilla Firefox Private Browsing", "Python Tutorial")]
        [DataRow("Python Tutorial - Personal - Microsoft​ Edge", "Python Tutorial")]
        [DataRow("Python Tutorial - Work - Microsoft Edge", "Python Tutorial")]
        [DataRow("Python Tutorial - Brave", "Python Tutorial")]
        [DataRow("Python Tutorial - Google Chrome (Incognito)", "Python Tutorial")]
        [DataRow("Python Tutorial", "Python Tutorial")]
        public void TheBrowserNameIsRemovedFromEveryTitle(string windowTitle, string expected)
        {
            Assert.AreEqual(expected, BrowserTitle.StripBrowserName(windowTitle));
        }

        [TestMethod]
        public void OnlyTheBrowserSegmentGoes_NotTheSiteName()
        {
            // An unknown middle segment is part of the page title (Gmail's account and product name).
            Assert.AreEqual("Inbox (12) - support@company.com - Gmail",
                BrowserTitle.StripBrowserName("Inbox (12) - support@company.com - Gmail - Google Chrome"));
        }

        [TestMethod]
        public void ABrowserTabInputHasTheBareTitleAndThePathWords()
        {
            RelevanceInput input = RelevanceInput.ForBrowserTab("Learn Python", "chapter 3",
                "https://docs.python.org/3/tutorial/index.html", "The Python Tutorial - Google Chrome");
            Assert.AreEqual("Learn Python chapter 3", input.Goal);
            Assert.AreEqual("The Python Tutorial", input.Title);
            Assert.AreEqual("The Python Tutorial tutorial index.html", input.Context);
        }

        [TestMethod]
        public void AYouTubeVideoUsesItsCleanTitleAsTheWholeContext()
        {
            RelevanceInput input = RelevanceInput.ForBrowserTab("Learn Python", "",
                "https://www.youtube.com/watch?v=abc", "(3) Python for Beginners - YouTube - Google Chrome");
            Assert.AreEqual("Python for Beginners", input.Context);
            Assert.AreEqual("Python for Beginners", input.Title);
        }

        [DataTestMethod]
        [DataRow("https://example.com/a", "")]
        [DataRow("https://example.com/a", "New Tab - Google Chrome")]
        [DataRow("https://example.com/a", "Untitled - Google Chrome")]
        [DataRow("https://example.com/a", "example.com/a - Google Chrome")]
        [DataRow("https://www.youtube.com/watch?v=abc", "YouTube - Google Chrome")]
        [DataRow("chrome://settings", "Settings - Google Chrome")]
        public void NoInputUntilThePageHasARealTitle(string url, string windowTitle)
        {
            Assert.IsNull(RelevanceInput.ForBrowserTab("Learn Python", "", url, windowTitle));
        }
    }
}
