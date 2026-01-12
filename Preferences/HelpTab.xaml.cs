using Focus_Bear.API;
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

        }
        private async void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            _ParentWindow.IsEnabled = false;
            bool xResponseUnused = await UploadLogs.UploadLogsToServer(TxtFeedback.Text);
            _ParentWindow.IsEnabled = true;
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
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Mouse.OverrideCursor = null;
                LogWriter.LogWrite("get support CoreWebView2NavigationCompleted() Success, Load Access token");
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                string Settings = string.Format("{{\"access_token\":\"{0}\",\"font\":\"{1}\",lang:\"{2}\",\"theme\":{3}}}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont), appSettings.sLangCode, sTheme);
                string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataFoGetSupport) {{ loadMetaDataFoGetSupport({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);
                var ss = webViewGetSupport.ExecuteScriptAsync(settingsFunc);

                
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
        private void CoreWebView2NavigationCompletedTutorials(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Mouse.OverrideCursor = null;
                LogWriter.LogWrite("tutorials CoreWebView2NavigationCompleted() Success, Load Access token");
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                string Settings = string.Format("{{\"access_token\":\"{0}\",\"font\":\"{1}\",lang:\"{2}\",\"theme\":{3}}}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont), appSettings.sLangCode, sTheme);
                string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataForEnrolledCourses) {{ loadMetaDataForEnrolledCourses({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);
                var ss = webViewTutorials.ExecuteScriptAsync(settingsFunc);


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

                    webViewTutorials.Source = new System.Uri("https://dashboard.focusbear.io/webview/enrolled-courses?bust_cache=" + DateTime.UtcNow.ToString());

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HelpTab CoreWebView2InitializationCompletedTutorials() ex - " + ex.Message);
                }
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
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            bool? bResult = dialog.ShowDialog();
            if (bResult.Value == true)
            {
                // Array of selected image file paths
                string[] imagesArray = dialog.FileNames;
 
                foreach (var file in dialog.FileNames)
                {
                    var df = new DroppedFile(file);
                    appSettings.droppedFiles.Add(df);
                }
                RefreshScreenshots();
            }
            
        }
    }
}
