using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Everything <see cref="BlockingDecision"/> consults, passed in instead of read from globals.
    ///
    /// On the Mac these are process-wide mutable variables (<c>isFocusBreakOn</c>,
    /// <c>currentState</c>, <c>isExpired</c>,
    /// <c>customBlockingScheduleManagerObj.isAnyCustomScheduleOn</c>) plus one UserDefaults read,
    /// which is why the Mac's own tests have to save and restore them around every case.
    /// </summary>
    public sealed class BlockingDecisionInputs
    {
        /// <summary>A custom blocking schedule is currently running. Gates ALL the break rules below.</summary>
        public bool IsAnyCustomScheduleOn { get; set; }

        /// <summary>The user is on a pomodoro/focus break.</summary>
        public bool IsFocusBreakOn { get; set; }

        /// <summary>The app state machine is in <c>_MICRO_BREAK_</c>.</summary>
        public bool IsMicroBreakState { get; set; }

        /// <summary>
        /// The "don't block during super-pomodoro breaks" preference — <c>null</c> when it has never
        /// been set. The Mac reads it as an optional and only acts on an explicit <c>true</c>, so
        /// absent and <c>false</c> both keep blocking; a non-nullable bool would have collapsed that.
        /// </summary>
        public bool? DoNotBlockUrlsDuringSuperPomodoroBreaks { get; set; }

        /// <summary>A calendar meeting is in progress.</summary>
        public bool IsUserOnMeeting { get; set; }

        /// <summary>The "keep blocking during meetings" setting.</summary>
        public bool IsBlockDistractingSitesDuringMeetingsEnabled { get; set; }

        /// <summary>The licence has expired, which caps the lists rather than clearing them.</summary>
        public bool IsExpired { get; set; }
    }

    /// <summary>
    /// The Mac's <c>getBlockedUrlsToBlock()</c> / <c>getBlockedAppsToBlock()</c> from
    /// <c>globalFile.swift</c> (Mac-App#1625) — what the enforcement loop is actually handed, as
    /// opposed to what the user configured.
    ///
    /// Two things happen here. Breaks and meetings can suppress the whole list, but ONLY while a
    /// custom blocking schedule is running — during a focus session or a routine the same break
    /// leaves blocking fully on. And an expired licence truncates the list to its first few entries
    /// rather than disabling blocking, so a lapsed user keeps some protection.
    /// </summary>
    public static class BlockingDecision
    {
        /// <summary>
        /// How many entries an expired app still enforces. Also the number the upgrade nag and the
        /// block-list editors quote to the user, so it is one constant, not three.
        /// </summary>
        public const int AlwaysBlockedCountInExpiredApp = 3;

        public static IReadOnlyList<string> BlockedUrlsToBlock(IReadOnlyList<string> configuredUrls,
                                                              BlockingDecisionInputs inputs)
        {
            return Apply(configuredUrls, inputs);
        }

        public static IReadOnlyList<string> BlockedAppsToBlock(IReadOnlyList<string> configuredApps,
                                                               BlockingDecisionInputs inputs)
        {
            return Apply(configuredApps, inputs);
        }

        /// <summary>
        /// The Mac duplicates this body once for URLs and once for apps; they have to stay in step,
        /// so it lives once here and the two named entry points above are kept for comparability.
        /// </summary>
        private static IReadOnlyList<string> Apply(IReadOnlyList<string> configured, BlockingDecisionInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (configured == null) return new string[0];
            if (SuppressesEverything(inputs)) return new string[0];

            int count = configured.Count;
            if (inputs.IsExpired && count > AlwaysBlockedCountInExpiredApp)
            {
                count = AlwaysBlockedCountInExpiredApp;
            }

            var result = new List<string>(count);
            for (int index = 0; index < count; index++) result.Add(configured[index]);
            return result;
        }

        private static bool SuppressesEverything(BlockingDecisionInputs inputs)
        {
            // Every rule below is inside this gate on the Mac: with no custom schedule running,
            // a break or a meeting suppresses nothing at all.
            if (!inputs.IsAnyCustomScheduleOn) return false;

            bool doNotBlockDuringBreaks = inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks == true;
            if (inputs.IsFocusBreakOn && doNotBlockDuringBreaks) return true;
            if (inputs.IsMicroBreakState && doNotBlockDuringBreaks) return true;
            if (inputs.IsUserOnMeeting && !inputs.IsBlockDistractingSitesDuringMeetingsEnabled) return true;
            return false;
        }
    }
}
