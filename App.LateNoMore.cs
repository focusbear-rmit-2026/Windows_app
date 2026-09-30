using Focus_Bear.Pomodoro;
using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using FocusBear.Core.Postpone;
using System;
using System.Linq;

namespace Focus_Bear
{
    /// <summary>
    /// The focus session Late No More starts for a meeting — the Mac's <c>startMeetingFocusSession</c>: the meeting's
    /// remaining time (1..240 min), on the Meetings block list (its allow-list pre-approves Teams / Zoom / Meet …),
    /// with the intention "attend and stay focused in your meeting: {title}". That intention is what marks the session
    /// as a Late No More one (<see cref="PostponeFocusSession.IsMeetingIntention"/>): the pause / stop rules
    /// (<see cref="IsUnpausableFocusSession"/>, <see cref="HandlePostponeSessionControl"/>) key off it.
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Starts the meeting session unless one is already running. With <paramref name="postponeRoutine"/>, a running
        /// morning / evening routine is postponed until the meeting ends first (the Mac's appointment popup does the same),
        /// so Stop hands the user back to it. Past the cut-off nothing happens (the Mac's cut-off lock: a meeting must not
        /// be a way out of the evening routine). Returns true when a session started.
        /// </summary>
        internal bool StartLateNoMoreMeetingSession(LateNoMoreEvent meeting, bool postponeRoutine)
        {
            try
            {
                if (meeting == null) return false;
                if (appSettings.isFocusModeRunning)
                {
                    // Never stack a second meeting session, and never take over a session the user started themselves.
                    LogWriter.LogWrite("LateNoMore session: a focus session is already running (" +
                        (PostponeFocusSession.IsMeetingIntention(appSettings.sRunningFocusModeIntention) ? "a meeting session" : "not a meeting") + ") - not starting another");
                    return false;
                }
                DateTime now = DateTime.Now;
                int minutes = LateNoMoreSession.Minutes(meeting.End, now);
                if (minutes <= 0) { LogWriter.LogWrite("LateNoMore session: the meeting is over"); return false; }
                if (appSettings.DidCutOffTimePassed())
                {
                    LogWriter.LogWrite("LateNoMore session: past cut-off - not postponing the routine or starting a session (cut-off lock)");
                    return false;
                }

                bool inRoutine = appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS
                                 || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS;
                if (postponeRoutine && inRoutine)
                {
                    LogWriter.LogWrite("LateNoMore session: postponing the routine for the meeting (" + minutes + " min)");
                    PostponeHabitWork(minutes);
                }

                // The Meetings block list, as on the Mac; else the default mode. Both are real server modes (never a
                // local-only id), so the backend start is accepted.
                FocusMode mode = appSettings.arrFocusModes.FirstOrDefault(m => m.isMeetings && !PostponeFocusSession.IsPostponeMode(m.id))
                                 ?? appSettings.arrFocusModes.FirstOrDefault(m => m.isDefault && !PostponeFocusSession.IsPostponeMode(m.id));
                if (mode == null) { LogWriter.LogWrite("LateNoMore session: no Meetings or default focus mode"); return false; }

                string goal = LateNoMoreSession.Goal(meeting.Title);
                appSettings.sRunningFocusModeIntention = goal;
                FocusButton.SendFocusGoal(goal);
                FocusSessionStarter.Start(mode, minutes, FocusSessionStarter.BreakFromRatio(minutes), "", personalSession: true);
                appSettings.CapturePostHog("meeting-ai-focus-session-started", "\"duration_minutes\":\"" + minutes + "\",\"source\":\"late-no-more\"");
                LogWriter.LogWrite("LateNoMore session: started " + minutes + " min on '" + mode.name + "' (routine " + (postponeRoutine && inRoutine ? "postponed" : "untouched") + ")");
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartLateNoMoreMeetingSession ex - " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// "I don't need to attend" (the Mac's <c>btnBackToHabitsClicked</c>): the session the alert started for this
        /// meeting ends quietly, and a routine postponed for it comes back. Any other session is left alone.
        /// </summary>
        internal void EndLateNoMoreMeetingSession(LateNoMoreEvent meeting)
        {
            try
            {
                if (meeting == null || RunningAutoStartedSession != AutoStartedSessionKind.Meeting) return;
                if (!LateNoMoreSession.IsSessionFor(appSettings.sRunningFocusModeIntention, meeting.Title)) return;
                if (Focus.TeacherSessionManager.Instance.IsTeacherSessionActive()) return;
                LogWriter.LogWrite("LateNoMore session: not attending - ending the meeting's session" + (RoutineIsPostponed ? " and resuming the routine" : ""));
                _ = EndAutoStartedSessionQuietly(resumeRoutine: RoutineIsPostponed);
            }
            catch (Exception ex) { LogWriter.LogWrite("EndLateNoMoreMeetingSession ex - " + ex.Message); }
        }
    }
}
