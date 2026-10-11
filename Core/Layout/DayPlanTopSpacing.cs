using System;

namespace FocusBear.Core.Layout
{
    /// <summary>
    /// How close to the top of the window the Day Plan starts (report dayplan-top-space-2026-09-26_11-54-27, "wasted
    /// blank space at top of day plan", then "there's still a bit of space above").
    /// <para>
    /// The main window keeps a slim bar above every screen for its minimise button, on the right. The agenda column
    /// sits on the left, where that bar is empty, so the agenda moves up into it and its date bar starts
    /// <see cref="AgendaGap"/> below the window's top instead of the bar's height plus that. The tasks panel stays
    /// under the bar, because the minimise button is above it, and clears the routine's "Exit Fullscreen" button only
    /// while that button is shown.
    /// </para>
    /// </summary>
    public static class DayPlanTopSpacing
    {
        /// <summary>The agenda's own top margin (HabitsDayPlan.xaml).</summary>
        public const double AgendaGap = 8;

        /// <summary>The tasks panel's top margin without the Exit Fullscreen button.</summary>
        public const double TasksPanelGap = 14;

        /// <summary>The tasks panel's top margin under the Exit Fullscreen button (Height 40 + 7 above and below).</summary>
        public const double TasksPanelUnderExitButton = 54;

        /// <summary>
        /// The agenda's top offset: up by the empty bar above it (<paramref name="topBarHeight"/>, 0 when the Day Plan
        /// is not hosted under the main window's bar).
        /// </summary>
        public static double AgendaTopOffset(double topBarHeight)
        {
            if (double.IsNaN(topBarHeight) || double.IsInfinity(topBarHeight) || topBarHeight <= 0) return 0;
            return -topBarHeight;
        }

        public static double TasksPanelTop(bool exitButtonShown) => exitButtonShown ? TasksPanelUnderExitButton : TasksPanelGap;
    }
}
