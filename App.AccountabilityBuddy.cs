using Focus_Bear.Habits;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Models;
using Focus_Bear.Pomodoro;
using Focus_Bear.Utils;
using FocusBear.Core.Postpone;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear
{
    /// <summary>
    /// The AI accountability buddy for postponing habits (the Mac's easy-postpone → buddy chat). With the
    /// setting on, postponing a morning or evening routine opens the buddy instead of the minutes picker —
    /// after the postpone password, which is unchanged. An approval postpones the routine for the approved
    /// length and starts a focus session scoped to the stated goal; closing the chat keeps the routine going.
    /// </summary>
    public partial class App
    {
        internal bool ShouldUseAccountabilityBuddy() =>
            appSettings.bAccountabilityBuddyEnabled
            && (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS);

        /// <summary>The Focus Summary's "I still need to work more" postpone gets the buddy too (Mac parity).</summary>
        internal bool ShouldUseAccountabilityBuddyForFocusSummary() => appSettings.bAccountabilityBuddyEnabled;

        internal void ShowAccountabilityBuddy() => ShowAccountabilityBuddy(minutes => PostponeHabitWork(minutes), EasyPostponeCancelled, wndFocusBearMainWindow);

        /// <summary>
        /// The chat, then <paramref name="postpone"/> for an approval or a completed hold (an approval also
        /// starts the scoped session), or <paramref name="cancelled"/> when the user goes back to the routine.
        /// </summary>
        internal void ShowAccountabilityBuddy(Action<int> postpone, Action cancelled, FocusBearMainWindow owner)
        {
            LogWriter.LogWrite("ShowAccountabilityBuddy()");
            var routine = appSettings.nCurrentActivityType;
            var habits = BuddyUncompletedHabits();
            var tasks = BuddyDayPlanTasks();
            var window = new AccountabilityBuddyWindow(messages =>
                new API.FocusBearAPIClient().EvaluateFocusPostponement(AccountabilityRequest.Build(messages, habits, tasks, null, "English")))
            { Owner = owner };
            owner.GridShade.Visibility = Visibility.Visible;
            window.ShowDialog();
            owner.GridShade.Visibility = Visibility.Collapsed;

            // The routine moved on while the chat was open (finished, skipped, switched): an approval is stale.
            if (appSettings.nCurrentActivityType != routine && window.Result != AccountabilityBuddyWindow.Outcome.Cancelled)
            {
                LogWriter.LogWrite("accountability buddy: stale result ignored; presented=" + routine + ", current=" + appSettings.nCurrentActivityType);
                return;
            }
            switch (window.Result)
            {
                case AccountabilityBuddyWindow.Outcome.Approved:
                    postpone(window.Minutes);
                    StartAccountabilityBuddySession(window.ApprovedVerdict, window.Goal, window.Minutes);
                    break;
                case AccountabilityBuddyWindow.Outcome.HoldPostpone:
                    // The recovery path: the routine is pushed back, and no scoped session starts.
                    postpone(window.Minutes);
                    break;
                default:
                    cancelled?.Invoke();
                    break;
            }
            if ((appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState) && !appSettings.isFocusModeRunning && appSettings.AutoCloseAfterRoutineSkipPostpone)
                owner.Hide();
        }

        /// <summary>
        /// A focus session on a temporary allow-list mode built from the approval: the apps and sites the goal
        /// needs are allowed, everything else goes through the AI check against the goal (or is blocked when AI
        /// checking is off). The mode is hidden from the block-list pickers and replaced by the next one.
        /// </summary>
        private void StartAccountabilityBuddySession(AccountabilityVerdict verdict, string goal, int minutes)
        {
            try
            {
                var apps = new ObservableCollection<AppDetails>(verdict.AllowedApps.Select(name => new AppDetails
                {
                    AppName = name,
                    ExeNames = new List<string> { name.Replace(" ", "") + ".exe", name + ".exe" }.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                }));
                var mode = new FocusMode(PostponeFocusSession.ModeIdPrefix + Guid.NewGuid().ToString("N"), goal.Length > 0 ? goal : "Accountability buddy",
                    new ObservableCollection<string>(verdict.AllowedUrls), new ObservableCollection<string>(), apps, "", false,
                    false /*isAllowedAllApps*/, true /*isAllowedListApproach*/, false /*isDefault*/, false /*isMeetings*/,
                    true /*isLockedFocus: kept out of the pickers*/, false, false, false);
                foreach (var stale in appSettings.arrFocusModes.Where(m => m.id != null && m.id.StartsWith(PostponeFocusSession.ModeIdPrefix)).ToList())
                    appSettings.arrFocusModes.Remove(stale);
                appSettings.arrFocusModes.Add(mode);

                appSettings.sRunningFocusModeIntention = goal;
                FocusButton.SendFocusGoal(goal);
                FocusSessionStarter.Start(mode, minutes, FocusSessionStarter.BreakFromRatio(minutes), "", personalSession: true);
                appSettings.CapturePostHog("accountability-buddy-approved-postpone", string.Format("\"duration_minutes\":\"{0}\",\"allowed_apps\":\"{1}\",\"allowed_urls\":\"{2}\"",
                    minutes, verdict.AllowedApps.Count, verdict.AllowedUrls.Count));
            }
            catch (Exception ex) { LogWriter.LogWrite("StartAccountabilityBuddySession ex - " + ex.Message); }
        }

        // ------------------------------------------------------------------ the super-strict postpone session

        /// <summary>The running session, if the app started it: a buddy-approved postpone or a Late No More meeting.</summary>
        private AutoStartedSessionKind RunningAutoStartedSession =>
            PostponeFocusSession.Classify(appSettings.isFocusModeRunning, appSettings.sRunningFocusModeID, appSettings.sRunningFocusModeIntention);

        private bool RoutineIsPostponed => appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState;

        /// <summary>
        /// Hook for focus screens: true while Stop / End Session means "back to my habits" — a buddy-approved
        /// postpone session, or a meeting session, while the routine it covers is postponed (see
        /// <see cref="HandlePostponeSessionControl"/>). Such a session also can't be minimised out of the way.
        /// </summary>
        internal bool IsPostponeLockedFocusSession => PostponeFocusSession.StopResumesRoutine(RunningAutoStartedSession, RoutineIsPostponed);

        /// <summary>Hook for focus screens: true while the running session can't be paused (hide Pause).</summary>
        internal bool IsUnpausableFocusSession => PostponeFocusSession.IsUnpausable(RunningAutoStartedSession);

        /// <summary>
        /// Whether the running session stands between the user and a routine (the Mac's <c>protectsRoutine</c> in
        /// <c>cutoffLimitedMinutes</c>): a postponed routine, or the buddy's postpone session. Adding time to it is
        /// limited by the cutoff.
        /// </summary>
        internal bool SessionProtectsRoutine => RoutineIsPostponed || RunningAutoStartedSession == AutoStartedSessionKind.BuddyPostpone;

        /// <summary>The Mac's <c>showCutoffPostponeRefusedAlert(forExtension: true)</c>, with its config keys.</summary>
        internal void ShowCutoffExtensionRefused()
        {
            try
            {
                string title = appSettings.GetConfigStringSafe("Your cutoff limits this extension", "activityPostponeInfo", "cutoffExtensionRefusedTitle");
                string message = appSettings.GetConfigStringSafe("This focus session cannot be extended past <TIME>. There is no room for another full minute.",
                    "activityPostponeInfo", "cutoffExtensionRefusedText").Replace("<TIME>", appSettings.sDayCutOffTime ?? "");
                new FBMessageBox(message, title) { Topmost = true }.ShowDialog();
            }
            catch (Exception ex) { LogWriter.LogWrite("ShowCutoffExtensionRefused ex - " + ex.Message); }
        }

        /// <summary>Hook for focus screens: true while the running auto-started session is super strict (no -5, goal locked).</summary>
        internal bool IsSuperStrictAutoStartedSession => PostponeFocusSession.RunsSuperStrict(RunningAutoStartedSession, RoutineIsPostponed);

        /// <summary>
        /// The one gate every pause, stop and remote finish goes through (TrayMenu.ToggleFocusPauseFromUI,
        /// TrayMenu.StopFocusSessionRealAsync, PusherHandler's focus-mode-finished). Returns true when it handled the
        /// request, and the caller must then do nothing more: a pause (or a remote finish of the buddy session) is
        /// refused and the full focus screen is brought back; a stop while the routine is postponed ends the session
        /// quietly and resumes the routine full screen.
        /// </summary>
        internal bool HandlePostponeSessionControl(FocusSessionControl control)
        {
            try
            {
                var kind = RunningAutoStartedSession;
                var response = PostponeFocusSession.Decide(control, kind, appSettings.isFocusModePaused, RoutineIsPostponed);
                if (response == PostponeSessionResponse.Proceed) return false;
                LogWriter.LogWrite("[user-action] " + kind + " session: " + control + " -> " + response);
                if (response == PostponeSessionResponse.KeepRunning)
                {
                    if (control != FocusSessionControl.RemoteFinish) ShowToDoPlayer();   // the full focus screen, again
                    return true;
                }
                EndPostponeSessionAndResumeRoutine();
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HandlePostponeSessionControl ex - " + ex.Message);
                return control == FocusSessionControl.Pause || control == FocusSessionControl.RemoteFinish;   // never loosen on error
            }
        }

        /// <summary>
        /// Stop, for a postpone session: a silent finish (no completion screen, no password) and the routine the user
        /// postponed comes back now instead of when the postpone timer runs out (the Mac's
        /// <c>stopFocusModeAndResumeRoutine</c>).
        /// </summary>
        private async void EndPostponeSessionAndResumeRoutine() => await EndAutoStartedSessionQuietly(resumeRoutine: true);

        /// <summary>
        /// A silent finish of the running auto-started session (no completion screen, no password); with
        /// <paramref name="resumeRoutine"/> the postponed routine comes back now.
        /// </summary>
        private async Task EndAutoStartedSessionQuietly(bool resumeRoutine)
        {
            try
            {
                wndTrayMenu?.Hide();
                foreach (var mode in appSettings.arrFocusModes.Where(m => m.isOn).ToList()) mode.isOn = false;
                if (appSettings.isFocusModePaused)
                {
                    appSettings.isFocusModePaused = false;
                    appSettings.nPausedFocusRemainingSeconds = 0;
                }
                if (wndTrayMenu != null) wndTrayMenu.OnUntill.Content = "";
                FinishFocusModeCommonTasks();
                // It is async void (it syncs the player's tasks first); let it finish before the routine takes over.
                for (int i = 0; i < 50 && appSettings.isFocusModeRunning; i++) await Task.Delay(100);

                if (!resumeRoutine) return;
                // Both resume paths only drop the activity back to NONE; the background loop then shows the routine
                // full screen on its next tick.
                if (appSettings.bFocusSummaryPostponedState && wndFocusBearMainWindow?.PageFocusSummary != null)
                    wndFocusBearMainWindow.PageFocusSummary.ResumeFocusSummary();
                else
                    ResumeAfterPostponeFinished();
            }
            catch (Exception ex) { LogWriter.LogWrite("EndPostponeSessionAndResumeRoutine ex - " + ex.Message); }
        }

        /// <summary>The routine the user is actually in, from the current habit on.</summary>
        private List<string> BuddyUncompletedHabits()
        {
            try
            {
                var source = appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                int from = Math.Max(0, appSettings.nHabitIndex);
                return source.Skip(from).Select(h => AccountabilityRequest.Line(string.IsNullOrWhiteSpace(h.nameWithoutIcon) ? h.name : h.nameWithoutIcon, h.durationSeconds / 60)).ToList();
            }
            catch (Exception ex) { LogWriter.LogWrite("BuddyUncompletedHabits ex - " + ex.Message); return new List<string>(); }
        }

        /// <summary>Today's not-yet-done Day Plan tasks, so the buddy can tell whether the goal is already planned.</summary>
        private static List<string> BuddyDayPlanTasks()
        {
            try { return DayPlanTaskStore.Shared.Tasks(DateTime.Today).Where(t => !t.Done).Select(t => AccountabilityRequest.Line(t.Title, t.DurationMinutes, t.Project)).ToList(); }
            catch (Exception ex) { LogWriter.LogWrite("BuddyDayPlanTasks ex - " + ex.Message); return new List<string>(); }
        }
    }
}
