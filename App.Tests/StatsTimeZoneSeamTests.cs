using Focus_Bear.StatsScreens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    [TestClass]
    public class StatsTimeZoneSeamTests
    {
        [TestMethod]
        public void TheWindowsToIanaTable_Loads_AndGivesThisMachineAZone()
        {
            // The table is a static dictionary: one duplicated Windows id would throw on first use and take the
            // Stats screen down with it.
            string zone = TimeZoneInfoToIana.Current();
            Assert.IsFalse(string.IsNullOrEmpty(zone));
        }
    }
}
