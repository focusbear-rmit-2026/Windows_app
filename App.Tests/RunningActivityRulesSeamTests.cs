using System.Collections.Generic;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The running habit's allow-list as the blocking paths read it. An index past the routine's list used to throw
    /// inside the blocking handlers, which caught it and blocked nothing for the rest of the habit.
    /// </summary>
    [TestClass]
    public class RunningActivityRulesSeamTests
    {
        private AppSettings S => AppSettings.GetInstance;
        private CurrentActivityType _type;
        private bool _allowedFlag, _focusModeFlag;
        private int _index;
        private List<habit> _morning, _evening;
        private habit _playing;

        [TestInitialize]
        public void Save()
        {
            _type = S.nCurrentActivityType; _index = S.nHabitIndex; _morning = S.arrAllMorningHabits; _evening = S.arrAllEveningHabits; _playing = S.currentHabitPlaying;
            _allowedFlag = S.bHabitwithAllowedAppsRunning; _focusModeFlag = S.bHabitwithUseFocusModeRunning;
        }

        [TestCleanup]
        public void Restore()
        {
            S.nCurrentActivityType = _type; S.nHabitIndex = _index; S.arrAllMorningHabits = _morning; S.arrAllEveningHabits = _evening; S.currentHabitPlaying = _playing;
            S.bHabitwithAllowedAppsRunning = _allowedFlag; S.bHabitwithUseFocusModeRunning = _focusModeFlag;
        }

        [TestMethod]
        public void AStaleFlagIsClearedButAResolvedHabitKeepsItsRules()
        {
            S.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
            S.arrAllMorningHabits = new List<habit> { Habit("h1", new List<string> { "Code" }, new List<string>()) };
            S.bHabitwithAllowedAppsRunning = true;
            S.bHabitwithUseFocusModeRunning = true;

            S.nHabitIndex = 0;
            Assert.IsFalse(RunningActivityRules.ClearIfStale(S));
            Assert.IsTrue(S.bHabitwithAllowedAppsRunning, "the habit resolves: its rules still apply");

            S.nHabitIndex = 7;
            S.currentHabitPlaying = new habit();
            Assert.IsTrue(RunningActivityRules.ClearIfStale(S), "no habit resolves: the flag is stale");
            Assert.IsFalse(S.bHabitwithAllowedAppsRunning, "so every app is no longer minimised");
            Assert.IsFalse(S.bHabitwithUseFocusModeRunning);
            Assert.IsFalse(RunningActivityRules.ClearIfStale(S), "and it's logged once: the next event has nothing to clear");
        }

        private static habit Habit(string id, List<string> apps, List<string> urls, bool relax = false) =>
            new habit { id = id, allowedApps = apps, allowedUrls = urls, show_saved_distracting_websites = relax };

        [TestMethod]
        public void BlankEntriesAreNotRules()
        {
            S.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
            S.arrAllMorningHabits = new List<habit> { Habit("h1", new List<string> { "", "Code.exe", " " }, new List<string> { "", "https://", "docs.google.com" }) };
            S.nHabitIndex = 0;
            var rules = RunningActivityRules.For(S);
            Assert.IsTrue(rules.Known);
            CollectionAssert.AreEqual(new[] { "code" }, rules.Apps);
            CollectionAssert.AreEqual(new[] { "docs.google.com" }, rules.Urls, "a blank or bare scheme row matched every page");
        }

        [TestMethod]
        public void AnIndexPastTheListFallsBackToThePlayingHabitElseNoRules()
        {
            S.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
            S.arrAllMorningHabits = new List<habit> { Habit("h1", new List<string> { "Code" }, new List<string>()) };
            S.nHabitIndex = 5;
            S.currentHabitPlaying = Habit("h9", new List<string> { "Kindle" }, new List<string>());
            var rules = RunningActivityRules.For(S);
            Assert.IsTrue(rules.Known);
            CollectionAssert.AreEqual(new[] { "kindle" }, rules.Apps);

            S.currentHabitPlaying = new habit();
            rules = RunningActivityRules.For(S);
            Assert.IsFalse(rules.Known, "nothing is allowed rather than everything");
            Assert.AreEqual(0, rules.Apps.Count);
            Assert.AreEqual("MORNING_HABITS", rules.Label);
        }

        [TestMethod]
        public void TheRelaxCheckIsBoundsCheckedAndIgnoresAStalePlayingHabit()
        {
            S.nCurrentActivityType = CurrentActivityType.EVENING_HABITS;
            S.arrAllEveningHabits = new List<habit> { Habit("e1", new List<string>(), new List<string>(), relax: true) };
            S.currentHabitPlaying = Habit("e1", new List<string>(), new List<string>(), relax: true);
            S.nHabitIndex = 0;
            Assert.IsTrue(RunningActivityRules.IsRelaxHabitRunning(S));
            S.nHabitIndex = 3;
            Assert.IsFalse(RunningActivityRules.IsRelaxHabitRunning(S));
            S.nHabitIndex = -1;
            Assert.IsFalse(RunningActivityRules.IsRelaxHabitRunning(S));
        }
    }
}
