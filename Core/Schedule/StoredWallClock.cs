using System;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The registry's "till" dates (shut-off aborted, app deactivated, routine shown, postpone end, first install,
    /// selected-habits day...) are stored as the local wall-clock time counted in seconds from 1970-01-01 00:00 -
    /// not a Unix time: Global.ConvertToUnixTime subtracts a UTC epoch from a local DateTime without converting.
    /// They were read back with Global.ConvertFromUnixTimeToDT as Kind=Utc, so the wall clock was right but
    /// labelled UTC: ToString("o") sent it with a "Z" (the install date on quit and uninstall feedback), and any
    /// ToLocalTime()/ToUniversalTime() would shift it by the UTC offset.
    ///
    /// These read the same stored numbers as Kind=Local wall-clock times, and write them the same way, so what is
    /// already stored keeps meaning what it meant. A wall-clock time survives a DST change unchanged.
    /// </summary>
    public static class StoredWallClock
    {
        private static readonly long EpochTicks = new DateTime(1970, 1, 1, 0, 0, 0).Ticks;

        /// <summary>
        /// The stored number for a wall-clock time: its fields as written, whatever its Kind. (A value read by the
        /// old code is Kind=Utc but holds the local wall clock, so it must not be converted either.)
        /// </summary>
        public static long ToStored(DateTime wallClock)
        {
            return (wallClock.Ticks - EpochTicks) / TimeSpan.TicksPerSecond;
        }

        /// <summary>A stored number as the local wall-clock time it was written from, Kind=Local.</summary>
        public static DateTime FromStored(long seconds)
        {
            return new DateTime(EpochTicks + seconds * TimeSpan.TicksPerSecond, DateTimeKind.Local);
        }

        /// <summary><see cref="FromStored(long)"/> for the registry's string value; null when it isn't a number.</summary>
        public static DateTime? FromStored(string stored)
        {
            if (stored == null || !long.TryParse(stored.Trim(), System.Globalization.NumberStyles.AllowLeadingSign,
                                                 System.Globalization.CultureInfo.InvariantCulture, out long seconds))
                return null;
            long maxSeconds = (DateTime.MaxValue.Ticks - EpochTicks) / TimeSpan.TicksPerSecond;
            long minSeconds = -EpochTicks / TimeSpan.TicksPerSecond;
            if (seconds < minSeconds || seconds > maxSeconds)
                return null;
            return FromStored(seconds);
        }
    }
}
