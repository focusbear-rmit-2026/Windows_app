using System;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// "47 minutes", "3.2 hours", "2 days 5 hours" — how a focus total is said out loud. Port of
    /// the Mac's <c>HumanizedFocusTime</c>, itself the web's <c>humanizeFocusedTime</c>.
    /// </summary>
    public sealed class HumanizedFocusTime
    {
        public enum Unit
        {
            Minutes,
            Hours,
            Days,
        }

        public HumanizedFocusTime(double value, Unit unit, int remainingHours)
        {
            Value = value;
            TimeUnit = unit;
            RemainingHours = remainingHours;
        }

        public double Value { get; }
        public Unit TimeUnit { get; }

        /// <summary>Only meaningful for <see cref="Unit.Days"/>; 0 otherwise.</summary>
        public int RemainingHours { get; }

        /// <summary>
        /// Under an hour stays in minutes; under 48 hours reads as hours to one decimal; beyond
        /// that it becomes whole days plus leftover hours. The 48-hour threshold rather than 24 is
        /// the web's: "1 day 3 hours" is harder to picture than "27 hours".
        /// </summary>
        public static HumanizedFocusTime From(double totalMinutes)
        {
            if (totalMinutes < 60)
            {
                return new HumanizedFocusTime(SwiftRounding.Rounded(totalMinutes), Unit.Minutes, 0);
            }

            double hours = totalMinutes / 60;
            if (hours >= 48)
            {
                double days = Math.Floor(hours / 24);
                int remainder = SwiftRounding.RoundedToInt(hours % 24);
                return new HumanizedFocusTime(days, Unit.Days, remainder);
            }

            return new HumanizedFocusTime(SwiftRounding.Rounded(hours * 10) / 10, Unit.Hours, 0);
        }
    }
}
