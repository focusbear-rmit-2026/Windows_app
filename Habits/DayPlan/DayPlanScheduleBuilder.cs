using System;
using System.Collections.Generic;
using System.Linq;
using Core = FocusBear.Core.Schedule;

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

                // An "earlier today" section is read-only history: place EVERY habit, never truncated to
                // what fits in its window (the normal overflow path would hide the still-undone ones).
                bool uncapped = seg.IsEarlier;

                // A habit with a cutoff stays visible until that cutoff time has actually passed — not
                // merely because its projected slot would land after it. (An "earlier today" section is
                // past history: show every habit regardless of cutoff.) (Mac-App #2010)
                var dueHabits = seg.Habits
                    .Where(h => seg.IsEarlier || !h.Cutoff.HasValue || now <= h.Cutoff.Value)
                    .ToList();

                // Same placement as the Mac (ScheduleKit.placeHabitsInGaps, ported to FocusBear.Core):
                // the habits fill the free gaps between meetings, gap by gap. Within a gap the earliest
                // habit in routine order that still fits goes next, so a short later habit is pulled into
                // a gap the next one is too long for, and the long one takes the first later gap that
                // holds it. The morning routine is therefore spread through the day's gaps instead of
                // being declared "not enough free time" (report 2026-09-25_09-01-38).
                var placement = PlaceHabitsInGaps(dueHabits, segStart, uncapped ? (DateTime?)null : seg.WindowEnd, habitBusy);

                var placed = new List<int>();
                foreach (var p in placement.Placed)
                {
                    var h = p.Item1;
                    var b = new ScheduleBlock(ScheduleBlockKind.Habit, h.Title, p.Item2, p.Item3,
                                              habitIndex: h.Index, routineName: seg.Name)
                    {
                        IsActionable = seg.Actionable,
                        IsDone = h.IsDone,       // (Mac-App #2012)
                        HabitId = h.Id,
                        IsEarlier = seg.IsEarlier
                    };
                    blocks.Add(b);
                    placed.Add(blocks.Count - 1);
                    occupied.Add((p.Item2, p.Item3));
                }

                // What no gap in the whole window can hold is still listed — never dropped — as its own
                // "not enough time today" group, anchored at the end of the last habit that fit (segStart
                // when nothing fit) so it sorts below the habits that did get a slot. Zero-length (it
                // isn't busy time); the habit's own length is kept for display. For the ACTIVE routine
                // this also keeps it startable on a fully-booked day (#1371).
                DateTime overflowAnchor = placement.Placed.Count > 0 ? placement.Placed.Max(p => p.Item3) : segStart;
                foreach (var h in placement.Unplaced)
                {
                    var b = new ScheduleBlock(ScheduleBlockKind.Habit, h.Title, overflowAnchor, overflowAnchor,
                                              habitIndex: h.Index, routineName: seg.Name)
                    {
                        IsActionable = seg.Actionable,
                        IsDone = h.IsDone,
                        HabitId = h.Id,
                        IsEarlier = seg.IsEarlier,
                        DidOverflow = true,
                        OverflowDurationSeconds = RemainingSeconds(h)
                    };
                    blocks.Add(b);
                    placed.Add(blocks.Count - 1);
                }

                // The whole active routine is tappable (tap a habit to move the action bar to it); its
                // next-up habit is the default selection — the earliest TIMED habit, or (if the whole
                // routine overflowed) the first overflow habit. Preview routines aren't actionable.
                if (seg.Actionable && placed.Count > 0)
                {
                    var timed = placed.Where(i => !blocks[i].DidOverflow).ToList();
                    int nextIdx = (timed.Count > 0 ? timed : placed).OrderBy(i => blocks[i].Start).First();
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

            // Same start & kind: order habits by routine index so the overflow group (all anchored at one
            // time) stays contiguous and in routine order.
            return blocks
                .OrderBy(b => b.Start)
                .ThenBy(b => KindOrder(b.Kind))
                .ThenBy(b => b.HabitIndex ?? int.MaxValue)
                .ToList();
        }

        // MARK: - Helpers

        /// <summary>Seconds a habit still needs: its full length, less what has elapsed if it's playing.</summary>
        public static int RemainingSeconds(ScheduleHabitInput h)
        {
            return Math.Max(0, h.DurationSeconds - (h.IsPlaying ? h.ElapsedSeconds : 0));
        }

        /// <summary>The outcome of packing habits into the gaps: (habit, start, end) per placed habit, plus
        /// the habits no gap was long enough for.</summary>
        public class HabitPlacement
        {
            public List<Tuple<ScheduleHabitInput, DateTime, DateTime>> Placed = new List<Tuple<ScheduleHabitInput, DateTime, DateTime>>();
            public List<ScheduleHabitInput> Unplaced = new List<ScheduleHabitInput>();
        }

        /// <summary>
        /// Packs <paramref name="habits"/> into the free gaps of [from, to] around <paramref name="busy"/>.
        /// Delegates to <see cref="FocusBear.Core.Schedule.ScheduleBuilder.PlaceHabitsInGaps"/> — the
        /// unit-tested port of the Mac's placeHabitsInGaps — so the two platforms place habits by one rule.
        /// A null <paramref name="to"/> means "no window cap" (an earlier-today history section).
        /// </summary>
        public static HabitPlacement PlaceHabitsInGaps(List<ScheduleHabitInput> habits, DateTime from, DateTime? to,
                                                       List<(DateTime, DateTime)> busy)
        {
            var byCore = new Dictionary<Core.ScheduleHabitInput, ScheduleHabitInput>();
            var coreHabits = new List<Core.ScheduleHabitInput>();
            foreach (var h in habits)
            {
                // Remaining time is folded into the duration so Core doesn't need the playing state.
                var c = new Core.ScheduleHabitInput(h.Index, h.Title, RemainingSeconds(h), id: h.Id);
                byCore[c] = h;
                coreHabits.Add(c);
            }
            var coreBusy = busy.Select(b => new Core.TimeRange(Offset(b.Item1), Offset(b.Item2))).ToList();
            DateTimeOffset end = to.HasValue ? Offset(to.Value) : Core.ScheduleBuilder.DistantFuture;
            var result = Core.ScheduleBuilder.PlaceHabitsInGaps(coreHabits, Offset(from), end, coreBusy);

            var outcome = new HabitPlacement();
            foreach (var p in result.Placed)
                outcome.Placed.Add(Tuple.Create(byCore[p.Habit], p.Start.LocalDateTime, p.End.LocalDateTime));
            foreach (var u in result.Unplaced)
                outcome.Unplaced.Add(byCore[u]);
            return outcome;
        }

        private static DateTimeOffset Offset(DateTime local)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local));
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
