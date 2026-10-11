using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using FocusBear.Core.Blocking;
using Newtonsoft.Json.Linq;

// FocusBear.Core.Blocking has a CustomBlockingSchedule of its own — the ported one, with no
// WPF in it. This file is the seam between the two, so it is the one place the names collide.
using WpfBlockingSchedule = Focus_Bear.Models.CustomBlockingSchedule;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The single answer to "should AI relevance checking run right now?".
    /// <para>
    /// The rule is <see cref="AiBlockingDecision"/> in FocusBear.Core, where it is tested. This file
    /// only gathers the inputs, because they live on WPF singletons that cannot follow it there.
    /// </para>
    /// <para>
    /// Before this, eleven call sites each answered the question themselves with
    /// <c>schedule.bAIBlockingEnabled</c> — which is one rung of a five-rung ladder, and not the
    /// first one. Call <see cref="IsEnabled"/> instead of reading that field.
    /// </para>
    /// </summary>
    public static class AiBlocking
    {
        /// <summary>The key the server carries a block list's AI switch under, inside <c>metadata</c>.</summary>
        private const string IsAiEnabledKey = "is_ai_enabled";

        public static bool IsEnabled()
        {
            return AiBlockingDecision.IsAiBlockingEnabled(ToInputs(Snapshot()));
        }

        /// <summary>
        /// The WPF state the decision reads, captured once. It exists so the mapping below can be
        /// tested without the <see cref="AppSettings"/> singleton, whose constructor touches the
        /// disk, the network adapters and the log.
        /// </summary>
        internal sealed class AppState
        {
            public bool TeacherSessionActive { get; set; }
            public bool TeacherAiEnabledForAllowList { get; set; }
            public bool FocusModeRunning { get; set; }
            public bool FocusModePaused { get; set; }
            public bool HabitWithAllowedAppsRunning { get; set; }
            public bool GlobalAiCheckUrlEnabled { get; set; }
            public IEnumerable<FocusMode> FocusModes { get; set; }

            /// <summary>The schedule that is on and not paused, or null when none is.</summary>
            public WpfBlockingSchedule ActiveSchedule { get; set; }
        }

        private static AppState Snapshot()
        {
            AppSettings settings = AppSettings.GetInstance;
            WpfBlockingSchedule activeSchedule;
            bool scheduleOn = settings.IsBlockingScheduleActiveAndNotPaused(out activeSchedule);

            return new AppState
            {
                TeacherSessionActive = TeacherSessionManager.Instance.IsTeacherSessionActive(),
                TeacherAiEnabledForAllowList = TeacherSessionManager.Instance.IsTeacherAiBlockingEnabled(),
                FocusModeRunning = settings.isFocusModeRunning,
                FocusModePaused = settings.isFocusModePaused,
                HabitWithAllowedAppsRunning = settings.bHabitwithAllowedAppsRunning,
                GlobalAiCheckUrlEnabled = settings.bAiCheckUrlEnabled,
                FocusModes = settings.arrFocusModes,
                ActiveSchedule = scheduleOn ? activeSchedule : null,
            };
        }

        internal static AiBlockingInputs ToInputs(AppState state)
        {
            return new AiBlockingInputs
            {
                // Two rungs the Mac has and Windows does not: the blocking-popup snooze
                // (FeatureSnooze) and accountability-buddy sessions. Both are hard-coded to their
                // inactive value rather than dropped from the rule, so that porting either feature
                // is a change here and not a change to the shared decision.
                BlockingPopupsSnoozed = false,
                AccountabilityBuddySessionActive = false,

                TeacherSessionActive = state.TeacherSessionActive,
                TeacherAiEnabledForAllowList = state.TeacherAiEnabledForAllowList,

                // Mirrors the context test ProcessBrowserURLs already uses: a paused focus mode is
                // not a running one.
                FocusModeRunning = state.FocusModeRunning && !state.FocusModePaused,
                AllowedFocusModeActivityRunning = state.HabitWithAllowedAppsRunning,

                AnyCustomScheduleOn = state.ActiveSchedule != null,
                RunningFocusModeAiEnabled = ReadAiEnabled(state.FocusModes?.FirstOrDefault(mode => mode.isOn)),
                ActiveSchedule = ToScheduleContext(state.FocusModes, state.ActiveSchedule),

                GlobalAiCheckUrlEnabled = state.GlobalAiCheckUrlEnabled,
            };
        }

        private static AiScheduleContext ToScheduleContext(IEnumerable<FocusMode> focusModes, WpfBlockingSchedule schedule)
        {
            if (schedule == null) return null;

            FocusMode blockList = focusModes?.FirstOrDefault(mode => mode.id == schedule.SelectedFocusModeId);

            return new AiScheduleContext
            {
                // Null when the mode was deleted or has not synced yet, which is exactly when the
                // schedule's own flag is allowed to answer.
                BlockList = blockList == null ? null : new ScheduleBlockList
                {
                    AiEnabled = ReadAiEnabled(blockList),
                    IsAllowedListApproach = blockList.isAllowedListApproach,
                },
                OwnAiFlag = schedule.bAIBlockingEnabled,
            };
        }

        /// <summary>
        /// Reads <c>is_ai_enabled</c> out of the metadata the server last sent. Null means the key
        /// was absent, which the rule reads as on — distinct from an explicit false, which vetoes.
        /// Windows never parses this into a field of its own, so the raw blob is the only source.
        /// </summary>
        private static bool? ReadAiEnabled(FocusMode mode)
        {
            JToken value = mode?.serverMetadata?[IsAiEnabledKey];
            if (value == null || value.Type != JTokenType.Boolean) return null;
            return value.Value<bool>();
        }
    }
}
