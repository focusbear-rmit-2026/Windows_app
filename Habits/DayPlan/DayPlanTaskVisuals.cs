using System.Windows.Media;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Importance ("outcome") band colours for Day Plan tasks — the Windows port of the macOS
    /// <c>DayPlanTask.outcomeColor</c>/<c>outcomeForeground</c>. Four bands (higher = hotter); the
    /// foreground is black or white by luminance so the text stays legible on the band.
    /// </summary>
    public static class DayPlanTaskVisuals
    {
        // System colours matching macOS: gray 8E8E93, blue 007AFF, orange FF9500, red FF3B30.
        private static readonly Color Gray = Color.FromRgb(0x8E, 0x8E, 0x93);
        private static readonly Color Blue = Color.FromRgb(0x00, 0x7A, 0xFF);
        private static readonly Color Orange = Color.FromRgb(0xFF, 0x95, 0x00);
        private static readonly Color Red = Color.FromRgb(0xFF, 0x3B, 0x30);

        /// <summary>Band colour for an importance value (1–10), or null when unset.</summary>
        public static Color? OutcomeColor(int? outcome)
        {
            if (!outcome.HasValue) return null;
            int o = outcome.Value;
            if (o <= 3) return Gray;      // 1–3 low
            if (o <= 6) return Blue;      // 4–6 normal
            if (o <= 8) return Orange;    // 7–8 high
            return Red;                   // 9–10 critical
        }

        /// <summary>High-contrast text colour on top of the band, or null when unset.</summary>
        public static Color? OutcomeForeground(int? outcome)
        {
            Color? bg = OutcomeColor(outcome);
            if (!bg.HasValue) return null;
            double lum = (0.299 * bg.Value.R + 0.587 * bg.Value.G + 0.114 * bg.Value.B) / 255.0;
            return lum > 0.6 ? Colors.Black : Colors.White;
        }
    }
}
