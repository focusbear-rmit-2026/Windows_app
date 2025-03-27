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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for Stats.xaml
    /// </summary>
    public partial class Stats : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public Stats()
        {
            InitializeComponent();

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            App.ThemeChanged += ReloadWebView;

        }        
        private void ReloadWebView()
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.Reload();
            }
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
                LogWriter.LogWrite("EDIT habits SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                LogWriter.LogWrite("stats CoreWebView2NavigationCompleted() Success, Load Access token");
                Mouse.OverrideCursor = null;

                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");

                string Settings = string.Format("{{\"access_token\":\"{0}\",\"type\":\"win\",\"lang\":\"{1}\",\"font\":\"{2}\",\"theme\":{3}}}", appSettings.userAccount.accessToken, appSettings.sLangCode, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont),sTheme);
                string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadAccessTokenForStats) {{ loadAccessTokenForStats({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);

                var ss = webView.ExecuteScriptAsync(settingsFunc);
                LogWriter.LogWrite(ss.ToString());
            }
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                    LogWriter.LogWrite("stats CoreWebView2InitializationCompleted() Success");
                   
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.Source = new System.Uri("https://dashboard.focusbear.io/webview/stats");
                   
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("stats CoreWebView2InitializationCompleted() ex - " + ex.Message);
                }
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }
    }
}
