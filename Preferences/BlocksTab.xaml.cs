using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
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
    /// Interaction logic for BlocksTab.xaml
    /// </summary>
    public partial class BlocksTab : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        PreferencesWindow _ParentWindow = null;
        public BlocksTab()
        {
        }
        public BlocksTab(PreferencesWindow ParentWindow)
        {
            _ParentWindow = ParentWindow;

            InitializeComponent();

            try
            {

                BlockingSchedule.Header = appSettings.ConfigFileStrings["settingsNew"]["blockingScheduleSettingTitle"].ToString();
                DistractingSites.Header = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();

                Welcome.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["subTitle"].ToString();
                RadioButton1.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnFirstText"].ToString();
                RadioButton2.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnSecondText"].ToString();
                RadioButton3.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnThirdText"].ToString();

                Description1.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionFirstText"].ToString();
                Description2.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionSecondText"].ToString();
                Description3.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionThirdText"].ToString();
                App.ThemeChanged += LoadURL;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlocksTab text setting ex - " + ex.Message);
            }

            if (appSettings.nBlockingSchedule == BLOCKINGSCHEDULE.UNTIL_I_FINISH_WORK)
            {
                RadioButton1.IsChecked = true;
                appSettings.CapturePostHog("choose-focus-schedule", "\"schedule-type\":\"until_i_finish_work\"");
            }
            else if (appSettings.nBlockingSchedule == BLOCKINGSCHEDULE.WHEN_STARTING_FOCUS_SESSION)
            {
                RadioButton2.IsChecked = true;
                appSettings.CapturePostHog("choose-focus-schedule", "\"schedule-type\":\"when_i_tell_you_to\"");
            }

            DistractingSites.Content = new AlwaysBlockedURLs(_ParentWindow);

            SetWebView();

            webViewBlockingSchedule.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webViewBlockingSchedule.NavigationCompleted += CoreWebView2NavigationCompleted;
        }

        private async Task SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webViewBlockingSchedule != null)
                {
                    await webViewBlockingSchedule.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("blocking schedule SetWebView() Ex - " + ex.Message);
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");

                string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"font\":\"{1}\",\"theme\":{2}}}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont), sTheme);

                string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadSettingsData) {{ loadSettingsData({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

                var sReturn = webViewBlockingSchedule.ExecuteScriptAsync(LoadTokenFunc);
            }
        }
        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    webViewBlockingSchedule.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webViewBlockingSchedule.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webViewBlockingSchedule.CoreWebView2.Settings.IsScriptEnabled = true;
                    webViewBlockingSchedule.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webViewBlockingSchedule.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webViewBlockingSchedule.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webViewBlockingSchedule.WebMessageReceived += webView_WebMessageReceived;

                    LoadURL();

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("webViewBlockingSchedule CoreWebView2InitializationCompleted() EX - " + ex.Message);
                }
            }
        }

        public void LoadURL()
        {
            string sURL = "https://settings.focusbear.io?edit_specific_routine=timing&bust_cache=" + DateTime.UtcNow.ToString();

            webViewBlockingSchedule.Source = new System.Uri(sURL);
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("Blocks Tab - webView_WebMessageReceived() - settings got from SG");
                //Set environment in settings page
                appSettings.sRawUserSettings = e.WebMessageAsJson;

                //We will need to put the settings.
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.PutUserSettings();

                if (xResponse == 1) //valid settings
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");
                    ff.Owner = this._ParentWindow;
                    ff.ShowDialog();
                    appSettings.CapturePostHog("save-edited-habits");

                    RegistryManager.SaveUserSettings();//save to local

                    //Restart the app.
                    try
                    {
                        LogWriter.LogWrite("blocking schedule changes in preferences - Restart app now for settings to take Effect!");

                        appSettings.bRestartAppNeeded = true;

                        Application.Current.Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("Blocks Tab - webView_WebMessageReceived app restart= " + ex.Message);
                    }

                }
                else
                {
                    LogWriter.LogWrite("Blocks Tab - blocking schedule - Settings cannot be validated from API.");

                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsNotValidText"].ToString(), "Focus Bear");
                    ff.Owner = this._ParentWindow;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Blocks Tab - blocking schedule - webView_WebMessageReceived()" + ex.Message);
            }
        }

        private void RadioButton1_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nBlockingSchedule = BLOCKINGSCHEDULE.UNTIL_I_FINISH_WORK;
        }

        private void RadioButton2_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nBlockingSchedule = BLOCKINGSCHEDULE.WHEN_STARTING_FOCUS_SESSION;
        }
    }
}
