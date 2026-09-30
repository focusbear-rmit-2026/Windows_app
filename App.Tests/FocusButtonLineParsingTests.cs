using System;
using Focus_Bear.Services.FocusButton;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Focus Button serial lines. EVENT|STARTHB read the name from parts[4] after checking only for 4 parts, so a
    /// 4-field line threw on the dispatcher, and a habit name containing '|' was cut at the pipe. FIRMWAREVER
    /// without a "-" threw and left the version unknown.
    /// </summary>
    [TestClass]
    public class FocusButtonLineParsingTests
    {
        [TestMethod]
        public void StartHabit_TheDocumentedFiveFields()
        {
            Assert.IsTrue(FocusButtonProtocol.TryParseStartHabit("EVENT|STARTHB|300|42|Stretch".Split('|'), out string id, out string name));
            Assert.AreEqual("42", id);
            Assert.AreEqual("Stretch", name);
        }

        [TestMethod]
        public void StartHabit_ANameWithAPipeStaysWhole()
        {
            Assert.IsTrue(FocusButtonProtocol.TryParseStartHabit("EVENT|STARTHB|300|42|Read | write ".Split('|'), out string id, out string name));
            Assert.AreEqual("Read | write", name);
        }

        [TestMethod]
        public void StartHabit_FourFieldsHasANameButNoId()
        {
            Assert.IsTrue(FocusButtonProtocol.TryParseStartHabit("EVENT|STARTHB|300|Stretch".Split('|'), out string id, out string name));
            Assert.AreEqual("", id);
            Assert.AreEqual("Stretch", name);
        }

        [TestMethod]
        public void StartHabit_TooShortIsRejected()
        {
            Assert.IsFalse(FocusButtonProtocol.TryParseStartHabit("EVENT|STARTHB|300".Split('|'), out _, out _));
            Assert.IsFalse(FocusButtonProtocol.TryParseStartHabit(null, out _, out _));
        }

        [TestMethod]
        public void FirmwareVersion()
        {
            Assert.IsTrue(FocusButtonProtocol.TryParseFirmwareVersion("M50-1.8.1", out string hw, out Version v));
            Assert.AreEqual("M50", hw);
            Assert.AreEqual(new Version(1, 8, 1), v);

            Assert.IsTrue(FocusButtonProtocol.TryParseFirmwareVersion(" C30-1.7.0-rc2 ", out hw, out v));
            Assert.AreEqual("C30", hw);
            Assert.AreEqual(new Version(1, 7, 0), v);

            Assert.IsFalse(FocusButtonProtocol.TryParseFirmwareVersion("M50", out hw, out v), "no version part");
            Assert.AreEqual("M50", hw);
            Assert.IsNull(v);
            Assert.IsFalse(FocusButtonProtocol.TryParseFirmwareVersion("M50-abc", out _, out v));
            Assert.IsFalse(FocusButtonProtocol.TryParseFirmwareVersion("", out _, out _));
        }
    }
}
