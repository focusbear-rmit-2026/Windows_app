using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The Day Plan's meetings cache, ported from the Mac's <c>MeetingsCache</c>. Free of any calendar
    /// API so its staleness policy is testable without a live event store; the platform's calendar code
    /// owns the actual query and calls into this.
    ///
    /// The policy exists because reading the system calendar blocks for seconds: an entry that has gone
    /// out of date is still SERVED while a background refresh replaces it, so a caller on the UI thread
    /// is never made to wait.
    /// </summary>
    public sealed class MeetingsCache
    {
        /// <summary>One cached day. <see cref="DayStart"/> is midnight, so two times on the same day
        /// share an entry.</summary>
        public struct Key : IEquatable<Key>
        {
            public Key(DateTimeOffset dayStart, bool includeDismissed = false)
            {
                DayStart = dayStart;
                IncludeDismissed = includeDismissed;
            }

            public DateTimeOffset DayStart { get; }
            public bool IncludeDismissed { get; }

            public bool Equals(Key other)
            {
                return DayStart == other.DayStart && IncludeDismissed == other.IncludeDismissed;
            }

            public override bool Equals(object obj)
            {
                return obj is Key && Equals((Key)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return (DayStart.GetHashCode() * 397) ^ IncludeDismissed.GetHashCode(); }
            }

            public override string ToString()
            {
                return DayPlanDayKey.For(DayStart) + (IncludeDismissed ? "+dismissed" : string.Empty);
            }
        }

        /// <summary>What a caller should do: use <see cref="Meetings"/> now, and start a background
        /// refresh if <see cref="NeedsRefresh"/>.</summary>
        public sealed class Lookup
        {
            public Lookup(IReadOnlyList<ScheduleMeetingInput> meetings, bool needsRefresh)
            {
                Meetings = meetings;
                NeedsRefresh = needsRefresh;
            }

            public IReadOnlyList<ScheduleMeetingInput> Meetings { get; }
            public bool NeedsRefresh { get; }
        }

        private readonly object _gate = new object();
        private readonly Dictionary<Key, IReadOnlyList<ScheduleMeetingInput>> _values =
            new Dictionary<Key, IReadOnlyList<ScheduleMeetingInput>>();
        private readonly HashSet<Key> _staleKeys = new HashSet<Key>();
        private readonly HashSet<Key> _refreshingKeys = new HashSet<Key>();

        /// <summary>
        /// The value to serve for <paramref name="key"/>. A key that has never been fetched serves an
        /// empty list — the same thing an unauthorized store returns — and asks for a refresh.
        /// </summary>
        public Lookup Get(Key key)
        {
            lock (_gate)
            {
                IReadOnlyList<ScheduleMeetingInput> cached;
                if (!_values.TryGetValue(key, out cached))
                {
                    return new Lookup(new List<ScheduleMeetingInput>(), true);
                }
                return new Lookup(cached, _staleKeys.Contains(key));
            }
        }

        /// <summary>
        /// Whether <paramref name="key"/> has ever been fetched — i.e. whether the empty list a cold
        /// <see cref="Get"/> serves means "no meetings" or merely "not read yet". <see cref="Get"/>
        /// deliberately conflates the two so the UI thread always has something to render; a caller that
        /// must not DECIDE on an unread day (the date picker's capacity badge) asks this first. A
        /// stale-but-present entry counts as known: it holds a real answer, just one refresh old.
        /// </summary>
        public bool HasValue(Key key)
        {
            lock (_gate) { return _values.ContainsKey(key); }
        }

        /// <summary>
        /// Records a fetched value and clears its stale mark. Returns true when the value differs from
        /// what lookups were already being served, i.e. when the UI must be told to re-render.
        /// </summary>
        public bool Store(IReadOnlyList<ScheduleMeetingInput> meetings, Key key)
        {
            lock (_gate)
            {
                IReadOnlyList<ScheduleMeetingInput> existing;
                bool changed = !_values.TryGetValue(key, out existing) || !existing.SequenceEqual(meetings);
                _values[key] = meetings;
                _staleKeys.Remove(key);
                return changed;
            }
        }

        /// <summary>
        /// Claims the refresh for <paramref name="key"/>. False when one is already in flight, so
        /// repeated ticks (and the several Day Plan hosts that each reload) collapse into one query.
        /// </summary>
        public bool BeginRefresh(Key key)
        {
            lock (_gate) { return _refreshingKeys.Add(key); }
        }

        public void EndRefresh(Key key)
        {
            lock (_gate) { _refreshingKeys.Remove(key); }
        }

        /// <summary>Marks every entry out of date WITHOUT dropping it, and returns the keys that now owe
        /// a refresh.</summary>
        public IReadOnlyList<Key> InvalidateAll()
        {
            lock (_gate)
            {
                _staleKeys.Clear();
                foreach (Key key in _values.Keys) _staleKeys.Add(key);
                return _values.Keys.ToList();
            }
        }
    }
}
