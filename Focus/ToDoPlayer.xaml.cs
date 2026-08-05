using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.UI.WebControls.Adapters;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for ToDoPlayer.xaml
    /// </summary>
    public partial class ToDoPlayer : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private double _lastSentFocusAlignmentScore = -1;

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            //On a user request allowing full minimization of the todo player - 06-Jan-2025

            return IntPtr.Zero;
        }

        public ToDoPlayer()
        {
            InitializeComponent();

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            App.ThemeChanged += TitleBarThemeChange;

            btnEndFocusLinkTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonEndFocusSessionText"].ToString();
        }

        private async Task SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webView != null)
                {
                    await webView.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SelectTasks SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                LoadMetaData();
            }
        }

        // Path A initial load + Path B reactive reply. Pass the echoed requestId on a reply
        // (omit on initial load); it rides along inside the same metadata payload.
        public void LoadMetaData(string requestId = null)
        {
            {
                DateTime end = DateTime.UtcNow;
                if (appSettings.sRunningFocusModeEndTime.Length > 0)
                {
                    end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                }
                
                DateTime start = DateTime.UtcNow;

                // If end time is earlier than start time, assume it's on the next day
                if (end < start)
                {
                    end = end.AddDays(1);
                }

                TimeSpan remaining = end.Subtract(start);
                double durationInMinutes = remaining.TotalMinutes;

                //get the brain dump if saved from previous
                RegistryManager.GetBrainDump();

                string sTheme = appSettings.sTheme == "Light" ? "LIGHT" : "DARK";
                bool isClassSessionTodo = !string.IsNullOrEmpty(appSettings.sClassSessionTodoSessionId);
                // Simple mode clears personal tasks so intention-only UX wins — keep class-mode empty inject.
                if (!appSettings.bIsGeekModeEnabled && !isClassSessionTodo)
                {
                    appSettings.sRawTasks = "[]"; //fix for amanuel's below work not handled in simple mode to show the intention
                }
                // Class session: always inject empty tasks; FE loads checklist via class_session_id.
                if (isClassSessionTodo)
                {
                    appSettings.sRawTasks = "[]";
                }

                string serializedMetaData;
                if (isClassSessionTodo)
                {
                    var metaData = new
                    {
                        access_token = appSettings.userAccount.accessToken,
                        platform = "win",
                        tasks = JsonDocument.Parse("[]").RootElement.Clone(),
                        class_session_id = appSettings.sClassSessionTodoSessionId,
                        total_duration = durationInMinutes,
                        lang = appSettings.sLangCode,
                        font = appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont),
                        theme = sTheme,
                        intention = appSettings.sRunningFocusModeIntention,
                        brain_dump = appSettings.sBrainDump
                    };
                    serializedMetaData = JsonSerializer.Serialize(metaData);
                    LogWriter.LogWrite("TodoPlayer LoadMetaData - class_session_id=" +
                        appSettings.sClassSessionTodoSessionId + " tasks=[]");
                }
                else
                {
                    var metaData = new
                    {
                        access_token = appSettings.userAccount.accessToken,
                        platform = "win",
                        tasks = JsonDocument.Parse(string.IsNullOrWhiteSpace(appSettings.sRawTasks) ? "[]" : appSettings.sRawTasks).RootElement.Clone(),
                        total_duration = durationInMinutes,
                        lang = appSettings.sLangCode,
                        font = appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont),
                        theme = sTheme,
                        intention = appSettings.sRunningFocusModeIntention,
                        brain_dump = appSettings.sBrainDump
                    };
                    serializedMetaData = JsonSerializer.Serialize(metaData);
                }

                // Path B - echo the requestId back on a REQUEST_ACCESS_TOKEN reply (omitted on initial load).
                if (!string.IsNullOrEmpty(requestId))
                {
                    var jo = Newtonsoft.Json.Linq.JObject.Parse(serializedMetaData);
                    jo["requestId"] = requestId;
                    serializedMetaData = jo.ToString(Newtonsoft.Json.Formatting.None);
                }

                string LoadTokenFunc = $@"
                    const waitForAppLoaded = setInterval(() => {{
                        if (window.loadMetaDataForToDoPlayer) {{
                            loadMetaDataForToDoPlayer({serializedMetaData});
                            clearInterval(waitForAppLoaded);
                        }}
                    }}, 100);
                ";

                var sReturn = webView.ExecuteScriptAsync(LoadTokenFunc);
            }            
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.WebMessageReceived += webView_WebMessageReceived;

                    LoadURL();

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("SelectTasks CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        public void LoadURL()
        {
            string sTodoPlayerURL = "https://dashboard.focusbear.io/webview/todo-player?bust_cache=" + DateTime.UtcNow.ToString();

            webView.Source = new System.Uri(sTodoPlayerURL);
        }

        private async void btnEndFocus_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Todo Player btnEndFocus_Click - Stopping Focus Session");

            try
            {
                app.HideToDoPlayer();

                ((App)Application.Current).StopFocusSessionFromEvents();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"Error in btnEndFocus_Click: {ex.Message}");
            }
        }


        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("todo player - webView_WebMessageReceived() - tasks got from webview");

                string sContent = e.WebMessageAsJson;

                // Path B - reactive token refresh. If the webview is asking for a token, handle
                // it and stop; it is not a normal task/brain-dump message.
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(sContent);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        LoadMetaData(req.RequestId);
                    return;
                }

                JObject sContentJson = JObject.Parse(sContent);
                if (sContentJson.ContainsKey("brain_dump_content"))
                {
                    appSettings.sBrainDump = sContentJson["brain_dump_content"].ToString();
                    RegistryManager.SaveBrainDump();
                    //Update the braindump window ui
                    if (app.wndBrainDump != null)
                    {
                        app.wndBrainDump.TxtDumpDown.Text = appSettings.sBrainDump;
                    }
                }
                if (sContentJson.ContainsKey("tasks"))
                {
                    // FE owns class checklist; do not write player tasks into personal focus stores.
                    if (string.IsNullOrEmpty(appSettings.sClassSessionTodoSessionId))
                    {
                        appSettings.sRawTasks = sContentJson["tasks"].ToString();
                        if(appSettings.sRawTasks == "[]")
                        {
                            appSettings.sRawTasks = "";//empty it
                        }

                        //We need to add it back to the focus mode
                        var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                        if (listItems.Count() != 0)
                        {
                            FocusMode CurrentRunningFocusMode = listItems.First<FocusMode>();
                            CurrentRunningFocusMode.lastTaskUsed = appSettings.sRawTasks;
                        }

                        //Find the isPlaying task
                        if (appSettings.sRawTasks.Length > 0)
                        {
                            JArray tasks = (JArray)sContentJson["tasks"];

                            JObject playingTask = tasks.FirstOrDefault(t => (bool?)t["isPlaying"] == true) as JObject;

                            if (playingTask != null)
                            {
                                appSettings.sCurrentTaskPlayingInToDoPlayer = (string)playingTask["title"];
                            }
                        }

                        //If we find a change in tasks, we should call the Get all tasks API to get the list of updated tasks
                        //We will need to get the list of all the tasks to show in gentle block
                        FocusBearAPIClient client = new FocusBearAPIClient();
                        _ = client.GetAllTasks();
                    }
                    else
                    {
                        appSettings.sRawTasks = "[]";
                    }
                }
                if (sContentJson.ContainsKey("intention"))
                {
                    appSettings.sRunningFocusModeIntention = sContentJson["intention"].ToString();
                    App.FocusButton.SendFocusGoal(appSettings.sRunningFocusModeIntention);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SelectTasks webView_WebMessageReceived()" + ex.Message);
            }
        }

        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }

            try
            {
                if (webView?.CoreWebView2 == null)
                    return;

                string themeValue = appSettings.sTheme == "Light" ? "LIGHT" : "DARK";
                string script = $@"
                    (function() {{
                        if (window.syncWithDevices) {{
                            window.syncWithDevices({{ theme: ""{themeValue}"" }});
                        }}
                    }})();
                ";

                var _ = webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TitleBarThemeChange syncWithDevices ex - " + ex.Message);
            }
        }

        public void UpdateBrainDumpContent(string sBrainDump)
        {
            try
            {
                string escapedBrainDump = JsonSerializer.Serialize(sBrainDump);
                string UpdateFunc = $"updateBrainDumpFromDesktopApp({escapedBrainDump});";

                var sReturn = webView.ExecuteScriptAsync(UpdateFunc);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateBrainDumpContent ex- " + ex.Message);
            }
        }
        public void UpdateIntentionForToDoPlayer()
        {
            try
            {
                string escapedIntention = JsonSerializer.Serialize(appSettings.sRunningFocusModeIntention);
                string UpdateFunc = $"loadIntentionForToDoPlayer({escapedIntention});";

                var sReturn = webView.ExecuteScriptAsync(UpdateFunc);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateBrainDumpContent ex- " + ex.Message);
            }
        }
        public async void AddAndPlayTaskInCurrentList(bool bMarkCurrentTaskAsComplete = false) //OK 
        {
            try
            {
                if (appSettings.sCurrentTaskCreated.Length > 0)
                {
                    LogWriter.LogWrite("AddAndPlayTaskInCurrentList - " + appSettings.sCurrentTaskCreated);

                    JObject currentTask = JObject.Parse(appSettings.sCurrentTaskCreated);

                    JObject selectedTaskDic = new JObject
                    {
                        ["id"] = currentTask["id"],
                        ["title"] = currentTask["title"],
                        ["duration"] = currentTask["duration"],
                        ["isPlaying"] = true
                    };

                    //Just updating the local copy meanwhile we get notification from the webview
                    appSettings.sCurrentTaskPlayingInToDoPlayer = currentTask["title"].ToString();

                    if (bMarkCurrentTaskAsComplete)
                    {
                        selectedTaskDic["markCurrentTaskAsComplete"] = true;
                    }

                    JObject payload = new JObject
                    {
                        ["todoPlayer"] = new JObject
                        {
                            ["selectedTask"] = selectedTaskDic
                        }
                    };

                    string payloadString = payload.ToString();
                    string jsFunction = $@"
                                (function() {{
                                    if (window.__taskInterval) {{
                                        clearInterval(window.__taskInterval);
                                    }}

                                    window.__taskInterval = setInterval(() => {{
                                        if (window.syncWithDevices) {{
                                            try {{
                                                const data = {payloadString};
                                                syncWithDevices(data);

                                                clearInterval(window.__taskInterval);
                                                delete window.__taskInterval;
                                            }}
                                            catch (error) {{
                                                console.error('JS Error:', error);

                                                clearInterval(window.__taskInterval);
                                                delete window.__taskInterval;
                                            }}
                                        }}
                                    }}, 100);
                                }})();
                                ";

                    // Example WebView2 execution
                    await webView.ExecuteScriptAsync(jsFunction);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddAndPlayTaskInCurrentList ex- " + ex.Message);
            }
        }

        public void NotifyFocusAlignmentScore(double score)
        {
            if (score == _lastSentFocusAlignmentScore)
                return;

            _lastSentFocusAlignmentScore = score;

            try
            {
                if (webView?.CoreWebView2 == null)
                    return;

                string scoreValue = score.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string script = $@"
                    (function() {{
                        if (window.syncWithDevices) {{
                            window.syncWithDevices({{ todoPlayer: {{ focusAlignmentScore: {scoreValue} }} }});
                        }}
                    }})();
                ";

                LogWriter.LogWrite("NotifyFocusAlignmentScore - sending score to ToDoPlayer: " + scoreValue);

                var _ = webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NotifyFocusAlignmentScore ex - " + ex.Message);
            }
        }

        public void ResetFocusAlignmentScore()
        {
            _lastSentFocusAlignmentScore = -1;
        }

    }
}
