using Focus_Bear.API;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for HelpTab.xaml
    /// </summary>
    /// 
   
    public partial class HelpTab : System.Windows.Controls.UserControl
    {
        FocusBearMainWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public HelpTab()
        {
        }
        public HelpTab(FocusBearMainWindow pp)
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

            SetWebViewGetSupport();
            webViewGetSupport.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webViewGetSupport.NavigationCompleted += CoreWebView2NavigationCompleted;


            SetWebViewTutorials();
            webViewTutorials.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompletedTutorials;
            webViewTutorials.NavigationCompleted += CoreWebView2NavigationCompletedTutorials;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["helpVcInfo"]["shareLogsText"].ToString();
                btnExportLogsTXT.Text = appSettings.ConfigFileStrings["helpVcInfo"]["buttonShareLogsText"].ToString();
                WhatHappened.Text = appSettings.ConfigFileStrings["helpVcInfo"]["placeHolderLogsFeedback"].ToString();

                ReportProblemTab.Header = appSettings.ConfigFileStrings["preferencesInfo"]["tabReportProblemText"].ToString();
                
                Community.Header = appSettings.ConfigFileStrings["helpVcInfo"]["tabCommunity"].ToString();


                CommunityHeading.Text = appSettings.ConfigFileStrings["helpVcInfo"]["titleCommunity"].ToString();
                btnJoinDiscordTXT.Text = appSettings.ConfigFileStrings["helpVcInfo"]["buttonJoinDiscordText"].ToString();
                BtnDontUseDiscordTxt.Text = appSettings.ConfigFileStrings["helpVcInfo"]["buttonDontUseDiscordText"].ToString();

                GetSupport.Header = appSettings.ConfigFileStrings["helpVcInfo"]["tabGetSupport"].ToString(); 
                Tutorials.Header = appSettings.ConfigFileStrings["helpVcInfo"]["tabCourses"].ToString();

                DragDesc.Text = appSettings.ConfigFileStrings["helpVcInfo"]["titleAddScreenshots"].ToString(); //"Drag and drop screenshots here that you want to share"; 
                btnSelectScreenshotsTXT.Text =  appSettings.ConfigFileStrings["helpVcInfo"]["buttonSelectScreenshots"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab() texts ex=" + ex.Message);
            }

            // The Report Problem tab is one form now (Mac ReportProblemForm): note, screenshots, one Send,
            // and for the tester account the support / local-Claude choice plus the local reports list.
            ReportProblemTab.Content = new Reports.ReportProblemForm(async note =>
            {
                if (_ParentWindow != null) _ParentWindow.IsEnabled = false;
                try { return await UploadLogs.UploadLogsToServer(note); }
                finally { if (_ParentWindow != null) _ParentWindow.IsEnabled = true; }
            });
        }

        private Reports.LocalReportsView _localReports;
        private TextBlock _localReportStatus;

        /// <summary>
        /// For the tester account only (as on the Mac): a second way to send a report — into
        /// %USERPROFILE%\FocusBear-Claude-Tasks for the local Claude review loop — and the list of reports filed that way.
        /// </summary>
        private void SetUpLocalReports()
        {
            try
            {
                if (_localReports != null) return;
                if (!FocusBear.Core.Reports.LocalReportCapability.IsEnabled(appSettings.userAccount?.userID)) return;
                var grid = (ReportProblemTab.Content as ScrollViewer)?.Content as Grid;
                if (grid == null) return;
                var strings = new Reports.LocalReportStrings();
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 10) };
                var create = Reports.LocalReportsView.SmallButton(strings["sendToLocal"], CreateLocalReport_Click, primary: true);
                create.Margin = new Thickness(0);
                System.Windows.Automation.AutomationProperties.SetAutomationId(create, "btnCreateLocalReport");
                row.Children.Add(create);
                _localReportStatus = Reports.LocalReportsView.Text("", 12, secondary: true);
                _localReportStatus.VerticalAlignment = VerticalAlignment.Center;
                _localReportStatus.Margin = new Thickness(12, 0, 0, 0);
                System.Windows.Automation.AutomationProperties.SetAutomationId(_localReportStatus, "txtLocalReportStatus");
                row.Children.Add(_localReportStatus);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(row, grid.RowDefinitions.Count - 1);
                grid.Children.Add(row);
                _localReports = new Reports.LocalReportsView(() => AppSettings.GetInstance.userAccount?.userID);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(_localReports, grid.RowDefinitions.Count - 1);
                grid.Children.Add(_localReports);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab SetUpLocalReports ex - " + ex.Message);
            }
        }

        /// <summary>The same note and screenshots as a support upload, written as a local report folder instead.</summary>
        private void CreateLocalReport_Click(object sender, RoutedEventArgs e)
        {
            var strings = new Reports.LocalReportStrings();
            var screenshots = appSettings.droppedFiles.Select(file => file.FilePath).ToList();
            LogWriter.Flush();
            string folder = FocusBear.Core.Reports.LocalReportTaskFile.Write(FocusBear.Core.Reports.LocalReportFolders.DefaultRoot, TxtFeedback.Text, screenshots,
                AppSettings.AppDataDirLogsPath, appSettings.AppVersion?.ToString() ?? "", appSettings.userAccount?.userID, DateTime.Now);
            if (folder == null)
            {
                _localReportStatus.Text = strings["localFailed"];
                return;
            }
            LogWriter.LogWrite("HelpTab - local report written to " + folder);
            _localReportStatus.Text = strings["localSaved"];
            TxtFeedback.Text = string.Empty;
            appSettings.droppedFiles.Clear();
            RefreshScreenshots();
            _localReports?.Refresh();
        }
        private async void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            _ParentWindow.IsEnabled = false;
            bool uploadSuccess = await UploadLogs.UploadLogsToServer(TxtFeedback.Text);
            _ParentWindow.IsEnabled = true;

            // Only clear screenshots and message on successful upload.
            // On failure, retain them so the user can retry without re-adding everything.
            if (uploadSuccess)
            {
                TxtFeedback.Text = string.Empty;
                appSettings.droppedFiles.Clear();
                RefreshScreenshots();
            }
        }

        
        private async Task SetWebViewGetSupport()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webViewGetSupport != null)
                {
                    await webViewGetSupport.EnsureCoreWebView2Async(task);
                }

                //webViewGetSupport.CoreWebView2.OpenDevToolsWindow();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab habits SetWebView get support () Ex - " + ex.Message);
            }
        }
        // Path A initial load + Path B reactive reply. Pass the echoed requestId on a reply
        // (omit on initial load); it rides along with the other flags in the same payload.
        public void LoadAccessTokenGetSupport(string requestId = null)
        {
            //Built as JSON (WebviewPayload), including the requestId the page echoes back.
            string settingsFunc = new FocusBear.Core.Ui.WebviewPayload()
                .Add("access_token", appSettings.userAccount.accessToken)
                .Add("font", appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont))
                .Add("lang", appSettings.sLangCode)
                .Add("theme", appSettings.sTheme == "Light" ? "LIGHT" : "DARK")
                .WithRequestId(requestId)
                .WaitAndCall("loadMetaDataFoGetSupport");
            var ss = webViewGetSupport.ExecuteScriptAsync(settingsFunc);
        }

        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Mouse.OverrideCursor = null;
                LogWriter.LogWrite("get support CoreWebView2NavigationCompleted() Success, Load Access token");
                LoadAccessTokenGetSupport();

                
            }
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                    LogWriter.LogWrite("Chat CoreWebView2InitializationCompleted() Success, Load URL");

                    webViewGetSupport.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webViewGetSupport.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webViewGetSupport.CoreWebView2.Settings.IsScriptEnabled = true;
                    webViewGetSupport.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webViewGetSupport.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webViewGetSupport.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webViewGetSupport.WebMessageReceived += webViewGetSupport_WebMessageReceived;
                    Utils.WebViewTabOut.Attach(webViewGetSupport);   // Tab / Shift+Tab can leave the chat widget

                    webViewGetSupport.Source = new System.Uri("https://dashboard.focusbear.io/webview/get-support?bust_cache=" + DateTime.UtcNow.ToString());

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HelpTab CoreWebView2InitializationCompleted() ex - " + ex.Message);
                }
            }
        }


        private async Task SetWebViewTutorials()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webViewTutorials != null)
                {
                    await webViewTutorials.EnsureCoreWebView2Async(task);
                }


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab webViewTutorials SetWebView get support () Ex - " + ex.Message);
            }
        }
        // Path A initial load + Path B reactive reply. Pass the echoed requestId on a reply
        // (omit on initial load); it rides along with the other flags in the same payload.
        public void LoadAccessTokenTutorials(string requestId = null)
        {
            //Built as JSON (WebviewPayload), including the requestId the page echoes back.
            string settingsFunc = new FocusBear.Core.Ui.WebviewPayload()
                .Add("access_token", appSettings.userAccount.accessToken)
                .Add("font", appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont))
                .Add("lang", appSettings.sLangCode)
                .Add("theme", appSettings.sTheme == "Light" ? "LIGHT" : "DARK")
                .WithRequestId(requestId)
                .WaitAndCall("loadMetaDataForEnrolledCourses");
            var ss = webViewTutorials.ExecuteScriptAsync(settingsFunc);
        }

        private void CoreWebView2NavigationCompletedTutorials(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Mouse.OverrideCursor = null;
                LogWriter.LogWrite("tutorials CoreWebView2NavigationCompleted() Success, Load Access token");
                LoadAccessTokenTutorials();


            }
        }
        private void CoreWebView2InitializationCompletedTutorials(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                    LogWriter.LogWrite("Chat CoreWebView2InitializationCompletedTutorials() Success, Load URL");

                    webViewTutorials.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webViewTutorials.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webViewTutorials.CoreWebView2.Settings.IsScriptEnabled = true;
                    webViewTutorials.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webViewTutorials.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webViewTutorials.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webViewTutorials.WebMessageReceived += webViewTutorials_WebMessageReceived;
                    Utils.WebViewTabOut.Attach(webViewTutorials);

                    webViewTutorials.Source = new System.Uri("https://dashboard.focusbear.io/webview/enrolled-courses?bust_cache=" + DateTime.UtcNow.ToString());

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HelpTab CoreWebView2InitializationCompletedTutorials() ex - " + ex.Message);
                }
            }
        }

        private async void webViewGetSupport_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Path B - reactive token refresh. This webview has no other native messages.
            try
            {
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(e.WebMessageAsJson);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        LoadAccessTokenGetSupport(req.RequestId);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab webViewGetSupport_WebMessageReceived() ex - " + ex.Message);
            }
        }

        private async void webViewTutorials_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Path B - reactive token refresh. This webview has no other native messages.
            try
            {
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(e.WebMessageAsJson);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        LoadAccessTokenTutorials(req.RequestId);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab webViewTutorials_WebMessageReceived() ex - " + ex.Message);
            }
        }

        private void BtnDontUseDiscord_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-i-dont-use-discord-clicked");
            //ask for preferred community
            _ParentWindow.RepositionWindow();
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            CommunityPreference wndCommunityPref = new CommunityPreference();
            wndCommunityPref.Owner = _ParentWindow;
            wndCommunityPref.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wndCommunityPref.Topmost = true;
            bool? DialogResult = wndCommunityPref.ShowDialog();

            _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

        }

        private void btnJoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sDiscordUrl = "https://discord.gg/VZdv3ZjhSq";//default
                sDiscordUrl = appSettings.ConfigFileStrings["discordUrl"].ToString();
                
                System.Diagnostics.Process.Start(sDiscordUrl);

                appSettings.CapturePostHog("button-join-discord-server-clicked");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnJoinDiscord_Click + " + ex.Message);
            }
        }

        private void Border_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void RefreshScreenshots()
        {
            ScreenshotsList.ItemsSource = null;
            ScreenshotsList.ItemsSource = appSettings.droppedFiles;

            PlaceholderPanel.Visibility = appSettings.droppedFiles.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        var df = new DroppedFile(file);
                        appSettings.droppedFiles.Add(df);
                    }
                }
                RefreshScreenshots();
            }
        }

        private void RemoveScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DroppedFile df)
            {
                appSettings.droppedFiles.Remove(df);
                RefreshScreenshots();
            }
        }

        private void btnSelectScreenshots_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = appSettings.ConfigFileStrings["helpVcInfo"]["buttonSelectScreenshots"].ToString(),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*",
                Multiselect = true
            };

            bool? bResult = dialog.ShowDialog();
            if (bResult == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    // Avoid adding the same file twice
                    if (!appSettings.droppedFiles.Any(f => f.FilePath == file))
                    {
                        var df = new DroppedFile(file);
                        appSettings.droppedFiles.Add(df);
                    }
                }
                RefreshScreenshots();
            }
        }
    }
}
