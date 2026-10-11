using System.Threading.Tasks;
using FocusBear.Core.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Auth
{
    [TestClass]
    public class TokenRefreshTests
    {
        [TestMethod]
        public void CallsThatFindTheTokenExpiredTogether_ShareOneRefresh()
        {
            var gate = new TokenRefreshGate();
            var auth0 = new TaskCompletionSource<bool>();
            int refreshes = 0;
            System.Func<Task<bool>> refresh = () => { refreshes++; return auth0.Task; };

            Task<bool> first = gate.RunAsync("rt-1", refresh);
            Task<bool> second = gate.RunAsync("rt-1", refresh);
            Task<bool> third = gate.RunAsync("rt-1", refresh);
            auth0.SetResult(true);

            Assert.AreEqual(1, refreshes, "one refresh token, one use: a second use trips Auth0's reuse detection");
            Assert.IsTrue(first.Result && second.Result && third.Result);
        }

        [TestMethod]
        public void AForcedRefresh_StartsAfterTheRunningOne_WithTheTokenItLeft()
        {
            // After a purchase the app refreshes to pick up new claims. Joining a refresh already under way handed
            // back that older answer, fetched before the purchase, and the upgrade didn't show until the next expiry.
            var gate = new TokenRefreshGate();
            var running = new TaskCompletionSource<bool>();
            string stored = "rt-1";
            var used = new System.Collections.Generic.List<string>();

            Task<bool> expiry = gate.RunAsync("rt-1", () => { used.Add("rt-1"); return running.Task; });
            Task<bool> forced = gate.RunForcedAsync(() => stored, token => { used.Add(token); return Task.FromResult(true); });
            Assert.IsFalse(forced.IsCompleted, "waits for the running refresh");

            stored = "rt-2";          // the running refresh rotated the token
            running.SetResult(true);
            Assert.IsTrue(forced.Result);
            CollectionAssert.AreEqual(new[] { "rt-1", "rt-2" }, used, "a second, new refresh, with the token the first one left");
        }

        [TestMethod]
        public void AForcedRefresh_WithNothingRunning_JustRefreshes()
        {
            var gate = new TokenRefreshGate();
            int n = 0;
            Assert.IsTrue(gate.RunForcedAsync(() => "rt", t => { n++; return Task.FromResult(true); }).Result);
            Assert.AreEqual(1, n);
        }

        [TestMethod]
        public void AFinishedRefresh_IsForgotten_SoTheNextExpiryRefreshesAgain()
        {
            var gate = new TokenRefreshGate();
            int refreshes = 0;
            gate.RunAsync("rt-1", () => { refreshes++; return Task.FromResult(true); }).Wait();
            gate.RunAsync("rt-1", () => { refreshes++; return Task.FromResult(false); }).Wait();
            var slow = new TaskCompletionSource<bool>();
            var pending = gate.RunAsync("rt-1", () => { refreshes++; return slow.Task; });
            slow.SetResult(true);
            pending.Wait();
            gate.RunAsync("rt-1", () => { refreshes++; return Task.FromResult(true); }).Wait();

            Assert.AreEqual(4, refreshes);
        }

        [TestMethod]
        public void ADifferentRefreshToken_IsItsOwnRefresh()
        {
            var gate = new TokenRefreshGate();
            var held = new TaskCompletionSource<bool>();
            int refreshes = 0;
            gate.RunAsync("old-account", () => { refreshes++; return held.Task; });
            gate.RunAsync("new-account", () => { refreshes++; return Task.FromResult(true); }).Wait();
            held.SetResult(false);

            Assert.AreEqual(2, refreshes);
        }

        [TestMethod]
        public void ARefreshThatThrows_DoesNotWedgeTheGate()
        {
            var gate = new TokenRefreshGate();
            var failing = gate.RunAsync("rt", () => Task.FromException<bool>(new System.InvalidOperationException("network")));
            Assert.IsTrue(failing.IsFaulted);
            Assert.IsTrue(gate.RunAsync("rt", () => Task.FromResult(true)).Result);
        }

        [TestMethod]
        public void AnAnswerWithoutANewRefreshToken_KeepsTheOneThatWasUsed()
        {
            Assert.AreEqual("rt-1", TokenRefreshGate.RefreshTokenToKeep("rt-1", null));
            Assert.AreEqual("rt-1", TokenRefreshGate.RefreshTokenToKeep("rt-1", ""));
            Assert.AreEqual("rt-2", TokenRefreshGate.RefreshTokenToKeep("rt-1", "rt-2"), "a rotated token replaces the old one");
        }
    }
}
