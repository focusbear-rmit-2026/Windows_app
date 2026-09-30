using System.Collections.Generic;
using Focus_Bear.Habits;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The "set your intention" screen's start button names the routine's first habit (the one it starts),
    /// but chose the timer / no-timer wording from the habit at nHabitIndex - a stale index. A no-timer first
    /// habit after a timed one read "Start Journal (0 min)"; at index -1 (after the Focus Summary) the lookup
    /// threw and the button kept its placeholder text.
    /// </summary>
    [TestClass]
    public class HabitExtraPersonalTitleTests
    {
        private const string Timed = "Start <NAME> (<TIME>)";
        private const string Untimed = "Start <NAME>";

        private static string Title(List<habit> routine) =>
            HabitExtraPersonal.StartButtonTitle(routine, Timed, Untimed, s => (s / 60) + " min");

        [TestMethod]
        public void TheWordingFollowsTheFirstHabit()
        {
            var routine = new List<habit>
            {
                new habit { name = "Journal", useTimer = false },
                new habit { name = "Run", useTimer = true, durationSeconds = 600 },
            };
            Assert.AreEqual("Start Journal", Title(routine));

            routine.Reverse();
            Assert.AreEqual("Start Run (10 min)", Title(routine));
        }

        [TestMethod]
        public void AnEmptyRoutineHasNoTitle()
        {
            Assert.IsNull(Title(new List<habit>()));
            Assert.IsNull(Title(null));
        }
    }
}
