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

            LoadURL();

        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("webView_WebMessageReceived() - settings got from SG" );
                //Set environment in settings page
                appSettings.sRawUserSettings = e.WebMessageAsJson;
                
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
                    MessageBox.Show(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear", MessageBoxButton.OK);

                    appSettings.CapturePostHog("save-edited-habits");

                    RegistryManager.SaveUserSettings();//save to local

                    //Restart the app.
                    try
                    {
                        LogWriter.LogWrite("Restart app now for settings to take Effect!");

                        string currentAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        // _MainWin.ShouldShutDown = true;
                        Application.Current.Shutdown(0);
                        Process.Start(currentAppPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("webView_WebMessageReceived app restart= " + ex.Message);
                    }

                }
                else
                {
                    LogWriter.LogWrite("Settings cannot be validated from API.");
                    MessageBox.Show(appSettings.ConfigFileStrings["settings"]["settingIsNotValidText"].ToString(), "Focus Bear", MessageBoxButton.OK);
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
 
        public void LoadSettings()
        {
            try
            {

                //Set environment in settings page
                string functiontocall = string.Format("focusbearEnv('win');");
                webView.ExecuteScriptAsync(functiontocall);

                //Load Settings in the webview
                if (appSettings.sRawUserSettings.Length > 0)
                {
                    string settingsFunc = string.Format("loadSettingsData({0});", appSettings.sRawUserSettings);
                    webView.ExecuteScriptAsync(settingsFunc);
                }
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Loading settings in webview()" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private async void SetWebView()
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
                LogWriter.LogWrite("SetWebView()" + ex.Message);
                Debug.WriteLine(ex.Message);
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

                webView.WebMessageReceived += webView_WebMessageReceived;

                    //this.webView.CoreWebView2.WebMessageReceived += webView_WebMessageReceived;
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                    Debug.WriteLine(ex.Message);
                }
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }


    }
}
