using System;
using System.Collections.Generic;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The maths behind "choose a good time for me": where a snoozed task should land.
    ///
    /// Port of the Mac's <c>SmartSnoozeMath</c>. Everything here is in minutes, and the day arrays are
    /// parallel and in chronological order.
    /// </summary>
    public static class SmartSnoozeMath
    {
        /// <summary>
        /// The app's rollover: half of a day's total load is assumed to spill into the next day.
        /// </summary>
        public const double DefaultRolloverFraction = 0.5;

        /// <summary>
        /// Effective free minutes per day under the CUMULATIVE ROLLOVER model.
        ///
        /// People rarely finish everything, so <paramref name="rolloverFraction"/> of each day's TOTAL
        /// load — its own plus what it carried from earlier days — is assumed to spill into the next
        /// one. The load carried into day i is therefore
        /// <c>rolloverFraction × (load[i-1] + carry[i-1])</c>, and effective free is the schedule's
        /// gaps minus (own load + carried load). Near days look busier, so a snooze targets a
        /// genuinely empty day rather than the first nominally free one. Can go negative.
        ///
        /// <paramref name="initialCarry"/> seeds the rollover BEFORE the first day, which is how the
        /// source day's own load (today's, say) spills into a horizon that starts tomorrow.
        /// </summary>
        public static IList<int> EffectiveFree(IList<int> scheduleFree, IList<int> load, double rolloverFraction,
                                               double initialCarry = 0)
        {
            if (scheduleFree.Count != load.Count)
            {
                throw new ArgumentException("scheduleFree and load must be parallel", nameof(load));
            }

            double carry = initialCarry;
            List<int> effectiveFree = new List<int>(scheduleFree.Count);
            for (int day = 0; day < scheduleFree.Count; day++)
            {
                double total = load[day] + carry;
                effectiveFree.Add(scheduleFree[day] - SwiftRounding.RoundedToInt(total));
                carry = total * rolloverFraction;
            }

            return effectiveFree;
        }

        /// <summary>
        /// Spreads <paramref name="taskDurations"/> (in order) across the days, returning the chosen
        /// day INDEX per task.
        ///
        /// Each task lands on the FIRST day whose effective free — with the rollover, and with this
        /// task added to that day's load — stays at or above zero; if no day in range can fit it, the
        /// emptiest effective day. Assigning a task raises that day's load, which via the rollover
        /// makes later days busier too, so tasks spread out instead of piling onto one day: that
        /// pile-up was the reported bug, and the rollover is what makes the fix stick.
        /// </summary>
        public static IList<int> Distribute(IList<int> taskDurations, IList<int> scheduleFree, IList<int> startLoad,
                                            double rolloverFraction, double initialCarry = 0)
        {
            List<int> load = new List<int>(startLoad);
            List<int> chosenDays = new List<int>(taskDurations.Count);

            foreach (int duration in taskDurations)
            {
                int placedDay = -1;
                for (int day = 0; day < load.Count; day++)
                {
                    load[day] += duration;
                    if (EffectiveFree(scheduleFree, load, rolloverFraction, initialCarry)[day] >= 0)
                    {
                        placedDay = day;
                        break;
                    }

                    load[day] -= duration;
                }

                if (placedDay < 0)
                {
                    placedDay = EmptiestDay(EffectiveFree(scheduleFree, load, rolloverFraction, initialCarry));
                    load[placedDay] += duration;
                }

                chosenDays.Add(placedDay);
            }

            return chosenDays;
        }

        /// <summary>
        /// The day with the most effective free time, EARLIEST first on a tie — Swift's
        /// <c>max(by:)</c> keeps the first of equal elements, and with an empty horizon every day is
        /// equally free, so the fallback has to land on day 0 rather than the last day.
        /// </summary>
        private static int EmptiestDay(IList<int> effectiveFree)
        {
            int emptiest = 0;
            for (int day = 1; day < effectiveFree.Count; day++)
            {
                if (effectiveFree[day] > effectiveFree[emptiest])
                {
                    emptiest = day;
                }
            }

            return emptiest;
        }
    }
}
