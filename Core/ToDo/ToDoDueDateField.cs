using System;
using System.Globalization;

namespace FocusBear.Core.ToDo
{
    public enum ToDoDueDatePreviewKind
    {
        /// <summary>Nothing typed and no due date yet — the line stays blank.</summary>
        Blank,

        /// <summary>A day to render: the live parse of what is being typed, or the date already saved.</summary>
        Day,

        /// <summary>The typed text is not a date the grammar understands.</summary>
        Unrecognised,
    }

    /// <summary>What the line beneath a due-date field shows while the user types.</summary>
    public struct ToDoDueDatePreview : IEquatable<ToDoDueDatePreview>
    {
        private ToDoDueDatePreview(ToDoDueDatePreviewKind kind, DateTime? day)
        {
            Kind = kind;
            Day = day;
        }

        public ToDoDueDatePreviewKind Kind { get; }

        /// <summary>Set only when <see cref="Kind"/> is <see cref="ToDoDueDatePreviewKind.Day"/>.</summary>
        public DateTime? Day { get; }

        public static readonly ToDoDueDatePreview Blank =
            new ToDoDueDatePreview(ToDoDueDatePreviewKind.Blank, null);

        public static readonly ToDoDueDatePreview Unrecognised =
            new ToDoDueDatePreview(ToDoDueDatePreviewKind.Unrecognised, null);

        public static ToDoDueDatePreview ForDay(DateTime day) =>
            new ToDoDueDatePreview(ToDoDueDatePreviewKind.Day, day);

        public bool Equals(ToDoDueDatePreview other) => Kind == other.Kind && Day == other.Day;
        public override bool Equals(object obj) => obj is ToDoDueDatePreview other && Equals(other);
        public override int GetHashCode() => (int)Kind * 397 ^ (Day?.GetHashCode() ?? 0);
        public override string ToString() => Kind == ToDoDueDatePreviewKind.Day
            ? "Day(" + ToDoDates.Ymd(Day.Value) + ")"
            : Kind.ToString();
    }

    /// <summary>
    /// Resolves the preview line without touching the saved date, so typing previews rather than commits.
    ///
    /// <c>hasSettled</c> is what keeps the "not a date" hint from strobing: typing "28th" passes
    /// through the unparseable "2", "28t", and only once typing pauses (or Return is pressed) does the
    /// caller pass true and let the hint show. Until then an unparseable draft keeps showing the saved
    /// date, which is still what a lost focus would leave behind.
    /// </summary>
    public static class ToDoDueDatePreviewResolver
    {
        public static ToDoDueDatePreview Preview(string input, DateTime? committedDay, bool hasSettled,
                                                 DateTime now, CultureInfo culture = null)
        {
            string typed = (input ?? string.Empty).Trim();

            // An emptied field falls back to the saved date rather than accusing the user of nonsense.
            if (typed.Length == 0) return FallBack(committedDay);

            DateTime? parsed = ToDoDueDateParser.Parse(typed, now, culture);
            if (parsed.HasValue) return ToDoDueDatePreview.ForDay(parsed.Value);

            if (!hasSettled) return FallBack(committedDay);
            return ToDoDueDatePreview.Unrecognised;
        }

        private static ToDoDueDatePreview FallBack(DateTime? committedDay)
        {
            return committedDay.HasValue
                ? ToDoDueDatePreview.ForDay(committedDay.Value)
                : ToDoDueDatePreview.Blank;
        }
    }

    /// <summary>
    /// The text inside the typed due-date field, which is the date itself rather than an empty box
    /// behind a placeholder (Mac report 2026-09-15_19-50-02).
    ///
    /// Two rules:
    ///  - <see cref="Text"/> writes a day in a form <see cref="ToDoDueDateParser"/> reads back, so what
    ///    the field shows is what the next save sends. "Fri 11 Sep" would not: the grammar has no
    ///    reading for a leading weekday name.
    ///  - <see cref="Reseed"/> re-syncs the text when the day moved for some *other* reason — a
    ///    calendar picker, a forecast suggestion — while leaving the user's own half-typed text alone.
    ///    Without that second rule, typing "2" (on the way to "2d") would rewrite the field to
    ///    "2 Oct 2026" under the caret.
    /// </summary>
    public static class ToDoDueDateFieldText
    {
        public static string Text(DateTime? day, CultureInfo culture = null)
        {
            return day.HasValue ? ToDoDates.DayMonthYear(day.Value, culture) : string.Empty;
        }

        /// <summary>The text the field should switch to, or null when the typed text already means <paramref name="day"/>.</summary>
        public static string Reseed(string current, DateTime? day, DateTime now, CultureInfo culture = null)
        {
            string typed = (current ?? string.Empty).Trim();
            DateTime? typedDay = typed.Length == 0 ? null : ToDoDueDateParser.Parse(typed, now, culture);
            if (typedDay == day) return null;
            return Text(day, culture);
        }
    }
}
