using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// What the native Stats screen needed beyond the stats math Core already had. The pep-talk, cache,
    /// forgiven-days and weekday cases are ported from <c>StatsLogicTests.swift</c>, the markdown cases from
    /// <c>MarkdownTextTests.swift</c>; the productivity, focus-log, level and wire cases pin the Mac's
    /// <c>StatsMath.swift</c> behaviour, which has no suite of its own for those parts.
    /// </summary>
    [TestClass]
    public class StatsScreenPortTests
    {
        [TestMethod]
        public void Readers_TolerateNullNestedObjects()
        {
            // "metadata": null / "focus_mode": null (a deleted focus mode) threw on indexing and emptied the list.
            var log = StatsWire.ReadFocusLog("[{\"id\":\"e1\",\"metadata\":null,\"focus_mode\":null}]");
            Assert.AreEqual(1, log.Count);
            Assert.IsFalse(log[0].IsManual);
            Assert.AreEqual("", log[0].FocusModeName);
            var sessions = StatsWire.ReadFocusSessions("[{\"name\":\"Deep work\",\"metadata\":null}]");
            Assert.AreEqual(1, sessions.Count);
            Assert.AreEqual(1, FocusBear.Core.Notes.NotesWire.ReadPage("{\"data\":[],\"meta\":null}") == null ? 0 : 1);
        }

        // ------------------------------------------------------------------ pep talk stream

        private static List<PepTalkEvent> Feed(PepTalkStreamDecoder decoder, byte[] bytes) => decoder.Consume(bytes).ToList();

        [TestMethod]
        public void PepTalkDecoderPreservesUnicodeAcrossEveryPacketBoundaryAndHidesPrompt()
        {
            byte[] payload = Encoding.UTF8.GetBytes("data: Ahoy \U0001F3F4‍☠️, ánimo!\r\n\r\ndata: [DONE]\r\n\r\ndata: PROMPT: private\n\n");
            for (int boundary = 0; boundary <= payload.Length; boundary++)
            {
                var decoder = new PepTalkStreamDecoder();
                var events = Feed(decoder, payload.Take(boundary).ToArray()).Concat(Feed(decoder, payload.Skip(boundary).ToArray())).ToList();
                CollectionAssert.AreEqual(new[] { PepTalkEvent.Chunk("Ahoy \U0001F3F4‍☠️, ánimo!"), PepTalkEvent.Done }, events, "boundary " + boundary);
                Assert.AreEqual(0, Feed(decoder, Encoding.UTF8.GetBytes("data: hidden\n")).Count, "nothing after [DONE] reaches the UI");
            }
        }

        [TestMethod]
        public void PepTalkDecoderReportsFatalAndMalformedStreamsWithoutPublishingBodies()
        {
            CollectionAssert.AreEqual(new[] { PepTalkEvent.Fatal }, Feed(new PepTalkStreamDecoder(), Encoding.UTF8.GetBytes("event: fatal_error\ndata: private server error\n")));
            CollectionAssert.AreEqual(new[] { PepTalkEvent.Invalid }, Feed(new PepTalkStreamDecoder(), new byte[] { 100, 97, 116, 97, 58, 32, 255, 10 }));
            CollectionAssert.AreEqual(new[] { PepTalkEvent.Invalid }, Feed(new PepTalkStreamDecoder(), Enumerable.Repeat((byte)65, 65537).ToArray()));
        }

        [TestMethod]
        public void PepTalkDecoderKeepsSentenceSpacesAndWaitsForWholeLines()
        {
            var decoder = new PepTalkStreamDecoder();
            Assert.AreEqual(0, Feed(decoder, Encoding.UTF8.GetBytes("data: Hello")).Count);
            CollectionAssert.AreEqual(new[] { PepTalkEvent.Chunk("Hello"), PepTalkEvent.Chunk(" world"), PepTalkEvent.Done },
                Feed(decoder, Encoding.UTF8.GetBytes("\ndata:  world\n\ndata: [DONE]\n")));
        }

        // ------------------------------------------------------------------ why a pep talk failed

        [TestMethod]
        public void ARateLimitIsToldApartFromEveryOtherFailure()
        {
            Assert.AreEqual(PepTalkFailure.RateLimited(null), PepTalkFailure.From(429, null));
            Assert.AreEqual(PepTalkFailure.ServerError, PepTalkFailure.From(500, null));
            Assert.AreEqual(PepTalkFailure.ServerError, PepTalkFailure.From(503, null));
            Assert.AreEqual(PepTalkFailure.Connection, PepTalkFailure.From(0, null));
            Assert.AreEqual(PepTalkFailure.Other, PepTalkFailure.From(404, null));
        }

        [TestMethod]
        public void TheServersOwnRetryAfterIsHonoured()
        {
            Assert.AreEqual(PepTalkFailure.RateLimited(TimeSpan.FromSeconds(30)), PepTalkFailure.From(429, "30"));
            Assert.AreEqual(PepTalkFailure.RateLimited(TimeSpan.FromSeconds(45)), PepTalkFailure.From(429, "  45 "), "headers arrive padded");
        }

        [TestMethod]
        public void AnUnusableRetryAfterFallsBackRatherThanGuessing()
        {
            foreach (var header in new[] { "Wed, 21 Oct 2026 07:28:00 GMT", "soon", "", "-5", "0" })
                Assert.AreEqual(PepTalkFailure.RateLimited(null), PepTalkFailure.From(429, header), header);
        }

        [TestMethod]
        public void AnAbsurdRetryAfterIsCapped()
        {
            Assert.AreEqual(PepTalkFailure.RateLimited(PepTalkFailure.MaximumHonouredWait), PepTalkFailure.From(429, "86400"));
        }

        [TestMethod]
        public void OnlyARateLimitHoldsTheRetryButton()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(30), PepTalkFailure.RateLimited(TimeSpan.FromSeconds(30)).RetryDelay);
            Assert.AreEqual(PepTalkFailure.DefaultRateLimitWait, PepTalkFailure.RateLimited(null).RetryDelay, "a 429 with no advice still waits");
            Assert.IsNull(PepTalkFailure.ServerError.RetryDelay);
            Assert.IsNull(PepTalkFailure.Connection.RetryDelay);
            Assert.IsNull(PepTalkFailure.Other.RetryDelay);
        }

        // ------------------------------------------------------------------ not spending the daily budget

        [TestMethod]
        public void ARecentPepTalkIsWorthShowingAgainAndAnOldOneIsNot()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            Assert.IsTrue(PepTalkCache.IsFresh(now.AddSeconds(-60), now));
            Assert.IsTrue(PepTalkCache.IsFresh(now - PepTalkCache.ReuseWindow + TimeSpan.FromSeconds(1), now));
            Assert.IsFalse(PepTalkCache.IsFresh(now - PepTalkCache.ReuseWindow, now), "the window is exclusive at its own edge");
            Assert.IsFalse(PepTalkCache.IsFresh(now.AddDays(-1), now));
            Assert.IsFalse(PepTalkCache.IsFresh(null, now), "nothing cached yet");
        }

        [TestMethod]
        public void AFutureTimestampIsTreatedAsStale()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            Assert.IsFalse(PepTalkCache.IsFresh(now.AddSeconds(60), now));
        }

        [TestMethod]
        public void TheCacheReusesTextWithItsToneInTheSameLanguageOnly()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            PepTalkCache.Clear();
            try
            {
                PepTalkCache.Store("go get em", "pirate", "en", now);
                var reusable = PepTalkCache.Reusable("en", now.AddSeconds(60));
                Assert.AreEqual("go get em", reusable?.Text);
                Assert.AreEqual("pirate", reusable?.Tone, "the tone must travel with the text, or the chips lie");
                Assert.IsNull(PepTalkCache.Reusable("es", now.AddSeconds(60)), "English for a Spanish session is worse than another request");
                Assert.IsNull(PepTalkCache.Reusable("en", now + PepTalkCache.ReuseWindow + TimeSpan.FromSeconds(1)));
            }
            finally { PepTalkCache.Clear(); }
        }

        [TestMethod]
        public void AFailedStreamDoesNotDestroyAGoodCachedMessage()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            PepTalkCache.Clear();
            try
            {
                PepTalkCache.Store("", "pirate", "en", now);
                Assert.IsNull(PepTalkCache.Reusable("en", now), "an empty message is never cached");
                PepTalkCache.Store("go get em", "pirate", "en", now);
                PepTalkCache.Store("", "scientist", "en", now.AddSeconds(1));
                var reusable = PepTalkCache.Reusable("en", now.AddSeconds(2));
                Assert.AreEqual("go get em", reusable?.Text);
                Assert.AreEqual("pirate", reusable?.Tone);
            }
            finally { PepTalkCache.Clear(); }
        }

        // ------------------------------------------------------------------ forgiven days

        private static LeaderboardUser User(double? focusDone = null, double? focusDays = null, double? window = 90,
                                            double? morningDone = null, double? morningDays = null, double? morningPercent = null,
                                            double? microDone = null, double? microDays = null, double? microPercent = null) =>
            new LeaderboardUser
            {
                Id = "u", Username = "u", FocusModesStreak = 149,
                FocusModesNumberDaysCompleted = focusDone, FocusModesNumDaysOfStats = focusDays,
                MorningNumberDaysCompleted = morningDone, MorningNumDaysOfStats = morningDays, MorningPercentNumberDayOfStatsCompleted = morningPercent,
                MicroBreaksNumberDaysCompleted = microDone, MicroBreaksNumDaysOfStats = microDays, MicroPercentNumberDayOfStatsCompleted = microPercent,
                NumDaysOfStats = window,
            };

        [TestMethod]
        public void ForgivenDaysAreTheGapBetweenTheMeasuredDaysAndTheWindow()
        {
            var metric = LeaderboardSorting.Metric(User(focusDone: 84, focusDays: 84), LeaderboardSortKey.FocusMode);
            Assert.AreEqual(100, metric.Percentage, "84 of 84 measured days is 100%, not 84/90 = 93%");
            Assert.AreEqual(84, metric.WindowDays);
            Assert.AreEqual(90, metric.WindowSpanDays);
            Assert.AreEqual(6, metric.ForgivenDays);
            Assert.IsTrue(metric.HasForgivenDays);
        }

        [TestMethod]
        public void NothingIsForgivenWhenEveryWindowDayWasMeasured()
        {
            var metric = LeaderboardSorting.Metric(User(morningDone: 88, morningDays: 90, morningPercent: 98), LeaderboardSortKey.Morning);
            Assert.AreEqual(0, metric.ForgivenDays);
            Assert.IsFalse(metric.HasForgivenDays);
        }

        [TestMethod]
        public void BreaksForgiveDaysTheSameWayFocusDoes()
        {
            var metric = LeaderboardSorting.Metric(User(microDone: 74, microDays: 77, microPercent: 96), LeaderboardSortKey.MicroBreak);
            Assert.AreEqual(77, metric.WindowDays);
            Assert.AreEqual(13, metric.ForgivenDays);
        }

        [TestMethod]
        public void ForgivenDaysStayNonNegativeWhenTheServerDisagreesWithItself()
        {
            var metric = LeaderboardSorting.Metric(User(focusDone: 95, focusDays: 95, window: 90), LeaderboardSortKey.FocusMode);
            Assert.AreEqual(0, metric.ForgivenDays);
            Assert.AreEqual(95, metric.WindowSpanDays);
            Assert.AreEqual(0, LeaderboardSorting.Metric(User(focusDone: 84, focusDays: 84, window: null), LeaderboardSortKey.FocusMode).ForgivenDays,
                            "no window from the server: nothing is known to be forgiven");
        }

        [TestMethod]
        public void CompletionSummaryNamesTheForgivenDaysOnlyWhenThereAreSome()
        {
            const string plain = "{completed} days completed out of last {total} days";
            const string expected = "Last {window} days: {completed} completed, {forgiven} weekend days forgiven";
            Assert.AreEqual("Last 90 days: 84 completed, 6 weekend days forgiven",
                LeaderboardSorting.CompletionSummary(LeaderboardSorting.Metric(User(focusDone: 84, focusDays: 84), LeaderboardSortKey.FocusMode), plain, expected));
            Assert.AreEqual("88 days completed out of last 90 days",
                LeaderboardSorting.CompletionSummary(LeaderboardSorting.Metric(User(morningDone: 88, morningDays: 90, morningPercent: 98), LeaderboardSortKey.Morning), plain, expected));
        }

        [TestMethod]
        public void WeekdayLabelsAreSundayFirstInTheAppLanguage()
        {
            CollectionAssert.AreEqual(new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }, StatsWeekdays.Short("en").ToArray());
            var spanish = StatsWeekdays.Short("es");
            Assert.AreEqual(7, spanish.Count);
            StringAssert.StartsWith(spanish[0].ToLowerInvariant(), "do");
            Assert.AreEqual(7, StatsWeekdays.Short("").Count);
            Assert.AreEqual(7, StatsWeekdays.Short("not-a-language").Count);
        }

        // ------------------------------------------------------------------ markdown (MarkdownTextTests.swift)

        private static MarkdownRun R(string text, bool bold = false, bool italic = false, bool code = false, string link = null) =>
            new MarkdownRun(text, bold, italic, code, link);

        [TestMethod]
        public void BoldRunsAreSplitOutOfPlainText()
        {
            var blocks = MarkdownParser.Parse("Your **focus blocks** streak is a powerhouse");
            Assert.AreEqual(1, blocks.Count);
            CollectionAssert.AreEqual(new[] { R("Your "), R("focus blocks", bold: true), R(" streak is a powerhouse") }, blocks[0].Runs.ToArray());
        }

        [TestMethod]
        public void UnderscoreBoldAndItalicMarkers()
        {
            CollectionAssert.AreEqual(new[] { R("bold", bold: true), R(" and "), R("italic", italic: true) }, MarkdownParser.ParseInline("__bold__ and _italic_").ToArray());
            CollectionAssert.AreEqual(new[] { R("italic", italic: true) }, MarkdownParser.ParseInline("*italic*").ToArray());
        }

        [TestMethod]
        public void NestedItalicInsideBold()
        {
            CollectionAssert.AreEqual(new[] { R("bold ", bold: true), R("both", bold: true, italic: true), R(" bold", bold: true) },
                MarkdownParser.ParseInline("**bold *both* bold**").ToArray());
        }

        [TestMethod]
        public void UnclosedMarkerStaysLiteral()
        {
            CollectionAssert.AreEqual(new[] { R("2 * 3 * 4 and snake_case_words") }, MarkdownParser.ParseInline("2 * 3 * 4 and snake_case_words").ToArray());
            CollectionAssert.AreEqual(new[] { R("a **b") }, MarkdownParser.ParseInline("a **b").ToArray());
        }

        [TestMethod]
        public void StreamingPartialBoldRendersLiterallyUntilClosed()
        {
            const string full = "You're doing amazing! \U0001F4AA Your **focus blocks** streak is a powerhouse";
            var streamed = new StringBuilder();
            foreach (char c in full)
            {
                streamed.Append(c);
                var blocks = MarkdownParser.Parse(streamed.ToString());
                Assert.AreEqual(1, blocks.Count);
                Assert.AreEqual(streamed.ToString().Replace("**", "").Trim(), blocks[0].PlainText.Replace("**", "").Trim());
            }
            CollectionAssert.AreEqual(new[] { R("Your **focus blocks*") }, MarkdownParser.Parse("Your **focus blocks*")[0].Runs.ToArray());
            CollectionAssert.AreEqual(new[] { R("Your **focus bl") }, MarkdownParser.Parse("Your **focus bl")[0].Runs.ToArray());
            CollectionAssert.AreEqual(new[] { false, true, false }, MarkdownParser.Parse(full)[0].Runs.Select(r => r.IsBold).ToArray());
        }

        [TestMethod]
        public void EmojiAndPunctuationPassThrough()
        {
            const string text = "Keep going! \U0001F680\U0001F3AF It's 100% worth it — you've earned it.";
            CollectionAssert.AreEqual(new[] { R(text) }, MarkdownParser.ParseInline(text).ToArray());
        }

        [TestMethod]
        public void BlankLinesSeparateParagraphsAndSingleNewlinesJoin()
        {
            var blocks = MarkdownParser.Parse("First line\nstill first\n\n\nSecond paragraph");
            Assert.AreEqual(2, blocks.Count);
            Assert.AreEqual("First line\nstill first", blocks[0].PlainText);
            Assert.AreEqual("Second paragraph", blocks[1].PlainText);
        }

        [TestMethod]
        public void HeadingsAndHashtagsAreDistinguished()
        {
            var blocks = MarkdownParser.Parse("# Big\n## Medium\n### Small\n#### too deep\n#hashtag");
            Assert.AreEqual(1, blocks[0].HeadingLevel);
            Assert.AreEqual(2, blocks[1].HeadingLevel);
            Assert.AreEqual(3, blocks[2].HeadingLevel);
            Assert.AreEqual(MarkdownBlockKind.Paragraph, blocks[3].Kind);
            Assert.AreEqual("#### too deep\n#hashtag", blocks[3].PlainText);
        }

        [TestMethod]
        public void BulletsAndNumberedLines()
        {
            var blocks = MarkdownParser.Parse("- dash\n* star\n• dot\n1. first\n2) second\n*not a bullet*");
            CollectionAssert.AreEqual(new[] { MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Numbered, MarkdownBlockKind.Numbered, MarkdownBlockKind.Paragraph },
                blocks.Select(b => b.Kind).ToArray());
            Assert.AreEqual("1.", blocks[3].Label);
            Assert.AreEqual("2)", blocks[4].Label);
            CollectionAssert.AreEqual(new[] { R("not a bullet", italic: true) }, blocks[5].Runs.ToArray());
        }

        [TestMethod]
        public void InlineCodeSuppressesMarkersInside()
        {
            CollectionAssert.AreEqual(new[] { R("run "), R("a**b**c", code: true), R(" now") }, MarkdownParser.ParseInline("run `a**b**c` now").ToArray());
            CollectionAssert.AreEqual(new[] { R("`unclosed") }, MarkdownParser.ParseInline("`unclosed").ToArray());
        }

        [TestMethod]
        public void LinksCarryTheirUrl()
        {
            CollectionAssert.AreEqual(new[] { R("see "), R("the ", link: "https://focusbear.io/help"), R("docs", bold: true, link: "https://focusbear.io/help"), R(" today") },
                MarkdownParser.ParseInline("see [the **docs**](https://focusbear.io/help) today").ToArray());
            CollectionAssert.AreEqual(new[] { R("[not a link] (x)") }, MarkdownParser.ParseInline("[not a link] (x)").ToArray());
        }

        [TestMethod]
        public void AdjacentRunsWithSameStyleMerge()
        {
            CollectionAssert.AreEqual(new[] { R("ab", bold: true) }, MarkdownParser.ParseInline("**a****b**").ToArray());
        }

        // ------------------------------------------------------------------ productivity

        private static FocusSessionSummary Session(DateTime start, double seconds, double? score = null, double? tabs = null, double? tabSeconds = null) =>
            new FocusSessionSummary { StartTime = start, DurationSeconds = seconds, FocusAlignmentScore = score, AverageTabCount = tabs, AverageTabDuration = tabSeconds };

        [TestMethod]
        public void FocusedTimeIsWeightedByTheAlignmentScore()
        {
            Assert.AreEqual(30, ProductivitySummary.FocusedMinutesOf(Session(DateTime.Now, 3600, score: 50)), 1e-9);
            Assert.AreEqual(60, ProductivitySummary.FocusedMinutesOf(Session(DateTime.Now, 3600)), 1e-9, "no score counts in full");
            Assert.AreEqual(0, ProductivitySummary.FocusedMinutesOf(Session(DateTime.Now, -5)), "a negative duration is nothing");
        }

        [TestMethod]
        public void TheSummaryAveragesOverDaysWithSessionsAndFindsTheBestDay()
        {
            var sessions = new[]
            {
                Session(new DateTime(2026, 9, 20, 9, 0, 0), 3600),
                Session(new DateTime(2026, 9, 20, 14, 0, 0), 1800),
                Session(new DateTime(2026, 9, 22, 9, 0, 0), 1200, tabs: 4, tabSeconds: 30),
            };
            var summary = ProductivitySummary.Compute(sessions, ProductivityPeriod.Daily);
            Assert.AreEqual(110, summary.FocusedMinutes, 1e-9);
            Assert.AreEqual(55, summary.AverageMinutesPerDay, "110 minutes over the two days that had any");
            Assert.AreEqual(new DateTime(2026, 9, 20), summary.MostFocusedDay.Date);
            Assert.AreEqual(90, summary.MostFocusedDay.Value);
            Assert.AreEqual(2, summary.Chart.Count);
            Assert.IsFalse(summary.UsesHours);
            Assert.IsTrue(summary.HasTabMetrics);
            Assert.AreEqual(4, summary.AverageTabCount, "averaged over sessions that reported tabs only");
        }

        [TestMethod]
        public void ABusyBucketSwitchesTheChartToHours()
        {
            var summary = ProductivitySummary.Compute(new[] { Session(new DateTime(2026, 9, 20, 9, 0, 0), 1000 * 60) }, ProductivityPeriod.Daily);
            Assert.IsTrue(summary.UsesHours);
            Assert.AreEqual(16.7, summary.Chart.Single().Value, 1e-9);
        }

        [TestMethod]
        public void OnlyTheLatestThirtyBucketsAreCharted()
        {
            var sessions = Enumerable.Range(0, 40).Select(i => Session(new DateTime(2026, 1, 1).AddDays(i), 60)).ToList();
            var chart = ProductivitySummary.Compute(sessions, ProductivityPeriod.Daily).Chart;
            Assert.AreEqual(ProductivitySummary.DataLimit, chart.Count);
            Assert.AreEqual(new DateTime(2026, 1, 11), chart.First().Date);
        }

        [TestMethod]
        public void TheRangeReachesBackThirtyPeriodsFromThePeriodStart()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            Assert.AreEqual(new DateTime(2026, 8, 25), ProductivitySummary.DateRange(ProductivityPeriod.Daily, now).From);
            Assert.AreEqual(new DateTime(2024, 3, 1), ProductivitySummary.DateRange(ProductivityPeriod.Monthly, now).From);
            Assert.AreEqual(new DateTime(1996, 1, 1), ProductivitySummary.DateRange(ProductivityPeriod.Yearly, now).From);
            Assert.AreEqual(now, ProductivitySummary.DateRange(ProductivityPeriod.Daily, now).To);
        }

        // ------------------------------------------------------------------ focus log

        private static FocusLogEntry Entry(string id, string start, string finish, double stored, string intention = null, string mode = "", string created = null) =>
            new FocusLogEntry { Id = id, StartTime = start, FinishTime = finish, FocusDurationSeconds = stored, Intention = intention, FocusModeName = mode, CreatedAt = created ?? start };

        [TestMethod]
        public void TheEffectiveDurationIsWallClockUnlessTheStoredValueIsLonger()
        {
            Assert.AreEqual(3600, FocusLogMath.EffectiveDurationSeconds(Entry("a", "2026-09-20T09:00:00.000Z", "2026-09-20T10:00:00.000Z", 1800)));
            Assert.AreEqual(5000, FocusLogMath.EffectiveDurationSeconds(Entry("b", "2026-09-20T09:00:00.000Z", "2026-09-20T10:00:00.000Z", 5000)));
            Assert.AreEqual(1200, FocusLogMath.EffectiveDurationSeconds(Entry("c", null, null, 1200)));
        }

        [TestMethod]
        public void FormEncodedIntentionsReadAsText()
        {
            Assert.AreEqual("Write the report", FocusLogMath.DisplayText("Write+the+report"));
            Assert.AreEqual("50% done", FocusLogMath.DisplayText("50%25+done"));
            Assert.AreEqual("-", FocusLogMath.DisplayText(null));
            Assert.AreEqual("100% sure", FocusLogMath.DisplayText("100% sure"), "a stray percent is not an escape");
        }

        [TestMethod]
        public void SortingIsAlwaysDescending()
        {
            var entries = new[]
            {
                Entry("old", "2026-09-19T09:00:00.000Z", "2026-09-19T09:30:00.000Z", 1800, "b"),
                Entry("new", "2026-09-21T09:00:00.000Z", "2026-09-21T09:10:00.000Z", 600, "a"),
            };
            CollectionAssert.AreEqual(new[] { "new", "old" }, FocusLogMath.Sorted(entries, FocusLogColumn.Time).Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "old", "new" }, FocusLogMath.Sorted(entries, FocusLogColumn.Duration).Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "old", "new" }, FocusLogMath.Sorted(entries, FocusLogColumn.Intention).Select(e => e.Id).ToArray());
        }

        [TestMethod]
        public void FiltersMatchTextCaseInsensitivelyAndTheTimeColumnByDay()
        {
            var entries = new[]
            {
                Entry("a", "2026-09-19T01:00:00.000Z", "2026-09-19T01:30:00.000Z", 1800, "Write the Report", "Deep work"),
                Entry("b", "2026-09-21T01:00:00.000Z", "2026-09-21T01:10:00.000Z", 600, "email", "Admin"),
            };
            Func<int, string> humanize = s => FocusLogMath.HumanizeSeconds(s, DurationWords.English);
            CollectionAssert.AreEqual(new[] { "a" }, FocusLogMath.Filtered(entries, FocusLogColumn.Intention, "report", null, humanize).Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "b" }, FocusLogMath.Filtered(entries, FocusLogColumn.FocusMode, "ADMIN", null, humanize).Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a" }, FocusLogMath.Filtered(entries, FocusLogColumn.Duration, "30 min", null, humanize).Select(e => e.Id).ToArray());
            var day = FocusBear.Core.ToDo.ToDoDates.Parse("2026-09-21T01:00:00.000Z").Value.Date;
            CollectionAssert.AreEqual(new[] { "b" }, FocusLogMath.Filtered(entries, FocusLogColumn.Time, "", day, humanize).Select(e => e.Id).ToArray());
            Assert.AreEqual(2, FocusLogMath.Filtered(entries, FocusLogColumn.Time, "", null, humanize).Count, "no day chosen yet: everything");
        }

        [TestMethod]
        public void DurationsAreSaidInAtMostTwoUnits()
        {
            var w = DurationWords.English;
            Assert.AreEqual("45 seconds", FocusLogMath.HumanizeSeconds(45, w));
            Assert.AreEqual("1 minute", FocusLogMath.HumanizeSeconds(60, w));
            Assert.AreEqual("1 hour, 25 minutes", FocusLogMath.HumanizeSeconds(5100, w));
            Assert.AreEqual("2 hours", FocusLogMath.HumanizeSeconds(7200, w));
            Assert.AreEqual("1 day, 2 hours", FocusLogMath.HumanizeSeconds(93780, w), "the minutes fall off after two units");
            Assert.AreEqual("0 seconds", FocusLogMath.HumanizeSeconds(-3, w));
        }

        [TestMethod]
        public void TheCsvQuotesWhatWouldBreakARow()
        {
            var csv = FocusLogMath.Csv(new[] { Entry("a", "s", "f", 60, "one, \"two\"", "Mode") }, new[] { "Mode", "Dur", "Start", "Finish", "Intention", "Achievements", "Distractions" });
            var lines = csv.Split('\n');
            Assert.AreEqual("Mode,Dur,Start,Finish,Intention,Achievements,Distractions", lines[0]);
            Assert.AreEqual("Mode,60,s,f,\"one, \"\"two\"\"\",,", lines[1]);
        }

        [TestMethod]
        public void TheSummaryGraphBucketsSecondsByCreatedAt()
        {
            var entries = new[]
            {
                Entry("a", "2026-09-19T01:00:00.000Z", "2026-09-19T01:30:00.000Z", 0, created: "2026-09-19T01:30:00.000Z"),
                Entry("b", "2026-09-19T03:00:00.000Z", "2026-09-19T03:10:00.000Z", 0, created: "2026-09-19T03:10:00.000Z"),
            };
            var points = FocusLogMath.GraphPoints(entries, ProductivityPeriod.Yearly);
            Assert.AreEqual(2400, points.Single().Value);
        }

        [TestMethod]
        public void AManualSessionMustBeInThePastAndEndAfterItStarts()
        {
            var now = new DateTime(2026, 9, 24, 21, 0, 0);
            Assert.IsNull(FocusLogMath.Validate(now.AddHours(-2), now.AddHours(-1), now));
            Assert.AreEqual(ManualSessionProblem.StartInFuture, FocusLogMath.Validate(now.AddMinutes(1), now.AddMinutes(2), now));
            Assert.AreEqual(ManualSessionProblem.FinishInFuture, FocusLogMath.Validate(now.AddHours(-1), now.AddMinutes(1), now));
            Assert.AreEqual(ManualSessionProblem.FinishNotAfterStart, FocusLogMath.Validate(now.AddHours(-1), now.AddHours(-1), now));
        }

        [TestMethod]
        public void AManualSessionReusesTheManualEntryFocusMode()
        {
            Assert.AreEqual("m2", FocusLogMath.ManualFocusModeId(new[] { new FocusModeSummary { Id = "m1" }, new FocusModeSummary { Id = "m2", IsManualEntry = true } }));
            Assert.IsNull(FocusLogMath.ManualFocusModeId(new[] { new FocusModeSummary { Id = "m1" } }), "the server makes one otherwise");
        }

        [TestMethod]
        public void LoadMoreKeepsOrderAndDropsRowsAlreadyListed()
        {
            var merged = FocusLogMath.Merge(new List<FocusLogEntry> { Entry("a", null, null, 0), Entry("b", null, null, 0) }, new[] { Entry("b", null, null, 0), Entry("c", null, null, 0) });
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, merged.Select(e => e.Id).ToArray());
        }

        // ------------------------------------------------------------------ levels, goals, username

        [TestMethod]
        public void LevelRequirementsFollowTheWebTable()
        {
            var stats = new OnboardingStats { Level = 3, MorningRoutineCompletionStreakDays = 6, FocusModeCompletionStreakDays = 2, HasChattedWithFocusBear = true };
            var level3 = LevelTargets.Requirements(3, stats);
            Assert.AreEqual(6, level3.Count, "level 3 adds the chat and focus-mode goals");
            Assert.IsTrue(level3.Single(r => r.Kind == LevelRequirementKind.Morning).IsMet);
            Assert.IsFalse(level3.Single(r => r.Kind == LevelRequirementKind.FocusMode).IsMet);
            Assert.IsTrue(level3.Single(r => r.Kind == LevelRequirementKind.Chat).IsBoolean);
            Assert.AreEqual(200, LevelTargets.Requirements(9, stats).Single(r => r.Kind == LevelRequirementKind.FocusMode).Target);
            Assert.AreEqual(0, LevelTargets.Requirements(10, stats).Count);
        }

        [TestMethod]
        public void TheHeaderWindowSlidesOncePastLevelFive()
        {
            Assert.AreEqual((0, 7), LevelTargets.VisibleHeaderRange(2));
            Assert.AreEqual((1, 7), LevelTargets.VisibleHeaderRange(6));
            Assert.AreEqual((4, 10), LevelTargets.VisibleHeaderRange(9));
        }

        [TestMethod]
        public void GoalsAreTrimmedAndBlanksDropped()
        {
            CollectionAssert.AreEqual(new[] { "Run a marathon", "Ship it" }, LongTermGoals.Cleaned(new[] { " Run a marathon ", "", "   ", "Ship it" }));
        }

        [TestMethod]
        public void UsernamesAreLettersAndUnderscoresOnly()
        {
            Assert.IsTrue(LeaderboardUsername.IsValid("jez_nag"));
            Assert.IsFalse(LeaderboardUsername.IsValid("jez nag"));
            Assert.IsFalse(LeaderboardUsername.IsValid("jez1"));
            Assert.IsFalse(LeaderboardUsername.IsValid(""));
            Assert.IsFalse(LeaderboardUsername.CanUpdate("jez", "jez", false), "unchanged is not an update");
            Assert.IsFalse(LeaderboardUsername.CanUpdate("new", "jez", true), "not while saving");
            Assert.IsTrue(LeaderboardUsername.CanUpdate("new", "jez", false));
        }

        [TestMethod]
        public void PlainNumbersDropATrailingZero()
        {
            Assert.AreEqual("12", StatsText.PlainNumber(12));
            Assert.AreEqual("12.5", StatsText.PlainNumber(12.5));
            Assert.AreEqual("0.3", StatsText.PlainNumber(0.33));
            Assert.AreEqual("Level 3 goals:", StatsText.Fill("Level {level} goals:", ("level", "3")));
        }

        [TestMethod]
        public void TabsRememberTheMacsRawValues()
        {
            Assert.AreEqual("pepTalk", StatsTab.PepTalk.RawValue());
            Assert.AreEqual(StatsTab.FocusLog, StatsTabs.Parse("focusLog"));
            Assert.AreEqual(StatsTab.Streak, StatsTabs.Parse("nonsense"));
        }

        // ------------------------------------------------------------------ wire

        [TestMethod]
        public void OnboardingStatsReadNumericStrings()
        {
            var stats = StatsWire.ReadOnboardingStats("{\"level\":3,\"morning_routine_completion_streak_days\":12,\"average_morning_routines_completion_percentage\":\"87.08\",\"routines_threshold\":15,\"has_edited_focus_mode\":true}");
            Assert.AreEqual(3, stats.Level);
            Assert.AreEqual(12, stats.MorningRoutineCompletionStreakDays);
            Assert.AreEqual(87.08, stats.AverageMorningRoutinesCompletionPercentage.Value, 1e-9);
            Assert.AreEqual(true, stats.HasEditedFocusMode);
            Assert.IsNull(StatsWire.ReadOnboardingStats("[]"));
        }

        [TestMethod]
        public void UserSettingsListEachHabitOnceWithItsQuestions()
        {
            var settings = StatsWire.ReadUserSettings(
                "{\"created_at\":\"2022-08-28T10:42:11.385Z\",\"morning_activities\":[{\"id\":\"a\",\"name\":\"Walk\",\"habit_icon\":\"\U0001F6B6\",\"log_quantity_questions\":[{\"id\":\"q\",\"question\":\"Steps?\",\"log_summary_type\":\"AVG\"}]}]," +
                "\"break_activities\":[{\"id\":\"a\",\"name\":\"dup\"}],\"evening_activities\":[{\"id\":\"b\",\"activity_data\":{\"name\":\"Read\",\"habit_icon\":\"\U0001F4DA\"}}]}");
            var unique = settings.UniqueActivities;
            CollectionAssert.AreEqual(new[] { "a", "b" }, unique.Select(a => a.Id).ToArray(), "first occurrence wins");
            Assert.AreEqual("\U0001F6B6 Walk", unique[0].Title);
            Assert.IsTrue(unique[0].Questions.Single().IsAveraged);
            Assert.AreEqual("Read", unique[1].Name, "an older payload keeps the name inside activity_data");
            Assert.IsNotNull(settings.CreatedAt);
        }

        [TestMethod]
        public void FocusLogTimestampsArriveExactlyAsSent()
        {
            var log = StatsWire.ReadFocusLog("[{\"id\":\"f\",\"start_time\":\"2026-09-20T09:00:00.123Z\",\"focus_duration_seconds\":\"600\",\"metadata\":{\"is_manual\":true},\"focus_mode\":{\"id\":\"m\",\"name\":\"Deep\"}}]");
            Assert.AreEqual("2026-09-20T09:00:00.123Z", log.Single().StartTime);
            Assert.AreEqual(600, log.Single().FocusDurationSeconds);
            Assert.IsTrue(log.Single().IsManual);
            Assert.AreEqual("Deep", log.Single().FocusModeName);
        }

        [TestMethod]
        public void ConsistencyAndLeaderboardAreRead()
        {
            var consistency = StatsWire.ReadConsistency("{\"days_number\":90,\"habits\":[{\"activity_id\":\"a\",\"completed_days\":\"60\"}]}");
            Assert.AreEqual(60, consistency["a"]);
            var board = StatsWire.ReadLeaderboard("{\"user_rank\":{\"id\":\"me\",\"username\":\"jez\"},\"users_rankings\":[{\"id\":\"x\",\"username\":\"x\",\"morning_routines_streak\":\"4\"}]}");
            Assert.AreEqual("me", board.UserRank.Id);
            Assert.AreEqual(4, board.UsersRankings.Single().MorningRoutinesStreak);
            Assert.IsNull(StatsWire.ReadLeaderboard("{}"));
        }

        [TestMethod]
        public void AManualSessionBodyCarriesUtcTimesAndItsDuration()
        {
            var start = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Local);
            var body = StatsWire.CreateFocusBlockBody("i", "a", "d", start, start.AddMinutes(25), null);
            Assert.AreEqual(1500, (int)body["focus_duration_seconds"]);
            StringAssert.EndsWith((string)body["start_time"], "Z");
            Assert.IsFalse(body.ContainsKey("focus_mode_id"), "the server picks the manual focus mode when none is known");
        }
    }
}
