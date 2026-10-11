using System;

namespace FocusBear.Core.Schedule
{
    // `EventOwnership` itself already lives in `ScheduleModels.cs`, where `ScheduleMeetingInput`
    // carries it. It is not redefined here: one spelling of "who owns this meeting" is the reason the
    // Fix modal and the event editor cannot disagree about when cancelling is safe.

    public static class EventOwnershipResolver
    {
        /// <param name="organizerIsCurrentUser">Null when the event carries no organiser at all.</param>
        public static EventOwnership Ownership(bool? organizerIsCurrentUser)
        {
            if (!organizerIsCurrentUser.HasValue) return EventOwnership.Unknown;
            return organizerIsCurrentUser.Value ? EventOwnership.Owned : EventOwnership.NotOwned;
        }
    }

    /// <summary>Which occurrences of a repeating event an edit applies to.</summary>
    public enum EventSpan
    {
        ThisEvent,
        FutureEvents,
    }

    /// <summary>The destructive action the event editor offers.</summary>
    public enum EventCancelAction
    {
        /// <summary>Organiser: remove the event, which a synced calendar account passes on to the
        /// attendees as a cancellation.</summary>
        CancelEvent,

        /// <summary>Attendee (or unknown ownership): remove only the user's own copy of the event.</summary>
        DeclineEvent,
    }

    public static class EventCancelActions
    {
        /// <summary>
        /// Ownership picks the action, and UNKNOWN ownership picks the non-destructive one.
        ///
        /// Declining only ever touches the user's own copy; cancelling takes the meeting away from
        /// everyone on it, and that must never happen on a guess.
        /// </summary>
        public static EventCancelAction For(EventOwnership ownership)
        {
            return ownership == EventOwnership.Owned
                ? EventCancelAction.CancelEvent
                : EventCancelAction.DeclineEvent;
        }

        /// <summary>True only for the action that reaches other people's calendars too.</summary>
        public static bool AffectsOtherAttendees(EventCancelAction action)
        {
            return action == EventCancelAction.CancelEvent;
        }
    }

    /// <summary>How the confirm → scope → remove sequence ended.</summary>
    public enum EventCancellationOutcomeKind
    {
        Removed,

        /// <summary>The user backed out, at the confirmation or at the occurrence-scope question.</summary>
        CancelledByUser,

        /// <summary>The user confirmed but the calendar store refused the removal (a read-only
        /// subscribed calendar, an event already gone).</summary>
        Failed,
    }

    public struct EventCancellationOutcome : IEquatable<EventCancellationOutcome>
    {
        private EventCancellationOutcome(EventCancellationOutcomeKind kind, EventSpan? span)
        {
            Kind = kind;
            Span = span;
        }

        public EventCancellationOutcomeKind Kind { get; }

        /// <summary>The span the removal used; null unless <see cref="Kind"/> is
        /// <see cref="EventCancellationOutcomeKind.Removed"/>.</summary>
        public EventSpan? Span { get; }

        public static EventCancellationOutcome Removed(EventSpan span)
        {
            return new EventCancellationOutcome(EventCancellationOutcomeKind.Removed, span);
        }

        public static readonly EventCancellationOutcome CancelledByUser =
            new EventCancellationOutcome(EventCancellationOutcomeKind.CancelledByUser, null);

        public static readonly EventCancellationOutcome Failed =
            new EventCancellationOutcome(EventCancellationOutcomeKind.Failed, null);

        public bool Equals(EventCancellationOutcome other)
        {
            return Kind == other.Kind && Span == other.Span;
        }

        public override bool Equals(object obj)
        {
            return obj is EventCancellationOutcome && Equals((EventCancellationOutcome)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((int)Kind * 397) ^ (Span.HasValue ? (int)Span.Value + 1 : 0); }
        }

        public override string ToString()
        {
            return Kind == EventCancellationOutcomeKind.Removed ? "Removed(" + Span + ")" : Kind.ToString();
        }
    }

    /// <summary>
    /// Drives "confirm, then (for a repeating event) ask which occurrences, then remove".
    ///
    /// Pure, with the three interactive steps injected, so the rule that matters — NOTHING is removed
    /// until the user has confirmed — is covered by tests rather than by reading the screen that
    /// implements it. Every early return in here is a path on which <c>remove</c> is never called.
    /// </summary>
    public static class EventCancellationFlow
    {
        /// <param name="isRecurring">Whether the this-occurrence-vs-series question applies at all.</param>
        /// <param name="knownScope">
        /// The span the user has ALREADY chosen for this editing session. The editor asks when the
        /// form opens, so by the time the destructive action runs the answer normally exists; asking
        /// a second time is the bug this parameter exists to prevent. Null means nobody has asked yet.
        /// </param>
        /// <param name="confirm">Shows the destructive confirmation, calling back with the answer.</param>
        /// <param name="askScope">The repeating-event question; null back means "back out".</param>
        /// <param name="remove">Performs the removal, returning whether the store accepted it.</param>
        public static void Run(EventCancelAction action, bool isRecurring, EventSpan? knownScope,
                               Action<EventCancelAction, Action<bool>> confirm,
                               Action<Action<EventSpan?>> askScope,
                               Func<EventSpan, bool> remove,
                               Action<EventCancellationOutcome> completion)
        {
            Action<EventSpan> finish = span =>
                completion(remove(span) ? EventCancellationOutcome.Removed(span)
                                        : EventCancellationOutcome.Failed);

            confirm(action, confirmed =>
            {
                if (!confirmed)
                {
                    completion(EventCancellationOutcome.CancelledByUser);
                    return;
                }

                // A one-off event has a single occurrence to remove, so there is nothing to ask —
                // even when a scope happens to have been recorded.
                if (!isRecurring)
                {
                    finish(EventSpan.ThisEvent);
                    return;
                }

                if (knownScope.HasValue)
                {
                    finish(knownScope.Value);
                    return;
                }

                askScope(span =>
                {
                    if (!span.HasValue)
                    {
                        completion(EventCancellationOutcome.CancelledByUser);
                        return;
                    }
                    finish(span.Value);
                });
            });
        }
    }
}
