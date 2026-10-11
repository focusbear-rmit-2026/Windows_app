using System;
using System.Collections.Generic;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class ProcessNameCacheTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 26, 5, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void AReusedProcessId_IsNotTakenForTheOldApp()
        {
            // A name was trusted for two minutes by id alone: when Windows handed a quickly exited app's id to a
            // browser, the browser was taken for the old app and its URL checks were skipped.
            var names = new Dictionary<int, string> { [100] = "notepad.exe" };
            var started = new Dictionary<int, DateTime> { [100] = T0 };
            var cache = new ProcessNameCache(pid => names[pid], _ => { }, pid => started[pid]);

            Assert.AreEqual("notepad.exe", cache.NameFor(100, T0));
            names[100] = "chrome.exe";
            started[100] = T0.AddSeconds(5);   // a new process under the same id
            Assert.AreEqual("chrome.exe", cache.NameFor(100, T0.AddSeconds(6)));
        }

        [TestMethod]
        public void TheSameProcess_StaysCached()
        {
            int lookups = 0;
            var cache = new ProcessNameCache(pid => { lookups++; return "chrome.exe"; }, _ => { }, pid => T0);
            for (int i = 0; i < 50; i++) cache.NameFor(7, T0.AddSeconds(i));
            Assert.AreEqual(1, lookups);
        }

        [TestMethod]
        public void AnExitedProcessIsLookedUpAndLoggedOnceNotOnEveryEvent()
        {
            int lookups = 0;
            var logged = new List<string>();
            var cache = new ProcessNameCache(pid => { lookups++; throw new InvalidOperationException("Process has exited"); }, logged.Add);

            for (int i = 0; i < 170; i++)
                Assert.AreEqual("", cache.NameFor(4242, T0.AddMilliseconds(i * 100)));

            Assert.AreEqual(1, lookups);
            Assert.AreEqual(1, logged.Count);
        }

        [TestMethod]
        public void AFailedIdIsRetriedLaterInCaseWindowsReusedIt()
        {
            bool exited = true;
            var logged = new List<string>();
            var cache = new ProcessNameCache(pid => exited ? throw new InvalidOperationException("gone") : "chrome.exe", logged.Add);

            cache.NameFor(7, T0);
            exited = false;
            Assert.AreEqual("", cache.NameFor(7, T0.AddSeconds(5)));
            Assert.AreEqual("chrome.exe", cache.NameFor(7, T0 + ProcessNameCache.FailureRetry));
        }

        [TestMethod]
        public void TheLastGoodNameIsReusedAndSwitchingAppsResolvesTheNewOne()
        {
            int lookups = 0;
            var cache = new ProcessNameCache(pid => { lookups++; return pid == 1 ? "code.exe" : "chrome.exe"; }, null);

            Assert.AreEqual("code.exe", cache.NameFor(1, T0));
            Assert.AreEqual("code.exe", cache.NameFor(1, T0));
            Assert.AreEqual("chrome.exe", cache.NameFor(2, T0));
            Assert.AreEqual(2, lookups);
            Assert.AreEqual("", cache.NameFor(0, T0));
        }

        [TestMethod]
        public void EventsAlternatingBetweenAppsLookEachUpOnce()
        {
            // A terminal redrawing while the browser is in front: the old one-entry cache looked one up on every switch.
            int lookups = 0;
            var cache = new ProcessNameCache(pid => { lookups++; return pid == 1 ? "WindowsTerminal.exe" : "chrome.exe"; }, null);
            for (int i = 0; i < 1000; i++) cache.NameFor(1 + i % 2, T0.AddMilliseconds(i * 20));
            Assert.AreEqual(2, lookups);
        }

        [TestMethod]
        public void ANameIsReadAgainAfterItsLifetimeInCaseTheIdWasReused()
        {
            string current = "notepad.exe";
            var cache = new ProcessNameCache(pid => current, null);
            Assert.AreEqual("notepad.exe", cache.NameFor(9, T0));
            current = "chrome.exe";
            Assert.AreEqual("notepad.exe", cache.NameFor(9, T0.AddSeconds(30)));
            Assert.AreEqual("chrome.exe", cache.NameFor(9, T0 + ProcessNameCache.NameLifetime));
        }
    }
}
