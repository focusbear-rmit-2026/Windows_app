using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The three orders the to-do list can be drawn in, ported case-for-case from the Mac's
    /// <c>ToDoLogicTests</c>.
    /// </summary>
    [TestClass]
    public class ToDoSortingTests
    {
        [TestMethod]
        public void DueDateSortPutsUndatedLast()
        {
            IList<ToDoTask> sorted = ToDoSorting.Sort(
                new[] { Task("late", dueOffset: 5), Task("none"), Task("soon", dueOffset: 1) },
                ToDoSortMode.DueDate, Now);

            CollectionAssert.AreEqual(new[] { "soon", "late", "none" }, Ids(sorted));
        }

        [TestMethod]
        public void ChronologicalSortIsNewestFirst()
        {
            IList<ToDoTask> sorted = ToDoSorting.Sort(
                new[]
                {
                    Task("old", createdAt: "2026-01-01T00:00:00.000Z"),
                    Task("new", createdAt: "2026-02-01T00:00:00.000Z"),
                },
                ToDoSortMode.Chronological, Now);

            CollectionAssert.AreEqual(new[] { "new", "old" }, Ids(sorted));
        }

        [TestMethod]
        public void ChronologicalSortSendsARowWithNoCreatedAtToTheBottom()
        {
            // `PUT /to-do` answers a create without `created_at`, and the Mac reads that as epoch zero.
            // Whatever the missing value is called, it must not out-rank a real timestamp.
            IList<ToDoTask> sorted = ToDoSorting.Sort(
                new[] { Task("dated", createdAt: "2026-02-01T00:00:00.000Z"), Task("undated", createdAt: null) },
                ToDoSortMode.Chronological, Now);

            CollectionAssert.AreEqual(new[] { "dated", "undated" }, Ids(sorted));
        }

        [TestMethod]
        public void SmartSortIsHighestScoreFirst()
        {
            IList<ToDoTask> sorted = ToDoSorting.Sort(
                new[] { Task("low", topScore: 1), Task("high", topScore: 9) }, ToDoSortMode.Smart, Now);

            CollectionAssert.AreEqual(new[] { "high", "low" }, Ids(sorted));
        }

        [TestMethod]
        public void EveryModeKeepsTheCallersOrderForTiedRows()
        {
            // The Mac carries an explicit index because Swift's sort is unstable. A port that leaned on
            // LINQ being stable instead would silently lose this the day the implementation changed.
            var tied = new[] { Task("first", dueOffset: 2), Task("second", dueOffset: 2) };
            CollectionAssert.AreEqual(new[] { "first", "second" },
                                      Ids(ToDoSorting.Sort(tied, ToDoSortMode.DueDate, Now)));
            CollectionAssert.AreEqual(new[] { "first", "second" },
                                      Ids(ToDoSorting.Sort(tied, ToDoSortMode.Chronological, Now)));
            CollectionAssert.AreEqual(new[] { "first", "second" },
                                      Ids(ToDoSorting.Sort(tied, ToDoSortMode.Smart, Now)));
        }

        [TestMethod]
        public void SortingDoesNotReorderTheCallersOwnCollection()
        {
            // The Mac's `[ToDoTask]` is a value, so a caller's array cannot be reordered underneath it.
            var original = new List<ToDoTask> { Task("late", dueOffset: 5), Task("soon", dueOffset: 1) };
            ToDoSorting.Sort(original, ToDoSortMode.DueDate, Now);

            CollectionAssert.AreEqual(new[] { "late", "soon" }, Ids(original));
        }

        [TestMethod]
        public void SmartSortAgreesWithTheScoringRuleRatherThanRepeatingIt()
        {
            // Asserting the ORDER against the scores the same rule computes would prove nothing; these
            // two rows are ranked by facts a reader can check: today beats next month, whatever T x O / P
            // works out to.
            var dueToday = Task("today", dueOffset: 0, outcome: 5, perspiration: 2);
            var dueLater = Task("later", dueOffset: 30, outcome: 5, perspiration: 2);

            CollectionAssert.AreEqual(new[] { "today", "later" },
                                      Ids(ToDoSorting.Sort(new[] { dueLater, dueToday }, ToDoSortMode.Smart, Now)));
        }

        [TestMethod]
        public void SortModeWireSpellingsMatchTheQueryParameter()
        {
            Assert.AreEqual("SMART", ToDoSortModeWire.Of(ToDoSortMode.Smart));
            Assert.AreEqual("CHRONOLOGICAL", ToDoSortModeWire.Of(ToDoSortMode.Chronological));
            Assert.AreEqual("DUE_DATE", ToDoSortModeWire.Of(ToDoSortMode.DueDate));
        }
    }
}
