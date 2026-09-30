using System;
using System.Globalization;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Reading the date shapes the to-do API sends. Ported from the Mac's <c>ToDoDates</c>; only the two
    /// members the Day Plan's due-date projection needs are here, since every other member of that enum
    /// formats dates for display and belongs with the UI that shows them.
    /// </summary>
    public static class ToDoDates
    {
        public const string DayFormat = "yyyy-MM-dd";

        private const string IsoWithFractions = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK";
        private const string IsoWithoutFractions = "yyyy-MM-dd'T'HH:mm:ssK";

        /// <summary>
        /// Accepts the API's ISO-8601 timestamps (with or without fractional seconds) and plain
        /// <c>YYYY-MM-DD</c>.
        ///
        /// A timestamp is converted to LOCAL time before it is returned, because the day a to-do belongs
        /// to is the day the user sees on their own clock — the Mac gets the same result by rendering
        /// through a system-timezone <c>DateFormatter</c>. A plain date is read as local midnight, so it
        /// round-trips through <see cref="Ymd"/> unchanged wherever the machine is.
        /// </summary>
        public static DateTimeOffset? Parse(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return null;

            DateTimeOffset parsed;
            string[] isoFormats = { IsoWithFractions, IsoWithoutFractions };
            if (DateTimeOffset.TryParseExact(trimmed, isoFormats, CultureInfo.InvariantCulture,
                                             DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToLocalTime();
            }

            if (DateTimeOffset.TryParseExact(trimmed, DayFormat, CultureInfo.InvariantCulture,
                                             DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// The <c>yyyy-MM-dd</c> day of an instant, read in that instant's OWN offset — a caller holding
        /// a UTC timestamp converts first, which is what <see cref="Parse"/> already does.
        /// </summary>
        public static string Ymd(DateTimeOffset date)
        {
            return date.ToString(DayFormat, CultureInfo.InvariantCulture);
        }
    }
}
