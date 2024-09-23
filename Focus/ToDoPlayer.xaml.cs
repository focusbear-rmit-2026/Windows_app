using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        public ToDoPlayer()
        {
            InitializeComponent();

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
                DateTime start = DateTime.Parse(appSettings.sRunningFocusModeStartTime).ToUniversalTime();
                DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime(); 
                TimeSpan difference = end.Subtract(start); // could also write `now - otherTime`
                double durationMinutes = difference.TotalMinutes;

                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"total_duration\":{1},lang:\"{2}\",\"font\":\"{3}\",\"intention\":\"{4}\",\"brain_dump\":\"{5}\"}}", appSettings.userAccount.accessToken, durationMinutes.ToString(), appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sRunningFocusModeIntention, appSettings.sBrainDump);

                if (appSettings.sRawTasks.Length > 0)
                {
                    LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"tasks\":{1},\"total_duration\":{2},lang:\"{3}\",\"font\":\"{4}\",\"intention\":\"{5}\",\"brain_dump\":\"{6}\"}}", appSettings.userAccount.accessToken, appSettings.sRawTasks, durationMinutes.ToString(), appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sRunningFocusModeIntention, appSettings.sBrainDump);
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
            Hide();

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

    }
}
