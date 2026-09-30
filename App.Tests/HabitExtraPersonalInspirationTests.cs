using Focus_Bear.Habits;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The intention screen picked its inspiration with rnd.Next(0, n - 1) - the upper bound is exclusive, so
    /// the last message never showed - and an empty list threw (n - 1 = -1), leaving the button text unset.
    /// </summary>
    [TestClass]
    public class HabitExtraPersonalInspirationTests
    {
        [TestMethod]
        public void EveryMessageCanBePicked_IncludingTheLast()
        {
            var messages = new JArray("one", "two", "three");
            int askedFor = -1;
            string picked = HabitExtraPersonal.PickInspiration(messages, n => { askedFor = n; return n - 1; });
            Assert.AreEqual(3, askedFor, "the chooser is asked for an index below the count");
            Assert.AreEqual("three", picked);
        }

        [TestMethod]
        public void NoMessages_NoPickAndNoThrow()
        {
            Assert.IsNull(HabitExtraPersonal.PickInspiration(new JArray(), n => 0));
            Assert.IsNull(HabitExtraPersonal.PickInspiration(null, n => 0));
        }
    }
}
