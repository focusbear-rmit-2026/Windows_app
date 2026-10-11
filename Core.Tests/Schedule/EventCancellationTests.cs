using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>EventCancellationTests.swift</c>: which of "Cancel Event" / "Decline event" the
    /// Day Plan's event editor offers, and the rule that NOTHING is removed until the user confirms.
    ///
    /// The button and confirmation copy is not ported — Core carries no translations — but everything
    /// that decides WHICH action runs, and when, is.
    /// </summary>
    [TestClass]
    public class EventCancellationTests
    {
        // MARK: - Ownership → action

        [TestMethod]
        public void TheOrganiserBeingTheCurrentUserMeansTheEventIsOwned()
        {
            Assert.AreEqual(EventOwnership.Owned, EventOwnershipResolver.Ownership(true));
        }

        [TestMethod]
        public void SomeoneElsesOrganiserMeansTheEventIsNotOwned()
        {
            Assert.AreEqual(EventOwnership.NotOwned, EventOwnershipResolver.Ownership(false));
        }

        /// <summary>A hand-made event in a local calendar carries no organiser — the store simply
        /// cannot say.</summary>
        [TestMethod]
        public void NoOrganiserAtAllLeavesOwnershipUnknown()
        {
            Assert.AreEqual(EventOwnership.Unknown, EventOwnershipResolver.Ownership(null));
        }

        [TestMethod]
        public void TheOwnerGetsCancelEvent()
        {
            EventCancelAction action = EventCancelActions.For(EventOwnership.Owned);
            Assert.AreEqual(EventCancelAction.CancelEvent, action);
            Assert.IsTrue(EventCancelActions.AffectsOtherAttendees(action),
                          "Cancelling takes the meeting off everyone's calendar");
        }

        [TestMethod]
        public void ANonOwnerGetsDeclineEvent()
        {
            EventCancelAction action = EventCancelActions.For(EventOwnership.NotOwned);
            Assert.AreEqual(EventCancelAction.DeclineEvent, action);
            Assert.IsFalse(EventCancelActions.AffectsOtherAttendees(action),
                           "Declining must leave the event intact for everyone else");
        }

        [TestMethod]
        public void UnknownOwnershipFallsBackToTheNonDestructiveAction()
        {
            EventCancelAction action = EventCancelActions.For(EventOwnership.Unknown);
            Assert.AreEqual(EventCancelAction.DeclineEvent, action,
                            "Never offer to cancel a meeting for everyone on a guess");
            Assert.IsFalse(EventCancelActions.AffectsOtherAttendees(action));
        }

        // MARK: - The confirm → scope → remove sequence

        /// <summary>Records what the flow asked for, so a test can assert the ORDER as well as the
        /// outcome.</summary>
        private sealed class FlowSpy
        {
            internal int ConfirmCount;
            internal int ScopeCount;
            internal readonly List<EventSpan> RemovedSpans = new List<EventSpan>();
            internal EventCancellationOutcome? Outcome;

            internal bool ConfirmAnswer = true;
            internal EventSpan? ScopeAnswer = EventSpan.ThisEvent;
            internal bool RemoveSucceeds = true;

            internal void Run(EventCancelAction action, bool isRecurring, EventSpan? knownScope = null)
            {
                EventCancellationFlow.Run(
                    action,
                    isRecurring,
                    knownScope,
                    (_, done) => { ConfirmCount++; done(ConfirmAnswer); },
                    done => { ScopeCount++; done(ScopeAnswer); },
                    span => { RemovedSpans.Add(span); return RemoveSucceeds; },
                    outcome => Outcome = outcome);
            }
        }

        [TestMethod]
        public void NothingIsRemovedUntilTheUserConfirms()
        {
            var spy = new FlowSpy { ConfirmAnswer = false };
            spy.Run(EventCancelAction.CancelEvent, false);

            Assert.AreEqual(1, spy.ConfirmCount, "The confirmation is not optional");
            Assert.AreEqual(0, spy.RemovedSpans.Count, "Declining the confirmation must remove nothing");
            Assert.AreEqual(EventCancellationOutcome.CancelledByUser, spy.Outcome);
        }

        [TestMethod]
        public void NothingIsRemovedUntilTheUserConfirmsARepeatingEventEither()
        {
            var spy = new FlowSpy { ConfirmAnswer = false };
            spy.Run(EventCancelAction.DeclineEvent, true);

            Assert.AreEqual(0, spy.ScopeCount,
                            "Backing out at the confirmation must not go on to ask the scope");
            Assert.AreEqual(0, spy.RemovedSpans.Count);
            Assert.AreEqual(EventCancellationOutcome.CancelledByUser, spy.Outcome);
        }

        [TestMethod]
        public void ConfirmingAOneOffEventRemovesJustThatEventWithoutAskingTheScope()
        {
            var spy = new FlowSpy();
            spy.Run(EventCancelAction.CancelEvent, false);

            Assert.AreEqual(0, spy.ScopeCount,
                            "A non-repeating event has no this-occurrence-vs-series question");
            CollectionAssert.AreEqual(new[] { EventSpan.ThisEvent }, spy.RemovedSpans);
            Assert.AreEqual(EventCancellationOutcome.Removed(EventSpan.ThisEvent), spy.Outcome);
        }

        [TestMethod]
        public void ARepeatingEventIsAskedTheScopeAfterConfirmation()
        {
            var spy = new FlowSpy { ScopeAnswer = EventSpan.FutureEvents };
            spy.Run(EventCancelAction.CancelEvent, true);

            Assert.AreEqual(1, spy.ConfirmCount);
            Assert.AreEqual(1, spy.ScopeCount);
            CollectionAssert.AreEqual(new[] { EventSpan.FutureEvents }, spy.RemovedSpans);
            Assert.AreEqual(EventCancellationOutcome.Removed(EventSpan.FutureEvents), spy.Outcome);
        }

        [TestMethod]
        public void BackingOutOfTheScopeQuestionRemovesNothing()
        {
            var spy = new FlowSpy { ScopeAnswer = null };
            spy.Run(EventCancelAction.CancelEvent, true);

            Assert.AreEqual(0, spy.RemovedSpans.Count,
                            "Cancel on the scope question must leave the event alone");
            Assert.AreEqual(EventCancellationOutcome.CancelledByUser, spy.Outcome);
        }

        // MARK: - The scope is asked ONCE (Mac report 2026-09-15_20-53-31)

        /// <summary>
        /// "When I declined a calendar series it asked me this at the end — I'd already said at the
        /// start that I wanted to edit all future events."
        /// </summary>
        [TestMethod]
        public void AScopeAlreadyChosenWhenTheFormOpenedIsNotAskedForAgain()
        {
            var spy = new FlowSpy();
            spy.Run(EventCancelAction.DeclineEvent, true, EventSpan.FutureEvents);

            Assert.AreEqual(1, spy.ConfirmCount, "The destructive confirmation is still shown");
            Assert.AreEqual(0, spy.ScopeCount,
                            "The user answered this when the form opened; asking again is the bug");
            Assert.AreEqual(EventCancellationOutcome.Removed(EventSpan.FutureEvents), spy.Outcome);
        }

        /// <summary>Asking once but then removing the wrong span would be worse than asking twice.</summary>
        [TestMethod]
        public void TheScopeChosenWhenTheFormOpenedIsTheOneTheRemovalUses()
        {
            foreach (EventSpan chosen in Enum.GetValues(typeof(EventSpan)).Cast<EventSpan>())
            {
                var spy = new FlowSpy
                {
                    // Must be ignored.
                    ScopeAnswer = chosen == EventSpan.ThisEvent ? EventSpan.FutureEvents : EventSpan.ThisEvent,
                };
                spy.Run(EventCancelAction.CancelEvent, true, chosen);

                CollectionAssert.AreEqual(new[] { chosen }, spy.RemovedSpans,
                                          "The removal must apply to the span the user picked");
                Assert.AreEqual(EventCancellationOutcome.Removed(chosen), spy.Outcome);
            }
        }

        /// <summary>A one-off event has a single occurrence; there is no series question to answer.</summary>
        [TestMethod]
        public void ANonRecurringEventIsNeverAskedTheScopeEvenWithOneRecorded()
        {
            var spy = new FlowSpy();
            spy.Run(EventCancelAction.DeclineEvent, false, EventSpan.FutureEvents);

            Assert.AreEqual(0, spy.ScopeCount);
            CollectionAssert.AreEqual(new[] { EventSpan.ThisEvent }, spy.RemovedSpans,
                                      "A one-off event only ever removes itself");
        }

        /// <summary>
        /// The editor normally asks when the form opens, so this is the fallback path — it must still
        /// ask rather than silently guessing a span.
        /// </summary>
        [TestMethod]
        public void ARepeatingEventWithNoRecordedScopeStillGetsAsked()
        {
            var spy = new FlowSpy { ScopeAnswer = EventSpan.FutureEvents };
            spy.Run(EventCancelAction.CancelEvent, true, null);

            Assert.AreEqual(1, spy.ScopeCount);
            CollectionAssert.AreEqual(new[] { EventSpan.FutureEvents }, spy.RemovedSpans);
        }

        [TestMethod]
        public void ARecordedScopeStillWaitsForTheDestructiveConfirmation()
        {
            var spy = new FlowSpy { ConfirmAnswer = false };
            spy.Run(EventCancelAction.CancelEvent, true, EventSpan.FutureEvents);

            Assert.AreEqual(0, spy.RemovedSpans.Count, "A recorded scope must not skip the confirmation");
            Assert.AreEqual(EventCancellationOutcome.CancelledByUser, spy.Outcome);
        }

        /// <summary>
        /// A read-only calendar (a subscribed feed) rejects the removal; the editor must say so
        /// instead of closing as if the event had gone.
        /// </summary>
        [TestMethod]
        public void ARefusedRemovalReportsFailureRatherThanPretendingItWorked()
        {
            var spy = new FlowSpy { RemoveSucceeds = false };
            spy.Run(EventCancelAction.DeclineEvent, false);

            CollectionAssert.AreEqual(new[] { EventSpan.ThisEvent }, spy.RemovedSpans);
            Assert.AreEqual(EventCancellationOutcome.Failed, spy.Outcome);
        }
    }
}
