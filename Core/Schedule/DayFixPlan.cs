using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// A way to make an over-committed day fit.
    ///
    /// The order these are DECLARED in is the order the report listed them, and
    /// <see cref="DayFixRoute"/>'s numeric values are that order — it breaks ties in
    /// <see cref="DayFixPlan.Options"/>, so the list is stable rather than arbitrary.
    /// </summary>
    public enum DayFixRoute
    {
        /// <summary>Cancel, decline, skip or shorten the day's meetings.</summary>
        Meetings = 0,

        /// <summary>Skip or shorten the day's habits.</summary>
        Routine = 1,

        /// <summary>Move tasks out of the day, on the re-prioritise screen.</summary>
        Tasks = 2,
    }

    /// <summary>One offered route with the room it could free.</summary>
    public struct DayFixOption : IEquatable<DayFixOption>
    {
        public DayFixOption(DayFixRoute route, double freeableMinutes)
        {
            Route = route;
            FreeableMinutes = freeableMinutes;
        }

        public DayFixRoute Route { get; }

        /// <summary>Minutes currently spent on this kind of thing today — the most it could free.</summary>
        public double FreeableMinutes { get; }

        public bool Equals(DayFixOption other)
        {
            return Route == other.Route && FreeableMinutes.Equals(other.FreeableMinutes);
        }

        public override bool Equals(object obj)
        {
            return obj is DayFixOption && Equals((DayFixOption)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((int)Route * 397) ^ FreeableMinutes.GetHashCode(); }
        }

        public override string ToString()
        {
            return Route + "(" + FreeableMinutes + "m)";
        }
    }

    /// <summary>
    /// What a single meeting on the Fix list offers: "Cancel (if they own it) / Decline (if there are
    /// other attendees but they don't own it) / Skip (if there are no other attendees)".
    /// </summary>
    public enum DayFixMeetingAction
    {
        /// <summary>Nobody else is on the meeting, so nothing needs to leave this machine: hide it
        /// from today's plan.</summary>
        Skip,

        /// <summary>The user organises it — removing it makes the account send the cancellation on to
        /// the attendees.</summary>
        Cancel,

        /// <summary>Someone else organises it — removing it takes the user's own copy off their
        /// calendar.</summary>
        Decline,
    }

    /// <summary>
    /// "Fix" — turning the Day Plan's red over-capacity warning into an action.
    ///
    /// Pure decisions only: which routes are worth offering, in what order, what each list inside a
    /// route shows, and what a single meeting is allowed to offer. The screens and the calendar
    /// writes stay with the host.
    /// </summary>
    public static class DayFixPlan
    {
        /// <summary>
        /// The routes worth showing, BIGGEST FIRST.
        ///
        /// Ordering by capacity freed rather than by the order the report listed them is the point of
        /// this method: three equal buttons make the user do arithmetic the app has already done. If
        /// the meetings are six hours and the routine is twenty minutes, the routine is not the answer
        /// and should not be the first thing offered. Ties keep the report's own order, which reads
        /// meetings → routine → tasks.
        ///
        /// A route with nothing behind it is NOT OFFERED AT ALL: an option that cannot free a minute
        /// is a dead button, and the whole point of this modal is that every row in it changes the
        /// number at the top.
        /// </summary>
        public static IList<DayFixOption> Options(double meetingMinutes, double habitMinutes,
                                                  double taskMinutes)
        {
            return new[]
                {
                    new DayFixOption(DayFixRoute.Meetings, meetingMinutes),
                    new DayFixOption(DayFixRoute.Routine, habitMinutes),
                    new DayFixOption(DayFixRoute.Tasks, taskMinutes),
                }
                .Where(option => option.FreeableMinutes > 0)
                .OrderByDescending(option => option.FreeableMinutes)
                .ThenBy(option => (int)option.Route)
                .ToList();
        }

        /// <summary>
        /// Rows for a list inside the modal: LONGEST FIRST, because that is where the minutes are and
        /// the report asked for it ("habits sorted by duration descending"). Ties fall back to the
        /// name so the list does not reshuffle between renders.
        ///
        /// The name compares as Swift's <c>&lt;</c> does, not ordinally: an accented or emoji habit
        /// title would otherwise tie-break differently on the two clients.
        /// </summary>
        public static IList<T> LongestFirst<T>(IEnumerable<T> rows, Func<T, int> seconds, Func<T, string> name)
        {
            return (rows ?? Enumerable.Empty<T>())
                .OrderByDescending(seconds)
                .ThenBy(name, SwiftStringComparer.Instance)
                .ToList();
        }

        /// <summary>
        /// The habits the routine route may offer: the ones that sit inside the working day.
        ///
        /// "Evening routine isn't relevant as we assume all tasks get completed before evening routine
        /// — just offer to shorten morning routine". Shortening something that runs AFTER the tasks
        /// were supposed to be done frees nothing for those tasks, and counting it made the route
        /// claim 5h 54m of habits on a day whose morning routine was a fraction of that.
        ///
        /// The rule is the reason, not the routine's name: anything starting at or after the evening
        /// routine begins is out, which also excludes a custom routine parked at the end of the day,
        /// while a custom routine at lunchtime — which really does eat into the day's tasks — stays in.
        ///
        /// With no evening routine time there is no evening to be after, so nothing is dropped on a
        /// guess: hiding a habit the user could have shortened is worse than listing one they won't.
        /// </summary>
        public static IList<T> HabitsInsideWorkingDay<T>(IEnumerable<T> habits,
                                                         Func<T, DateTimeOffset> start,
                                                         Func<T, bool> isEveningRoutine,
                                                         DateTimeOffset? eveningRoutineStart)
        {
            return (habits ?? Enumerable.Empty<T>())
                .Where(habit =>
                {
                    if (isEveningRoutine(habit)) return false;
                    if (!eveningRoutineStart.HasValue) return true;
                    return start(habit) < eveningRoutineStart.Value;
                })
                .ToList();
        }

        /// <summary>
        /// Which action one meeting gets.
        ///
        /// The ownership half deliberately keeps coming from <see cref="EventCancelActions.For"/>, so
        /// the rule that UNKNOWN ownership never offers to cancel — that would take the meeting off
        /// everyone else's calendar on a guess — is stated in exactly one place.
        /// </summary>
        public static DayFixMeetingAction MeetingAction(bool hasOtherAttendees, EventOwnership ownership)
        {
            // No one else is on it, so there is nobody to notify and nothing to decline.
            if (!hasOtherAttendees) return DayFixMeetingAction.Skip;

            return EventCancelActions.For(ownership) == EventCancelAction.CancelEvent
                ? DayFixMeetingAction.Cancel
                : DayFixMeetingAction.Decline;
        }
    }
}
