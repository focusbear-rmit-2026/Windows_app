using System;
using System.Collections.Generic;
using System.Text;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// A specific, fixable reason blocking is not happening. Ported from the Mac's
    /// <c>BlockingOffReason</c> (Mac-App#1978): users who skipped the OS permission prompts ended up
    /// with blocking silently doing nothing, and the menu never said why.
    /// </summary>
    public enum BlockingOffReason
    {
        /// <summary>The whole app was deactivated — the "OFF" badge.</summary>
        AppDeactivated,

        /// <summary>A CONFIRMED OS permission issue. Other blocking mechanisms can still work.</summary>
        PermissionsMissing,

        /// <summary>No blocking schedule has ever been created.</summary>
        NoSchedule,

        /// <summary>A schedule exists but the user paused it.</summary>
        SchedulePaused,

        /// <summary>A schedule exists and isn't paused, but its window doesn't cover right now.</summary>
        NoScheduleRightNow,

        /// <summary>
        /// The OS grants are in place, but no browser has been set up, so websites can't be blocked.
        /// Kept apart from <see cref="PermissionsMissing"/>: the fix is the browser list, not the
        /// permissions wizard.
        /// </summary>
        NoBrowserEnabled,

        /// <summary>
        /// A browser is set up, but the "what is the active tab?" read has been failing, so no
        /// website can be checked at all (report 2026-09-11_16-56-33).
        /// </summary>
        BrowserNotResponding,
    }

    /// <summary>
    /// The identifiers and English copy for each reason.
    ///
    /// The Mac resolves the text through its config-driven <c>menuLocalized(key, fallback)</c>, which
    /// is not ported — the lookup is a Mac subsystem and each platform has its own. What IS ported is
    /// the mapping itself, so a reason added without copy is caught here rather than shipping a card
    /// with a blank sentence and a blank button.
    /// </summary>
    public static class BlockingOffReasons
    {
        /// <summary>Declaration order, which is also severity order. Used for the log and the tests.</summary>
        public static readonly IReadOnlyList<BlockingOffReason> All = new[]
        {
            BlockingOffReason.AppDeactivated,
            BlockingOffReason.PermissionsMissing,
            BlockingOffReason.NoSchedule,
            BlockingOffReason.SchedulePaused,
            BlockingOffReason.NoScheduleRightNow,
            BlockingOffReason.NoBrowserEnabled,
            BlockingOffReason.BrowserNotResponding,
        };

        /// <summary>The stable identifier, as it appears in the Mac's debug log line.</summary>
        public static string Identifier(BlockingOffReason reason)
        {
            switch (reason)
            {
                case BlockingOffReason.AppDeactivated: return "appDeactivated";
                case BlockingOffReason.PermissionsMissing: return "permissionsMissing";
                case BlockingOffReason.NoSchedule: return "noSchedule";
                case BlockingOffReason.SchedulePaused: return "schedulePaused";
                case BlockingOffReason.NoScheduleRightNow: return "noScheduleRightNow";
                case BlockingOffReason.NoBrowserEnabled: return "noBrowserEnabled";
                case BlockingOffReason.BrowserNotResponding: return "browserNotResponding";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        /// <summary>The sentence shown under the headline.</summary>
        public static string Explanation(BlockingOffReason reason)
        {
            switch (reason)
            {
                case BlockingOffReason.AppDeactivated:
                    return "Focus Bear is deactivated, so nothing is being blocked.";
                case BlockingOffReason.PermissionsMissing:
                    return "Some blocking features may be unavailable because a permission is missing. Other blocking can continue.";
                case BlockingOffReason.NoSchedule:
                    return "You haven't set up a blocking schedule yet.";
                case BlockingOffReason.SchedulePaused:
                    return "Your blocking schedule is paused.";
                case BlockingOffReason.NoScheduleRightNow:
                    return "No blocking schedule covers right now, and no focus session is running.";
                case BlockingOffReason.NoBrowserEnabled:
                    return "No browser is set up for blocking, so websites won't be blocked.";
                case BlockingOffReason.BrowserNotResponding:
                    return "Your browser has stopped telling Focus Bear which page is open, so websites aren't being checked. Quitting and reopening the browser usually fixes it.";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        /// <summary>The button that takes the user to where this reason is fixed.</summary>
        public static string FixTitle(BlockingOffReason reason)
        {
            switch (reason)
            {
                case BlockingOffReason.AppDeactivated: return "Reactivate Focus Bear";
                case BlockingOffReason.PermissionsMissing: return "Grant permissions";
                case BlockingOffReason.NoSchedule: return "Set up blocking";
                case BlockingOffReason.SchedulePaused: return "Resume blocking";
                case BlockingOffReason.NoScheduleRightNow: return "Edit blocking schedules";
                case BlockingOffReason.NoBrowserEnabled: return "Set up browsers";
                case BlockingOffReason.BrowserNotResponding: return "Check browser setup";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }

    /// <summary>
    /// Which headline the card gets. Permission and browser-setup gaps do NOT establish that all
    /// blocking has stopped — per-browser hooks and the extension are independent of them — so on
    /// their own they are a warning, not an "off".
    /// </summary>
    public enum BlockingStatusCardKind
    {
        Off,
        Attention,
    }

    public static class BlockingStatusCard
    {
        public static BlockingStatusCardKind KindFor(IEnumerable<BlockingOffReason> reasons)
        {
            if (reasons == null) return BlockingStatusCardKind.Attention;
            foreach (BlockingOffReason reason in reasons)
            {
                if (reason == BlockingOffReason.PermissionsMissing) continue;
                if (reason == BlockingOffReason.NoBrowserEnabled) continue;
                return BlockingStatusCardKind.Off;
            }
            return BlockingStatusCardKind.Attention;
        }
    }

    /// <summary>
    /// What is actually KNOWN about the OS grants blocking needs.
    ///
    /// A read-only permission query can be inconclusive, and old evidence expires. Neither case
    /// warrants claiming a permission is missing — which is the false positive report
    /// 2026-09-10_07-54-05 was about.
    /// </summary>
    public enum BlockingPermissionsState
    {
        /// <summary>A check confirmed the grants are there.</summary>
        Granted,

        /// <summary>The OS refused, or reports that consent is required. A setting cannot establish this.</summary>
        Missing,

        /// <summary>Not probed yet, or the probe was inconclusive. Never worth a claim either way.</summary>
        Unknown,
    }

    public enum BlockingPermissionIssue
    {
        Accessibility,
        SystemEvents,
    }

    /// <summary>
    /// Which capabilities a permission probe actually established something about. Only CONFIRMED
    /// denials are named: an unknown probe result leaves both the issue list and the overall state
    /// unknown, rather than being rounded down to "missing".
    /// </summary>
    public struct BlockingPermissionHealth
    {
        public BlockingPermissionHealth(bool accessibilityGranted, BlockingPermissionsState systemEvents)
        {
            AccessibilityGranted = accessibilityGranted;
            SystemEvents = systemEvents;
        }

        public bool AccessibilityGranted { get; set; }
        public BlockingPermissionsState SystemEvents { get; set; }

        public IReadOnlyList<BlockingPermissionIssue> Issues
        {
            get
            {
                var issues = new List<BlockingPermissionIssue>();
                if (!AccessibilityGranted) issues.Add(BlockingPermissionIssue.Accessibility);
                if (SystemEvents == BlockingPermissionsState.Missing) issues.Add(BlockingPermissionIssue.SystemEvents);
                return issues;
            }
        }

        public BlockingPermissionsState State
        {
            get
            {
                if (Issues.Count > 0) return BlockingPermissionsState.Missing;
                return SystemEvents;
            }
        }
    }

    /// <summary>
    /// One in-flight permission check, with expiry measured from when the observation BEGAN.
    ///
    /// Measuring from the start rather than the completion is the point: a slow probe must not give
    /// stale evidence a fresh lifetime, and a late duplicate completion must not overwrite a newer
    /// result. Times are seconds from any monotonic source the caller owns.
    /// </summary>
    public struct BlockingPermissionProbeCache
    {
        /// <summary>How soon another probe may start after the last one was sampled.</summary>
        public const double RefreshIntervalSeconds = 10;

        /// <summary>How long a sample counts as evidence, measured from when it STARTED.</summary>
        public const double ValidityIntervalSeconds = 30;

        private double? _inFlightAt;
        private double? _sampledAt;
        private BlockingPermissionsState _sample;

        /// <summary>Whether a new probe may start now. False while one is in flight, or too soon after the last.</summary>
        public bool Begin(double now)
        {
            if (_inFlightAt.HasValue) return false;
            if (_sampledAt.HasValue && now >= _sampledAt.Value
                && now - _sampledAt.Value < RefreshIntervalSeconds) return false;
            _inFlightAt = now;
            return true;
        }

        /// <summary>
        /// Records a probe's result. Ignored unless it belongs to the probe currently in flight, so a
        /// duplicate or late completion of an older probe cannot overwrite a newer answer.
        /// </summary>
        public void Complete(BlockingPermissionsState state, double startedAt)
        {
            if (_inFlightAt != startedAt) return;
            _inFlightAt = null;
            _sampledAt = startedAt;
            _sample = state;
        }

        /// <summary>The sampled state, or unknown once the evidence is older than its validity window.</summary>
        public BlockingPermissionsState State(double now)
        {
            if (!_sampledAt.HasValue) return BlockingPermissionsState.Unknown;
            if (now < _sampledAt.Value) return BlockingPermissionsState.Unknown;
            if (now - _sampledAt.Value > ValidityIntervalSeconds) return BlockingPermissionsState.Unknown;
            return _sample;
        }
    }

    /// <summary>
    /// Everything the reasons depend on, passed in rather than read from the app's globals so the
    /// answer can be unit-tested. Ported from the Mac's <c>BlockingStatus</c>.
    /// </summary>
    public sealed class BlockingStatus
    {
        public bool IsAppEnabled { get; set; }
        public BlockingPermissionsState Permissions { get; set; }

        /// <summary>
        /// Whether any browser has been set up for blocking. TRUE when we can't tell — an unread
        /// list is not evidence against the user.
        /// </summary>
        public bool IsAnyBrowserEnabled { get; set; }

        public int ScheduleCount { get; set; }
        public bool IsAnyScheduleOn { get; set; }
        public bool IsAnySchedulePaused { get; set; }

        /// <summary>
        /// Whether some schedule's window covers this moment — a different question from
        /// <see cref="IsAnyScheduleOn"/>, which is whether one has actually been switched on.
        /// </summary>
        public bool IsAnyScheduleCoveringNow { get; set; }

        public bool IsFocusSessionRunning { get; set; }

        /// <summary>
        /// Whether a routine is running. Routines block on their own AND suppress schedule
        /// activation, so while one runs <see cref="IsAnyScheduleOn"/> is false whatever the
        /// schedules say.
        /// </summary>
        public bool IsRoutineRunning { get; set; }

        /// <summary>Whether the active-tab reads every website check depends on have stopped answering.</summary>
        public bool IsBrowserUrlReadFailing { get; set; }

        /// <summary>
        /// Known blocking issues, most severe first.
        ///
        /// A reason is listed only when the inputs actually ESTABLISH it. Anything undetermined — an
        /// unfinished permission probe, a schedule that covers now but isn't on — is left unsaid:
        /// silence is recoverable, but sending someone to fix a thing that isn't broken is not.
        /// </summary>
        public IReadOnlyList<BlockingOffReason> OffReasons
        {
            get
            {
                // A deactivated app is the whole story — the schedule gaps below are its consequence,
                // and listing them too would bury the one thing the user has to undo.
                if (!IsAppEnabled) return new[] { BlockingOffReason.AppDeactivated };

                var reasons = new List<BlockingOffReason>();

                // Missing grants limit some features even mid-session, so the warning is retained.
                // Unknown says nothing: the probe is async and the menu renders before it lands.
                if (Permissions == BlockingPermissionsState.Missing)
                {
                    reasons.Add(BlockingOffReason.PermissionsMissing);
                }

                // Granted permissions with no browser set up still means no website blocking — but
                // that is a gap in the browser list, so it gets its own reason and its own fix.
                if (Permissions != BlockingPermissionsState.Missing && !IsAnyBrowserEnabled)
                {
                    reasons.Add(BlockingOffReason.NoBrowserEnabled);
                }

                // A browser that has stopped answering is a live failure of the thing the user set
                // up, so it is reported whether or not a schedule is on.
                if (IsAnyBrowserEnabled && IsBrowserUrlReadFailing)
                {
                    reasons.Add(BlockingOffReason.BrowserNotResponding);
                }

                if (!IsFocusSessionRunning && !IsRoutineRunning && !IsAnyScheduleOn)
                {
                    if (ScheduleCount == 0) reasons.Add(BlockingOffReason.NoSchedule);
                    else if (IsAnySchedulePaused) reasons.Add(BlockingOffReason.SchedulePaused);
                    else if (!IsAnyScheduleCoveringNow) reasons.Add(BlockingOffReason.NoScheduleRightNow);
                    // A schedule covers right now yet none is on: real, but the cause can't be named
                    // from here, and "no schedule covers right now" would be a plain falsehood.
                }

                return reasons;
            }
        }

        /// <summary>
        /// One line naming the inputs and the conclusion, so the next report of "it says blocking is
        /// off but it isn't" is answered by reading a log instead of the source.
        ///
        /// Rendered with Swift's spellings — lower-case booleans and state names — so a Windows log
        /// line and a Mac one can be compared side by side without translating either.
        /// </summary>
        public string LogDescription
        {
            get
            {
                var identifiers = new List<string>();
                foreach (BlockingOffReason reason in OffReasons) identifiers.Add(BlockingOffReasons.Identifier(reason));
                string conclusion = identifiers.Count == 0
                    ? "no confirmed blocking issue"
                    : string.Join(",", identifiers.ToArray());

                var line = new StringBuilder();
                line.Append("appEnabled=").Append(Lower(IsAppEnabled));
                line.Append(" permissions=").Append(Lower(Permissions));
                line.Append(" browserEnabled=").Append(Lower(IsAnyBrowserEnabled));
                line.Append(" schedules=").Append(ScheduleCount);
                line.Append(" scheduleOn=").Append(Lower(IsAnyScheduleOn));
                line.Append(" schedulePaused=").Append(Lower(IsAnySchedulePaused));
                line.Append(" scheduleCoversNow=").Append(Lower(IsAnyScheduleCoveringNow));
                line.Append(" focusSession=").Append(Lower(IsFocusSessionRunning));
                line.Append(" routine=").Append(Lower(IsRoutineRunning));
                line.Append(" urlReadFailing=").Append(Lower(IsBrowserUrlReadFailing));
                line.Append(" → ").Append(conclusion);
                return line.ToString();
            }
        }

        private static string Lower(bool value) { return value ? "true" : "false"; }

        private static string Lower(BlockingPermissionsState state)
        {
            switch (state)
            {
                case BlockingPermissionsState.Granted: return "granted";
                case BlockingPermissionsState.Missing: return "missing";
                default: return "unknown";
            }
        }
    }
}
