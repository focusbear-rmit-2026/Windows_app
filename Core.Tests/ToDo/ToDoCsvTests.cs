using System.Collections.Generic;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoCsvTests
    {
        [TestMethod]
        public void Build_QuotesEveryCellAndFlattensHtmlTagsAndSubtasks()
        {
            var task = new ToDoTask
            {
                Title = "Say \"hi\"",
                Details = "<p>Line one</p><p>Line &amp; two</p>",
                Status = ToDoStatusWire.InProgress,
                Tags = new List<ToDoTag> { new ToDoTag("p1", "Work"), new ToDoTag("p2", "Home") },
                Subtasks = new List<ToDoSubtask> { new ToDoSubtask("a", false), new ToDoSubtask("b", true) },
                DueDate = "2026-09-25",
                CreatedAt = "2026-09-01T00:00:00Z",
            };

            string csv = ToDoCsv.Build(new[] { task }, new[] { "Title", "Details", "Objective", "Status", "Quadrant", "Project", "Subtasks", "Due", "Created" },
                                       t => "Doing", t => "Do");

            string[] lines = csv.Split('\n');
            Assert.AreEqual("\"Title\",\"Details\",\"Objective\",\"Status\",\"Quadrant\",\"Project\",\"Subtasks\",\"Due\",\"Created\"", lines[0]);
            StringAssert.StartsWith(lines[1], "\"Say \"\"hi\"\"\",\"Line one");
            StringAssert.Contains(csv, "Line & two");
            StringAssert.EndsWith(csv, "\"Doing\",\"Do\",\"Work,Home\",\"a,b\",\"2026-09-25\",\"" + LocalDay("2026-09-01T00:00:00Z") + "\"");
        }

        private static readonly string[] Headers = { "Title", "Details", "Objective", "Status", "Quadrant", "Project", "Subtasks", "Due", "Created" };

        private static string LocalDay(string iso) => System.DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture)
                                                            .LocalDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        private static string[] Cells(string line) => line.Substring(1, line.Length - 2).Split(new[] { "\",\"" }, System.StringSplitOptions.None);

        [TestMethod]
        public void Build_WritesDatesAsTheLocalDayTheyMean_NotTheRawUtcInstant()
        {
            // The server stores a due date as a timestamp. "2026-09-30T14:00:00.000Z" is 1 October in Sydney
            // — the day the list shows — and the web exports the local day too. Writing the instant through
            // put the previous day in the file for everyone east of UTC.
            var task = new ToDoTask { Title = "t", DueDate = "2026-09-30T14:00:00.000Z", CreatedAt = "2026-09-01T23:30:00.000Z" };

            string[] cells = Cells(ToDoCsv.Build(new[] { task }, Headers, t => "", t => "").Split('\n')[1]);

            Assert.AreEqual(LocalDay("2026-09-30T14:00:00.000Z"), cells[7]);
            Assert.AreEqual(LocalDay("2026-09-01T23:30:00.000Z"), cells[8]);
        }

        [TestMethod]
        public void Build_UndatedAndUnreadableDatesStayBlankOrAsSent()
        {
            var task = new ToDoTask { Title = "t", DueDate = null, CreatedAt = "not a date" };

            string[] cells = Cells(ToDoCsv.Build(new[] { task }, Headers, t => "", t => "").Split('\n')[1]);

            Assert.AreEqual("", cells[7]);
            Assert.AreEqual("not a date", cells[8], "a value we cannot read is not ours to drop");
        }

        [TestMethod]
        public void Build_NeverWritesACellASpreadsheetWouldRunAsAFormula()
        {
            // Titles come from shared projects and integrations, not only from the user. A cell starting with
            // = + - @ (or a tab / CR) is a formula to Excel and Sheets — the classic CSV injection.
            var task = new ToDoTask
            {
                Title = "=HYPERLINK(\"http://evil\")",
                Details = "+1 more",
                Objective = "@SUM(A1)",
                Tags = new List<ToDoTag> { new ToDoTag("p", "-cmd") },
                Subtasks = new List<ToDoSubtask> { new ToDoSubtask("\tsneaky", false) },
            };

            string[] cells = Cells(ToDoCsv.Build(new[] { task }, Headers, t => "", t => "").Split('\n')[1]);

            Assert.AreEqual("'=HYPERLINK(\"\"http://evil\"\")", cells[0]);
            Assert.AreEqual("'+1 more", cells[1]);
            Assert.AreEqual("'@SUM(A1)", cells[2]);
            Assert.AreEqual("'-cmd", cells[5]);
            Assert.AreEqual("'\tsneaky", cells[6]);
        }

        [TestMethod]
        public void Build_LeavesOrdinaryTextAlone()
        {
            var task = new ToDoTask { Title = "Plan Q4 = big", Details = "a - b" };

            string[] cells = Cells(ToDoCsv.Build(new[] { task }, Headers, t => "", t => "").Split('\n')[1]);

            Assert.AreEqual("Plan Q4 = big", cells[0]);
            Assert.AreEqual("a - b", cells[1]);
        }
    }
}
