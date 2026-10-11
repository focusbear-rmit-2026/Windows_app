using System;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Ported from the completion-banner half of <c>ToDoLogicTests.swift</c>: one banner that absorbs
    /// rapid completions instead of stacking a new one over the list the user is ticking.
    /// </summary>
    [TestClass]
    public class ToDoCompletionBatchTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void RapidCompletionsJoinOneBatch()
        {
            ToDoCompletionBatch first = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual("A", first.LastTitle);

            ToDoCompletionBatch second = ToDoCompletionBatch.Recording("B", first, "Cool", T0.AddSeconds(2));
            Assert.AreEqual(2, second.Count, "A completion within the lifetime joins the batch");
            Assert.AreEqual("Woah", second.Phrase, "The batch keeps its first phrase");
            Assert.AreEqual("B", second.LastTitle);
            Assert.AreEqual(T0.AddSeconds(2) + ToDoCompletionBatch.Lifetime, second.ExpiresAt,
                            "Each completion restarts the clock");
        }

        [TestMethod]
        public void AnExpiredBatchStartsOver()
        {
            ToDoCompletionBatch batch = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            ToDoCompletionBatch afterExpiry =
                ToDoCompletionBatch.Recording("C", batch, "Cool", batch.ExpiresAt.AddSeconds(1));

            Assert.AreEqual(1, afterExpiry.Count);
            Assert.AreEqual("Cool", afterExpiry.Phrase, "A new banner gets the new phrase");
        }

        /// <summary>Expiry is inclusive: exactly on the deadline the old banner is already gone.</summary>
        [TestMethod]
        public void ACompletionExactlyOnTheDeadlineStartsANewBatch()
        {
            ToDoCompletionBatch batch = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            Assert.IsTrue(batch.IsExpired(batch.ExpiresAt));
            Assert.AreEqual(1, ToDoCompletionBatch.Recording("B", batch, "Cool", batch.ExpiresAt).Count);
        }

        [TestMethod]
        public void UncompletingDecrementsAndThenHidesTheBanner()
        {
            ToDoCompletionBatch batch = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            batch = ToDoCompletionBatch.Recording("B", batch, "Woah", T0);

            ToDoCompletionBatch decremented = batch.Uncompleting(T0.AddSeconds(1));
            Assert.AreEqual(1, decremented.Count);
            Assert.IsNull(decremented.Uncompleting(T0.AddSeconds(1)),
                          "Taking back the last completion hides the banner");
            Assert.IsNull(batch.Uncompleting(batch.ExpiresAt.AddSeconds(1)),
                          "Nothing to take back once it has expired");
        }

        /// <summary>
        /// Un-ticking is a correction, not an achievement: it must not extend the banner, or a user
        /// fixing a mis-click would hold their own congratulations on screen.
        /// </summary>
        [TestMethod]
        public void UncompletingDoesNotRestartTheClock()
        {
            ToDoCompletionBatch batch = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            batch = ToDoCompletionBatch.Recording("B", batch, "Woah", T0);

            Assert.AreEqual(batch.ExpiresAt, batch.Uncompleting(T0.AddSeconds(1)).ExpiresAt);
        }

        /// <summary>
        /// The title shown is the LAST one completed, kept even as the count comes back down — the
        /// banner names the task that put it there, not the one that was taken back.
        /// </summary>
        [TestMethod]
        public void TheBannerNamesTheMostRecentlyCompletedTask()
        {
            ToDoCompletionBatch batch = ToDoCompletionBatch.Recording("A", null, "Woah", T0);
            batch = ToDoCompletionBatch.Recording("B", batch, "Woah", T0);
            Assert.AreEqual("B", batch.Uncompleting(T0.AddSeconds(1)).LastTitle);
        }
    }
}
