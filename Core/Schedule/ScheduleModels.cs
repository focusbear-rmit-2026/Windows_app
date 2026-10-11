using System;
using System.Collections.Generic;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>ScheduleKit.swift</c>. The inputs and outputs of
    /// <see cref="ScheduleBuilder"/>: the day's timetable, expressed without a single UI type so the
    /// timing rules can be reasoned about and tested on their own.
    ///
    /// Instants are <see cref="DateTimeOffset"/> rather than <see cref="DateTime"/> deliberately.
    /// Swift's <c>Date</c> is an absolute instant and <c>addingTimeInterval</c> is absolute-time
    /// arithmetic; a local <c>DateTime</c> would instead do wall-clock arithmetic and disagree with the
    /// Mac across a DST boundary (see <c>ScheduleBuilderTests.HabitCrossingSpringForwardKeepsItsRealLength</c>).
    /// Callers convert to local time for display.
    /// </summary>
    public enum ScheduleBlockKind
    {
        Habit,
        Meeting,
        DeepWork,
        /// <summary>Short break between deep-work sprints.</summary>
        BreakSprint,
        /// <summary>Overnight rest, after the evening routine.</summary>
        Sleep,
        /// <summary>Marker row: "Habits done".</summary>
        AllDone,
        /// <summary>Marker row: "Evening/Morning routine starts".</summary>
        RoutineTransition,
    }

    /// <summary>One row of the day's timetable.</summary>
    public sealed class ScheduleBlock
    {
        public ScheduleBlock(ScheduleBlockKind kind, string title, DateTimeOffset start, DateTimeOffset end,
                             int? habitIndex = null, bool isActionable = false, bool isCurrent = false,
                             string subtitle = null, bool didOverflow = false, string eventId = null,
                             string routineName = null)
        {
            Kind = kind;
            Title = title;
            Start = start;
            End = end;
            HabitIndex = habitIndex;
            IsActionable = isActionable;
            IsCurrent = isCurrent;
            Subtitle = subtitle;
            DidOverflow = didOverflow;
            EventId = eventId;
            RoutineName = routineName;
        }

        public ScheduleBlockKind Kind { get; }
        /// <summary>Includes the emoji for habits and meetings.</summary>
        public string Title { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        /// <summary>Index into the routine's remaining-habits array.</summary>
        public int? HabitIndex { get; set; }
        /// <summary>Show Play/Done/Skip on this card.</summary>
        public bool IsActionable { get; set; }

        /// <summary>
        /// The habit's settings can be edited from this card. Separate from <see cref="IsActionable"/>:
        /// a preview/future routine can be edited but not started.
        /// </summary>
        public bool IsEditable { get; set; }

        /// <summary>The next habit up (highlighted).</summary>
        public bool IsCurrent { get; set; }
        public string Subtitle { get; set; }
        /// <summary>The habit could not be placed before the day ended.</summary>
        public bool DidOverflow { get; set; }
        /// <summary>Calendar event identifier, so meetings can be dismissed.</summary>
        public string EventId { get; set; }
        /// <summary>For habits: which routine they belong to, used to group them.</summary>
        public string RoutineName { get; set; }
        /// <summary>Habit completed today — rendered struck through in "earlier today" sections.</summary>
        public bool IsDone { get; set; }
        /// <summary>The habit's server id — used to complete/skip a PAST habit by id.</summary>
        public string HabitId { get; set; }
        /// <summary>From an "earlier today" section (past routine); actioned by id, not index.</summary>
        public bool IsEarlier { get; set; }
        /// <summary>A meeting the user hid; shown only when "Show dismissed events" is on.</summary>
        public bool IsDismissed { get; set; }

        /// <summary>
        /// Overflow habits are zero-duration at the cursor (so they group and don't skew free time);
        /// this carries each habit's own length so the UI can lay the "not enough time today" rows out
        /// back-to-back instead of as a bogus 8:29–8:29 slot.
        /// </summary>
        public int? OverflowDurationSeconds { get; set; }

        public int DurationSeconds
        {
            get { return (int)(End - Start).TotalSeconds; }
        }
    }

    /// <summary>A habit offered to the builder for placement.</summary>
    public sealed class ScheduleHabitInput
    {
        public ScheduleHabitInput(int index, string title, int durationSeconds, bool isPlaying = false,
                                  int elapsedSeconds = 0, DateTimeOffset? cutoff = null, bool isDone = false,
                                  string id = null)
        {
            Index = index;
            Title = title;
            DurationSeconds = durationSeconds;
            IsPlaying = isPlaying;
            ElapsedSeconds = elapsedSeconds;
            Cutoff = cutoff;
            IsDone = isDone;
            Id = id;
        }

        /// <summary>Position in the routine's remaining-habits array.</summary>
        public int Index { get; }
        /// <summary>Emoji + name.</summary>
        public string Title { get; }
        public int DurationSeconds { get; }
        /// <summary>Currently running.</summary>
        public bool IsPlaying { get; }
        /// <summary>Already elapsed, for the playing habit.</summary>
        public int ElapsedSeconds { get; }
        /// <summary>Latest time it must be finished by; dropped once that time has passed.</summary>
        public DateTimeOffset? Cutoff { get; }
        /// <summary>Completed today — rendered struck through in "earlier today" sections.</summary>
        public bool IsDone { get; }
        /// <summary>The habit's server id — needed to complete/skip a PAST habit by id.</summary>
        public string Id { get; }
    }

    /// <summary>Who organises a meeting, as far as the calendar can prove.</summary>
    public enum EventOwnership
    {
        /// <summary>The signed-in user organises the event.</summary>
        Owned,
        /// <summary>Someone else organises it — the user is an attendee.</summary>
        NotOwned,
        /// <summary>The event records no organiser, so ownership genuinely cannot be established.</summary>
        Unknown,
    }

    /// <summary>One of today's timed calendar events.</summary>
    public sealed class ScheduleMeetingInput : IEquatable<ScheduleMeetingInput>
    {
        public ScheduleMeetingInput(string id, string title, DateTimeOffset start, DateTimeOffset end,
                                    bool isDismissed = false, EventOwnership ownership = EventOwnership.Unknown,
                                    bool hasOtherAttendees = false, bool isRecurring = false)
        {
            Id = id;
            Title = title;
            Start = start;
            End = end;
            IsDismissed = isDismissed;
            Ownership = ownership;
            HasOtherAttendees = hasOtherAttendees;
            IsRecurring = isRecurring;
        }

        public string Id { get; }
        public string Title { get; }
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
        /// <summary>Hidden by the user; surfaced (struck through) when "Show dismissed events" is on.</summary>
        public bool IsDismissed { get; }
        public EventOwnership Ownership { get; }
        /// <summary>Whether anyone besides the signed-in user is on it — the difference between
        /// "skip it quietly" and "someone has to be told".</summary>
        public bool HasOtherAttendees { get; }
        /// <summary>Whether it repeats, so a removal can ask which occurrences it means.</summary>
        public bool IsRecurring { get; }

        public bool Equals(ScheduleMeetingInput other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;
            return Id == other.Id && Title == other.Title && Start == other.Start && End == other.End
                   && IsDismissed == other.IsDismissed && Ownership == other.Ownership
                   && HasOtherAttendees == other.HasOtherAttendees && IsRecurring == other.IsRecurring;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScheduleMeetingInput);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id == null ? 0 : Id.GetHashCode();
                hash = (hash * 397) ^ Start.GetHashCode();
                hash = (hash * 397) ^ End.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// A routine (morning / evening / a custom routine) and the time window its habits may run in.
    /// Habits are packed inside [WindowStart, WindowEnd] and never spill past it, so one routine does
    /// not bleed into the next. Only the active routine's habits are actionable.
    /// </summary>
    public sealed class ScheduleRoutineSegment
    {
        public ScheduleRoutineSegment(string name, IReadOnlyList<ScheduleHabitInput> habits,
                                      DateTimeOffset windowStart, DateTimeOffset windowEnd, bool actionable,
                                      bool isEarlier = false)
        {
            Name = name;
            Habits = habits ?? new List<ScheduleHabitInput>();
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            Actionable = actionable;
            IsEarlier = isEarlier;
        }

        public string Name { get; }
        public IReadOnlyList<ScheduleHabitInput> Habits { get; }
        public DateTimeOffset WindowStart { get; }
        public DateTimeOffset WindowEnd { get; }
        public bool Actionable { get; }

        /// <summary>
        /// #2010: an "earlier today" section (the past morning routine, shown under the "Show events
        /// from earlier today" toggle once we are past it). The builder lays these out at their
        /// historical window instead of clamping the start up to <c>now</c> (which would drop a wholly
        /// past window), and it does not apply the per-habit cutoff drop — they are read-only history.
        /// </summary>
        public bool IsEarlier { get; }
    }

    /// <summary>A half-open span of time. The Swift engine uses <c>(Date, Date)</c> tuples for these.</summary>
    public struct TimeRange : IEquatable<TimeRange>
    {
        public TimeRange(DateTimeOffset start, DateTimeOffset end)
        {
            Start = start;
            End = end;
        }

        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }

        public double TotalSeconds
        {
            get { return (End - Start).TotalSeconds; }
        }

        public bool Equals(TimeRange other)
        {
            return Start == other.Start && End == other.End;
        }

        public override bool Equals(object obj)
        {
            return obj is TimeRange && Equals((TimeRange)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return (Start.GetHashCode() * 397) ^ End.GetHashCode(); }
        }

        public override string ToString()
        {
            return Start.ToString("O") + "–" + End.ToString("O");
        }
    }
}
