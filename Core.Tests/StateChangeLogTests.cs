using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class StateChangeLogTests
    {
        [TestMethod]
        public void ARepeatedTickLineIsLoggedOncePerStayInAState()
        {
            var log = new StateChangeLog();
            int written = 0;
            for (int tick = 0; tick < 720; tick++)   // an hour of 5 s ticks in the evening routine
            {
                log.Observe("EVENING_HABITS");
                if (log.ShouldLog("EVENING_HABITS STATE - Return from timer")) written++;
            }
            Assert.AreEqual(1, written);

            log.Observe("SHUT_OFF");
            Assert.IsTrue(log.ShouldLog("SHUT_OFF STATE - Return from timer"), "a new state logs");
            log.Observe("EVENING_HABITS");
            Assert.IsTrue(log.ShouldLog("EVENING_HABITS STATE - Return from timer"), "coming back logs again");
        }
    }
}
