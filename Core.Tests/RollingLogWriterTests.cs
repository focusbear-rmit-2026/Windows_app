using System;
using System.IO;
using System.Linq;
using System.Threading;
using FocusBear.Core.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class RollingLogWriterTests
    {
        private string _dir;
        private string LogPath => Path.Combine(_dir, "Debug_Logs.txt");

        [TestInitialize]
        public void Make() { _dir = Path.Combine(Path.GetTempPath(), "fb-log-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }

        [TestCleanup]
        public void Remove() { try { Directory.Delete(_dir, true); } catch { } }

        [TestMethod]
        public void APathThatThrows_KeepsTheLinesAndNeverThrows()
        {
            // Flush runs on a thread-pool timer: an exception there ends the process. The path is read through
            // AppSettings.AppDataDirLogsPath, which creates the folder and can throw (access denied, disk gone).
            bool fail = true;
            var log = new RollingLogWriter(() => { if (fail) throw new UnauthorizedAccessException("no"); return LogPath; },
                                           flushDelay: TimeSpan.FromHours(1));
            log.Write("kept");
            log.Flush();                      // must not throw
            fail = false;
            log.Flush();
            CollectionAssert.AreEqual(new[] { "kept" }, File.ReadAllLines(LogPath), "the line waited for the next flush");
        }

        [TestMethod]
        public void AfterARoll_TheNewLiveFileIsNewNotTheOldFilesCreationDate()
        {
            // NTFS "tunnelling": a file created under a name renamed away moments earlier inherits the old file's
            // creation time. The daily rotation reads that time, so a live log that looked days old was rotated on
            // every start (and the 5-file cleanup could pick the live file as the oldest).
            File.WriteAllText(LogPath, new string('x', 200));
            File.SetCreationTime(LogPath, DateTime.Now.AddDays(-10));
            var log = new RollingLogWriter(() => LogPath, maxBytes: 100, flushDelay: TimeSpan.FromHours(1));

            log.Write("rolls it");
            log.Flush();                          // appended, over the cap: rolled to .1
            Assert.IsTrue(File.Exists(RollingLogWriter.RolledPath(LogPath, 1)));
            log.Write("new file");
            log.Flush();

            Assert.IsTrue(File.Exists(LogPath));
            Assert.IsTrue((DateTime.Now - File.GetCreationTime(LogPath)).TotalHours < 1,
                "the live file's creation time is now, not " + File.GetCreationTime(LogPath));
        }

        [TestMethod]
        public void LinesAreBufferedAndAppendedInOrderToTheSameFile()
        {
            File.WriteAllText(LogPath, "earlier" + Environment.NewLine);
            var log = new RollingLogWriter(() => LogPath, flushDelay: TimeSpan.FromHours(1));
            log.Write("one");
            log.Write("two");
            Assert.AreEqual(1, File.ReadAllLines(LogPath).Length, "nothing is written per line");

            log.Flush();

            CollectionAssert.AreEqual(new[] { "earlier", "one", "two" }, File.ReadAllLines(LogPath), "appended, never rewritten");
        }

        [TestMethod]
        public void AWriteReachesTheFileOnItsOwnShortlyAfter()
        {
            var log = new RollingLogWriter(() => LogPath, flushDelay: TimeSpan.FromMilliseconds(50));
            log.Write("hello");
            for (int i = 0; i < 100 && !(File.Exists(LogPath) && File.ReadAllText(LogPath).Contains("hello")); i++) Thread.Sleep(20);
            StringAssert.Contains(File.ReadAllText(LogPath), "hello");
        }

        [TestMethod]
        public void TheFileRollsAtTheSizeAndKeepsThreeOldOnes()
        {
            var log = new RollingLogWriter(() => LogPath, maxBytes: 100, keep: 3, flushDelay: TimeSpan.FromHours(1));
            for (int round = 1; round <= 5; round++)
            {
                log.Write("round " + round + " " + new string('x', 120));
                log.Flush();
            }

            Assert.IsFalse(File.Exists(LogPath), "the last write rolled it; the next line starts a new file");
            string[] kept = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "Debug_Logs.1.txt", "Debug_Logs.2.txt", "Debug_Logs.3.txt" }, kept);
            StringAssert.StartsWith(File.ReadAllText(Path.Combine(_dir, "Debug_Logs.1.txt")), "round 5");
            StringAssert.StartsWith(File.ReadAllText(Path.Combine(_dir, "Debug_Logs.3.txt")), "round 3");

            log.Write("after");
            log.Flush();
            StringAssert.StartsWith(File.ReadAllText(LogPath), "after", "the live file keeps its name");
        }

        [TestMethod]
        public void TheFileCanBeReadAndMovedBetweenWrites()
        {
            var log = new RollingLogWriter(() => LogPath, flushDelay: TimeSpan.FromHours(1));
            log.Write("a");
            log.Flush();
            using (File.OpenRead(LogPath)) { }   // what the log upload does
            File.Move(LogPath, Path.Combine(_dir, "Debug_Logs-2026-09-27.txt"));   // the daily rotation
            log.Write("b");
            log.Flush();
            Assert.AreEqual("b", File.ReadAllText(LogPath).Trim());
        }

        [TestMethod]
        public void LinesSurviveAFileThatCantBeWrittenForAWhile()
        {
            var log = new RollingLogWriter(() => LogPath, flushDelay: TimeSpan.FromHours(1));
            using (new FileStream(LogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                log.Write("kept");
                log.Flush();
            }
            log.Write("next");
            log.Flush();
            CollectionAssert.AreEqual(new[] { "kept", "next" }, File.ReadAllLines(LogPath));
        }

        [TestMethod]
        public void APathThatCannotBeWorkedOutNeverThrowsAndTheLinesWait()
        {
            // The flush runs on a thread-pool timer: an exception there would take the whole app down.
            bool broken = true;
            var log = new RollingLogWriter(() => broken ? throw new InvalidOperationException("no data folder yet") : LogPath, flushDelay: TimeSpan.FromHours(1));
            log.Write("kept");
            log.Flush();   // must not throw

            broken = false;
            log.Flush();
            CollectionAssert.AreEqual(new[] { "kept" }, File.ReadAllLines(LogPath));
        }
    }
}
