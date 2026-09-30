using System.Collections.Generic;
using FocusBear.Core.Onboarding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Onboarding
{
    [TestClass]
    public class OnboardingChoicesTests
    {
        [TestMethod]
        public void EachOccupationRadio_SendsItsOwnOccupation()
        {
            Assert.AreEqual("student", OnboardingChoices.OccupationQuery("", 1));
            Assert.AreEqual("business owner", OnboardingChoices.OccupationQuery("", 2));
            Assert.AreEqual("employee", OnboardingChoices.OccupationQuery("  ", 3));
            Assert.AreEqual("freelancer", OnboardingChoices.OccupationQuery(null, 4));
            Assert.AreEqual("other", OnboardingChoices.OccupationQuery("", 5));
            Assert.AreEqual("nurse", OnboardingChoices.OccupationQuery(" nurse ", 3), "what was typed wins");
        }

        private static KeyValuePair<string, bool> Tick(string url, bool on) => new KeyValuePair<string, bool>(url, on);

        [TestMethod]
        public void ProceedingTwice_AfterGoingBack_ListsEachSiteOnce()
        {
            var blocked = new List<string> { "reddit.com" };
            var offered = new[] { Tick("youtube.com", true), Tick("facebook.com", true), Tick("tiktok.com", false) };

            OnboardingChoices.ApplyTicks(blocked, offered);
            OnboardingChoices.ApplyTicks(blocked, offered);

            CollectionAssert.AreEqual(new[] { "reddit.com", "youtube.com", "facebook.com" }, blocked);
        }

        [TestMethod]
        public void UntickingAfterGoingBack_TakesTheSiteOut_ButNotOnesAddedElsewhere()
        {
            var blocked = new List<string> { "reddit.com" };
            OnboardingChoices.ApplyTicks(blocked, new[] { Tick("youtube.com", true) });
            OnboardingChoices.ApplyTicks(blocked, new[] { Tick("YouTube.com", false) });

            CollectionAssert.AreEqual(new[] { "reddit.com" }, blocked);
            Assert.IsTrue(OnboardingChoices.Offers(new[] { "Docs.google.com " }, "docs.google.com"));
        }
    }
}
