using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>'s "mid-routine habit switch" section.
    ///
    /// Regression: with a habit paused, tapping Play on a *different* habit in the Day Plan did nothing.
    /// The switch now mirrors the player's "Select another habit" sheet — the chosen habit takes the
    /// currently-playing slot and the habit that was paused stays directly behind it, so it is neither
    /// skipped nor completed.
    /// </summary>
    [TestClass]
    public class RoutineReorderTests
    {
        private static List<string> Moving(IReadOnlyList<string> habits, int from, int anchor)
        {
            IReadOnlyList<string> moved = RoutineReorder.Moving(habits, from, anchor);
            return moved?.ToList();
        }

        [TestMethod]
        public void MovingLaterHabitToAnchorPutsPausedHabitDirectlyBehindIt()
        {
            // B (playing/paused) is still next after D.
            CollectionAssert.AreEqual(new List<string> { "A", "D", "B", "C" },
                                      Moving(new[] { "A", "B", "C", "D" }, 3, 1));
        }

        [TestMethod]
        public void MovingEarlierHabitToAnchorKeepsItJustBeforeThePausedHabit()
        {
            // C (playing/paused) still follows A.
            CollectionAssert.AreEqual(new List<string> { "B", "A", "C", "D" },
                                      Moving(new[] { "A", "B", "C", "D" }, 0, 2));
        }

        [TestMethod]
        public void MovingHabitAlreadyAtAnchorIsANoOp()
        {
            Assert.IsNull(RoutineReorder.Moving(new[] { "A", "B", "C" }, 1, 1));
        }

        [TestMethod]
        public void MovingOutOfRangeIndexIsANoOp()
        {
            Assert.IsNull(RoutineReorder.Moving(new[] { "A", "B" }, 5, 0));
            Assert.IsNull(RoutineReorder.Moving(new string[0], 0, 0));
            Assert.IsNull(RoutineReorder.Moving(new[] { "A", "B" }, -1, 0));
        }

        [TestMethod]
        public void AnchorPastTheEndClampsToTheLastPosition()
        {
            // The anchor clamps to C, so A lands just before it.
            CollectionAssert.AreEqual(new List<string> { "B", "A", "C" },
                                      Moving(new[] { "A", "B", "C" }, 0, 99));
        }

        [TestMethod]
        public void TheSourceListIsNeverMutated()
        {
            // Swift arrays are values, so the Mac gets this for free; here it is a real risk worth pinning.
            List<string> original = new List<string> { "A", "B", "C" };
            RoutineReorder.Moving(original, 0, 2);
            CollectionAssert.AreEqual(new List<string> { "A", "B", "C" }, original);
        }
    }
}
