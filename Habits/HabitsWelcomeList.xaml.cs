using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

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
        private DispatcherTimer popupTimer;
        private ObservableCollection<HabitList> _ObjListActivityModel = null;
        AppSettings appSettings = AppSettings.GetInstance;
        private FocusBearMainWindow _parentWindow;
        public HabitsWelcomeList()
        {
        }
        public HabitsWelcomeList(FocusBearMainWindow parentWindow) : this()
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


            _parentWindow = parentWindow;

            //if(appSettings.bHasUserEditedSettings)
            //{
            //    btnCustomizeHabits.IsEnabled = false;
            //    btnCustomizeHabits.Visibility = Visibility.Hidden;
            //}
            //else
            {

                SetWebView();

                webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
                webView.NavigationCompleted += CoreWebView2NavigationCompleted;
                App.ThemeChanged += LoadUrl;
            }


            int nCount = 0;
            try
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["startMorningHabitTitleText"].ToString();
                    nCount = appSettings.arrAllMorningHabitsFiltered.Count;

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
                    nCount = appSettings.arrAllEveningHabitsFiltered.Count;
                    if(appSettings.bSomeHabitsRemovedDueToCutOffTime)
                    {
                        PopupTitle.Text = appSettings.ConfigFileStrings["activityInfo"]["habitsRemovedWarningText"].ToString();
                        MyPopup.IsOpen = true;
                        // Create and start a timer to trigger fade-out after 5 seconds.
                        popupTimer = new DispatcherTimer();
                        popupTimer.Interval = TimeSpan.FromSeconds(5);
                        popupTimer.Tick += PopupTimer_Tick;
                        popupTimer.Start();

                    }
                }

                if (nCount < 2)
                {
                    //let's not show the I do not have time for all these habits button
                    DoNoHaveTime.Visibility = Visibility.Collapsed;
                }
               
                _ObjListActivityModel = new ObservableCollection<HabitList>();
                ListActivities.ItemsSource = _ObjListActivityModel;

                UpdateHabitsList();//habits to be shown in the list

                btnBeginTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonBeginText"].ToString();
                btnCustomizeHabitsTXT.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonEditHabitsText"].ToString();
                DoNoHaveTimeTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSelectHabitsText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsWelcomeList initialization ex - " + ex.Message);
            }
            Sounds.PlayRoutineStartSound();
        }
        private void InitializeHabitList()
        {
            appSettings._ObjHabitListSelectable.Clear(); //Remove all old and refill

            int nCount = 0;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                nCount = appSettings.arrAllMorningHabits.Count;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                nCount = appSettings.arrAllEveningHabits.Count;
            }

            for (int i = 0; i < nCount; i++)
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

                string sNo = string.Format("{0}.", (i + 1).ToString());
                var ActivityItem = new HabitListSelectable { SNumber = sNo, ActivityName = sActivityName };
                appSettings._ObjHabitListSelectable.Add(ActivityItem);
            }
        }
        private void UpdateHabitsList()
        {
            try
            {
                LogWriter.LogWrite("Welcome UpdateHabitsList()");
                //The choose habit window have already removed the unselected ones from the habits list to actually be done, so just refill
                _ObjListActivityModel.Clear();

                int nCount = 0;
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    nCount = appSettings.arrAllMorningHabits.Count;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    nCount = appSettings.arrAllEveningHabits.Count;
                }

                for (int i = 0; i < nCount; i++)
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

                    string sNo = string.Format("{0}.", (i + 1).ToString());
                    var ActivityItem = new HabitList { SNumber = sNo, ActivityName = sActivityName };
                    _ObjListActivityModel.Add(ActivityItem);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Welcome UpdateHabitsList() ex - " + ex.Message);
            }
        }
        private void btnBegin_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Welcome Begin Clicked");
            try
            {
                //To allow out of order habits, we will first need to check the Selected habit from the list if any.
                // First, get the selected item from ListActivities
                int selectedIndex = ListActivities.SelectedIndex;

                if (selectedIndex != -1)  // Ensure an item is selected
                {
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        habit itemToMove = appSettings.arrAllMorningHabits[selectedIndex];
                        // Remove the item from arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Insert(0, itemToMove);
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        habit itemToMove = appSettings.arrAllEveningHabits[selectedIndex];
                        // Remove the item from arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Insert(0, itemToMove);
                    }
                }

                _parentWindow.PageHabits.ShowExtraPersonalFocus();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WElcome btnBegin_Click" + ex.Message);
            }
        }

        private void btnCustomizeHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Welcome btnCustomizeHabits_Click");

            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInChangeSettings)
                {
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = _parentWindow;
                    _parentWindow.GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                    if (bResult.Value)
                    {
                        //Password is correct
                    }
                    else
                    {
                        return;
                    }
                }
            }

            //change the button text to back tomy habits
            _parentWindow.PageHabits.btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonBackToMyHabitsText"].ToString();  
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
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    webView.WebMessageReceived += webView_WebMessageReceived;

                    //string sSettingsURL = appSettings.ConfigFileStrings["settings"]["settingUrl"].ToString();

                    //string routine = null;
                    //if(appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    //{
                    //    routine = "morning";
                    //}
                    //else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    //{
                    //    routine = "evening";
                    //}

                    //sSettingsURL = sSettingsURL + "?bust_cache=" + DateTime.UtcNow.ToString("o") + "&edit_specific_routine=" + routine;

                    //webView.Source = new System.Uri(sSettingsURL);
                    LoadUrl();

                    CommandManager.InvalidateRequerySuggested();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Habit WelcomeList CoreWebView2InitializationCompleted() EX=" + ex.Message);
                }
            }
        }
        private void LoadUrl()
        {
            string sSettingsURL = "https://dashboard.focusbear.io/webview/settings?device_id=" + appSettings.sDeviceID; 

            string routine = null;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                routine = "morning";
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                routine = "evening";
            }

            sSettingsURL = sSettingsURL + "&bust_cache=" + DateTime.UtcNow.ToString("o") + "&edit_specific_routine=" + routine;

            webView.Source = new System.Uri(sSettingsURL);
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
                //Load Settings in the webview
                try
                {
                     
                    
                    if (appSettings.userAccount.accessToken.Length > 0)
                    {
                        LogWriter.LogWrite("attempt to LoadSettings()");

                        //New format 03 July 2023
                        string sFlags = string.Format("[\"{0}\", \"save-settings-migrate-to-web\"]", appSettings.bIsGeekModeEnabled ? "geek-mode" : "simple-mode");

                        string sTheme = string.Format("\"{0}\"", appSettings.sTheme == "Light" ? "LIGHT" : "DARK");
                        string Settings = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",\"font\":\"{1}\",\"theme\":{2}, \"flags\":{3}}}", appSettings.userAccount.accessToken, appSettings.GetFontEquivalentForWeb(appSettings.sSelectedAppFont),sTheme, sFlags);

                        string settingsFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadSettingsData) {{ loadSettingsData({0}); clearInterval(waitForAppLoaded); }} }}, 100);", Settings);
                        var ss1 = await webView.ExecuteScriptAsync(settingsFunc);
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

                if (!appSettings.bPlayActivitiesInFullScreenMode)
                {
                    _parentWindow.PageHabits.btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonPostponeText"].ToString();
                }
                else
                {
                    _parentWindow.PageHabits.btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();
                }

            }
        }

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("HabitsWelcomeList - webView_WebMessageReceived() - settings got from SG");
                
                if (e.WebMessageAsJson == "invoke-js-function" || e.WebMessageAsJson == "\"invoke-js-function\"")
                {
                    return;
                }
                try
                {
                    string eventDataJsonString = e.WebMessageAsJson;
                    if (eventDataJsonString == "user-settings-update-failed" || eventDataJsonString == "\"user-settings-update-failed\"")
                    {
                        FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["tokenErrorText"].ToString());
                        messageBox.Owner = _parentWindow;
                        messageBox.ShowDialog();

                        //Logout user and restart the app
                        RegistryManager.RemoveLoggedInStatus();

                        //Disable the focus bear time tracker status
                        appSettings.bFocusBearTimeTrackerEnabled = false;
                        RegistryManager.SetFocusBearTimeTrackerStatus();

                        appSettings.bRestartAppNeeded = true;

                        Application.Current.Shutdown(0);

                    }
                    else if (eventDataJsonString == "\"user-settings-updated\"" || eventDataJsonString == "user-settings-updated")
                    {
                        appSettings.CapturePostHog("save-edited-habits");

                        FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingIsValidText"].ToString(), "Focus Bear");
                        messageBox.Owner = _parentWindow;
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
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsWelcomeList - webView_WebMessageReceived()" + ex.Message);
            }
        }

        private void btnDoNoHaveTime_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageHabits.ShowChooseHabitsWindow();
            UpdateHabitsList();
        }

        private void PopupTimer_Tick(object sender, EventArgs e)
        {
            popupTimer.Stop();
            popupTimer.Tick -= PopupTimer_Tick;

            // Create a fade out animation.
            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(2)
            };

            fadeOut.Completed += (s, a) => { MyPopup.IsOpen = false; };

            // Start the animation on the border containing the popup content.
            PopupBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
