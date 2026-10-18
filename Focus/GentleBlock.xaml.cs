using Focus_Bear.API;
using Focus_Bear.RelevanceClassifier;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web.ModelBinding;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Focus_Bear.RelevanceClassifier.NativeRelevanceClassifier;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for GentleBlock.xaml
    /// </summary>
    public partial class GentleBlock : Window
    {
        /// <summary>
        /// The "report a bad AI decision" body. Built as JSON: it was spliced, so a tab title or intention with a
        /// quote (a YouTube title, say) made it invalid.
        /// </summary>
        internal static string ReportBody(bool isApp, string appName, string url, string tabTitle, string focusMode, string intention, string language)
        {
            JObject body = isApp
                ? new JObject
                {
                    ["appName"] = appName ?? "",
                    ["focusMode"] = focusMode ?? "",
                    ["intention"] = intention ?? "",
                    ["language"] = language ?? "",
                }
                : new JObject
                {
                    ["url"] = url ?? "",
                    ["tab_title"] = tabTitle ?? "",
                    ["meta_description"] = "",
                    ["focus_mode"] = focusMode ?? "",
                    ["intention"] = intention ?? "",
                    ["language"] = language ?? "",
                };
            return body.ToString(Formatting.None);
        }

        public class TaskItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        int nTimer = 60; //60 Seconds
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");

        }

        private string sFocusModeName = "";
        public string sActiveAppName = "";
        public string sActiveURL = "";
        public string sFullURLForBlockedPage = "";
        public string sCurrentTabTitle = "";
        IntPtr HWND = IntPtr.Zero;

        public string CurrentURLBeingProcessedWithChatGPTAPI = "";

        ConvinceAIFailed wndConvinceAiFailed = null;
        TemporaryAccess wndTemporaryAccess = null;
        AddFocusGoalAndTask wndAddGoal = null;
        ConvinceAI wndConvince = null;

        // Guards against re-entrant stacking of the modal sub-popups below. ShowDialog() spins a nested
        // message loop, and because this GentleBlock window is a singleton reused across URLs, the async
        // AI callback for a newly-focused tab can fire inside that loop and open a second modal on top -
        // orphaning the first (the wnd* fields get overwritten, so StopTimer can no longer close it).
        // While a modal is open we refuse to open another; the pipeline re-blocks on the next focus
        // event once the open one is dealt with.
        private bool bModalSubPopupOpen = false;

        public GentleBlock()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            // Manually alter window height and width
            this.SizeToContent = SizeToContent.Manual;

            // Automatically resize height relative to content
            this.SizeToContent = SizeToContent.Height;

            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            IsVisibleChanged += (s, e) => { if (IsVisible) appSettings.NotifyBlockPopupShown(); };   // on the backdrop, never under it
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        /// <summary>
        /// Extracts the host from a URL without ever throwing. The previous pattern
        /// (try new Uri(url) / catch new UriBuilder(url).Uri) crashed the gentle-block popup because
        /// <c>new UriBuilder(url).Uri</c> itself throws <see cref="UriFormatException"/> for malformed
        /// URLs (spaces, unicode, bare IP:port, …) — 87 crash events across 5 users (#1095, windows-app#5).
        /// <see cref="FocusBear.Core.SafeUri.TryCreate(string, UriKind, out Uri)"/> never throws; we retry with an
        /// <c>https://</c> prefix for scheme-less URLs (mirroring UriBuilder's old scheme-adding).
        /// Returns false (host = "") when no host can be parsed.
        /// </summary>
        private static bool TryGetHost(string sUrl, out string sHost)
        {
            sHost = "";
            if (string.IsNullOrWhiteSpace(sUrl)) return false;
            if (FocusBear.Core.SafeUri.TryCreate(sUrl, UriKind.Absolute, out Uri parsed) && !string.IsNullOrEmpty(parsed.Host))
            {
                sHost = parsed.Host;
                return true;
            }
            if (FocusBear.Core.SafeUri.TryCreate("https://" + sUrl, UriKind.Absolute, out Uri prefixed) && !string.IsNullOrEmpty(prefixed.Host))
            {
                sHost = prefixed.Host;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Builds the PostHog metadata fragment (comma-separated "key":value pairs, no braces -
        /// <see cref="AppSettings.CapturePostHog"/> wraps it) describing WHAT set off this gentle-block
        /// popup: whether the trigger was an app or a URL, which app/host, the blocking source
        /// (teacher session / blocking schedule / focus mode), the bear mode, and - when the AI
        /// distraction check ran - its probability and reason. Shared by "block-popup-shown" (fired only
        /// where the popup is actually rendered: the non-AI branch of ShowGentleBlockWindowAndStartTimer,
        /// ShowDistractingCuddlyPopup for apps, and the equivalent inline branch in CheckWithChatGPT for
        /// URLs) and the existing "block-popup-allow-automatically" events, so all of them carry the same
        /// shape and analytics can tell an AI-distraction popup apart from a plain block-list one.
        /// </summary>
        private string BuildBlockPopupMetadata(bool bAICheckEnabled, double? xAiProbability = null)
        {
            bool bIsApp = sActiveAppName.Length > 0;

            string sBlockingSource;
            string sContextName;
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                sBlockingSource = "teacher-session";
                sContextName = sFocusModeName;
            }
            else if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var schedule))
            {
                sBlockingSource = "blocking-schedule";
                sContextName = schedule.name;
            }
            else
            {
                sBlockingSource = "focus-mode";
                sContextName = sFocusModeName;
            }

            var parts = new List<string>
            {
                "\"triggerType\":" + JsonConvert.SerializeObject(bIsApp ? "app" : "url"),
                "\"blockingSource\":" + JsonConvert.SerializeObject(sBlockingSource),
                "\"focusContextName\":" + JsonConvert.SerializeObject(sContextName ?? ""),
                "\"bearMode\":" + JsonConvert.SerializeObject(appSettings.nFocusBearMode == 1 ? "cuddly" : "grizzly"),
                "\"aiCheckEnabled\":" + JsonConvert.SerializeObject(bAICheckEnabled),
            };

            if (bIsApp)
            {
                parts.Add("\"blockedApp\":" + JsonConvert.SerializeObject(sActiveAppName));
            }
            else
            {
                TryGetHost(sActiveURL, out string sHost);
                parts.Add("\"blockedHost\":" + JsonConvert.SerializeObject(sHost));
            }

            if (xAiProbability.HasValue)
            {
                parts.Add("\"aiProbability\":" + xAiProbability.Value.ToString("0.###", CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(appSettings.sChatGPTReasonForCurrentURL))
            {
                parts.Add("\"aiReason\":" + JsonConvert.SerializeObject(appSettings.sChatGPTReasonForCurrentURL));
            }

            return string.Join(",", parts);
        }

        public void ShowGentleBlockWindowAndStartTimer(IntPtr hWnd, string sFocusModeTitle, string sAppName, string sURL = "", string sIntention = "", string sTabTitle = "")
        {
            sFocusModeName = sFocusModeTitle;
            sActiveAppName = sAppName;
            sFullURLForBlockedPage = sURL;
            sActiveURL = sURL;
            sCurrentTabTitle = sTabTitle;
            HWND = hWnd;

            //Don't (re)show the block popup if the focus session / schedule has ended in the meantime - e.g.
            //RecheckCurrentWithAI re-enters here after the user closes the goal/task modal, by which point the
            //session may have expired and the focus-end screen is showing.
            if (SuppressPopupIfBlockingEnded())
                return;

            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.Height = 600;
                MainGrid.RowDefinitions[1].Height = new GridLength(180);
            }
            Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["titleChangedFocus"].ToString();
            WantToChangeCurrentTask.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["changeTaskText"].ToString();
            MarkPreviousTaskCompleteText.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["buttonMarkCurrentTaskAsCompleted"].ToString();

            btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonAllowText"].ToString();
            //Block button wording depends on app vs URL. Set it up-front (covers the AI app path, which
            //hides the window and never reaches the non-AI override below, so it used to read "Block this
            //page" for apps).
            TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"][sAppName.Length > 0 ? "buttonBlockAppText" : "buttonBlockUrlText"].ToString();

            TextReportBadAIDecision.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["reportBadAiTooltip"].ToString();
            btnSetTaskText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonSetTask"].ToString();

            bool bAICheckRequired = false;
            if (sAppName.Length > 0)
            {
                // One ladder, in FocusBear.Core: snooze > teacher > buddy > the block list's
                // is_ai_enabled > the global toggle. This used to be hand-rolled here and skipped
                // the block list, so the Mac's is_ai_enabled=false had no effect on Windows.
                bAICheckRequired = AiBlocking.IsEnabled();
                ReportBadAIDecision.Visibility = bAICheckRequired ? Visibility.Visible : Visibility.Collapsed;

                if (bAICheckRequired)
                {
                    //Keep the window hidden until the AI verdict comes back (same as the URL flow).
                    StopTimer();
                    appSettings.HideBlurWindow();
                    this.Hide();
                }
                else
                {
                    //No AI - show the gentle app popup straight away (existing behaviour).
                    ReportBadAIDecision.Visibility = Visibility.Collapsed;
                    appSettings.ShowBlurWindow(HWND);
                    this.Show(); //Will show this window for apps
                }
            }
            else
            {
                ///////////////////////////////////////////////////////////////////
                ///
                // One ladder, in FocusBear.Core: snooze > teacher > buddy > the block list's
                // is_ai_enabled > the global toggle. This used to be hand-rolled here and skipped
                // the block list, so the Mac's is_ai_enabled=false had no effect on Windows.
                bAICheckRequired = AiBlocking.IsEnabled();
                ReportBadAIDecision.Visibility = bAICheckRequired ? Visibility.Visible : Visibility.Collapsed;


                // Safe host extraction — the old try/UriBuilder pattern crashed on malformed URLs (#1095).
                string sHost = "";
                TryGetHost(sActiveURL, out sHost);

                string sDomain = appSettings.ConfigFileStrings["common"]["allowAllUrlsFromDomainText"].ToString();
                sDomain = sDomain.Replace("<DOMAIN>", sHost);

                ////////////////////////////////////////////////////

                ////sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);
                ///
                //Close all existing popups if any which is handled by Stop timer function
                StopTimer();

                appSettings.HideBlurWindow();
                this.Hide(); //Will keep this window hidden will we get response from API
            }

            if (appSettings.sAllTasks.Length > 0)
            {
                //File the tasks combo box
                JArray tasks = JArray.Parse(appSettings.sAllTasks);

                var taskList = tasks
                    .Select(t => new TaskItem
                    {
                        Id = t["id"]?.ToString(),
                        Name = t["title"]?.ToString()
                    })
                    .ToList();

                ComboBoxTasks.ItemsSource = taskList;
                ComboBoxTasks.DisplayMemberPath = "Name";
            }
            /** if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
             {
                 sHeading = sHeading.Replace("<FOCUSTYPE>", appSettings.ActiveSchedule.name);
             }
             else
             {
                 sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeTitle);
             }

             //sHeading = sHeading.Length > 108 ? sHeading.Substring(0, 108) : sHeading; //If there are more characters, take only first 100 approx
             Heading.Text = sHeading;

             if (sIntention.Length > 0) //Set the activity title if thelength is > 0
             {
                 string sInt = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["intentionText"].ToString();
                 sInt = sInt.Replace("<INTENTION>",sIntention);
                 Intention.Text = sInt;
                 Intention.Visibility = Visibility.Visible;
             }
             else
             {
                 Intention.Visibility = Visibility.Collapsed;
             }**/
            dispatcherTimer.Stop();
            //Remove the previous event handler so that new can be set
            try
            {
                var eventField = dispatcherTimer.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimer);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimer.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowGentleBlockWindowAndStartTimer - " + ex.Message);
            }
            nTimer = 60;
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            if (sActiveAppName.Length > 0 && bAICheckRequired) //App AI check (separate endpoint)
            {
                CheckWithChatGPTForApp();
            }
            else if (sURL.Length > 0 && bAICheckRequired) //check settings from AI tab
            {
                CheckWithChatGPT();
            }
            else
            {
                string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();
                if (sAppName.Length > 0)
                {
                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sAppName);
                    TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockAppText"].ToString();
                }
                else
                {
                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);
                    TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockUrlText"].ToString();
                }

                if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForHeading)) // Blocking schedule
                {
                    sHeading = sHeading.Replace("<FOCUSTYPE>", scheduleForHeading.name);
                }
                else
                {
                    sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                }

                Heading.Text = sHeading;

                string sDescription = "";
                if(appSettings.sRunningFocusModeIntention.Length > 0)
                {
                    string sIntent = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["intentionText"].ToString();
                    sIntent = sIntent.Replace("<INTENTION>", appSettings.sRunningFocusModeIntention);
                    sDescription = sIntent;
                }

                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                {
                    string sTask = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["currentTaskText"].ToString();
                    sTask = sTask.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                    sDescription = sDescription + "\n\n" + sTask;
                }
                WorkingOnTask.Text = sDescription;

                WantToChangeCurrentTask.Visibility = Visibility.Collapsed;
                MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                ReportBadAIDecision.Visibility = Visibility.Collapsed;
                ComboxBoxSection.Visibility = Visibility.Collapsed;
                ScoreSection.Visibility = Visibility.Collapsed;
                btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Collapsed;

                CommandManager.InvalidateRequerySuggested();

                //Analytics: no AI check applies here (grizzly mode / AI off / AI off for this schedule or
                //teacher session), so this block popup is shown to the user unconditionally - record it.
                appSettings.CapturePostHog("block-popup-shown", BuildBlockPopupMetadata(false));

                appSettings.ShowBlurWindow(HWND);
                this.Show(); //Will show this window
                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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
            clean = System.Text.RegularExpressions.Regex.Replace(clean,
                @"\s+[-–—]\s+(Google Chrome|Microsoft Edge|Mozilla Firefox)\s*$",
                string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+[-–—]\s+YouTube\s*$",
                string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"^\s*\(\d+\)\s*", string.Empty);
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
            // Generic/loading titles are not valid content inputs, even when stable.
            if (clean.Length == 0 ||
                string.Equals(clean, "YouTube", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, "Untitled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, "New Tab", StringComparison.OrdinalIgnoreCase))
                return null;
            Uri titleUrl;
            if (!clean.Any(char.IsWhiteSpace) &&
                Uri.TryCreate(clean.Contains("://") ? clean : "https://" + clean,
                    UriKind.Absolute, out titleUrl) &&
                string.Equals(titleUrl.Authority, url.Authority, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(titleUrl.PathAndQuery, url.PathAndQuery, StringComparison.Ordinal) &&
                string.Equals(titleUrl.Fragment, url.Fragment, StringComparison.Ordinal))
                return null;
            return clean;
        }

        private async void CheckWithChatGPT()
        {
            //Here first we need to call the chat GPT API and decide per that to show the Gentle block window or not
            ///
            if (CurrentURLBeingProcessedWithChatGPTAPI == sActiveURL)
                return;
            CurrentURLBeingProcessedWithChatGPTAPI = sActiveURL;

            if(appSettings.sConvincingAIForURL != sActiveURL)
            {
                appSettings.sExtraJustificationForThisSite = ""; //empty the extra justification if the url is changed.
                appSettings.sConvincingAIForURL = ""; //reset
            }

            await Task.Run(async () =>
            {
                try
                {
                    //Show thinking icon just before the API call
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (((App)(Application.Current)).wndFocusProgress != null)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/thinking_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();
                            ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                        }
                    }));

                    string shadowAction = "";
                    string shadowConfidence = "";
                    string shadowTier = "";
                    string shadowFallbackRequired = "";
                    // Shadow-mode check with the native C# ONNX relevance classifier.
                    // This does NOT change the current Focus Bear blocking decision.
                    try
                    {
                        string shadowTitle = System.Text.RegularExpressions.Regex.Replace(
                            sCurrentTabTitle ?? string.Empty, @"^\s*\(\d+\)\s*", string.Empty).Trim();
                        shadowTitle = System.Text.RegularExpressions.Regex.Replace(
                            shadowTitle, @"\s+[-–—]\s+(Google Chrome|Microsoft Edge|Mozilla Firefox)\s*$",
                            string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                        Uri shadowUrl;
                        string rawShadowUrl = sActiveURL ?? string.Empty;
                        string cleanVideoTitle = Uri.TryCreate(
                            rawShadowUrl.Contains("://") ? rawShadowUrl : "https://" + rawShadowUrl,
                            UriKind.Absolute, out shadowUrl)
                            ? CleanYouTubeShadowTitle(shadowUrl, sCurrentTabTitle)
                            : null;
                        if (cleanVideoTitle != null)
                            shadowTitle = cleanVideoTitle;
                        bool placeholderTitle = string.IsNullOrWhiteSpace(shadowTitle) ||
                            string.Equals(shadowTitle, "Untitled", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(shadowTitle, "New Tab", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(shadowTitle, "YouTube", StringComparison.OrdinalIgnoreCase);
                        bool youtubeContentPage = shadowUrl != null &&
                            (string.Equals(shadowUrl.Host, "youtube.com", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(shadowUrl.Host, "www.youtube.com", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(shadowUrl.Host, "m.youtube.com", StringComparison.OrdinalIgnoreCase)) &&
                            (string.Equals(shadowUrl.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(shadowUrl.AbsolutePath, "/results", StringComparison.OrdinalIgnoreCase));
                        if (youtubeContentPage)
                        {
                            // URL navigation can precede the window title update. The browser
                            // shadow queue waits for a stable title and validates the active URL.
                            // Leave the existing backend check below to continue normally.
                            Debug.WriteLine("[NativeRelevanceModel] GENTLE BLOCK SHADOW deferred to stable browser check | URL=" + sActiveURL);
                        }
                        else if (placeholderTitle)
                        {
                            Debug.WriteLine("[NativeRelevanceModel] GENTLE BLOCK SHADOW skipped: title not ready | URL=" + sActiveURL);
                        }
                        else
                        {
                            App currentApp = (App)Application.Current;

                            if (currentApp.NativeRelevanceClassifier == null)
                            {
                                throw new InvalidOperationException(
                                    "Native relevance classifier is not loaded."
                                );
                            }

                            string urlPathKeywords = string.Empty;

                            try
                            {
                                string rawUrl = sActiveURL ?? string.Empty;

                                if (!string.IsNullOrWhiteSpace(rawUrl))
                                {
                                    Uri parsedUrl = new Uri(
                                        rawUrl.Contains("://") ? rawUrl : "https://" + rawUrl);

                                    string[] pathParts = parsedUrl.AbsolutePath
                                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                                    urlPathKeywords = string.Join(" ",
                                        pathParts.Where(part =>
                                            part.Length > 2 &&
                                            !part.All(char.IsDigit)));
                                }
                            }
                            catch (UriFormatException)
                            {
                                urlPathKeywords = string.Empty;
                            }

                            string modelTitle = cleanVideoTitle ?? sCurrentTabTitle;
                            string websiteContext = cleanVideoTitle ?? (
                               (sCurrentTabTitle ?? string.Empty) + " " + urlPathKeywords
                            ).Trim();

                            string userGoal = (
                                 (appSettings.sRunningFocusModeIntention ?? string.Empty)
                                 + " " +
                                 (appSettings.sCurrentTaskPlayingInToDoPlayer ?? string.Empty)
                            ).Trim();

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] CURRENT TASK CHECK=" +
                                 (appSettings.sCurrentTaskPlayingInToDoPlayer ?? "<NULL>")
                            );

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] REAL INPUT - UserGoal=" +
                                 userGoal
                            );

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] REAL INPUT - FocusIntention=" +
                                appSettings.sRunningFocusModeIntention
                            );

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] REAL INPUT - TabTitle=" +
                                sCurrentTabTitle
                            );

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] REAL INPUT - URL=" +
                                sActiveURL
                            );

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] REAL INPUT - WebsiteContext=" +
                                websiteContext
                            );

                            var nativePredictionStopwatch = System.Diagnostics.Stopwatch.StartNew();

                            NativePredictionResult relevanceResult =
                                currentApp.NativeRelevanceClassifier.Predict(
                                    userGoal,
                                    websiteContext,
                                    sActiveURL,
                                    modelTitle
                                );
                            nativePredictionStopwatch.Stop();

                            double nativePredictionLatencyMs =
                                nativePredictionStopwatch.Elapsed.TotalMilliseconds;

                            Debug.WriteLine(
                                "[NativeRelevanceClassifier] WEBSITE LATENCY MS=" +
                                nativePredictionLatencyMs.ToString(
                                    "0.00",
                                    CultureInfo.InvariantCulture
                                )
                            );

                            shadowAction = relevanceResult.Action;

                            shadowConfidence =
                                relevanceResult.Confidence.ToString(
                                    "0.0000",
                                    CultureInfo.InvariantCulture
                                );

                            shadowTier =
                                relevanceResult.Tier.ToString(
                                    CultureInfo.InvariantCulture
                                );

                            shadowFallbackRequired =
                                (!relevanceResult.ConfidenceGatePassed).ToString();

                            LogWriter.LogWrite(
                                "NativeRelevanceModel SHADOW" +
                                " | Action=" + shadowAction +
                                " | Tier=" + shadowTier +
                                " | Confidence=" + shadowConfidence +
                                " | NativeLatencyMs=" +
                                relevanceResult.LatencyMs.ToString(
                                   "0.00",
                                   CultureInfo.InvariantCulture
                                )
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        // Shadow mode must never interrupt the existing Focus Bear behaviour.
                        LogWriter.LogWrite(
                            "NativeRelevanceModel SHADOW failed - " + ex.Message
                        );
                    }
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var nResponse = await client.ChatGPTIsURLSafe(sActiveURL, sCurrentTabTitle, "", appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);
                    double xProbability = nResponse;   //already a double: the culture round trip could only lose it

                    bool backendAllows = xProbability >= 0.80;
                    bool shadowAllows = shadowAction.Equals(
                       "Allow",
                        StringComparison.OrdinalIgnoreCase
                    );

                    bool isDisagreement =




                        !string.IsNullOrEmpty(shadowAction) &&
                        xProbability >= 0 &&
                        (backendAllows != shadowAllows);

                    if (CurrentURLBeingProcessedWithChatGPTAPI != sActiveURL) //If user have moved to another url, do not do anything
                        return;

                    await Task.Delay(20);

                    //Write to focus ai decisions csv
                    // Safe host extraction — the old try/UriBuilder pattern crashed on malformed URLs (#1095).
                    string sHost = "";
                    TryGetHost(sActiveURL, out sHost);

                    try
                    {
                        //"Timestamp,Url,Domain,TabTitle,AIRelevanceScore,AdditionalUserJustification,currentTaskName,focusIntention,AIReason”
                        string csvLine = FocusBear.Core.Blocking.AiDecisionCsv.Row(DateTime.UtcNow, sActiveURL, sHost, sCurrentTabTitle, xProbability,
                            "", "", appSettings.sRunningFocusModeIntention, appSettings.sChatGPTReasonForCurrentURL);
                        appSettings.csvfocusAIDecisions.Append(csvLine);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("csvfocusAIDecisions write error: " + ex.Message);
                    }

                    // Record classifier/backend disagreements for shadow-mode analysis.
                    if (isDisagreement)
                    {
                            LogWriter.LogWrite(
                                    "RelevanceModel DISAGREEMENT - URL=" + sActiveURL +
                                    " | ShadowAction=" + shadowAction +
                                    " | ShadowTier=" + shadowTier +
                                    " | ShadowConfidence=" + shadowConfidence +
                                    " | ShadowLLMFallback=" + shadowFallbackRequired +
                                    " | BackendProbability=" + xProbability +
                                    " | BackendAction=" + (backendAllows ? "Allow" : "Block")
                            );
                    }



                    // Non-cuddly (grizzly) / Teacher Mode: no gentle popup and no questions asked. Decide from
                    // the AI verdict alone - an aligned site is allowed silently; anything else (distracting, or
                    // the status couldn't be verified) is blocked immediately.
                    bool bEffectiveCuddly = (appSettings.nFocusBearMode == 1) && !TeacherSessionManager.Instance.IsTeacherSessionActive();
                    if (!bEffectiveCuddly)
                    {
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (xProbability >= 0.80)
                            {
                                //AI says the site is aligned with the focus session - allow it silently
                                appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));
                                appSettings.CapturePostHog("block-popup-allow-automatically", BuildBlockPopupMetadata(true, xProbability));

                                //Reflect the aligned verdict in the focus-progress popup: productive icon + reason tooltip.
                                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sFullURLForBlockedPage] = new Dictionary<string, object>
                                {
                                    { "probability", xProbability },
                                    { "isVisibleUrl", true },
                                    { "totalSeconds", 0 },
                                    { "startedTime", DateTime.Now },
                                    { "reason", appSettings.sChatGPTReasonForCurrentURL }
                                };
                                FocusAlignmentScoreManager.CustomToolTip = appSettings.sChatGPTReasonForCurrentURL;
                                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(xProbability);
                                FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Google Chrome", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" — Mozilla Firefox", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Microsoft? Edge", "");
                                if (sCurrentTabTitle.Length > 0)
                                {
                                    var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sCurrentTabTitle.ToLower());
                                    if (titleExists.Count() == 0)
                                        appSettings.TemporaryAllowedURLsTabTitles.Add(sCurrentTabTitle);
                                }
                            }
                            else
                            {
                                //AI negative (or status could not be verified) - block now, no reasons/questions asked.
                                //We are redirecting to the (neutral) block page, so reset the focus-progress icon to
                                //normal and stop tracking this URL - otherwise it lingers as a productive/smiling score.
                                appSettings.TemporarryBlockedURLsGentleBlock.Add(sActiveURL.ToLower());
                                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                                ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "focus");
                            }
                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                        }));
                        return;
                    }

                    if (xProbability == -1)
                    {
                        CurrentURLBeingProcessedWithChatGPTAPI = "";
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            // The AI check failed (couldn't verify), not "AI says distracting" - fail open
                            // the same way the non-cuddly branch already does, rather than stopping the user
                            // with a popup over something that isn't actually a verdict. Record a neutral
                            // 0.5 probability so this URL still contributes to the focus-alignment average
                            // instead of being silently excluded from it.
                            const double NeutralProbabilityOnVerificationFailure = 0.5;
                            string sUrlNotVerifiedText = appSettings.GetConfigStringSafe("The status of this url could not be verified.", "popUpBlockConfirmVcInfo", "urlNotVerifiedText");

                            FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sFullURLForBlockedPage] = new Dictionary<string, object>
                            {
                                { "probability", NeutralProbabilityOnVerificationFailure },
                                { "isVisibleUrl", true },
                                { "totalSeconds", 0 },
                                { "startedTime", DateTime.Now },
                                { "reason", sUrlNotVerifiedText }
                            };
                            FocusAlignmentScoreManager.CustomToolTip = sUrlNotVerifiedText;
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(NeutralProbabilityOnVerificationFailure);
                            FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                            appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));

                            sCurrentTabTitle = sCurrentTabTitle.Replace(" - Google Chrome", "");
                            sCurrentTabTitle = sCurrentTabTitle.Replace(" — Mozilla Firefox", "");
                            sCurrentTabTitle = sCurrentTabTitle.Replace(" - Microsoft? Edge", "");
                            if (sCurrentTabTitle.Length > 0)
                            {
                                var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sCurrentTabTitle.ToLower());
                                if (titleExists.Count() == 0)
                                {
                                    appSettings.TemporaryAllowedURLsTabTitles.Add(sCurrentTabTitle);
                                }
                            }

                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                        }));
                    }
                    else  //we have got the probability
                    {

                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            ReportBadAIDecision.Visibility = Visibility.Visible;
                            Console.WriteLine("*****CheckWithChatGPT****current URL:" + sFullURLForBlockedPage);
                            //Safe URL, need to allow.
                            //if (!FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.ContainsKey(sFullURLForBlockedPage))
                            {
                                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sFullURLForBlockedPage] = new Dictionary<string, object>
                                {
                                    { "probability", xProbability },
                                    { "isVisibleUrl", true },
                                    { "totalSeconds", 0 },
                                    { "startedTime", DateTime.Now },
                                    { "reason", appSettings.sChatGPTReasonForCurrentURL }
                                };

                                FocusAlignmentScoreManager.CustomToolTip = appSettings.sChatGPTReasonForCurrentURL;
                                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(xProbability);

                                Console.WriteLine("*****CheckWithChatGPT****current URL tooltip:" + appSettings.sChatGPTReasonForCurrentURL);
                                Console.WriteLine("*****CheckWithChatGPT****current URL xProbability:" + xProbability.ToString());

                            }

                            double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                            Console.WriteLine("******currentFocusAlignmentScore: - " + currentFocusAlignmentScore.ToString());

                            Task.Delay(2000);

                            if (xProbability >= 0.80)
                            {
                                /**ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                invokeProv.Invoke();**/

                                appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));

                                appSettings.CapturePostHog("block-popup-allow-automatically", BuildBlockPopupMetadata(true, xProbability));

                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Google Chrome", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" — Mozilla Firefox", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Microsoft? Edge", "");
                                //Lets add it to keyword array, if its not already there
                                if (sCurrentTabTitle.Length > 0)
                                {
                                    var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sCurrentTabTitle.ToLower());
                                    if (titleExists.Count() == 0)
                                    {
                                        //add to list
                                        appSettings.TemporaryAllowedURLsTabTitles.Add(sCurrentTabTitle);
                                    }
                                }
                                dispatcherTimer.Stop();
                                appSettings.HideBlurWindow();
                                Hide();

                            }
                            else if (currentFocusAlignmentScore < 80 /*minFocusAlignmentScoreToShowDistractingPopUp*/)// Need to ASk USER if probability is < 0
                            {
                                //The AI check is async - the focus session may have ended while it was in
                                //flight. Don't pop the distracting/goal-task UI over the focus-end screen.
                                if (SuppressPopupIfBlockingEnded())
                                    return;

                                //Re-entrancy guard: if a modal sub-popup is already open (e.g. the user switched
                                //tabs while the goal/task box was up), don't stack another one for this URL - that
                                //is what left duplicate/orphaned goal-task popups. Just hide; the next focus event
                                //re-evaluates once the open modal is dealt with.
                                if (bModalSubPopupOpen)
                                {
                                    LogWriter.LogWrite("GentleBlock AI callback - modal sub-popup already open, skipping to avoid stacking");
                                    dispatcherTimer.Stop();
                                    appSettings.HideBlurWindow();
                                    Hide();
                                    return;
                                }

                                //Analytics: URL counterpart of ShowDistractingCuddlyPopup's "block-popup-shown" -
                                //this branch is its own un-refactored copy of that flow (ask for goal/task,
                                //convince AI, suggested task) rather than calling the shared method, so it needs
                                //its own call. Past both early-return guards above, so this only fires when a
                                //popup is genuinely about to be shown.
                                appSettings.CapturePostHog("block-popup-shown", BuildBlockPopupMetadata(true, xProbability));

                                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length == 0 && appSettings.sRunningFocusModeIntention.Length == 0)
                                {
                                    //Mean there is no task and no intention
                                    //Fire the yes button to ask for goal and intention
                                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                    invokeProv.Invoke();

                                }
                                else if (appSettings.sConvincingAIForURL == sActiveURL)
                                {
                                    //Means AI was not convinced and we need to show a different popup to user that AI is not convinced and allow to convince again

                                    wndConvinceAiFailed = new ConvinceAIFailed(xProbability, nTimer, IsAppBlocking());
                                    wndConvinceAiFailed.Owner = this;

                                    //Show the main window first
                                    appSettings.ShowBlurWindow(HWND);
                                    this.Show();
                                    dispatcherTimer.Stop();//stop the timer here as the convince failed will start it.

                                    GridShade.Visibility = Visibility.Visible;
                                    bModalSubPopupOpen = true;
                                    bool? bResult = null;
                                    try { bResult = wndConvinceAiFailed.ShowDialog(); } //yeh beechmein hai
                                    finally { bModalSubPopupOpen = false; }
                                    GridShade.Visibility = Visibility.Collapsed;

                                    appSettings.sConvincingAIForURL = ""; //reset

                                    if (bResult.Value)
                                    {
                                        if (wndConvinceAiFailed.bReportBadAIDecision)
                                        {
                                            Hyperlink link = BtnReportBadAIDecision;   //  Hyperlink control
                                            var peerH = new HyperlinkAutomationPeer(link);
                                            var invokeProvH = peerH.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                            invokeProvH?.Invoke();
                                        }
                                        else if (wndConvinceAiFailed.bShowConvincePopupAgain) //Let me convince AI again -> Yes this site is relevant
                                        {
                                            //This button click will automatically take care of firing the convince aI as task is already there
                                            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                            invokeProv.Invoke();
                                        }
                                        else if (wndConvinceAiFailed.bShowAskNewGoalPopup) //Let me convince AI again -> I am working on something else
                                        {
                                            ShowAskNewGoalPopup();
                                        }
                                        else if (wndConvinceAiFailed.bShowTemporaryAccessPopup)
                                        {
                                            wndTemporaryAccess = new TemporaryAccess(sActiveURL);
                                            wndTemporaryAccess.Owner = this;

                                            GridShade.Visibility = Visibility.Visible;
                                            bModalSubPopupOpen = true;
                                            bool? bResultTemp = null;
                                            try { bResultTemp = wndTemporaryAccess.ShowDialog(); }
                                            finally { bModalSubPopupOpen = false; }
                                            GridShade.Visibility = Visibility.Collapsed;
                                            if (bResult.Value) //User temporarily allowed the url
                                            {
                                                dispatcherTimer.Stop();
                                                appSettings.HideBlurWindow();
                                                Hide();
                                                return;//allow the url
                                            }
                                            else //BLock this page
                                            {
                                                BlockSiteWork();
                                            }
                                            wndTemporaryAccess = null;
                                        }

                                    }
                                    else
                                    {
                                        BlockSiteWork();
                                    }
                                    wndConvinceAiFailed = null;
                                }
                                else if (appSettings.sChatGPTReasonForCurrentURL.Length > 0 && ShowAiBlockBuddy(xProbability))
                                {
                                    // The redesigned check-in took over (AiBlockBuddyWindow).
                                }
                                else if (appSettings.sChatGPTReasonForCurrentURL.Length > 0) //add reason from chat gpt
                                {
                                    JObject obj = JObject.Parse(appSettings.sChatGPTResponse);

                                    string suggestedTask = obj["suggested_task"]?.ToString();
                                    if(suggestedTask != null)
                                    {
                                        string suggestedTaskId = obj["suggested_task_id"]?.ToString();

                                        string sTask = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["suggestedTaskText"].ToString();
                                        WorkingOnTask.Text = sTask.Replace("<TASK>", suggestedTask);

                                        ComboBoxTasks.Text = suggestedTask; //Add the suggested task to the textview of the combo box

                                        WantToChangeCurrentTask.Visibility = Visibility.Visible;
                                        MarkPreviousTaskComplete.Visibility = Visibility.Visible;
                                        ComboxBoxSection.Visibility = Visibility.Visible;

                                        btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Collapsed;
                                    }
                                    else
                                    {
                                        WorkingOnTask.Text = "";

                                        WantToChangeCurrentTask.Visibility = Visibility.Collapsed;
                                        MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                                        ComboxBoxSection.Visibility = Visibility.Collapsed;

                                        string sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoal"].ToString();
                                        if(appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                                        {
                                            sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoalAndTask"].ToString();
                                            sHead = sHead.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                                        }
                                        sHead = sHead.Replace("<GOAL>", appSettings.sRunningFocusModeIntention );
                                        Heading.Text = sHead;

                                        btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Visible;
                                        btnNoIamWorkingOnSomethingElseText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonWorkingSomethingElseText"].ToString();

                                        btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonSiteRelevantText"].ToString();
                                    }

                                    ScoreSection.Visibility = Visibility.Visible;
                                    string sScore = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["aiRelevanceScoreText"].ToString();
                                    AIRelevanceScore.Text = sScore.Replace("<SCORE>", ((int)(xProbability * 100)).ToString());

                                    InfoIcon.ToolTip = appSettings.sChatGPTReasonForCurrentURL;
                                    if (suggestedTask != null) //only if there was a suggested task
                                    {
                                        string sStillWorking = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttoWorkingOnOldTask"].ToString();
                                        if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                                        {
                                            sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                                            btnJustThisTimeText.Text = sStillWorking;
                                        }
                                        else if (appSettings.sRunningFocusModeIntention.Length > 0)
                                        {
                                            sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sRunningFocusModeIntention);
                                            btnJustThisTimeText.Text = sStillWorking;
                                        }
                                    }
                                    /***
                                    sHeading = sHeading + "\n\n" + appSettings.sChatGPTReasonForCurrentURL;
                                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);


                                    if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForHeading3)) // Blocking schedule
                                    {
                                        sHeading = sHeading.Replace("<FOCUSTYPE>", scheduleForHeading3.name);
                                    }
                                    else
                                    {
                                        sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                                    }

                                    Heading.Text = sHeading;***/

                                    CommandManager.InvalidateRequerySuggested();
                                    if (appSettings.sLastActiveURLInFirefox == sActiveURL)
                                    {
                                        dispatcherTimer.Start();
                                        appSettings.ShowBlurWindow(HWND);
                                        this.Show();
                                    }
                                }
                            }
                        }));

                        return;//allow the url
                    }



                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("attemp to ChatGPTIsURLSafe EX -" + ex.Message);
                }
            });
        }

        // App equivalent of CheckWithChatGPT - runs the AI app-safety check (separate endpoint) and decides:
        //  - aligned (>= 0.70)  -> allow the app silently (productive icon + reason tooltip)
        //  - distracting (< 0.70) -> cuddly: show the gentle popup with score + reason; grizzly/Teacher: block now, no popup
        //  - could not verify (-1) -> allow (fail-open), matching the macOS app behaviour
        private async void CheckWithChatGPTForApp()
        {
            //No app name to check (some windows have no resolvable process name) - skip the AI call, it can
            //only match nothing and just wastes a request. Allow and hide, like the fail-open path.
            if (string.IsNullOrWhiteSpace(sActiveAppName))
            {
                LogWriter.LogWrite("CheckWithChatGPTForApp - empty app name, skipping AI check");
                dispatcherTimer.Stop();
                appSettings.HideBlurWindow();
                Hide();
                return;
            }

            if (CurrentURLBeingProcessedWithChatGPTAPI == sActiveAppName)
                return;
            CurrentURLBeingProcessedWithChatGPTAPI = sActiveAppName;

            await Task.Run(async () =>
            {
                try
                {
                    //Show thinking icon just before the API call
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (((App)(Application.Current)).wndFocusProgress != null)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/thinking_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();
                            ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                        }
                    }));

                    //Guard the race where a URL block reset sActiveAppName to "" after this app check was
                    //launched - don't fire the AI call with an empty app name (it can only match nothing).
                    if (string.IsNullOrWhiteSpace(sActiveAppName))
                    {
                        LogWriter.LogWrite("CheckWithChatGPTForApp - app name cleared before AI call, skipping");
                        return;
                    }

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var nResponse = await client.ChatGPTIsAppSafe(sActiveAppName, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);
                    double xProbability = nResponse;   //already a double: the culture round trip could only lose it

                    if (CurrentURLBeingProcessedWithChatGPTAPI != sActiveAppName) //user switched app while the request was in flight
                        return;

                    string sAppKey = sActiveAppName.ToLower();

                    //Write to focus ai decisions csv
                    try
                    {
                        string csvLine = FocusBear.Core.Blocking.AiDecisionCsv.Row(DateTime.UtcNow, sActiveAppName, sActiveAppName, "", xProbability,
                            "", "", appSettings.sRunningFocusModeIntention, appSettings.sChatGPTReasonForCurrentURL);
                        appSettings.csvfocusAIDecisions.Append(csvLine);
                    }
                    catch (Exception exCsv)
                    {
                        Console.WriteLine("csvfocusAIDecisions (app) write error: " + exCsv.Message);
                    }

                    bool bEffectiveCuddly = (appSettings.nFocusBearMode == 1) && !TeacherSessionManager.Instance.IsTeacherSessionActive();

                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (xProbability == -1)
                        {
                            //Could not verify - trust the user and allow (fail-open), like the macOS app.
                            appSettings.TemporaryAllowedAppsGentleBlock.Add(new DistractingURL(sAppKey, true));
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                            return;
                        }

                        if (xProbability >= 0.70)
                        {
                            //AI says the app is aligned with the focus session - allow it silently.
                            appSettings.TemporaryAllowedAppsGentleBlock.Add(new DistractingURL(sAppKey, true));
                            appSettings.CapturePostHog("block-popup-allow-automatically", BuildBlockPopupMetadata(true, xProbability));

                            FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sAppKey] = new Dictionary<string, object>
                            {
                                { "probability", xProbability },
                                { "isVisibleApp", true },
                                { "totalSeconds", 0 },
                                { "startedTime", DateTime.Now },
                                { "reason", appSettings.sChatGPTReasonForCurrentURL }
                            };
                            FocusAlignmentScoreManager.CustomToolTip = appSettings.sChatGPTReasonForCurrentURL;
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(xProbability);
                            FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                            return;
                        }

                        //Distracting (< 0.70)
                        if (!bEffectiveCuddly)
                        {
                            //grizzly / Teacher Mode -> block now, no reasons/questions. Reset the focus-progress
                            //icon to normal; FocusChangedHandler minimizes the app on its next cycle.
                            appSettings.TemporarryBlockedAppsGentleBlock.Add(sAppKey);
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                            return;
                        }

                        //cuddly -> record the score, then run the SAME distracting-popup flow as URLs
                        //(ask for goal/task when none, convince AI, set/suggest task).
                        FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sAppKey] = new Dictionary<string, object>
                        {
                            { "probability", xProbability },
                            { "isVisibleApp", true },
                            { "totalSeconds", 0 },
                            { "startedTime", DateTime.Now },
                            { "reason", appSettings.sChatGPTReasonForCurrentURL }
                        };
                        FocusAlignmentScoreManager.CustomToolTip = appSettings.sChatGPTReasonForCurrentURL;
                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(xProbability);
                        double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                        if (currentFocusAlignmentScore < 80)
                        {
                            ReportBadAIDecision.Visibility = Visibility.Visible;
                            ShowDistractingCuddlyPopup(xProbability);
                        }
                        else
                        {
                            //Overall alignment is still healthy - allow the app without a popup.
                            appSettings.TemporaryAllowedAppsGentleBlock.Add(new DistractingURL(sAppKey, true));
                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                        }
                    }));
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CheckWithChatGPTForApp EX - " + ex.Message);
                }
            });
        }

        private bool IsAppBlocking() => !string.IsNullOrEmpty(sActiveAppName);

        // True while something that legitimately blocks is active: a (non-paused) focus session, a
        // focus-mode habit, or a (non-paused) blocking schedule. Mirrors the app/URL blocking gates.
        private bool IsBlockingStillActive()
        {
            return (appSettings.isFocusModeRunning && !appSettings.isFocusModePaused)
                || appSettings.bHabitwithUseFocusModeRunning
                || (appSettings.isBlockingScheduleRunning && appSettings.ActiveSchedule != null && !appSettings.ActiveSchedule.isPaused);
        }

        // Guard used before this block popup (and its goal/task modal) is made visible. If blocking is no
        // longer active - e.g. the focus session expired while the user sat on the goal/task modal or moved
        // to the main window - tear the popup down and report suppression, so it can't reappear over the
        // focus-end screen. Returns true when the popup was suppressed (caller should return).
        private bool SuppressPopupIfBlockingEnded()
        {
            if (IsBlockingStillActive())
                return false;

            LogWriter.LogWrite("GentleBlock - blocking no longer active (session/schedule ended), suppressing popup");
            try
            {
                StopTimer();                    //also closes any modal sub-popups over it
                DismissAiBlockBuddy();
                Hide();
                appSettings.HideBlurWindow();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GentleBlock SuppressPopupIfBlockingEnded ex - " + ex.Message);
            }
            return true;
        }

        // Re-run the AI check for whatever is currently blocked (URL or app), after the user sets a goal/task
        // or convinces the AI. Resets the in-flight guard first so the same identity is checked again.
        private void RecheckCurrentWithAI()
        {
            CurrentURLBeingProcessedWithChatGPTAPI = "";
            if (IsAppBlocking())
                appSettings.ShowGentleBlockWindow(HWND, sActiveAppName);
            else
                appSettings.ShowGentleBlockWindow(HWND, "", sActiveURL, sCurrentTabTitle);
        }

        // Cuddly "this is distracting" popup flow for the APP check (CheckWithChatGPTForApp): ask for a
        // goal/task when none is set, let the user convince the AI, or set/pick a suggested task.
        // NOTE: despite the name, this is not actually shared with the URL check - CheckWithChatGPT has its
        // own separate, un-refactored copy of this same flow inline (search "block-popup-shown" there);
        // keep both in sync (and their "block-popup-shown" analytics calls) until they're unified.
        private void ShowDistractingCuddlyPopup(double xProbability)
        {
            //The app AI check is async - if the focus session ended while it was in flight, don't pop the
            //distracting/goal-task UI over the focus-end screen.
            if (SuppressPopupIfBlockingEnded())
                return;

            //Analytics: the AI came back with a distracting verdict, so - whichever branch below actually
            //renders (ask for a goal/task, the convince-AI dialog, or the main decision screen) - the user
            //is about to see a block popup. Includes the AI's probability/reason for this verdict, matching
            //what the auto-allow events already record.
            appSettings.CapturePostHog("block-popup-shown", BuildBlockPopupMetadata(true, xProbability));

            string identity = IsAppBlocking() ? sActiveAppName : sActiveURL;

            if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length == 0 && appSettings.sRunningFocusModeIntention.Length == 0)
            {
                //No task and no intention - fire the yes button to ask for a goal/task first.
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            else if (appSettings.sConvincingAIForURL == identity)
            {
                //AI was not convinced - show the "couldn't convince" popup and let the user try again.
                wndConvinceAiFailed = new ConvinceAIFailed(xProbability, nTimer, IsAppBlocking());
                wndConvinceAiFailed.Owner = this;

                appSettings.ShowBlurWindow(HWND);
                this.Show();
                dispatcherTimer.Stop();

                GridShade.Visibility = Visibility.Visible;
                bool? bResult = wndConvinceAiFailed.ShowDialog();
                GridShade.Visibility = Visibility.Collapsed;

                appSettings.sConvincingAIForURL = ""; //reset

                if (bResult.Value)
                {
                    if (wndConvinceAiFailed.bReportBadAIDecision)
                    {
                        Hyperlink link = BtnReportBadAIDecision;
                        var peerH = new HyperlinkAutomationPeer(link);
                        var invokeProvH = peerH.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        invokeProvH?.Invoke();
                    }
                    else if (wndConvinceAiFailed.bShowConvincePopupAgain) //Yes this is relevant
                    {
                        ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                        IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                        invokeProv.Invoke();
                    }
                    else if (wndConvinceAiFailed.bShowAskNewGoalPopup) //I am working on something else
                    {
                        ShowAskNewGoalPopup();
                    }
                    else if (wndConvinceAiFailed.bShowTemporaryAccessPopup)
                    {
                        wndTemporaryAccess = new TemporaryAccess(identity);
                        wndTemporaryAccess.Owner = this;

                        GridShade.Visibility = Visibility.Visible;
                        bool? bResultTemp = wndTemporaryAccess.ShowDialog();
                        GridShade.Visibility = Visibility.Collapsed;
                        if (bResult.Value) //User temporarily allowed it
                        {
                            dispatcherTimer.Stop();
                            appSettings.HideBlurWindow();
                            Hide();
                            return;
                        }
                        else //Block it
                        {
                            BlockSiteWork();
                        }
                        wndTemporaryAccess = null;
                    }
                }
                else
                {
                    BlockSiteWork();
                }
                wndConvinceAiFailed = null;
            }
            else if (appSettings.sChatGPTReasonForCurrentURL.Length > 0 && ShowAiBlockBuddy(xProbability))
            {
                // The redesigned check-in took over (AiBlockBuddyWindow).
            }
            else if (appSettings.sChatGPTReasonForCurrentURL.Length > 0) //show reason + suggested task from AI
            {
                JObject obj = JObject.Parse(appSettings.sChatGPTResponse);

                string suggestedTask = obj["suggested_task"]?.ToString();
                if (suggestedTask != null)
                {
                    string suggestedTaskId = obj["suggested_task_id"]?.ToString();

                    string sTask = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["suggestedTaskText"].ToString();
                    WorkingOnTask.Text = sTask.Replace("<TASK>", suggestedTask);

                    ComboBoxTasks.Text = suggestedTask; //Add the suggested task to the textview of the combo box

                    WantToChangeCurrentTask.Visibility = Visibility.Visible;
                    MarkPreviousTaskComplete.Visibility = Visibility.Visible;
                    ComboxBoxSection.Visibility = Visibility.Visible;

                    btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Collapsed;
                }
                else
                {
                    WorkingOnTask.Text = "";

                    WantToChangeCurrentTask.Visibility = Visibility.Collapsed;
                    MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                    ComboxBoxSection.Visibility = Visibility.Collapsed;

                    string sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoal"].ToString();
                    if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                    {
                        sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoalAndTask"].ToString();
                        sHead = sHead.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                    }
                    sHead = sHead.Replace("<GOAL>", appSettings.sRunningFocusModeIntention);
                    Heading.Text = sHead;

                    btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Visible;
                    btnNoIamWorkingOnSomethingElseText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonWorkingSomethingElseText"].ToString();

                    //App vs URL wording (this popup path is the app AI check).
                    btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"][IsAppBlocking() ? "buttonAppRelevantText" : "buttonSiteRelevantText"].ToString();
                }

                ScoreSection.Visibility = Visibility.Visible;
                string sScore = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"][IsAppBlocking() ? "aiRelevanceScoreAppText" : "aiRelevanceScoreText"].ToString();
                AIRelevanceScore.Text = sScore.Replace("<SCORE>", ((int)(xProbability * 100)).ToString());

                InfoIcon.ToolTip = appSettings.sChatGPTReasonForCurrentURL;
                if (suggestedTask != null) //only if there was a suggested task
                {
                    string sStillWorking = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttoWorkingOnOldTask"].ToString();
                    if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                    {
                        sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                        btnJustThisTimeText.Text = sStillWorking;
                    }
                    else if (appSettings.sRunningFocusModeIntention.Length > 0)
                    {
                        sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sRunningFocusModeIntention);
                        btnJustThisTimeText.Text = sStillWorking;
                    }
                }

                CommandManager.InvalidateRequerySuggested();
                //URL: only show if the browser is still on the same URL. App: always show (we know it's focused).
                if (IsAppBlocking() || appSettings.sLastActiveURLInFirefox == sActiveURL)
                {
                    dispatcherTimer.Start();
                    appSettings.ShowBlurWindow(HWND);
                    this.Show();
                }
            }
        }

        private void ShowAskNewGoalPopup()
        {
            if (bModalSubPopupOpen)
            {
                LogWriter.LogWrite("ShowAskNewGoalPopup - modal sub-popup already open, ignoring re-entrant call");
                return;
            }
            dispatcherTimer.Stop();

            wndAddGoal = new AddFocusGoalAndTask(false); //true for bNoGoalNoTask
            wndAddGoal.Owner = this;

            GridShade.Visibility = Visibility.Visible;
            bModalSubPopupOpen = true;
            try { wndAddGoal.ShowDialog(); } finally { bModalSubPopupOpen = false; }
            GridShade.Visibility = Visibility.Collapsed;

            wndAddGoal = null;

            //now we need to re-run the AI check for this url/app
            RecheckCurrentWithAI();

            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    if (sActiveAppName.Length > 0)
                    {
                        appSettings.TemporarryBlockedAppsGentleBlock.Add(sActiveAppName.ToLower());//block the app
                    }
                    else
                    {
                        appSettings.TemporarryBlockedURLsGentleBlock.Add(sActiveURL.ToLower());//block the URL
                        ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "focus"); //here we will send the full URL
                    }
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    appSettings.HideBlurWindow();
                    Hide();
                }
                else if (nTimer > 30)
                {
                    BorderTimer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BorderTimer.Visibility = Visibility.Visible;
                    if (nTimer == 15)
                    {
                        BorderTimer.Background = new SolidColorBrush(Colors.Orange);
                    }
                    else if (nTimer > 15)
                    {
                        BorderTimer.Background = new SolidColorBrush(Colors.Transparent);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowGentleBlockWindowAndStartTimer Window timer EX -  " + ex.Message);
            }

        }

        private void btnJustThisTime_Click(object sender, RoutedEventArgs e)
        {
            //Re-entrancy guard: this handler is also fired programmatically from the async AI callback,
            //so a fast tab switch could invoke it again while its own modal is still open. Refuse the
            //re-entrant call so we never stack a second goal/task (or convince) popup.
            if (bModalSubPopupOpen)
            {
                LogWriter.LogWrite("btnJustThisTime_Click - modal sub-popup already open, ignoring re-entrant call");
                return;
            }
            ///////////////////////////////////////////////////////////////////
            ///
            // One ladder, in FocusBear.Core: snooze > teacher > buddy > the block list's
            // is_ai_enabled > the global toggle. This used to be hand-rolled here and skipped
            // the block list, so the Mac's is_ai_enabled=false had no effect on Windows.
            bool bAICheckRequired = AiBlocking.IsEnabled();
            ReportBadAIDecision.Visibility = bAICheckRequired ? Visibility.Visible : Visibility.Collapsed;
            ///////////////////////////////////////////
            if (!bAICheckRequired) //No AI check - allow directly (URL or app)
            {
                if (sActiveAppName.Length > 0)
                    appSettings.TemporaryAllowedAppsGentleBlock.Add(new DistractingURL(sActiveAppName.ToLower(), true));
                else
                    appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), false));
            }
            else
            {
                //If the task name and intention are empty, we will ask the user the new goal and the task
                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0 || appSettings.sRunningFocusModeIntention.Length > 0)
                {
                    //ask user to convinve the AI

                    dispatcherTimer.Stop();
                    appSettings.sExtraJustificationForThisSite = ""; //Reset
                    //Ask the user to convinve the AI
                    wndConvince = new ConvinceAI(nTimer, IsAppBlocking()); //Pass the current timer value + app/URL wording
                    wndConvince.Owner = this;

                    GridShade.Visibility = Visibility.Visible;
                    bModalSubPopupOpen = true;
                    bool? bResult = null;
                    try { bResult = wndConvince.ShowDialog(); } finally { bModalSubPopupOpen = false; }
                    GridShade.Visibility = Visibility.Collapsed;

                    if (bResult.Value == true) //Convince clicked, get the reason
                    {
                        LogWriter.LogWrite("ConvinceAI with intention - " + wndConvince.TxtIntention.Text);
                        appSettings.sExtraJustificationForThisSite = wndConvince.TxtIntention.Text;

                        //store to compare when AI results come (URL or app identity).
                        appSettings.sConvincingAIForURL = IsAppBlocking() ? sActiveAppName : sActiveURL;

                        //now we need to re-run the AI check for this url/app with the new justification
                        RecheckCurrentWithAI();
                        dispatcherTimer.Stop();
                        appSettings.HideBlurWindow();
                        Hide();
                    }
                    else
                    {
                        //Cancel clicked or timer went off. wndConvince can already be null here if the
                        //dialog was force-closed from elsewhere (e.g. StopTimer on a tab switch), so guard
                        //against the NRE that crashed this path before.
                        if (wndConvince != null)
                            nTimer = wndConvince.nTimer;//Let's see how much time was spent after convince was taken off, start from there.
                        dispatcherTimer.Start();
                    }
                    wndConvince = null;
                    return;
                }
                else
                {
                    //Last-chance guard: if the focus session/schedule ended (e.g. it expired while this popup
                    //was being prepared), don't pop the no-goal/no-task modal over the focus-end screen.
                    if (SuppressPopupIfBlockingEnded())
                        return;

                    //Log which app/URL is triggering the no-goal/no-task popup, to identify spurious triggers.
                    LogWriter.LogWrite("GentleBlock - showing no-goal/no-task modal for " +
                        (IsAppBlocking() ? "app '" + sActiveAppName + "'" : "url '" + sActiveURL + "'") +
                        " | focusRunning=" + appSettings.isFocusModeRunning + " paused=" + appSettings.isFocusModePaused +
                        " scheduleRunning=" + appSettings.isBlockingScheduleRunning);

                    //If the task name and intention are empty, we will ask the user the new goal and the task
                    this.Show(); //We need to show the window too

                    dispatcherTimer.Stop();

                    wndAddGoal = new AddFocusGoalAndTask(true); //true for bNoGoalNoTask
                    wndAddGoal.Owner = this;

                    GridShade.Visibility = Visibility.Visible;
                    bModalSubPopupOpen = true;
                    bool? bResult = null;
                    try { bResult = wndAddGoal.ShowDialog(); } finally { bModalSubPopupOpen = false; }
                    GridShade.Visibility = Visibility.Collapsed;

                    wndAddGoal = null;

                    //now we need to re-run the AI check for this url/app
                    RecheckCurrentWithAI();

                    dispatcherTimer.Stop();
                    appSettings.HideBlurWindow();
                    Hide();

                }

                /*** if(AllowThisDomain.IsChecked.Value)
                 {
                     string sHost = "";
                     try
                     {
                         Uri myUri = new Uri(sActiveURL);
                         sHost = myUri.Host; //fails if there is no http://
                     }
                     catch (Exception ex)
                     {
                         /*
                         This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                         If uri does not specify a scheme, the scheme defaults to "http:".
                         * /
                         Uri myUri = new UriBuilder(sActiveURL).Uri;
                         sHost = myUri.Host;
                         Debug.WriteLine(ex.Message);
                     }

                     appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sHost.ToLower(), true, true));
                 }
                 else
                 {
                     appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));
                 }***/

            }
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        private void BlockSiteWork()
        {
            // The check is decided: forget the in-flight guard, or the same app is never asked about again. It was
            // left set for apps, so "Oops I actually need it" (which un-blocks the app and asks again) brought the app
            // back with no popup at all, and returning to it only re-checked after some other app had been checked.
            CurrentURLBeingProcessedWithChatGPTAPI = "";
            if (sActiveAppName.Length > 0)
            {
                appSettings.TemporarryBlockedAppsGentleBlock.Add(sActiveAppName.ToLower());
            }
            else
            {
                appSettings.TemporarryBlockedURLsGentleBlock.Add(sActiveURL.ToLower());
                ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "focus"); //here we will send the full URL
            }
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        private void btnGettingDistracted_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();
        }

        private void btnSaveForRelax_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();

            FocusBearAPIClient client = new FocusBearAPIClient();
            var sResponse = client.PostSavedWebsite(sFullURLForBlockedPage);
        }

        public void StopTimer()
        {
            dispatcherTimer.Stop();

            //reset and close all popups if any is open
            wndConvinceAiFailed?.dispatcherTimerBlock?.Stop();
            wndConvinceAiFailed?.Close();

            wndTemporaryAccess?.dispatcherTimerAutoBlock?.Stop();
            wndTemporaryAccess?.Close();

            wndAddGoal?.Close();

            wndConvince?.dispatcherTimerBlock?.Stop();
            wndConvince?.Close();

            wndConvinceAiFailed = null;
            wndTemporaryAccess = null;
            wndAddGoal = null;
            wndConvince = null;
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
        private void BtnReportBadAIDecision_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();

            // Reporting a bad AI decision means the AI wrongly blocked this site/app — so allow the current
            // URL/app too (same as the direct-allow path), otherwise it stays blocked after reporting. #1290
            if (IsAppBlocking())
                appSettings.TemporaryAllowedAppsGentleBlock.Add(new DistractingURL(sActiveAppName.ToLower(), true));
            else
                appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), false));

            //Built as JSON: a tab title or intention with a quote made the report invalid.
            string addItemJsonString = ReportBody(IsAppBlocking(), sActiveAppName, sActiveURL, sCurrentTabTitle,
                appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);

            //Show the Report Bad AI Desicion Feedback Window
            appSettings.ShowReportBadAIDecision(addItemJsonString, IsAppBlocking() ? sActiveAppName : sActiveURL);
        }

        private async void btnSetTask_Click(object sender, RoutedEventArgs e)
        {
            // Guard against repeated clicks while the async work is in flight.
            // Previously this handler never dismissed the popup or gave feedback,
            // so users clicked "Set Task" multiple times and created duplicate
            // tasks (each click hits the AddTask API).
            btnSetTask.IsEnabled = false;
            LogWriter.LogWrite("btnSetTask_Click()");

            TaskItem selectedTask = ComboBoxTasks.SelectedItem as TaskItem;

            string selectedName;
            string selectedId;

            if (selectedTask != null)
            {
                // Existing item selected
                selectedName = selectedTask.Name;
                selectedId = selectedTask.Id;

                if (appSettings.sAllTasks.Length > 0)
                {
                    JArray tasks = JArray.Parse(appSettings.sAllTasks);

                    JObject matchedTask = tasks.FirstOrDefault(t => t["id"]?.ToString() == selectedId) as JObject;

                    if (matchedTask != null)
                    {
                        // Found exact task
                        //Let's put it in the sCurrentTaskCreated and use the same in AddAndPlayTaskInCurrentList
                        appSettings.sCurrentTaskCreated = matchedTask.ToString();

                        //Add in current tasks list and play it in todoplayer
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;

                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                            {
                                ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                            }
                        }));

                        //now we need to re-run the AI check for this url/app
                        RecheckCurrentWithAI();
                    }
                }

            }
            else
            {
                // User typed new value
                selectedName = ComboBoxTasks.Text;
                selectedId = null;

                if (selectedName.Length > 0)
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var m = await client.AddTask(selectedName);
                    if (m == 1)
                    {
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;
                        //Add in current tasks list and play it in todoplayer
                        if (appSettings.sCurrentTaskCreated.Length > 0)
                        {
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                                {
                                    ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                                }
                            }));

                            //now we need to re-run the AI check for this url/app
                            RecheckCurrentWithAI();
                        }
                    }
                }
            }

            // Dismiss the popup once the task is set, mirroring AddFocusGoalAndTask
            // which closes after Set Task. Re-enable the button first so the popup
            // works again if it is re-shown (it is hidden, not closed).
            btnSetTask.IsEnabled = true;
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        // ── The AI block check-in (Mac "AI Blocking Latest") ─────────────────────────────────────────────

        private AiBlockBuddyWindow _aiBlockBuddy;
        private string _aiBlockBuddyKey = "";

        /// <summary>Whether the AI check-in (and so its backdrop) is on screen.</summary>
        internal bool IsAiBlockBuddyOpen => _aiBlockBuddy != null && _aiBlockBuddy.IsVisible;

        /// <summary>
        /// Takes the AI check-in down without blocking or allowing, for when blocking has ended. Closing it the
        /// ordinary way counts as "Block". Not part of <see cref="StopTimer"/>: that also runs each time this window
        /// takes up the next app or page, and the check-in decides for itself when the user has left its item.
        /// </summary>
        internal void DismissAiBlockBuddy()
        {
            if (_aiBlockBuddy == null) return;
            LogWriter.LogWrite("AiBlockBuddy - dismissed for " + _aiBlockBuddyKey + " (blocking ended)");
            var buddy = _aiBlockBuddy;
            _aiBlockBuddy = null;
            buddy.DismissStale();
        }

        private string CurrentItemKey() => FocusBear.Core.Blocking.AiBlockBuddy.ItemKey(IsAppBlocking(), sActiveAppName, sActiveURL);

        /// <summary>
        /// Closing the check-in blocks the app/page it was about - which, if this window has since moved on to the
        /// next app, is not the one in its fields now.
        /// </summary>
        private void BlockBuddyItem(bool isApp, string app, string url, string fullUrl, IntPtr hwnd)
        {
            if (!IsBlockingStillActive())
            {
                // The session or schedule ended while the check-in was up: there is nothing left to block, and
                // suppressing the page now would close a tab the user is free to use.
                LogWriter.LogWrite("AiBlockBuddy - closed after blocking ended, not blocking " + FocusBear.Core.Blocking.AiBlockBuddy.ItemKey(isApp, app, url));
                appSettings.HideBlurWindow();
                return;
            }
            if (CurrentItemKey() == FocusBear.Core.Blocking.AiBlockBuddy.ItemKey(isApp, app, url)) { BlockSiteWork(); return; }
            if (isApp) appSettings.TemporarryBlockedAppsGentleBlock.Add(app.ToLower());
            else
            {
                appSettings.TemporarryBlockedURLsGentleBlock.Add(url.ToLower());
                ProcessBrowserURLs.suppressURLForFirefox(hwnd, fullUrl, "focus");
            }
            appSettings.HideBlurWindow();
        }

        /// <summary>
        /// Shows the check-in in place of this window's decision view. Its outcomes use this window's own
        /// primitives: closing blocks (BlockSiteWork), an argued allow adds a timed temporary allow, a task
        /// switch plays that task and re-judges, and leaving the page just dismisses.
        /// </summary>
        private bool ShowAiBlockBuddy(double xProbability)
        {
            try
            {
                bool isApp = IsAppBlocking();
                string app = sActiveAppName;
                string url = sActiveURL;
                string key = FocusBear.Core.Blocking.AiBlockBuddy.ItemKey(isApp, app, url);
                if (_aiBlockBuddy != null)
                {
                    // One check-in at a time, for what is in front now: the same app/page keeps the one on screen;
                    // a different one replaces it (it used to be dropped, leaving the stale check-in up).
                    if (_aiBlockBuddyKey == key) return true;
                    LogWriter.LogWrite("AiBlockBuddy - replacing the check-in for " + _aiBlockBuddyKey + " with one for " + key);
                    var stale = _aiBlockBuddy;
                    _aiBlockBuddy = null;
                    stale.DismissStale();
                }
                // URL: only while the browser is still on this page (as the old view checked before showing).
                if (!isApp && appSettings.sLastActiveURLInFirefox != url) return true;
                string fullUrl = sFullURLForBlockedPage.Length > 0 ? sFullURLForBlockedPage : url;
                IntPtr hwnd = HWND;
                string itemProcess = isApp ? app : BlockForeground.ProcessOf(hwnd);
                var request = new AiBlockBuddyRequest
                {
                    IsApp = isApp,
                    Url = fullUrl,
                    PageTitle = sCurrentTabTitle,
                    Label = FocusBear.Core.Blocking.AiBlockBuddy.DisplayLabel(isApp, app, fullUrl, sCurrentTabTitle),
                    Reason = appSettings.sChatGPTReasonForCurrentURL,
                    Probability = xProbability,
                    CurrentTask = appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0 ? appSettings.sCurrentTaskPlayingInToDoPlayer : appSettings.sRunningFocusModeIntention,
                    Argue = async why =>
                    {
                        appSettings.AddJustification(why);
                        appSettings.sExtraJustificationForThisSite = why;
                        var client = new FocusBearAPIClient();
                        double p = isApp
                            ? await client.ChatGPTIsAppSafe(app, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode)
                            : await client.ChatGPTIsURLSafe(url, sCurrentTabTitle, "", appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);
                        return p;
                    },
                    Allow = (minutes, why) => AllowForMinutes(isApp, isApp ? app.ToLower() : url.ToLower(), minutes),
                    Block = () => BlockBuddyItem(isApp, app, url, fullUrl, hwnd),
                    PickTask = task => SwitchTaskFromAiBlockBuddy(task),
                    Report = () => BtnReportBadAIDecision_Click(null, null),
                    ItemProcess = itemProcess,
                    StillOnItem = isApp ? (Func<bool>)null : () => appSettings.sLastActiveURLInFirefox == url,
                    // Leaving (or being replaced) neither blocks nor allows. Forget the in-flight marker so coming
                    // back to the app/page runs the check again - otherwise it is skipped as "already running" and
                    // the app would be let through. Only this item's own popup state is touched: this window may
                    // already be working on the next app.
                    Dismissed = () =>
                    {
                        if (CurrentURLBeingProcessedWithChatGPTAPI == (isApp ? app : url)) CurrentURLBeingProcessedWithChatGPTAPI = "";
                        appSettings.HideBlurWindow();
                        if (CurrentItemKey() == key) { dispatcherTimer.Stop(); Hide(); }
                    },
                };
                dispatcherTimer.Stop();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                appSettings.ShowBlurWindow(HWND);
                long backdropMs = sw.ElapsedMilliseconds;
                var buddy = new AiBlockBuddyWindow(request);
                long builtMs = sw.ElapsedMilliseconds;
                _aiBlockBuddy = buddy;
                _aiBlockBuddyKey = key;
                buddy.Closed += (s, e) => { if (_aiBlockBuddy == buddy) _aiBlockBuddy = null; };
                buddy.Show();
                buddy.Activate();
                LogWriter.LogWrite("AiBlockBuddy - shown for " + request.Label + " (backdrop " + backdropMs + " ms, build " + (builtMs - backdropMs)
                    + " ms, show " + (sw.ElapsedMilliseconds - builtMs) + " ms)");
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowAiBlockBuddy ex - " + ex.Message);
                _aiBlockBuddy = null;
                return false;   // the old decision view still works
            }
        }

        /// <summary>A temporary allow that lapses after <paramref name="minutes"/>, so the buddy checks in again.</summary>
        private void AllowForMinutes(bool isApp, string key, int minutes)
        {
            var entry = new DistractingURL(key, true);
            if (isApp) appSettings.TemporaryAllowedAppsGentleBlock.Add(entry);
            else appSettings.TemporaryAllowedURLsGentleBlock.Add(entry);
            appSettings.CapturePostHog("block-popup-allowed-after-explaining", BuildBlockPopupMetadata(true));
            var expiry = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(1, minutes)) };
            expiry.Tick += (s, e) =>
            {
                expiry.Stop();
                if (isApp) appSettings.TemporaryAllowedAppsGentleBlock.Remove(entry);
                else appSettings.TemporaryAllowedURLsGentleBlock.Remove(entry);
                CurrentURLBeingProcessedWithChatGPTAPI = "";
                LogWriter.LogWrite("AiBlockBuddy - allow for " + key + " lapsed after " + minutes + " min");
            };
            expiry.Start();
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        /// <summary>Plays the picked task (a new to-do when it is not one yet), then re-judges the page against it.</summary>
        private async void SwitchTaskFromAiBlockBuddy(Habits.DayPlan.DayPlanTask task)
        {
            try
            {
                JObject matched = null;
                if (appSettings.sAllTasks.Length > 0)
                {
                    JArray all = JArray.Parse(appSettings.sAllTasks);
                    if (!string.IsNullOrEmpty(task.SourceTaskId))
                        matched = all.FirstOrDefault(t => t["id"]?.ToString() == task.SourceTaskId) as JObject;
                    if (matched == null)
                        matched = all.FirstOrDefault(t => string.Equals(t["title"]?.ToString(), task.Title, StringComparison.OrdinalIgnoreCase)) as JObject;
                }
                bool ready;
                if (matched != null)
                {
                    appSettings.sCurrentTaskCreated = matched.ToString();
                    ready = true;
                }
                else ready = await new FocusBearAPIClient().AddTask(task.Title) == 1 && appSettings.sCurrentTaskCreated.Length > 0;
                if (ready) ((App)Application.Current).wndFocusBearMainWindow.PageToDoPlayer?.AddAndPlayTaskInCurrentList(false);
                RecheckCurrentWithAI();
            }
            catch (Exception ex) { LogWriter.LogWrite("SwitchTaskFromAiBlockBuddy ex - " + ex.Message); }
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        private void BtnBlockThisPage_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();
        }

        private void ComboBoxTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void btnNoIamWorkingOnSomethingElse_Click(object sender, RoutedEventArgs e)
        {
            ShowAskNewGoalPopup();
        }
    }
}
