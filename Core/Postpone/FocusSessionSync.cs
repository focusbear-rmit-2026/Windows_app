namespace FocusBear.Core.Postpone
{
    /// <summary>
    /// How a buddy-approved postpone session reaches other devices, and what this device does with sessions they
    /// start. Report 2026-09-26 ("I postponed but it let me stop"): the server rejected the buddy session's
    /// temporary focus mode id, so the account showed no running session; the Mac's Late No More then started a
    /// meeting session, and this device let that remote start replace the buddy session, which was no longer
    /// recognised and so could be paused and stopped.
    /// </summary>
    /// <summary>
    /// Whether the account has a personal focus session running that this device started or adopted, so the
    /// end screen knows whether there is one to finish. A teacher's class session is not one (it is started and
    /// ended through class-management), and neither is a session already finished from another device: finishing
    /// either sent <c>POST /focus-mode/{id}/finish</c> for a stale id and got "Focus mode with id: … is not
    /// current, the current one is null!" (26 Sep, 12:03:58 and 12:10:49).
    /// </summary>
    public sealed class ServerFocusSession
    {
        private readonly object _lock = new object();
        private bool _live;

        /// <summary>The backend accepted <c>POST /focus-mode/{id}/start</c>, or a running one was adopted.</summary>
        public void PersonalStarted() { lock (_lock) _live = true; }

        /// <summary>A class session started instead: there is no personal session on the server.</summary>
        public void ClassSessionStarted() { lock (_lock) _live = false; }

        /// <summary>Finished here or on another device.</summary>
        public void Finished() { lock (_lock) _live = false; }

        public bool HasSessionToFinish { get { lock (_lock) return _live; } }
    }

    public static class FocusSessionSync
    {
        /// <summary>
        /// Whether a focus-mode-started event from another device is ignored. It never replaces a session that is
        /// already running here, as on the Mac: that is how a meeting session from another computer took over a
        /// buddy-approved postpone session and made it stoppable. With no session running, the old rule stands:
        /// a routine in progress ignores it, a postponed one doesn't.
        /// </summary>
        public static bool IgnoresRemoteStart(bool isFocusModeRunning, bool isBusyWithRoutine, bool routineIsPostponed) =>
            isFocusModeRunning || (isBusyWithRoutine && !routineIsPostponed);

        /// <summary>
        /// The id the backend knows the session by. The buddy's temporary mode exists only on this device, and the
        /// server rejects it ("focus_mode_id must be a UUID"), which left the account with no running session for
        /// other devices to see; like the Mac, the session is reported against the user's default focus mode.
        /// </summary>
        public static string BackendFocusModeId(string localFocusModeId, string defaultFocusModeId) =>
            PostponeFocusSession.IsPostponeMode(localFocusModeId) && !string.IsNullOrEmpty(defaultFocusModeId) ? defaultFocusModeId : localFocusModeId;
    }
}
