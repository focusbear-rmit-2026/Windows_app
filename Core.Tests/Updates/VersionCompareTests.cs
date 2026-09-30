using System;
using FocusBear.Core.Updates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Updates
{
    /// <summary>
    /// System.Version treats a missing component as -1, so 1.6 &lt; 1.6.0 and 3.0.249 &lt; 3.0.249.0. The updater
    /// compared the server's version with the app's and the device's that way: a server INI written with one
    /// component more than the build (3.0.249.0 against 3.0.249) offered the same version again on every check,
    /// and a device reporting "1.6" was treated as older than the 1.6.0 feature gates.
    /// </summary>
    [TestClass]
    public class VersionCompareTests
    {
        [TestMethod]
        public void MissingComponentsCountAsZero()
        {
            Assert.IsFalse(VersionCompare.IsNewer(new Version(3, 0, 249, 0), new Version(3, 0, 249)));
            Assert.IsFalse(VersionCompare.IsNewer(new Version(3, 0, 249), new Version(3, 0, 249, 0)));
            Assert.IsTrue(VersionCompare.AtLeast(new Version(1, 6), new Version(1, 6, 0)));
        }

        [TestMethod]
        public void RealDifferencesStillCount()
        {
            Assert.IsTrue(VersionCompare.IsNewer(new Version(3, 0, 249, 2), new Version(3, 0, 249, 1)));
            Assert.IsTrue(VersionCompare.IsNewer(new Version(1, 8, 1), new Version(1, 8)));
            Assert.IsFalse(VersionCompare.IsNewer(new Version(3, 0, 248, 1), new Version(3, 0, 249, 1)));
            Assert.IsFalse(VersionCompare.AtLeast(new Version(1, 5, 9), new Version(1, 6, 0)));
        }

        [TestMethod]
        public void NullsAreNeverNewerOrAtLeast()
        {
            Assert.IsFalse(VersionCompare.IsNewer(null, new Version(1, 0)));
            Assert.IsTrue(VersionCompare.IsNewer(new Version(1, 0), null), "anything beats an unknown current version");
            Assert.IsFalse(VersionCompare.AtLeast(null, new Version(1, 0)));
        }

        [TestMethod]
        public void ParsingToleratesAVAndWhitespace()
        {
            Assert.AreEqual(new Version(3, 0, 248, 1), VersionCompare.Parse(" v3.0.248.1\r"));
            Assert.AreEqual(new Version(1, 8, 1), VersionCompare.Parse("1.8.1"));
            Assert.IsNull(VersionCompare.Parse("latest"));
            Assert.IsNull(VersionCompare.Parse(null));
        }
    }
}
