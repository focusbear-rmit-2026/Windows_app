using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>ScheduleKit.swift</c> (<c>ScheduleBuilder</c>).
    ///
    /// Turns the remaining habits of the current routine — plus the day's calendar meetings — into a
    /// timetable of blocks (habits, meetings, deep-work sprints) with projected start/finish times.
    ///
    /// Two parameters of the Swift signature are deliberately NOT ported: <c>outOfOrderAllowed</c> and
    /// <c>morningRoutineStart</c>. Neither is read anywhere in the Swift body — the overnight "Sleep"
    /// block and next-day morning marker were removed when the UI grew a "View tomorrow's schedule"
    /// button — so carrying them over would be importing dead parameters.
    /// </summary>
    public static class ScheduleBuilder
    {
        /// <summary>Minimum free span that becomes its own "deep work" block.</summary>
        public const int MinDeepWorkSeconds = 10 * 60;

        /// <summary>
        /// Slack allowed past a gap's end so a habit that finishes a fraction of a second late (because
        /// <c>now</c> carries sub-second time while window and meeting bounds are whole minutes) still
        /// counts as fitting.
        /// </summary>
        private const double FitToleranceSeconds = 0.5;

        /// <summary>
        /// The Swift engine places an "earlier today" section against <c>Date.distantFuture</c>. Swift's
        /// distant future is year 4001; <see cref="DateTimeOffset.MaxValue"/> is NOT usable as the
        /// equivalent because adding the fit tolerance to it throws.
        /// </summary>
        public static readonly DateTimeOffset DistantFuture =
            new DateTimeOffset(4001, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Display strings for the engine-generated blocks, passed in (localized) by the caller so the
        /// engine stays free of any config/localization dependency.
        /// </summary>
        public sealed class Labels
        {
            public static readonly Labels Default = new Labels();

            public Labels(string deepWork = "🧠 Deep work", string breakSprint = "☕️ Break",
                          string eveningRoutine = "🌙 Evening routine")
            {
                DeepWork = deepWork;
                BreakSprint = breakSprint;
                EveningRoutine = eveningRoutine;
            }

            public string DeepWork { get; }
            public string BreakSprint { get; }
            /// <summary>Title of the "evening routine starts" marker row.</summary>
            public string EveningRoutine { get; }
        }

        /// <summary>A habit the gap filler found a concrete slot for.</summary>
        public sealed class PlacedHabit
        {
            public PlacedHabit(ScheduleHabitInput habit, DateTimeOffset start, DateTimeOffset end)
            {
                Habit = habit;
                Start = start;
                End = end;
            }

            public ScheduleHabitInput Habit { get; }
            public DateTimeOffset Start { get; }
            public DateTimeOffset End { get; }
        }

        /// <summary>The outcome of packing a routine's habits into its window.</summary>
        public sealed class HabitPlacement
        {
            public HabitPlacement(IReadOnlyList<PlacedHabit> placed, IReadOnlyList<ScheduleHabitInput> unplaced)
            {
                Placed = placed;
                Unplaced = unplaced;
            }

            public IReadOnlyList<PlacedHabit> Placed { get; }
            /// <summary>Habits no gap in the whole window was long enough for.</summary>
            public IReadOnlyList<ScheduleHabitInput> Unplaced { get; }
        }

        /// <summary>Build the day's timetable.</summary>
        /// <param name="segments">Each routine and the window its habits may run in.</param>
        /// <param name="meetings">The day's timed calendar events, any order.</param>
        /// <param name="now">Current time — habits are packed from here.</param>
        /// <param name="dayEnd">End of the scheduling window (typically 23:59).</param>
        /// <param name="leaveBufferSeconds">Time reserved before each meeting so a habit does not run
        /// right up to the meeting start.</param>
        /// <param name="eveningRoutineStart">Where to draw the "evening routine starts" marker.</param>
        /// <param name="meetingsFrom">Meetings are shown from <paramref name="now"/> by default; pass an
        /// earlier value (e.g. the start of the day) to also surface meetings that already finished.</param>
        /// <param name="deepWorkCutoff">No deep-work filler is suggested after this time.</param>
        /// <param name="sprintWorkSeconds">How long the user works before a break (the break cadence).</param>
        /// <param name="sprintBreakSeconds">How long that break lasts.</param>
        public static IReadOnlyList<ScheduleBlock> Build(
            IEnumerable<ScheduleRoutineSegment> segments,
            IEnumerable<ScheduleMeetingInput> meetings,
            DateTimeOffset now,
            DateTimeOffset dayEnd,
            int leaveBufferSeconds,
            DateTimeOffset? eveningRoutineStart = null,
            DateTimeOffset? meetingsFrom = null,
            DateTimeOffset? deepWorkCutoff = null,
            int sprintWorkSeconds = 0,
            int sprintBreakSeconds = 0,
            Labels labels = null)
        {
            labels = labels ?? Labels.Default;
            List<ScheduleBlock> blocks = new List<ScheduleBlock>();

            DateTimeOffset meetingFloor = meetingsFrom ?? now;
            List<ScheduleMeetingInput> upcomingMeetings = (meetings ?? Enumerable.Empty<ScheduleMeetingInput>())
                .Where(m => m.End > meetingFloor)
                .OrderBy(m => m.Start)
                .ToList();

            // Habits may not run inside [meetingStart - buffer, meetingEnd]. The Min guards a negative
            // buffer, which would otherwise move the busy span forwards instead of backwards.
            List<TimeRange> habitBusy = upcomingMeetings
                .Select(m => new TimeRange(Min(m.Start.AddSeconds(-leaveBufferSeconds), m.Start), m.End))
                .ToList();

            // Grows as habits are placed, so deep-work avoids them.
            List<TimeRange> occupied = new List<TimeRange>(habitBusy);

            foreach (ScheduleRoutineSegment seg in (segments ?? Enumerable.Empty<ScheduleRoutineSegment>())
                     .OrderBy(s => s.WindowStart))
            {
                // Active/preview routines pack from `now` (their remaining habits start here); an
                // "earlier today" section is history, so lay it out at its real window start instead of
                // clamping up to now (which would drop a window that already ended).
                DateTimeOffset segStart = seg.IsEarlier ? seg.WindowStart : Max(seg.WindowStart, now);
                if (seg.WindowEnd <= segStart || seg.Habits.Count == 0) continue;

                // An "earlier today"/past routine is read-only history: render EVERY habit, never
                // truncated to what fit in its window. That window (e.g. a ~3h morning block) is far
                // shorter than the routine's full duration, so the normal overflow path would DROP the
                // later habits — typically the still-undone ones — leaving only the early, already
                // completed habits visible, and the card would report itself "Completed" when the
                // routine is only partly done.
                DateTimeOffset placementWindowEnd = seg.IsEarlier ? DistantFuture : seg.WindowEnd;

                // Habits that still count today: a habit with a cutoff stays visible until that cutoff
                // has actually passed — not merely because its *projected* slot would land after it. The
                // user can still do it (possibly out of order) right up to the cutoff, so don't hide it
                // early. An "earlier today" section is past history: show every habit regardless.
                List<ScheduleHabitInput> dueHabits = seg.Habits
                    .Where(h => seg.IsEarlier || !h.Cutoff.HasValue || now <= h.Cutoff.Value)
                    .ToList();

                HabitPlacement placement = PlaceHabitsInGaps(dueHabits, segStart, placementWindowEnd, habitBusy);
                List<int> placedIndexes = new List<int>();
                foreach (PlacedHabit p in placement.Placed)
                {
                    ScheduleBlock b = new ScheduleBlock(ScheduleBlockKind.Habit, p.Habit.Title, p.Start, p.End,
                                                        habitIndex: p.Habit.Index, routineName: seg.Name);
                    b.IsActionable = seg.Actionable;
                    // Every habit row is editable, "earlier today" history and completed habits included:
                    // the editor changes the habit's SETTINGS, not the day's record. Only what you can
                    // *do* to today's row (Play/Done/Skip) is gated on Actionable.
                    b.IsEditable = true;
                    b.IsDone = p.Habit.IsDone;
                    b.HabitId = p.Habit.Id;
                    b.IsEarlier = seg.IsEarlier;
                    blocks.Add(b);
                    placedIndexes.Add(blocks.Count - 1);
                    occupied.Add(new TimeRange(p.Start, p.End));
                }

                // Where the overflow group is anchored: the end of the last habit that fit (segStart when
                // nothing fit), so it sorts BELOW every placed habit of this routine — not at the top of
                // a group that did have time ("habits I don't have time for appear at the top of an
                // accordion where I do"). Every overflow habit shares that one anchor deliberately: a
                // deep-work filler occupying the freed time also starts there, and the final sort's kind
                // tie-break (habit before deepWork) plus the habitIndex tie-break keep the whole overflow
                // group contiguous and in routine order. A previous per-item offset gave them distinct
                // starts, so a deep-work block sorted BETWEEN two anchors and split them into two "not
                // enough time today" cards.
                DateTimeOffset overflowAnchor = placement.Placed.Count > 0
                    ? placement.Placed.Max(p => p.End)
                    : segStart;

                // This runs for EVERY segment, not just the actionable one. A preview segment (the
                // evening routine, a custom routine, or any routine on a past/future day) used to drop
                // its unplaced habits silently, so a packed future day rendered "🧩 Morning routine
                // habits · 6:30–6:57am" as if the whole routine fitted in a 27-minute gap.
                foreach (ScheduleHabitInput h in placement.Unplaced)
                {
                    ScheduleBlock b = new ScheduleBlock(ScheduleBlockKind.Habit, h.Title, overflowAnchor,
                                                        overflowAnchor, habitIndex: h.Index, routineName: seg.Name);
                    b.IsActionable = seg.Actionable;
                    b.IsEditable = true;
                    b.IsDone = h.IsDone;
                    b.HabitId = h.Id;
                    b.DidOverflow = true;
                    // Keep the block zero-duration (start == end) so it groups and doesn't count as busy
                    // time, but remember the habit's own length so the UI can show its real length.
                    b.OverflowDurationSeconds = RemainingSeconds(h);
                    blocks.Add(b);
                    placedIndexes.Add(blocks.Count - 1);
                }

                // The whole active routine is tappable; its next-up habit is the default selection — the
                // earliest TIMED habit, or (if the whole routine overflowed) the first overflow habit.
                if (seg.Actionable)
                {
                    foreach (int i in placedIndexes) blocks[i].IsActionable = true;
                    int? nextIdx = FirstMinimumBy(placedIndexes.Where(i => !blocks[i].DidOverflow), i => blocks[i].Start)
                                   ?? FirstMinimumBy(placedIndexes, i => blocks[i].Start);
                    if (nextIdx.HasValue) blocks[nextIdx.Value].IsCurrent = true;
                }
                else
                {
                    foreach (int i in placedIndexes) blocks[i].IsActionable = false;
                }
            }

            foreach (ScheduleMeetingInput m in upcomingMeetings)
            {
                ScheduleBlock meetingBlock = new ScheduleBlock(ScheduleBlockKind.Meeting, m.Title, m.Start, m.End,
                                                               eventId: m.Id);
                meetingBlock.IsDismissed = m.IsDismissed;
                blocks.Add(meetingBlock);
            }

            // Marker for the transition into the evening routine (the user's shutdown time).
            if (eveningRoutineStart.HasValue && eveningRoutineStart.Value > now)
            {
                blocks.Add(new ScheduleBlock(ScheduleBlockKind.RoutineTransition, labels.EveningRoutine,
                                             eveningRoutineStart.Value, eveningRoutineStart.Value));
            }

            // Deep-work / free time fills gaps up to the day's end — but never past the user's cutoff
            // time: we don't suggest deep work once they've called it a day. The timetable still runs to
            // the day end (meetings can sit later); there is just no deep-work filler after cutoff.
            DateTimeOffset deepWorkEnd = deepWorkCutoff.HasValue ? Min(deepWorkCutoff.Value, dayEnd) : dayEnd;
            foreach (TimeRange iv in FreeIntervals(now, deepWorkEnd, occupied))
            {
                if (iv.TotalSeconds >= MinDeepWorkSeconds)
                {
                    blocks.AddRange(DeepWorkSprints(iv.Start, iv.End, labels.DeepWork, sprintWorkSeconds,
                                                    sprintBreakSeconds, labels));
                }
            }

            // Same start & kind: order habits by their routine index so the overflow "not enough time
            // today" habits (all anchored at the same cursor) stay contiguous and in routine order — a
            // deep-work block at the same start still sorts after them (habit KindOrder < deepWork).
            return blocks
                .OrderBy(b => b.Start)
                .ThenBy(b => KindOrder(b.Kind))
                .ThenBy(b => b.HabitIndex ?? int.MaxValue)
                .ToList();
        }

        /// <summary>
        /// Packs <paramref name="habits"/> into the free gaps of [from, to] (everything not covered by
        /// <paramref name="busy"/>), gap by gap in time order. Within a gap, the earliest habit in
        /// routine order that still fits is placed next — so when the next habit in order can't finish
        /// before a meeting, a shorter later habit is pulled forward into that gap instead of leaving it
        /// to deep work, and the long habit takes the first later gap that fits it. Only what is needed
        /// to fill a gap is pulled forward, so the list order stays as stable as possible.
        ///
        /// "Fits nowhere" is judged against the whole window: a habit is unplaced only when no gap
        /// between <paramref name="from"/> and <paramref name="to"/> is long enough for it.
        /// </summary>
        public static HabitPlacement PlaceHabitsInGaps(IEnumerable<ScheduleHabitInput> habits,
                                                       DateTimeOffset from, DateTimeOffset to,
                                                       IReadOnlyList<TimeRange> busy)
        {
            List<ScheduleHabitInput> remaining = habits == null
                ? new List<ScheduleHabitInput>()
                : habits.ToList();
            List<PlacedHabit> placed = new List<PlacedHabit>();

            foreach (TimeRange gap in FreeIntervals(from, to, busy))
            {
                DateTimeOffset cursor = gap.Start;
                DateTimeOffset gapEnd = gap.End.AddSeconds(FitToleranceSeconds);
                while (true)
                {
                    int i = remaining.FindIndex(h => cursor.AddSeconds(RemainingSeconds(h)) <= gapEnd);
                    if (i < 0) break;
                    ScheduleHabitInput habit = remaining[i];
                    remaining.RemoveAt(i);
                    DateTimeOffset end = cursor.AddSeconds(RemainingSeconds(habit));
                    placed.Add(new PlacedHabit(habit, cursor, end));
                    cursor = end;
                }
                if (remaining.Count == 0) break;
            }

            return new HabitPlacement(placed, remaining);
        }

        /// <summary>Seconds a habit still needs: its full duration, less what has already elapsed if it
        /// is playing.</summary>
        public static int RemainingSeconds(ScheduleHabitInput h)
        {
            return Math.Max(0, h.DurationSeconds - (h.IsPlaying ? h.ElapsedSeconds : 0));
        }

        /// <summary>
        /// Splits a deep-work interval into work/break sprints based on the user's break cadence.
        /// <paramref name="workSeconds"/> is the break frequency (how long you work before a break). With
        /// no cadence set the interval stays one big deep-work block. A trailing remainder too small for
        /// a break is absorbed into the final work sprint, so we never end on a stray break.
        /// </summary>
        private static IEnumerable<ScheduleBlock> DeepWorkSprints(DateTimeOffset start, DateTimeOffset end,
                                                                  string workTitle, int workSeconds,
                                                                  int breakSeconds, Labels labels)
        {
            if (workSeconds <= 0 || breakSeconds <= 0)
            {
                return new[] { new ScheduleBlock(ScheduleBlockKind.DeepWork, workTitle, start, end) };
            }

            List<ScheduleBlock> outBlocks = new List<ScheduleBlock>();
            DateTimeOffset t = start;
            while (t < end)
            {
                DateTimeOffset workEnd = Min(t.AddSeconds(workSeconds), end);
                // If what is left after a full work sprint is too small for a break, fold it in.
                if ((end - workEnd).TotalSeconds <= breakSeconds) workEnd = end;
                outBlocks.Add(new ScheduleBlock(ScheduleBlockKind.DeepWork, workTitle, t, workEnd));
                t = workEnd;
                if (t >= end) break;
                DateTimeOffset breakEnd = Min(t.AddSeconds(breakSeconds), end);
                outBlocks.Add(new ScheduleBlock(ScheduleBlockKind.BreakSprint, labels.BreakSprint, t, breakEnd));
                t = breakEnd;
            }
            return outBlocks;
        }

        /// <summary>Tie-break ordering when two blocks share a start time.</summary>
        private static int KindOrder(ScheduleBlockKind k)
        {
            switch (k)
            {
                case ScheduleBlockKind.RoutineTransition: return 0;  // precedes that routine's habits
                case ScheduleBlockKind.Habit: return 1;
                case ScheduleBlockKind.AllDone: return 2;
                case ScheduleBlockKind.Meeting: return 3;
                case ScheduleBlockKind.DeepWork: return 4;
                case ScheduleBlockKind.BreakSprint: return 4;
                case ScheduleBlockKind.Sleep: return 5;
                default: return 5;
            }
        }

        /// <summary>Free intervals within [from, to] after removing the (possibly overlapping) busy
        /// intervals.</summary>
        public static IReadOnlyList<TimeRange> FreeIntervals(DateTimeOffset from, DateTimeOffset to,
                                                             IReadOnlyList<TimeRange> busy)
        {
            List<TimeRange> result = new List<TimeRange>();
            if (to <= from) return result;

            List<TimeRange> clamped = (busy ?? new List<TimeRange>())
                .Select(b => new TimeRange(Max(b.Start, from), Min(b.End, to)))
                .Where(b => b.End > b.Start)
                .ToList();

            DateTimeOffset cursor = from;
            foreach (TimeRange b in MergeIntervals(clamped))
            {
                if (b.Start > cursor) result.Add(new TimeRange(cursor, b.Start));
                cursor = Max(cursor, b.End);
            }
            if (cursor < to) result.Add(new TimeRange(cursor, to));
            return result;
        }

        private static IReadOnlyList<TimeRange> MergeIntervals(IEnumerable<TimeRange> intervals)
        {
            List<TimeRange> merged = new List<TimeRange>();
            foreach (TimeRange iv in intervals.OrderBy(i => i.Start))
            {
                if (merged.Count > 0 && iv.Start <= merged[merged.Count - 1].End)
                {
                    TimeRange last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new TimeRange(last.Start, Max(last.End, iv.End));
                }
                else
                {
                    merged.Add(iv);
                }
            }
            return merged;
        }

        /// <summary>
        /// The first element with the smallest key, mirroring Swift's <c>min(by:)</c> — which keeps the
        /// FIRST of several equal minima. <c>OrderBy(...).First()</c> would too, but this says so.
        /// </summary>
        private static T? FirstMinimumBy<T, TKey>(IEnumerable<T> items, Func<T, TKey> key)
            where T : struct
            where TKey : IComparable<TKey>
        {
            T? best = null;
            TKey bestKey = default(TKey);
            foreach (T item in items)
            {
                TKey k = key(item);
                if (!best.HasValue || k.CompareTo(bestKey) < 0)
                {
                    best = item;
                    bestKey = k;
                }
            }
            return best;
        }

        private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b)
        {
            return a <= b ? a : b;
        }

        private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b)
        {
            return a >= b ? a : b;
        }
    }
}
