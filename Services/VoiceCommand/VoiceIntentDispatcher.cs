using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Focus_Bear.API;
using Focus_Bear.Utils;

namespace Focus_Bear.Services.VoiceCommand
{
    /// <summary>
    /// Result of dispatching a parsed voice command. Callers use this to drive any UI
    /// (e.g. show a confirmation) without the dispatcher itself owning the presentation.
    /// </summary>
    public class VoiceDispatchResult
    {
        public string Action;       // the classified action (create_task, start_focus, ...)
        public bool Success;        // true when the action was actually carried out
        public string Message;      // human-readable result suitable for display
        public string TaskTitle;    // populated for create_task
        // True when Focus Bear understood the command (mapped it to a known action), even if the action
        // was a no-op (e.g. "start focus" while already running). Only false when the command wasn't
        // understood — the UI keeps the popup open to retry in that case, and dismisses otherwise.
        public bool Recognized = true;
    }

    /// <summary>
    /// Executes a parsed voice-command intent against the app. Shared by both the Focus Button
    /// hardware trigger and the in-app menu microphone so the two behave identically.
    /// The dispatcher performs the app-level effect only; it never shows UI of its own.
    /// </summary>
    public static class VoiceIntentDispatcher
    {
        /// <param name="fromPhysicalDevice">
        /// True when triggered by the Focus Button hardware (so pause/resume is not echoed back to
        /// the device). False for the in-app menu microphone (the device display is then updated).
        /// </param>
        public static async Task<VoiceDispatchResult> DispatchAsync(IntentResponse result, bool fromPhysicalDevice = false)
        {
            var res = new VoiceDispatchResult { Action = result?.action };
            AppSettings appSettings = AppSettings.GetInstance;
            App app = (App)Application.Current;

            if (result == null || string.IsNullOrEmpty(result.action))
            {
                res.Recognized = false;
                res.Message = "Sorry, I didn't catch that.";
                return res;
            }

            switch (result.action)
            {
                case "create_task":
                    {
                        string task = result.parameters?.task;
                        string time = result.parameters?.time;

                        if (!string.IsNullOrWhiteSpace(task))
                        {
                            // Resolve the spoken time to a UTC due date. When no usable time was
                            // given, default to tomorrow (mirrors the Mac app's fallback).
                            DateTime dueUtc = ParseVoiceDateTime(time)
                                              ?? DateTime.Now.AddDays(1).ToUniversalTime();

                            FocusBearAPIClient client = new FocusBearAPIClient();
                            int m = await client.AddTask(task, dueUtc);
                            if (m == 1)
                            {
                                res.Success = true;
                                res.TaskTitle = task;
                                res.Message = "\"" + task + "\" task created successfully.";
                            }
                            else
                            {
                                res.Message = "Sorry, I couldn't create that task.";
                            }
                        }
                        break;
                    }

                case "start_focus":
                    {
                        int duration = result.parameters?.duration_minutes ?? 25;
                        if (duration <= 0) duration = 25;
                        string goal = result.parameters?.goal;

                        if (appSettings.isFocusModeRunning)
                        {
                            // Already running — nothing to start.
                            res.Message = "A focus session is already running.";
                            break;
                        }

                        if (!string.IsNullOrEmpty(goal))
                        {
                            appSettings.sRunningFocusModeIntention = goal;
                        }

                        app.StartWorkFocusMode(appSettings.sMostRecentFocusmodeDoneID, duration * 60, fromPhysicalDevice);
                        res.Success = true;
                        res.Message = "Started a " + duration + " minute focus session.";
                        break;
                    }

                case "pause_focus":
                    {
                        if (appSettings.isFocusModeRunning)
                        {
                            if (appSettings.isFocusModePaused)
                            {
                                // Already paused (possibly from the device) — leave as-is.
                                res.Message = "Focus session is already paused.";
                                break;
                            }
                            app.wndTrayMenu.ToggleFocusPauseFromUI(fromPhysicalDevice);
                        }
                        res.Success = true;
                        res.Message = "Focus session paused.";
                        break;
                    }

                case "resume_focus":
                    {
                        if (appSettings.isFocusModeRunning && appSettings.isFocusModePaused)
                        {
                            app.wndTrayMenu.ToggleFocusPauseFromUI(fromPhysicalDevice);
                            res.Success = true;
                            res.Message = "Focus session resumed.";
                        }
                        break;
                    }

                case "stop_focus":
                    {
                        if (appSettings.isFocusModeRunning)
                        {
                            app.StopFocusSessionFromEvents();
                            res.Success = true;
                            res.Message = "Focus session stopped.";
                        }
                        break;
                    }

                case "start_habit":
                    {
                        string habit = result.parameters?.habit_name;

                        if (!string.IsNullOrWhiteSpace(habit))
                        {
                            var pageHabits = app.wndFocusBearMainWindow.PageHabits;
                            if (pageHabits.PageHabitActivityOnGoing?.bTimerRunning == true)
                            {
                                // A habit is already ongoing — ignore.
                                res.Message = "A habit is already in progress.";
                                break;
                            }

                            if ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                                 appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) &&
                                appSettings.bHabitsPostponedState)
                            {
                                // Habits are postponed — resume them first, then start the habit.
                                App.FocusButton?.SendIdle();
                                pageHabits.ResumeHabitsBeforeStartActivityTrigger(habit);
                                res.Success = true;
                                res.Message = "Resuming habits to start \"" + habit + "\".";
                                break;
                            }

                            appSettings.bActivityStartedFromDevice = false; // Important
                            pageHabits.StartActivityTriggeredFromAudioCommand(habit);
                            res.Success = true;
                            res.Message = "Started habit \"" + habit + "\".";
                        }
                        break;
                    }

                case "pause_habit":
                    {
                        var ongoing = app.wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing;
                        if (ongoing?.bTimerRunning == true)
                        {
                            InvokeButton(ongoing.btnPausePlay);
                            res.Success = true;
                            res.Message = "Habit paused.";
                        }
                        break;
                    }

                case "resume_habit":
                    {
                        var ongoing = app.wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing;
                        if (ongoing?.bTimerRunning == false)
                        {
                            InvokeButton(ongoing.btnPausePlay);
                            res.Success = true;
                            res.Message = "Habit resumed.";
                        }
                        break;
                    }

                default:
                    {
                        LogWriter.LogWrite("VoiceIntentDispatcher - unknown action: " + result.action);
                        res.Recognized = false;
                        res.Message = "Sorry, I didn't understand that command.";
                        break;
                    }
            }

            return res;
        }

        /// <summary>
        /// Parses the datetime string produced by the intent classifier. The classifier is
        /// instructed to emit an absolute local datetime ("yyyy-MM-dd HH:mm:ss"), resolving any
        /// relative phrasing ("tomorrow 9am", "in 2 hours"). Returns the value in UTC, or null
        /// when the string is empty or cannot be parsed.
        /// </summary>
        private static DateTime? ParseVoiceDateTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeLocal, out DateTime dt))
            {
                return dt.ToUniversalTime();
            }
            LogWriter.LogWrite("VoiceIntentDispatcher - could not parse datetime: " + s);
            return null;
        }

        // Invokes a WPF button via the UI Automation InvokePattern, matching how the existing
        // Focus Button code toggles the habit play/pause button.
        private static void InvokeButton(System.Windows.Controls.Button button)
        {
            if (button == null) return;
            var peer = new ButtonAutomationPeer(button);
            var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv?.Invoke();
        }
    }
}
