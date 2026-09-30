using System;
using System.IO;
using FocusBear.Core.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Reports
{
    [TestClass]
    public class E2ETestPauseTests
    {
        private string _root;

        [TestInitialize]
        public void MakeRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "fb-e2e-pause-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void RemoveRoot()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }

        [TestMethod]
        public void NotPausedWithoutTheFlag()
        {
            Assert.IsFalse(E2ETestPause.IsPaused(_root));
        }

        [TestMethod]
        public void ResumeClearsThePauseAndLeavesAStampedRequest()
        {
            File.WriteAllText(Path.Combine(_root, E2ETestPause.PausedFileName), "paused by hotkey");
            Assert.IsTrue(E2ETestPause.IsPaused(_root));

            Assert.IsTrue(E2ETestPause.RequestResume(_root, new DateTime(2026, 9, 27, 21, 40, 5)));

            Assert.IsFalse(E2ETestPause.IsPaused(_root));
            StringAssert.Contains(File.ReadAllText(Path.Combine(_root, E2ETestPause.ResumeFileName)), "2026-09-27 21:40:05");
        }

        [TestMethod]
        public void ResumeFailsWhenTheFolderIsMissing()
        {
            Assert.IsFalse(E2ETestPause.RequestResume(Path.Combine(_root, "gone"), DateTime.Now));
        }
    }
}
