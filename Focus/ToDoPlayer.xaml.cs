using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for ToDoPlayer.xaml
    /// </summary>
    public partial class ToDoPlayer : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
            // 0xF020 == SC_MINIMIZE, command to minimize the window.
            /**if (msg == 0x0112 && ((int)wParam & 0xFFF0) == 0xF020)
            {
                // Cancel the minimize.
                handled = true;
                this.WindowState = WindowState.Normal;
                this.Height = 306;
                this.Width = 504;
            }**/ //On a user request allowing full minimization of the todo player - 06-Jan-2025

            return IntPtr.Zero;
        }

        public ToDoPlayer()
        {
            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.SourceInitialized += new EventHandler(OnSourceInitialized);

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            App.ThemeChanged += TitleBarThemeChange;
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
                DateTime start = DateTime.Parse(appSettings.sRunningFocusModeStartTime).ToUniversalTime();
                DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime(); 
                TimeSpan difference = end.Subtract(start); // could also write `now - otherTime`
                double durationMinutes = difference.TotalMinutes;
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");

                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"total_duration\":{1},lang:\"{2}\",\"font\":\"{3}\",\"theme\":{6},\"intention\":\"{4}\",\"brain_dump\":\"{5}\"}}", appSettings.userAccount.accessToken, durationMinutes.ToString(), appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sRunningFocusModeIntention, appSettings.sBrainDump,sTheme);

                if (appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 2)
                {
                    LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"tasks\":{1},\"total_duration\":{2},lang:\"{3}\",\"font\":\"{4}\",\"theme\":{7},\"intention\":\"{5}\",\"brain_dump\":\"{6}\"}}", appSettings.userAccount.accessToken, appSettings.sRawTasks, durationMinutes.ToString(), appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sRunningFocusModeIntention, appSettings.sBrainDump, sTheme);
                }

                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataForToDoPlayer) {{ loadMetaDataForToDoPlayer({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);
             
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
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void btnEndFocus_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox msgBox = new FBMessageBox(
                "Are you sure you want to end the focus session early?",
                "End Focus Session",
                MessageBoxButton.YesNo,
                btnYesText: "Yes",
                btnNoText: "No"
            );

            msgBox.Owner = this;
            msgBox.ShowDialog();

            if (msgBox.btnClicked == MessageBoxResult.Yes)
            {
                LogWriter.LogWrite("btnEndFocus_Click - Stopping Focus Session");

                try
                {
                    Hide();

                    var runningFocusModes = appSettings.arrFocusModes.Where(i => i.isOn == true);
                    if (!runningFocusModes.Any())
                    {
                        LogWriter.LogWrite("No focus mode is currently running");
                        return;
                    }

                    FocusMode currentRunningFocusMode = runningFocusModes.First();

                    StopFocusMode stopDialog = new StopFocusMode();
                    bool? dialogResult = stopDialog.ShowDialog();

                    if (dialogResult.Value == false)
                    {
                        return;
                    }
                    currentRunningFocusMode.isOn = false;
                    Sounds.PlayFocusModeEndSound();

                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                    FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                    appSettings.bUseSuperPomodoroMode = false;

                    appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                    appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                    appSettings.isFocusModeRunning = false;

                    ((App)Application.Current).CloseFocusProgressWindow();

                    ((App)(Application.Current)).HideToDoPlayer();

                    LogWriter.LogWrite("Focus session stopped successfully");
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite($"Error in btnEndFocus_Click: {ex.Message}");
                }
            }
        }


        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("todo player - webView_WebMessageReceived() - tasks got from webview");

                string sContent = e.WebMessageAsJson;
                JObject sContentJson = JObject.Parse(sContent);
                if (sContentJson.ContainsKey("brain_dump_content"))
                {
                    appSettings.sBrainDump = sContentJson["brain_dump_content"].ToString();
                }
                if (sContentJson.ContainsKey("tasks"))
                {
                    appSettings.sRawTasks = sContentJson["tasks"].ToString();
                    if(appSettings.sRawTasks == "[]")
                    {
                        appSettings.sRawTasks = "";//empty it
                    }

                    //We need to add it back to the focus mode
                    var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                    FocusMode CurrentRunningFocusMode = listItems.First<FocusMode>();
                    CurrentRunningFocusMode.lastTaskUsed = appSettings.sRawTasks;

                }
                if (sContentJson.ContainsKey("intention"))
                {
                    appSettings.sRunningFocusModeIntention = sContentJson["intention"].ToString();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SelectTasks webView_WebMessageReceived()" + ex.Message);
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }

        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }

    }
}