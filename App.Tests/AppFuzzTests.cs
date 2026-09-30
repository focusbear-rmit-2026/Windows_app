using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Focus_Bear.API;
using Focus_Bear.Services.FocusButton;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Seeded random inputs for the app-side readers: Focus Button serial lines, device text, the offline queue's
    /// lines and stored answers, and the completion bodies. A failure names its seed and case.
    /// </summary>
    [TestClass]
    public class AppFuzzTests
    {
        private static readonly string[] Pieces =
        {
            "EVENT", "STARTHB", "INFO", "FIRMWAREVER", "HABIT", "M50", "C30", "-", "1.8.1", "|", "||", "300", "-1", "99999999999",
            " ", "\t", "\r", "\n", "\0", "\"", "\\", "é", "😀", "\uD83D", "→", "‍", "️", ",", ";", "[", "]", "{", "}",
            "null", "true", "question_id", "logged_value", "abc", "x", "0", "1e308", "NaN",
        };

        private static string Text(Random r, int maxPieces = 10)
        {
            var sb = new StringBuilder();
            int n = r.Next(maxPieces + 1);
            for (int i = 0; i < n; i++) sb.Append(r.Next(8) == 0 ? ((char)r.Next(0, 0x3000)).ToString() : Pieces[r.Next(Pieces.Length)]);
            return r.Next(15) == 0 ? null : sb.ToString();
        }

        private static void Run(int seed, int count, Action<Random, int> check)
        {
            var r = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                try { check(r, i); }
                catch (Exception ex) { throw new Exception("seed " + seed + ", case " + i + ": " + ex.GetType().Name + ": " + ex.Message, ex); }
            }
        }

        [TestMethod]
        public void FocusButtonLines_NeverThrow()
        {
            Run(301, 3000, (r, i) =>
            {
                string line = Text(r, 12);
                string[] parts = (line ?? "").Split('|');
                if (FocusButtonProtocol.TryParseStartHabit(r.Next(10) == 0 ? null : parts, out string id, out string name))
                {
                    Assert.IsNotNull(id);
                    Assert.IsNotNull(name);
                }
                if (FocusButtonProtocol.TryParseFirmwareVersion(line, out string hw, out Version v))
                    Assert.IsNotNull(v);
            });
        }

        [TestMethod]
        public void DeviceText_IsAlwaysSafeForAPipeLine()
        {
            Run(302, 3000, (r, i) =>
            {
                string safe = FocusButtonProtocol.SanitizeDeviceText(Text(r, 30));
                Assert.IsTrue(safe.Length <= FocusButtonProtocol.MAX_DEVICE_TEXT_LENGTH, safe.Length.ToString());
                Assert.IsFalse(safe.Contains("|") || safe.Any(char.IsControl) || safe.Any(char.IsSurrogate), safe);
                Assert.AreEqual(safe.Trim(), safe);
            });
        }

        [TestMethod]
        public void HabitsCrc_NeverThrows()
        {
            Run(303, 500, (r, i) =>
            {
                var entries = Enumerable.Range(0, r.Next(0, 6)).Select(_ =>
                    new FocusButtonProtocol.HabitWireEntry("h" + r.Next(), r.Next(), Text(r) ?? "", r.Next())).ToList();
                FocusButtonProtocol.HabitsCrc(entries);
            });
        }

        [TestMethod]
        public void OfflineQueueLines_AndStoredAnswers_NeverThrow_AndAlwaysMakeValidJson()
        {
            Run(304, 3000, (r, i) =>
            {
                string line = string.Join(",", Enumerable.Range(0, r.Next(0, 13)).Select(_ => Text(r, 3) ?? ""));
                if (OfflineActivities.TryParse(line, out OfflineEntry entry))
                {
                    JObject body = ActivityCompletedBody.FromOffline(entry, Text(r));
                    JObject.Parse(body.ToString());   // always a valid document
                    Assert.IsNotNull(body["activity_id"]);
                }
                JArray answers = ActivityCompletedBody.OfflineAnswersToJson(Text(r, 8));
                Assert.IsNotNull(answers);
            });
        }

        [TestMethod]
        public void CompletionBodies_ForAnyQuestions_AreValidJson_AndStoredAnswersRoundTrip()
        {
            Run(305, 2000, (r, i) =>
            {
                var questions = r.Next(5) == 0 ? null : Enumerable.Range(0, r.Next(0, 4)).Select(_ => new QuantityQuestion
                {
                    id = Text(r, 3),
                    quantity = r.Next(6) == 0 ? double.NaN : r.Next(8) == 0 ? double.PositiveInfinity : (r.NextDouble() - 0.5) * 1e6,
                }).ToList();
                string body = ActivityCompletedBody.Build(Text(r) ?? "", r.Next(), Text(r), Text(r), DateTime.Now, DateTime.Now, r.Next(2) == 0, Text(r), questions);
                JObject parsed = JObject.Parse(body);
                int expected = questions?.Count ?? 0;
                Assert.AreEqual(expected, (parsed["log_quantity_answers"] as JArray)?.Count ?? 0);

                string stored = ActivityCompletedBody.OfflineAnswers(questions);
                Assert.IsFalse(stored.Contains(","), "one CSV column");
                Assert.AreEqual(expected, ActivityCompletedBody.OfflineAnswersToJson(stored).Count, stored);
            });
        }
    }
}
