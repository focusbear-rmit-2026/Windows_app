using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The rule the Day Plan composer commits a typed name by, ported from the Mac's
    /// <c>FocusBearTests/TaskNameCommitTests.swift</c> (its pure cases; the AppKit panel cases are
    /// covered by the Windows harness instead). The case worth protecting is RefuseTokensOnly: typing
    /// only a <c>#project</c>, <c>!priority</c> or <c>*minutes</c> used to be committed as a rename to
    /// the empty string, which blanked the title and lost the row (Mac report 2026-09-09_20-38-22).
    /// </summary>
    [TestClass]
    public class TaskNameCommitTests
    {
        private static readonly string[] Known = { "Focus Bear", "class" };

        private static ParsedQuickAddTask Saved(TaskNameCommit commit)
        {
            Assert.AreEqual(TaskNameCommitKind.Save, commit.Kind, "expected Save, got " + commit);
            return commit.Task;
        }

        // MARK: A name made only of tokens is refused

        [TestMethod]
        public void AcceptedLeadingProjectKeepsAnEmptyNameAsAnUnfinishedTask()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("", new[] { "Focus Bear" }, "Focus Bear"));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("   ", new[] { "Focus Bear" }, "Focus Bear"));
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForDayPlanRow("", new[] { "Focus Bear" }));
            Assert.AreEqual("Write report", Saved(TaskNameCommit.ForDayPlanRow("Write report", new[] { "Focus Bear" }, "Focus Bear")).Title);
        }

        [TestMethod]
        public void BareProjectTokenIsRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("#class", Known));
        }

        [TestMethod]
        public void BareUnknownProjectTokenIsRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("#gardening", Known));
        }

        [TestMethod]
        public void BareMultiWordProjectTokenIsRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("#Focus Bear", Known));
        }

        [TestMethod]
        public void BarePriorityTokenIsRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("!3", Known));
        }

        /// <summary><c>*N</c> is a DURATION since 2026-09-21, and a duration on its own still leaves nothing to call the task.</summary>
        [TestMethod]
        public void BareMinutesTokenIsRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("*15", Known));
        }

        [TestMethod]
        public void SeveralTokensAndNothingElseAreRefused()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("!3 #class", Known));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("30m !3 #class", Known));
        }

        [TestMethod]
        public void SurroundingWhitespaceDoesNotRescueATokenOnlyName()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("   #class   ", Known));
        }

        // MARK: Duration — a token in the quick-add grammar, plain text on a Day Plan row

        [TestMethod]
        public void BareDurationTokenIsRefusedInQuickAdd()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("30m", Known));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("1h30", Known));
        }

        [TestMethod]
        public void BareDurationTokenIsANameOnADayPlanRow()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("30m", Known));
            Assert.AreEqual("30m", parsed.Title);
            Assert.IsNull(parsed.DurationMinutes);
        }

        // MARK: A token plus a real title still saves, with the token stripped

        [TestMethod]
        public void ProjectTokenWithTitleSaves()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("draft the brief #Focus Bear", Known));
            Assert.AreEqual("draft the brief", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
        }

        [TestMethod]
        public void ProjectTokenReusesTheKnownProjectCasing()
        {
            Assert.AreEqual("Focus Bear", Saved(TaskNameCommit.ForDayPlanRow("read chapter 4 #focus bear", Known)).Project);
        }

        [TestMethod]
        public void PriorityTokenWithTitleSaves()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("draft report !3", Known));
            Assert.AreEqual("draft report", parsed.Title);
            Assert.IsNotNull(parsed.Outcome);
        }

        [TestMethod]
        public void StarDurationWithTitleSavesOnTheRow()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("draft report *25", Known));
            Assert.AreEqual("draft report", parsed.Title);
            Assert.AreEqual(25, parsed.DurationMinutes);
            Assert.IsNull(parsed.Outcome, "a star is a duration now, not a priority");
        }

        [TestMethod]
        public void EveryTokenWithTitleSavesInQuickAdd()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForQuickAdd("write the essay #class !3 45m", Known));
            Assert.AreEqual("write the essay", parsed.Title);
            Assert.AreEqual("class", parsed.Project);
            Assert.AreEqual(45, parsed.DurationMinutes);
            Assert.IsNotNull(parsed.Outcome);
        }

        [TestMethod]
        public void PlainTitleSaves()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("tidy the inbox", Known));
            Assert.AreEqual("tidy the inbox", parsed.Title);
            Assert.IsNull(parsed.Project);
            Assert.IsNull(parsed.Outcome);
        }

        /// <summary>A lone <c>#</c> isn't a project token, so it is just an odd little name — not a refusal.</summary>
        [TestMethod]
        public void StrayHashIsATitle()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("#", Known));
            Assert.AreEqual("#", parsed.Title);
            Assert.IsNull(parsed.Project);
        }

        // MARK: A genuinely empty name still discards

        [TestMethod]
        public void EmptyStringDiscards()
        {
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForDayPlanRow("", Known));
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForQuickAdd("", Known));
        }

        [TestMethod]
        public void WhitespaceOnlyDiscards()
        {
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForDayPlanRow("   \n  ", Known));
        }

        // MARK: The row composer carries the duration too (Mac report 2026-09-21_20-39-15)

        [TestMethod]
        public void TheReportedLineParsesEveryToken()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("Denzel attend MCP meeting !5 *2", new[] { "HR" }));
            Assert.AreEqual("Denzel attend MCP meeting", parsed.Title, "both tokens stripped from the name");
            Assert.AreEqual(2, parsed.DurationMinutes, "*2 is two minutes");
            Assert.AreEqual(10, parsed.Outcome, "!5 on the /10 wire scale");
        }

        [TestMethod]
        public void TheStarTokenSurvivesWhateverElseIsOnTheLine()
        {
            var cases = new[]
            {
                new { Line = "write the spec *45", Minutes = 45 },
                new { Line = "write the spec *45 !3", Minutes = 45 },
                new { Line = "*90 write the spec", Minutes = 90 },
                new { Line = "write the spec #HR *5", Minutes = 5 },
            };
            foreach (var c in cases)
                Assert.AreEqual(c.Minutes, Saved(TaskNameCommit.ForDayPlanRow(c.Line, new[] { "HR" })).DurationMinutes, c.Line);
        }

        [TestMethod]
        public void TheProjectBeforeAStarTokenIsNotSwallowedIntoIt()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("write the spec #HR *5", new[] { "HR" }));
            Assert.AreEqual("HR", parsed.Project);
            Assert.AreEqual("write the spec", parsed.Title);
        }

        [TestMethod]
        public void ALineWithNoStarTokenLeavesTheDurationToTheCaller()
        {
            Assert.IsNull(Saved(TaskNameCommit.ForDayPlanRow("write the spec !2", new string[0])).DurationMinutes,
                "null, not a default — a default here would overwrite a duration the user already set");
        }

        [TestMethod]
        public void ASpelledDurationIsStillPartOfTheNameOnARow()
        {
            ParsedQuickAddTask parsed = Saved(TaskNameCommit.ForDayPlanRow("run 30m sprint", new string[0]));
            Assert.AreEqual("run 30m sprint", parsed.Title);
            Assert.IsNull(parsed.DurationMinutes);
        }

        // MARK: Leading `#` + Tab (Mac report 2026-09-09_20-38-22 follow-up: "#v2 then Tab, the task vanishes")

        /// <summary>
        /// Accepting a suggestion for a line that is ONLY the token leaves nothing to call the task. The
        /// accepted line must still be refused (not saved blank), and once the project is held as pending
        /// an emptied editor must not discard the row either.
        /// </summary>
        [TestMethod]
        public void AcceptingALeadingProjectThenBlurringKeepsTheRow()
        {
            string[] projects = { "Codex Autocomplete Regression" };
            string accepted = QuickAddProjectSuggestions.Accepting("#Codex Autocomplete", projects, 0);
            Assert.AreEqual("#Codex Autocomplete Regression", accepted);
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow(accepted, projects));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("", projects, "Codex Autocomplete Regression"));
            ParsedQuickAddTask finished = Saved(TaskNameCommit.ForDayPlanRow("Write the test", projects, "Codex Autocomplete Regression"));
            Assert.AreEqual("Write the test", finished.Title);
            Assert.IsNull(finished.Project, "the pending project is already on the task; the name carries none");
        }
    }
}
