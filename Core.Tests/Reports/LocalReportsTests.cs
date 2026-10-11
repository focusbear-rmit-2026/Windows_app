using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FocusBear.Core.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Reports
{
    /// <summary>
    /// Ported from the Mac's <c>LocalReportStatusTests.swift</c>: the folder format both apps share, the status a
    /// report shows, the review-notes parser, and the writers that must never lose the user's words or write
    /// into another account's report.
    /// </summary>
    [TestClass]
    public class LocalReportsTests
    {
        private const string Me = LocalReportCapability.AccountId;
        private const string SomeoneElse = "00000000-0000-0000-0000-000000000000";
        private string _root;

        [TestInitialize]
        public void MakeRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "fb-reports-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void RemoveRoot()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }

        private static string Task(string created, string note, string owner = Me, string extra = "") =>
            LocalReportTaskFile.Build(note, created, "3.0.250", owner, new List<string>(), new List<string>(), "C:/x") + extra;

        private string Folder(string created, string note, string location = null, string owner = Me, string extra = "")
        {
            string parent = location == null ? _root : Path.Combine(_root, location);
            string folder = Path.Combine(parent, created);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, LocalReportTaskFile.TaskFileName), Task(created, note, owner, extra));
            return folder;
        }

        private LocalReport Only() => LocalReportReader.Scan(_root, Me).Reports.Single();

        // ---------------------------------------------------------------- filing

        [TestMethod]
        public void Content_IsADescriptionOrAScreenshot_NotWhitespace()
        {
            Assert.IsTrue(LocalReportTaskFile.HasContent("it broke", null));
            Assert.IsTrue(LocalReportTaskFile.HasContent("  ", new[] { "a.png" }));
            Assert.IsFalse(LocalReportTaskFile.HasContent(" \n ", new string[0]));
        }

        [TestMethod]
        public void Write_BundlesLogsAndScreenshots_AndRefusesOtherAccountsAndEmptyReports()
        {
            string logs = Path.Combine(_root, "_logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "Debug_Logs.txt"), "log line");
            File.WriteAllText(Path.Combine(logs, "ignored.bin"), "x");
            string shot = Path.Combine(_root, "_shot.png");
            File.WriteAllBytes(shot, new byte[] { 1, 2, 3 });
            string reports = Path.Combine(_root, "reports");
            Directory.CreateDirectory(reports);
            var now = new DateTime(2026, 9, 25, 1, 2, 3);

            Assert.IsNull(LocalReportTaskFile.Write(reports, "  ", new string[0], logs, "3.0", Me, now));
            Assert.IsNull(LocalReportTaskFile.Write(reports, "hi", new string[0], logs, "3.0", SomeoneElse, now));
            Assert.AreEqual(0, Directory.GetDirectories(reports).Length, "a refused report creates nothing");

            string folder = LocalReportTaskFile.Write(reports, "Tray menu froze", new[] { shot }, logs, "3.0", Me, now);
            Assert.AreEqual("2026-09-25_01-02-03", Path.GetFileName(folder));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "logs", "Debug_Logs.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(folder, "logs", "ignored.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "screenshots", "_shot.png")));
            string task = File.ReadAllText(Path.Combine(folder, "TASK.md"));
            StringAssert.Contains(task, "- Created: 2026-09-25_01-02-03");
            StringAssert.Contains(task, "- User: " + Me);
            StringAssert.Contains(task, "## What to investigate\nTray menu froze");
            StringAssert.Contains(task, "- screenshots/_shot.png");

            var report = LocalReportReader.Scan(reports, Me).Reports.Single();
            Assert.AreEqual("Tray menu froze", report.Title);
            Assert.AreEqual(LocalReportReviewState.Queued, report.State);
            Assert.AreEqual(1, report.ScreenshotPaths.Count);
        }

        // ---------------------------------------------------------------- reading

        [TestMethod]
        public void OnlyASingleHeaderOwnerCanSeeAReport()
        {
            Folder("2026-09-20_10-00-00", "mine");
            Folder("2026-09-20_11-00-00", "theirs", owner: SomeoneElse);
            string twoOwners = Folder("2026-09-20_12-00-00", "ambiguous");
            File.WriteAllText(Path.Combine(twoOwners, "TASK.md"), "# t\n\n- Created: 2026-09-20_12-00-00\n- User: " + Me + "\n- User: " + SomeoneElse + "\n\n## What to investigate\nx\n");

            var reports = LocalReportReader.Scan(_root, Me).Reports;
            CollectionAssert.AreEqual(new[] { "mine" }, reports.Select(r => r.Title).ToArray());
            Assert.AreEqual(0, LocalReportReader.Scan(_root, "").Reports.Count);
        }

        [TestMethod]
        public void TheFolderAReportSitsInIsItsState()
        {
            Folder("2026-09-20_10-00-00", "queued one");
            Folder("2026-09-20_11-00-00", "ready one", LocalReportFolders.ReadyForQA);
            Folder("2026-09-20_12-00-00", "blocked one", LocalReportFolders.NeedsClarification);
            Folder("2026-09-20_13-00-00", "backend one", LocalReportFolders.BlockedOnBackend);

            var byTitle = LocalReportReader.Scan(_root, Me).Reports.ToDictionary(r => r.Title, r => r.State);
            Assert.AreEqual(LocalReportReviewState.Queued, byTitle["queued one"]);
            Assert.AreEqual(LocalReportReviewState.ReadyForQA, byTitle["ready one"]);
            Assert.AreEqual(LocalReportReviewState.NeedsClarification, byTitle["blocked one"]);
            Assert.AreEqual(LocalReportReviewState.BlockedOnBackend, byTitle["backend one"]);
        }

        [TestMethod]
        public void OnlyExplicitStatusMetadataClaimsInvestigating()
        {
            string explicitStatus = Folder("2026-09-20_10-00-00", "a");
            File.WriteAllText(Path.Combine(explicitStatus, "CLAUDE-NOTES.md"), "# Notes\n\n## Status\nInvestigating\n");
            string prose = Folder("2026-09-20_11-00-00", "b");
            File.WriteAllText(Path.Combine(prose, "CLAUDE-NOTES.md"), "I was investigating the parser, still queued in my head.\n");

            var byTitle = LocalReportReader.Scan(_root, Me).Reports.ToDictionary(r => r.Title, r => r.State);
            Assert.AreEqual(LocalReportReviewState.Investigating, byTitle["a"]);
            Assert.AreEqual(LocalReportReviewState.Queued, byTitle["b"]);
        }

        [TestMethod]
        public void NewestReportsComeFirst()
        {
            Folder("2026-09-20_10-00-00", "older");
            Folder("2026-09-21_10-00-00", "newer", LocalReportFolders.ReadyForQA);
            CollectionAssert.AreEqual(new[] { "newer", "older" }, LocalReportReader.Scan(_root, Me).Reports.Select(r => r.Title).ToArray());
        }

        [TestMethod]
        public void InvalidCreatedStampsAndOversizedTasksAreIgnored()
        {
            string bad = Path.Combine(_root, "2026-09-20_10-00-00");
            Directory.CreateDirectory(bad);
            File.WriteAllText(Path.Combine(bad, "TASK.md"), "- Created: yesterday\n- User: " + Me + "\n");
            string huge = Folder("2026-09-20_11-00-00", "huge");
            File.AppendAllText(Path.Combine(huge, "TASK.md"), new string('x', LocalReportReader.MaximumTextBytes));
            Assert.AreEqual(0, LocalReportReader.Scan(_root, Me).Reports.Count);
            Assert.AreEqual(0, LocalReportReader.Scan(Path.Combine(_root, "missing"), Me).Reports.Count);
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "missing")), "scanning never creates the root");
        }

        [TestMethod]
        public void TitlesAreBoundedAndNeverComeFromTheLogsSection()
        {
            Folder("2026-09-20_10-00-00", new string('a', 300));
            Assert.AreEqual(180, Only().Title.Length);
        }

        [TestMethod]
        public void ScreenshotsComeOnlyFromTheReportsOwnFolder()
        {
            string folder = Folder("2026-09-20_10-00-00", "shots");
            Directory.CreateDirectory(Path.Combine(folder, "screenshots"));
            File.WriteAllBytes(Path.Combine(folder, "screenshots", "b.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(folder, "screenshots", "a.jpg"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(folder, "screenshots", "notes.txt"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(folder, "stray.png"), new byte[] { 1 });

            var report = Only();
            CollectionAssert.AreEqual(new[] { "a.jpg", "b.png" }, report.ScreenshotPaths.Select(Path.GetFileName).ToArray());
            Assert.IsTrue(LocalReportReader.MayOpenScreenshot(report.ScreenshotPaths[0], report, Me));
            Assert.IsFalse(LocalReportReader.MayOpenScreenshot(Path.Combine(folder, "stray.png"), report, Me));
            Assert.IsFalse(LocalReportReader.MayOpenScreenshot(report.ScreenshotPaths[0], report, SomeoneElse));
        }

        // ---------------------------------------------------------------- rework and confirmation

        [TestMethod]
        public void FeedbackNewerThanTheNotesMeansReworkNeeded_AndOlderLeavesTheStateAlone()
        {
            string folder = Folder("2026-09-20_10-00-00", "a", LocalReportFolders.ReadyForQA,
                                   extra: "\n\n## Additional instructions\n\n- Added: 2026-09-21T08:00:00Z\n\nStill broken\n");
            string notes = Path.Combine(folder, "CLAUDE-NOTES.md");
            File.WriteAllText(notes, "# Fix\n\nFixed it.\n");
            File.SetLastWriteTimeUtc(notes, new DateTime(2026, 9, 21, 7, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(LocalReportReviewState.ReworkNeeded, Only().State);
            Assert.AreEqual("Still broken", Only().LatestInstructions);

            File.SetLastWriteTimeUtc(notes, new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(LocalReportReviewState.ReadyForQA, Only().State);
        }

        [TestMethod]
        public void ConfirmationRoundTrips_WithoutTouchingTheTaskOrNotes()
        {
            string folder = Folder("2026-09-20_10-00-00", "a", LocalReportFolders.ReadyForQA);
            string task = File.ReadAllText(Path.Combine(folder, "TASK.md"));
            var report = Only();

            LocalReportConfirmationWriter.SetConfirmedFixed(true, report, _root, Me, new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc));
            var confirmed = Only();
            Assert.AreEqual(LocalReportReviewState.ConfirmedFixed, confirmed.State);
            Assert.AreEqual(task, File.ReadAllText(Path.Combine(folder, "TASK.md")));

            LocalReportConfirmationWriter.SetConfirmedFixed(false, confirmed, _root, Me, new DateTime(2026, 9, 22, 11, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(LocalReportReviewState.ReadyForQA, Only().State);
        }

        [TestMethod]
        public void AStaleConfirmationCannotReplaceANewerChoice()
        {
            Folder("2026-09-20_10-00-00", "a", LocalReportFolders.ReadyForQA);
            var stale = Only();
            LocalReportConfirmationWriter.SetConfirmedFixed(true, stale, _root, Me, DateTime.UtcNow);
            Assert.ThrowsException<LocalReportWriteException>(() =>
                LocalReportConfirmationWriter.SetConfirmedFixed(false, stale, _root, Me, DateTime.UtcNow));
            Assert.AreEqual(LocalReportReviewState.ConfirmedFixed, Only().State);
        }

        [TestMethod]
        public void ForeignOrMalformedStatusCannotClaimConfirmation()
        {
            string folder = Folder("2026-09-20_10-00-00", "a");
            File.WriteAllText(Path.Combine(folder, LocalReportUserStatus.FileName),
                "{\"schema_version\":1,\"account_id\":\"" + SomeoneElse + "\",\"report_created\":\"2026-09-20_10-00-00\",\"confirmed_fixed\":true,\"updated_at\":\"2026-09-21T00:00:00Z\"}");
            Assert.AreEqual(LocalReportReviewState.Queued, Only().State);
            File.WriteAllText(Path.Combine(folder, LocalReportUserStatus.FileName), "not json");
            Assert.AreEqual(LocalReportReviewState.Queued, Only().State);
            Assert.ThrowsException<LocalReportWriteException>(() =>
                LocalReportConfirmationWriter.SetConfirmedFixed(true, Only(), _root, Me, DateTime.UtcNow), "an unknown status file is not silently replaced");
        }

        [TestMethod]
        public void FeedbackAfterConfirmingReopens_AndConfirmingAfterFeedbackStaysFixed()
        {
            string folder = Folder("2026-09-20_10-00-00", "a", LocalReportFolders.ReadyForQA);
            LocalReportConfirmationWriter.SetConfirmedFixed(true, Only(), _root, Me, new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc));
            LocalReportInstructionWriter.Append("Came back", Only(), _root, Me, new DateTime(2026, 9, 22, 11, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(LocalReportReviewState.ReworkNeeded, Only().State);

            LocalReportConfirmationWriter.SetConfirmedFixed(true, Only(), _root, Me, new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(LocalReportReviewState.ConfirmedFixed, Only().State);
        }

        // ---------------------------------------------------------------- instructions

        [TestMethod]
        public void InstructionsPreserveTheTaskAndAppearOnRescan()
        {
            string folder = Folder("2026-09-20_10-00-00", "a");
            string before = File.ReadAllText(Path.Combine(folder, "TASK.md"));
            LocalReportInstructionWriter.Append("  Try with two monitors  ", Only(), _root, Me, new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc));

            string after = File.ReadAllText(Path.Combine(folder, "TASK.md"));
            Assert.IsTrue(after.StartsWith(before));
            StringAssert.Contains(after, "## Additional instructions\n\n- Added: 2026-09-21T08:00:00Z\n\nTry with two monitors\n");
            Assert.AreEqual("Try with two monitors", Only().LatestInstructions);
        }

        [TestMethod]
        public void BlankOversizedAndForeignInstructionsCannotChangeTheTask()
        {
            Folder("2026-09-20_10-00-00", "a");
            var report = Only();
            Assert.ThrowsException<LocalReportWriteException>(() => LocalReportInstructionWriter.Append("   ", report, _root, Me, DateTime.UtcNow));
            Assert.ThrowsException<LocalReportWriteException>(() => LocalReportInstructionWriter.Append(new string('x', 4001), report, _root, Me, DateTime.UtcNow));
            Assert.ThrowsException<LocalReportWriteException>(() => LocalReportInstructionWriter.Append("hi", report, _root, SomeoneElse, DateTime.UtcNow));
            Assert.IsNull(Only().LatestInstructions);
        }

        [TestMethod]
        public void AReportMovedOutOfTheKnownLocationsCannotBeWritten()
        {
            string folder = Folder("2026-09-20_10-00-00", "a");
            var report = Only();
            string elsewhere = Path.Combine(_root, "Somewhere Else");
            Directory.CreateDirectory(elsewhere);
            string moved = Path.Combine(elsewhere, "2026-09-20_10-00-00");
            Directory.Move(folder, moved);
            report.Id = moved;
            Assert.ThrowsException<LocalReportWriteException>(() => LocalReportInstructionWriter.Append("hi", report, _root, Me, DateTime.UtcNow));
        }

        [TestMethod]
        public void APastedImageLandsInTheReportsScreenshotsFolder()
        {
            string folder = Folder("2026-09-20_10-00-00", "a");
            var now = new DateTime(2026, 9, 21, 8, 30, 0);
            string name = LocalReportImageWriter.Save(new byte[] { 137, 80, 78, 71 }, Only(), _root, Me, now);
            Assert.AreEqual("pasted-2026-09-21_08-30-00.png", name);
            Assert.IsTrue(File.Exists(Path.Combine(folder, "screenshots", name)));
            Assert.AreEqual("[image: screenshots/pasted-2026-09-21_08-30-00.png]", LocalReportImageWriter.Reference(name));
            Assert.ThrowsException<LocalReportWriteException>(() => LocalReportImageWriter.Save(new byte[] { 1 }, Only(), _root, SomeoneElse, now));
        }

        // ---------------------------------------------------------------- notes

        [TestMethod]
        public void ANoteWithoutUpdatesIsAllOriginal()
        {
            var notes = LocalReportNoteParser.Parse("# Title\n\nThe diagnosis.\n\n---\n");
            Assert.AreEqual("Title", notes.Title);
            Assert.AreEqual("The diagnosis.", notes.Original);
            Assert.IsNull(notes.Latest);
        }

        [TestMethod]
        public void UpdatesKeepTheirOrder_StampedOnesCarryTheirMoment()
        {
            var notes = LocalReportNoteParser.Parse("# T\n\nFirst.\n\n## Update\n\nBare one.\n\n## Update 2 — 2026-09-15 22:40 — parser fixed\n\nLead words\nmore.\n\n### How to test\n\nsteps\n");
            Assert.AreEqual(2, notes.Updates.Count);
            Assert.AreEqual("", notes.Updates[0].Summary);
            Assert.IsNull(notes.Updates[0].Date);
            Assert.AreEqual("parser fixed", notes.Latest.Summary);
            Assert.AreEqual(new DateTime(2026, 9, 15, 22, 40, 0), notes.Latest.Date);
            Assert.AreEqual("Lead words more.", notes.Latest.Lead);
            StringAssert.Contains(notes.Latest.Body, "### How to test");
            Assert.AreEqual(2, notes.NewestFirst[0].Number);
        }

        [TestMethod]
        public void LookalikeHeadingsAreNotSplitOn()
        {
            Assert.IsNull(LocalReportNoteParser.UpdateHeading("## Updated the parser"));
            Assert.IsNull(LocalReportNoteParser.UpdateHeading("### Update 2 code"));
            Assert.IsNotNull(LocalReportNoteParser.UpdateHeading("##Update (2026-09-15) — x"));
            Assert.IsNotNull(LocalReportNoteParser.UpdateHeading("## updates"));
        }

        [TestMethod]
        public void TheRowSummarisesTheNewestUpdate()
        {
            string folder = Folder("2026-09-20_10-00-00", "a");
            File.WriteAllText(Path.Combine(folder, "CLAUDE-NOTES.md"), "# N\n\nOld diagnosis.\n\n## Update — first\n\nOlder news.\n\n## Update — second\n\nThe news.\n");
            var report = Only();
            Assert.AreEqual("The news.", report.NotesExcerpt);
            Assert.AreEqual(2, report.LatestUpdate.Number);
            Assert.IsNotNull(report.LastAIUpdate);
            Assert.IsTrue(LocalReportReader.MayOpenNotes(report, Me));
            Assert.IsFalse(LocalReportReader.MayOpenNotes(report, SomeoneElse));
        }

        // ---------------------------------------------------------------- list

        private static LocalReport R(string title, LocalReportReviewState state, string notes = null, string instructions = null, string created = "2026-09-20_10-00-00") =>
            new LocalReport { Id = title, Title = title, State = state, NotesExcerpt = notes, LatestInstructions = instructions, ReportCreated = created };

        [TestMethod]
        public void GroupsPutAttentionFirst_EmptyOnesAreNotShown()
        {
            var groups = LocalReportGrouping.Groups(new[]
            {
                R("q", LocalReportReviewState.Queued), R("f", LocalReportReviewState.ConfirmedFixed),
                R("r", LocalReportReviewState.ReworkNeeded), R("ready", LocalReportReviewState.ReadyForQA),
            });
            CollectionAssert.AreEqual(new[] { LocalReportReviewState.ReworkNeeded, LocalReportReviewState.ReadyForQA, LocalReportReviewState.Queued, LocalReportReviewState.ConfirmedFixed },
                                      groups.Select(g => g.Key).ToArray());
            Assert.IsFalse(LocalReportGrouping.ExpandedByDefault.Contains(LocalReportReviewState.Queued));
            Assert.IsFalse(LocalReportGrouping.ExpandedByDefault.Contains(LocalReportReviewState.ConfirmedFixed));
        }

        [TestMethod]
        public void SearchIgnoresCaseAndAccents_AndLooksAtNotesInstructionsAndDate()
        {
            var reports = new[] { R("Café crash", LocalReportReviewState.Queued), R("x", LocalReportReviewState.Queued, notes: "tray icon"), R("y", LocalReportReviewState.Queued, instructions: "two MONITORS"), R("z", LocalReportReviewState.Queued, created: "2026-09-01_08-00-00") };
            Assert.AreEqual("Café crash", LocalReportSearch.Matching(reports, "cafe").Single().Title);
            Assert.AreEqual("x", LocalReportSearch.Matching(reports, "TRAY").Single().Title);
            Assert.AreEqual("y", LocalReportSearch.Matching(reports, "monitors").Single().Title);
            Assert.AreEqual("z", LocalReportSearch.Matching(reports, "2026-09-01").Single().Title);
            Assert.AreEqual(4, LocalReportSearch.Matching(reports, "  ").Count);
            Assert.AreEqual(0, LocalReportSearch.Matching(reports, "nothing like it").Count);
        }

        [TestMethod]
        public void AReportIsFiledUnderTheAreaItTalksAbout()
        {
            Assert.AreEqual(LocalReportArea.DayPlan, LocalReportAreas.Detect("Day plan shows yesterday", null));
            Assert.AreEqual(LocalReportArea.TasksProjects, LocalReportAreas.Detect("Weird", "the project board"));
            Assert.AreEqual(LocalReportArea.Other, LocalReportAreas.Detect("Weird", null));
            var present = LocalReportAreas.Present(new[] { R("tray froze", LocalReportReviewState.Queued), R("streak wrong", LocalReportReviewState.Queued) });
            CollectionAssert.AreEqual(new[] { LocalReportArea.Stats, LocalReportArea.MenuBar }, present.ToArray());
        }

        [TestMethod]
        public void UpdateAgeReadsBothHalves()
        {
            var now = new DateTime(2026, 9, 25, 12, 0, 0);
            var culture = CultureInfo.GetCultureInfo("en-AU");
            Assert.IsNull(LocalReportUpdateAge.Describe(null, now, culture));
            Assert.AreEqual("now", LocalReportUpdateAge.Describe(now.AddSeconds(-20), now, culture).Item1);
            Assert.AreEqual("now", LocalReportUpdateAge.Describe(now.AddSeconds(30), now, culture).Item1, "a clock a little ahead reads as now");
            Assert.AreEqual("5m ago", LocalReportUpdateAge.Describe(now.AddMinutes(-5), now, culture).Item1);
            Assert.AreEqual("2h ago", LocalReportUpdateAge.Describe(now.AddHours(-2), now, culture).Item1);
            StringAssert.StartsWith(LocalReportUpdateAge.Describe(now.AddDays(-1), now, culture).Item2, "Yesterday");
            Assert.AreEqual("1y ago", LocalReportUpdateAge.Describe(now.AddDays(-400), now, culture).Item1);
        }

        [TestMethod]
        public void ImageReferences_ThatWouldTakeTheInstructionsPastTheLimit_AreRefusedBeforeAnyImageIsSaved()
        {
            var now = new DateTime(2026, 9, 27, 10, 0, 0);
            string nearlyFull = new string('x', LocalReportInstructionWriter.MaximumCharacters - 20);

            Assert.IsFalse(LocalReportInstructionWriter.ReferencesFit(nearlyFull, 1, now));
            Assert.IsTrue(LocalReportInstructionWriter.ReferencesFit("short", 3, now));
            Assert.IsTrue(LocalReportInstructionWriter.ReferencesFit(nearlyFull, 0, now));
        }

        [TestMethod]
        public void ReferencesThatFit_GoOnLinesOfTheirOwn_AndTheResultIsSaveable()
        {
            string combined = LocalReportInstructionWriter.WithReferences("Look at this  ", new[] { "[image: screenshots/a.png]", "[image: screenshots/b.png]" });
            Assert.AreEqual("Look at this\n[image: screenshots/a.png]\n[image: screenshots/b.png]", combined);
            Assert.IsNull(LocalReportInstructionWriter.WithReferences(new string('x', LocalReportInstructionWriter.MaximumCharacters), new[] { "[image: a]" }));
        }
    }
}
