using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    [TestClass]
    public class UnchangedFileGateTests
    {
        private static readonly DateTime T = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void AnEmptyQueueIsNotReadAgainUntilItChanges()
        {
            var gate = new UnchangedFileGate();
            Assert.IsTrue(gate.ShouldRead(157, T), "never read");
            gate.RememberEmpty(157, T);
            Assert.IsFalse(gate.ShouldRead(157, T), "same file, every 5 s");
            Assert.IsTrue(gate.ShouldRead(240, T.AddSeconds(3)), "an entry was appended");
            Assert.IsTrue(gate.ShouldRead(null, null), "the file went");
        }

        [TestMethod]
        public void ForgettingReadsAgain()
        {
            var gate = new UnchangedFileGate();
            gate.RememberEmpty(157, T);
            gate.Forget();
            Assert.IsTrue(gate.ShouldRead(157, T));
        }
    }
}
