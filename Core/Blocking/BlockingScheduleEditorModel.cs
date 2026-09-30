using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// What a BRAND NEW blocking schedule starts out as. Only ever applied when creating one — an
    /// existing schedule is always loaded from its saved values, which is what stops a change here
    /// rewriting anybody's saved window.
    /// </summary>
    public static class BlockingScheduleDefaults
    {
        /// <summary>Minutes in a day; a window's end may sit exactly here (midnight).</summary>
        public const int MinutesPerDay = 24 * 60;

        /// <summary>
        /// 08:00–17:00, a plausible workday, from report 2026-09-15_17-43-33. The old "two minutes
        /// from now, for an hour" made onboarding create a block that expired the same morning.
        /// </summary>
        public const int StartMinutes = 8 * 60;

        public const int EndMinutes = 17 * 60;

        /// <summary>
        /// Both off: a new user opts IN to AI relevance checking and to losing microbreaks, rather
        /// than discovering them already ticked.
        /// </summary>
        public const bool IsAiEnabled = false;

        public const bool SkipMicroBreaks = false;

        public const FocusModeStrictness Strictness = FocusModeStrictness.Normal;

        /// <summary>Window a pre-filled start falls back to when its end is not after it.</summary>
        public const int MinimumNewWindowMinutes = 60;

        /// <summary>Shortest window a SAVED schedule is allowed to collapse to on the slider.</summary>
        public const int MinimumSavedWindowMinutes = 15;
    }

    /// <summary>
    /// The state the blocking-schedule editor is seeded with, ported from the Mac's
    /// <c>BlockingScheduleEditorModel</c>. Only the two seeding paths are here; the SwiftUI bindings,
    /// the block-list picker and the save/overlap plumbing are not.
    ///
    /// These two paths are the whole point: <see cref="ApplyNewScheduleDefaults"/> is the ONLY one
    /// that consults <see cref="BlockingScheduleDefaults"/>, and <see cref="ApplySavedSchedule"/>
    /// never does.
    /// </summary>
    public sealed class BlockingScheduleEditorModel
    {
        public int StartMinutes { get; private set; } = BlockingScheduleDefaults.StartMinutes;
        public int EndMinutes { get; private set; } = BlockingScheduleDefaults.EndMinutes;
        public bool UseAI { get; set; } = BlockingScheduleDefaults.IsAiEnabled;
        public bool SkipMicroBreaks { get; set; } = BlockingScheduleDefaults.SkipMicroBreaks;
        public bool IsEditing { get; private set; }

        /// <summary>
        /// Seed the editor for a NEW schedule. Times can be pre-filled — dragging a slot on the
        /// calendar, or picking a preset — and anything not pre-filled falls back to the defaults.
        /// </summary>
        public void ApplyNewScheduleDefaults(int? startMinutes = null, int? endMinutes = null)
        {
            IsEditing = false;
            StartMinutes = startMinutes ?? BlockingScheduleDefaults.StartMinutes;
            EndMinutes = endMinutes ?? BlockingScheduleDefaults.EndMinutes;
            if (EndMinutes <= StartMinutes)
            {
                EndMinutes = Math.Min(BlockingScheduleDefaults.MinutesPerDay,
                                      StartMinutes + BlockingScheduleDefaults.MinimumNewWindowMinutes);
            }
            UseAI = BlockingScheduleDefaults.IsAiEnabled;
            SkipMicroBreaks = BlockingScheduleDefaults.SkipMicroBreaks;
        }

        /// <summary>
        /// Load an EXISTING schedule's saved window and toggles. Defaults never apply here — this is
        /// what keeps a user who already picked 9–6, or ticked "skip microbreaks", seeing their own
        /// values.
        ///
        /// Note the toggle is inverted on the way in: the schedule stores whether microbreaks are
        /// ENABLED, the editor offers to SKIP them.
        /// </summary>
        public void ApplySavedSchedule(int startMinutes, int endMinutes,
                                       bool isAIBlockingEnabled, bool isMicroBreaksEnabled)
        {
            IsEditing = true;
            StartMinutes = startMinutes;
            // A cross-midnight window isn't representable on the slider; clamp it to the end of day.
            int rawEnd = endMinutes <= startMinutes ? BlockingScheduleDefaults.MinutesPerDay : endMinutes;
            EndMinutes = Math.Min(BlockingScheduleDefaults.MinutesPerDay,
                                  Math.Max(startMinutes + BlockingScheduleDefaults.MinimumSavedWindowMinutes, rawEnd));
            UseAI = isAIBlockingEnabled;
            SkipMicroBreaks = !isMicroBreaksEnabled;
        }
    }
}
