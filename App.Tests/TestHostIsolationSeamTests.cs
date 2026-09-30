using System.IO;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// These tests run the app's own code, which logs, writes CSVs and an offline queue, and reads and writes the
    /// registry. Run from a test host they used to do all of it in the person's live Focus Bear folder and key
    /// (27 Sep: App.Tests lines in the live Debug_Logs.txt, and entries in the live offline queue).
    /// </summary>
    [TestClass]
    public class TestHostIsolationSeamTests
    {
        [TestMethod]
        public void TheAppsDataFolderUnderATestHostIsItsOwn()
        {
            string folder = Path.GetFileName(AppSettings.AppDataDirPath.TrimEnd(Path.DirectorySeparatorChar, '/'));
            Assert.AreEqual("Focus Bear Tests", folder);
            StringAssert.StartsWith(AppSettings.AppDataDirLogsPath, AppSettings.AppDataDirPath);
        }

        [TestMethod]
        public void TheRegistryRootUnderATestHostIsItsOwn()
        {
            Assert.AreEqual(@"Software\Focus Bear Tests", RegistryManager.GetRegistryGlobalPath());
            StringAssert.StartsWith(RegistryManager.GetRegistryPathBackup(), @"Software\Focus Bear Tests");
            StringAssert.StartsWith(RegistryManager.GetRegistryGlobalPathFBTT(), @"Software\Focus Bear Tests");
        }
    }
}
