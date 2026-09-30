using System;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>The project card's due-date popover: presets, the wire value, and what counts as a change.</summary>
    [TestClass]
    public class ProjectDueDateEditTests
    {
        // Fri 25 Sep 2026, late in the evening: presets are whole local days, whatever the time.
        private static readonly DateTime Now = new DateTime(2026, 9, 25, 23, 30, 0);

        [TestMethod]
        public void QuickOptions_OfferClearOnlyWhenThereIsADate()
        {
            CollectionAssert.AreEqual(new[] { DueDateQuickOption.Today, DueDateQuickOption.Tomorrow, DueDateQuickOption.NextWeek },
                                      ProjectDueDateEdit.QuickOptions(null).ToArray());
            CollectionAssert.AreEqual(new[] { DueDateQuickOption.Today, DueDateQuickOption.Tomorrow, DueDateQuickOption.NextWeek },
                                      ProjectDueDateEdit.QuickOptions("").ToArray());
            CollectionAssert.AreEqual(new[] { DueDateQuickOption.Today, DueDateQuickOption.Tomorrow, DueDateQuickOption.NextWeek, DueDateQuickOption.Clear },
                                      ProjectDueDateEdit.QuickOptions("2026-09-23").ToArray());
        }

        [TestMethod]
        public void DateFor_IsTheLocalDay_NextWeekBeingSevenDaysOn()
        {
            Assert.AreEqual(new DateTime(2026, 9, 25), ProjectDueDateEdit.DateFor(DueDateQuickOption.Today, Now));
            Assert.AreEqual(new DateTime(2026, 9, 26), ProjectDueDateEdit.DateFor(DueDateQuickOption.Tomorrow, Now));
            Assert.AreEqual(new DateTime(2026, 10, 2), ProjectDueDateEdit.DateFor(DueDateQuickOption.NextWeek, Now));
            Assert.IsNull(ProjectDueDateEdit.DateFor(DueDateQuickOption.Clear, Now));
        }

        [TestMethod]
        public void DateFor_CrossesMonthAndYearEnds()
        {
            var newYearsEve = new DateTime(2026, 12, 31, 9, 0, 0);
            Assert.AreEqual(new DateTime(2027, 1, 1), ProjectDueDateEdit.DateFor(DueDateQuickOption.Tomorrow, newYearsEve));
            Assert.AreEqual(new DateTime(2027, 1, 7), ProjectDueDateEdit.DateFor(DueDateQuickOption.NextWeek, newYearsEve));
        }

        [TestMethod]
        public void WireValue_IsTheDayOrNull()
        {
            Assert.AreEqual("2026-09-25", ProjectDueDateEdit.WireValue(Now));
            Assert.AreEqual("2026-01-05", ProjectDueDateEdit.WireValue(new DateTime(2026, 1, 5)));
            Assert.IsNull(ProjectDueDateEdit.WireValue(null));
        }

        [TestMethod]
        public void IsUnchanged_ComparesDaysNotStrings()
        {
            Assert.IsTrue(ProjectDueDateEdit.IsUnchanged("2026-09-23", new DateTime(2026, 9, 23)));
            Assert.IsTrue(ProjectDueDateEdit.IsUnchanged(null, null));
            Assert.IsTrue(ProjectDueDateEdit.IsUnchanged("", null));
            Assert.IsFalse(ProjectDueDateEdit.IsUnchanged("2026-09-23", new DateTime(2026, 9, 24)));
            Assert.IsFalse(ProjectDueDateEdit.IsUnchanged("2026-09-23", null));
            Assert.IsFalse(ProjectDueDateEdit.IsUnchanged(null, new DateTime(2026, 9, 23)));
            // An ISO instant means the local day it falls on.
            var local = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Local);
            string iso = local.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            Assert.IsTrue(ProjectDueDateEdit.IsUnchanged(iso, new DateTime(2026, 9, 23)));
        }

        [TestMethod]
        public void CalendarStart_IsTheCurrentDueDateElseToday()
        {
            Assert.AreEqual(new DateTime(2026, 11, 3), ProjectDueDateEdit.CalendarStart("2026-11-03", Now));
            Assert.AreEqual(new DateTime(2026, 9, 25), ProjectDueDateEdit.CalendarStart(null, Now));
            Assert.AreEqual(new DateTime(2026, 9, 25), ProjectDueDateEdit.CalendarStart("not a date", Now));
        }

        [TestMethod]
        public void DaysUntilDue_CountsCalendarDays()
        {
            Assert.AreEqual(0, ProjectDueDateEdit.DaysUntilDue(new DateTime(2026, 9, 25), Now));
            Assert.AreEqual(7, ProjectDueDateEdit.DaysUntilDue(new DateTime(2026, 10, 2), Now));
            Assert.AreEqual(-2, ProjectDueDateEdit.DaysUntilDue(new DateTime(2026, 9, 23), Now));
            Assert.IsNull(ProjectDueDateEdit.DaysUntilDue(null, Now));
        }
    }
}
