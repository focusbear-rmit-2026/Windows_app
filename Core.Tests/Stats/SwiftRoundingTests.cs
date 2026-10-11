using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// The one place Swift and C# genuinely disagree across this port, pinned so nobody "simplifies"
    /// these calls back to <c>Math.Round</c>.
    /// </summary>
    [TestClass]
    public class SwiftRoundingTests
    {
        [TestMethod]
        public void AHalfRoundsAwayFromZeroLikeSwiftAndUnlikeMathRound()
        {
            Assert.AreEqual(0, Math.Round(0.5), ".NET's default is banker's rounding: 0.5 goes to even");
            Assert.AreEqual(1, SwiftRounding.Rounded(0.5), "Swift's Double.rounded() goes away from zero");

            Assert.AreEqual(2, Math.Round(2.5));
            Assert.AreEqual(3, SwiftRounding.Rounded(2.5));

            Assert.AreEqual(-1, SwiftRounding.Rounded(-0.5), "away from zero, in both directions");
        }

        /// <summary>
        /// The difference is not academic: it moves a real leaderboard percentage. 1 day of 8 is
        /// 12.5%, which the Mac prints as 13% — a Windows client using Math.Round would print 12%
        /// for the same user on the same day.
        /// </summary>
        [TestMethod]
        public void ALeaderboardPercentageWouldDisagreeWithTheMacUnderBankersRounding()
        {
            Assert.AreEqual(13, LeaderboardSorting.Percentage(1, 8));
            Assert.AreEqual(12, Math.Round(1.0 / 8 * 100), "what the same column would have shown otherwise");
        }

        [TestMethod]
        public void RoundedToIntMatchesSwiftsIntOfARoundedDouble()
        {
            Assert.AreEqual(3, SwiftRounding.RoundedToInt(2.5));
            Assert.AreEqual(2, SwiftRounding.RoundedToInt(2.4));
            Assert.AreEqual(-3, SwiftRounding.RoundedToInt(-2.5));
        }
    }
}
