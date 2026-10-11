using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>The to-do list's client-side filters, ported from the Mac's `testFiltersProjectEnergyAndDueRange`.</summary>
    [TestClass]
    public class ToDoFiltersTests
    {
        [TestMethod]
        public void ProjectFilterMatchesTheLinkOrTheTag()
        {
            var filters = new ToDoFilters { ProjectId = "p" };

            Assert.IsTrue(filters.Matches(Task("a", projectId: "p")));
            Assert.IsTrue(filters.Matches(Task("b", tags: new[] { new ToDoTag("p", "P") })),
                          "the tag of a project's id is how the list links a task to it");
            Assert.IsFalse(filters.Matches(Task("c")));
        }

        [TestMethod]
        public void EnergyFilterSplitsPerspirationAtFour()
        {
            var low = new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Low };
            Assert.IsTrue(low.Matches(Task("d", perspiration: 3)));
            Assert.IsFalse(low.Matches(Task("e", perspiration: 4)));

            var full = new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Full };
            Assert.IsTrue(full.Matches(Task("f", perspiration: 4)));
            Assert.IsFalse(full.Matches(Task("g", perspiration: 3)));
        }

        [TestMethod]
        public void AnUnratedTaskCountsAsTheLowestEnergy()
        {
            Assert.IsTrue(new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Low }.Matches(Task("unrated")));
            Assert.IsFalse(new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Full }.Matches(Task("unrated")));
        }

        [TestMethod]
        public void DueDateRangeIsInclusiveOfBothEndDays()
        {
            var filters = new ToDoFilters { DueDateStart = Day(0), DueDateEnd = Day(6) };

            Assert.IsTrue(filters.Matches(Task("start", dueOffset: 0)));
            Assert.IsTrue(filters.Matches(Task("end", dueOffset: 6)));
            Assert.IsFalse(filters.Matches(Task("after", dueOffset: 7)));
            Assert.IsFalse(filters.Matches(Task("before", dueOffset: -1)));
            Assert.IsFalse(filters.Matches(Task("undated")), "an undated task is in no range at all");
        }

        [TestMethod]
        public void AnEmptyFilterSetIsInactiveAndMatchesEverything()
        {
            var filters = new ToDoFilters();

            Assert.IsFalse(filters.IsActive);
            Assert.IsTrue(filters.Matches(Task("anything")));
            Assert.IsTrue(filters.Matches(Task("undated")));
        }

        [TestMethod]
        public void SettingAnyOneFilterMakesTheBarActive()
        {
            Assert.IsTrue(new ToDoFilters { ProjectId = "p" }.IsActive);
            Assert.IsTrue(new ToDoFilters { EnergyLevel = ToDoEnergyLevel.Low }.IsActive);
            Assert.IsTrue(new ToDoFilters { DueDateStart = Day(0) }.IsActive);
            Assert.IsTrue(new ToDoFilters { DueDateEnd = Day(0) }.IsActive);
        }
    }
}
