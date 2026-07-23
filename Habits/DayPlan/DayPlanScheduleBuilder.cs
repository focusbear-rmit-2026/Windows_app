using System;
using System.Collections.Generic;
using System.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Pure-logic engine for the Day Plan: turns the remaining habits of the current routine —
    /// plus (later) today's calendar meetings — into a timetable of blocks (habits, meetings,
    /// deep-work gaps, break sprints) with projected start/finish times.
    ///
    /// A faithful C# port of the macOS ScheduleBuilder (Mac-App: FocusBear/Schedule/ScheduleKit.swift).
    /// Kept free of WPF so the timing logic can be reasoned about and unit-tested in isolation
    /// (see DayPlanScheduleBuilderTests / RunSelfTest). Rendering lives in HabitsDayPlan.
    /// </summary>
    public static class DayPlanScheduleBuilder
    {
        /// Minimum free span that becomes its own "deep work" block.
        public const int MinDeepWorkSeconds = 10 * 60;

        /// <summary>
        /// Build the day's timetable.
        /// </summary>
        /// <param name="segments">Routines to schedule, each with the window its habits may run in.</param>
        /// <param name="meetings">Today's timed calendar events (any order). Empty until calendar lands.</param>
        /// <param name="now">Current time — habits are packed from here.</param>
        /// <param name="dayEnd">End of the scheduling window (typically the user's shutdown time / 23:59).</param>
        /// <param name="leaveBufferSeconds">Time reserved before each meeting so a habit doesn't run
        /// right up to the meeting start.</param>
        /// <param name="outOfOrderAllowed">Kept for parity with macOS; actionability is driven by the
        /// segment, so this is currently unused by the placement logic.</param>
        public static List<ScheduleBlock> Build(
            List<ScheduleRoutineSegment> segments,
            List<ScheduleMeetingInput> meetings,
            DateTime now,
            DateTime dayEnd,
            int leaveBufferSeconds,
            bool outOfOrderAllowed,
            DateTime? eveningRoutineStart = null,
            DateTime? morningRoutineStart = null,
            DateTime? meetingsFrom = null,
            DateTime? deepWorkCutoff = null,
            int sprintWorkSeconds = 0,
            int sprintBreakSeconds = 0,
            ScheduleLabels labels = null)
        {
            labels = labels ?? new ScheduleLabels();
            meetings = meetings ?? new List<ScheduleMeetingInput>();

            var blocks = new List<ScheduleBlock>();

            // Meetings are shown from `now` by default; pass an earlier `meetingsFrom` (e.g. the start
            // of the day) to also surface meetings that already finished — habits still pack from `now`.
            DateTime meetingFloor = meetingsFrom ?? now;
            var upcomingMeetings = meetings
                .Where(m => m.End > meetingFloor)
                .OrderBy(m => m.Start)
                .ToList();

            // Habits may not run inside [meetingStart - buffer, meetingEnd].
            var habitBusy = upcomingMeetings
                .Select(m => (Min(m.Start.AddSeconds(-leaveBufferSeconds), m.Start), m.End))
                .ToList();

            // Grows as we place habits, so deep-work avoids them.
            var occupied = new List<(DateTime, DateTime)>(habitBusy);

            // Schedule each routine's habits within its own window, capped at the window end, so the
            // morning routine never bleeds past the evening start and each routine shows in its block.
            foreach (var seg in segments.OrderBy(s => s.WindowStart))
            {
                // Active/preview routines pack from `now` (their remaining habits start here); an
                // "earlier today" section is history, so lay it out at its real window start instead of
                // clamping up to now (which would drop a window that already ended). (Mac-App #2010)
                DateTime segStart = seg.IsEarlier ? seg.WindowStart : Max(seg.WindowStart, now);
                if (!(seg.WindowEnd > segStart) || seg.Habits.Count == 0) continue;

                // Place habits strictly in list order (no reordering). Each starts at the running
                // cursor, pushed past any meeting it would overlap, and everything after it follows —
                // so a long first habit stays first instead of being leap-frogged.
                DateTime cursor = segStart;
                var placed = new List<int>();
                foreach (var h in seg.Habits)
                {
                    // A habit with a cutoff stays visible until that cutoff time has actually passed —
                    // not merely because its projected slot would land after it. (An "earlier today"
                    // section is past history: show every habit regardless of cutoff.) (Mac-App #2010)
                    if (!seg.IsEarlier && h.Cutoff.HasValue && now > h.Cutoff.Value) continue;
                    int remaining = Math.Max(0, h.DurationSeconds - (h.IsPlaying ? h.ElapsedSeconds : 0));
                    double dur = remaining;
                    DateTime? start = NextFreeStart(cursor, dur, habitBusy, seg.WindowEnd);
                    if (start == null)
                    {
                        // Can't fit before the window ends. Habits run in sequence, so everything after
                        // this one can't fit either — stop rather than leap-frogging to a shorter later habit.
                        break;
                    }
                    DateTime end = start.Value.AddSeconds(dur);
                    cursor = end;
                    var b = new ScheduleBlock(ScheduleBlockKind.Habit, h.Title, start.Value, end,
                                              habitIndex: h.Index, routineName: seg.Name)
                    {
                        IsActionable = seg.Actionable,
                        IsDone = h.IsDone,       // (Mac-App #2012)
                        HabitId = h.Id,
                        IsEarlier = seg.IsEarlier
                    };
                    blocks.Add(b);
                    placed.Add(blocks.Count - 1);
                    occupied.Add((start.Value, end));
                }

                // The whole active routine is tappable (tap a habit to move the action bar to it);
                // its next-up habit is the default selection. Preview routines aren't actionable.
                if (seg.Actionable && placed.Count > 0)
                {
                    int nextIdx = placed.OrderBy(i => blocks[i].Start).First();
                    foreach (int i in placed) blocks[i].IsActionable = true;
                    blocks[nextIdx].IsCurrent = true;
                }
                else
                {
                    foreach (int i in placed) blocks[i].IsActionable = false;
                }
            }

            foreach (var m in upcomingMeetings)
            {
                blocks.Add(new ScheduleBlock(ScheduleBlockKind.Meeting, m.Title, m.Start, m.End,
                                             subtitle: labels.Meeting, eventID: m.Id)
                {
                    IsDismissed = m.IsDismissed,   // (Mac-App #2049)
                    Colour = m.Colour
                });
            }

            // Marker for the transition into the evening routine (the user's shutdown time).
            if (eveningRoutineStart.HasValue && eveningRoutineStart.Value > now)
            {
                blocks.Add(new ScheduleBlock(ScheduleBlockKind.RoutineTransition, labels.EveningRoutine,
                                             eveningRoutineStart.Value, eveningRoutineStart.Value));
            }

            // Deep-work / free time fills gaps up to the day's end — but never past the user's cutoff:
            // we don't suggest deep work once they've called it a day.
            DateTime deepWorkEnd = dayEnd;
            if (deepWorkCutoff.HasValue && deepWorkCutoff.Value < deepWorkEnd) deepWorkEnd = deepWorkCutoff.Value;
            var habitStarts = blocks.Where(b => b.Kind == ScheduleBlockKind.Habit).Select(b => b.Start).ToList();
            foreach (var iv in FreeIntervals(now, deepWorkEnd, occupied))
            {
                if ((iv.Item2 - iv.Item1).TotalSeconds >= MinDeepWorkSeconds)
                {
                    // A gap is "no room for the next habit" only when it ends because a meeting begins
                    // *and* a habit was pushed to start right after that meeting ends.
                    var closingMeeting = upcomingMeetings.FirstOrDefault(m =>
                        m.Start >= iv.Item2.AddSeconds(-1) &&
                        m.Start <= iv.Item2.AddSeconds(leaveBufferSeconds + 60));
                    bool pushedHabitAfter = false;
                    if (closingMeeting != null)
                    {
                        pushedHabitAfter = habitStarts.Any(s =>
                            s >= closingMeeting.End.AddSeconds(-60) &&
                            s <= closingMeeting.End.AddSeconds(MinDeepWorkSeconds));
                    }
                    string title = pushedHabitAfter ? labels.DeepWorkNoRoom : labels.DeepWork;
                    blocks.AddRange(DeepWorkSprints(iv.Item1, iv.Item2, title,
                                                    sprintWorkSeconds, sprintBreakSeconds, labels));
                }
            }

            // The day ends at dayEnd. No overnight "Sleep" block or next-day "Morning routine" marker —
            // the "View tomorrow's schedule" button (UI) handles the next day instead.

            return blocks
                .OrderBy(b => b.Start)
                .ThenBy(b => KindOrder(b.Kind))
                .ToList();
        }

        // MARK: - Helpers

        /// <summary>
        /// Earliest start >= `from` where [start, start+duration] fits before `windowEnd` without
        /// overlapping any busy interval — it pushes past a conflicting meeting rather than
        /// reordering habits around it. Returns null if it can't fit before the window ends.
        /// </summary>
        public static DateTime? NextFreeStart(DateTime from, double duration,
                                              List<(DateTime, DateTime)> busy, DateTime windowEnd)
        {
            var sorted = busy.OrderBy(b => b.Item1).ToList();
            DateTime start = from;
            bool advanced = true;
            while (advanced)
            {
                advanced = false;
                if (start.AddSeconds(duration) > windowEnd.AddSeconds(0.5)) return null;
                foreach (var b in sorted)
                {
                    if (start < b.Item2 && start.AddSeconds(duration) > b.Item1)
                    {
                        start = b.Item2;   // push to the end of the conflicting meeting
                        advanced = true;
                        break;
                    }
                }
            }
            return start;
        }

        /// <summary>
        /// Splits a deep-work interval into work/break sprints based on the user's break cadence.
        /// `workSeconds` is the break frequency (how long you work before a break). With no cadence
        /// set (workSeconds &lt;= 0) the interval stays one big deep-work block. A trailing remainder
        /// too small for a break is absorbed into the final work sprint.
        /// </summary>
        private static List<ScheduleBlock> DeepWorkSprints(DateTime start, DateTime end, string workTitle,
                                                           int workSeconds, int breakSeconds, ScheduleLabels labels)
        {
            var outList = new List<ScheduleBlock>();
            if (!(workSeconds > 0 && breakSeconds > 0))
            {
                outList.Add(new ScheduleBlock(ScheduleBlockKind.DeepWork, workTitle, start, end));
                return outList;
            }
            DateTime t = start;
            while (t < end)
            {
                DateTime workEnd = Min(t.AddSeconds(workSeconds), end);
                // If what's left after a full work sprint is too small for a break, fold it in.
                if ((end - workEnd).TotalSeconds <= breakSeconds) workEnd = end;
                outList.Add(new ScheduleBlock(ScheduleBlockKind.DeepWork, workTitle, t, workEnd));
                t = workEnd;
                if (t >= end) break;
                DateTime breakEnd = Min(t.AddSeconds(breakSeconds), end);
                outList.Add(new ScheduleBlock(ScheduleBlockKind.BreakSprint, labels.BreakSprint, t, breakEnd));
                t = breakEnd;
            }
            return outList;
        }

        /// Tie-break ordering when two blocks share a start time.
        private static int KindOrder(ScheduleBlockKind k)
        {
            switch (k)
            {
                case ScheduleBlockKind.RoutineTransition: return 0;  // a transition marker precedes its habits
                case ScheduleBlockKind.Habit: return 1;
                case ScheduleBlockKind.AllDone: return 2;
                case ScheduleBlockKind.Meeting: return 3;
                case ScheduleBlockKind.DeepWork: return 4;
                case ScheduleBlockKind.BreakSprint: return 4;
                case ScheduleBlockKind.Sleep: return 5;
                default: return 6;
            }
        }

        /// Free intervals within [from, to] after removing the (possibly overlapping) busy intervals.
        public static List<(DateTime, DateTime)> FreeIntervals(DateTime from, DateTime to,
                                                               List<(DateTime, DateTime)> busy)
        {
            var result = new List<(DateTime, DateTime)>();
            if (!(to > from)) return result;
            var clipped = busy
                .Select(b => (Max(b.Item1, from), Min(b.Item2, to)))
                .Where(b => b.Item2 > b.Item1)
                .ToList();
            var merged = MergeIntervals(clipped);
            DateTime cursor = from;
            foreach (var b in merged)
            {
                if (b.Item1 > cursor) result.Add((cursor, b.Item1));
                cursor = Max(cursor, b.Item2);
            }
            if (cursor < to) result.Add((cursor, to));
            return result;
        }

        private static List<(DateTime, DateTime)> MergeIntervals(List<(DateTime, DateTime)> intervals)
        {
            var sorted = intervals.OrderBy(i => i.Item1).ToList();
            var merged = new List<(DateTime, DateTime)>();
            foreach (var iv in sorted)
            {
                if (merged.Count > 0 && iv.Item1 <= merged[merged.Count - 1].Item2)
                {
                    var last = merged[merged.Count - 1];
                    last.Item2 = Max(last.Item2, iv.Item2);
                    merged[merged.Count - 1] = last;
                }
                else
                {
                    merged.Add(iv);
                }
            }
            return merged;
        }

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
