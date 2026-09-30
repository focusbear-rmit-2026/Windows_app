using System;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>: the routine's "get focused" prompt was gated only by
    /// an in-memory flag, so restarting a routine — or the app — showed the star rating and "what's
    /// motivating you today?" again, several times in one evening (report 2026-09-13_20-30-50).
    /// </summary>
    [TestClass]
    public class RoutineVisualizePromptTests
    {
        [TestMethod]
        public void VisualizePromptIsShownOncePerRoutinePerDay()
        {
            Assert.IsTrue(RoutineVisualizePrompt.ShouldShow(null, "2026-09-13"), "Never shown: ask");
            Assert.IsTrue(RoutineVisualizePrompt.ShouldShow(string.Empty, "2026-09-13"));
            Assert.IsFalse(RoutineVisualizePrompt.ShouldShow("2026-09-13", "2026-09-13"),
                           "Already answered today — restarting the routine must not ask again");
            Assert.IsTrue(RoutineVisualizePrompt.ShouldShow("2026-09-13", "2026-09-14"), "A new day asks again");
        }

        [TestMethod]
        public void VisualizePromptKeepsCustomRoutinesApart()
        {
            Assert.AreEqual("routineVisualizeShownDay.morning", RoutineVisualizePrompt.StorageKey.Morning);
            Assert.AreEqual("routineVisualizeShownDay.evening", RoutineVisualizePrompt.StorageKey.Evening);
            Assert.AreNotEqual(RoutineVisualizePrompt.StorageKey.Custom("a"),
                               RoutineVisualizePrompt.StorageKey.Custom("b"),
                               "Answering one custom routine must not silence another");
        }

        [TestMethod]
        public void TheDayKeyIsTheSameOneEveryPerDayStoreUses()
        {
            DateTimeOffset day = new DateTimeOffset(2026, 9, 13, 20, 30, 0, TimeSpan.FromHours(10));
            Assert.AreEqual("2026-09-13", RoutineVisualizePrompt.DayKey(day));
            Assert.AreEqual(DayPlanDayKey.For(day), RoutineVisualizePrompt.DayKey(day));
        }
    }
}
