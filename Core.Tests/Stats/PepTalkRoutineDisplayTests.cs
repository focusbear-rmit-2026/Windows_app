using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    [TestClass]
    public class PepTalkRoutineDisplayTests
    {
        [TestMethod]
        public void OncePerRoutinePerDay()
        {
            var display = new PepTalkRoutineDisplay();
            var morning = new DateTime(2026, 9, 25, 7, 30, 0);
            Assert.IsTrue(display.ShouldShow("MORNING_HABITS", morning));
            display.Record("MORNING_HABITS", morning);
            Assert.IsFalse(display.ShouldShow("MORNING_HABITS", morning.AddMinutes(20)), "a habit later, same routine");
            Assert.IsFalse(display.ShouldShow("MORNING_HABITS", morning.AddHours(3)), "postponed and resumed");
            Assert.IsTrue(display.ShouldShow("EVENING_HABITS", morning.AddHours(12)), "the evening routine gets its own");
            Assert.IsTrue(display.ShouldShow("MORNING_HABITS", morning.AddDays(1)), "tomorrow's morning");
            Assert.IsFalse(display.ShouldShow("", morning));
        }
    }
}
