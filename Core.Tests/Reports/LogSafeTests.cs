using FocusBear.Core.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Reports
{
    /// <summary>
    /// The log goes out with every problem report, and it held the whole settings document, the buddy list
    /// (emails), the onboarding webview's access token and a password-reset email. LogSafe says what a body
    /// was - its size, a short hash, its keys, what changed - without its contents.
    /// </summary>
    [TestClass]
    public class LogSafeTests
    {
        [TestMethod]
        public void ADescriptionHasTheSizeAndAHashButNotTheText()
        {
            string body = "{\"email\":\"someone@example.com\"}";
            string d = LogSafe.Describe(body);
            StringAssert.Contains(d, body.Length + " chars");
            Assert.IsFalse(d.Contains("someone"), d);
            Assert.AreEqual(d, LogSafe.Describe(body), "the same body describes the same way");
            Assert.AreNotEqual(d, LogSafe.Describe(body + " "));
            Assert.AreEqual("empty", LogSafe.Describe(null));
            Assert.AreEqual("empty", LogSafe.Describe(""));
        }

        [TestMethod]
        public void ChangedKeysNamesWhatMovedAndNothingElse()
        {
            string before = "{\"a\":1,\"settings\":{\"theme\":\"dark\",\"font\":\"Poppins\"},\"gone\":true}";
            string after = "{\"a\":1,\"settings\":{\"theme\":\"light\",\"font\":\"Poppins\"},\"new\":\"secret text\"}";
            string changed = LogSafe.ChangedKeys(before, after);
            Assert.AreEqual("gone, new, settings.theme", changed);
            Assert.IsFalse(changed.Contains("secret"), changed);
        }

        [TestMethod]
        public void ChangedKeysForTheFirstFetchOrAnUnchangedDocument()
        {
            Assert.AreEqual("none", LogSafe.ChangedKeys("{\"a\":1}", "{\"a\":1}"));
            Assert.AreEqual("first load", LogSafe.ChangedKeys(null, "{\"a\":1}"));
            Assert.AreEqual("unreadable", LogSafe.ChangedKeys("{\"a\":1}", "not json"));
        }

        [TestMethod]
        public void ChangedKeysIsCapped()
        {
            var before = new System.Text.StringBuilder("{");
            var after = new System.Text.StringBuilder("{");
            for (int i = 0; i < 40; i++)
            {
                before.Append(i == 0 ? "" : ",").Append("\"k").Append(i.ToString("D2")).Append("\":0");
                after.Append(i == 0 ? "" : ",").Append("\"k").Append(i.ToString("D2")).Append("\":1");
            }
            string changed = LogSafe.ChangedKeys(before.Append('}').ToString(), after.Append('}').ToString());
            StringAssert.EndsWith(changed, "(+20 more)");
        }

        [TestMethod]
        public void KeysListsTheTopLevelShape()
        {
            Assert.AreEqual("object: access_token, lang, theme", LogSafe.Keys("{\"access_token\":\"eyJ.x.y\",\"lang\":\"en\",\"theme\":\"DARK\"}"));
            Assert.AreEqual("array of 2", LogSafe.Keys("[{\"email\":\"a@b.c\"},{}]"));
            Assert.AreEqual("unreadable, 8 chars", LogSafe.Keys("not json"));
        }
    }
}
