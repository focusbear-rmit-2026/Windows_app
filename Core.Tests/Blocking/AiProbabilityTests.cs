using System.Globalization;
using System.Threading;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class AiProbabilityTests
    {
        [TestMethod]
        public void ReadsTheServersNumberWhateverTheCulture()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var body = JObject.Parse("{\"allowed_probability\":0.85,\"as_text\":\"0.3\"}");
                Assert.AreEqual(0.85, AiProbability.Read(body["allowed_probability"]));
                Assert.AreEqual(0.3, AiProbability.Read(body["as_text"]));
            }
            finally { Thread.CurrentThread.CurrentCulture = original; }
        }

        [TestMethod]
        public void AnythingElseIsNoProbability()
        {
            var body = JObject.Parse("{\"a\":null,\"b\":\"high\",\"c\":true,\"d\":{},\"e\":\"NaN\",\"f\":1}");
            Assert.IsNull(AiProbability.Read(body["a"]));
            Assert.IsNull(AiProbability.Read(body["b"]));
            Assert.IsNull(AiProbability.Read(body["c"]));
            Assert.IsNull(AiProbability.Read(body["d"]));
            Assert.IsNull(AiProbability.Read(body["e"]));
            Assert.IsNull(AiProbability.Read(body["missing"]));
            Assert.AreEqual(1.0, AiProbability.Read(body["f"]));
        }
    }
}
