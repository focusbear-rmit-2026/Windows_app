using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The left menu's Stats entry showed the old "motivation" sun (report 2026-09-28_21-44-58: "Should be a little
    /// graph"). The Mac draws it with SF Symbol <c>chart.bar.xaxis</c>; Windows draws the same bar chart as a vector
    /// icon in App.xaml, like the Tab Audit and Late No More icons.
    /// </summary>
    [TestClass]
    public class StatsMenuIconTests
    {
        private static string Repo([CallerFilePath] string here = "") => Path.GetDirectoryName(Path.GetDirectoryName(here));

        [TestMethod]
        public void TheStatsEntryUsesTheBarChartIcon()
        {
            string menu = File.ReadAllText(Path.Combine(Repo(), "FocusBearMainWindow.xaml.cs"));
            Match stats = Regex.Match(menu, @"new NavLeaf \{[^\r\n]*ButtonType = LeftButtonType\.Motivation \}");
            Assert.IsTrue(stats.Success, "the Stats menu entry");
            StringAssert.Contains(stats.Value, "FindResource(\"StatsIcon\")");
            Assert.IsFalse(stats.Value.Contains("motivation.png"), "not the old sun");
        }

        [TestMethod]
        public void TheBarChartIconIsDefined()
        {
            string app = File.ReadAllText(Path.Combine(Repo(), "App.xaml"));
            StringAssert.Contains(app, "<DrawingImage x:Key=\"StatsIcon\">");
        }
    }
}
