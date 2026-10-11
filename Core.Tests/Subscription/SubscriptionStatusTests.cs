using FocusBear.Core.Subscription;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Subscription
{
    /// <summary>
    /// GET subscription status. The app read only activeEntitlements[0] and a paid list without team_admin (or
    /// the 10/15 team sizes), so a paying user whose first entitlement was "trial" or "team_admin" was treated as
    /// on trial and nagged to upgrade; an empty list or a missing expiry threw half-way and left the previous
    /// user's state. The Mac takes the first paying entitlement among all of them.
    /// </summary>
    [TestClass]
    public class SubscriptionStatusTests
    {
        [TestMethod]
        public void APayingEntitlementAnywhereInTheListCounts()
        {
            var s = SubscriptionStatus.Parse(
                "{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"trial\",\"personal\"],\"expirations\":{\"trial\":{\"days_left\":3},\"personal\":{\"days_left\":200}}}");
            Assert.IsTrue(s.HasActiveSubscription);
            Assert.IsTrue(s.IsPurchased);
            Assert.IsNull(s.TrialDaysLeft, "a paying user is not on a trial");
        }

        [TestMethod]
        public void TeamAdminAndLargerTeamsArePaid()
        {
            foreach (string e in new[] { "team_admin", "team_member", "team_owner", "team_size_5", "team_size_10", "team_size_15", "personal" })
                Assert.IsTrue(SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"" + e + "\"]}").IsPurchased, e);
        }

        [TestMethod]
        public void ATrialHasItsDaysLeft()
        {
            var s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"trial\"],\"expirations\":{\"trial\":{\"days_left\":5}}}");
            Assert.IsTrue(s.HasActiveSubscription);
            Assert.IsFalse(s.IsPurchased);
            Assert.AreEqual(5, s.TrialDaysLeft);
        }

        [TestMethod]
        public void MissingOrOddExpiriesDontThrow()
        {
            var s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"trial\"]}");
            Assert.IsNotNull(s);
            Assert.IsNull(s.TrialDaysLeft);
            s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"trial\"],\"expirations\":{\"trial\":{\"days_left\":null}}}");
            Assert.IsNull(s.TrialDaysLeft);
            s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[\"trial\"],\"expirations\":{\"trial\":{\"days_left\":\"4\"}}}");
            Assert.AreEqual(4, s.TrialDaysLeft);
        }

        [TestMethod]
        public void NoActiveEntitlementsIsExpired()
        {
            var s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":true,\"activeEntitlements\":[]}");
            Assert.IsFalse(s.HasActiveSubscription, "the backend derives hasActiveSubscription from the list");
            s = SubscriptionStatus.Parse("{\"hasActiveSubscription\":false,\"activeEntitlements\":[]}");
            Assert.IsFalse(s.HasActiveSubscription);
            Assert.IsFalse(s.IsPurchased);
        }

        [TestMethod]
        public void UnreadableIsNull_SoTheCallerKeepsWhatItKnew()
        {
            Assert.IsNull(SubscriptionStatus.Parse("not json"));
            Assert.IsNull(SubscriptionStatus.Parse(""));
            Assert.IsNull(SubscriptionStatus.Parse("[1]"));
        }
    }
}
