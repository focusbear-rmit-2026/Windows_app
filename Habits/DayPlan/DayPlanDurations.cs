using FocusBear.Core.Schedule;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// How the Day Plan reads a task's length, from a typed field and from an imported to-do. The rules
    /// are FocusBear.Core's; this maps WPF inputs onto them, so both import paths and the edit field
    /// agree and can be tested headlessly.
    /// </summary>
    public static class DayPlanDurations
    {
        /// <summary>
        /// Minutes typed into a duration field: "45", "45m", "1h", "1h30". Null when nothing in it
        /// reads as a length, so the caller keeps what it had.
        /// <para>
        /// The field used to keep only its digits, so "1h30" saved as 130 minutes and "4h" as 4.
        /// </para>
        /// </summary>
        internal static int? FromFieldText(string text)
        {
            return DurationFieldParser.DurationMinutesFromFieldText(text);
        }

        /// <summary>What a task row's inline duration field shows: "15m" (the Mac's <c>"\(minutes)m"</c>).</summary>
        internal static string RowLabel(int minutes) => minutes + "m";

        /// <summary>
        /// The minutes a task row's duration field saves when it loses focus or Enter is pressed: what was typed, or
        /// <paramref name="current"/> when nothing in it reads as a length (Mac <c>DayPlanTasks.commitDuration</c>).
        /// </summary>
        internal static int CommitRowField(string text, int current) => FromFieldText(text) ?? current;

        /// <summary>
        /// Minutes for an imported to-do's <c>duration</c> (seconds, sent as a number or a numeric
        /// string), or the Day Plan's 15-minute default when it has none.
        /// <para>
        /// This used to be <c>int.TryParse(token.ToString())</c>, which rejects a JSON number with a
        /// fraction (300.5, or 3600.0 from a numeric column) and so fell back to 15 minutes; Core
        /// truncates it to whole seconds.
        /// </para>
        /// </summary>
        internal static int FromTodoDuration(JToken duration)
        {
            // The raw value, not the JToken: a JSON null would otherwise reach Core as a JValue whose
            // conversion throws a type Core does not expect.
            object raw = (duration as JValue)?.Value;
            return TodoFieldParsing.DurationMinutes(raw);
        }
    }
}
