using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    [TestClass]
    public class QuantityTypingTests
    {
        private static string Type(string keys)
        {
            string text = "";
            foreach (char c in keys)
            {
                string next = QuantityTyping.Apply(text, text.Length, 0, c.ToString());
                if (next != null) text = next;
            }
            return text;
        }

        [TestMethod]
        public void ACommaIsTheDecimalPoint_NotSwallowed()
        {
            // The comma used to be dropped, so "6,85" became 685.
            Assert.AreEqual("6.85", Type("6,85"));
            Assert.AreEqual("6.85", Type("6.85"));
        }

        [TestMethod]
        public void OnlyOneSeparator_AndNoLetters()
        {
            Assert.AreEqual("6.85", Type("6.8.5,"));
            Assert.AreEqual("12", Type("1a2 "));
            Assert.AreEqual(".5", Type(".5"));
        }

        [TestMethod]
        public void TypingOverASelection_ReplacesIt()
        {
            Assert.AreEqual("9", QuantityTyping.Apply("10", 0, 2, "9"));
            Assert.AreEqual("1.5", QuantityTyping.Apply("10", 1, 1, ",5"));
            Assert.IsNull(QuantityTyping.Apply("1.2", 3, 0, "."));
        }
    }
}
