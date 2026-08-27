using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Threading.Tasks;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// Interaction logic for BrainDumpWindow.xaml - hosts the shared web dashboard's Notes page
    /// (opened directly to the Brain Dump section), replacing the old local-only text box. Any
    /// existing local Brain Dump text is migrated to the backend once, elsewhere
    /// (AppSettings.MigrateBrainDumpToBackendIfNeeded), before this page ever loads it.
    /// </summary>
    public partial class BrainDumpWindow : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public BrainDumpWindow()
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

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            App.ThemeChanged += TitleBarThemeChange;
        }

        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }

            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.Reload();
            }
        }

        // Kept for existing callers (App.ShowBrainDumpWindow, the "please spare me" popup path,
        // Pomodoro postpone) - the webview loads itself once and stays loaded, so there's nothing
        // else to (re)populate here now.
        public void ShowBrainDumpWindowAndStartTimer()
        {
            LogWriter.LogWrite("ShowBrainDumpWindowAndStartTimer()");
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
                LogWriter.LogWrite("BrainDumpWindow SetWebView() Ex - " + ex.Message);
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    LogWriter.LogWrite("BrainDumpWindow CoreWebView2InitializationCompleted() Success");

                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.WebMessageReceived += webView_WebMessageReceived;

                    webView.Source = new Uri("https://dashboard.focusbear.io/webview/notes?open=braindump");
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("BrainDumpWindow CoreWebView2InitializationCompleted() ex - " + ex.Message);
                }
            }
        }

        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                LogWriter.LogWrite("BrainDumpWindow CoreWebView2NavigationCompleted() Success, init app metadata");
                InitAppMetaData();
            }
        }

        // Path A initial load + Path B reactive reply. Pass the echoed requestId on a reply
        // (omit on initial load); it rides along with the other flags in the same payload.
        // This webview's own page expects window.initAppMetaData(...) specifically (confirmed from
        // the mac app's equivalent screen) - not the loadAccessTokenFor<Screen> convention other
        // Windows webview screens (e.g. Stats) use for their own, different pages.
        public void InitAppMetaData(string requestId = null)
        {
            string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");

            string sRequestId = string.IsNullOrEmpty(requestId) ? "" : string.Format(",\"requestId\":\"{0}\"", requestId);

            string Settings = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"theme\":{1}{2}}}", appSettings.userAccount.accessToken, sTheme, sRequestId);
            string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.initAppMetaData) {{ initAppMetaData({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);

            var ss = webView.ExecuteScriptAsync(settingsFunc);
            LogWriter.LogWrite(ss.ToString());
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Reactive token refresh, same as the other dashboard webview screens.
            try
            {
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(e.WebMessageAsJson);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        InitAppMetaData(req.RequestId);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BrainDumpWindow webView_WebMessageReceived() ex - " + ex.Message);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
