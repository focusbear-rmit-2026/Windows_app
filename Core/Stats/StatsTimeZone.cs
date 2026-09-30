using System;
using System.Globalization;

namespace FocusBear.Core.Stats
{
    /// <summary>The zone the stats endpoints bucket days in, for a Windows zone the IANA table does not list.</summary>
    public static class StatsTimeZone
    {
        /// <summary>
        /// A zone the server's Luxon reads for <paramref name="offset"/>. A whole-hour offset is <c>Etc/GMT∓N</c>
        /// (sign-inverted, as those names are). A fractional one - India +5:30, Nepal +5:45, Iran +3:30, Newfoundland
        /// -3:30, Adelaide's +9:30 when it is not in the table - used to fall back to "UTC", so every day on the
        /// habit heat map and in the consistency stats started five and a half hours out; it is now the fixed offset
        /// <c>UTC+05:30</c>, which the rest of the app already sends.
        /// </summary>
        public static string ForOffset(TimeSpan offset)
        {
            if (offset == TimeSpan.Zero) return "UTC";
            if (offset.Minutes == 0 && offset.Seconds == 0)
            {
                int whole = (int)offset.TotalHours;
                return "Etc/GMT" + (whole > 0 ? "-" : "+") + Math.Abs(whole).ToString(CultureInfo.InvariantCulture);
            }
            string sign = offset < TimeSpan.Zero ? "-" : "+";
            return "UTC" + sign + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
    }
}
