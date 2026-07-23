using System;
using System.Collections.Generic;

namespace Focus_Bear.Habits.DayPlan
{
    // C# port of the macOS Day Plan model types (Mac-App: FocusBear/Schedule/ScheduleKit.swift).
    // Pure data — no WPF/UI dependency — so the scheduling logic can be reasoned about and
    // unit-tested in isolation. Rendering lives in HabitsDayPlan (later milestones).

    public enum ScheduleBlockKind
    {
        Habit,
        Meeting,
        DeepWork,
        BreakSprint,        // short break between deep-work sprints
        Sleep,              // overnight rest, after the evening routine
        AllDone,            // marker row: "Habits done"
        RoutineTransition   // marker row: "Evening/Morning routine starts"
    }

    /// One row in the day's timetable. Reference type (class) so the builder can flip
    /// IsActionable / IsCurrent on blocks already added to the list (mirrors the Swift struct
    /// mutated in-place via array index).
    public class ScheduleBlock
    {
        public ScheduleBlockKind Kind;
        public string Title;            // includes emoji for habits/meetings
        public DateTime Start;
        public DateTime End;
        public int? HabitIndex;         // index into the current routine's remaining-habits array
        public bool IsActionable;       // show Play/Done/Skip on this card
        public bool IsCurrent;          // the next habit up (highlighted)
        public string Subtitle;         // e.g. "meeting", or a duration label
        public bool DidOverflow;        // habit couldn't be placed before day end
        public string EventID;          // calendar event identifier (for meetings, so they can be dismissed)
        public string RoutineName;      // for habits: which routine they belong to (used to group them)
        // Follow-up fields (Mac-App #2010/#2012/#2049) — used by the past-routine / completed /
        // dismissed rendering added in later milestones.
        public bool IsDone;             // habit completed today — rendered struck through (earlier-today sections)
        public string HabitId;          // the habit's server id — used to complete/skip a PAST habit by id
        public bool IsEarlier;          // from an "earlier today" section (past routine); actioned by id, not index
        public bool IsDismissed;        // a meeting hidden with ✕, shown only when "Show dismissed events" is on
        public string Colour;           // per-meeting colour override (hex, e.g. "#FF9500"); null = default blue

        public ScheduleBlock(ScheduleBlockKind kind, string title, DateTime start, DateTime end,
                             int? habitIndex = null, bool isActionable = false, bool isCurrent = false,
                             string subtitle = null, bool didOverflow = false, string eventID = null,
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
            EventID = eventID;
            RoutineName = routineName;
        }

        public int DurationSeconds => (int)(End - Start).TotalSeconds;
    }

    public class ScheduleHabitInput
    {
        public int Index;               // position in the routine's remaining-habits array
        public string Title;            // emoji + name
        public int DurationSeconds;
        public bool IsPlaying;          // currently running
        public int ElapsedSeconds;      // already elapsed (for the playing habit)
        public DateTime? Cutoff;        // latest time it must be finished by; dropped if it can't be
        public bool IsDone;             // completed today — rendered struck through (earlier-today sections)
        public string Id;               // the habit's server id — needed to complete/skip a PAST habit by id

        public ScheduleHabitInput(int index, string title, int durationSeconds,
                                  bool isPlaying = false, int elapsedSeconds = 0, DateTime? cutoff = null,
                                  bool isDone = false, string id = null)
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
    }

    public class ScheduleMeetingInput
    {
        public string Id;
        public string Title;
        public DateTime Start;
        public DateTime End;
        public bool IsDismissed;   // hidden by the user; surfaced (struck through) when "Show dismissed events" is on
        public string Colour;      // per-meeting colour override (hex, e.g. "#FF9500"); null = default blue

        public ScheduleMeetingInput(string id, string title, DateTime start, DateTime end, bool isDismissed = false, string colour = null)
        {
            Id = id;
            Title = title;
            Start = start;
            End = end;
            IsDismissed = isDismissed;
            Colour = colour;
        }
    }

    /// A routine (morning / evening / a custom routine) and the time window its habits may run in.
    /// Habits are packed inside [WindowStart, WindowEnd] and never spill past it, so one routine
    /// doesn't bleed into the next. Only the active routine's habits are Actionable.
    public class ScheduleRoutineSegment
    {
        public string Name;
        public List<ScheduleHabitInput> Habits;
        public DateTime WindowStart;
        public DateTime WindowEnd;
        public bool Actionable;
        /// #2010: an "earlier today" section (the past morning routine, shown once we're past it).
        /// The builder lays these out at their historical window [WindowStart, WindowEnd] instead of
        /// clamping the start up to `now` (which would drop a wholly-past window), and it doesn't apply
        /// the per-habit cutoff drop — they're read-only history.
        public bool IsEarlier;

        public ScheduleRoutineSegment(string name, List<ScheduleHabitInput> habits,
                                      DateTime windowStart, DateTime windowEnd, bool actionable,
                                      bool isEarlier = false)
        {
            Name = name;
            Habits = habits;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            Actionable = actionable;
            IsEarlier = isEarlier;
        }
    }

    /// Display strings for the engine-generated blocks, passed in (localized) by the view so the
    /// engine stays free of any config/localization dependency. Defaults are English fallbacks;
    /// the real strings come from the config file at the UI layer (scheduleViewInfo block).
    public class ScheduleLabels
    {
        public string DeepWork = "🧠 Deep work";
        public string DeepWorkNoRoom = "🧠 Deep work (no room for next habit)";
        public string BreakSprint = "☕️ Break";
        public string Sleep = "😴 Sleep";
        public string EveningRoutine = "🌙 Evening routine";
        public string MorningRoutine = "🌅 Morning routine";
        public string Meeting = "meeting";
    }
}
