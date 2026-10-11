using System;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// Rounding that agrees with Swift's <c>Double.rounded()</c>.
    ///
    /// Swift rounds a half away from zero; .NET's <c>Math.Round(double)</c> rounds it to even
    /// ("banker's rounding"), so 0.5 is 1 on the Mac and 0 here, and 12.5 is 13 there and 12 here.
    /// Every number ported into this namespace is one both clients compute for the same user — the
    /// same leaderboard row, the same busy-hours payload, the same alignment score — so an
    /// off-by-one from a rounding mode would show up as the two apps disagreeing. Rounding therefore
    /// goes through here instead of through <c>Math.Round</c> at the call site.
    /// </summary>
    public static class SwiftRounding
    {
        /// <summary>Swift's <c>value.rounded()</c>.</summary>
        public static double Rounded(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>Swift's <c>Int(value.rounded())</c>.</summary>
        public static int RoundedToInt(double value)
        {
            return (int)Rounded(value);
        }
    }
}
