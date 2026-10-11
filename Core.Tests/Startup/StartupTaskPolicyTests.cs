using System;
using System.Threading.Tasks;
using FocusBear.Core.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Startup
{
    /// <summary>Report 2026-09-28_21-45-38: Focus Bear took minutes to come back after a restart or an "End task".</summary>
    [TestClass]
    public class StartupTaskPolicyTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void TheLogonTaskNoLongerWaits()
        {
            Assert.IsFalse(StartupTaskPolicy.DelaysStart(StartupTaskPolicy.LogonDelay));
        }

        [TestMethod]
        public void TheOldFourMinuteDelayIsRepaired()
        {
            Assert.IsTrue(StartupTaskPolicy.DelaysStart("PT4M"));
            Assert.IsTrue(StartupTaskPolicy.DelaysStart("PT30S"));
            Assert.IsTrue(StartupTaskPolicy.DelaysStart("garbage"), "unreadable is rewritten to a known value");
            Assert.IsFalse(StartupTaskPolicy.DelaysStart(null));
            Assert.IsFalse(StartupTaskPolicy.DelaysStart("PT0S"));
        }

        [TestMethod]
        public void TheWatchdogChecksEveryMinute()
        {
            Assert.AreEqual("PT1M", StartupTaskPolicy.WatchdogRepetitionInterval);
            Assert.IsTrue(StartupTaskPolicy.WatchdogIntervalIsStale("PT2M"), "tasks registered by older builds");
            Assert.IsTrue(StartupTaskPolicy.WatchdogIntervalIsStale(""));
            Assert.IsFalse(StartupTaskPolicy.WatchdogIntervalIsStale("PT1M"));
        }

        [TestMethod]
        public void OnlyAnOfflineRenewFailureWaitsForTheNetwork()
        {
            Assert.IsTrue(TokenRenewRetry.ShouldRetry(false, TimeSpan.Zero), "Wi-Fi not up yet at sign-in");
            Assert.IsTrue(TokenRenewRetry.ShouldRetry(false, TimeSpan.FromMinutes(4)));
            Assert.IsFalse(TokenRenewRetry.ShouldRetry(false, TokenRenewRetry.MaxWaitForNetwork), "gives up in the end");
            Assert.IsFalse(TokenRenewRetry.ShouldRetry(true, TimeSpan.Zero), "online: the server refused the token");
        }

        [TestMethod]
        public async Task APrefetchIsHandedOverOnce()
        {
            var prefetch = new OneShotPrefetch<int>(TimeSpan.FromMinutes(2));
            int calls = 0;
            prefetch.Start(() => { calls++; return Task.FromResult(1); }, T0);
            prefetch.Start(() => { calls++; return Task.FromResult(2); }, T0);   // already waiting: ignored

            Assert.AreEqual(1, await prefetch.Take(() => Task.FromResult(9), T0.AddSeconds(5)));
            Assert.AreEqual(9, await prefetch.Take(() => Task.FromResult(9), T0.AddSeconds(6)), "a later caller asks again");
            Assert.AreEqual(1, calls);
            Assert.IsFalse(prefetch.IsWaiting);
        }

        [TestMethod]
        public async Task AStalePrefetchIsNotUsed()
        {
            var prefetch = new OneShotPrefetch<int>(TimeSpan.FromMinutes(2));
            prefetch.Start(() => Task.FromResult(1), T0);
            Assert.AreEqual(9, await prefetch.Take(() => Task.FromResult(9), T0.AddMinutes(3)));
        }

        [TestMethod]
        public async Task ADiscardedPrefetchIsNotHandedToTheNextSession()
        {
            var prefetch = new OneShotPrefetch<int>(TimeSpan.FromMinutes(2));
            prefetch.Start(() => Task.FromResult(1), T0);
            prefetch.Discard();
            Assert.AreEqual(9, await prefetch.Take(() => Task.FromResult(9), T0));
        }

        [TestMethod]
        public async Task AFailedPrefetchFallsBackToARequestMadeNow()
        {
            var prefetch = new OneShotPrefetch<int>(TimeSpan.FromMinutes(2));
            var failed = new TaskCompletionSource<int>();
            failed.SetException(new InvalidOperationException("offline"));
            prefetch.Start(() => failed.Task, T0);
            Assert.AreEqual(9, await prefetch.Take(() => Task.FromResult(9), T0));

            prefetch.Start(() => throw new InvalidOperationException("sync throw"), T0);
            Assert.IsFalse(prefetch.IsWaiting);
            Assert.AreEqual(9, await prefetch.Take(() => Task.FromResult(9), T0));
        }
    }
}
