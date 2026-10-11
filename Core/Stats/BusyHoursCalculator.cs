using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// A calendar event as the busy-hours report sees it. The platform's calendar layer maps its own
    /// event type onto this, which is what keeps every rule below testable without a calendar store.
    /// </summary>
    public sealed class BusyEventInput
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsAllDay { get; set; }
        public bool IsCancelled { get; set; }

        /// <summary>The user explicitly declined. Accepted / tentative / no-RSVP all still count as busy.</summary>
        public bool IsDeclined { get; set; }

        /// <summary>Hidden from the Day Plan by the user ("dismiss" on a meeting).</summary>
        public bool IsDismissed { get; set; }

        /// <summary>Whether the event occupies the user's time: timed, still on, and not opted out of.</summary>
        public bool IsBusy
        {
            get { return !IsAllDay && !IsCancelled && !IsDeclined && !IsDismissed && End > Start; }
        }
    }

    /// <summary>A half-open span of time. Public so the merging rules can be exercised on their own.</summary>
    public struct BusyInterval : IEquatable<BusyInterval>
    {
        public BusyInterval(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }

        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        public bool Equals(BusyInterval other)
        {
            return Start == other.Start && End == other.End;
        }

        public override bool Equals(object obj)
        {
            return obj is BusyInterval && Equals((BusyInterval)obj);
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() ^ End.GetHashCode();
        }

        public override string ToString()
        {
            return Start.ToString("s", CultureInfo.InvariantCulture) + "→" + End.ToString("s", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The maths behind the <c>PUT /portfolio/busy-hours</c> report: how many hours of each coming day
    /// are already taken, so the backend's Day Plan simulation can subtract them from the user's
    /// capacity.
    ///
    /// Port of the Mac's <c>BusyHoursCalculator</c>. Dates are local wall-clock times; the caller
    /// supplies them already in the user's own zone, the way the Mac's <c>Calendar.current</c> does.
    /// </summary>
    public static class BusyHoursCalculator
    {
        /// <summary>The backend validates hours as a number; two decimals keeps the payload stable and readable.</summary>
        public const int HoursDecimalPlaces = 2;

        private const double SecondsPerHour = 3600.0;
        private const string DayKeyFormat = "yyyy-MM-dd";

        /// <summary>
        /// Busy hours for each of the <paramref name="horizonDays"/> days starting at
        /// <paramref name="firstDay"/>'s day.
        ///
        /// Every day in the horizon is present, 0 when free, so the report states "free" explicitly
        /// rather than by omission. Overlapping events are merged before summing, so a double-booked
        /// hour counts once; an event spanning midnight is split so each day gets its own share.
        /// </summary>
        public static IDictionary<string, double> HoursByDay(IEnumerable<BusyEventInput> events, DateTime firstDay,
                                                             int horizonDays)
        {
            Dictionary<string, double> hoursByDay = new Dictionary<string, double>();
            if (horizonDays <= 0)
            {
                return hoursByDay;
            }

            DateTime horizonStart = firstDay.Date;
            DateTime horizonEnd = horizonStart.AddDays(horizonDays);

            Dictionary<string, double> secondsByDay = new Dictionary<string, double>();
            for (int offset = 0; offset < horizonDays; offset++)
            {
                secondsByDay[DayKey(horizonStart.AddDays(offset))] = 0;
            }

            IEnumerable<BusyInterval> busy = Merge((events ?? Enumerable.Empty<BusyEventInput>())
                .Where(candidate => candidate.IsBusy)
                .Select(candidate => new BusyInterval(candidate.Start, candidate.End)));

            foreach (BusyInterval interval in busy)
            {
                foreach (BusyInterval piece in SplitAtMidnight(interval))
                {
                    if (piece.Start < horizonStart || piece.Start >= horizonEnd)
                    {
                        continue;
                    }

                    string key = DayKey(piece.Start);
                    double existing;
                    secondsByDay.TryGetValue(key, out existing);
                    secondsByDay[key] = existing + (piece.End - piece.Start).TotalSeconds;
                }
            }

            foreach (KeyValuePair<string, double> day in secondsByDay)
            {
                hoursByDay[day.Key] = Rounded(day.Value / SecondsPerHour);
            }

            return hoursByDay;
        }

        /// <summary>Coalesces overlapping and touching intervals, returned in start order.</summary>
        public static IList<BusyInterval> Merge(IEnumerable<BusyInterval> intervals)
        {
            List<BusyInterval> merged = new List<BusyInterval>();
            foreach (BusyInterval interval in intervals.OrderBy(candidate => candidate.Start))
            {
                BusyInterval last = merged.Count > 0 ? merged[merged.Count - 1] : default(BusyInterval);
                if (merged.Count > 0 && interval.Start <= last.End)
                {
                    merged[merged.Count - 1] = new BusyInterval(last.Start, last.End > interval.End ? last.End : interval.End);
                }
                else
                {
                    merged.Add(interval);
                }
            }

            return merged;
        }

        /// <summary>Cuts an interval at each midnight it crosses, so no piece belongs to more than one day.</summary>
        public static IList<BusyInterval> SplitAtMidnight(BusyInterval interval)
        {
            List<BusyInterval> pieces = new List<BusyInterval>();
            DateTime cursor = interval.Start;
            while (cursor < interval.End)
            {
                DateTime nextMidnight = cursor.Date.AddDays(1);
                DateTime pieceEnd = nextMidnight < interval.End ? nextMidnight : interval.End;
                pieces.Add(new BusyInterval(cursor, pieceEnd));
                cursor = pieceEnd;
            }

            return pieces;
        }

        public static double Rounded(double hours)
        {
            double scale = Math.Pow(10.0, HoursDecimalPlaces);
            return SwiftRounding.Rounded(hours * scale) / scale;
        }

        /// <summary>Local <c>YYYY-MM-DD</c> — the key format the backend expects.</summary>
        public static string DayKey(DateTime date)
        {
            return date.ToString(DayKeyFormat, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The body of the busy-hours report, and the local fingerprint that suppresses re-sending it.</summary>
    public static class BusyHoursReporter
    {
        /// <summary>
        /// A value that changes exactly when the report does, so an unchanged day plan is not re-sent.
        ///
        /// Order-independent: the day map has no inherent order, and a re-ordered dictionary is not a
        /// change. This never leaves the machine, so it does not have to match the Mac's string byte
        /// for byte — and it does not: .NET renders 2.0 as "2" where Swift renders "2.0".
        /// </summary>
        public static string Fingerprint(string source, IDictionary<string, double> days)
        {
            IEnumerable<string> entries = days.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => key + "=" + days[key].ToString("R", CultureInfo.InvariantCulture));
            return source + "|" + string.Join(",", entries.ToArray());
        }
    }
}
