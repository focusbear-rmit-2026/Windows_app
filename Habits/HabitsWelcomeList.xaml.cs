using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitsWelcomeList.xaml
    /// </summary>
    public partial class HabitsWelcomeList : Page
    {
        public class HabitList : INotifyPropertyChanged
        {
            public string SNumber { get; set; }
            public string ActivityName { get; set; }

            public HabitList()
            {
                //// NavItems = new ObservableCollection<StartupAppModel>();
            }
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private ObservableCollection<HabitList> _ObjListActivityModel = null;
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitWindow _parentWindow;
        public HabitsWelcomeList()
        {
        }
        public HabitsWelcomeList(HabitWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;
            /*
            "activityInfo":{
                "startMorningHabitTitleText":"Good Morning! Here are the habits to kick off an awesome day.",
                 "buttonBeginText": "Begin",
                "startEveningHabitTitleText":"Good Evening! Let’s go through your wind down routine.",

            */
            if(appSettings.bHasUserEditedSettings)
            {
                btnCustomizeHabits.IsEnabled = false;
                btnCustomizeHabits.Visibility = Visibility.Hidden;
            }
            else
            {
            
                SetWebView();

                webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
                webView.NavigationCompleted += CoreWebView2NavigationCompleted;


            }


            int nCount = 0;
            try
            { 
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["startMorningHabitTitleText"].ToString();
                    nCount = appSettings.arrAllMorningHabits.Count;

                    string imagePath = ("pack://application:,,,/Focus Bear;component/images/gifs/Wakingup.gif");
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(imagePath);
                    image.EndInit();
                    ImageBehavior.SetAnimatedSource(TopIcon, image);

                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["startEveningHabitTitleText"].ToString();
                    nCount = appSettings.arrAllEveningHabits.Count;
                }

               
                _ObjListActivityModel = new ObservableCollection<HabitList>();
                ListActivities.ItemsSource = _ObjListActivityModel;

                for(int i = 0; i < nCount; i++)
                {
                    string sActivityName = "";
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        sActivityName = appSettings.arrAllMorningHabits[i].name;
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sActivityName = appSettings.arrAllEveningHabits[i].name;
                    }

                    string sNo = string.Format("{0}.", (i+1).ToString());
                    var ActivityItem = new HabitList { SNumber = sNo, ActivityName = sActivityName };
                    _ObjListActivityModel.Add(ActivityItem);
                }

                btnBeginTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonBeginText"].ToString();
                btnCustomizeHabitsTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonEditHabitsText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsWelcomeList initialization" + ex.Message);
            }
            Sounds.PlayRoutineStartSound();
        }

        private void btnBegin_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Welcome Begin Clicked");
            try
            {
                _parentWindow.ShowExtraPersonalFocus();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WElcome btnBegin_Click" + ex.Message);
            }
        }

        private void btnCustomizeHabits_Click(object sender, RoutedEventArgs e)
        {
            WebGrid.Visibility = Visibility.Visible;
            Normal.Visibility = Visibility.Collapsed;
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
                LogWriter.LogWrite("HabitsWelcomeList SetWebView() EX - " + ex.Message);
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

                    string sSettingsURL = appSettings.ConfigFileStrings["settings"]["settingUrl"].ToString();

                    string routine = null;
                    if(appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        routine = "morning";
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        routine = "evening";
                    }

                    sSettingsURL = sSettingsURL + "?bust_cache=" + DateTime.UtcNow.ToString() + "&edit_specific_routine=" + routine;
                    webView.Source = new System.Uri(sSettingsURL);

                    CommandManager.InvalidateRequerySuggested();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Habit WelcomeList CoreWebView2InitializationCompleted() EX=" + ex.Message);
                }
            }
        }

        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
               LoadSettings();
            }
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

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible)
            {
                WebGrid.Visibility = Visibility.Collapsed;
                Normal.Visibility = Visibility.Visible;
            }
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("webView_WebMessageReceived() - settings got from SG");
                //Set environment in settings page
                appSettings.sRawUserSettings = e.WebMessageAsJson;

                //string ss = e.WebMessageAsJson.ToString();
                //We will need to put the settings.
                FocusBearAPIClient client = new FocusBearAPIClient();
                var x = await client.PutUserSettings();

                if (x == 1) //valid settings
                {
                    /*
                     *  "settings":{
                     *          "settingIsValidText":"Your settings have been validated and will be applied now."
                     *                  "settingIsNotValidText":"Sorry it seems like your settings aren’t valid. Please generate settings at https://settings.focusbear.io",
                     *                  
                     */
                    /// MessageBox.Show(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear", MessageBoxButton.OK);
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");
                    ff.Owner = this._parentWindow;
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
                    ff.Owner = this._parentWindow;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("webView_WebMessageReceived()" + ex.Message);
            }
        }
    }
}
