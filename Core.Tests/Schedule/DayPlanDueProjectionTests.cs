using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>: synced unfinished work has one canonical home — its
    /// due date, or today if overdue. Future days contain only work due on that day; viewing a future
    /// date must not age today's work.
    /// </summary>
    [TestClass]
    public class DayPlanDueProjectionTests
    {
        private const string Today = "2026-09-18";

        private static IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> Stored(
            params KeyValuePair<string, DayPlanTask[]>[] days)
        {
            return days.ToDictionary(d => d.Key, d => (IReadOnlyList<DayPlanTask>)d.Value.ToList());
        }

        private static KeyValuePair<string, DayPlanTask[]> Day(string key, params DayPlanTask[] tasks)
        {
            return new KeyValuePair<string, DayPlanTask[]>(key, tasks);
        }

        [TestMethod]
        public void DueDateIsCanonicalAndOverdueStartsToday()
        {
            Assert.AreEqual("2026-09-21", DayPlanDueProjection.CanonicalDay("2026-09-21", Today));
            Assert.AreEqual(Today, DayPlanDueProjection.CanonicalDay("2026-09-01", Today));
            Assert.IsNull(DayPlanDueProjection.CanonicalDay(null, Today));
            Assert.IsNull(DayPlanDueProjection.CanonicalDay("whenever", Today));
        }

        [TestMethod]
        public void ChangedDueDateRemovesOldPlacementWithoutReprioritising()
        {
            DayPlanTask task = new DayPlanTask("Task", sourceTaskId: "remote");
            var stored = Stored(Day("2026-09-21", task));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> result = DayPlanDueProjection.Reconcile(
                stored, new[] { new DayPlanDueProjection.DueRow("2026-09-23", task) }, Today);

            Assert.IsFalse(result.ContainsKey("2026-09-21"));
            CollectionAssert.AreEqual(new List<DayPlanTask> { task }, result["2026-09-23"].ToList());
            Assert.IsFalse(result.ContainsKey("2026-09-24"));
            Assert.AreEqual(1, result.Values.SelectMany(v => v).Count());
        }

        [TestMethod]
        public void DueTaskIgnoresFutureLegacyPlacementAndKeepsHistoryAndLocalTasks()
        {
            DayPlanTask task = new DayPlanTask("Reopened", sourceTaskId: "remote");
            DayPlanTask history = new DayPlanTask("Done previously", done: true, sourceTaskId: "remote-old");
            DayPlanTask local = new DayPlanTask("Local");
            DayPlanTask noLongerDue = new DayPlanTask("No date", sourceTaskId: "cleared");
            var stored = Stored(Day("2026-09-01", history),
                                Day(Today, local, noLongerDue),
                                Day("2026-09-30", task));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> result = DayPlanDueProjection.Reconcile(
                stored,
                new[]
                {
                    new DayPlanDueProjection.DueRow(Today, task),
                    new DayPlanDueProjection.DueRow(Today, task),   // the same to-do twice
                },
                Today);

            CollectionAssert.AreEqual(new List<DayPlanTask> { history }, result["2026-09-01"].ToList());
            CollectionAssert.AreEqual(new List<DayPlanTask> { local, task }, result[Today].ToList());
            Assert.IsFalse(result.ContainsKey("2026-09-30"), "the legacy future placement is not kept");
        }

        [TestMethod]
        public void PendingCompletionTodayDoesNotResurface()
        {
            DayPlanTask unfinished = new DayPlanTask("Just finished", sourceTaskId: "remote");
            DayPlanTask done = new DayPlanTask("Just finished", unfinished.Id, done: true, sourceTaskId: "remote");

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> result = DayPlanDueProjection.Reconcile(
                Stored(Day(Today, done)),
                new[] { new DayPlanDueProjection.DueRow(Today, unfinished) },
                Today);

            CollectionAssert.AreEqual(new List<DayPlanTask> { done }, result[Today].ToList());
        }

        [TestMethod]
        public void TheUsersRowOrderSurvivesARefresh()
        {
            DayPlanTask first = new DayPlanTask("First", sourceTaskId: "a");
            DayPlanTask second = new DayPlanTask("Second", sourceTaskId: "b");
            DayPlanTask third = new DayPlanTask("Third", sourceTaskId: "c");
            var stored = Stored(Day(Today, first, second, third));

            // The backend hands them back in whatever order it likes.
            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> result = DayPlanDueProjection.Reconcile(
                stored,
                new[]
                {
                    new DayPlanDueProjection.DueRow(Today, third),
                    new DayPlanDueProjection.DueRow(Today, first),
                    new DayPlanDueProjection.DueRow(Today, second),
                },
                Today);

            CollectionAssert.AreEqual(new List<DayPlanTask> { first, second, third }, result[Today].ToList());
        }

        [TestMethod]
        public void AnEmptiedDayIsDroppedRatherThanKeptAsAnEmptyRow()
        {
            var stored = Stored(Day("2026-09-21", new DayPlanTask("Task", sourceTaskId: "remote")));

            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> result =
                DayPlanDueProjection.Reconcile(stored, new List<DayPlanDueProjection.DueRow>(), Today);

            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// The day keys are plain <c>yyyy-MM-dd</c> strings and compare as such, which is what makes
        /// "overdue means today" a string comparison rather than date arithmetic. Pinned because a
        /// C# port could reasonably have reached for <see cref="DateTime"/> here and picked up a time
        /// zone along the way.
        /// </summary>
        [TestMethod]
        public void APlainDueDateRoundTripsWhateverTheMachinesTimeZone()
        {
            DateTimeOffset? parsed = ToDoDates.Parse("2026-09-18");
            Assert.IsTrue(parsed.HasValue);
            Assert.AreEqual("2026-09-18", ToDoDates.Ymd(parsed.Value));
            Assert.AreEqual("2026-09-18", DayPlanDueProjection.CanonicalDay("2026-09-18", "2026-09-18"));
        }

        [TestMethod]
        public void AnIsoTimestampIsAcceptedTooWithOrWithoutFractionalSeconds()
        {
            Assert.IsTrue(ToDoDates.Parse("2026-09-18T04:30:00.000Z").HasValue);
            Assert.IsTrue(ToDoDates.Parse("2026-09-18T04:30:00Z").HasValue);
            Assert.IsTrue(ToDoDates.Parse("2026-09-18T14:30:00+10:00").HasValue);
            Assert.IsNull(ToDoDates.Parse("   "));
            Assert.IsNull(ToDoDates.Parse("18/09/2026"), "only the API's own shapes are accepted");
        }
    }
}
