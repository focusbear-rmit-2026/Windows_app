using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
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

namespace Focus_Bear.Survey
{
    /// <summary>
    /// Interaction logic for SurveyWindow.xaml
    /// </summary>
    public partial class SurveyWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public SurveyWindow()
        {
            InitializeComponent();

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;

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
                LogWriter.LogWrite("SurveyWindow SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\"}}", appSettings.userAccount.accessToken);
                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadAccessTokenForSurvey) {{ loadAccessTokenForSurvey({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);
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

                    string sSurveyURL = "https://dashboard.focusbear.io/webview/survey?bust_cache=" + DateTime.UtcNow.ToString();
                    webView.Source = new System.Uri(sSurveyURL);

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("survey CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("webView_WebMessageReceived() - survey ");
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("survey webView_WebMessageReceived() - survey - " + ex.Message);
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
                LogWriter.LogWrite("survey webView.Dispose(); - " + ex.Message);
            }
           
        }
    }
}
