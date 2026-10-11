using System;
using System.Globalization;
using System.Threading;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class AiDecisionCsvTests
    {
        private static readonly DateTime At = new DateTime(2026, 9, 27, 1, 2, 3, DateTimeKind.Utc);

        [TestMethod]
        public void QuotesCommasAndLineBreaksStayInsideTheirColumn()
        {
            string row = AiDecisionCsv.Row(At, "https://example.com/a?b=1,2", "example.com", "\"Best\" deals, today",
                0.35, "", "", "Write the \"Q3\" report", "The page is about \"shopping\",\nnot the report");

            string[] fields = AiDecisionCsv.Parse(row);
            Assert.AreEqual(9, fields.Length, row);
            Assert.AreEqual("\"Best\" deals, today", fields[3]);
            Assert.AreEqual("0.35", fields[4]);
            Assert.AreEqual("Write the \"Q3\" report", fields[7]);
            Assert.AreEqual("The page is about \"shopping\", not the report", fields[8]);
            Assert.IsFalse(row.Contains("\n"), "one row per line");
        }

        [TestMethod]
        public void TheScoreIsWrittenTheSameInEveryCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");   // 0,35 would be two columns
                Assert.AreEqual("0.35", AiDecisionCsv.Parse(AiDecisionCsv.Row(At, "u", "d", "t", 0.35, null, null, null, null))[4]);
                Assert.AreEqual("2026-09-27T01:02:03.0000000Z", AiDecisionCsv.Parse(AiDecisionCsv.Row(At, "u", "d", "t", 1, null, null, null, null))[0]);
            }
            finally { Thread.CurrentThread.CurrentCulture = original; }
        }
    }
}
