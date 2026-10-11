using FocusBear.Core.Postpone;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Postpone
{
    /// <summary>
    /// The postpone box takes typed minutes (#1036). Past about 35,791 minutes the DispatcherTimer refuses the
    /// interval; PostponeHabitWork caught the throw, so the dialog closed, nothing was postponed and the user
    /// was told nothing. A value too long for an int was reported as "not a valid number". Postpones are now
    /// capped at a day - past that the routine's own window is over anyway (the Mac offers at most 90).
    /// </summary>
    [TestClass]
    public class PostponeMinutesTests
    {
        [TestMethod]
        public void OrdinaryMinutesPassThrough()
        {
            Assert.AreEqual(PostponeMinutesInput.Ok, PostponeMinutes.Parse("45", out int m));
            Assert.AreEqual(45, m);
        }

        [TestMethod]
        public void ZeroBecomesOneMinute()
        {
            Assert.AreEqual(PostponeMinutesInput.Ok, PostponeMinutes.Parse("0", out int m));
            Assert.AreEqual(1, m);
        }

        [TestMethod]
        public void TooLongIsCappedAndSaysSo()
        {
            Assert.AreEqual(PostponeMinutesInput.Capped, PostponeMinutes.Parse("50000", out int m));
            Assert.AreEqual(PostponeMinutes.Max, m);
            Assert.AreEqual(PostponeMinutesInput.Capped, PostponeMinutes.Parse("99999999999999", out m), "longer than an int is still just too long");
            Assert.AreEqual(PostponeMinutes.Max, m);
            Assert.AreEqual(PostponeMinutesInput.Ok, PostponeMinutes.Parse(PostponeMinutes.Max.ToString(), out m));
        }

        [TestMethod]
        public void NotANumberIsInvalid()
        {
            Assert.AreEqual(PostponeMinutesInput.Invalid, PostponeMinutes.Parse("", out _));
            Assert.AreEqual(PostponeMinutesInput.Invalid, PostponeMinutes.Parse("ten", out _));
            Assert.AreEqual(PostponeMinutesInput.Invalid, PostponeMinutes.Parse(null, out _));
        }

        [TestMethod]
        public void ClampKeepsEveryTimerInRange()
        {
            Assert.AreEqual(1, PostponeMinutes.Clamp(-5));
            Assert.AreEqual(PostponeMinutes.Max, PostponeMinutes.Clamp(int.MaxValue));
            Assert.AreEqual(30, PostponeMinutes.Clamp(30));
        }
    }
}
