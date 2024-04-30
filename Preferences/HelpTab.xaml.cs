using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for HelpTab.xaml
    /// </summary>
    public partial class HelpTab : Page
    {
        PreferencesWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public HelpTab()
        {
        }
        public HelpTab(PreferencesWindow pp)
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

            _ParentWindow = pp;

            SetWebView();
            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;

    
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["preferencesInfo"]["helpText"].ToString();
                btnExportLogsTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonExportLogsText"].ToString();

                ReportProblemTab.Header = appSettings.ConfigFileStrings["preferencesInfo"]["tabReportProblemText"].ToString();
                ChatTab.Header = appSettings.ConfigFileStrings["preferencesInfo"]["tabChatText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab() texts ex=" + ex.Message);
            }

        }
        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
               
                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["preferencesInfo"]["panelExportLogsText"].ToString();

                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string folderName = folderBrowserDialog1.SelectedPath;
                    string startPath = AppSettings.AppDataDirLogsPath;
                    string zipPath = folderName + "\\FocusBearLogs.zip";

                    if (File.Exists(zipPath))
                    {
                        Global.DeleteFile(zipPath, false);
                    }

                    ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);
                    string ss = appSettings.ConfigFileStrings["preferencesInfo"]["exportSuccessText"].ToString();
                    ///MessageBox.Show(ss.Replace("<URL>", folderName), "Focus Bear");
                    FBMessageBox ff = new FBMessageBox(ss.Replace("<URL>", folderName), "Focus Bear");
                    ff.Owner = _ParentWindow;
                    ff.ShowDialog();
                }


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab btnExportLogs_Click ex -" + ex.Message);
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
                LogWriter.LogWrite("HelpTab habits SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Mouse.OverrideCursor = null;
                LogWriter.LogWrite("Chat CoreWebView2NavigationCompleted() Success, Load Access token");
                string Settings = string.Format("{{\"access_token\":\"{0}\",\"font\":\"{1}\",lang:\"{2}\"}}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont), appSettings.sLangCode);
                string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadAccessTokenForChat) {{ loadAccessTokenForChat({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);
                var ss = webView.ExecuteScriptAsync(settingsFunc);

                
            }
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                    LogWriter.LogWrite("Chat CoreWebView2InitializationCompleted() Success, Load URL");
                   
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.Source = new System.Uri("https://dashboard.focusbear.io/webview/chat");

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HelpTab CoreWebView2InitializationCompleted() ex - " + ex.Message);
                }
            }
        }
    }
}
