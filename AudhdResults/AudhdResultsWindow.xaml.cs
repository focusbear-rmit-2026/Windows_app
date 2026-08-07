using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;

namespace Focus_Bear.AudhdResults
{
    /// <summary>
    /// Full-screen WebView2 window that renders the gated advanced AuDHD results,
    /// opened from a <c>focusbear://view-audhd-results?test-code=…</c> deep link
    /// (Focus-Bear/online-tests#57). The URL is hidden (no address bar) and the page
    /// itself performs the app-attested results fetch.
    /// </summary>
    public partial class AudhdResultsWindow : Window
    {
        private readonly string _resultsUrl;

        public AudhdResultsWindow(string resultsUrl)
        {
            InitializeComponent();
            _resultsUrl = resultsUrl;
            Loaded += async (s, e) => await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Reuse the same WebView2 user-data folder as the rest of the app (see HelpTab).
                var userDataFolder = Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                await webView.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AudhdResultsWindow InitializeWebViewAsync ex - " + ex.Message);
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                LogWriter.LogWrite("AudhdResultsWindow WebView2 init failed");
                return;
            }

            try
            {
                var settings = webView.CoreWebView2.Settings;
                settings.IsScriptEnabled = true;
                settings.IsWebMessageEnabled = true;
                // Kiosk-style: no dev-tools accelerator keys, no right-click menu (mirrors HelpTab).
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;

                webView.Source = new Uri(_resultsUrl);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AudhdResultsWindow CoreWebView2InitializationCompleted ex - " + ex.Message);
            }
        }

        /// <summary>Re-point an already-open results window at a newer deep link.</summary>
        public void Navigate(string resultsUrl)
        {
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(resultsUrl);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AudhdResultsWindow Navigate ex - " + ex.Message);
            }
        }
    }
}
