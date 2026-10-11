using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// The routine's finished-screen graph was a getData({...}) call spliced together with string.Format, so
    /// a habit or question named with a quote or a backslash (Read "Dune") broke the script and the graph
    /// stayed blank - and the name ran as script inside the graph page.
    /// </summary>
    [TestClass]
    public class HabitGraphScriptTests
    {
        private static JObject Payload(string script)
        {
            Assert.IsTrue(script.StartsWith("getData(") && script.EndsWith(");"), script);
            return JObject.Parse(script.Substring("getData(".Length, script.Length - "getData(".Length - 2));
        }

        [TestMethod]
        public void ANameWithQuotesAndBackslashesStaysOneLabel()
        {
            string name = "Read \"Dune\" \\ 20 pages\", \"max\": alert(1)//";
            var p = Payload(HabitGraphScript.Days(name, "Date", "Minutes", "[0,5,6.5]", 12));
            Assert.AreEqual(name, (string)p["label"]);
            Assert.AreEqual(12, (int)p["max"]);
            Assert.AreEqual(6.5, (double)p["data"][2]);
            Assert.IsNull(p["TimeValues"]);
        }

        [TestMethod]
        public void TheTimeGraphCarriesItsTimeValues()
        {
            var p = Payload(HabitGraphScript.Time("Stretch", "Time", "Minutes", "[0,3,0]", 8, "[\"2026-09-27 08:00:00\",\"2026-09-27 17:00:00\"]"));
            Assert.AreEqual("2026-09-27 17:00:00", (string)p["TimeValues"][1]);
            Assert.AreEqual(3, (int)p["data"][1]);
        }

        [TestMethod]
        public void UnreadableDataGivesAnEmptyGraphNotABrokenScript()
        {
            var p = Payload(HabitGraphScript.Days(null, "Date", "Quantity", "[0,abc,]", 5));
            Assert.AreEqual("", (string)p["label"]);
            Assert.AreEqual(0, ((JArray)p["data"]).Count);
        }
    }
}
