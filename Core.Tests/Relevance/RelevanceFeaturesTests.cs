using System;
using System.Linq;
using FocusBear.Core.Relevance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Relevance
{
    [TestClass]
    public class RelevanceFeaturesTests
    {
        private const string Gmail = "https://mail.google.com/mail/u/0/#inbox";
        private const string GmailTitle = "Inbox (12) - support@company.com - Gmail";

        [DataTestMethod]
        [DataRow("Reply to customer support emails")]
        [DataRow("Clear my inbox")]
        [DataRow("Answer support tickets")]
        [DataRow("Process e-mails")]
        public void AnInboxIsNotAFeedWhenTheGoalIsAboutEmail(string goal)
        {
            Assert.IsFalse(RelevanceFeatures.IsFeedOrMessaging(GmailTitle, Gmail, goal));
            Assert.IsTrue(EmailGoal.AllowsMailPage(goal, Gmail, GmailTitle));
        }

        [TestMethod]
        public void AnInboxIsStillAFeedForAnUnrelatedGoal()
        {
            const string goal = "Study for biology exam on cell respiration";
            Assert.IsTrue(RelevanceFeatures.IsFeedOrMessaging("Inbox (3) - Gmail", Gmail, goal));
            Assert.IsFalse(EmailGoal.AllowsMailPage(goal, Gmail, "Inbox (3) - Gmail"));
        }

        [TestMethod]
        public void OutlookIsAMailPageByItsHostEvenWithoutInboxInTheTitle()
        {
            Assert.IsTrue(EmailGoal.AllowsMailPage("Reply to emails", "https://outlook.office.com/mail/", "Mail - Jeremy - Outlook"));
            Assert.IsTrue(EmailGoal.AllowsMailPage("Reply to emails", "https://outlook.live.com/mail/0/", "Mail"));
        }

        [TestMethod]
        public void AnEmailGoalDoesNotAllowSocialFeeds()
        {
            Assert.IsFalse(EmailGoal.AllowsMailPage("Reply to customer support emails", "https://twitter.com/home", "Home / X"));
            Assert.IsTrue(RelevanceFeatures.IsFeedOrMessaging("Home / X", "https://twitter.com/home", "Reply to customer support emails"));
        }

        [DataTestMethod]
        [DataRow("Home page | NSW health", false)]
        [DataRow("fivtran homepage", false)]
        [DataRow("Home / X", true)]
        [DataRow("Homepage feed", true)]
        [DataRow("Notifications | LinkedIn", true)]
        [DataRow("The Python Tutorial", false)]
        public void HomePageIsNotTheHomeFeedKeyword(string title, bool expected)
        {
            Assert.AreEqual(expected, RelevanceFeatures.IsFeedOrMessaging(title, "https://example.com", "Learn Python"));
        }

        [TestMethod]
        public void ScalarsMatchTheResearchPredictor()
        {
            // Values from Deployment/predictor.py's _build_features for the same row.
            float[] scalars = RelevanceFeatures.Scalars("learn python programming", "Python tutorial docs learn", 0.5f,
                "docs.python.org/3/tutorial/index.html?x=1", "Python tutorial");
            CollectionAssert.AreEqual(new[]
            {
                0.5f, 3f, 49f, 1f, 1f, 0f, 0f, 2f / 3f, 3f, 4f, 0.5f * (2f / 3f), 0f,
            }, scalars);
        }

        [TestMethod]
        public void NoUrlGivesZeroUrlFeatures()
        {
            CollectionAssert.AreEqual(new float[4], RelevanceFeatures.UrlMetadata(""));
            CollectionAssert.AreEqual(new float[4], RelevanceFeatures.UrlMetadata(null));
        }

        [TestMethod]
        public void TheVectorIsLaidOutInTrainingOrder()
        {
            float[] features = RelevanceFeatures.Assemble(Filled(533, 1), Filled(5000, 2), Filled(12, 3), Filled(384, 4), Filled(384, 5));
            Assert.AreEqual(6313, features.Length);
            Assert.AreEqual(1f, features[532]);
            Assert.AreEqual(2f, features[533]);
            Assert.AreEqual(3f, features[RelevanceFeatures.CosineIndex]);
            Assert.AreEqual(4f, features[5545]);
            Assert.AreEqual(5f, features[5929]);
            Assert.AreEqual(5f, features[6312]);
        }

        [TestMethod]
        public void AWrongSizedPartIsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                RelevanceFeatures.Assemble(Filled(533, 1), Filled(5000, 2), Filled(12, 3), Filled(383, 4), Filled(384, 5)));
        }

        [TestMethod]
        public void TheVoteWeightsTheTreesTwice()
        {
            float[] vote = RelevanceEnsemble.Vote(new[] { 1f, 0f, 0f }, new[] { 0f, 0f, 1f }, new[] { 0f, 1f, 0f });
            CollectionAssert.AreEqual(new[] { 0.2f, 0.4f, 0.4f }, vote);
            Assert.AreEqual(RelevanceTier.Caution, RelevanceEnsemble.MostLikely(vote), "ties go to the lower tier, like argmax");
        }

        [TestMethod]
        public void TreesSeeZerosAsMissing()
        {
            float[] trees = RelevanceEnsemble.ZerosAsMissing(new[] { 0f, 0.5f });
            Assert.IsTrue(float.IsNaN(trees[0]));
            Assert.AreEqual(0.5f, trees[1]);
        }

        private static float[] Filled(int count, float value) => Enumerable.Repeat(value, count).ToArray();
    }
}
