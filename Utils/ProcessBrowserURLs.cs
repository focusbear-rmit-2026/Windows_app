using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Windows.Threading;
using FocusBear.Core.Blocking;

namespace Focus_Bear.Utils
{
    public static class ProcessBrowserURLs
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static string sAlwaysBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type=always-blocked&block_mode={1}&version={2}";
        private static string sFcousBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}&version={4}";
        private static string sActivityBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type={1}&version={2}"; //evening-habit, morning-habit, break-activity

        //The block page's "Oops, I actually need this" is honoured only for a URL this app blocked, on its own block
        //page, with the token it put in that page's URL (BlockPageUnblock). A typed "&unblock_it=true" unblocks nothing.
        //Kept in the app's own data folder (the E2E / harness / test builds each have theirs), so a block page that is
        //already open still unblocks after the app restarts. Expired ones are dropped on load.
        internal static readonly BlockPageTokens UnblockTokens = LoadUnblockTokens();

        internal static string UnblockTokensPath => System.IO.Path.Combine(AppSettings.AppDataDirPath, "block-page-tokens.json");

        private static readonly object _unblockTokensFileLock = new object();

        private static BlockPageTokens LoadUnblockTokens()
        {
            BlockPageTokens tokens;
            try
            {
                string path = UnblockTokensPath;
                tokens = BlockPageTokens.FromJson(System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PBU.cs - unblock tokens load ex - " + ex.Message);
                tokens = new BlockPageTokens();
            }
            tokens.Saved = SaveUnblockTokens;
            return tokens;
        }

        private static void SaveUnblockTokens(string json)
        {
            try
            {
                lock (_unblockTokensFileLock)
                {
                    string path = UnblockTokensPath, temp = path + ".tmp";
                    System.IO.File.WriteAllText(temp, json);
                    if (System.IO.File.Exists(path)) System.IO.File.Replace(temp, path, null); else System.IO.File.Move(temp, path);
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("PBU.cs - unblock tokens save ex - " + ex.Message); }
        }

        /// <summary>A block page URL with a fresh unblock token for <paramref name="oldUrl"/>.</summary>
        private static string Stamped(string blockPageUrl, string oldUrl)
        {
            string page = UnblockTokens.Stamp(blockPageUrl, oldUrl, DateTime.UtcNow);
            LogWriter.LogWrite("PBU.cs - block page for " + (BlockPageUnblock.HostOf(oldUrl) ?? "?") + ", unblock "
                + (BlockPageUnblock.Query(page, BlockPageUnblock.TokenParam) != null ? "offered" : "not offered"));
            return page;
        }

        /// <summary>The URL to reopen when <paramref name="url"/> is an unblock this app offered, else null.</summary>
        internal static string AuthorisedUnblock(string url)
        {
            if (!BlockPageUnblock.HasTrigger(url)) return null;
            string oldUrl = BlockPageUnblock.AuthorisedOldUrl(url, AppSettings.FocusBlockPagePath, UnblockTokens, DateTime.UtcNow);
            if (oldUrl == null) LogWriter.LogWrite("PBU.cs - unblock refused: not an unblock this app's block page offered");
            return oldUrl;
        }


       
        public static void ProcessCurrentURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;
            Debug.WriteLine("[NativeRelevanceModel] URL EVENT | URL=" +
                (appSettings.sLastActiveURLInFirefox ?? "<missing>"));

            //In case the set intention window is open, we should hide it
            appSettings.Hide_SetIntentionWindow();
            appSettings.HideGentleBlockWindow();

            //If its evening routine and the relax activity is on, no need to check anhything or block anything
            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)
            {
                // Bounds-checked: an index past the list threw here, and the whole URL check with it (nothing blocked).
                if (RunningActivityRules.IsRelaxHabitRunning(appSettings))
                {
                    return;
                }
            }

            // A flag left set after its habit has gone must not block every site: clear it (logged once).
            RunningActivityRules.ClearIfStale(appSettings);

            //Only if its not none
            if (appSettings.nCurrentActivityType != CurrentActivityType.NONE)  
            {
                if (appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning)//
                {
                    LogWriter.LogWrite("appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning");

                    ProcessAlloweURLsInActivity(hwnd);
                }
            }

            //A paused focus session must not keep blocking (URLs / AI) - pausing means "suspend focus
            //enforcement until resumed" (#1274). Treat isFocusModePaused like a paused blocking schedule
            //(which is already excluded via !ActiveSchedule.isPaused). Falls through to the baseline
            //always-blocked-URLs handling below, same as when no focus session is running.
            if ((appSettings.isFocusModeRunning && !appSettings.isFocusModePaused) || appSettings.bHabitwithUseFocusModeRunning || appSettings.IsBlockingScheduleActiveAndNotPaused(out _))
            {
                ProcessURLForFocusModes(hwnd);
            }

            if (appSettings.bHabitwithAllowedAppsRunning || (appSettings.isFocusModeRunning && !appSettings.isFocusModePaused) || appSettings.IsBlockingScheduleActiveAndNotPaused(out _))
                return;

            if(appSettings.IsAppDeactivated())//no need to track always blocked urls
            {
                return;
            }

            if (appSettings.bIsUserOnCall && !appSettings.bBlockSuperDistractingSitesDuringMeetings)
            {
                LogWriter.LogWrite("User is on Call - bBlockSuperDistractingSitesDuringMeetings false, return");
                return;
            }

            if (appSettings.bBlockWhileOnbording) // Used only while onboarding
            {
                ProcessCurrentAlwaysBlockedURL(hwnd); //For always Blocked URLS
            }
            //else //Normal
            //{
            //    if (appSettings.arrAlwaysBlockedURLs.Count() > 0 ) //only block when untill I finish work is selected => updated on 22 Aug 2025 to show notification if user settings are set to manul blocking
            //        ProcessCurrentAlwaysBlockedURL(hwnd); //For always Blocked URLS
            //}

            //Finally check the url and if work focus mode has that alowed, suggest starting the work focsu mode
            //Only if its none
            if (appSettings.nCurrentActivityType == CurrentActivityType.NONE)
            {
                CheckAndSuggestWorkFocusMode(hwnd);
            }
            
        }

        private static readonly object NativeShadowLock = new object();
        private static long NativeShadowGeneration;
        private static string NativeShadowPendingKey;
        private static string LastNativeShadowKey;
        private static DateTime LastNativeShadowTimeUtc;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

        // Input repair for the native SHADOW check only. Keep the actual URL
        // for URL features and keep the original window title for deduplication.
        private static string CleanYouTubeShadowTitle(Uri url, string windowTitle)
        {
            string host = url.Host;
            bool youtube = string.Equals(host, "youtube.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "www.youtube.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "m.youtube.com", StringComparison.OrdinalIgnoreCase);
            bool supportedPage = string.Equals(url.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(url.AbsolutePath, "/results", StringComparison.OrdinalIgnoreCase);
            if (!youtube || !supportedPage)
                return null;

            string clean = windowTitle ?? string.Empty;
            clean = Regex.Replace(clean,
                @"\s+[-–—]\s+(Google Chrome|Microsoft Edge|Mozilla Firefox)\s*$",
                string.Empty, RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\s+[-–—]\s+YouTube\s*$",
                string.Empty, RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"^\s*\(\d+\)\s*", string.Empty);
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            // Do not turn an unready or generic page title into a blank input.
            if (clean.Length == 0 || string.Equals(clean, "YouTube", StringComparison.OrdinalIgnoreCase))
                return null;
            return clean;
        }

        private static bool IsUrlPlaceholderTitle(Uri currentUrl, string windowTitle)
        {
            string candidate = Regex.Replace(windowTitle ?? string.Empty,
                @"\s+[-–—]\s+(Google Chrome|Microsoft Edge|Mozilla Firefox)\s*$",
                string.Empty, RegexOptions.IgnoreCase);
            candidate = Regex.Replace(candidate, @"^\s*\(\d+\)\s*", string.Empty).Trim();
            candidate = Regex.Replace(candidate, @"\s+[-–—]\s+YouTube\s*$",
                string.Empty, RegexOptions.IgnoreCase).Trim();
            Uri titleUrl;
            if (candidate.Length == 0 || candidate.Any(char.IsWhiteSpace) ||
                !Uri.TryCreate(candidate.Contains("://") ? candidate : "https://" + candidate,
                    UriKind.Absolute, out titleUrl) ||
                (titleUrl.Scheme != Uri.UriSchemeHttp && titleUrl.Scheme != Uri.UriSchemeHttps))
                return false;
            // Ignore only the scheme; keep path/query case and values intact.
            return string.Equals(titleUrl.Authority, currentUrl.Authority,
                       StringComparison.OrdinalIgnoreCase) &&
                string.Equals(titleUrl.PathAndQuery, currentUrl.PathAndQuery,
                       StringComparison.Ordinal) &&
                string.Equals(titleUrl.Fragment, currentUrl.Fragment, StringComparison.Ordinal);
        }

        private static void QueueNativeWebsiteShadowCheck(IntPtr hWnd, string url)
        {
            try
            {
                AppSettings settings = AppSettings.GetInstance;
                var app = System.Windows.Application.Current as App;
                var classifier = app == null ? null : app.NativeRelevanceClassifier;
                if (classifier == null)
                    return;

                Uri parsedUrl;
                string rawUrl = (url ?? string.Empty).Trim();
                if (rawUrl.Length == 0 ||
                    !Uri.TryCreate(rawUrl.Contains("://") ? rawUrl : "https://" + rawUrl,
                        UriKind.Absolute, out parsedUrl) ||
                    (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
                    return;

                string goal = ((settings.sRunningFocusModeIntention ?? string.Empty) + " " +
                    (settings.sCurrentTaskPlayingInToDoPlayer ?? string.Empty)).Trim();
                if (goal.Length == 0 || string.Equals(goal,
                    "What do you want to achieve in this focus session?", StringComparison.OrdinalIgnoreCase))
                    return;

                string pendingKey = goal + "\n" + parsedUrl.AbsoluteUri;
                long generation;
                lock (NativeShadowLock)
                {
                    if (NativeShadowPendingKey == pendingKey)
                        return;
                    generation = ++NativeShadowGeneration;
                    NativeShadowPendingKey = pendingKey;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        // Navigation and title updates arrive separately. This asynchronous
                        // settling delay is excluded from classifier prediction latency.
                        await Task.Delay(1500).ConfigureAwait(false);
                        string previousTitle = null;
                        string title = null;
                        bool stable = false;
                        for (int attempt = 0; attempt < 7; attempt++)
                        {
                            lock (NativeShadowLock)
                            {
                                if (generation != NativeShadowGeneration)
                                    return;
                            }
                            if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
                                return;

                            bool validSnapshot = false;
                            app.Dispatcher.Invoke((Action)(() =>
                            {
                                IntPtr root = GetAncestor(hWnd, 2); // GA_ROOT
                                if (root == IntPtr.Zero) root = hWnd;
                                if (GetForegroundWindow() != root)
                                    return;
                                string currentGoal = ((settings.sRunningFocusModeIntention ?? string.Empty) + " " +
                                    (settings.sCurrentTaskPlayingInToDoPlayer ?? string.Empty)).Trim();
                                bool active = (settings.isFocusModeRunning && !settings.isFocusModePaused) ||
                                    settings.bHabitwithUseFocusModeRunning ||
                                    (settings.isBlockingScheduleRunning && settings.ActiveSchedule != null &&
                                     !settings.ActiveSchedule.isPaused);
                                string currentRawUrl = (settings.sLastActiveURLInFirefox ?? string.Empty).Trim();
                                Uri currentUrl;
                                if (!active || currentGoal != goal ||
                                    !Uri.TryCreate(currentRawUrl.Contains("://") ? currentRawUrl : "https://" + currentRawUrl,
                                        UriKind.Absolute, out currentUrl) ||
                                    currentUrl.AbsoluteUri != parsedUrl.AbsoluteUri)
                                    return;
                                var buffer = new StringBuilder(1024);
                                title = GetWindowText(root, buffer, buffer.Capacity) > 0
                                    ? buffer.ToString() : string.Empty;
                                validSnapshot = true;
                            }));
                            if (!validSnapshot)
                                return;
                            bool placeholder = string.IsNullOrWhiteSpace(title) ||
                                title.StartsWith("Untitled", StringComparison.OrdinalIgnoreCase) ||
                                title.StartsWith("New Tab", StringComparison.OrdinalIgnoreCase) ||
                                IsUrlPlaceholderTitle(parsedUrl, title);
                            if (!placeholder && title == previousTitle)
                            {
                                stable = true;
                                break;
                            }
                            previousTitle = title;
                            if (attempt < 6)
                                await Task.Delay(500).ConfigureAwait(false);
                        }
                        if (!stable)
                        {
                            Debug.WriteLine("[NativeRelevanceModel] WEBSITE SHADOW skipped: title not ready | URL=" + rawUrl);
                            return;
                        }
                        string key = goal + "\n" + rawUrl + "\n" + title;
                        lock (NativeShadowLock)
                        {
                            if (generation != NativeShadowGeneration ||
                                (key == LastNativeShadowKey &&
                                 DateTime.UtcNow - LastNativeShadowTimeUtc < TimeSpan.FromSeconds(30)))
                                return;
                        }
                        string keywords = string.Join(" ", parsedUrl.AbsolutePath
                            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(part => part.Length > 2 && !part.All(char.IsDigit)));
                        string cleanVideoTitle = CleanYouTubeShadowTitle(parsedUrl, title);
                        string modelTitle = cleanVideoTitle ?? title;
                        string context = cleanVideoTitle ?? (title + " " + keywords).Trim();
                        if (cleanVideoTitle != null)
                            Debug.WriteLine("[NativeRelevanceModel] WEBSITE SHADOW CONTEXT POLICY=YouTubeCleanTitle | " +
                                "RawTitle=" + title + " | ModelTitle=" + modelTitle);
                        Debug.WriteLine("[NativeRelevanceModel] WEBSITE SHADOW INPUT | UserGoal=" +
                            goal + " | URL=" + rawUrl + " | Title=" + title);
                        var stopwatch = Stopwatch.StartNew();
                        var result = classifier.Predict(goal, context, rawUrl, modelTitle);
                        stopwatch.Stop();
                        Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "[NativeRelevanceModel] WEBSITE SHADOW RESULT | Action={0} | Tier={1} | " +
                            "Confidence={2:F4} | Cosine={3:F4} | LLMFallback={4} | " +
                            "WebsiteLatencyMs={5:F2} | URL={6}",
                            result.Action, result.Tier, result.Confidence, result.CosineSimilarity,
                            result.LlmFallbackRequired, stopwatch.Elapsed.TotalMilliseconds, rawUrl));
                        lock (NativeShadowLock)
                        {
                            LastNativeShadowKey = key;
                            LastNativeShadowTimeUtc = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[NativeRelevanceModel] WEBSITE SHADOW failed: " + ex);
                    }
                    finally
                    {
                        lock (NativeShadowLock)
                        {
                            if (generation == NativeShadowGeneration)
                                NativeShadowPendingKey = null;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // Shadow diagnostics must not change existing blocking behaviour.
                Debug.WriteLine("[NativeRelevanceModel] WEBSITE SHADOW setup failed: " + ex);
            }
        }

        public static string ExtractOldUrl(string input)
        {
            // Regular expression to find the string between old_url= and &block_type=
            string pattern = @"[?&]old_url=([^&]+)";
            Match match = Regex.Match(input, pattern);

            if (match.Success)
            {
                return HttpUtility.UrlDecode(match.Groups[1].Value);
            }

            return null; // Return null if no match is found
        }

        //Navigate the foreground browser tab to a URL using the same address-bar navigation the block and
        //unblock flows use. Used to reload a now-allowed URL after the teacher approves it.
        public static void ReloadForegroundBrowserToUrl(IntPtr hWnd, string url)
        {
            AppSettings appSettings = AppSettings.GetInstance;
            try
            {
                if (appSettings.FirefoxLastAccWindow == null || string.IsNullOrEmpty(url))
                {
                    LogWriter.LogWrite("ReloadForegroundBrowserToUrl - no FirefoxLastAccWindow or empty url, cannot reload");
                    return;
                }
                LogWriter.LogWrite("ReloadForegroundBrowserToUrl - navigating browser to " + url);
                appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = url;
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                }
                appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection
                SendKeys.SendWait("{ENTER}");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReloadForegroundBrowserToUrl ex - " + ex.Message);
            }
        }

        public static bool ProcessCurrentAlwaysBlockedURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 ))
            {
                Console.WriteLine("MILA: - " + appSettings.sLastActiveURLInFirefox);
                //Check if the user is trying to unblock the URL that is blocked using Oops I actually need this
                string oldURLToOpen = AuthorisedUnblock(appSettings.sLastActiveURLInFirefox);
                if (oldURLToOpen != null)
                {
                    LogWriter.LogWrite("PBU.cs - ProcessCurrentAlwaysBlockedURL - URL = " + appSettings.sLastActiveURLInFirefox);

                    string sDomain = BlockPageUnblock.HostOf(oldURLToOpen) ?? "";
                    if (sDomain.Length > 0)
                    {

                        //Open the url and also add it to temporary allowed URL list
                        var blockedURLs = appSettings.TemporarryBlockedURLsWithSetIntention.Where(i => i.ToLower() == sDomain.ToLower());

                        if (blockedURLs.Count() != 0)
                        {
                            appSettings.TemporarryBlockedURLsWithSetIntention.RemoveAll(i => i.ToLower() == sDomain.ToLower()); //We need to remove all instanes
                        }

                        if(appSettings.bImmediatelyBlockDistractingSites)
                        {
                            //add a dummmy intention and allow for 12 hours
                            appSettings.TemporaryAllowedURLsWithSetIntention.Add(new DistractingURLAllowedWithIntention(sDomain.ToLower(), " ", DateTime.Now.AddMinutes(60*12), "720", false));
                        }

                        //Now fire the OLD url so that it will ask for intention again
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = oldURLToOpen;
                        SetForegroundWindow(hwnd);
                        Task.Delay(1);
                        appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection

                        SendKeys.SendWait("{ENTER}");

                        appSettings.CapturePostHog("unblock-distracting-url");
                    }
                }
                else if(appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))
                {
                    //return if its a normal block page
                    return false;
                }
                //Check if its an always unblocked url. One list, in FocusBear.Core: the two copies of
                //UNBLOCKED_URLS had drifted, and substring matching let any site opt out of blocking by
                //naming an exempt domain anywhere in its URL.
                if (UnblockedUrls.IsAlwaysUnblocked(appSettings.sLastActiveURLInFirefox))
                    {
                        return false;
                    }

                if (BrowserUrlEntries.Match(appSettings.sLastActiveURLInFirefox, appSettings.arrAlwaysBlockedURLs) != BrowserUrlEntryMatch.None)
                {
                    LogWriter.LogWrite(appSettings.sLastActiveURLInFirefox + " found in ALWAYS blocked URLS - BLOCK");
                    suppressURLForFirefox(hwnd, appSettings.sLastActiveURLInFirefox, "blocked");
                    return true;
                }
             }

             appSettings.Hide_ShowIntentionWindow();
             Console.WriteLine("Hide_ShowIntentionWindow from processbrowserurl");

             return false;
         }



         private static void ProcessURLForFocusModes(IntPtr hWnd )
         {
             AppSettings appSettings = AppSettings.GetInstance;

             // Teacher Mode forces non-cuddly (grizzly) behaviour, regardless of the user's own mode setting.
             bool bEffectiveCuddly = (appSettings.nFocusBearMode == 1) && !TeacherSessionManager.Instance.IsTeacherSessionActive();

             string sURL = "";
             if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 ))
             {
                 sURL = appSettings.sLastActiveURLInFirefox;
             }
             else
             {
                TabMetrics.Instance.CloseLastDomainIfStarted_Internal();

                if (bEffectiveCuddly && appSettings.bAiCheckUrlEnabled)
                 {
                     FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                     FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                 }
                 return;
             }

             //"focus_bear_temporarily_allow=true" is the version-less block page's unblock, which puts the parameter on
             //the blocked site's own URL - anyone could type it. This app's pages always carry a version, so theirs is the
             //tokenised unblock below and the old parameter is no longer honoured.

             //Check if the user is trying to unblock the URL that is blocked using Oops I actually need this
             string sAuthorisedOldUrl = AuthorisedUnblock(sURL);
             if (sAuthorisedOldUrl != null)
             {
                 LogWriter.LogWrite("PBU.cs - ProcessURLForFocusModes - URL = " + sURL);

                 //Teacher Mode - a class session's allow-list is controlled by the teacher, so the student
                 //must not directly unblock a site. Open the unlock-request window instead, pre-filled with
                 //the URL they were trying to reach so they don't have to retype it. This runs on the UI
                 //Automation event thread, so marshal the WPF window onto the UI dispatcher (BeginInvoke -
                 //do not block this thread), then stop the direct-unblock flow.
                 if (TeacherSessionManager.Instance.IsTeacherSessionActive())
                 {
                     string sUrlToRequest = sAuthorisedOldUrl;
                     LogWriter.LogWrite("PBU.cs - unblock blocked - teacher class session active, opening pre-filled unlock request for " + sUrlToRequest);

                     //Strip the "&unblock_it=true" trigger and reload the (clean) block-page URL BEFORE
                     //showing the request box, so the tab no longer carries the trigger. Otherwise, if the
                     //student cancels the request and re-focuses this tab, the box would keep re-appearing.
                     //Same address-bar navigation the normal unblock flow uses below.
                     try
                     {
                         string sCleanUrl = BlockPageUnblock.WithoutTrigger(sURL);
                         appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sCleanUrl;
                         SetForegroundWindow(hWnd);
                         Task.Delay(1);
                         appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection
                         SendKeys.SendWait("{ENTER}");

                         //Teacher Mode - this is the reliable capture point for the reload-on-approval: we
                         //know the exact URL the student is requesting. Remember it, and set the current tab
                         //to the clean block-page URL (the block page's address bar reads empty otherwise) so
                         //the same-tab check on approval can confirm the student is still on this block page.
                         appSettings.sLastFocusBlockedUrl = sUrlToRequest;
                         appSettings.sLastActiveURLInFirefox = sCleanUrl;
                         LogWriter.LogWrite("PBU.cs - teacher unblock - captured requested url '" + sUrlToRequest + "' on block page '" + sCleanUrl + "'");
                     }
                     catch (Exception exReload)
                     {
                         LogWriter.LogWrite("PBU.cs - teacher unblock reload ex - " + exReload.Message);
                     }

                     System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                     {
                         TeacherSessionManager.Instance.ShowTeacherUnlockRequestWindow(sUrlToRequest);
                     }));
                     return;
                 }

                 string oldURLToOpen = sAuthorisedOldUrl;

                 if (oldURLToOpen.Length > 0)
                 {
                     if(appSettings.isFocusModeRunning && !appSettings.isFocusModePaused)
                     {
                        //check if url is listed as blocked url then do not allow to open and show message: 
                        //"common""unblockNotAvailableForFocusBlockedUrlText": "You can't unblock this site because it's currently listed under the blocked URLs for your focus session",
                        if(CheckIfURLIsInBlockedURLs(oldURLToOpen))
                        {

                            string sTemp = appSettings.ConfigFileStrings["common"]["unblockNotAvailableForFocusBlockedUrlText"].ToString();
                            appSettings.ShowBaloonTip(sTemp);
                            return;
                        }
                     }
                     //Open the url and also add it to temporary allowed URL list
                     var blockedURLs = appSettings.TemporarryBlockedURLsGentleBlock.Where(i => i.ToLower() == oldURLToOpen.ToLower());

                     if (blockedURLs.Count() != 0)
                     {
                        LogWriter.LogWrite("PBU.cs - ProcessURLForFocusModes - URL found in TemporarryBlockedURLsGentleBlock, removing it = " + sURL);

                        appSettings.TemporarryBlockedURLsGentleBlock.RemoveAll(i => i.ToLower() == oldURLToOpen.ToLower()); //We need to remove all instanes

                        //If found in this, just unblock it and return from here / normal flow as a new url will follow
                        //Now fire the OLD url so that it will ask for intention again
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = oldURLToOpen;
                        SetForegroundWindow(hWnd);
                        Task.Delay(1);
                        appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection

                        SendKeys.SendWait("{ENTER}");
                        return;
                     }

 
                     /////////////////////////////////////////////////////////////////////////
                     //Might be possible that it was an always blocked URL
                     //The blocked URL's host (the old fallback parsed the block page's own URL, and could throw).
                     string sActiveURL = BlockPageUnblock.HostOf(oldURLToOpen) ?? oldURLToOpen;

                    var AlwaysBlockedURLsAllowed = appSettings.TemporarryBlockedURLsWithSetIntention.Where(i => i.ToLower() == sActiveURL.ToLower());
                    if (AlwaysBlockedURLsAllowed.Count() != 0)
                    {
                        appSettings.TemporarryBlockedURLsWithSetIntention.RemoveAll(i => i.ToLower() == sActiveURL.ToLower()); //We need to remove all instanes
                    }
                    //For supporting unblock, we should add it to blocked array with dummy intention
                    // appSettings.TemporaryAllowedURLsWithSetIntention.Add(new DistractingURLAllowedWithIntention(sActiveURL.ToLower(), " ", DateTime.Now.AddMinutes(60 * 12), "720", false));
                    //Ask for intention flow
                    //Check the URL in Always BLocked URL List
                    {
                        if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                        {
                            if (ProcessCurrentAlwaysBlockedURL(hWnd))//For always Blocked URLS
                            {
                                return; //return as this is handled byy always blocked url
                            }
                        }
                    }
                    /////////////////////////////////////////////////////////////////////////
                    ///

                    //Now fire the OLD url so that it will ask for intention again
                    appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sActiveURL;
                    SetForegroundWindow(hWnd);
                    Task.Delay(1);
                    appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection

                    SendKeys.SendWait("{ENTER}");

                    return;
                }
            }
            else if (appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))
            {
                //return if its a normal block page
                return ;
            }
            if (sURL.Contains("file://"))
            {
                if (bEffectiveCuddly && appSettings.bAiCheckUrlEnabled)
                {
                    FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                }

                return;
            }
            //Check if its an always unblocked url. One list, in FocusBear.Core: the two copies of
            //UNBLOCKED_URLS had drifted, and substring matching let any site opt out of blocking by
            //naming an exempt domain anywhere in its URL.
            if (UnblockedUrls.IsAlwaysUnblocked(sURL))
                {
                    TabMetrics.Instance.CloseLastDomainIfStarted_Internal();

                    if (bEffectiveCuddly && appSettings.bAiCheckUrlEnabled)
                    {
                        FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                    }
                    return;
                }

            TabMetrics.Instance.FrontDomainChanged(sURL);

            // Observe ordinary websites before allow-list/cached decisions return.
            // Queue a settled URL/title snapshot, then run native inference in the background.
            QueueNativeWebsiteShadowCheck(hWnd, sURL);

            FocusAlignmentScoreManager.FocusModeOpenCurrentUrlForAlignmentScore(sURL);

            bool bAllowedURLFound = true; //by default allowed url is true

            List<FocusMode> listItems = new List<FocusMode>();

            if (appSettings.bHabitwithUseFocusModeRunning)
            {
                listItems = appSettings.arrFocusModes.Where(i => i.id == appSettings.sUseFocusModeIDInActivity).ToList();
            }
            else if(appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForFocusMode)) // Blocking schedule
            {
                listItems = appSettings.arrFocusModes.Where(i => i.id == scheduleForFocusMode.SelectedFocusModeId).ToList();
            }
            else //Normal focus mode running
            {
                listItems = appSettings.arrFocusModes.Where(i => i.isOn == true).ToList();
            }

            if (listItems.Count() != 0)
            {
                FocusMode focusType = listItems.First();
                
                if(focusType.isRelaxFocus)
                {
                    //if its relax focus mode, we will not block anything, so return from here
                    return;
                }

                if (focusType.isDefault)
                {
                    //if its default focus mode, we will keep it false
                    focusType.isAllowedListApproach = false;
                    focusType.arrBlockedUrls.Clear(); //We just rely on the Always blocked sites
                    focusType.arrAllowedUrls.Clear();
                }

                if (focusType != null )
                {
                    if (focusType.isAllowedListApproach)//Block everything except safe sites I choose - Allow List
                    {
                        LogWriter.LogWrite("isAllowedListApproach - Block everything except safe sites I choose - " + focusType.name);

                        {
                            LogWriter.LogWrite("isAllowedListApproach - Selected URLs are allowed");
                            //We will first check if the url is in the blocked urls list of the focusmode, then we will not check allowed
                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                            //check if the current URL is in the list of Blocked urls, then block else allow
                            BrowserUrlEntryMatch blockedMatch = BrowserUrlEntries.Match(sURL, focusType.arrBlockedUrls, () => ReadWindowTitle(hWnd));
                            if (blockedMatch != BrowserUrlEntryMatch.None)
                            {
                                LogWriter.LogWrite(sURL + " found in blocked URLS of focus mode (" + blockedMatch + " match) - BLOCK");
                                bAllowedURLFound = false;
                            }
                            bool bBlockImmediately = false;

                            if (!bAllowedURLFound)
                            {
                                bBlockImmediately = true;
                            }

                            if (bAllowedURLFound)//Means it was not found in the blocked urls..now let's check Allowed URLs
                            {
                                //check if the current URL is in the list of allowed urls, then allow else block
                                BrowserUrlEntryMatch allowedMatch = BrowserUrlEntries.Match(sURL, focusType.arrAllowedUrls, () => ReadWindowTitle(hWnd));
                                bAllowedURLFound = allowedMatch != BrowserUrlEntryMatch.None;
                                if (bAllowedURLFound)
                                {
                                    LogWriter.LogWrite(sURL + " found in ALLOWED URLS of focus mode (" + allowedMatch + " match) - ALLOW");
                                    if (appSettings.arrAlwaysBlockedURLs.Count > 0 &&
                                        ProcessCurrentAlwaysBlockedURL(hWnd))
                                    {
                                        return;
                                    }

                                    string sHeading = appSettings.ConfigFileStrings["common"]["appAllowedInFocusModeText"].ToString();
                                    sHeading = sHeading.Replace("<APP>", sURL);
                                    sHeading = sHeading.Replace("<FOCUSMODE>", appSettings.sRunningFocusModeName);
                              
                                    //Safe URL, need to allow.
                                    if (!FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.ContainsKey(sURL))
                                    {

                                        FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sURL] = new Dictionary<string, object>
                                        {
                                            { "probability", 1.0 },
                                            { "isVisibleUrl", true },
                                            { "totalSeconds", 0 },
                                            { "startedTime", DateTime.Now },
                                            { "reason", sHeading }
                                        };

                                        FocusAlignmentScoreManager.CustomToolTip = sHeading;
                                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(1);
                                    }

                                    double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                                    Console.WriteLine("*****************************currentFocusAlignmentScore");
                                    Console.WriteLine(currentFocusAlignmentScore);

                                    Task.Delay(1000);
                                    return;
                                }
                            }

                            if (!bAllowedURLFound)
                            {
                                LogWriter.LogWrite("PBU processCurrentFocusElement  isAllowedListApproach - Attempt to Suppress URL");

                                //Check the URL in Always BLocked URL List
                                {
                                    if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                                    {
                                        if (ProcessCurrentAlwaysBlockedURL(hWnd))//For always Blocked URLS
                                        {
                                            return; //return as this is handled byy always blocked url
                                        }
                                    }
                                }

                                suppressURLForFirefox(hWnd, sURL, "focus", null, bBlockImmediately);
                                return;
                            }
                        }
                    }
                    else //Allow everything except districting sites
                    {
                        LogWriter.LogWrite("isBlockedListApproach - Allow everything except districting sites");

                        {
                            LogWriter.LogWrite("isBlockedListApproach - selected URLS are blocked");

                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                            //check if the current URL is in the list of Blocked urls, then block else allow
                            BrowserUrlEntryMatch blockedMatch = BrowserUrlEntries.Match(sURL, focusType.arrBlockedUrls, () => ReadWindowTitle(hWnd));
                            if (blockedMatch != BrowserUrlEntryMatch.None)
                            {
                                LogWriter.LogWrite(sURL + " (BLocked List APproach) found in blocked URLS of focus mode (" + blockedMatch + " match) - BLOCK");
                                bAllowedURLFound = false;
                            }
                            if (!bAllowedURLFound)
                            {
                                LogWriter.LogWrite("processCurrentFocusElement isBlockedListApproach - Attempt to Suppress URL");
                                suppressURLForFirefox(hWnd, sURL, "focus", focusType.name, true);
                               
                                return;
                            }
                            else //Check the URL in Always BLocked URL List
                            {
                                if (appSettings.arrAlwaysBlockedURLs.Count() > 0 &&
                                    ProcessCurrentAlwaysBlockedURL(hWnd))
                                {
                                    // if returned true means it was blocked by always blocked url
                                    return;
                                }

                                string sHeading = appSettings.ConfigFileStrings["common"]["appAllowedInFocusModeText"].ToString();
                                sHeading = sHeading.Replace("<APP>", sURL);
                                sHeading = sHeading.Replace("<FOCUSMODE>", appSettings.sRunningFocusModeName);

                                //Safe URL, need to allow.
                                if (!FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.ContainsKey(sURL))
                                {

                                    FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sURL] = new Dictionary<string, object>
                                            {
                                                { "probability", 1.0 },
                                                { "isVisibleUrl", true },
                                                { "totalSeconds", 0 },
                                                { "startedTime", DateTime.Now },
                                                { "reason", sHeading }
                                            };

                                    FocusAlignmentScoreManager.CustomToolTip = sHeading;
                                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(1);
                                }

                                double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                                Console.WriteLine("*****************************currentFocusAlignmentScore");
                                Console.WriteLine(currentFocusAlignmentScore);

                                Task.Delay(1000);
                                return;
                            }
                        }
                    }
                }
            }

        }

        private static bool CheckIfURLIsInBlockedURLs(string sURL)
        {
            LogWriter.LogWrite("CheckIfURLIsInBlockedURLs() - ");
            AppSettings appSettings = AppSettings.GetInstance;

            List<FocusMode> listItems = new List<FocusMode>();

            if (appSettings.bHabitwithUseFocusModeRunning)
            {
                listItems = appSettings.arrFocusModes.Where(i => i.id == appSettings.sUseFocusModeIDInActivity).ToList();
            }
            else if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForFocusMode2)) // Blocking schedule
            {
                listItems = appSettings.arrFocusModes.Where(i => i.id == scheduleForFocusMode2.SelectedFocusModeId).ToList();
            }
            else //Normal focus mode running
            {
                listItems = appSettings.arrFocusModes.Where(i => i.isOn == true).ToList();
            }

            if (listItems.Count() != 0)
            {
                FocusMode focusType = listItems.First();

                if (focusType != null)
                {
                    LogWriter.LogWrite("isAllowedListApproach - Selected URLs are allowed");
                    //We will check if the url is in the blocked urls list of the focusmode 
                    if (BrowserUrlEntries.Match(sURL, focusType.arrBlockedUrls) != BrowserUrlEntryMatch.None)
                    {
                        LogWriter.LogWrite(sURL + " found in blocked URLS of focus mode - do not UNBLOCK");
                        return true;
                    }
                }
            }
            return false;
        }
        private static void ProcessAlloweURLsInActivity(IntPtr hWnd )
        {
            try
            {
                AppSettings appSettings = AppSettings.GetInstance;

                string sURL = "";
                if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 && (!appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))))
                {
                    sURL = appSettings.sLastActiveURLInFirefox;
                }
                else
                {
                    return;
                }

                {
                    if (sURL.Contains("file://"))
                    {
                        return;
                    }
                    //Check if its an always unblocked url. One list, in FocusBear.Core: the two copies of
                    //UNBLOCKED_URLS had drifted, and substring matching let any site opt out of blocking by
                    //naming an exempt domain anywhere in its URL.
                    if (UnblockedUrls.IsAlwaysUnblocked(sURL))
                        {
                            return;
                        }

                    // The running habit's or break activity's sites, bounds-checked (RunningActivityRules): an index out of
                    // range used to throw here, and the catch below then blocked nothing. Unknown rules allow nothing.
                    string sHabitType = appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ? "morning-habit"
                        : appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ? "evening-habit"
                        : appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY ? "break-activity" : "";
                    var rules = RunningActivityRules.For(appSettings);
                    if (rules.Label.Length > 0 && rules.Known)
                    {
                        BrowserUrlEntryMatch allowedMatch = BrowserUrlEntries.Match(sURL, rules.Urls, () => ReadWindowTitle(hWnd));
                        if (allowedMatch != BrowserUrlEntryMatch.None)
                        {
                            LogWriter.LogWrite(sURL + " found in ALLOWED URLS of " + rules.Label + " (" + allowedMatch + " match) - ALLOW");
                            return;
                        }
                    }
                    else if (rules.Label.Length > 0)
                    {
                        LogWriter.LogWrite("PBU - the running " + rules.Label + " activity's rules could not be found (index " + appSettings.nHabitIndex + ")");
                    }

                    // Every allowed match returned above, so reaching here means nothing allowed it.
                    LogWriter.LogWrite(sURL + "PBU - BLOCKED URL found in activity - BLOCK");
                    // "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={0}"; //evening-habit, morning-habit, break-activity
                    suppressURLForFirefox(hWnd, sURL, sHabitType);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAlloweURLsInActivity - " + ex.Message);
            }


        }

        private static void CheckAndSuggestWorkFocusMode(IntPtr hWnd)
        {
            LogWriter.LogWrite("CheckAndSuggestWorkFocusMode () ");

            AppSettings appSettings = AppSettings.GetInstance;

            string sURL = "";
            if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0))
            {
                sURL = appSettings.sLastActiveURLInFirefox;
            }
            else
            {
                return;
            }

            if (sURL.Contains("focus_bear_temporarily_allow=true"))//If user have clicked I need this URL from blocked page, ignore it
            {
                return;
            }

            //Check if the user is trying to unblock the URL that is blocked using Oops I actually need this
            if (sURL.Contains("&unblock_it=true"))
            {
                return;
            }
            else if (appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))
            {
                //return if its a normal block page
                return;
            }
            if (sURL.Contains("file://"))
            {
                return;
            }
            //Check if its an always unblocked url. One list, in FocusBear.Core: the two copies of
            //UNBLOCKED_URLS had drifted, and substring matching let any site opt out of blocking by
            //naming an exempt domain anywhere in its URL.
            if (UnblockedUrls.IsAlwaysUnblocked(sURL))
                {
                    return;
                }

            if (appSettings.arrFocusModes.Count() != 0) //will work with any focus mode
            {
                foreach (FocusMode focusType in appSettings.arrFocusModes)
                {
                    if (focusType != null && !focusType.bUserClickedNoDoNotSuggestAnymore && focusType.AutoSuggestFocusModeOnAllowedURL)
                    {
                        if (focusType.isAllowedListApproach)//Block everything except safe sites I choose
                        {
                            LogWriter.LogWrite("isAllowedListApproach - Block everything except safe sites I choose - " + focusType.name);


                            //suggest the mode when the current URL is on its allowed list
                            if (BrowserUrlEntries.Match(sURL, focusType.arrAllowedUrls, () => ReadWindowTitle(hWnd)) != BrowserUrlEntryMatch.None)
                            {
                                LogWriter.LogWrite(sURL + " found in ALLOWED URLS of " + focusType.name + " - suggest starting it");
                                appSettings.ShowSuggestStartWorkFocusModeWindow(hWnd, focusType);
                                return;
                            }

                        }
                        else //Allow everything except districting sites
                        {
                            LogWriter.LogWrite("isBlockedListApproach - do not check to suggest");
                           
                        }
                    }
                }
            }

        }
        private const int WindowTitleMaxChars = 256;

        /// <summary>
        /// The browser window's title for the entry-in-title fallback, or null when it cannot be read.
        /// A failure here must not decide anything, so it reads as "no title" rather than throwing.
        /// </summary>
        private static string ReadWindowTitle(IntPtr hWnd)
        {
            try
            {
                StringBuilder title = new StringBuilder(WindowTitleMaxChars);
                return GetWindowText(hWnd, title, WindowTitleMaxChars) > 0 ? title.ToString() : null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PBU ReadWindowTitle EX - " + ex.Message);
                return null;
            }
        }

        public static void suppressURLForFirefox(IntPtr hWnd, string val, string type, string focusMode = null, bool bBlockImmediately = false)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            // Teacher Mode forces non-cuddly (grizzly) behaviour for the whole focus-block flow,
            // regardless of the user's own Focus Bear mode setting.
            bool bTeacherActive = TeacherSessionManager.Instance.IsTeacherSessionActive();
            bool bEffectiveCuddly = (appSettings.nFocusBearMode == 1) && !bTeacherActive;

            // One ladder, in FocusBear.Core: snooze > teacher > buddy > the block list's
            // is_ai_enabled > the global toggle. This used to be hand-rolled here and skipped
            // the block list, so the Mac's is_ai_enabled=false had no effect on Windows.
            bool bAiCheckEnabled = AiBlocking.IsEnabled();

            try
            {
                LogWriter.LogWrite("PBU - suppressURLForFirefox - Block URL - " + val);


                if (type == "focus")
                {

                    // Cuddly → gentle-block/AI flow (asks the user before blocking).
                    // Non-cuddly (incl. Teacher Mode) WITH the AI check enabled → same allow-list checks, but
                    // GentleBlock blocks immediately on an AI-negative verdict (no reasons/questions asked).
                    // Non-cuddly WITHOUT the AI check falls through to the hard block below.
                    if (!bBlockImmediately && (bEffectiveCuddly || bAiCheckEnabled))//Cuddly bearMode, or non-cuddly with AI check, and not bFocusModeBlockedListApproach
                    {
                        string sURLForFocusMode = "";
                        sURLForFocusMode = val;
                      
                        //Check if the user had allowed the entire domain
                        var domainAllowed = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.BAllowDomain == true);
                        if (domainAllowed.Count() != 0)
                        {
                            //here we need to check the domain of the url in question
                            sURLForFocusMode = BlockPageUnblock.HostOf(val) ?? "";   //never throws (the UriBuilder fallback could)

                            var allowedURLs = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.sURL.ToLower() == sURLForFocusMode.ToLower());
                            if (allowedURLs.Count() != 0)
                            {
                                //found in allowed url
                                //if found in allowed URLs then add the title of what ever tab that is opened to the keywords list to send to server
                                if (hWnd != IntPtr.Zero)
                                {
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(hWnd, Buff, nChars) > 0)
                                        {
                                            string sTitle = Buff.ToString();

                                            sTitle = sTitle.Replace(" - Google Chrome", "");
                                            sTitle = sTitle.Replace(" — Mozilla Firefox", "");
                                            sTitle = sTitle.Replace(" - Microsoft? Edge", "");
                                            sTitle = sTitle.Replace(" - Vivaldi", "");
                                            sTitle = sTitle.Replace(" - Comet", "");

                                            if (sTitle.Length > 0)
                                            {
                                                var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sTitle.ToLower());
                                                if (titleExists.Count() == 0)
                                                {
                                                    //add to list
                                                    appSettings.TemporaryAllowedURLsTabTitles.Add(sTitle);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("suppressURLForFirefox GetWindowText blockedurls EX - " + ex.Message);
                                    }
                                }
                                LogWriter.LogWrite("Found in appSettings.TemporaryAllowedURLsGentleBlock - returning - " + sURLForFocusMode);
                                return;//allow the url
                            }

                        }

                        ///
                        //If a URL was temporarily allowed in the focus mode, we will check that URL if in allowed array and allow it for respective time
                        //30 May 2026
                        var allowedTempURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sURLForFocusMode.ToLower());
                        if (allowedTempURLs.Count() != 0)
                        {
                            //found in allowed url
                            if (DateTime.Now < allowedTempURLs.First().EndTime)
                            {
                                return;//allow the url
                            }
                        }
                        ///

                        //if the control is not returned from above, we still need to check the full urls
                        {

                            var allowedURLs = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.sURL.ToLower() == sURLForFocusMode.ToLower());
                            if (allowedURLs.Count() != 0)
                            {
                                //found in allowed url
                                //if found in allowed URLs then add the title of what ever tab that is opened to the keywords list to send to server
                                if (hWnd != IntPtr.Zero)
                                {
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(hWnd, Buff, nChars) > 0)
                                        {
                                            string sTitle = Buff.ToString();

                                            sTitle = sTitle.Replace(" - Google Chrome", "");
                                            sTitle = sTitle.Replace(" — Mozilla Firefox", "");
                                            sTitle = sTitle.Replace(" - Microsoft? Edge", "");
                                            sTitle = sTitle.Replace(" - Vivaldi", "");
                                            sTitle = sTitle.Replace(" - Comet", "");

                                            if (sTitle.Length > 0)
                                            {
                                                var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sTitle.ToLower());
                                                if (titleExists.Count() == 0)
                                                {
                                                    //add to list
                                                    appSettings.TemporaryAllowedURLsTabTitles.Add(sTitle);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("suppressURLForFirefox GetWindowText blockedurls EX - " + ex.Message);
                                    }
                                }
                                LogWriter.LogWrite("Found in appSettings.TemporaryAllowedURLsGentleBlock - returning - " + sURLForFocusMode);
                                return;//allow the url
                            }
                            else
                            {
                                var blockedURLs = appSettings.TemporarryBlockedURLsGentleBlock.Where(i => i.ToLower() == sURLForFocusMode.ToLower());

                                if (blockedURLs.Count() != 0)
                                {
                                    //found in blocked urls..block
                                }
                                else
                                {
                                    string sTitle = "";
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(hWnd, Buff, nChars) > 0)
                                        {
                                            sTitle = Buff.ToString();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("suppressURLForFirefox 1 GetWindowText blockedurls EX - " + ex.Message);
                                    }

                                    appSettings.ShowGentleBlockWindow(hWnd, "", val, sTitle); //we will send full url here
                                    return;
                                }
                            }
                        }
                    }


                    if (appSettings.bHabitwithUseFocusModeRunning)
                    {
                        //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                        string sURL = string.Format(sActivityBlockedWebUrl, HttpUtility.UrlEncode(val), appSettings.sHabitTypeForUseFocusModeIDInActivity, appSettings.AppVersion.ToString());
                        if (bEffectiveCuddly)//cuddly mode
                            sURL = sURL + "&cuddly_bear_mode=true";
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = Stamped(sURL, val);

                    }
                    else
                    {
                        string sEndTime = null;

                        if (DateTime.TryParse(appSettings.sRunningFocusModeEndTime,
                                              out DateTime endT))
                        {
                            sEndTime = endT.ToUniversalTime().ToString("o");
                        }
                        else
                        {
                            // handle invalid value
                            sEndTime = "";
                        }

                        if(appSettings.TryGetActiveSchedule(out var scheduleForEndTime))
                        {
                            if (DateTime.TryParse(scheduleForEndTime.EndTimeValue,
                                              out DateTime endTBS))
                            {
                                sEndTime = endTBS.ToUniversalTime().ToString("o");
                            }
                            else
                            {
                                // handle invalid value
                                sEndTime = "";
                            }
                        }

                        string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, HttpUtility.UrlEncode(val), HttpUtility.UrlEncode(sEndTime), HttpUtility.UrlEncode(appSettings.sRunningFocusModeName.Length > 0 ? appSettings.sRunningFocusModeName : focusMode), HttpUtility.UrlEncode(appSettings.sRunningFocusModeIntention ?? ""), appSettings.AppVersion.ToString());
                        // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";

                        if (bEffectiveCuddly)//cuddly mode
                            sFcousBlockedUrl = sFcousBlockedUrl + "&cuddly_bear_mode=true";

                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = Stamped(sFcousBlockedUrl, val);

                    }

                }
                else if (type == "blocked")
                {
                    /**if(!appSettings.isFocusModeRunning)
                    {
                        /*(Focus - Bear / windows - app - v2#783)
                        @manishjain999 can you also show a notification in the floating bear: "Super distracting site not blocked ℹ️" with tooltip "Your blocking setting is set to manual and you're not currently in a focus session"* /
                        
                        string sHeading = "Super distracting site not blocked!"; ///appSettings.ConfigFileStrings["common"]["appBlockNotificationTitle"].ToString();
                        string sDesc = "ℹ️ Your blocking setting is set to manual and you're not currently in a focus session."; ///appSettings.ConfigFileStrings["common"]["appBlockNotificationTitle"].ToString();
                        appSettings.ShowBaloonTip(sDesc, sHeading);

                        return;
                    }**/

                    // A teacher class session forces immediate blocking of super-distracting sites,
                    // regardless of the student's own "Immediately block super distracting sites"
                    // setting - teacher sessions already run non-cuddly/grizzly, and a student shouldn't
                    // be able to negotiate a delay via the Set-Intention flow during a class.
                    if (appSettings.bImmediatelyBlockDistractingSites || TeacherSessionManager.Instance.IsTeacherSessionActive()) //BLock immediately
                    {
                        string sDomain = "";
                        /// //here we need to check the domain of the url in question
                        sDomain = BlockPageUnblock.HostOf(val) ?? "";   //never throws (the UriBuilder fallback could)

                        var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sDomain.ToLower());
                        if (allowedURLs.Count() != 0)
                        {
                            //found in allowed url
                            return;//allow the unblocked url
                        }
                        else
                        {
                            appSettings.nTotalDistractingSitesBlocked++;

                            //For supporting unblock, we should add it to blocked array with dummy intention
                            appSettings.TemporarryBlockedURLsWithSetIntention.Add(sDomain.ToLower()); //add the domain only

                            appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = Stamped(string.Format(sAlwaysBlockedWebUrl, HttpUtility.UrlEncode(val), bEffectiveCuddly ? "cuddly" : "grizzly", appSettings.AppVersion.ToString()), val); //1 is cuddly bear mode
                        }

                        
                    }
                    else //show Set intention box
                    {
                        string sURLForSetIntention = BlockPageUnblock.HostOf(val) ?? "";

                        var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sURLForSetIntention.ToLower());
                        if (allowedURLs.Count() != 0)
                        {
                            //found in allowed url
                            //if the intention box is not closed for this URL, show it
                            //we will also need to check if the time for this url is elapsed, then we need to ask interntion and confirmation again
                            if (DateTime.Now > allowedURLs.First().EndTime)
                            {
                                appSettings.ShowSetIntentionWindow(hWnd, "", val, "", true); //we will send full url here
                            }
                            else if (!allowedURLs.First().bIsShowIntentionWindowClosedForThisURL)
                            {
                                appSettings.Show_ShowIntentionWindow(hWnd, val, allowedURLs.First().sIntention);
                            }
                            else
                            {
                                appSettings.Hide_ShowIntentionWindow();
                                Console.WriteLine("Hide_ShowIntentionWindow from else of TemporaryAllowedURLsWithSetIntention");
                            }
                            return;//allow the url
                        }
                        else
                        {
                            var blockedURLs = appSettings.TemporarryBlockedURLsWithSetIntention.Where(i => i.ToLower() == sURLForSetIntention.ToLower());

                            if (blockedURLs.Count() != 0)
                            {
                                //found in blocked urls..block
                                appSettings.nTotalDistractingSitesBlocked++;

                                appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = Stamped(string.Format(sAlwaysBlockedWebUrl, HttpUtility.UrlEncode(val), bEffectiveCuddly ? "cuddly" : "grizzly", appSettings.AppVersion.ToString()), val); //1 is cuddly bear mode
                            }
                            else
                            {
                                appSettings.ShowSetIntentionWindow(hWnd, "", val, ""); //we will send full url here
                                return;
                            }
                        }
         
                    }
                }
                else
                {
                    //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                    appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = Stamped(string.Format(sActivityBlockedWebUrl, HttpUtility.UrlEncode(val), type, appSettings.AppVersion.ToString()), val);
                }
                               
                SetForegroundWindow(hWnd);
                Task.Delay(1); 
                appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection
                
                
                SendKeys.SendWait("{ENTER}");

                // type values come from suppressURLForFirefox callers: "blocked", "focus", or one of
                // the habit types ("morning-habit" / "evening-habit" / "break-activity").
                string blockReason;
                switch (type)
                {
                    case "blocked":
                        blockReason = "super_distracting_url";
                        break;
                    case "focus":
                        // A non-immediate focus block that went through the AI path (cuddly, or non-cuddly with the
                        // AI check enabled) is AI blocking; a straight blocked-list / non-AI block is not.
                        blockReason = (!bBlockImmediately && (bEffectiveCuddly || bAiCheckEnabled))
                            ? "ai_blocking" : "block_in_focus_mode";
                        break;
                    case "morning-habit":
                    case "evening-habit":
                    case "break-activity":
                        blockReason = "block_in_habit";
                        break;
                    default:
                        blockReason = "unknown";
                        break;
                }
                string blockMeta = $"\"block-reason\":\"{blockReason}\"";
                if (blockReason == "ai_blocking" || blockReason == "block_in_focus_mode")
                {
                    string blockMode;
                    if (appSettings.TryGetActiveSchedule(out var scheduleForBlockMode))
                    {
                        if (scheduleForBlockMode.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
                            blockMode = "super_strict";
                        else if (scheduleForBlockMode.BlockLevel == FOCUSMODESTRICTNESS.STRICT)
                            blockMode = "strict";
                        else
                            blockMode = "normal";
                    }
                    else
                    {
                        blockMode = bEffectiveCuddly ? "normal" : "strict";
                    }
                    blockMeta += $",\"block-mode\":\"{blockMode}\"";
                }
                appSettings.CapturePostHog("block-distracting-url", blockMeta);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("suppressURLForFirefox EX -" + ex.Message);

                //Fallback for Opera
                //if (appSettings.sLastActiveAppName == "opera.exe")
                //{    // Focus Opera
                //    SetForegroundWindow(hWnd);

                //    // Ctrl+L to focus the address bar
                //    SendKeys.SendWait("^{l}");

                //    // Replace with your URL
                //    string sTemp = string.Format(sActivityBlockedWebUrl, val, type, appSettings.AppVersion.ToString()) + "{ENTER}";
                //    SendKeys.SendWait(sTemp);
                //}
            }


        }
    }
}