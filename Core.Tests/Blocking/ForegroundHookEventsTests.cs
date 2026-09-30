using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class ForegroundHookEventsTests
    {
        [TestMethod]
        public void TheHookTakesOnlyTheEventsTheAppActsOn()
        {
            Assert.IsTrue(ForegroundHookEvents.IsHooked(ForegroundHookEvents.SystemForeground), "app switch");
            Assert.IsTrue(ForegroundHookEvents.IsHooked(ForegroundHookEvents.ObjectFocus), "app switch, browser focus");
            Assert.IsTrue(ForegroundHookEvents.IsHooked(ForegroundHookEvents.ObjectValueChange), "the address bar's URL");

            // Measured on 26 Sep over 20 s: 1,096 state, 1,183 name and 209 location changes, against 30 that mattered.
            Assert.IsFalse(ForegroundHookEvents.IsHooked(ForegroundHookEvents.ObjectStateChange));
            Assert.IsFalse(ForegroundHookEvents.IsHooked(ForegroundHookEvents.ObjectNameChange));
            Assert.IsFalse(ForegroundHookEvents.IsHooked(ForegroundHookEvents.ObjectLocationChange));
        }

        [TestMethod]
        public void OnlyAValueChangeIsReadForAUrl()
        {
            Assert.IsTrue(ForegroundHookEvents.CarriesUrl(0x800E));
            Assert.IsFalse(ForegroundHookEvents.CarriesUrl(0x8005));
        }
    }
}
