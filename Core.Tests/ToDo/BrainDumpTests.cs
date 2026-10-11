using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Ported from the brain-dump half of <c>ToDoLogicTests.swift</c>: the body one accepted AI
    /// suggestion is written as, and the sequential writer that decides which suggestions leave the
    /// review draft.
    ///
    /// The writer's cases are the ones worth having. Every one of them is a way the obvious
    /// implementation (fire them all at once) loses the user's work: a partial failure the draft
    /// cannot describe, a sign-out mid-run, a dismissed draft edited by a late response, and a
    /// callback delivered twice.
    /// </summary>
    [TestClass]
    public class BrainDumpTests
    {
        private const string Today = "2026-09-10";

        /// <summary>A run's write calls, held so a test can answer them in its own order.</summary>
        private sealed class WriterHarness
        {
            internal readonly List<string> Attempted = new List<string>();
            internal readonly List<Action<bool>> Responses = new List<Action<bool>>();
            internal readonly List<string> Saved = new List<string>();
            internal readonly List<BrainDumpWriteResult> Results = new List<BrainDumpWriteResult>();
            internal bool IsCurrent = true;

            internal BrainDumpTaskWriter Writer(IEnumerable<BrainDumpTask> tasks, bool answerImmediately = false)
            {
                return new BrainDumpTaskWriter(
                    tasks,
                    () => IsCurrent,
                    (task, done) =>
                    {
                        Attempted.Add(task.Id);
                        if (answerImmediately) done(true);
                        else Responses.Add(done);
                    },
                    id => Saved.Add(id),
                    result => Results.Add(result));
            }
        }

        private static IList<BrainDumpTask> Tasks(int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => new BrainDumpTask("Task " + index, 5, id: "task-" + index))
                .ToList();
        }

        // MARK: - The writer

        /// <summary>
        /// The whole reason the run is sequential: the draft is left holding EXACTLY the tasks the
        /// server did not confirm, so retrying it is an upsert of the same ids rather than a guess.
        /// </summary>
        [TestMethod]
        public void PartialFailureKeepsUnconfirmedIdsForRetry()
        {
            IList<BrainDumpTask> tasks = Tasks(3);
            var harness = new WriterHarness();
            harness.Writer(tasks).Start();

            CollectionAssert.AreEqual(new[] { tasks[0].Id }, harness.Attempted,
                                      "One write in flight at a time");
            harness.Responses[0](true);
            CollectionAssert.AreEqual(new[] { tasks[0].Id, tasks[1].Id }, harness.Attempted);

            // A duplicate callback for a write already answered cannot advance the run.
            harness.Responses[0](true);
            CollectionAssert.AreEqual(new[] { tasks[0].Id, tasks[1].Id }, harness.Attempted,
                                      "A response delivered twice must not skip a task");

            harness.Responses[1](false);
            CollectionAssert.AreEqual(new[] { tasks[0].Id }, harness.Saved);
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Failed(1) }, harness.Results);

            // What the review screen would do next: retry only what is left.
            IList<BrainDumpTask> pending = tasks.Where(task => !harness.Saved.Contains(task.Id)).ToList();
            harness.Writer(pending, answerImmediately: true).Start();

            CollectionAssert.AreEqual(new[] { tasks[0].Id, tasks[1].Id, tasks[1].Id, tasks[2].Id },
                                      harness.Attempted);
            CollectionAssert.AreEqual(tasks.Select(task => task.Id).ToArray(), harness.Saved);
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Failed(1), BrainDumpWriteResult.Complete },
                                      harness.Results);
        }

        /// <summary>
        /// A sign-out mid-run must not go on writing the previous account's tasks, and must not touch
        /// the draft either — a confirmation that arrives after the account changed is not ours to act on.
        /// </summary>
        [TestMethod]
        public void AnAccountChangeStopsWritesAndDraftRemoval()
        {
            var harness = new WriterHarness();
            harness.Writer(Tasks(2)).Start();

            harness.IsCurrent = false;
            harness.Responses[0](true);

            Assert.AreEqual(1, harness.Attempted.Count, "The second task is never written");
            Assert.AreEqual(0, harness.Saved.Count, "And the first is not removed from the draft");
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Superseded }, harness.Results);
        }

        [TestMethod]
        public void ADismissedDraftIgnoresALateResponse()
        {
            var harness = new WriterHarness();
            BrainDumpTaskWriter writer = harness.Writer(Tasks(1));
            writer.Start();
            writer.Cancel();

            harness.Responses[0](true);

            Assert.AreEqual(0, harness.Saved.Count, "A dismissed draft must not be changed");
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Superseded }, harness.Results,
                                      "And it is reported exactly once");
        }

        [TestMethod]
        public void AnEmptyRunCompletesWithoutWritingAnything()
        {
            var harness = new WriterHarness();
            harness.Writer(new List<BrainDumpTask>()).Start();

            Assert.AreEqual(0, harness.Attempted.Count);
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Complete }, harness.Results);
        }

        /// <summary>Cancelling after the run ended cannot rewrite how it ended.</summary>
        [TestMethod]
        public void ARunThatAlreadyFinishedReportsItsOutcomeOnce()
        {
            var harness = new WriterHarness();
            BrainDumpTaskWriter writer = harness.Writer(Tasks(1), answerImmediately: true);
            writer.Start();
            writer.Cancel();

            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Complete }, harness.Results);
        }

        /// <summary>An account already gone when the run starts writes nothing at all.</summary>
        [TestMethod]
        public void ARunStartedAfterTheAccountChangedNeverWrites()
        {
            var harness = new WriterHarness { IsCurrent = false };
            harness.Writer(Tasks(2)).Start();

            Assert.AreEqual(0, harness.Attempted.Count);
            CollectionAssert.AreEqual(new[] { BrainDumpWriteResult.Superseded }, harness.Results);
        }

        // MARK: - The body one suggestion is written as

        [TestMethod]
        public void RetryPayloadPreservesIdentityAndNullableRatings()
        {
            var task = new BrainDumpTask("Review report", 10, new[] { "Read draft" });

            IDictionary<string, object> first = BrainDumpTaskPayload.Body(task, Today);
            Assert.AreEqual(task.Id.ToLowerInvariant(), first[BrainDumpTaskPayload.Keys.Id]);
            Assert.IsTrue(first.ContainsKey(BrainDumpTaskPayload.Keys.Outcome));
            Assert.IsNull(first[BrainDumpTaskPayload.Keys.Outcome],
                          "An unrated suggestion sends an explicit null, not an absent key");
            Assert.IsNull(first[BrainDumpTaskPayload.Keys.PerspirationLevel]);

            task.TaskName = "Review revised report";
            task.Outcome = 9;
            task.PerspirationLevel = 3;

            IDictionary<string, object> retry = BrainDumpTaskPayload.Body(task, Today);
            Assert.AreEqual(first[BrainDumpTaskPayload.Keys.Id], retry[BrainDumpTaskPayload.Keys.Id],
                            "A retry upserts the same row rather than creating a second copy");
            Assert.AreEqual("Review revised report", retry[BrainDumpTaskPayload.Keys.Title]);
            Assert.AreEqual(9, retry[BrainDumpTaskPayload.Keys.Outcome]);
            Assert.AreEqual(3, retry[BrainDumpTaskPayload.Keys.PerspirationLevel]);
            Assert.AreEqual(600, retry[BrainDumpTaskPayload.Keys.Duration], "Minutes become seconds");
        }

        /// <summary>
        /// The estimate is the AI's, not a person's, so the nonsense cases have to be refused here:
        /// no estimate at all, a negative one, and one whose seconds will not fit the server's column.
        /// </summary>
        [TestMethod]
        public void MissingAndOverflowingEffortIsRefused()
        {
            foreach (long minutes in new long[] { 0, -1, long.MaxValue, int.MaxValue / 60 + 1 })
            {
                Assert.IsNull(BrainDumpTaskPayload.Body(new BrainDumpTask("Task", minutes), Today),
                              minutes + " minutes is not a writable estimate");
            }
        }

        /// <summary>The largest estimate that still fits is written, so the guard is a boundary and
        /// not an arbitrary ceiling.</summary>
        [TestMethod]
        public void TheLargestEstimateThatFitsIsStillWritten()
        {
            const long minutes = int.MaxValue / 60;
            IDictionary<string, object> body = BrainDumpTaskPayload.Body(new BrainDumpTask("Task", minutes), Today);
            Assert.IsNotNull(body);
            Assert.AreEqual((int)(minutes * 60), body[BrainDumpTaskPayload.Keys.Duration]);
        }

        [TestMethod]
        public void ASuggestionWithNoTitleIsRefused()
        {
            Assert.IsNull(BrainDumpTaskPayload.Body(new BrainDumpTask("", 10), Today));
            Assert.IsNull(BrainDumpTaskPayload.Body(new BrainDumpTask("   \n ", 10), Today),
                          "Whitespace is not a title");
        }

        [TestMethod]
        public void BlankAiSubtasksAreDiscardedAndTheRestAreTrimmed()
        {
            var task = new BrainDumpTask("Review report", 10, new[] { "", " \n", " Read draft " });
            IDictionary<string, object> body = BrainDumpTaskPayload.Body(task, Today);

            var subtasks = (List<object>)body[BrainDumpTaskPayload.Keys.Subtasks];
            Assert.AreEqual(1, subtasks.Count);

            var only = (Dictionary<string, object>)subtasks[0];
            Assert.AreEqual("Read draft", only[ToDoUpsertBody.SubtaskKeys.Name]);
            Assert.AreEqual(false, only[ToDoUpsertBody.SubtaskKeys.IsCompleted]);
        }

        /// <summary>
        /// A suggestion with no date of its own is due TODAY. The point of a brain dump is to get
        /// what is in the user's head onto the day in front of them; an undated task would land in
        /// the backlog they were trying to empty.
        /// </summary>
        [TestMethod]
        public void ASuggestionWithNoDateOfItsOwnIsDueToday()
        {
            IDictionary<string, object> undated = BrainDumpTaskPayload.Body(new BrainDumpTask("Task", 10), Today);
            Assert.AreEqual(Today, undated[BrainDumpTaskPayload.Keys.DueDate]);

            var dated = new BrainDumpTask("Task", 10, dueDate: "2026-12-01");
            Assert.AreEqual("2026-12-01", BrainDumpTaskPayload.Body(dated, Today)[BrainDumpTaskPayload.Keys.DueDate]);
        }

        /// <summary>Every suggestion starts as an un-started "do it" task — the AI does not triage.</summary>
        [TestMethod]
        public void EverySuggestionStartsUnstartedInTheDoItQuadrant()
        {
            IDictionary<string, object> body = BrainDumpTaskPayload.Body(new BrainDumpTask("Task", 10), Today);
            Assert.AreEqual(ToDoStatusWire.NotStarted, body[BrainDumpTaskPayload.Keys.Status]);
            Assert.AreEqual((int)EisenhowerQuadrant.DoIt, body[BrainDumpTaskPayload.Keys.EisenhowerQuadrant]);
            Assert.AreEqual(0, ((List<object>)body[BrainDumpTaskPayload.Keys.Tags]).Count);
        }
    }
}
