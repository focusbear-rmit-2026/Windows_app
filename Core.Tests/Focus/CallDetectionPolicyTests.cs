using FocusBear.Core.Focus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Focus
{
    [TestClass]
    public class CallDetectionPolicyTests
    {
        [TestMethod]
        public void TheCallCheckRunsOnlyWhenItCouldFindACall()
        {
            Assert.IsTrue(CallDetectionPolicy.ShouldCheck(true, new[] { "Teams.exe", "zoom.exe" }));
            Assert.IsFalse(CallDetectionPolicy.ShouldCheck(false, new[] { "Teams.exe" }), "meetings detection is off");
            Assert.IsFalse(CallDetectionPolicy.ShouldCheck(true, null), "no Meetings focus mode");
            Assert.IsFalse(CallDetectionPolicy.ShouldCheck(true, new string[0]), "a Meetings mode that allows no apps");
            Assert.IsFalse(CallDetectionPolicy.ShouldCheck(true, new[] { " ", "" }));
        }
    }
}
