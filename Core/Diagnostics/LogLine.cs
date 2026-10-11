using System;
using System.Globalization;

namespace FocusBear.Core.Diagnostics
{
    /// <summary>
    /// One app log line: "yyyy-MM-dd HH:mm:ss.fff - message", always in the invariant culture. It used to take the
    /// calling thread's culture, so the same log mixed "26/09/2026 3:03:25 PM" (threads started before the app set
    /// the invariant culture, and pool threads that kept the machine's) with "09/26/2026 15:03:26".
    /// </summary>
    public static class LogLine
    {
        public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

        public static string Format(DateTime localTime, string message) =>
            localTime.ToString(TimestampFormat, CultureInfo.InvariantCulture) + " - " + (message ?? "");
    }
}
