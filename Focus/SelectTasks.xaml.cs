using Focus_Bear.Pomodoro;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
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
    /// Interaction logic for SelectTasks.xaml
    /// </summary>
    public partial class SelectTasks : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        bool bFocusModeEndWindow = false;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public SelectTasks(StartFocusMode ParentWindow)
        {
            InitializeComponent();
            this.Owner = ParentWindow;
            InitialSettings();
        }
        public SelectTasks(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();
            this.Owner = ParentWindow;
            InitialSettings();
        }
        private void InitialSettings()
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            bFocusModeEndWindow = false;
            appSettings.sRawTasks = ""; //Initialize with empty task list

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
        }


        public SelectTasks(FocusModeEnd ParentWindow)
        {
            InitializeComponent();
            this.Owner = ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            bFocusModeEndWindow = true;

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
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
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"theme\":{1}}}", appSettings.userAccount.accessToken, sTheme);
                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadAccessTokenForToDo) {{ loadAccessTokenForToDo({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

                if (bFocusModeEndWindow)
                {
                    DateTime start = DateTime.Parse(appSettings.sRunningFocusModeStartTime).ToUniversalTime();
                    DateTime end = DateTime.UtcNow;
                    TimeSpan difference = end.Subtract(start); // could also write `now - otherTime`
                    double durationMinutes = difference.TotalMinutes;
                    LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"tasks\":{1},\"total_duration\":{2},lang:\"{3}\",\"theme\":{4}}}", appSettings.userAccount.accessToken, appSettings.sRawTasks, durationMinutes.ToString(), appSettings.sLangCode,sTheme);
                    LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadTasks) {{ loadTasks({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);
                }

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

                    string sTasksURL = "https://dashboard.focusbear.io/webview/todo-list?bust_cache=" + DateTime.UtcNow.ToString();

                    if (bFocusModeEndWindow)
                    {
                        sTasksURL = "https://dashboard.focusbear.io/webview/focus-end?bust_cache=" + DateTime.UtcNow.ToString();
                    }

                    webView.Source = new System.Uri(sTasksURL);

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("SelectTasks CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("webView_WebMessageReceived() - tasks got from webview");

                if (bFocusModeEndWindow)
                {
                    if (e.WebMessageAsJson == "\"close-focus-end-ui\"") //means user just closed the window from webview
                    {
                        appSettings.sRawTasksDone = "";
                    }
                    else
                    {
                        appSettings.sRawTasksDone = e.WebMessageAsJson;
                    }
                }
                else
                {
                   

                    if (e.WebMessageAsJson == "\"close-to-do-task-ui\"") //means user just closed the window from webview
                    {
                        appSettings.sRawTasks = "";
                    }
                    else
                    {
                        appSettings.sRawTasks = e.WebMessageAsJson;
                    }
                }

                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SelectTasks webView_WebMessageReceived()" + ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                webView.Dispose();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SelectTasks webView.Dispose(); - " + ex.Message);
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            var isLightTheme = Global.IsLightTheme();
            if (appSettings.bAutoTheme)
            {
                if (source != null)
                {
                    app.UseImmersiveDarkMode(source.Handle, !isLightTheme);
                }
            }
            else
            {
                if (appSettings.sTheme == "Light")
                {
                    app.UseImmersiveDarkMode(source.Handle, false);
                }
                else if (appSettings.sTheme == "Dark")
                {
                    app.UseImmersiveDarkMode(source.Handle, true);
                }
            }
        }
    }
}