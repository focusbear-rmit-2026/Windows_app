using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Focus_Bear.Focus;
using Newtonsoft.Json.Linq;
using PusherClient;


namespace Focus_Bear.Utils
{
    class PusherHandler
    {
        static AppSettings appSettings = AppSettings.GetInstance;
        private static Pusher _pusher;
        private static Channel _privateChannel;
        public PusherHandler()
        {
            _ = InitPusher();
        }
        public static async Task InitPusher()
        {
            LogWriter.LogWrite("InitPusher()");

            _pusher = new Pusher("e6efc69cb17b6816c56c", new PusherOptions
            {
                Authorizer = new HttpAuthorizer("https://api.focusbear.io/pusher/user-auth")
                {
                    AuthenticationHeader = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken),
                },
                Cluster = "mt1",
                TraceLogger = new TraceLogger(),
            });

            _pusher.ConnectionStateChanged += PusherConnectionStateChanged;
            _pusher.Error += PusherError;
            _pusher.Subscribed += SubscribedHandler;
            _pusher.CountHandler += CountHandler;
            _pusher.Connected += Connected;
            _pusher.BindAll(EventsListener);

            // Subscribe, Setup private encrypted channel
            try
            {
                _privateChannel = await _pusher.SubscribeAsync("private-" + appSettings.userAccount.userID);
                _privateChannel.BindAll(ChannelEvent);

                LogWriter.LogWrite("Pusher BindAll Done");
            }
            catch (ChannelUnauthorizedException unauthorizedException)
            {
                LogWriter.LogWrite($"Authorization failed for {unauthorizedException.ChannelName}. {unauthorizedException.Message}");
            }
            // Connect
            await _pusher.ConnectAsync().ConfigureAwait(false);

        }


        static void EventsListener(string eventName, PusherEvent eventData)
        {
            LogWriter.LogWrite($"{Environment.NewLine} GeneralListner {eventName} {eventData.Data}");

            //Check if the device id is the same, return if same
            string sData = eventData.Data;
            if (sData.Length > 0)
            {
                JObject ObjData = JObject.Parse(sData);
                string sDeviceID = ObjData.ContainsKey("device_id") ? ObjData["device_id"].ToString() : "";
                if (sDeviceID == appSettings.sDeviceID)
                {
                    //Same device return
                    return;
                }

                //In case of activities we get the leader device ID.
                string sLeaderDeviceID = ObjData.ContainsKey("leader_device_id") ? ObjData["leader_device_id"].ToString() : "";
                if (sLeaderDeviceID == appSettings.sDeviceID)
                {
                    //Same device return
                    return;
                }
            }

            if (eventName == "settings-updated")
            {
                LogWriter.LogWrite("Pusher settings-updated - Restart app now for settings to take Effect!");

                ((App)(Application.Current)).RestartApp();
            }
            else if (eventName == "activity-completed")
            {
                HandleActivityCompletedEvent(sData);
            }
            else if (eventName == "focus-mode-finished")
            {
                HandleFocusModeFnishedEvent(sData);
            }
            else if (eventName == "focus-mode-started")
            {
                /*
                {"id":"fffc7659-b162-4d60-a437-c733ea348214","start_time":"2023-12-14T12:59:17.000Z","scheduled_finish_time":"2023-12-14T13:00:17.000Z","intention":"","user_id":"f255d04d-c45a-4114-b68c-1b4682e15bbe","focus_mode_id":"2ebb19d1-d597-4911-bc6e-2d81677cd69a","is_finished":false,"focus_duration_seconds":"0"}
                */
                HandleFocusModeStartedEvent(sData);
            }
            //Teacher Mode class session events (canonical kebab-case names, same vocabulary as the
            //backend + FCM data.type). Unlike focus-mode-started (auto starts), class-session-invite
            //shows a JOIN prompt first - the session never starts without the student's approval.
            //Stale/duplicate deliveries are dropped in the manager via allowlist_version.
            else if (eventName == "class-session-invite" ||
                     eventName == "class-allowlist-changed" ||
                     eventName == "class-session-ended" ||
                     eventName == "class-unlock-resolved")
            {
                HandleClassSessionEvent(eventName, sData);
            }
        }

