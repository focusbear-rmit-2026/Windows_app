using Focus_Bear.Habits;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Resuming a postponed habit read the saved remaining time with int.Parse. A corrupt, empty-but-spaced or
    /// negative registry value threw (caught, leaving the habit in a half-started state) instead of simply
    /// starting the habit from its full duration.
    /// </summary>
    [TestClass]
    public class HabitSavedTimerTests
    {
        [TestMethod]
        public void AGoodValueIsUsed()
        {
            Assert.AreEqual(125, HabitActivityOnGoing.SavedTimerSeconds("125"));
            Assert.AreEqual(125, HabitActivityOnGoing.SavedTimerSeconds(" 125 "));
        }

        [TestMethod]
        public void AnythingElseMeansStartFromTheFullDuration()
        {
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds(null));
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds(""));
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds("abc"));
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds("12.5"));
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds("-3"));
            Assert.IsNull(HabitActivityOnGoing.SavedTimerSeconds("99999999999"));
        }
    }
}
