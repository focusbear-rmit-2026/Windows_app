using System.Collections.Generic;
using FocusBear.Core.Analytics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Analytics
{
    [TestClass]
    public class EventPayloadTests
    {
        [TestMethod]
        public void AFragmentFromACaller_ReadsAsItsMembers()
        {
            JObject props = EventPayload.Properties("\"type\":\"simple-focus\",\"focusDuration\":25");
            Assert.AreEqual("simple-focus", (string)props["type"]);
            Assert.AreEqual(25, (int)props["focusDuration"]);
            Assert.AreEqual(0, EventPayload.Properties("").Count);
        }

        [TestMethod]
        public void ABrokenFragment_IsNull_SoTheEventStillGoesWithoutIt()
        {
            Assert.IsNull(EventPayload.Properties("\"feedback\":\"it said \"no\"\""));
            Assert.IsNull(EventPayload.Properties("\"a\":1}, {\"b\":2"), "a fragment cannot smuggle in a second object");
        }

        [TestMethod]
        public void MembersBuiltFromTypedText_AreEscaped()
        {
            string fragment = EventPayload.Members(new KeyValuePair<string, object>("community", "Mum's \"group\" \\ x"),
                                                   new KeyValuePair<string, object>("n", 3));
            JObject props = EventPayload.Properties(fragment);
            Assert.AreEqual("Mum's \"group\" \\ x", (string)props["community"]);
            Assert.AreEqual(3, (int)props["n"]);
        }

        [TestMethod]
        public void Merging_KeepsTheTargetsOwnKeys()
        {
            var target = new JObject { ["distinct_id"] = "u1" };
            EventPayload.Merge(target, new JObject { ["distinct_id"] = "spoofed", ["extra"] = 1 });
            Assert.AreEqual("u1", (string)target["distinct_id"]);
            Assert.AreEqual(1, (int)target["extra"]);
        }
    }
}
