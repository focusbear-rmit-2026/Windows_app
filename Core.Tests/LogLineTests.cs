using System;
using System.Globalization;
using System.Threading;
using FocusBear.Core.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class LogLineTests
    {
        private static readonly DateTime At = new DateTime(2026, 9, 26, 15, 3, 25, 417);

        [TestMethod]
        public void EveryLineHasOneInvariantTimestampWhateverTheThreadsCulture()
        {
            // The log mixed "26/09/2026 3:03:25 PM" (threads still on the machine's culture) with "09/26/2026 15:03:26"
            // (after the app switched to the invariant culture); a Thai culture would even write year 2569.
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-AU", "en-US", "th-TH", "ar-SA", "" })
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                    Assert.AreEqual("2026-09-26 15:03:25.417 - GetCurrentAppVersion()", LogLine.Format(At, "GetCurrentAppVersion()"), culture);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void ANullMessageStillMakesALine()
        {
            Assert.AreEqual("2026-09-26 15:03:25.417 - ", LogLine.Format(At, null));
        }
    }
}
