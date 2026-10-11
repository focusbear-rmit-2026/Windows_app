using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The blocking focus handler is now registered only while something can be blocked, and was re-evaluated only
    /// by the 5 s background tick (and two tray paths). A session started from the main window, the web, a habit, a
    /// schedule or the shut-off screen ran unblocked until the next tick. The flags that decide it now say when they
    /// change, so the subscription is updated at once.
    /// </summary>
    [TestClass]
    public class BlockingStateChangedTests
    {
        [TestMethod]
        public void EachBlockingFlagAnnouncesARealChangeOnly()
        {
            var s = AppSettings.GetInstance;
            bool focus = s.isFocusModeRunning, paused = s.isFocusModePaused, apps = s.bHabitwithAllowedAppsRunning, mode = s.bHabitwithUseFocusModeRunning;
            var activity = s.nCurrentActivityType;
            int raised = 0;
            System.Action onChange = () => raised++;
            AppSettings.BlockingStateChanged += onChange;
            try
            {
                s.isFocusModeRunning = !focus;
                s.isFocusModePaused = !paused;
                s.bHabitwithAllowedAppsRunning = !apps;
                s.bHabitwithUseFocusModeRunning = !mode;
                s.nCurrentActivityType = activity == CurrentActivityType.SHUT_OFF ? CurrentActivityType.NONE : CurrentActivityType.SHUT_OFF;
                Assert.AreEqual(5, raised);

                s.isFocusModeRunning = s.isFocusModeRunning;   // no change
                Assert.AreEqual(5, raised, "setting the same value says nothing");
            }
            finally
            {
                AppSettings.BlockingStateChanged -= onChange;
                s.isFocusModeRunning = focus;
                s.isFocusModePaused = paused;
                s.bHabitwithAllowedAppsRunning = apps;
                s.bHabitwithUseFocusModeRunning = mode;
                s.nCurrentActivityType = activity;
            }
        }
    }
}
