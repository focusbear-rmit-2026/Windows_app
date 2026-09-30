using System.Collections.Generic;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Grammar tests for the one-line quick-add composer, ported case-for-case from the Mac's
    /// <c>FocusBearTests/QuickAddTaskParserTests.swift</c>. A port that passed different cases would
    /// prove nothing about parity, so the cases are the Mac's even where a C# author would pick others.
    /// </summary>
    [TestClass]
    public class QuickAddTaskParserTests
    {
        private static readonly string[] Projects = { "product", "Product marketing", "admin", "Geelong Trees" };

        // MARK: Plain titles

        [TestMethod]
        public void PlainTitleHasNoTokens()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("write the sprint report");

            Assert.AreEqual("write the sprint report", parsed.Title);
            Assert.IsNull(parsed.Project);
            Assert.IsNull(parsed.DurationMinutes);
            Assert.IsNull(parsed.Outcome);
            Assert.IsFalse(parsed.IsEmpty);
        }

        [TestMethod]
        public void BlankInputIsEmpty()
        {
            Assert.IsTrue(QuickAddTaskParser.Parse("   ").IsEmpty);
            Assert.IsTrue(QuickAddTaskParser.Parse("").IsEmpty);
        }

        [TestMethod]
        public void TitleIsTrimmed()
        {
            Assert.AreEqual("tidy inbox", QuickAddTaskParser.Parse("  tidy inbox  ").Title);
        }

        // MARK: `#project`

        [TestMethod]
        public void TrailingMultiWordProject()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("draft the brief #Focus Bear");

            Assert.AreEqual("draft the brief", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
        }

        [TestMethod]
        public void UnderscoreProjectKeepsTrailingTitle()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("review notes #Focus_Bear then ship");

            Assert.AreEqual("review notes then ship", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
        }

        [TestMethod]
        public void KnownProjectCasingIsReused()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("ping the team #focus bear", new[] { "Focus Bear" });

            Assert.AreEqual("Focus Bear", parsed.Project);
        }

        [TestMethod]
        public void StrayHashIsNotAProject()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("buy milk #");

            Assert.AreEqual("buy milk #", parsed.Title);
            Assert.IsNull(parsed.Project);
        }

        // MARK: Duration forms

        [TestMethod]
        public void MinutesForms()
        {
            Assert.AreEqual(30, QuickAddTaskParser.ExtractDurationToken("standup 30m").Minutes);
            Assert.AreEqual(45, QuickAddTaskParser.ExtractDurationToken("standup 45min").Minutes);
            Assert.AreEqual(45, QuickAddTaskParser.ExtractDurationToken("standup 45mins").Minutes);
            Assert.AreEqual(90, QuickAddTaskParser.ExtractDurationToken("standup 90 m").Minutes);
        }

        [TestMethod]
        public void HourForms()
        {
            Assert.AreEqual(60, QuickAddTaskParser.ExtractDurationToken("deep work 1h").Minutes);
            Assert.AreEqual(120, QuickAddTaskParser.ExtractDurationToken("deep work 2hr").Minutes);
            Assert.AreEqual(120, QuickAddTaskParser.ExtractDurationToken("deep work 2hrs").Minutes);
        }

        [TestMethod]
        public void CombinedHourMinuteForms()
        {
            Assert.AreEqual(90, QuickAddTaskParser.ExtractDurationToken("plan 1h30").Minutes);
            Assert.AreEqual(90, QuickAddTaskParser.ExtractDurationToken("plan 1h30m").Minutes);
            Assert.AreEqual(90, QuickAddTaskParser.ExtractDurationToken("plan 1H30M").Minutes);
        }

        [TestMethod]
        public void DurationIsStrippedFromTitle()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("book the venue 1h30");

            Assert.AreEqual("book the venue", parsed.Title);
            Assert.AreEqual(90, parsed.DurationMinutes);
        }

        [TestMethod]
        public void LastDurationTokenWins()
        {
            Assert.AreEqual(60, QuickAddTaskParser.Parse("swim 30m then run 1h").DurationMinutes);
        }

        [TestMethod]
        public void DurationIsClampedToADay()
        {
            Assert.AreEqual(QuickAddTaskParser.MaxDurationMinutes, QuickAddTaskParser.Parse("marathon 99h").DurationMinutes);
            Assert.AreEqual(QuickAddTaskParser.MaxDurationMinutes, QuickAddTaskParser.Parse("marathon 5000m").DurationMinutes);
        }

        [TestMethod]
        public void ZeroDurationIsNotADuration()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("stretch 0m");

            Assert.IsNull(parsed.DurationMinutes);
            Assert.AreEqual("stretch 0m", parsed.Title);
        }

        [TestMethod]
        public void BareNumberIsNotADuration()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("read chapter 3");

            Assert.IsNull(parsed.DurationMinutes);
            Assert.AreEqual("read chapter 3", parsed.Title);
        }

        [TestMethod]
        public void DurationMustStandAlone()
        {
            // "30m" inside a word — no whitespace boundary before the digits — is not a token.
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("email tim30m");

            Assert.IsNull(parsed.DurationMinutes);
            Assert.AreEqual("email tim30m", parsed.Title);
        }

        // MARK: Combinations

        [TestMethod]
        public void ProjectAndDurationTogetherTrailingDuration()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("write the spec #Focus_Bear 45m");

            Assert.AreEqual("write the spec", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
            Assert.AreEqual(45, parsed.DurationMinutes);
        }

        /// <summary>
        /// The duration must be stripped BEFORE the project, or the "# to end of line" convention
        /// would swallow "Focus Bear 45m" as the project name.
        /// </summary>
        [TestMethod]
        public void DurationAfterMultiWordProject()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("write the spec #Focus Bear 45m");

            Assert.AreEqual("write the spec", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
            Assert.AreEqual(45, parsed.DurationMinutes);
        }

        [TestMethod]
        public void DurationBeforeProject()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("write the spec 45m #Focus Bear");

            Assert.AreEqual("write the spec", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
            Assert.AreEqual(45, parsed.DurationMinutes);
        }

        [TestMethod]
        public void ProjectDurationAndPriority()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("ship release !4 2h #Focus Bear");

            Assert.AreEqual("ship release", parsed.Title);
            Assert.AreEqual("Focus Bear", parsed.Project);
            Assert.AreEqual(120, parsed.DurationMinutes);
            Assert.AreEqual(8, parsed.Outcome, "the displayed 1-5 importance doubles onto the server's 1-10 scale");
        }

        // MARK: The 2026-09-21 token change (Mac report 2026-09-21_07-42-02)

        /// <summary>
        /// <c>*</c> used to mean priority and now means minutes. <c>*5</c> therefore has to read as five
        /// minutes, not priority 5 — that reversal is the whole point of the report.
        /// </summary>
        [TestMethod]
        public void StarIsNowADurationAndBangIsPriority()
        {
            ParsedQuickAddTask star = QuickAddTaskParser.Parse("post focus button *5");
            Assert.AreEqual("post focus button", star.Title);
            Assert.AreEqual(5, star.DurationMinutes);
            Assert.IsNull(star.Outcome);

            ParsedQuickAddTask bang = QuickAddTaskParser.Parse("post focus button !5");
            Assert.AreEqual("post focus button", bang.Title);
            Assert.AreEqual(10, bang.Outcome);
            Assert.IsNull(bang.DurationMinutes);
        }

        [TestMethod]
        public void StarDurationTakesMoreThanOneDigit()
        {
            Assert.AreEqual(90, QuickAddTaskParser.Parse("write spec *90").DurationMinutes);
            Assert.AreEqual(15, QuickAddTaskParser.Parse("write spec *15").DurationMinutes);
        }

        /// <summary>The star form is what people reach for on a Day Plan row, where a spelled "30m" stays in the name.</summary>
        [TestMethod]
        public void TheDayPlanRowTakesAStarDurationButNotASpelledOne()
        {
            TaskNameCommit starred = TaskNameCommit.ForDayPlanRow("post focus button *5", new string[0]);
            Assert.AreEqual(TaskNameCommitKind.Save, starred.Kind);
            Assert.AreEqual("post focus button", starred.Task.Title);
            Assert.AreEqual(5, starred.Task.DurationMinutes);

            TaskNameCommit spelled = TaskNameCommit.ForDayPlanRow("run 30m sprint", new string[0]);
            Assert.AreEqual(TaskNameCommitKind.Save, spelled.Kind);
            Assert.AreEqual("run 30m sprint", spelled.Task.Title, "a spelled duration is prose on this row");
            Assert.IsNull(spelled.Task.DurationMinutes);
        }

        [TestMethod]
        public void TheDayPlanRowStillTakesPriorityAndProject()
        {
            TaskNameCommit commit = TaskNameCommit.ForDayPlanRow("draft memo !3 #Focus Bear", new[] { "Focus Bear" });
            Assert.AreEqual(TaskNameCommitKind.Save, commit.Kind);
            Assert.AreEqual("draft memo", commit.Task.Title);
            Assert.AreEqual(6, commit.Task.Outcome);
            Assert.AreEqual("Focus Bear", commit.Task.Project);
        }

        /// <summary>A star with no digits is not a duration, so it stays in the name rather than vanishing.</summary>
        [TestMethod]
        public void ABareStarIsStillJustText()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("rate it ***");
            Assert.AreEqual("rate it ***", parsed.Title);
            Assert.IsNull(parsed.DurationMinutes);
        }

        /// <summary>The Windows report 2026-09-25_20-13-11: "Start outreach bot !3" was saved literally.</summary>
        [TestMethod]
        public void TheWindowsReportedLineSetsItsImportance()
        {
            TaskNameCommit commit = TaskNameCommit.ForDayPlanRow("Start outreach bot !3", new[] { "sales" });
            Assert.AreEqual(TaskNameCommitKind.Save, commit.Kind);
            Assert.AreEqual("Start outreach bot", commit.Task.Title);
            Assert.AreEqual(6, commit.Task.Outcome);
            Assert.IsNull(commit.Task.Project);
            Assert.IsNull(commit.Task.DurationMinutes);
        }

        [TestMethod]
        public void OnlyTokensIsEmpty()
        {
            Assert.IsTrue(QuickAddTaskParser.Parse("#Focus Bear").IsEmpty);
            Assert.IsTrue(QuickAddTaskParser.Parse("30m").IsEmpty);
        }

        [TestMethod]
        public void JunkIsKeptAsATitle()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("!!! ??? ***");

            Assert.AreEqual("!!! ??? ***", parsed.Title);
            Assert.IsNull(parsed.Project);
            Assert.IsNull(parsed.DurationMinutes);
            Assert.IsNull(parsed.Outcome);
        }

        // MARK: Duration label

        [TestMethod]
        public void DurationLabel()
        {
            Assert.AreEqual("45m", QuickAddTaskParser.DurationLabel(45));
            Assert.AreEqual("2h", QuickAddTaskParser.DurationLabel(120));
            Assert.AreEqual("1h 30m", QuickAddTaskParser.DurationLabel(90));
        }

        // MARK: `#project` suggestions (Mac report 2026-09-14_16-17-22)

        [TestMethod]
        public void TheTokenBeingTypedIsEverythingAfterTheLastHash()
        {
            Assert.AreEqual("prod", QuickAddProjectSuggestions.ActiveToken("write up #prod"));
            Assert.AreEqual("life admin", QuickAddProjectSuggestions.ActiveToken("write up #life admin"),
                            "A multi-word project runs to the end of the line, as the parser reads it");
            Assert.IsNull(QuickAddProjectSuggestions.ActiveToken("no token here"));
        }

        [TestMethod]
        public void NamesStartingWithTheTokenComeBeforeNamesMerelyContainingIt()
        {
            CollectionAssert.AreEqual(new[] { "product", "Product marketing" },
                                      (System.Collections.ICollection)QuickAddProjectSuggestions.Matches("task #pro", Projects));
            CollectionAssert.AreEqual(new[] { "Product marketing" },
                                      (System.Collections.ICollection)QuickAddProjectSuggestions.Matches("task #marketing", Projects));
        }

        [TestMethod]
        public void ABareHashOffersTheProjectsYouHave()
        {
            CollectionAssert.AreEqual(new[] { "product", "Product marketing" },
                                      (System.Collections.ICollection)QuickAddProjectSuggestions.Matches("task #", Projects, 2));
        }

        [TestMethod]
        public void AnExactMatchNeedsNoSuggesting()
        {
            Assert.AreEqual(0, QuickAddProjectSuggestions.Matches("task #admin", Projects).Count);
            Assert.AreEqual(0, QuickAddProjectSuggestions.Matches("task #ADMIN", Projects).Count);
        }

        [TestMethod]
        public void NoSuggestionsWithoutAToken()
        {
            Assert.AreEqual(0, QuickAddProjectSuggestions.Matches("just a task", Projects).Count);
        }

        [TestMethod]
        public void AcceptingASuggestionReplacesOnlyTheHalfTypedToken()
        {
            Assert.AreEqual("email Tim 30m #Geelong Trees",
                            QuickAddProjectSuggestions.Applying("Geelong Trees", "email Tim 30m #gee"));
            Assert.AreEqual("no hash", QuickAddProjectSuggestions.Applying("admin", "no hash"));
        }

        [TestMethod]
        public void AnAcceptedSuggestionParsesBackToThatProject()
        {
            string line = QuickAddProjectSuggestions.Applying("Geelong Trees", "email Tim 30m #gee");
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse(line, Projects);

            Assert.AreEqual("Geelong Trees", parsed.Project);
            Assert.AreEqual("email Tim", parsed.Title);
            Assert.AreEqual(30, parsed.DurationMinutes);
        }

        // MARK: Tab accepts the highlighted suggestion (Mac report 2026-09-15_12-24-31)

        [TestMethod]
        public void TabAcceptsWhicheverSuggestionIsHighlighted()
        {
            Assert.AreEqual("task #product", QuickAddProjectSuggestions.Accepting("task #pro", Projects, 0));
            Assert.AreEqual("task #Product marketing", QuickAddProjectSuggestions.Accepting("task #pro", Projects, 1),
                            "Tab accepts the row the arrow keys moved the highlight to, not always the first");
        }

        /// <summary>
        /// The guard that keeps Tab from becoming a worse bug than the one it fixes: with nothing to
        /// accept the decision is null, the field's Tab handler returns false, and focus moves as usual.
        /// </summary>
        [TestMethod]
        public void ThereIsNothingToAcceptWithoutAMatchingSuggestion()
        {
            Assert.IsNull(QuickAddProjectSuggestions.Accepting("just a task", Projects, 0), "no '#' token at all");
            Assert.IsNull(QuickAddProjectSuggestions.Accepting("task #zzz", Projects, 0), "a token that matches no project");
            Assert.IsNull(QuickAddProjectSuggestions.Accepting("task #admin", Projects, 0),
                          "an exact match is already the project; there is nothing left to accept");
            Assert.IsNull(QuickAddProjectSuggestions.Accepting("task #pro", Projects, 9),
                          "a highlight past the end of the list accepts nothing rather than throwing");
            Assert.IsNull(QuickAddProjectSuggestions.Accepting("task #pro", Projects, -1));
        }

        /// <summary>
        /// Clicking a row and pressing Tab on that same row are the same act, so they go through one
        /// function. This pins that: accepting row N is exactly the list's Nth match applied to the line.
        /// </summary>
        [TestMethod]
        public void ClickingARowAndTabbingOntoItAcceptTheSameThing()
        {
            const string line = "BAS statements #pro";
            IList<string> matches = QuickAddProjectSuggestions.Matches(line, Projects);

            Assert.AreNotEqual(0, matches.Count);
            for (int index = 0; index < matches.Count; index++)
            {
                Assert.AreEqual(QuickAddProjectSuggestions.Applying(matches[index], line),
                                QuickAddProjectSuggestions.Accepting(line, Projects, index),
                                "Tab and a click must accept the same suggestion the same way");
            }
        }

        /// <summary>
        /// The data-loss guard: accepting a project must not turn a token-only line into a task with a
        /// blank title. It only rewrites the token — the line stays empty, so Enter still refuses it.
        /// </summary>
        [TestMethod]
        public void AcceptingOnATokenOnlyLineStillLeavesNothingWorthAdding()
        {
            string accepted = QuickAddProjectSuggestions.Accepting("#pro", Projects, 0);
            Assert.AreEqual("#product", accepted);

            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse(accepted, Projects);
            Assert.AreEqual("product", parsed.Project);
            Assert.IsTrue(parsed.IsEmpty, "a line that is still only a project token must never be added");
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd(accepted, Projects));
        }

        [TestMethod]
        public void AcceptingKeepsTheRestOfTheLineIntact()
        {
            string accepted = QuickAddProjectSuggestions.Accepting("email Tim 30m #gee", Projects, 0);
            Assert.AreEqual("email Tim 30m #Geelong Trees", accepted);

            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse(accepted, Projects);
            Assert.AreEqual("email Tim", parsed.Title);
            Assert.AreEqual(30, parsed.DurationMinutes);
            Assert.IsFalse(parsed.IsEmpty);
        }

        // MARK: Duration fields (Mac report 2026-07-15_20-25-27)

        /// <summary>The reported bug: the duration fields stripped non-digits, so "4h" became 4 minutes.</summary>
        [TestMethod]
        public void HoursInADurationFieldAreNotReadAsMinutes()
        {
            Assert.AreEqual(240, QuickAddTaskParser.DurationMinutesFromFieldText("4h"));
            Assert.AreEqual(90, QuickAddTaskParser.DurationMinutesFromFieldText("1h30"));
            Assert.AreEqual(120, QuickAddTaskParser.DurationMinutesFromFieldText("2 hrs"));
        }

        /// <summary>A bare number is the commonest thing typed into a duration field and still means minutes.</summary>
        [TestMethod]
        public void BareNumberIsMinutes()
        {
            Assert.AreEqual(30, QuickAddTaskParser.DurationMinutesFromFieldText("30"));
            Assert.AreEqual(90, QuickAddTaskParser.DurationMinutesFromFieldText("90"));
            Assert.AreEqual(45, QuickAddTaskParser.DurationMinutesFromFieldText(" 45 "));
        }

        [TestMethod]
        public void ExplicitMinuteSuffixesStillWork()
        {
            Assert.AreEqual(30, QuickAddTaskParser.DurationMinutesFromFieldText("30m"));
            Assert.AreEqual(45, QuickAddTaskParser.DurationMinutesFromFieldText("45min"));
            Assert.AreEqual(90, QuickAddTaskParser.DurationMinutesFromFieldText("90 mins"));
        }

        [TestMethod]
        public void EmptyOrZeroIsNoDuration()
        {
            Assert.IsNull(QuickAddTaskParser.DurationMinutesFromFieldText(""));
            Assert.IsNull(QuickAddTaskParser.DurationMinutesFromFieldText("   "));
            Assert.IsNull(QuickAddTaskParser.DurationMinutesFromFieldText("0"));
            Assert.IsNull(QuickAddTaskParser.DurationMinutesFromFieldText("abc"));
        }

        [TestMethod]
        public void DurationFieldIsClampedToADay()
        {
            Assert.AreEqual(QuickAddTaskParser.MaxDurationMinutes, QuickAddTaskParser.DurationMinutesFromFieldText("99h"));
            Assert.AreEqual(QuickAddTaskParser.MaxDurationMinutes, QuickAddTaskParser.DurationMinutesFromFieldText("5000"));
        }

        // MARK: Committing a typed name (Mac report 2026-09-09_20-38-22)

        [TestMethod]
        public void NothingTypedDiscardsTheRow()
        {
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForQuickAdd("   ", Projects));
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForDayPlanRow("", Projects));
        }

        /// <summary>
        /// The reported data loss: a line of only tokens is an unfinished name, not an empty one. Read
        /// as empty it blanked the title, the next save PUT the blank, and the row was pruned.
        /// </summary>
        [TestMethod]
        public void ALineOfOnlyTokensIsRefusedRatherThanSavedBlank()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("#class", Projects));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("!3", Projects));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForQuickAdd("30m", Projects));
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("#class", Projects));
        }

        /// <summary>
        /// A Day Plan row has its own duration field, so "30m" there is part of the name — the one
        /// place the two composers deliberately disagree.
        /// </summary>
        [TestMethod]
        public void ADayPlanRowKeepsADurationInItsName()
        {
            TaskNameCommit commit = TaskNameCommit.ForDayPlanRow("30m", Projects);

            Assert.AreEqual(TaskNameCommitKind.Save, commit.Kind);
            Assert.AreEqual("30m", commit.Task.Title);
            Assert.IsNull(commit.Task.DurationMinutes, "the row's duration comes from its own field, never the name");
        }

        /// <summary>
        /// Accepting a project leaves the name editor empty, but the task is unfinished rather than
        /// abandoned — a blur must not read it as deleting the row.
        /// </summary>
        [TestMethod]
        public void APendingProjectHoldsAnEmptyDayPlanRowOpen()
        {
            Assert.AreEqual(TaskNameCommit.RefuseTokensOnly, TaskNameCommit.ForDayPlanRow("", Projects, "admin"));
            Assert.AreEqual(TaskNameCommit.Discard, TaskNameCommit.ForDayPlanRow("", Projects),
                            "without one, an empty row is still discarded");
        }

        [TestMethod]
        public void ARealNameSavesWithItsTokensStripped()
        {
            TaskNameCommit commit = TaskNameCommit.ForQuickAdd("ship release !4 2h #Focus Bear", Projects);

            Assert.AreEqual(TaskNameCommitKind.Save, commit.Kind);
            Assert.AreEqual("ship release", commit.Task.Title);
            Assert.AreEqual("Focus Bear", commit.Task.Project);
            Assert.AreEqual(120, commit.Task.DurationMinutes);
            Assert.AreEqual(8, commit.Task.Outcome);
        }
    }
}
