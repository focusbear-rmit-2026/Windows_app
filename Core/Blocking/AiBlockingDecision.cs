using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The block list a schedule runs, as far as the AI decision cares. Absent values matter here:
    /// <see cref="AiEnabled"/> is null when the server never sent <c>is_ai_enabled</c>, which means
    /// "on", not "off".
    /// </summary>
    public sealed class ScheduleBlockList
    {
        /// <summary>The block list's <c>is_ai_enabled</c>. Null = the key was absent, which reads as on.</summary>
        public bool? AiEnabled { get; set; }

        /// <summary>
        /// The block list's <c>isAllowedListApproach</c>. AI relevance checking is only wired into the
        /// allow-list path, so a blacklist never consults it however its flag is set.
        /// </summary>
        public bool IsAllowedListApproach { get; set; }
    }

    /// <summary>
    /// The custom blocking schedule that is currently on, plus the block list it runs.
    /// </summary>
    public sealed class AiScheduleContext
    {
        /// <summary>
        /// The block list named by the schedule's focus-mode id, or null when it could not be
        /// resolved (deleted mode, list not synced yet). Null is what makes
        /// <see cref="OwnAiFlag"/> load-bearing.
        /// </summary>
        public ScheduleBlockList BlockList { get; set; }

        /// <summary>
        /// The schedule's own AI checkbox. A fallback only — consulted when <see cref="BlockList"/>
        /// cannot be resolved. On Windows this is <c>CustomBlockingSchedule.bAIBlockingEnabled</c>.
        /// </summary>
        public bool OwnAiFlag { get; set; }
    }

    /// <summary>
    /// Everything the AI decision reads. Gathered by the caller so the rule itself stays pure and
    /// testable; the Mac reads these off globals scattered across two files.
    /// </summary>
    public sealed class AiBlockingInputs
    {
        /// <summary>The quit-popup pause. A hard veto over everything below it.</summary>
        public bool BlockingPopupsSnoozed { get; set; }

        /// <summary>A teacher-controlled class session is running.</summary>
        public bool TeacherSessionActive { get; set; }

        /// <summary>
        /// The running focus mode's <c>isAiBlockingEnabledForAllowList</c> — the teacher's choice.
        /// Absent reads as false, so a teacher who did not ask for AI gets immediate blocking
        /// rather than a ~3-4s round trip.
        /// </summary>
        public bool TeacherAiEnabledForAllowList { get; set; }

        /// <summary>An accountability-buddy session is running. AI-driven by design.</summary>
        public bool AccountabilityBuddySessionActive { get; set; }

        /// <summary>A focus mode is running.</summary>
        public bool FocusModeRunning { get; set; }

        /// <summary>An allowed-app routine activity is running.</summary>
        public bool AllowedFocusModeActivityRunning { get; set; }

        /// <summary>Any custom blocking schedule is on.</summary>
        public bool AnyCustomScheduleOn { get; set; }

        /// <summary>
        /// The running focus mode's <c>is_ai_enabled</c>. Null = absent = on. Only ever a veto:
        /// true or absent falls through, so this can never force AI on where the user turned it off.
        /// </summary>
        public bool? RunningFocusModeAiEnabled { get; set; }

        /// <summary>The schedule that is on, or null when none is.</summary>
        public AiScheduleContext ActiveSchedule { get; set; }

        /// <summary>The user's global "use AI to check URLs" setting.</summary>
        public bool GlobalAiCheckUrlEnabled { get; set; }
    }

    /// <summary>
    /// Whether AI relevance checking should run right now.
    /// <para>
    /// Ported from the Mac's <c>SettingsManager.isAIBlockingEnabled()</c> and
    /// <c>customBlockingScheduleData.isAIBlockingEnabled()</c>, which together form one precedence
    /// ladder split across two files.
    /// </para>
    /// <para>
    /// Windows previously answered this question with <c>schedule.bAIBlockingEnabled</c> alone, at
    /// eleven separate call sites. That misses every rung of the ladder — the snooze, the teacher's
    /// choice, the buddy session, and most visibly the block list's own <c>is_ai_enabled</c>. Since
    /// the Mac writes that key and Windows ignored it, a user who turned AI off for a block list on
    /// the Mac still got AI checks on Windows.
    /// </para>
    /// <para>
    /// AI belongs to the BLOCK LIST, not the schedule (Mac-App CLAUDE.md). The schedule's own flag
    /// survives only as a fallback for an unresolvable block list, which is why the checkbox that
    /// writes it is hidden rather than deleted.
    /// </para>
    /// </summary>
    public static class AiBlockingDecision
    {
        /// <summary>
        /// Evaluates the ladder. Each rung returns outright; order is the rule.
        /// </summary>
        public static bool IsAiBlockingEnabled(AiBlockingInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            // The user switched blocking pop-ups off for a while. Stay quiet on every AI path until
            // the window passes, rather than asking the AI questions whose answer cannot be shown.
            if (inputs.BlockingPopupsSnoozed) return false;

            // A teacher's choice outranks the student's own setting, in both directions.
            if (inputs.TeacherSessionActive) return inputs.TeacherAiEnabledForAllowList;

            // A buddy session judges everything against the stated goal, so it IS the AI feature.
            if (inputs.AccountabilityBuddySessionActive) return true;

            // Veto only, and only when a focus mode is the running context with no schedule active
            // (a schedule brings its own block list, handled below).
            bool focusModeIsTheContext = inputs.FocusModeRunning || inputs.AllowedFocusModeActivityRunning;
            if (focusModeIsTheContext && !inputs.AnyCustomScheduleOn && inputs.RunningFocusModeAiEnabled == false)
            {
                return false;
            }

            return inputs.AnyCustomScheduleOn
                ? IsAiEnabledForActiveSchedule(inputs.ActiveSchedule)
                : inputs.GlobalAiCheckUrlEnabled;
        }

        /// <summary>
        /// The schedule half of the ladder. Note it does NOT consult the global toggle: once a
        /// schedule is on, the block list it runs is the whole answer.
        /// </summary>
        private static bool IsAiEnabledForActiveSchedule(AiScheduleContext schedule)
        {
            // "Any schedule on" was true but no schedule is actually in the on state — a race
            // between the two reads on the Mac. Treated as off there; kept identical here.
            if (schedule == null) return false;

            // The block list could not be resolved, so the schedule's hidden flag is all that is left.
            if (schedule.BlockList == null) return schedule.OwnAiFlag;

            // A blacklist short-circuits every un-listed URL to allowed and never reaches the AI, so
            // reporting true would start an "AI blocking session" that blocks nothing — a real Mac
            // bug (a user in a meeting watched AI blocking start while everything stayed allowed).
            return schedule.BlockList.IsAllowedListApproach && (schedule.BlockList.AiEnabled ?? true);
        }
    }
}
