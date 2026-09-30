using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    [TestClass]
    public class FocusSessionTextTests
    {
        private static readonly Func<string, string> Encode = s => System.Net.WebUtility.UrlEncode(s);

        [TestMethod]
        public void AchievementsThatEncodePastTheLimit_AreCutToFit()
        {
            // 100 characters of "a, " encode to "a%2C+" - well past the server's 255.
            string typed = string.Concat(System.Linq.Enumerable.Repeat("a, ", 100));

            string sent = FocusSessionText.EncodedWithin(typed, Encode);

            Assert.IsTrue(sent.Length <= FocusSessionText.MaxLength, sent.Length.ToString());
            Assert.IsTrue(sent.Length > 240, "as much as fits is kept");
            Assert.IsFalse(sent.EndsWith("%") || sent.EndsWith("%2"), "never half an escape");
        }

        [TestMethod]
        public void AnEmoji_IsNeverSplitByTheCut()
        {
            string typed = new string('a', 250) + "😀😀";
            string sent = FocusSessionText.EncodedWithin(typed, Encode);
            Assert.AreEqual(new string('a', 250), sent, "the emoji that does not fit whole goes whole");
            Assert.AreEqual(new string('b', 254), FocusSessionText.Within(new string('b', 254) + "😀"));
        }

        [TestMethod]
        public void ShortTexts_GoAsTheyAre()
        {
            Assert.AreEqual("wrote+2+pages", FocusSessionText.EncodedWithin("wrote 2 pages", Encode));
            Assert.AreEqual("", FocusSessionText.EncodedWithin(null, Encode));
            Assert.AreEqual("phone", FocusSessionText.Within("phone"));
            Assert.AreEqual(255, FocusSessionText.Within(new string('x', 400)).Length);
        }
    }
}
