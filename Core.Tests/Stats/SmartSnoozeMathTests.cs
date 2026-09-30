using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// Ported from the Mac's SmartSnoozeMathTests: the "Choose a good time for me" maths — the
    /// cumulative rollover model (half of each day's task load spills into the next day) and the task
    /// distribution that uses it to spread snoozed tasks out to genuinely empty days.
    /// </summary>
    [TestClass]
    public class SmartSnoozeMathTests
    {
        private const double Rollover = SmartSnoozeMath.DefaultRolloverFraction;

        private static IList<int> EffectiveFree(int[] scheduleFree, int[] load, double rolloverFraction)
        {
            return SmartSnoozeMath.EffectiveFree(scheduleFree, load, rolloverFraction);
        }

        [TestMethod]
        public void EffectiveFreeSubtractsOwnLoad()
        {
            // One day, 480 min free, 200 min of tasks → 280 effective free.
            CollectionAssert.AreEqual(new[] { 280 },
                EffectiveFree(new[] { 480 }, new[] { 200 }, Rollover).ToArray());
        }

        [TestMethod]
        public void EffectiveFreeAppliesCumulativeRollover()
        {
            // The user's example: Thu 4h tasks, Fri 4h tasks, Mon empty; 8h (480m) free each day.
            // Thu: 480-240 = 240 free            (carry = 0.5 * 240 = 120 into Fri)
            // Fri: 480-(240+120) = 120 free      (carry = 0.5 * 360 = 180 into Mon)
            // Mon: 480-(0+180) = 300 free
            CollectionAssert.AreEqual(new[] { 240, 120, 300 },
                EffectiveFree(new[] { 480, 480, 480 }, new[] { 240, 240, 0 }, Rollover).ToArray());
        }

        [TestMethod]
        public void EffectiveFreeCanGoNegativeWhenOverCommitted()
        {
            // Day0: 120-200 = -80 (carry = 100). Day1: 120-(200+100) = -180.
            CollectionAssert.AreEqual(new[] { -80, -180 },
                EffectiveFree(new[] { 120, 120 }, new[] { 200, 200 }, Rollover).ToArray());
        }

        [TestMethod]
        public void EffectiveFreeWithZeroRolloverIsPlainNet()
        {
            CollectionAssert.AreEqual(new[] { 200, 250 },
                EffectiveFree(new[] { 300, 300 }, new[] { 100, 50 }, 0).ToArray());
        }

        [TestMethod]
        public void EffectiveFreeOfNoDaysIsNoDays()
        {
            Assert.AreEqual(0, EffectiveFree(new int[0], new int[0], Rollover).Count);
        }

        [TestMethod]
        public void DistributeLandsOnFirstDayWithRoom()
        {
            // Day0 already full (60/60), day1 empty → the 60-min task goes to day1.
            CollectionAssert.AreEqual(new[] { 1 },
                SmartSnoozeMath.Distribute(new[] { 60 }, new[] { 60, 480 }, new[] { 60, 0 }, Rollover).ToArray());
        }

        [TestMethod]
        public void DistributeSpreadsAcrossDaysInsteadOfPiling()
        {
            // Several tasks that can't all share one day get spread forward (distinct, increasing
            // days) — the pile-up bug's fix, made stricter by the rollover.
            IList<int> days = SmartSnoozeMath.Distribute(new[] { 60, 60, 60, 60 },
                new[] { 90, 90, 90, 90, 90, 90 }, new[] { 0, 0, 0, 0, 0, 0 }, Rollover);

            Assert.AreEqual(4, days.Count);
            CollectionAssert.AreEqual(days.OrderBy(day => day).ToArray(), days.ToArray(),
                "tasks are placed forward in time");
            Assert.AreEqual(days.Count, days.Distinct().Count(),
                "no two tasks piled on the same day when they don't fit together");
        }

        [TestMethod]
        public void DistributeFallsBackToEmptiestWhenNothingFits()
        {
            // A 500-min task can't fit any 100-min day → the emptiest effective day (day 0, before
            // the carry grows), which is also the earliest of the equally empty ones.
            CollectionAssert.AreEqual(new[] { 0 },
                SmartSnoozeMath.Distribute(new[] { 500 }, new[] { 100, 100, 100 }, new[] { 0, 0, 0 }, Rollover).ToArray());
        }

        /// <summary>
        /// Swift arrays are values, so the Mac's version cannot touch its caller's load array. C#
        /// lists are references, so the copy has to be deliberate — without it, calling this twice
        /// would place the second batch of tasks against the first batch's leftovers.
        /// </summary>
        [TestMethod]
        public void DistributeLeavesTheCallersStartLoadAlone()
        {
            int[] startLoad = { 60, 0 };

            SmartSnoozeMath.Distribute(new[] { 60 }, new[] { 60, 480 }, startLoad, Rollover);

            CollectionAssert.AreEqual(new[] { 60, 0 }, startLoad);
        }

        [TestMethod]
        public void DistributeRolloverPushesPastANominallyFreeDay()
        {
            // Day1 looks free by its own load (0), but the rollover from a heavily-loaded day0 fills
            // it, so a task that wouldn't fit day1's effective free is pushed further out.
            // day0: schedule 100, load 100 → eff 0, carry 50. day1: schedule 100, load 0 → eff 50.
            // A 60-min task fits neither day0 (eff would be -60) nor day1 (eff 50-60 = -10) → day2.
            CollectionAssert.AreEqual(new[] { 2 },
                SmartSnoozeMath.Distribute(new[] { 60 }, new[] { 100, 100, 100, 100 }, new[] { 100, 0, 0, 0 }, Rollover)
                    .ToArray());
        }
    }
}
