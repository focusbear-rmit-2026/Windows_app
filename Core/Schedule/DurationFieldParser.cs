using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The Day Plan's duration grammar, ported from the Mac's <c>QuickAddTaskParser</c>. One parser, so
    /// "4h", "1h30", "90min" and a bare number mean the same thing in every duration field.
    ///
    /// The bug it exists for: the Day Plan's duration fields did <c>filter(isNumber)</c>, so "4h" became
    /// 4 minutes and "1h30" became 130 (report 2026-07-15_20-25-27).
    /// </summary>
    public static class DurationFieldParser
    {
        /// <summary>The ceiling every Day Plan duration field clamps to.</summary>
        public const int MaxDurationMinutes = 24 * 60;

        /// <summary>
        /// A standalone duration token: <c>Nm</c>/<c>Nmin</c>/<c>Nmins</c>, <c>Nh</c>/<c>Nhr</c>/<c>Nhrs</c>,
        /// or <c>NhM</c>/<c>NhMm</c>. The token must stand alone (whitespace or line edge on both sides)
        /// so "swimming", or a bare number in a task title, is never mistaken for one.
        /// </summary>
        private const string DurationTokenPattern =
            @"(?:^|\s)(?:(\d{1,2})\s*h(?:rs?)?\s*(\d{1,2})\s*m?(?:ins?)?|(\d{1,2})\s*h(?:rs?)?|(\d{1,4})\s*m(?:ins?)?)(?=\s|$)";

        private static readonly Regex TokenRegex =
            new Regex(DurationTokenPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const int MinutesPerHour = 60;

        /// <summary>
        /// What a duration FIELD should accept, as opposed to a duration token buried in a task title.
        /// <see cref="ExtractDurationToken"/> already understands every form a person types, but it
        /// deliberately ignores a BARE number so a title's "call Bob 5" is not eaten as a duration. In a
        /// field a bare number is the commonest input of all, so it falls back to reading digits as
        /// minutes. Returns null when nothing in the text reads as a length.
        /// </summary>
        public static int? DurationMinutesFromFieldText(string text)
        {
            string trimmed = FoldDigits(text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return null;

            int? tokenMinutes = ExtractDurationToken(trimmed).Minutes;
            if (tokenMinutes.HasValue) return tokenMinutes;

            string digits = new string(trimmed.Where(char.IsDigit).ToArray());
            int bare;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out bare) || bare <= 0) return null;
            return Math.Min(bare, MaxDurationMinutes);
        }

        /// <summary>Every script's decimal digits as ASCII: an IME's full-width "４５" is 45 (as in the due-date parser).</summary>
        private static string FoldDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var folded = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                folded.Append(c > '\u007f' && char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c);
            return folded.ToString();
        }

        /// <summary>The title with its duration token removed, and the minutes that token meant.</summary>
        public struct DurationToken
        {
            public DurationToken(string title, int? minutes)
            {
                Title = title;
                Minutes = minutes;
            }

            public string Title { get; }
            public int? Minutes { get; }
        }

        /// <summary>
        /// Pull a standalone duration token out of a typed line and strip it: "write report 1h30" →
        /// ("write report", 90). The LAST such token wins. The result is clamped to
        /// 1…<see cref="MaxDurationMinutes"/>; "0m" is not a duration and is left in the title.
        /// </summary>
        public static DurationToken ExtractDurationToken(string raw)
        {
            string text = raw ?? string.Empty;
            Match match = TokenRegex.Matches(text).Cast<Match>().LastOrDefault();
            if (match == null) return new DurationToken(text.Trim(), null);

            int? hoursAndMinutes = Group(match, 1);
            int? minutesOfPair = Group(match, 2);
            int? hours = Group(match, 3);
            int? minutes = Group(match, 4);

            int total;
            if (hoursAndMinutes.HasValue && minutesOfPair.HasValue)
                total = hoursAndMinutes.Value * MinutesPerHour + minutesOfPair.Value;
            else if (hours.HasValue) total = hours.Value * MinutesPerHour;
            else if (minutes.HasValue) total = minutes.Value;
            else return new DurationToken(text.Trim(), null);

            if (total <= 0) return new DurationToken(text.Trim(), null);

            string title = text.Remove(match.Index, match.Length).Insert(match.Index, " ")
                               .Replace("  ", " ").Trim();
            return new DurationToken(title, Math.Min(total, MaxDurationMinutes));
        }

        private static int? Group(Match match, int index)
        {
            Group group = match.Groups[index];
            if (!group.Success) return null;
            int value;
            return int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                ? (int?)value
                : null;
        }
    }
}
