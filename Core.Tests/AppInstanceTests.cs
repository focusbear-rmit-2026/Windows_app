using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class AppInstanceTests
    {
        [TestMethod]
        public void ATestHostNeverUsesTheRunningAppsDataFolder()
        {
            // The test host carries no E2E marker, and it isn't the app: its logs, CSVs and queue go elsewhere.
            Assert.IsFalse(AppInstance.IsE2E);
            Assert.IsTrue(AppInstance.IsTestHost);
            Assert.AreEqual("Focus Bear Tests", AppInstance.DataFolderName);
            Assert.AreEqual("Focus Bear", AppInstance.DataFolderNameFor(false, false, false), "the app itself");
            Assert.AreEqual("FocusBear-Claude-Tasks", AppInstance.LocalReportsFolderName);
        }

        [TestMethod]
        public void OnlyTheAppsOwnExeIsNotATestHost()
        {
            Assert.IsTrue(AppInstance.IsTestHostAssembly(null));
            Assert.IsTrue(AppInstance.IsTestHostAssembly(typeof(AppInstance).Assembly), "FocusBear.Core is not the app");
            Assert.IsTrue(AppInstance.IsTestHostAssembly(Assembly.GetExecutingAssembly()));
        }

        [TestMethod]
        public void OnlyTheE2EMarkerCounts()
        {
            Assert.IsFalse(AppInstance.IsE2EAssembly(null));
            Assert.IsFalse(AppInstance.IsE2EAssembly(typeof(AppInstance).Assembly));
            Assert.IsFalse(AppInstance.IsE2EAssembly(Assembly.GetExecutingAssembly()));
        }

        [TestMethod]
        public void TheE2ELocationsAreSeparate()
        {
            Assert.AreNotEqual(AppInstance.DefaultDataFolderName, AppInstance.E2EDataFolderName);
            Assert.AreNotEqual(AppInstance.DefaultLocalReportsFolderName, AppInstance.E2ELocalReportsFolderName);
        }

        [TestMethod]
        public void AScreenHarnessGetsADataFolderOfItsOwn()
        {
            // Harness runs used to write into the live app's Debug_Logs.txt and CSVs (and lock them).
            Assert.IsFalse(AppInstance.IsHarness, "the test host carries no marker");
            Assert.AreEqual("Focus Bear Harness", AppInstance.DataFolderNameFor(isE2E: false, isHarness: true));
            Assert.AreEqual("Focus Bear", AppInstance.DataFolderNameFor(false, false));
            Assert.AreEqual("Focus Bear Harness", AppInstance.DataFolderNameFor(false, true, true), "a marked harness keeps its folder");
            Assert.AreEqual("Focus Bear E2E", AppInstance.DataFolderNameFor(true, true), "the E2E build keeps its own folder");
            Assert.IsFalse(AppInstance.HasBuildMarker(typeof(AppInstance).Assembly, AppInstance.HarnessMetadataValue));
        }

        [TestMethod]
        public void ANamedE2EInstanceTakesOnlyASafeShortName()
        {
            Assert.AreEqual("smoke", AppInstance.E2EInstanceFrom(" smoke "));
            Assert.AreEqual("run-2", AppInstance.E2EInstanceFrom("run-2"));
            Assert.AreEqual("", AppInstance.E2EInstanceFrom(null));
            Assert.AreEqual("", AppInstance.E2EInstanceFrom(@"..\Focus Bear"), "never a path out of the E2E folders");
            Assert.AreEqual("", AppInstance.E2EInstanceFrom("a b"));
            Assert.AreEqual("", AppInstance.E2EInstanceFrom(new string('x', 25)));
            Assert.AreEqual("", AppInstance.E2EInstance, "only the E2E build reads the variable; a test host never does");
            Assert.AreEqual("", AppInstance.E2EInstanceSuffix);
        }
    }
}
