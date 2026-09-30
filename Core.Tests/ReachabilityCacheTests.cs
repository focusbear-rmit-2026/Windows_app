using System;
using FocusBear.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class ReachabilityCacheTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 27, 2, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void AReachableAnswerIsReusedForHalfAMinute()
        {
            var cache = new ReachabilityCache();
            Assert.IsNull(cache.Cached(T0), "nothing asked yet");
            cache.Record(true, T0);
            Assert.AreEqual(true, cache.Cached(T0.AddSeconds(29)));
            Assert.IsNull(cache.Cached(T0 + ReachabilityCache.ReachableFor));
        }

        [TestMethod]
        public void AnUnreachableAnswerIsRetriedAlmostAtOnce()
        {
            var cache = new ReachabilityCache();
            cache.Record(false, T0);
            Assert.AreEqual(false, cache.Cached(T0.AddSeconds(1)), "a burst of calls doesn't each wait for its own timeout");
            Assert.IsNull(cache.Cached(T0 + ReachabilityCache.UnreachableFor), "a blip must not fail every call for long");
        }

        [TestMethod]
        public void AClockThatWentBackOrAForgetAsksAgain()
        {
            var cache = new ReachabilityCache();
            cache.Record(true, T0);
            Assert.IsNull(cache.Cached(T0.AddMinutes(-5)));
            cache.Forget();
            Assert.IsNull(cache.Cached(T0.AddSeconds(1)));
        }
    }
}
