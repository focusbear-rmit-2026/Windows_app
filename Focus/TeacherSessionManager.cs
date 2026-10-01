using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Interop.UIAutomationClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Teacher Mode - a teacher starts a super strict class focus session from the web dashboard.
    /// This manager owns the teacher session state + handlers. The session never starts without the
    /// student accepting the join prompt, cannot be stopped early (only unlock requests), hot-syncs
    /// the allow-list live from pusher events and reports presence with heartbeats.
    /// Mirrors the Mac app's teacherSessionManager.
    /// </summary>
    class TeacherSessionManager
    {
        private static readonly TeacherSessionManager instance = new TeacherSessionManager();
        public static TeacherSessionManager Instance { get { return instance; } }

        AppSettings appSettings = AppSettings.GetInstance;

        public string sActiveTeacherSessionId = "";
        public string sActiveTeacherSessionTeamId = "";
        public string sActiveTeacherSessionName = "";
        public int nAppliedAllowlistVersion = 0;
        public DateTime? dtTeacherSessionScheduledFinishTime = null;
        public bool bTeacherSessionEndAllowed = false; //only true when class-session-ended arrived or the scheduled finish time is reached
        public bool bTeacherSessionAiBlockingEnabled = false; //ai_blocking_enabled from the class session payload - AI URL checks depend on this while the session is active

        string sPendingInviteSessionId = "";
        // Set when the student declines the join prompt: keeps the session payload so they can still join
        // later from the tray "A class focus session is running" card (declining otherwise left no way back).
        JObject _joinableSessionData = null;
        bool bDidPostponeHabitsForSession = false; //true only when we auto-postponed a running routine on join, so we resume it on finish
        FocusMode TeacherFocusMode = null;
        DispatcherTimer dispatcherTimerHeartbeat = null;
        TeacherUnlockRequestWindow wndTeacherUnlockRequest = null;
        TeacherPactInviteWindow wndTeacherPactInvite = null;

        const int HEARTBEAT_INTERVAL_SECONDS = 25; //canonical 20-30s heartbeat interval
        const int OPEN_ENDED_FALLBACK_HOURS = 12; //mirror the backend fallback for sessions with no scheduled finish time

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private TeacherSessionManager()
        {
        }

        public bool IsTeacherSessionActive()
        {
            return sActiveTeacherSessionId.Length > 0;
        }

        //pusher payloads carry class_session_id, rest api responses carry id
        private string GetSessionIdFromData(JObject ObjData)
        {
            if (ObjData == null)
            {
                return "";
            }
            if (ObjData.ContainsKey("class_session_id"))
            {
                return ObjData["class_session_id"].ToString();
            }
            if (ObjData.ContainsKey("id"))
            {
                return ObjData["id"].ToString();
            }
            return "";
        }

        //Reads a UTC timestamp out of a pusher/rest payload.
        //JObject.Parse converts ISO 8601 timestamps into JTokenType.Date, so ToString() renders them
        //in the CURRENT culture (e.g. "16/07/2026 8:00:00 am" on a day-first machine) which then does
        //not parse back with InvariantCulture. Reading the token as a DateTimeOffset skips the string
        //round-trip entirely and keeps the offset, so this works on every culture.
        //Returns false for a missing/null/unreadable value so the caller can fall back instead of
        //throwing - a throw here would abort StartTeacherSession and leave the student joined server
        //side with NO local enforcement (Stop/Pause would work normally).
        private bool TryGetUtcDate(JObject ObjData, string sKey, out DateTime dtUtc)
        {
            dtUtc = DateTime.MinValue;
            if (ObjData == null || !ObjData.ContainsKey(sKey) || ObjData[sKey].Type == JTokenType.Null)
            {
                return false;
            }
            JToken token = ObjData[sKey];
            try
            {
                if (token.Type == JTokenType.Date)
                {
                    dtUtc = token.ToObject<DateTimeOffset>().UtcDateTime;
                    return true;
                }
                //still handle a plain string in case date parsing is ever turned off upstream
                string sValue = token.ToString();
                if (sValue.Length > 0 && DateTimeOffset.TryParse(sValue,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset parsed))
                {
                    dtUtc = parsed.UtcDateTime;
                    return true;
                }
                LogWriter.LogWrite("TeacherSessionManager - unparseable " + sKey + " '" + sValue + "', using fallback");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager TryGetUtcDate " + sKey + " ex - " + ex.Message);
            }
            return false;
        }

        //ai_blocking_enabled from a class session payload - defaults to false if absent/unreadable
        private bool ReadAiBlockingEnabled(JObject ObjData)
        {
            try
            {
                if (ObjData != null && ObjData.ContainsKey("ai_blocking_enabled") && ObjData["ai_blocking_enabled"].Type != JTokenType.Null)
                {
                    return ObjData["ai_blocking_enabled"].ToObject<bool>();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ReadAiBlockingEnabled ex - " + ex.Message);
            }
            return false;
        }

        //true only while a teacher class session is active and its ai_blocking_enabled setting is on -
        //AI URL checks are gated on this (mirrors the blocking schedule's bAIBlockingEnabled).
        public bool IsTeacherAiBlockingEnabled()
        {
            return IsTeacherSessionActive() && bTeacherSessionAiBlockingEnabled;
        }

        //strings are localized via config.json / config-es.json (section teacherSessionInfo) with
        //hardcoded english fallbacks because the remotely downloaded config may not have the section yet
        public string GetLocalizedString(string sKey, string sFallback)
        {
            try
            {
                if (appSettings.ConfigFileStrings != null)
                {
                    JToken section = appSettings.ConfigFileStrings["teacherSessionInfo"];
                    if (section != null && section[sKey] != null)
                    {
                        return section[sKey].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager GetLocalizedString ex - " + ex.Message);
            }
            return sFallback;
        }

        //Teacher popups are raised asynchronously (pusher events / the browser thread). During the
        //evening routine and focus summary the main window is a full-screen, TOPMOST window, and a modal
        //shown without an owner can end up BEHIND it: the main window stays in front but is disabled by
        //the modal, so mouse hover/click hits the disabled window (nothing happens) while the keyboard
        //still reaches the modal (Tab/Enter work). Owning the popup to the main window forces it above the
        //owner in the z-order, so it is clickable. Same-thread only, so this must run on the UI dispatcher.
        private void OwnToMainWindow(Window wnd)
        {
            try
            {
                if (wnd == null)
                {
                    return;
                }
                Window owner = ((App)Application.Current).wndFocusBearMainWindow;
                if (owner != null && owner != wnd && owner.IsVisible)
                {
                    wnd.Owner = owner;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager OwnToMainWindow ex - " + ex.Message);
            }
        }

        //*********** Pusher event handlers (must be called on the UI dispatcher, PusherHandler dispatches) ***********//

        public void HandleSessionInvite(JObject ObjData)
        {
            LogWriter.LogWrite("TeacherSessionManager - class-session-invite received");
            if (IsTeacherSessionActive()) //one teacher session at a time
            {
                return;
            }
            ShowJoinPrompt(ObjData);
        }

        public void HandleAllowlistChanged(JObject ObjData)
        {
            string sSessionId = GetSessionIdFromData(ObjData);
            if (sSessionId.Length == 0 || sSessionId != sActiveTeacherSessionId)
            {
                return;
            }
            int nVersion = ObjData.ContainsKey("allowlist_version") ? ObjData["allowlist_version"].ToObject<int>() : 0;
            if (nVersion <= nAppliedAllowlistVersion) //drop stale/duplicate deliveries
            {
                LogWriter.LogWrite("TeacherSessionManager - drop stale class-allowlist-changed version " + nVersion + " (applied " + nAppliedAllowlistVersion + ")");
                return;
            }
            nAppliedAllowlistVersion = nVersion;

            //hot-sync - the enforcement in FocusChangedHandler re-reads the running FocusMode's
            //arrAllowedAppsWin/arrAllowedUrls on every focus change, so mutating the live
            //collections is enough - no restart needed
            if (TeacherFocusMode != null)
            {
                bTeacherSessionAiBlockingEnabled = ReadAiBlockingEnabled(ObjData); //the teacher can toggle AI mid-session
                ApplyAllowlistToTeacherFocusMode(ObjData);
                ForceReEvaluateForegroundApp(); //immediate re-eval of the app currently in front (block direction)
                ReloadForegroundBlockedUrlIfNowAllowed(); //if the student is on the block page for a just-approved URL, load it back
            }
            LogWriter.LogWrite("TeacherSessionManager - allow-list hot-synced to version " + nVersion);
        }

        //ForceReEvaluateForegroundApp only re-runs enforcement (the block direction): for a browser it
        //re-reads the current URL, which is the Focus Bear block page (an allowed local file), so it never
        //reloads the URL the teacher just approved. We captured the requested URL when the student asked
        //for it (sLastFocusBlockedUrl); if it is now allowed AND the student is still on that block-page tab
        //(sLastActiveURLInFirefox unchanged since we set it), navigate the browser back to it.
        private void ReloadForegroundBlockedUrlIfNowAllowed()
        {
            try
            {
                string sBlockedUrl = appSettings.sLastFocusBlockedUrl;
                string sTab = appSettings.sLastActiveURLInFirefox ?? "";
                LogWriter.LogWrite("TeacherSessionManager - reload check, requested url = '" + sBlockedUrl + "', tab now = '" + sTab + "'");
                if (string.IsNullOrEmpty(sBlockedUrl))
                {
                    return; //no pending request
                }
                //same-tab guard: still on the block page for THIS url? Compare by the block page's old_url,
                //not the whole address (Firefox reports the block-page URL in a slightly different form than
                //we stored, so an exact string match gives false "navigated away").
                string sTabOldUrl = "";
                if (sTab.IndexOf("focus-block.html", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sTabOldUrl = ProcessBrowserURLs.ExtractOldUrl(sTab) ?? "";
                }
                if (!sBlockedUrl.Equals(sTabOldUrl, StringComparison.OrdinalIgnoreCase))
                {
                    LogWriter.LogWrite("TeacherSessionManager - reload skipped, foreground tab is not the block page for this url (tab old_url = '" + sTabOldUrl + "')");
                    return;
                }
                if (!IsUrlAllowedInTeacherMode(sBlockedUrl))
                {
                    LogWriter.LogWrite("TeacherSessionManager - reload skipped, '" + sBlockedUrl + "' is still not in the allow-list");
                    return;
                }
                LogWriter.LogWrite("TeacherSessionManager - reloading now-allowed URL after approval - " + sBlockedUrl);
                ProcessBrowserURLs.ReloadForegroundBrowserToUrl(GetForegroundWindow(), sBlockedUrl);
                appSettings.sLastFocusBlockedUrl = ""; //consumed
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ReloadForegroundBlockedUrlIfNowAllowed ex - " + ex.Message);
            }
        }

        //matches the enforcement's allowed-URL check (strip scheme/www, prefix match) against the live
        //teacher allow-list, so we only reload a block-page URL that is genuinely allowed now.
        public bool IsUrlAllowedInTeacherMode(string sUrl)
        {
            if (TeacherFocusMode == null || string.IsNullOrEmpty(sUrl))
            {
                return false;
            }
            string sToCheck = sUrl.Replace("http://", "").Replace("https://", "").Replace("www.", "");
            foreach (string sAllowed in TeacherFocusMode.arrAllowedUrls)
            {
                if (string.IsNullOrEmpty(sAllowed))
                {
                    continue;
                }
                string sAllowedClean = sAllowed.Replace("http://", "").Replace("https://", "").Replace("www.", "").TrimEnd('/');
                if (sAllowedClean.Length > 0 && sToCheck.StartsWith(sAllowedClean, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public void HandleSessionEnded(JObject ObjData)
        {
            string sSessionId = GetSessionIdFromData(ObjData);
            if (sSessionId.Length == 0)
            {
                return;
            }

            // A class session ended - drop any pending "Join" offer in the tray. Only one teacher session
            // runs at a time, so any class-session-ended the student receives means the session they could
            // join is over; clear regardless of an exact id match (id mismatch was leaving the card up).
            if (_joinableSessionData != null)
            {
                LogWriter.LogWrite("TeacherSessionManager - class-session-ended (ended id=" + sSessionId
                    + ", joinable id=" + GetSessionIdFromData(_joinableSessionData) + ") - clearing tray join offer");
                _joinableSessionData = null;
                RefreshTrayMenu();
            }

            if (sSessionId != sActiveTeacherSessionId)
            {
                return;
            }
            LogWriter.LogWrite("TeacherSessionManager - class-session-ended received");
            bTeacherSessionEndAllowed = true;
            TeacherSessionFinished("ended by teacher");

            string sTitle = GetLocalizedString("sessionEndedTitle", "Class focus session ended");
            string sBody = GetLocalizedString("sessionEndedBody", "Your teacher has ended the class focus session.");
            try
            {
                FBMessageBox msg = new FBMessageBox(sBody, sTitle, MessageBoxButton.OK, bAutoClose: true);
                OwnToMainWindow(msg); //render above the (possibly full-screen topmost) main window so it's clickable
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.ShowDialog();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager HandleSessionEnded alert ex - " + ex.Message);
            }
        }

        public void HandleUnlockResolved(JObject ObjData)
        {
            string sSessionId = GetSessionIdFromData(ObjData);
            if (sSessionId.Length == 0 || sSessionId != sActiveTeacherSessionId)
            {
                return;
            }
            string sStatus = ObjData.ContainsKey("status") ? ObjData["status"].ToString().ToLower() : "";
            LogWriter.LogWrite("TeacherSessionManager - class-unlock-resolved received with status " + sStatus);
            if (sStatus == "rejected" || sStatus == "expired") //approvals arrive as class-allowlist-changed, nothing to do for them here
            {
                string sRequested = "";
                if (ObjData.ContainsKey("requested_app") && ObjData["requested_app"].Type != JTokenType.Null)
                {
                    sRequested = ObjData["requested_app"].ToString();
                }
                else if (ObjData.ContainsKey("requested_domain") && ObjData["requested_domain"].Type != JTokenType.Null)
                {
                    sRequested = ObjData["requested_domain"].ToString();
                }
                string sTitle = GetLocalizedString("unlockDeclinedTitle", "Unlock request declined");
                string sBody = GetLocalizedString("unlockDeclinedBody", "Your teacher declined your unlock request.");
                if (sRequested.Length > 0)
                {
                    sBody = sBody + " (" + sRequested + ")";
                }
                appSettings.ShowBaloonTip(sBody, sTitle); //brief notice only
            }
        }

        //*********** Join prompt + start (requirement - no start without approval) ***********//

        public void ShowJoinPrompt(JObject ObjData)
        {
            string sSessionId = GetSessionIdFromData(ObjData);
            if (sSessionId.Length == 0)
            {
                return;
            }
            if (sPendingInviteSessionId == sSessionId) //prompt already showing for this session
            {
                return;
            }
            sPendingInviteSessionId = sSessionId;

            LogWriter.LogWrite("TeacherSessionManager - showing PACT invite for session " + sSessionId);
            try
            {
                ShowTeacherPactInvitePrompt(ObjData, sSessionId);
            }
            catch (Exception ex)
            {
                sPendingInviteSessionId = "";
                LogWriter.LogWrite("TeacherSessionManager ShowJoinPrompt ex - " + ex.Message);
            }
        }

        //Two-screen PACT invite window (mirrors Mac-App#2377). Non-modal, callback driven: Activate joins
        //the session, Decline (after the friction hold) reports a reason to the teacher.
        private void ShowTeacherPactInvitePrompt(JObject ObjData, string sSessionId)
        {
            CloseTeacherPactInviteWindowIfVisible();

            string sSubtitle = BuildSessionSubtitle(ObjData);
            string sEndsLine;
            string sInviteBody = BuildInviteBody(ObjData, out sEndsLine);
            System.Collections.Generic.List<string> allowedSites = BuildAllowedSites(ObjData);

            wndTeacherPactInvite = new TeacherPactInviteWindow(
                sSubtitle, sEndsLine, sInviteBody, allowedSites,
                onActivate: () =>
                {
                    LogWriter.LogWrite("TeacherSessionManager - PACT invite accepted");
                    JoinTeacherSession(ObjData);
                },
                onDecline: (sReason) =>
                {
                    LogWriter.LogWrite("TeacherSessionManager - PACT invite declined");
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    _ = client.DeclineClassSession(sSessionId, sReason);

                    // Keep the session so the student can still join later from the tray - declining
                    // otherwise left no way back into a running class session.
                    _joinableSessionData = ObjData;
                    RefreshTrayMenu();
                });

            wndTeacherPactInvite.Closed += (s, e) =>
            {
                sPendingInviteSessionId = "";
                wndTeacherPactInvite = null;
            };

            OwnToMainWindow(wndTeacherPactInvite); //render above the (possibly full-screen topmost) main window
            wndTeacherPactInvite.Show();
            wndTeacherPactInvite.Activate();

            //On launch-adoption (AdoptCurrentSessionOnLaunch), the main/full-screen-lock window is already
            //shown by the time the async current-session lookup resolves and this prompt appears - both are
            //Topmost, and being shown later isn't enough to win the z-order. Toggling Topmost off/on forces
            //this window back to the very front of the always-on-top band.
            wndTeacherPactInvite.Topmost = false;
            wndTeacherPactInvite.Topmost = true;
        }

        public void CloseTeacherPactInviteWindowIfVisible()
        {
            try
            {
                if (wndTeacherPactInvite != null)
                {
                    if (wndTeacherPactInvite.IsVisible)
                    {
                        wndTeacherPactInvite.Close();
                    }
                    wndTeacherPactInvite = null;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager CloseTeacherPactInviteWindowIfVisible ex - " + ex.Message);
            }
        }

        private string GetStringField(JObject ObjData, string sKey)
        {
            if (ObjData.ContainsKey(sKey) && ObjData[sKey].Type != JTokenType.Null)
            {
                return ObjData[sKey].ToString();
            }
            return "";
        }

        //mirror Mac teacherSessionSubtitle: "<subject> · <topic>", falling back to the session name
        private string BuildSessionSubtitle(JObject ObjData)
        {
            string sSubject = GetStringField(ObjData, "subject");
            string sTopic = GetStringField(ObjData, "topic");
            if (sSubject.Length > 0 && sTopic.Length > 0)
            {
                return sSubject + " · " + sTopic;
            }
            if (sSubject.Length > 0)
            {
                return sSubject;
            }
            if (sTopic.Length > 0)
            {
                return sTopic;
            }
            string sName = GetStringField(ObjData, "name");
            return sName.Length > 0 ? sName : GetLocalizedString("defaultSessionName", "Class focus session");
        }

        //mirror Mac teacherSessionAllowedSites: allowed_urls, falling back to allowed_apps (both string arrays)
        private System.Collections.Generic.List<string> BuildAllowedSites(JObject ObjData)
        {
            System.Collections.Generic.List<string> sites = ReadStringArray(ObjData, "allowed_urls");
            if (sites.Count == 0)
            {
                sites = ReadStringArray(ObjData, "allowed_apps");
            }
            return sites;
        }

        private System.Collections.Generic.List<string> ReadStringArray(JObject ObjData, string sKey)
        {
            System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();
            if (ObjData.ContainsKey(sKey) && ObjData[sKey].Type == JTokenType.Array)
            {
                foreach (JToken item in (JArray)ObjData[sKey])
                {
                    if (item.Type != JTokenType.Null)
                    {
                        string sItem = item.ToString().Trim();
                        if (sItem.Length > 0)
                        {
                            result.Add(sItem);
                        }
                    }
                }
            }
            return result;
        }

        //"Ends <TIME>" (empty when open-ended) + the invite body with <TIME> resolved to the local end
        //time, or the "the session ends" fallback - mirrors Mac's pactEndsLine / pactInviteBody handling.
        private string BuildInviteBody(JObject ObjData, out string sEndsLine)
        {
            string sBodyTime;
            if (TryGetUtcDate(ObjData, "scheduled_finish_time", out DateTime dtFinishUtc))
            {
                string sTime = dtFinishUtc.ToLocalTime().ToString("h:mm tt");
                sEndsLine = GetLocalizedString("pactEndsLine", "Ends <TIME>").Replace("<TIME>", sTime);
                sBodyTime = sTime;
            }
            else
            {
                sEndsLine = "";
                sBodyTime = GetLocalizedString("pactEndFallback", "the session ends");
            }
            return GetLocalizedString("pactInviteBody",
                "Everything else is paused until <TIME>. If you need another site, you can ask your teacher to add it. You cannot end the session early.")
                .Replace("<TIME>", sBodyTime);
        }

        public async void JoinTeacherSession(JObject ObjData)
        {
            //async void: wrap the whole body so an exception (e.g. from the awaited join call or the
            //dialog) is logged instead of escaping to an unhandled exception that crashes the app.
            try
            {
                string sSessionId = GetSessionIdFromData(ObjData);
                if (sSessionId.Length == 0)
                {
                    return;
                }
                FocusBearAPIClient client = new FocusBearAPIClient();
                JObject joinedSession = await client.JoinClassSession(sSessionId);
                if (joinedSession != null)
                {
                    StartTeacherSession(joinedSession);
                }
                else
                {
                    LogWriter.LogWrite("TeacherSessionManager - join api failed for session " + sSessionId);
                    // The session is no longer joinable (likely ended) - drop the tray "join" offer for it.
                    _joinableSessionData = null;
                    RefreshTrayMenu();
                    string sTitle = GetLocalizedString("joinFailedTitle", "Could not join the class session");
                    string sBody = GetLocalizedString("joinFailedBody", "The class focus session is no longer active or could not be joined. Please ask your teacher to start it again.");
                    FBMessageBox msg = new FBMessageBox(sBody, sTitle, MessageBoxButton.OK, bAutoClose: true);
                    OwnToMainWindow(msg); //render above the (possibly full-screen topmost) main window so it's clickable
                    msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    msg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager JoinTeacherSession ex - " + ex.Message);
            }
        }

        // A class session the student declined but can still join from the tray card. False once they join,
        // once a session is active, or once joining it fails (ended).
        public bool HasJoinableSession()
        {
            return _joinableSessionData != null && !IsTeacherSessionActive();
        }

        // Name of the joinable (declined) session, for the tray card. Empty if none/unnamed.
        public string GetJoinableSessionName()
        {
            try
            {
                if (_joinableSessionData != null && _joinableSessionData.ContainsKey("name")
                    && _joinableSessionData["name"].Type != JTokenType.Null)
                    return _joinableSessionData["name"].ToString();
            }
            catch (Exception ex) { LogWriter.LogWrite("GetJoinableSessionName ex - " + ex.Message); }
            return "";
        }

        // Join the previously-declined class session (from the tray "join" card).
        public void JoinDeclinedSession()
        {
            JObject data = _joinableSessionData;
            if (data == null)
            {
                return;
            }
            LogWriter.LogWrite("TeacherSessionManager - joining previously-declined class session from tray");
            JoinTeacherSession(data);
        }

        // Repaint the tray menu so the "A class focus session is running / Join" card appears or disappears.
        private void RefreshTrayMenu()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try { ((App)Application.Current).wndTrayMenu?.UpdateTrayMenu(); }
                    catch (Exception exInner) { LogWriter.LogWrite("RefreshTrayMenu inner ex - " + exInner.Message); }
                }));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshTrayMenu ex - " + ex.Message);
            }
        }

        //builds the locked allow-list focus mode for the class session and starts the local
        //focus session state (mirrors PusherHandler.HandleFocusModeStartedEvent) - the enforced
        //block was already started server side by POST /class-session/:id/join
        public void StartTeacherSession(JObject ObjData)
        {
            string sSessionId = GetSessionIdFromData(ObjData);
            if (sSessionId.Length == 0)
            {
                return;
            }
            if (sActiveTeacherSessionId == sSessionId) //already running - treat as an allow-list refresh
            {
                HandleAllowlistChanged(ObjData);
                return;
            }
            _joinableSessionData = null; //joined now - clear the "join from tray" offer
            LogWriter.LogWrite("TeacherSessionManager - starting teacher session " + sSessionId);

            try
            {
                //the server force-finished any personal focus block on join, clean the local flags silently
                if (appSettings.isFocusModeRunning)
                {
                    var runningModes = appSettings.arrFocusModes.Where(i => i.isOn == true).ToList();
                    foreach (FocusMode mode in runningModes)
                    {
                        mode.isOn = false;
                    }
                    appSettings.isFocusModePaused = false;
                    appSettings.nPausedFocusRemainingSeconds = 0;
                }
                appSettings.bUseSuperPomodoroMode = false;

                string sSessionName = ObjData.ContainsKey("name") && ObjData["name"].Type != JTokenType.Null ? ObjData["name"].ToString() : "";
                if (sSessionName.Length == 0)
                {
                    sSessionName = GetLocalizedString("defaultSessionName", "Class focus session");
                }

                //scheduled_finish_time comes as UTC ISO 8601; open ended sessions mirror the backend 12h fallback
                DateTime FinishTimeLocal;
                if (TryGetUtcDate(ObjData, "scheduled_finish_time", out DateTime dtFinishUtc))
                {
                    FinishTimeLocal = dtFinishUtc.ToLocalTime();
                }
                else
                {
                    FinishTimeLocal = DateTime.Now.AddHours(OPEN_ENDED_FALLBACK_HOURS);
                }
                TimeSpan difference = FinishTimeLocal.Subtract(DateTime.Now);
                int nTotalMinutes = Convert.ToInt32(difference.TotalMinutes);
                if (nTotalMinutes < 1)
                {
                    nTotalMinutes = 1;
                }
                int nHours = nTotalMinutes / 60;
                int nMinutes = nTotalMinutes % 60;

                //build the synthetic allow-list focus mode for this class session.
                //isLockedFocus keeps it out of the user facing focus mode lists; isAllowedListApproach +
                //isAllowedAllApps=false make the enforcement block everything except the teacher's allow-list.
                TeacherFocusMode = new FocusMode("teacher-" + sSessionId, sSessionName,
                    new ObservableCollection<string>(), new ObservableCollection<string>(),
                    new ObservableCollection<AppDetails>(), "", false,
                    false /*isAllowedAllApps*/, true /*isAllowedListApproach*/, false /*isDefault*/,
                    false /*isMeetings*/, true /*isLockedFocus*/, false /*isRelaxFocus*/,
                    false /*isDeepWorkFocus*/, false /*isWorkFocus*/);
                ApplyAllowlistToTeacherFocusMode(ObjData);

                //remove any stale teacher mode from a previous session before adding the new one
                var oldTeacherModes = appSettings.arrFocusModes.Where(i => i.id != null && i.id.StartsWith("teacher-")).ToList();
                foreach (FocusMode mode in oldTeacherModes)
                {
                    appSettings.arrFocusModes.Remove(mode);
                }
                appSettings.arrFocusModes.Add(TeacherFocusMode);

                sActiveTeacherSessionId = sSessionId;
                sActiveTeacherSessionTeamId = ObjData.ContainsKey("team_id") && ObjData["team_id"].Type != JTokenType.Null ? ObjData["team_id"].ToString() : "";
                sActiveTeacherSessionName = sSessionName;
                nAppliedAllowlistVersion = ObjData.ContainsKey("allowlist_version") ? ObjData["allowlist_version"].ToObject<int>() : 1;
                dtTeacherSessionScheduledFinishTime = FinishTimeLocal;
                bTeacherSessionEndAllowed = false;
                bTeacherSessionAiBlockingEnabled = ReadAiBlockingEnabled(ObjData);

                //mirror PusherHandler.HandleFocusModeStartedEvent - set the running session globals
                Sounds.PlayFocusModeStartSound();
                TeacherFocusMode.isOn = true;
                appSettings.sMostRecentFocusmodeDoneID = TeacherFocusMode.id;
                appSettings.isFocusModeRunning = true;
                appSettings.sRunningFocusModeID = TeacherFocusMode.id;
                appSettings.sRunningFocusModeName = TeacherFocusMode.name;
                appSettings.sRunningFocusModeIntention = sSessionName;
                appSettings.sRunningFocusModeStartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                appSettings.focusSessionStartTime = DateTime.Now;
                appSettings.isFocusModePaused = false;
                appSettings.CurrentFocusSessionDuration = 0;
                appSettings.sRunningFocusModeHours = nHours.ToString();
                appSettings.sRunningFocusModeMinutes = nMinutes.ToString();
                appSettings.sRunningFocusModeEndTime = FinishTimeLocal.ToString();

                //clear any old stuff for gentle blocking
                appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                appSettings.TemporarryBlockedURLsGentleBlock.Clear();
                appSettings.TemporaryAllowedKeywords.Clear();
                appSettings.TemporaryAllowedURLsTabTitles.Clear();

                // TodoPlayer: FE owns class checklist — pass empty tasks + class_session_id in metadata.
                PrepareClassSessionTodoPlayer(sSessionId);
                // The teacher's checklist for the native session screen. A join / current payload carries the
                // tasks and this student's ticks; a Pusher invite payload has no tasks, so fetch them.
                if (ObjData["tasks"] is JArray) ClassSessionChecklistStore.Shared.Apply(ObjData);
                else { ClassSessionChecklistStore.Shared.Apply(new JObject { ["id"] = sSessionId }); _ = ClassSessionChecklistStore.Shared.ReloadAsync(); }

                try
                {
                    ((App)Application.Current).ShowFocusProgressWindow();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("TeacherSessionManager ShowFocusProgressWindow ex - " + ex.Message);
                }

                //start the focus mode end timer (also arms the hardware focus button + todo player)
                ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes);

                //if the student was in the middle of their morning/evening routine when the class
                //session started, postpone it for the class duration (+1 min) so the locked session
                //takes over the screen and the routine resumes just after the class ends. The focus
                //session globals are already set above, so PostponeHabitsForClassSession runs its
                //focus-running path. ShowMorningRoutine is also guarded to stay away while active.
                try
                {
                    bDidPostponeHabitsForSession = ((App)(Application.Current)).PostponeHabitsForClassSession(nTotalMinutes);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("TeacherSessionManager PostponeHabitsForClassSession ex - " + ex.Message);
                }

                StartHeartbeatTimer();
                SendHeartbeat(); //immediate first beat so the teacher roster updates right away
                ForceReEvaluateForegroundApp();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager StartTeacherSession ex - " + ex.Message);
            }
        }

        private void ApplyAllowlistToTeacherFocusMode(JObject ObjData)
        {
            if (TeacherFocusMode == null)
            {
                return;
            }
            TeacherFocusMode.arrAllowedAppsWin.Clear();
            if (ObjData.ContainsKey("allowed_apps") && ObjData["allowed_apps"].Type == JTokenType.Array)
            {
                foreach (JToken app in (JArray)ObjData["allowed_apps"])
                {
                    string sApp = app.ToString();
                    if (sApp.Length > 0)
                    {
                        //AppName drives the window title wildcard match, ExeNames the process name match
                        TeacherFocusMode.arrAllowedAppsWin.Add(new AppDetails(sApp, sApp));
                    }
                }
            }
            TeacherFocusMode.arrAllowedUrls.Clear();
            if (ObjData.ContainsKey("allowed_urls") && ObjData["allowed_urls"].Type == JTokenType.Array)
            {
                foreach (JToken url in (JArray)ObjData["allowed_urls"])
                {
                    string sUrl = url.ToString();
                    if (sUrl.Length > 0)
                    {
                        TeacherFocusMode.arrAllowedUrls.Add(sUrl);
                    }
                }
            }
        }

        /// <summary>
        /// Prepares TodoPlayer for a student class session: backup personal tasks, force empty tasks
        /// (FE loads checklist via class-session APIs using class_session_id from LoadMetaData).
        /// Must run before StartFocusModeTimer / ShowToDoPlayer.
        /// </summary>
        private void PrepareClassSessionTodoPlayer(string sSessionId)
        {
            try
            {
                appSettings.sPersonalRawTasksBackup = appSettings.sRawTasks ?? "";
                appSettings.sClassSessionTodoSessionId = sSessionId ?? "";
                appSettings.sRawTasks = "[]";
                LogWriter.LogWrite("TeacherSessionManager - class session TodoPlayer ready; class_session_id=" +
                    appSettings.sClassSessionTodoSessionId + " tasks=[] (FE-owned checklist)");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager PrepareClassSessionTodoPlayer ex - " + ex.Message);
                appSettings.sClassSessionTodoSessionId = sSessionId ?? "";
                appSettings.sRawTasks = "[]";
            }
        }

        /// <summary>
        /// Clears class-session TodoPlayer mode and optionally restores the personal sRawTasks backup.
        /// </summary>
        private void ClearClassSessionTodoPlayerState(bool restorePersonal)
        {
            try
            {
                bool wasActive = !string.IsNullOrEmpty(appSettings.sClassSessionTodoSessionId);
                if (restorePersonal && wasActive)
                {
                    appSettings.sRawTasks = appSettings.sPersonalRawTasksBackup ?? "";
                }
                appSettings.sClassSessionTodoSessionId = "";
                appSettings.sPersonalRawTasksBackup = "";
                ClassSessionChecklistStore.Shared.Clear();
                if (wasActive)
                {
                    LogWriter.LogWrite("TeacherSessionManager - cleared class session TodoPlayer state" +
                        (restorePersonal ? " (personal sRawTasks restored)" : ""));
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ClearClassSessionTodoPlayerState ex - " + ex.Message);
            }
        }

        //the enforcement is event driven (UI Automation focus change), so after a hot-sync we push the
        //current foreground app through the handler once so a newly blocked app is minimized immediately
        public void ForceReEvaluateForegroundApp()
        {
            try
            {
                IntPtr hForegroundWindow = GetForegroundWindow();
                if (hForegroundWindow != IntPtr.Zero && appSettings.automation != null && appSettings.handler != null)
                {
                    IUIAutomationElement element = appSettings.automation.ElementFromHandle(hForegroundWindow);
                    if (element != null)
                    {
                        appSettings.handler.HandleFocusChangedEvent(element);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ForceReEvaluateForegroundApp ex - " + ex.Message);
            }
        }

        //*********** Early-stop lock ***********//

        public bool CanTeacherSessionFinishNow()
        {
            if (bTeacherSessionEndAllowed)
            {
                return true;
            }
            if (dtTeacherSessionScheduledFinishTime != null)
            {
                //30 seconds of tolerance for the local end timer firing marginally before the scheduled finish time
                if (DateTime.Now > dtTeacherSessionScheduledFinishTime.Value.AddSeconds(-30))
                {
                    return true;
                }
            }
            return false;
        }

        public void ShowTeacherSessionLockedAlert()
        {
            string sTitle = GetLocalizedString("lockedAlertTitle", "This session is controlled by your teacher");
            string sBody = GetLocalizedString("lockedAlertBody", "You can't stop or pause this focus session early. It ends when your teacher ends it or the scheduled finish time is reached. Need an app or website unlocked? Send your teacher an unlock request.");
            string sButtonRequestUnlock = GetLocalizedString("buttonRequestUnlock", "Request unlock");
            string sButtonOk = "OK";
            try
            {
                sButtonOk = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ShowTeacherSessionLockedAlert ex - " + ex.Message);
            }
            try
            {
                FBMessageBox msg = new FBMessageBox(sBody, sTitle, MessageBoxButton.YesNo, btnYesText: sButtonRequestUnlock, btnNoText: sButtonOk, btnClicked: MessageBoxResult.None);
                OwnToMainWindow(msg); //render above the (possibly full-screen topmost) main window so it's clickable
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.ShowDialog();
                if (msg.btnClicked == MessageBoxResult.Yes)
                {
                    ShowTeacherUnlockRequestWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ShowTeacherSessionLockedAlert ex - " + ex.Message);
            }
        }

        //legit end only - class-session-ended event or scheduled_finish_time reached.
        //no api call here: the server already finished (or will finish) the enforced block.
        public void TeacherSessionFinished(string sReason)
        {
            if (!IsTeacherSessionActive())
            {
                return;
            }
            LogWriter.LogWrite("TeacherSessionManager - finished, reason - " + sReason);

            string sSessionName = sActiveTeacherSessionName;
            string sStartTime = appSettings.sRunningFocusModeStartTime;

            StopHeartbeatTimer();
            sActiveTeacherSessionId = "";
            sActiveTeacherSessionTeamId = "";
            sActiveTeacherSessionName = "";
            nAppliedAllowlistVersion = 0;
            dtTeacherSessionScheduledFinishTime = null;
            bTeacherSessionEndAllowed = false;
            bTeacherSessionAiBlockingEnabled = false;
            CloseTeacherUnlockRequestWindowIfVisible();
            CloseTeacherPactInviteWindowIfVisible();

            try
            {
                if (TeacherFocusMode != null)
                {
                    TeacherFocusMode.isOn = false;
                    if (appSettings.arrFocusModes.Contains(TeacherFocusMode))
                    {
                        appSettings.arrFocusModes.Remove(TeacherFocusMode);
                    }
                    TeacherFocusMode = null;
                }
                appSettings.bUseSuperPomodoroMode = false;

                ((App)(Application.Current)).FinishFocusModeCommonTasks();

                //stats csv entry (mirrors PusherHandler.HandleFocusModeFnishedEvent)
                try
                {
                    TimeSpan difference = DateTime.Now.Subtract(DateTime.Parse(sStartTime));
                    int nSeconds = Convert.ToInt32(difference.TotalSeconds);
                    appSettings.AddEntryInCSV("focus block", sSessionName, nSeconds.ToString(), "", "", sStartTime, "", "", "", "");
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("TeacherSessionManager AddEntryInCSV ex - " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager TeacherSessionFinished ex - " + ex.Message);
            }
            finally
            {
                // After finish sync is skipped, restore personal todos and clear class checklist mode.
                ClearClassSessionTodoPlayerState(restorePersonal: true);
            }

            //if we auto-postponed the student's morning/evening routine when the session started, resume
            //it now that the session has ended (covers an early teacher-end too, instead of waiting out
            //the class-duration postpone timer). Only when WE postponed it - a routine the user postponed
            //themselves before the class is left untouched.
            if (bDidPostponeHabitsForSession)
            {
                try
                {
                    ((App)(Application.Current)).ResumeHabitsAfterClassSession();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("TeacherSessionManager ResumeHabitsAfterClassSession ex - " + ex.Message);
                }
                bDidPostponeHabitsForSession = false;
            }
        }

        //*********** Heartbeat + quit + next-launch adoption ***********//

        private void StartHeartbeatTimer()
        {
            StopHeartbeatTimer();
            dispatcherTimerHeartbeat = new DispatcherTimer(DispatcherPriority.Normal);
            dispatcherTimerHeartbeat.Tick += new EventHandler(dispatcherTimer_Heartbeat);
            dispatcherTimerHeartbeat.Interval = new TimeSpan(0, 0, HEARTBEAT_INTERVAL_SECONDS);
            dispatcherTimerHeartbeat.Start();
        }

        private void StopHeartbeatTimer()
        {
            if (dispatcherTimerHeartbeat != null)
            {
                dispatcherTimerHeartbeat.Stop();
                dispatcherTimerHeartbeat = null;
            }
        }

        private void dispatcherTimer_Heartbeat(object sender, EventArgs e)
        {
            SendHeartbeat();
        }

        public void SendHeartbeat()
        {
            if (!IsTeacherSessionActive())
            {
                StopHeartbeatTimer();
                return;
            }
            bool bInFocus = appSettings.isFocusModeRunning && !appSettings.isFocusModePaused;
            FocusBearAPIClient client = new FocusBearAPIClient();
            _ = client.ClassSessionHeartbeat(sActiveTeacherSessionId, bInFocus, GetIdleSeconds(), GetAlignmentScoreForHeartbeat());
        }

        //Seconds since the last keyboard/mouse input (Win32 GetLastInputInfo). Sent on every beat so the
        //teacher report can derive Engagement = (1 - idle_fraction) * 100 on the server.
        private int GetIdleSeconds()
        {
            try
            {
                return Math.Max(0, (int)IdleWaiter.GetIdleTime().TotalSeconds);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager GetIdleSeconds ex - " + ex.Message);
                return 0;
            }
        }

        //Focus-alignment / site-relevance score (0-100), reported only when AI URL relevance checking is
        //enabled (mirrors Mac's isAiCheckUrlEnabled gating). Returns null otherwise so the field is omitted.
        private int? GetAlignmentScoreForHeartbeat()
        {
            if (!appSettings.bAiCheckUrlEnabled)
            {
                return null;
            }
            try
            {
                double score = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                return (int)Math.Round(Math.Max(0, Math.Min(100, score)));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager GetAlignmentScoreForHeartbeat ex - " + ex.Message);
                return null;
            }
        }

        //best-effort final {in_focus:false} beat on quit; the server also infers quit from the heartbeat gap.
        //synchronous with a short timeout because the process is terminating (called from Application_Exit).
        public void SendFinalHeartbeatBeforeExit()
        {
            if (!IsTeacherSessionActive())
            {
                return;
            }
            LogWriter.LogWrite("TeacherSessionManager - sending final heartbeat before exit");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("device-id", appSettings.sDeviceID);
                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    string sBody = "{\"in_focus\":false,\"device_id\":\"" + appSettings.sDeviceID + "\",\"idle_seconds\":" + GetIdleSeconds() + "}";
                    StringContent content = new StringContent(sBody, Encoding.UTF8, "application/json");
                    string sPath = FocusBearAPIClient.ClassSession + "/" + sActiveTeacherSessionId + "/heartbeat";
                    client.PostAsync(sPath, content).Wait(2000); //don't hold up termination for more than 2 seconds
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager SendFinalHeartbeatBeforeExit ex - " + ex.Message);
            }
        }

        //next-launch adoption via GET /class-session/current - active+unjoined shows the join prompt,
        //joined rebuilds the allow-list mode and resumes (with heartbeats)
        public async void AdoptCurrentSessionOnLaunch()
        {
            try
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                JObject session = await client.GetCurrentClassSession();
                if (session == null)
                {
                    return;
                }
                string sStatus = session.ContainsKey("status") ? session["status"].ToString() : "";
                if (sStatus != "live")
                {
                    return;
                }
                string sJoinStatus = session.ContainsKey("join_status") && session["join_status"].Type != JTokenType.Null ? session["join_status"].ToString() : "";
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (sJoinStatus == "joined")
                    {
                        LogWriter.LogWrite("TeacherSessionManager - adopting joined class session on launch");
                        StartTeacherSession(session);
                    }
                    else if (sJoinStatus == "invited")
                    {
                        LogWriter.LogWrite("TeacherSessionManager - live class session invitation found on launch");
                        ShowJoinPrompt(session);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager AdoptCurrentSessionOnLaunch ex - " + ex.Message);
            }
        }

        //*********** Unlock request ***********//

        public void ShowTeacherUnlockRequestWindow(string prefillAppOrUrl = "")
        {
            if (!IsTeacherSessionActive())
            {
                return;
            }
            try
            {
                CloseTeacherUnlockRequestWindowIfVisible();
                wndTeacherUnlockRequest = new TeacherUnlockRequestWindow(sActiveTeacherSessionId, prefillAppOrUrl);
                OwnToMainWindow(wndTeacherUnlockRequest); //render above the (possibly full-screen topmost) main window
                wndTeacherUnlockRequest.Show();
                wndTeacherUnlockRequest.Activate();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager ShowTeacherUnlockRequestWindow ex - " + ex.Message);
            }
        }

        public void CloseTeacherUnlockRequestWindowIfVisible()
        {
            try
            {
                if (wndTeacherUnlockRequest != null)
                {
                    if (wndTeacherUnlockRequest.IsVisible)
                    {
                        wndTeacherUnlockRequest.Close();
                    }
                    wndTeacherUnlockRequest = null;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherSessionManager CloseTeacherUnlockRequestWindowIfVisible ex - " + ex.Message);
            }
        }
    }
}
