using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Analytics;
using FocusBear.Core.FocusButton;
using FocusBear.Core.Notes;
using FocusBear.Core.Postpone;
using FocusBear.Core.ToDo;
using FocusBear.Core.Ui;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Fuzz
{
    /// <summary>The second batch: dates on the wire, HTML, event fragments, webview payloads, INI files, typed minutes.</summary>
    [TestClass]
    public class ReaderFuzzTests
    {
        private const int N = 2000;
        private static readonly DateTime Now = new DateTime(2026, 9, 27, 10, 30, 0);

        [TestMethod]
        public void WireDates_NeverThrow_AndTheirLabelsAreShort()
        {
            Fuzz.Run(201, N, (f, i) =>
            {
                string wire = i % 3 == 0 ? f.Date().ToString(i % 2 == 0 ? "o" : "yyyy-MM-dd", CultureInfo.InvariantCulture) : f.Any();
                DateTime now = i % 7 == 0 ? f.Date() : Now;
                DateTime? d = ToDoDates.Parse(wire);
                string ymd = ToDoDates.YmdFromIso(wire);
                if (ymd != null) Assert.AreEqual(10, ymd.Length, ymd);
                ToDoDates.DueState(wire, f.Coin(), now);
                string label = ToDoDates.DueLabel(wire, now, "Today", "Tomorrow", "Yesterday");
                Assert.IsTrue(label.Length <= 40, label);
                ToDoDates.DurationLabelFromSeconds(f.Next(int.MinValue, int.MaxValue));
                ToDoDates.DurationLabel(f.Next(-5, 100000), f.Next(-5, 100000));
                if (d.HasValue) { ToDoDates.WeekdayDayMonth(d.Value, now); ToDoDates.DayMonthYear(d.Value); }
            });
        }

        [TestMethod]
        public void Html_AnEmptyNestedListBeforeText_IsStableAcrossSaves()
        {
            // Found by seed 202 case 124: a list item holding an empty nested list and then text gained a <br>
            // on every parse-and-write, so each save of the note grew it.
            string html = "<ol><li><ol></ol>text</li></ol>";
            string once = NoteHtml.Write(NoteHtml.Parse(html));
            string twice = NoteHtml.Write(NoteHtml.Parse(once));
            string thrice = NoteHtml.Write(NoteHtml.Parse(twice));
            Assert.AreEqual(once, twice);
            Assert.AreEqual(twice, thrice);
        }

        [TestMethod]
        public void Html_ParsesAnything_AndPlainTextHasNoTags()
        {
            var pieces = new[] { "<p>", "</p>", "<b>", "</b>", "<a href=\"", "\">", "</a>", "<ul><li>", "</li></ul>", "<br>", "&amp;", "&lt;", "&#x", "&#999999999;", "<", ">", "<!--", "-->", "<script>", "<img src=x>", "<ol>", "<h1>", "<input type=\"checkbox\" checked>" };
            Fuzz.Run(202, N, (f, i) =>
            {
                string html = string.Concat(Enumerable.Range(0, f.Next(0, 14)).Select(_ => f.Coin() ? pieces[f.Next(pieces.Length)] : f.Text(2)));
                NoteDocument doc = NoteHtml.Parse(html);
                string again = NoteHtml.Write(doc);
                Assert.AreEqual(again, NoteHtml.Write(NoteHtml.Parse(again)), "writing is stable after one round trip");
                string plain = NoteHtml.PlainText(html);
                Assert.IsFalse(plain.Contains("<p>") || plain.Contains("<script>"), plain);
                ToDoHtml.PlainText(html);
            });
        }

        [TestMethod]
        public void EventFragments_TypedTextNeverBreaksThem()
        {
            Fuzz.Run(203, N, (f, i) =>
            {
                string key = f.Any() ?? "k", value = f.Any();
                if (key == "n") key = "k";   // the second member's own key
                string fragment = EventPayload.Members(new KeyValuePair<string, object>(key, value), new KeyValuePair<string, object>("n", f.Next(0, 9)));
                JObject o = EventPayload.Properties(fragment);
                Assert.IsNotNull(o, fragment);
                Assert.AreEqual(value, (string)o[key]);
                EventPayload.Properties(f.Any());
            });
        }

        [TestMethod]
        public void WebviewPayloads_CarryAnyTextAsOneString()
        {
            Fuzz.Run(204, N, (f, i) =>
            {
                string text = f.Any() ?? "";
                string script = new WebviewPayload().Add("goal", text).AddJson("tasks", f.Any()).WithRequestId(f.Any()).WaitAndCall("loadIt");
                Assert.IsFalse(script.Contains("\n") || script.Contains("\r") || script.Contains("\u2028"), "one line");
                int start = script.IndexOf("loadIt(") + "loadIt(".Length, end = script.LastIndexOf("); clearInterval");
                JObject arg = JObject.Parse(script.Substring(start, end - start));
                Assert.AreEqual(text, (string)arg["goal"]);
            });
        }

        [TestMethod]
        public void FirmwareIni_AndImageHeaders_NeverThrow()
        {
            Fuzz.Run(205, N, (f, i) =>
            {
                string ini = string.Join("\n", Enumerable.Range(0, f.Next(0, 8)).Select(_ =>
                    f.Coin() ? "[" + f.Text(2) + "]" : (f.Coin() ? "program_version" : f.Text(1)) + "=" + f.Text(3)));
                var parsed = FirmwareBoard.ParseIni(ini);
                FirmwareRelease r = FirmwareBoard.ReleaseFor(parsed, f.Coin() ? "M50" : f.Any());
                if (r != null) Assert.IsTrue(r.HardwareCode == "M50" || r.HardwareCode == "C30");
                var image = new byte[f.Next(0, 40)];
                for (int k = 0; k < image.Length; k++) image[k] = (byte)f.Next(256);
                FirmwareBoard.ImageFitsChip(image, f.Any());
            });
        }

        [TestMethod]
        public void TypedPostponeMinutes_AreAlwaysInRange()
        {
            Fuzz.Run(206, N, (f, i) =>
            {
                string text = i % 4 == 0 ? f.Next(0, 100000).ToString() : f.Any();
                PostponeMinutesInput kind = PostponeMinutes.Parse(text, out int minutes);
                if (kind != PostponeMinutesInput.Invalid) Assert.IsTrue(minutes >= 1 && minutes <= PostponeMinutes.Max, minutes.ToString());
                int c = PostponeMinutes.Clamp(f.Next(int.MinValue, int.MaxValue));
                Assert.IsTrue(c >= 1 && c <= PostponeMinutes.Max);
            });
        }

        [TestMethod]
        public void BrainDumpDraftRule_AndSaveRules_NeverThrow()
        {
            Fuzz.Run(207, N, (f, i) =>
            {
                string a = f.Any(), b = f.Any();
                BrainDumpDraft.ShouldRestore(a, b);
                BrainDumpSaveRules.NextSave(f.Coin(), a, b, f.Coin());
                BrainDumpSaveRules.ShouldSchedule(a, b);
            });
        }
    }
}
