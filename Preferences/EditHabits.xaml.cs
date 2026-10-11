using Focus_Bear.API;
using Focus_Bear.Constants;
using Focus_Bear.Models;
using Focus_Bear.Properties;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for EditHabits.xaml
    /// </summary>
    public partial class EditHabits : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusBearMainWindow _ParentWindow = null;

        private bool bWebViewInitialized = false;
        public EditHabits()
        {
        }
        public EditHabits(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;
            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Path B - reactive token refresh. Reply by reloading through this screen's own
                // load function with the echoed requestId; stop before the settings messages below.
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(e.WebMessageAsJson);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        LoadSettings(req.RequestId);
                    return;
                }

                string eventDataJsonString = e.WebMessageAsJson;
                if(eventDataJsonString == "user-settings-update-failed" || eventDataJsonString == "\"user-settings-update-failed\"")
                {
                    FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["tokenErrorText"].ToString());
                    messageBox.Owner = _ParentWindow;
                    messageBox.ShowDialog();

                    //Logout user and restart the app
                    appSettings.CaptureLogout(Constants.Events.LogoutReason.AuthError);
                    RegistryManager.RemoveLoggedInStatus();

                    //Disable the focus bear time tracker status
                    appSettings.bFocusBearTimeTrackerEnabled = false;
                    RegistryManager.SetFocusBearTimeTrackerStatus();

                    appSettings.bRestartAppNeeded = true;

                    Application.Current.Shutdown(0);

                }
                else  if(eventDataJsonString == "\"user-settings-updated\"" || eventDataJsonString == "user-settings-updated")
                {
                    appSettings.CapturePostHog("save-edited-habits");
                    
                    FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");
                    messageBox.Owner = _ParentWindow;
                    messageBox.ShowDialog();
                   
                    //Restart the app.
                    try
                    {
                        LogWriter.LogWrite("Edit Habits - Restart app now for settings to take Effect!");

                        appSettings.bRestartAppNeeded = true;

                        Application.Current.Shutdown(0);

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Edit Habits - webView_WebMessageReceived app restart= " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Edit habits webView_WebMessageReceived() ex - " + ex.Message);
            }
        }

        public void LoadURL()
        {
            string sSettingsURL = "https://dashboard.focusbear.io/webview/settings?device_id=" + appSettings.sDeviceID; // appSettings.ConfigFileStrings["settings"] ["settingUrl"].ToString();
            sSettingsURL = sSettingsURL + "&bust_cache=" + DateTime.UtcNow.ToString("o");
            webView.Source = new System.Uri(sSettingsURL);
        }

        public async void LoadSettings(string requestId = null)
        {
            try
            {
                LogWriter.LogWrite("LoadSettings()");

                try
                {
                    //Load Settings in the webview
                    if (appSettings.userAccount.accessToken.Length > 0)
                    {
                        LogWriter.LogWrite("attempt to LoadSettings()");

                        //Built as JSON (WebviewPayload), including the requestId the page echoes back (Path B).
                        string settingsFunc = new FocusBear.Core.Ui.WebviewPayload()
                            .Add("access_token", appSettings.userAccount.accessToken)
                            .Add("platform", "win")
                            .Add("font", appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont))
                            .Add("lang", appSettings.sLangCode)
                            .Add("theme", appSettings.sTheme == "Light" ? "LIGHT" : "DARK")
                            .Add("flags", new Newtonsoft.Json.Linq.JArray(appSettings.bIsGeekModeEnabled ? "geek-mode" : "simple-mode", "save-settings-migrate-to-web"))
                            .WithRequestId(requestId)
                            .WaitAndCall("loadSettingsData");
                        var ss1 = webView.ExecuteScriptAsync(settingsFunc);
                        LogWriter.LogWrite("attempt to LoadSettings() done res:" + ss1.ToString());
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("attempt to LoadSettings()  EX: " + ex);
                }

                if (appSettings.arrFocusModes.Count > 0)
                {
                    // Built as JSON: a name spliced into script text broke the call, or ran as script.
                    string arrAllFocusModesArray = FocusModeMetadata.FocusNamesScriptArgument(appSettings.arrFocusModes.ToList());

                    LogWriter.LogWrite("Attempt to Load arrAllFocusModesArray ");

                    try
                    {
                        string settingsFunc = string.Format("LoadFocusNames({0});", arrAllFocusModesArray);
                        var sScriptResponse = webView.ExecuteScriptAsync(settingsFunc);
                        LogWriter.LogWrite("Load arrAllFocusModesArray Done res:" + sScriptResponse.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("attempt to arrAllFocusModesArray()  EX: " + ex);
                    }


                }

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Loading settings in webview()" + ex.Message);
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
                    bWebViewInitialized = true;
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
                LoadSettings();
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

                    App.ThemeChanged += LoadURL;

                    LoadURL();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
             
        }
        private async void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible && bWebViewInitialized)
            {
                if (!appSettings.bProgrammaticSelectioninLeftTree) //Only when user clicks
                { 
                    appSettings.bProgrammaticSelectioninLeftTree = false;
                    string GetCheckUserSettingsModifiedDirectReturn = @"
                                        (function() {
                                            try {
                                                const data = window?.hasUserModifiedSettings?.();
                                                return data?.isUserSettingsModified ?? false;
                                            } catch (e) {
                                                return false;
                                            }
                                        })();
                                        ";
                    string jsResult = await webView.ExecuteScriptAsync(GetCheckUserSettingsModifiedDirectReturn);

                    bool hasModified = System.Text.Json.JsonSerializer.Deserialize<bool>(jsResult);

                    if (hasModified)
                    {
                        FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingsEditedButNotSavedText"].ToString(), "Focus Bear", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["settings"]["buttonReviewSettingsText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                        messageBox.Owner = _ParentWindow;
                        ((App)(Application.Current)).wndFocusBearMainWindow.GridShade.Visibility = Visibility.Visible;
                        messageBox.ShowDialog();
                        ((App)(Application.Current)).wndFocusBearMainWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (messageBox.btnClicked == MessageBoxResult.Yes)
                        {
                            ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.EditHabits);
                            return;
                        }
                    }
                }
            }
        }

        public void RefreshOnModeChange()
        {
            try
            {
                if (bWebViewInitialized) //not first time when creating, only when webview is initialized
                {
                    var sTemp = webView.ExecuteScriptAsync("refreshSettingPage();");
                    LoadSettings();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshOnModeChange() ex - " + ex.Message);
            }
        }
    
        private void UserControl_IsHitTestVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

        }
    }
}
