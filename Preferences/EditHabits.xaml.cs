using Focus_Bear.API;
using Focus_Bear.Constants;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for EditHabits.xaml
    /// </summary>
    public partial class EditHabits : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        PreferencesWindow _ParentWindow = null;

        public EditHabits()
        {
        }
        public EditHabits(PreferencesWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;
            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;
            App.ThemeChanged += LoadURL;
        }


        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string eventDataJsonString = e.WebMessageAsJson;
                WebMessageEvent webMessageEvent = JsonConvert.DeserializeObject<WebMessageEvent>(eventDataJsonString);

                if (webMessageEvent?.Event == Events.WindowClosing)
                {
                    if (webMessageEvent.HasUserModifiedSettings)
                    {

                        /* "youAreAboutCloseTheEditHabitsMenu":"You are about to close the Edit habits menu. If you have made any changes, please tap the \"Save Habits\" button.",
                        "warning":"Warning",
                        "continue":"Continue",
                        "cancel":"Cancel"*/ 
                        //todo win texts
                        FBMessageBox messageBox = new FBMessageBox("You are about to close the Edit habits menu. If you have made any changes, please tap the \"Save Habits\" button.", "Warning", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["common"]["buttonContinueText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                        messageBox.Owner = _ParentWindow;
                        messageBox.ShowDialog();

                        if (messageBox.btnClicked == MessageBoxResult.Yes)
                        {
                            _ParentWindow.Hide();
                        }
                        else
                        {
                            messageBox.Close(); //WRONG implementation with ShowDialog()
                        }
                    }
                    else
                    {
                        _ParentWindow.Hide();
                    }
                }
                else
                {
                  await  saveHabits(eventDataJsonString);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("webView_WebMessageReceived()" + ex.Message);
            }
        }

        public void LoadURL()
        {
            string sSettingsURL = appSettings.ConfigFileStrings["settings"] ["settingUrl"].ToString();
            sSettingsURL = sSettingsURL + "?bust_cache=" + DateTime.UtcNow.ToString("o");
            webView.Source = new System.Uri(sSettingsURL);
        }

        public async void LoadSettings()
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

                        string sFlags = string.Format("[\"{0}\"]", appSettings.bIsGeekModeEnabled ? "geek-mode" : "simple-mode");

                        string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");

                        string Settings = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"font\":\"{1}\",\"lang\":\"{2}\",\"flags\":{3},\"theme\":{4} }}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont), appSettings.sLangCode, sFlags, sTheme);

                        string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadSettingsData) {{ loadSettingsData({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);
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
                    string arrAllFocusModesArray = $"{{\"focusNames\":[";

                    foreach (FocusMode FF in appSettings.arrFocusModes)
                    {
                        string addFocusJsonString = $"{{\"id\":\"{FF.id}\"," +
                            $"\"name\":\"{FF.name}\"}},";
                        arrAllFocusModesArray = arrAllFocusModesArray + addFocusJsonString;
                    }

                    //Remove last comma -  we have 100% added above
                    arrAllFocusModesArray = arrAllFocusModesArray.Remove(arrAllFocusModesArray.Length - 1, 1);
                    arrAllFocusModesArray = arrAllFocusModesArray + "]}";

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
                Mouse.OverrideCursor = null;
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
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

        }
        public void RefreshOnModeChange()
        {
            try
            {
                var sTemp = webView.ExecuteScriptAsync("refreshSettingPage();");
                LoadSettings();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshOnModeChange() ex - " + ex.Message);
            }
        }
    
        private async Task saveHabits(string eventDataJsonString)
        {
            LogWriter.LogWrite("webView_WebMessageReceived() - settings got from SG");
            //Set environment in settings page
            appSettings.sRawUserSettings = eventDataJsonString;

            //We will need to put the settings.
            FocusBearAPIClient client = new FocusBearAPIClient();
            var xResponse = await client.PutUserSettings();

            if (xResponse == 1) //valid settings
            {
                FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");

                messageBox.Owner = _ParentWindow;
                messageBox.ShowDialog();
                appSettings.CapturePostHog("save-edited-habits");

                RegistryManager.SaveUserSettings();//save to local

                //Restart the app.
                try
                {
                    LogWriter.LogWrite("Edit Habits - Restart app now for settings to take Effect!");

                    appSettings.bRestartAppNeeded = true;

                    Application.Current.Shutdown(0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("webView_WebMessageReceived app restart= " + ex.Message);
                }

            }
            else
            {
                // TODO: The value of xRenwTokenStatus should not be zero. If it is, prompt the user to sign in again or initiate a token refresh.
                LogWriter.LogWrite("Settings cannot be validated from API.");
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsNotValidText"].ToString(), "Focus Bear");
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
            }
        }
    }
}
