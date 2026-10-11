using System;
using System.Globalization;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Forecast
{
    /// <summary>
    /// Whether the server row is still the one the user reviewed, so a reschedule write cannot clobber an
    /// edit made elsewhere — the Mac's <c>ForecastRowGuard</c>.
    ///
    /// Compared field by field (due date, title, status), not by timestamp: the catalogue the plan is built
    /// from (<c>GET /to-do?…&amp;exclude_completed=true</c>) carries no <c>updated_at</c>, while the re-read
    /// before each write (<c>GET /to-do/by-ids</c>) does.
    ///
    /// The due date is compared as an INSTANT, not as text. The same moment reaches the app spelled
    /// differently depending on who serialised it last — <c>2026-09-27T00:00:00.000Z</c> straight from the
    /// server, <c>2026-09-27T00:00:00Z</c> once Json.NET has read it as a DateTime and written it back —
    /// and a text comparison between those refused every dated task: the apply loop stopped at the first
    /// row, never sent a <c>PUT /to-do</c>, and said "Saved 0 date changes" (Windows report 2026-09-25).
    /// </summary>
    public static class ForecastRowGuard
    {
        public enum Refusal { MissingRow, UnknownToSnapshot, EditedElsewhere }

        /// <summary>Null when the write may proceed.</summary>
        public static Refusal? For(ToDoTask latest, ToDoTask previous)
        {
            if (latest == null) return Refusal.MissingRow;
            if (previous == null || previous.Id != latest.Id) return Refusal.UnknownToSnapshot;
            if (!SameDueDate(latest.DueDate, previous.DueDate)) return Refusal.EditedElsewhere;
            if ((latest.Title ?? "") != (previous.Title ?? "")) return Refusal.EditedElsewhere;
            if ((latest.Status ?? "") != (previous.Status ?? "")) return Refusal.EditedElsewhere;
            return null;
        }

        /// <summary>Two wire due dates that name the same moment (or the same plain day), however spelled.</summary>
        public static bool SameDueDate(string a, string b)
        {
            bool aEmpty = string.IsNullOrWhiteSpace(a), bEmpty = string.IsNullOrWhiteSpace(b);
            if (aEmpty || bEmpty) return aEmpty && bEmpty;
            if (string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal)) return true;
            DateTimeOffset x, y;
            if (TryInstant(a, out x) && TryInstant(b, out y)) return x.UtcDateTime == y.UtcDateTime;
            DateTime? dx = ToDoDates.Parse(a), dy = ToDoDates.Parse(b);
            return dx.HasValue && dy.HasValue && dx.Value == dy.Value;
        }

        private static bool TryInstant(string value, out DateTimeOffset instant)
        {
            instant = default(DateTimeOffset);
            string trimmed = value.Trim();
            return trimmed.IndexOf('T') >= 0
                   && DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out instant);
        }
    }
}
