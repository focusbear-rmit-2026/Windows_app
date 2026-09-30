using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Step = FocusBear.Core.Blocking.SubscriptionState.Step;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class FocusEventSubscriptionTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void FocusEventsAreWantedOnlyWhileSomethingCanBeBlocked()
        {
            Assert.IsFalse(FocusEventSubscription.IsNeeded(false, false, false, false, false, false), "idle: nothing to block");
            Assert.IsTrue(FocusEventSubscription.IsNeeded(false, false, false, true, false, false), "a focus session");
            Assert.IsFalse(FocusEventSubscription.IsNeeded(false, false, false, true, true, false), "a paused one blocks nothing (#1274)");
            Assert.IsTrue(FocusEventSubscription.IsNeeded(false, false, false, false, false, true), "a blocking schedule");
            Assert.IsTrue(FocusEventSubscription.IsNeeded(true, false, false, false, false, false), "shut-off time");
            Assert.IsTrue(FocusEventSubscription.IsNeeded(false, true, false, false, false, false), "a habit with allowed apps");
            Assert.IsTrue(FocusEventSubscription.IsNeeded(false, true, true, false, false, false), "a habit on a focus mode");
        }

        [TestMethod]
        public void OnlyAChangeMakesACallAndOneRunsAtATime()
        {
            var s = new SubscriptionState();
            Assert.AreEqual(Step.None, s.Want(false, T0), "starts unregistered");
            Assert.AreEqual(Step.Add, s.Want(true, T0));
            Assert.AreEqual(Step.None, s.Want(true, T0), "already adding");
            Assert.AreEqual(Step.None, s.Want(false, T0), "a remove waits for the add");
            Assert.AreEqual(Step.Remove, s.Completed(Step.Add, true, T0), "then runs");
            Assert.AreEqual(Step.None, s.Completed(Step.Remove, true, T0));
            Assert.IsFalse(s.IsRegistered);
            Assert.AreEqual(Step.None, s.Want(false, T0), "every idle tick after that is free");
        }

        [TestMethod]
        public void AFailedRemoveIsRetriedOnlyAfterAWhile()
        {
            var s = new SubscriptionState();
            s.Want(true, T0);
            s.Completed(Step.Add, true, T0);
            Assert.AreEqual(Step.Remove, s.Want(false, T0));
            Assert.AreEqual(Step.None, s.Completed(Step.Remove, false, T0), "a failed remove is not retried at once");
            Assert.AreEqual(Step.None, s.Want(false, T0.AddSeconds(5)), "nor on the next tick");
            Assert.AreEqual(Step.Remove, s.Want(false, T0 + SubscriptionState.RetryAfter), "but after a while");
            Assert.IsTrue(s.IsRegistered, "still registered as far as we know");
        }

        [TestMethod]
        public void AnAddThatTimesOutButFinishesLateNeverLeavesTwoHandlers()
        {
            var s = new SubscriptionState();
            Assert.AreEqual(Step.Add, s.Want(true, T0));
            s.TimedOut(Step.Add);
            Assert.IsTrue(s.IsInDoubt);
            // While the abandoned add may still land, no second add (a double registration: every focus change
            // processed twice, two block popups) and no remove racing it on the same handler.
            Assert.AreEqual(Step.None, s.Want(true, T0 + SubscriptionState.RetryAfter + TimeSpan.FromMinutes(5)));
            Assert.AreEqual(Step.None, s.Want(false, T0 + SubscriptionState.RetryAfter + TimeSpan.FromMinutes(6)));

            // It lands after all: registered once, and the remove wanted meanwhile follows.
            Assert.AreEqual(Step.Remove, s.Completed(Step.Add, true, T0.AddMinutes(7)));
            Assert.IsTrue(s.IsRegistered);
            Assert.IsFalse(s.IsInDoubt);
            Assert.AreEqual(Step.None, s.Completed(Step.Remove, true, T0.AddMinutes(7)));
            Assert.IsFalse(s.IsRegistered);
        }

        [TestMethod]
        public void ALateAddStillWantedIsNotAddedAgain()
        {
            var s = new SubscriptionState();
            s.Want(true, T0);
            s.TimedOut(Step.Add);
            Assert.AreEqual(Step.None, s.Completed(Step.Add, true, T0.AddMinutes(3)), "wanted and now registered: nothing to do");
            Assert.AreEqual(Step.None, s.Want(true, T0.AddMinutes(4)));
            Assert.IsTrue(s.IsRegistered);
        }

        [TestMethod]
        public void ATimedOutAddThatFailsInTheEndIsRetriedLater()
        {
            var s = new SubscriptionState();
            s.Want(true, T0);
            s.TimedOut(Step.Add);
            Assert.AreEqual(Step.None, s.Completed(Step.Add, false, T0.AddMinutes(2)));
            Assert.IsFalse(s.IsRegistered);
            Assert.AreEqual(Step.Add, s.Want(true, T0.AddMinutes(2) + SubscriptionState.RetryAfter));
        }
    }
}
