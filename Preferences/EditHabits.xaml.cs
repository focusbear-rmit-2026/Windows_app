using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
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
        public EditHabits()
        {
            InitializeComponent();

            SetWebView();

            //    "settings":{
            //        "title":"Enter settings JSON below:",
            //"buttonSaveText":"Save",
            //"visitText":"Visit <SETTINGURL> to generate settings",
            //"settingUrl":"https://settings.focusbear.io",

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;

            //LoadURL();

        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("webView_WebMessageReceived() - settings got from SG" );
                //Set environment in settings page
                appSettings.sRawUserSettings = e.WebMessageAsJson;
                
                //string ss = e.WebMessageAsJson.ToString();
                //We will need to put the settings.
                FocusBearAPIClient client = new FocusBearAPIClient();
                var x = await client.PutUserSettings();
                
                if(x == 1) //valid settings
                {
                    /*
                     *  "settings":{
                     *          "settingIsValidText":"Your settings have been validated and will be applied now."
                     *                  "settingIsNotValidText":"Sorry it seems like your settings aren’t valid. Please generate settings at https://settings.focusbear.io",
                     *                  
                     */
                   /// MessageBox.Show(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear", MessageBoxButton.OK);
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");
                    ff.ShowDialog();
                    appSettings.CapturePostHog("save-edited-habits");

                    RegistryManager.SaveUserSettings();//save to local

                    //Restart the app.
                    try
                    {
                        LogWriter.LogWrite("Restart app now for settings to take Effect!");

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
                    LogWriter.LogWrite("Settings cannot be validated from API.");
                    ///MessageBox.Show(appSettings.ConfigFileStrings["settings"]["settingIsNotValidText"].ToString(), "Focus Bear", MessageBoxButton.OK);
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsNotValidText"].ToString(), "Focus Bear");
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("webView_WebMessageReceived()" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        public void LoadURL()
        {
            string sSettingsURL = appSettings.ConfigFileStrings["settings"]["settingUrl"].ToString();
            sSettingsURL = sSettingsURL + "?bust_cache=" + DateTime.UtcNow.ToString();
            //webView.Source = new System.Uri("http://jaininfotech.com/temp/test.html");
            webView.Source = new System.Uri(sSettingsURL);

        }
 
        public async void LoadSettings()
        {
            try
            {

                //Set environment in settings page
                string functiontocall = string.Format("focusbearEnv('win');");
                string ss = await webView.ExecuteScriptAsync(functiontocall);
                //Now await will keep waiting till the eecution return OK, no need to check it as we will still call load settings.

                //Load Settings in the webview
                if (appSettings.sRawUserSettings.Length > 0)
                {
                    string Settings = string.Format("{{ \"metadata\":{{\"access_token\":\"{0}\"}},\"settings\":{1}}}", appSettings.userAccount.accessToken, appSettings.sRawUserSettings);
                    string settingsFunc = string.Format("loadSettingsData({0});", Settings);
                    string ss1 = await webView.ExecuteScriptAsync(settingsFunc);
                }

                if(appSettings.arrFocusModes.Count > 0)
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

                    string settingsFunc = string.Format("LoadFocusNames({0});", arrAllFocusModesArray);
                    await webView.ExecuteScriptAsync(settingsFunc);
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

               
                    //this.webView.NavigationStarting += NavigationStarting;
                    //this.webView.NavigationCompleted += NavigationCompleted;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.WebMessageReceived += webView_WebMessageReceived;

                    LoadURL();

                    //this.webView.CoreWebView2.WebMessageReceived += webView_WebMessageReceived;
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


    }
}
