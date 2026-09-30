using System;
using System.Globalization;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The typed-input parsers run on every keystroke (the due-date field's preview, the quick-add line), so
    /// input they cannot read must come back as "not understood" — never an exception on the UI thread.
    /// </summary>
    [TestClass]
    public class TypedInputRobustnessTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 10, 0, 0);
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        [TestMethod]
        public void DueDate_FullWidthAndOtherScriptDigits_ReadAsTheNumberTheyAre()
        {
            // .NET's \d matches every Unicode decimal digit, and int.Parse then threw on "２５" — the digits a
            // Japanese IME types — and on Arabic-Indic "٢٥/٩".
            Assert.AreEqual(new DateTime(2026, 9, 25), ToDoDueDateParser.Parse("２５", Now, English));
            Assert.AreEqual(new DateTime(2026, 9, 25), ToDoDueDateParser.Parse("٢٥/٩", Now, English));
            Assert.AreEqual(new DateTime(2026, 9, 10), ToDoDueDateParser.Parse("３d", Now, English));
        }

        [TestMethod]
        public void DueDate_AnOffsetPastTheCalendar_IsNotADate_NotACrash()
        {
            Assert.IsNull(ToDoDueDateParser.Parse("99999999d", Now, English));
            Assert.IsNull(ToDoDueDateParser.Parse("-99999999d", Now, English));
            Assert.IsNull(ToDoDueDateParser.Parse("20000y", Now, English));
            Assert.IsNull(ToDoDueDateParser.Parse("999999m", Now, English));
            Assert.IsNull(ToDoDueDateParser.Parse("99999999999999d", Now, English), "too big for an int as well");
        }

        [TestMethod]
        public void QuickAdd_FullWidthDigitsDoNotThrow()
        {
            ParsedQuickAddTask parsed = QuickAddTaskParser.Parse("read ２h");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Title));

            QuickAddTaskParser.Parse("stretch ３０m");
            QuickAddTaskParser.Parse("plan ١h٣٠");
            QuickAddTaskParser.Parse("call mum *４５");
            QuickAddTaskParser.Parse("ship it !３");
        }
    }
}
