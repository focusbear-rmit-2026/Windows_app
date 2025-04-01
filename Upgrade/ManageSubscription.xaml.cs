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

namespace Focus_Bear.Upgrade
{
    /// <summary>
    /// Interaction logic for ManageSubscription.xaml
    /// </summary>
    public partial class ManageSubscription : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public ManageSubscription()
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
                LogWriter.LogWrite("ManageSubscription SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"type\":\"win\",lang:\"{1}\",\"font\":\"{2}\",\"theme\":{3}}}", appSettings.userAccount.accessToken, appSettings.sLangCode, appSettings.sSelectedAppFont,sTheme);

                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataForSubscription) {{ loadMetaDataForSubscription({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

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
                    LogWriter.LogWrite("ManageSubscription CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        public void LoadURL()
        {
            string sSubscriptionURL = "https://dashboard.focusbear.io/webview/manage-subscription?bust_cache=" + DateTime.UtcNow.ToString();

            webView.Source = new System.Uri(sSubscriptionURL);
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("todo player - webView_WebMessageReceived() - tasks got from webview");
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ManageSubscription webView_WebMessageReceived()" + ex.Message);
            }
        }
    }
}