        static void HandleClassSessionEvent(string eventName, string sData)
        {
            LogWriter.LogWrite("HandleClassSessionEvent() - " + eventName);
            try
            {
                JObject ObjData = JObject.Parse(sData);
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        if (eventName == "class-session-invite")
                        {
                            TeacherSessionManager.Instance.HandleSessionInvite(ObjData);
                        }
                        else if (eventName == "class-allowlist-changed")
                        {
                            TeacherSessionManager.Instance.HandleAllowlistChanged(ObjData);
                        }
                        else if (eventName == "class-session-ended")
                        {
                            TeacherSessionManager.Instance.HandleSessionEnded(ObjData);
                        }
                        else if (eventName == "class-unlock-resolved")
                        {
                            TeacherSessionManager.Instance.HandleUnlockResolved(ObjData);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("HandleClassSessionEvent dispatcher ex - " + ex.Message);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HandleClassSessionEvent ex - " + ex.Message);
            }
        }

        static void Connected(object sender)
        {
            LogWriter.LogWrite($"Pusher Channel Connected");
        }

        // Subscribed event handler
        static void SubscribedHandler(object sender, Channel channel)
        {
            LogWriter.LogWrite($"Subscribed To Channel {channel.Name}");
        }

        static void CountHandler(object sender, string data)
        {
            Console.WriteLine($"CountHandler {data}");
        }

        static void ChannelEvent(string eventName, PusherEvent eventData)
        {
            LogWriter.LogWrite($"{Environment.NewLine}{eventName} {eventData.Data}");
        }

        static void PusherError(object sender, PusherException error)
        {
            TraceMessage(sender, $"{Environment.NewLine}Pusher Error: {error.Message}{Environment.NewLine}{error}");
        }

        static void PusherConnectionStateChanged(object sender, ConnectionState state)
        {
            TraceMessage(sender, $"Connection state: {state}");
        }

        static void TraceMessage(object sender, string message)
        {
            Pusher client = sender as Pusher;
            LogWriter.LogWrite($"{DateTime.Now:o} - {client.SocketID} - {message}");
        }

        //Handler functions
        static void HandleFocusModeStartedEvent(string sData)
        {
            LogWriter.LogWrite("HandleFocusModeStartedEvent()");

            if ((appSettings.isFocusModeRunning ||
               appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
               appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
               appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF ||
               appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY ||
               appSettings.bUseSuperPomodoroMode) &&
               (!appSettings.bHabitsPostponedState ) && (!appSettings.bFocusSummaryPostponedState))
            {
                return;
            }

            JObject ObjData = JObject.Parse(sData);
            string sStartTime = ObjData.ContainsKey("start_time") ? ObjData["start_time"].ToString() : "";
            string sFinishTime = ObjData.ContainsKey("scheduled_finish_time") ? ObjData["scheduled_finish_time"].ToString() : "";
            string sFocusModeId = ObjData.ContainsKey("focus_mode_id") ? ObjData["focus_mode_id"].ToString() : "";
            string sIntention = ObjData.ContainsKey("intention") ? ObjData["intention"].ToString() : "";
            appSettings.sRunningFocusModeIntention = sIntention;
            App.FocusButton.SendFocusGoal(sIntention);

            DateTime StartTimeLocal = DateTime.Parse(sStartTime).ToLocalTime(); //Convert the UTC time to Local time
            DateTime FinishTimeLocal = DateTime.Parse(sFinishTime).ToLocalTime(); //Convert the UTC time to Local time

            TimeSpan difference = FinishTimeLocal.Subtract(StartTimeLocal);

            var DurationMinutes = difference.TotalMinutes;
            int nTotalMinutes = Convert.ToInt32(DurationMinutes);

            if (nTotalMinutes > 0)
            {
                for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
                {
                    if (appSettings.arrFocusModes[i].id == sFocusModeId)
                    {
                        int nHours = nTotalMinutes / 60;
                        int nMinutes = nTotalMinutes % 60;

                        //used when user starts another focus block
                        appSettings.sRunningFocusModeHours = nHours.ToString();
                        appSettings.sRunningFocusModeMinutes = nMinutes.ToString();


                        try
                        {
                            Sounds.PlayFocusModeStartSound();
                            //Start the Focus Mode
                            //Change the play icon for this to STOP icon
                            appSettings.arrFocusModes[i].isOn = true;
                            appSettings.sMostRecentFocusmodeDoneID = appSettings.arrFocusModes[i].id;

                            //Make the global variable true
                            appSettings.isFocusModeRunning = true;
                            appSettings.sRunningFocusModeStartTime = StartTimeLocal.ToString("yyyy-MM-dd HH:mm:ss");
                            appSettings.focusSessionStartTime = DateTime.Now;
                            appSettings.isFocusModePaused = false;
                            appSettings.CurrentFocusSessionDuration = 0;

                            DateTime dTill = FinishTimeLocal;
                            string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                            sTill = sTill.Replace("<TIME>", dTill.ToString());

                            appSettings.sRunningFocusModeEndTime = dTill.ToString();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    ((App)Application.Current).ShowFocusProgressWindow();
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("Pusher Focus Mode Started UI work EX = " + ex.Message);
                                }
                            });

                            appSettings.sRunningFocusModeID = appSettings.arrFocusModes[i].id;
                            appSettings.sRunningFocusModeName = appSettings.arrFocusModes[i].name;

                            //Clear any old stuff for gentle blocking
                            appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                            appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                            appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                            appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                            appSettings.TemporaryAllowedKeywords.Clear();
                            appSettings.TemporaryAllowedURLsTabTitles.Clear();

                            //Start the Focus Mode Timer to end after x hours / minutes
                            ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes);

                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("PUSHER - Start focus mode Exception - " + ex.Message);
                        }

                        break;
                    }
                }
            }
        }

        static void HandleFocusModeFnishedEvent(string sData)
        {
            LogWriter.LogWrite("HandleFocusModeFnishedEvent()");

            //When a student joins a class session the server force-finishes any personal focus block
            //and fires focus-mode-finished - that event must not tear down the just-started teacher
            //session. A teacher session only ends via class-session-ended or its scheduled finish time.
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("HandleFocusModeFnishedEvent ignored - teacher class session is active");
                return;
            }

            //First check if it is already added
            var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
            
            LogWriter.LogWrite("HandleFocusModeFnishedEvent() - Stop the Focus Mode");

            if (listItems.Count() != 0)
            {
                try
                {
                   
                    //Stop the focus mode
                    listItems.First<FocusMode>().isOn = false;
                  
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ((App)Application.Current).FinishFocusModeCommonTasks();
                        ((App)Application.Current).ShowFocusEndWindow();
                    });

                    DateTime now = DateTime.Now;
                    TimeSpan difference = now.Subtract(DateTime.Parse(appSettings.sRunningFocusModeStartTime)); // could also write `now - otherTime`
                    var durationSeconds = difference.TotalSeconds;
                    int nSeconds = Convert.ToInt32(durationSeconds);

                    JObject ObjData = JObject.Parse(sData);
                    string sAchieve = ObjData.ContainsKey("achievements") ? ObjData["achievements"].ToString() : "";
                    string sDistractions = ObjData.ContainsKey("distractions") ? ObjData["distractions"].ToString() : "";
                    appSettings.AddEntryInCSV("focus block", appSettings.sRunningFocusModeName, nSeconds.ToString(), "", "", appSettings.sRunningFocusModeStartTime, sAchieve, sDistractions, "", "");

                    //Ask if we need to show the Add URLS/Keywords window and update focus modes
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HandleFocusModeFnishedEvent ex - " + ex.Message);
                }
            }
        }
        static void HandleActivityCompletedEvent(string sData)
        {
            LogWriter.LogWrite("HandleActivityCompletedEvent()");

            if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState ||
                appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS
                )
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)(Application.Current)).ResumeAfterPostponeFinished();
                });
            }
        }
    }
 
}
