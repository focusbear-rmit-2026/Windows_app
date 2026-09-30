using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FocusBear.Core.Blocking;
using FocusBear.Core.Forecast;
using FocusBear.Core.LateNoMore;
using FocusBear.Core.Reports;
using FocusBear.Core.Schedule;
using FocusBear.Core.Subscription;
using FocusBear.Core.ToDo;
using FocusBear.Core.Updates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Fuzz
{
    /// <summary>
    /// Seeded random and edge inputs for the parsers and readers that take typed text or server JSON. Each asserts
    /// invariants that hold for every input: nothing throws, a parsed date is in range, a round trip holds, an
    /// output is bounded, a CSV cell stays in its column. A failure names its seed and case, which replay exactly.
    /// </summary>
    [TestClass]
    public class ParserFuzzTests
    {
        private const int N = 2000;
        private static readonly DateTime Now = new DateTime(2026, 9, 27, 10, 30, 0);

        [TestMethod]
        public void SnoozeParser_NeverThrows_AndLandsInARange()
        {
            Fuzz.Run(101, N, (f, i) =>
            {
                DateTime baseDate = i % 5 == 0 ? f.Date() : Now;
                DateTime? d = DayPlanSnoozeParser.Parse(f.Any(), baseDate);
                // Relative offsets can be large ("999m" is 999 months) or negative ("-1d"); an absolute day is never in the past.
                if (d.HasValue) Assert.AreEqual(d.Value.Date, d.Value, "a day, never a time");
            });
        }

        [TestMethod]
        public void Snooze_AHugeOffset_IsNotADate_AndDoesNotThrow()
        {
            // Found by the snooze fuzz: "+9999y" (or "9999999d") threw ArgumentOutOfRange out of the snooze box, and
            // "400000000w" overflowed int and landed in the past.
            foreach (string input in new[] { "+9999y", "9999 years", "9999999d", "5000000w", "400000000w", "999999mo", "-9999y", "-9999999d" })
                Assert.IsNull(DayPlanSnoozeParser.Parse(input, Now), input);
            Assert.AreEqual(Now.Date.AddYears(3), DayPlanSnoozeParser.Parse("3y", Now));
        }

        [TestMethod]
        public void Snooze_OnTheLastRepresentableDay_DoesNotThrow()
        {
            // Found by seed 1101 case 300 ("tomorrow" on 9999-12-31) and seed 2102 ("mon"): no tomorrow to go to.
            DateTime last = DateTime.MaxValue.Date.AddHours(9);
            foreach (string input in new[] { "tomorrow", "mon", "next week", "next month", "next year", "1st", "+1d" })
                Assert.IsNull(DayPlanSnoozeParser.Parse(input, last), input);
            Assert.AreEqual(last.Date, DayPlanSnoozeParser.Parse("today", last));
            Assert.IsNull(ToDoDueDateParser.Parse("tomorrow", last), "the due-date parser too");
            Assert.IsNull(ToDoDueDateParser.Parse("mon", last));
        }

        [TestMethod]
        public void DateParsersAndLabels_WorkInEveryCulture()
        {
            // Every specific culture the machine knows (Thai Buddhist, Hijri, Japanese era calendars among them),
            // with the inputs people type: month names in that culture included.
            var inputs = new List<string> { "today", "tomorrow", "mon", "next week", "3d", "-1w", "15", "15th", "2026-10-02", "2/10", "10/2/27", "" };
            // A stride through every specific culture (seeded, so the same ones each run), plus the calendars most
            // likely to trip a date parser: Japanese era, Thai Buddhist, Hijri (Um Al Qura), Persian, Hebrew.
            var all = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
            var pick = new Random(117);
            int stride = int.TryParse(Environment.GetEnvironmentVariable("FB_FUZZ_SCALE"), out int scale) && scale > 1 ? 1 : 6;
            var cultures = all.Where(_ => pick.Next(stride) == 0)
                .Concat(new[] { "ja-JP", "th-TH", "ar-SA", "fa-IR", "he-IL", "en-US", "de-DE" }.Select(n => new CultureInfo(n)));
            foreach (CultureInfo culture in cultures)
            {
                string[] months = culture.DateTimeFormat.AbbreviatedMonthGenitiveNames;
                string sample = months.Length > 2 ? months[2] : "mar";
                foreach (string input in inputs.Concat(new[] { "12 " + sample, sample + " 12", culture.DateTimeFormat.GetDayName(DayOfWeek.Friday) }))
                {
                    try
                    {
                        DateTime? due = ToDoDueDateParser.Parse(input, Now, culture);
                        DateTime? snooze = DayPlanSnoozeParser.Parse(input, Now, culture);
                        if (due.HasValue) Assert.IsTrue(due.Value.Year > 1900 && due.Value.Year < 2200, culture.Name + " '" + input + "' -> " + due.Value.ToString("o"));
                        if (snooze.HasValue) Assert.IsTrue(snooze.Value.Year > 1900 && snooze.Value.Year < 2200, culture.Name + " '" + input + "'");
                        string label = FocusBear.Core.ToDo.ToDoDates.DueLabel("2027-03-12", Now, "Today", "Tomorrow", "Yesterday", culture);
                        Assert.IsTrue(label.Length > 0 && label.Length <= 40, culture.Name + " " + label);
                        FocusBear.Core.ToDo.ToDoDates.WeekdayDayMonth(new DateTime(2026, 12, 31), Now, culture);
                    }
                    catch (Exception ex) when (!(ex is AssertFailedException))
                    {
                        Assert.Fail(culture.Name + " '" + input + "': " + ex.GetType().Name + " " + ex.Message);
                    }
                }
            }
        }

        [TestMethod]
        public void DueDateParser_RoundTrips_IsoDaysAndRelativeOffsets()
        {
            Fuzz.Run(118, N, (f, i) =>
            {
                DateTime day = new DateTime(2000, 1, 1).AddDays(f.Next(0, 365 * 100));
                Assert.AreEqual(day, ToDoDueDateParser.Parse(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Now));
                int n = f.Next(-3000, 3000);
                Assert.AreEqual(Now.Date.AddDays(n), ToDoDueDateParser.Parse(n + "d", Now), n + "d");
                Assert.AreEqual(Now.Date.AddDays(7 * n), ToDoDueDateParser.Parse(n + "w", Now), n + "w");
                if (n > 0) Assert.AreEqual(Now.Date.AddDays(n), DayPlanSnoozeParser.Parse("+" + n + "d", Now));
            });
        }

        [TestMethod]
        public void DueDateParser_NeverThrows_AndLandsInARange()
        {
            Fuzz.Run(102, N, (f, i) =>
            {
                DateTime now = i % 5 == 0 ? f.Date() : Now;
                DateTime? d = ToDoDueDateParser.Parse(f.Any(), now);
                if (d.HasValue) Assert.IsTrue(d.Value.Year >= 1 && d.Value.Year <= 9999);
            });
        }

        [TestMethod]
        public void QuickAddComposer_NeverThrows_AndItsTokensAreInRange()
        {
            var projects = new[] { "Thesis", "House move", "a_b", "#x", "" };
            Fuzz.Run(103, N, (f, i) =>
            {
                string raw = f.Any();
                ParsedQuickAddTask t = QuickAddTaskParser.Parse(raw, i % 2 == 0 ? projects : null);
                if (t.DurationMinutes.HasValue) Assert.IsTrue(t.DurationMinutes >= 0 && t.DurationMinutes <= QuickAddTaskParser.MaxDurationMinutes, "duration " + t.DurationMinutes);
                if (t.Outcome.HasValue) Assert.IsTrue(t.Outcome >= 0 && t.Outcome <= 10, "outcome " + t.Outcome);
                if (t.Title != null) Assert.IsTrue(t.Title.Length <= (raw ?? "").Length, "the title is never longer than the line");
                QuickAddTaskParser.ExtractStarDurationToken(raw);
                QuickAddTaskParser.ExtractPriorityToken(raw);
                QuickAddTaskParser.ExtractProjectToken(raw, projects);
            });
        }

        [TestMethod]
        public void DurationFields_ReadAnImesDigits()
        {
            // Found while fuzzing: the duration fields matched full-width or Arabic-Indic digits (\d) and then failed to
            // read them, so a Japanese IME's "\uFF14\uFF15m" was no duration at all (the due-date parser already folds them).
            foreach (Func<string, int?> read in new Func<string, int?>[] { DurationFieldParser.DurationMinutesFromFieldText, QuickAddTaskParser.DurationMinutesFromFieldText })
            {
                Assert.AreEqual(45, read("\uFF14\uFF15m"));
                Assert.AreEqual(90, read("\uFF11h\uFF13\uFF10"));
                Assert.AreEqual(45, read("\u0664\u0665"));
                Assert.AreEqual(30, read("30m"), "ASCII unchanged");
            }
        }

        [TestMethod]
        public void DurationParsers_NeverThrow_AndStayInTheirCeiling()
        {
            Fuzz.Run(104, N, (f, i) =>
            {
                string text = f.Any();
                int? a = DurationFieldParser.DurationMinutesFromFieldText(text);
                int? b = QuickAddTaskParser.DurationMinutesFromFieldText(text);
                foreach (int? m in new[] { a, b })
                    if (m.HasValue) Assert.IsTrue(m >= 0 && m <= 24 * 60, "minutes " + m + " for '" + text + "'");
                DurationFieldParser.ExtractDurationToken(text);
                TodoFieldParsing.DurationSeconds(text);
                TodoFieldParsing.DurationMinutes(text);
                TodoFieldParsing.DurationMinutes(f.NextDouble() * 1e12);
                HabitDurationText.Short(f.Next(int.MinValue, int.MaxValue));
            });
        }

        private static readonly Regex Quantity = new Regex(@"^[0-9]*\.?[0-9]*$");

        [TestMethod]
        public void QuantityTyping_TakesAnImesDigits()
        {
            // Same finding as the duration fields: a full-width "４" (or the IME's "．" and "，") was refused.
            Assert.AreEqual("4", QuantityTyping.Apply("", 0, 0, "\uFF14"));
            Assert.AreEqual("6.8", QuantityTyping.Apply("6", 1, 0, "\uFF0C\uFF18"));
            Assert.AreEqual("2.5", QuantityTyping.Apply("2", 1, 0, "\uFF0E5"));
        }

        [TestMethod]
        public void QuantityTyping_NeverThrows_AndOnlyEverHoldsANumber()
        {
            Fuzz.Run(105, N, (f, i) =>
            {
                string text = i % 3 == 0 ? f.Any() : (f.Coin() ? "12.5" : "3");
                int start = f.Next(-3, 20), length = f.Next(-3, 20);
                string result = QuantityTyping.Apply(text, start, length, f.Any());
                if (result != null) Assert.IsTrue(Quantity.IsMatch(result), "'" + result + "'");
            });
        }

        [TestMethod]
        public void VersionCompare_NeverThrows_AndIsAnOrder()
        {
            Fuzz.Run(106, N, (f, i) =>
            {
                Version a = VersionCompare.Parse(f.Any()), b = VersionCompare.Parse(i % 2 == 0 ? f.Any() : f.Next(0, 5) + "." + f.Next(0, 5) + (f.Coin() ? "." + f.Next(0, 3) : ""));
                Assert.IsFalse(VersionCompare.IsNewer(a, a) && a != null, "nothing is newer than itself");
                if (a != null && b != null) Assert.IsFalse(VersionCompare.IsNewer(a, b) && VersionCompare.IsNewer(b, a));
                if (a != null) Assert.IsTrue(VersionCompare.AtLeast(a, a));
            });
        }

        [TestMethod]
        public void StoredWallClock_RoundTripsEveryTime_AndReadsAnyString()
        {
            Fuzz.Run(107, N, (f, i) =>
            {
                DateTime t = f.Date();
                DateTime back = StoredWallClock.FromStored(StoredWallClock.ToStored(t));
                // Whole seconds, truncated toward 1970 as the old writer did (so pre-1970 fractions round up): within 1 s.
                Assert.IsTrue(Math.Abs(back.Ticks - t.Ticks) < TimeSpan.TicksPerSecond, t.ToString("o") + " -> " + back.ToString("o"));
                Assert.AreEqual(StoredWallClock.ToStored(back), StoredWallClock.ToStored(t), "stable once stored");
                Assert.AreEqual(DateTimeKind.Local, back.Kind);
                StoredWallClock.FromStored(f.Any());
                StoredWallClock.FromStored(f.Next(int.MinValue, int.MaxValue).ToString() + f.Text(1));
            });
        }

        private static readonly string[] ToDoKeys =
        {
            "data", "meta", "hasNextPage", "itemCount", "id", "title", "status", "due_date", "created_at", "duration",
            "eisenhower_quadrant", "tags", "subtasks", "project_id", "assignee", "text", "name", "custom_status_id",
        };

        [TestMethod]
        public void ToDoJson_ReadsAnyBody_WithoutThrowing()
        {
            Fuzz.Run(108, N, (f, i) =>
            {
                string json = f.Json(ToDoKeys);
                ToDoJson.ReadPage(json);
                IList<ToDoTask> tasks = ToDoJson.ReadTasks(json);
                if (tasks != null) Assert.IsFalse(tasks.Any(t => t == null), "no null rows");
                ToDoJson.ReadTask(json);
                ToDoJson.ReadProjectsPage(json);
                ToDoJson.ReadProject(json);
                ToDoJson.ReadPortfolio(json);
            });
        }

        [TestMethod]
        public void ForecastRowGuard_SameDueDate_IsReflexiveAndSymmetric()
        {
            Fuzz.Run(109, N, (f, i) =>
            {
                string a = i % 3 == 0 ? f.Date().ToString("o", CultureInfo.InvariantCulture) : f.Any();
                string b = f.Coin() ? a : f.Any();
                if (!string.IsNullOrWhiteSpace(a)) Assert.IsTrue(ForecastRowGuard.SameDueDate(a, a));
                Assert.AreEqual(ForecastRowGuard.SameDueDate(a, b), ForecastRowGuard.SameDueDate(b, a));
                ForecastRowGuard.For(new ToDoTask { Id = "x", DueDate = a, Title = f.Any() }, new ToDoTask { Id = "x", DueDate = b });
            });
        }

        [TestMethod]
        public void SubscriptionStatus_ReadsAnyBody_WithoutThrowing()
        {
            var keys = new[] { "hasActiveSubscription", "activeEntitlements", "expirations", "trial", "personal", "days_left", "team_admin" };
            Fuzz.Run(110, N, (f, i) =>
            {
                SubscriptionStatus s = SubscriptionStatus.Parse(f.Json(keys));
                if (s != null && s.TrialDaysLeft.HasValue) Assert.IsFalse(s.IsPurchased);
                if (s != null && s.IsPurchased) Assert.IsTrue(s.HasActiveSubscription);
            });
        }

        [TestMethod]
        public void CalendarReaders_ReadAnyEvent_AndOnlyEverOfferAWebLink()
        {
            var keys = new[] { "external_metadata", "hangoutLink", "onlineMeeting", "joinUrl", "onlineMeetingUrl", "conferenceData",
                               "entryPoints", "entryPointType", "uri", "location", "displayName", "description", "body", "content",
                               "bodyPreview", "attendees", "self", "responseStatus", "response", "calendars", "items", "account",
                               "calendar_id", "summary", "is_selected", "requires_reauth" };
            Fuzz.Run(111, N, (f, i) =>
            {
                JToken token = null;
                try { token = JToken.Parse(f.Json(keys)); } catch (Newtonsoft.Json.JsonException) { }
                if (token is JObject o)
                {
                    string link = MeetingLink.FromBackendEvent(o);
                    if (link != null)
                        Assert.IsTrue(link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase), link);
                    MeetingLink.IsDeclined(o);
                }
                LateNoMoreCalendar.ParseCalendarDatas(token, f.Coin() ? "google" : "microsoft");
                string text = MeetingLink.FromText(f.Any());
                if (text != null) Assert.IsTrue(text.StartsWith("https://", StringComparison.OrdinalIgnoreCase), text);
                MeetingLink.FromLocation(f.Any());
            });
        }

        [TestMethod]
        public void LogSafe_NeverThrows_AndNeverWritesTheBody()
        {
            var keys = new[] { "access_token", "email", "settings", "a", "b" };
            Fuzz.Run(112, N, (f, i) =>
            {
                string before = f.Json(keys), after = f.Coin() ? before : f.Json(keys);
                string d = LogSafe.Describe(after);
                Assert.IsTrue(d.Length <= 40, d);
                string changed = LogSafe.ChangedKeys(before, after);
                Assert.IsTrue(changed.Length < 20 * 4 * 200, "bounded");
                LogSafe.Keys(after);
            });
        }

        [TestMethod]
        public void AiDecisionCsv_EveryFieldRoundTrips_InItsColumn()
        {
            Fuzz.Run(113, N, (f, i) =>
            {
                string[] fields = Enumerable.Range(0, 7).Select(_ => f.Any()).ToArray();
                double p = f.NextDouble();
                string row = AiDecisionCsv.Row(f.Date() == DateTime.MinValue ? new DateTime(2026, 1, 1) : Now, fields[0], fields[1], fields[2], p, fields[3], fields[4], fields[5], fields[6]);
                Assert.IsFalse(row.Contains("\n") || row.Contains("\r"), "one line");
                string[] back = AiDecisionCsv.Parse(row);
                Assert.AreEqual(9, back.Length, row);
                int[] columns = { 1, 2, 3, 5, 6, 7, 8 };
                for (int k = 0; k < 7; k++)
                    Assert.AreEqual((fields[k] ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '), back[columns[k]]);
            });
        }

        [TestMethod]
        public void ToDoCsv_EveryCellStaysInItsColumn()
        {
            var headers = new[] { "Title", "Details", "Objective", "Status", "Quadrant", "Tags", "Subtasks", "Due", "Created" };
            Fuzz.Run(114, 400, (f, i) =>
            {
                var tasks = Enumerable.Range(0, f.Next(1, 5)).Select(_ => new ToDoTask
                {
                    Id = "t", Title = f.Any(), Details = f.Any(), Objective = f.Any(), DueDate = f.Any(), CreatedAt = f.Any(),
                    Tags = f.Coin() ? null : new List<ToDoTag> { new ToDoTag("p", f.Any()) },
                    Subtasks = new List<ToDoSubtask> { new ToDoSubtask(f.Any(), f.Coin()) },
                }).ToList();
                string csv = ToDoCsv.Build(tasks, headers, t => f.Any(), t => f.Any());
                var rows = Fuzz.ReadCsv(csv, "\n");
                Assert.AreEqual(tasks.Count + 1, rows.Count, csv);
                foreach (var row in rows) Assert.AreEqual(headers.Length, row.Count, csv);
                foreach (var row in rows.Skip(1))
                    foreach (string cell in row)
                        Assert.IsFalse(cell.Length > 0 && "=+-@\t\r".IndexOf(cell[0]) >= 0, "a formula lead: " + cell);
            });
        }

        [TestMethod]
        public void HostOf_AnAddressThatBreaksSystemUri_IsNoHost()
        {
            // Found by seed 4115 case 8981: Uri.TryCreate itself threw IndexOutOfRange on "//" + an RTL mark, out of
            // the blocking path that reads the host of whatever is in the address bar.
            foreach (string url in new[] { "//‏", "http://‏", "//‏/x" })
            {
                BlockPageUnblock.HostOf(url);
                FocusBear.Core.SafeUri.TryCreate(url, UriKind.Absolute, out _);
            }
        }

        [TestMethod]
        public void BlockPageUrls_NeverThrow()
        {
            const string page = @"C:\Users\x\AppData\Local\Focus Bear\focus-block.html";
            var tokens = new BlockPageTokens();
            Fuzz.Run(115, N, (f, i) =>
            {
                string url = i % 3 == 0 ? "file:///" + page + "?old_url=" + f.Any() + "&" + f.Any() : f.Any();
                BlockPageUnblock.HasTrigger(url);
                BlockPageUnblock.OffersUnblock(url);
                BlockPageUnblock.PageParam(url, "old_url");
                BlockPageUnblock.IsOwnBlockPage(url, page);
                BlockPageUnblock.MentionsBlockPage(url, page);
                BlockPageUnblock.AuthorisedOldUrl(url, page, tokens, DateTime.UtcNow);
                string host = BlockPageUnblock.HostOf(url);
                if (host != null) Assert.AreEqual(host.ToLowerInvariant(), host);
                BlockPageUnblock.Query(url, f.Any() ?? "x");
                string without = BlockPageUnblock.WithoutTrigger(url);
                Assert.IsFalse(BlockPageUnblock.HasTrigger(without) && without.Contains("&unblock_it=true"), without);
                BlockPageTokens.FromJson(f.Json(new[] { "token", "old_url", "expires" }), DateTime.UtcNow);
            });
        }

        [TestMethod]
        public void DaysCovered_AMeetingOnTheLastRepresentableDay_DoesNotThrow()
        {
            // Found by seed 116, case 18: stepping past 9999-12-31 threw from AddDays, and the calendar reader's
            // per-item catch then dropped the event (and logged it) on every refresh.
            DateTime last = DateTime.MaxValue.Date.AddHours(10);
            CollectionAssert.AreEqual(new[] { "9999-12-31" }, DayPlanDayKey.DaysCovered(last, DateTime.MaxValue, 7).ToList());
            CollectionAssert.AreEqual(new[] { "9999-12-30", "9999-12-31" }, DayPlanDayKey.DaysCovered(last.AddDays(-1), DateTime.MaxValue, 7).ToList());
        }

        [TestMethod]
        public void DaysCovered_IsBoundedOrderedAndNeverThrows()
        {
            Fuzz.Run(116, N, (f, i) =>
            {
                DateTime start = f.Date();
                long endTicks = start.Ticks + f.Next(-1000, 20000) * TimeSpan.TicksPerMinute;
                DateTime end = f.Coin() && endTicks >= 0 && endTicks <= DateTime.MaxValue.Ticks ? new DateTime(endTicks) : f.Date();
                if (end < start && f.Coin()) end = start;
                int max = f.Next(-2, 10);
                IList<string> days = DayPlanDayKey.DaysCovered(start, end, max);
                Assert.IsTrue(days.Count >= 1 && days.Count <= Math.Max(1, max), days.Count + " for max " + max);
                Assert.AreEqual(DayPlanDayKey.For(start.Date), days[0], "starts on the start day");
                for (int k = 1; k < days.Count; k++) Assert.IsTrue(string.CompareOrdinal(days[k - 1], days[k]) < 0);
            });
        }
    }
}
