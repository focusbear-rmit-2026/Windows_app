using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FocusBear.Core.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Reports
{
    [TestClass]
    public class LogBundleTests
    {
        private string _dir;

        [TestInitialize] public void Make() { _dir = Path.Combine(Path.GetTempPath(), "fb-logbundle-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TestCleanup] public void Clean() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

        private string File(string name, string text) { string p = Path.Combine(_dir, name); System.IO.File.WriteAllText(p, text); return p; }

        [TestMethod]
        public void TheLogBeingWrittenRightNow_IsStillBundled()
        {
            string live = File("Debug_Logs.txt", "hello");
            string zip = Path.Combine(_dir, "out.zip");
            // The app's LogWriter holds the file open for writing (File.AppendText) while the report is built.
            using (var writer = new FileStream(live, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                var skipped = LogBundle.Write(zip, new[] { live }, new string[0]);
                Assert.AreEqual(0, skipped.Count);
            }
            using (var archive = ZipFile.OpenRead(zip))
            using (var reader = new StreamReader(archive.GetEntry("Logs/Debug_Logs.txt").Open()))
                Assert.AreEqual("hello", reader.ReadToEnd());
        }

        [TestMethod]
        public void AScreenshotDeletedSinceItWasPicked_IsLeftOut_NotTheWholeReport()
        {
            string log = File("a.txt", "x");
            string gone = Path.Combine(_dir, "gone.png");
            string zip = Path.Combine(_dir, "out.zip");

            var skipped = LogBundle.Write(zip, new[] { log }, new[] { gone });

            CollectionAssert.AreEqual(new[] { gone }, skipped.ToArray());
            using (var archive = ZipFile.OpenRead(zip)) Assert.AreEqual(1, archive.Entries.Count);
        }

        [TestMethod]
        public void TwoScreenshotsWithTheSameName_AreBothKept()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "one"));
            Directory.CreateDirectory(Path.Combine(_dir, "two"));
            string a = File(Path.Combine("one", "Screenshot.png"), "1"), b = File(Path.Combine("two", "Screenshot.png"), "2");
            string zip = Path.Combine(_dir, "out.zip");

            LogBundle.Write(zip, new string[0], new[] { a, b });

            using (var archive = ZipFile.OpenRead(zip))
                CollectionAssert.AreEquivalent(new[] { "Screenshots/Screenshot.png", "Screenshots/Screenshot (2).png" }, archive.Entries.Select(e => e.FullName).ToArray());
        }
    }
}
