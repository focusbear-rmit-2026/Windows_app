using System;
using System.Collections.Generic;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// RegistryManager write-then-read round trips, one per kind of stored value: dates (Unix seconds), a routine
    /// postpone (date + enum + flags), string pairs, plain strings, bools, ints and a JSON blob. The test host has
    /// its own registry root (HKCU\Software\Focus Bear Tests), never the live app's.
    /// </summary>
    [TestClass]
    public class RegistryRoundTripSeamTests
    {
        private static readonly AppSettings S = AppSettings.GetInstance;

        [ClassInitialize]
        public static void OnlyTheTestRoot(TestContext _)
        {
            Assert.IsTrue(FocusBear.Core.AppInstance.IsTestHost, "never run these against the live registry");
            StringAssert.EndsWith(E2EBuild.RegistryRoot, "Focus Bear Tests");
        }

        private static DateTime Seconds(DateTime d) => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, d.Kind);

        [TestMethod]
        public void AHabitsPostpone_RoundTrips()
        {
            DateTime savedEnd = S.HabitsPostponeEndTime;
            var savedType = S.HabitsPostponeActivityType;
            bool savedAfter = S.bEveningHabitsPostponedAfterCutoffTime, savedFs = S.bHabitsPostponeWasFocusSummary;
            try
            {
                DateTime end = Seconds(DateTime.Now.AddMinutes(25));
                S.HabitsPostponeEndTime = end;
                S.HabitsPostponeActivityType = CurrentActivityType.EVENING_HABITS;
                S.bEveningHabitsPostponedAfterCutoffTime = true;
                S.bHabitsPostponeWasFocusSummary = false;
                RegistryManager.SetHabitsPostponeEndTime();

                S.HabitsPostponeEndTime = DateTime.MinValue;
                S.HabitsPostponeActivityType = CurrentActivityType.NONE;
                S.bEveningHabitsPostponedAfterCutoffTime = false;
                S.bHabitsPostponeWasFocusSummary = true;

                Assert.IsTrue(RegistryManager.GetHabitsPostponeEndTime());
                Assert.AreEqual(end.Ticks, S.HabitsPostponeEndTime.Ticks, "the wall-clock end time comes back unchanged");
                Assert.AreEqual(CurrentActivityType.EVENING_HABITS, S.HabitsPostponeActivityType);
                Assert.IsTrue(S.bEveningHabitsPostponedAfterCutoffTime);
                Assert.IsFalse(S.bHabitsPostponeWasFocusSummary);

                RegistryManager.RemoveHabitsPostponeEndTime();
                Assert.IsFalse(RegistryManager.GetHabitsPostponeEndTime(), "removed means nothing to restore");
            }
            finally
            {
                RegistryManager.RemoveHabitsPostponeEndTime();
                S.HabitsPostponeEndTime = savedEnd;
                S.HabitsPostponeActivityType = savedType;
                S.bEveningHabitsPostponedAfterCutoffTime = savedAfter;
                S.bHabitsPostponeWasFocusSummary = savedFs;
            }
        }

        [TestMethod]
        public void TillDates_RoundTrip()
        {
            DateTime savedShut = S.ShutOFFAbortedTillDate, savedDeact = S.AppDeactivatedTillDate;
            try
            {
                DateTime t = Seconds(DateTime.Now.AddHours(3));
                S.ShutOFFAbortedTillDate = t;
                RegistryManager.SetShutOffAbortedTillDate();
                S.ShutOFFAbortedTillDate = DateTime.MinValue;
                Assert.IsTrue(RegistryManager.GetShutOffAbortedTillDate());
                Assert.AreEqual(t.Ticks, S.ShutOFFAbortedTillDate.Ticks);
                Assert.AreEqual(DateTimeKind.Local, S.ShutOFFAbortedTillDate.Kind, "a local wall-clock time, not labelled UTC");
                RegistryManager.RemoveShutOffAbortedTillDate();
                Assert.IsFalse(RegistryManager.GetShutOffAbortedTillDate());

                S.AppDeactivatedTillDate = t;
                RegistryManager.SetAppDeactivatedTillDate();
                S.AppDeactivatedTillDate = DateTime.MinValue;
                Assert.IsTrue(RegistryManager.GetAppDeactivatedTillDate());
                Assert.AreEqual(t.Ticks, S.AppDeactivatedTillDate.Ticks);
                RegistryManager.RemoveAppDeactivatedTillDate();
                Assert.IsFalse(RegistryManager.GetAppDeactivatedTillDate());
            }
            finally
            {
                RegistryManager.RemoveShutOffAbortedTillDate();
                RegistryManager.RemoveAppDeactivatedTillDate();
                S.ShutOFFAbortedTillDate = savedShut;
                S.AppDeactivatedTillDate = savedDeact;
            }
        }

        [TestMethod]
        public void LastActiveUtc_AndBreaksOffToday_RoundTrip()
        {
            DateTime utc = Seconds(DateTime.UtcNow);
            RegistryManager.SetLastActiveUtc(utc);
            Assert.AreEqual(utc, RegistryManager.GetLastActiveUtc());
            Assert.AreEqual(DateTimeKind.Utc, RegistryManager.GetLastActiveUtc().Value.Kind);

            DateTime now = DateTime.Now;
            RegistryManager.SaveBreaksOffToday(now);
            Assert.AreEqual(now, RegistryManager.GetBreaksOffToday());
        }

        [TestMethod]
        public void APostponedHabitsTimer_RoundTrips_AndIsRemoved()
        {
            try
            {
                RegistryManager.SetPostponedHabitData("habit-7", "125");
                Assert.AreEqual("habit-7", RegistryManager.GetCurrentActivityPostponedID());
                Assert.AreEqual("125", RegistryManager.GetCurrentActivityPostponedTimerVal());
                RegistryManager.RemovePostponedHabitData();
                Assert.IsTrue(string.IsNullOrEmpty(RegistryManager.GetCurrentActivityPostponedTimerVal()));
            }
            finally
            {
                RegistryManager.RemovePostponedHabitData();
            }
        }

        [TestMethod]
        public void StringsAndBools_RoundTrip()
        {
            string scope = RegistryManager.GetNotesScope(), tab = RegistryManager.GetStatsSelectedTab(), sort = RegistryManager.GetToDoSortMode();
            bool visible = RegistryManager.GetNotesBrainDumpVisible();
            try
            {
                RegistryManager.SetNotesScope("project:é \"x\"");
                Assert.AreEqual("project:é \"x\"", RegistryManager.GetNotesScope());
                RegistryManager.SetStatsSelectedTab("PepTalk");
                Assert.AreEqual("PepTalk", RegistryManager.GetStatsSelectedTab());
                RegistryManager.SetToDoSortMode("due");
                Assert.AreEqual("due", RegistryManager.GetToDoSortMode());

                RegistryManager.SetNotesBrainDumpVisible(!visible);
                Assert.AreEqual(!visible, RegistryManager.GetNotesBrainDumpVisible());

                RegistryManager.SaveDisableNotificationSounds(true);
                Assert.IsTrue(RegistryManager.GetDisableNotificationSounds());
                RegistryManager.SaveDisableNotificationSounds(false);
                Assert.IsFalse(RegistryManager.GetDisableNotificationSounds());
            }
            finally
            {
                if (scope != null) RegistryManager.SetNotesScope(scope);
                if (tab != null) RegistryManager.SetStatsSelectedTab(tab);
                if (sort != null) RegistryManager.SetToDoSortMode(sort);
                RegistryManager.SetNotesBrainDumpVisible(visible);
            }
        }

        [TestMethod]
        public void AnIntCounter_RoundTrips()
        {
            int saved = S.nTotalTimeForDistractingSitesToday;
            try
            {
                S.nTotalTimeForDistractingSitesToday = 4321;
                RegistryManager.SetTotalTimeForDistractingSites();
                S.nTotalTimeForDistractingSitesToday = 0;
                Assert.IsTrue(RegistryManager.GetTotalTimeForDistractingSites());
                Assert.AreEqual(4321, S.nTotalTimeForDistractingSitesToday);
            }
            finally
            {
                S.nTotalTimeForDistractingSitesToday = saved;
            }
        }

        [TestMethod]
        public void TodaysSelectedMorningHabits_RoundTripAsJson()
        {
            var saved = S.arrAllMorningHabits;
            try
            {
                S.arrAllMorningHabits = new List<habit>
                {
                    new habit { id = "h1", name = "Stretch \"long\" é", durationSeconds = 300, useTimer = true },
                    new habit { id = "h2", name = "Journal", useTimer = false },
                };
                RegistryManager.SaveSelectedMorningHabitsForToday();
                S.arrAllMorningHabits = new List<habit>();

                Assert.IsTrue(RegistryManager.GetSelectedMorningHabitsForToday());
                Assert.AreEqual(2, S.arrAllMorningHabits.Count);
                Assert.AreEqual("Stretch \"long\" é", S.arrAllMorningHabits[0].name);
                Assert.AreEqual(300, S.arrAllMorningHabits[0].durationSeconds);
                Assert.IsFalse(S.arrAllMorningHabits[1].useTimer);
            }
            finally
            {
                S.arrAllMorningHabits = saved;
            }
        }
    }
}
