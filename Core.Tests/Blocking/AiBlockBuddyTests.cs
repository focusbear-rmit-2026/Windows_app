using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class AiBlockBuddyTests
    {
        [TestMethod]
        public void LongLinksCollapseToAShortReadableLabel()
        {
            Assert.AreEqual("checkout.stripe.com/c/pay/…", AiBlockBuddy.TruncatedUrl("https://checkout.stripe.com/c/pay/cs_live_a1b2c3d4e5f6g7h8i9?x=1#frag"));
            Assert.AreEqual("youtube.com/watch", AiBlockBuddy.TruncatedUrl("http://youtube.com/watch?v=abc"));
            Assert.AreEqual("news.ycombinator.com", AiBlockBuddy.TruncatedUrl("https://news.ycombinator.com/"));
            Assert.AreEqual(new string('a', 40) + "…", AiBlockBuddy.TruncatedUrl(new string('a', 60)), "no slash to cut at");
        }

        [TestMethod]
        public void PagesShowTheirTitle_AppsTheirName()
        {
            Assert.AreEqual("Funny cats (youtube.com/watch)", AiBlockBuddy.DisplayLabel(false, "", "https://youtube.com/watch?v=1", " Funny cats "));
            Assert.AreEqual("youtube.com/watch", AiBlockBuddy.DisplayLabel(false, "", "https://youtube.com/watch?v=1", ""));
            Assert.AreEqual("Steam", AiBlockBuddy.DisplayLabel(true, " Steam ", "", "ignored"));
        }

        [TestMethod]
        public void ArguedTimeAndRelevance()
        {
            Assert.AreEqual(1, AiBlockBuddy.ClampAllowMinutes(0));
            Assert.AreEqual(120, AiBlockBuddy.ClampAllowMinutes(500));
            Assert.IsTrue(AiBlockBuddy.IsRelevant(0.5));
            Assert.IsFalse(AiBlockBuddy.IsRelevant(0.49));
            Assert.IsFalse(AiBlockBuddy.IsRelevant(null));
            Assert.AreEqual("82%", AiBlockBuddy.ScoreLabel(0.82));
            Assert.AreEqual("", AiBlockBuddy.ScoreLabel(-1));
        }
    }
}
