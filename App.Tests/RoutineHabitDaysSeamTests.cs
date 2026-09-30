using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Core's RoutineHabitSchedule tests prove the day rule. These prove the app's mapping onto it: the
    /// WPF habit's list, the wall-clock weekday of a DateTime, and the empty-list default. The Day Plan,
    /// the tray menu and all four routine-player paths in App.xaml.cs now call
    /// <see cref="RoutineHabitDays"/>, which is what keeps the plan and the player from disagreeing.
    /// </summary>
    [TestClass]
    public class RoutineHabitDaysSeamTests
    {
        // 2026-09-21 is a Monday.
        private static readonly DateTime Monday = new DateTime(2026, 9, 21, 7, 0, 0, DateTimeKind.Local);
        private static readonly DateTime[] OneWeek = Enumerable.Range(0, 7).Select(offset => Monday.AddDays(offset)).ToArray();

        private static habit Habit(string id, params string[] days)
        {
            return new habit { id = id, name = id, DaysOfWeek = days == null ? null : days.ToList() };
        }

        [TestMethod]
        public void AWeekdaysHabitRunsMondayToFridayOnly()
        {
            // The routine player only understood "all" and exact day codes, so this habit sat in the
            // Day Plan every weekday and never played.
            habit weekdays = Habit("h", "weekdays");

            bool[] runs = OneWeek.Select(day => RoutineHabitDays.IsScheduledOn(weekdays, day)).ToArray();

            CollectionAssert.AreEqual(new[] { true, true, true, true, true, false, false }, runs);
        }

        [TestMethod]
        public void AWeekendHabitRunsSaturdayAndSundayOnly()
        {
            habit weekend = Habit("h", "weekend");

            bool[] runs = OneWeek.Select(day => RoutineHabitDays.IsScheduledOn(weekend, day)).ToArray();

            CollectionAssert.AreEqual(new[] { false, false, false, false, false, true, true }, runs);
        }

        [TestMethod]
        public void TheServersDayCodesMatchTheirOwnDayOnly()
        {
            habit mondays = Habit("h", "MON");

            Assert.IsTrue(RoutineHabitDays.IsScheduledOn(mondays, Monday));
            Assert.IsFalse(RoutineHabitDays.IsScheduledOn(mondays, Monday.AddDays(1)));
            Assert.IsTrue(RoutineHabitDays.IsScheduledOn(Habit("h", "mon"), Monday), "case is not significant");
        }

        [TestMethod]
        public void MondayIsMondayWhateverTheMachinesCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                // ar-SA formats dates on the Umm al-Qura calendar; es-ES names the day "lunes".
                foreach (string culture in new[] { "ar-SA", "es-ES", "ja-JP" })
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                    Assert.IsTrue(RoutineHabitDays.IsScheduledOn(Habit("h", "MON"), Monday), culture);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void TheWeekdayIsTheOneOnTheClockThatWasPassed()
        {
            // A late-evening UTC value must not be converted into Tuesday somewhere east of Greenwich.
            var lateMondayUtc = new DateTime(2026, 9, 21, 23, 30, 0, DateTimeKind.Utc);

            Assert.IsTrue(RoutineHabitDays.IsScheduledOn(Habit("h", "MON"), lateMondayUtc));
        }

        [TestMethod]
        public void AHabitWithNoDaysRunsEveryDay()
        {
            // The server's reading: it filters a routine with `days_of_week ?? [ALL]` and will not store an
            // empty list. Before, the Day Plan showed such a habit and the routine player dropped it.
            foreach (habit noDays in new[] { Habit("null", null), Habit("empty") })
            {
                Assert.IsTrue(OneWeek.All(day => RoutineHabitDays.IsScheduledOn(noDays, day)), noDays.id);
            }
        }

        [TestMethod]
        public void TodaysRoutineKeepsItsOrder()
        {
            var routine = new List<habit>
            {
                Habit("every-day", "all"),
                Habit("weekend", "weekend"),
                Habit("weekdays", "weekdays"),
                Habit("mondays", "MON"),
                Habit("tuesdays", "TUE"),
            };

            string[] monday = RoutineHabitDays.ScheduledOn(routine, Monday).Select(h => h.id).ToArray();

            CollectionAssert.AreEqual(new[] { "every-day", "weekdays", "mondays" }, monday);
        }

        [TestMethod]
        public void TheListAndTheSingleHabitQuestionNeverDisagree()
        {
            // The player asks per habit (RoutineHasRemainingHabits) and per list (ShowMorningRoutine); the
            // Day Plan asks per list. One rule behind both means one answer for every list on every day.
            string[][] lists =
            {
                null, new string[0], new[] { "all" }, new[] { "weekdays" }, new[] { "weekend" },
                new[] { "MON", "WED" }, new[] { "sun" }, new[] { "weekdays", "SAT" },
            };

            foreach (string[] days in lists)
            {
                habit h = Habit("h", days);
                foreach (DateTime day in OneWeek)
                {
                    bool inList = RoutineHabitDays.ScheduledOn(new[] { h }, day).Count == 1;
                    Assert.AreEqual(RoutineHabitDays.IsScheduledOn(h, day), inList,
                                    (days == null ? "null" : string.Join(",", days)) + " on " + day.DayOfWeek);
                }
            }
        }

        [TestMethod]
        public void NoRoutineAtAllIsAnEmptyDay()
        {
            Assert.AreEqual(0, RoutineHabitDays.ScheduledOn(null, Monday).Count);
        }
    }
}
