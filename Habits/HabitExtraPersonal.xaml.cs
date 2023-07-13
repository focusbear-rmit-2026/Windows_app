using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitExtraPersonal.xaml
    /// </summary>
    public partial class HabitExtraPersonal : Page
    {
        private HabitWindow _parentWindow;
        private int nTimer = 30;
        private DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        AppSettings appSettings = AppSettings.GetInstance;
        private string sButtonTitle;

        public HabitExtraPersonal()
        {
        }
        public HabitExtraPersonal(HabitWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;
 
           //Get the First activity name and duration to set on the Button
           if(appSettings.bMotivationalMessageEnabled)
           {
                SetWebView();

                webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
                webView.NavigationCompleted += CoreWebView2NavigationCompleted;

                ConatainerWebView.Visibility = Visibility.Visible;
                CrossHair.Visibility = Visibility.Collapsed;

            }

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["titleGetFocused"].ToString();
                Description.Text = appSettings.ConfigFileStrings["activityInfo"]["visualizeDescriptionText"].ToString();
                btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSkipHabitText"].ToString();
   
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    appSettings.CapturePostHog("visualize-morning-routine");

                    //Simple take the first element
                    if (appSettings.arrAllMorningHabits.Count > 0)
                    {
                        if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[0].name);
                            sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllMorningHabits[0].durationSeconds));

                        }
                        else
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[0].name);
                        }


                        btnStartACtivityTXT.Text = sButtonTitle;
                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    appSettings.CapturePostHog("visualize-evening-routine");

                    //Simple take the first element
                    if (appSettings.arrAllEveningHabits.Count > 0)
                    {
                        if (appSettings.arrAllEveningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[0].name);
                            sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllEveningHabits[0].durationSeconds));

                        }
                        else
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[0].name);
                        }
                        btnStartACtivityTXT.Text = sButtonTitle;
                    }

                }

                if (appSettings.bMotivationalMessageEnabled)
                {
                    JArray arrMessages = (JArray)appSettings.ConfigFileStrings["inspirations"];
                    int n = arrMessages.Count;
                    Random rnd = new Random();
                    int nRandom = rnd.Next(0, n - 1);
                    btnStartACtivityTXT.Text = arrMessages[nRandom].ToString();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitExtraPersonal - " + ex.Message);
            }

            //Start timer only when we are not showing the webview
            if (!appSettings.bMotivationalMessageEnabled)
            {
                dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                LowerButtons.Visibility = Visibility.Visible;
                TxtTimer.Visibility = Visibility.Collapsed;
                Description.Visibility = Visibility.Collapsed;
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 1;
#else
            int val = nTimer--;
#endif

            string msg = "00:" + val.ToString("D2");
            TxtTimer.Text = msg;

            CommandManager.InvalidateRequerySuggested();

            if (nTimer < 0)
            {
                TxtTimer.Text = "00:00";
                try
                {
                    dispatcherTimer.Stop();
                    LowerButtons.Visibility = Visibility.Visible; //Show the lower buttons, start activity and skip activity
                    TxtTimer.Visibility = Visibility.Collapsed;

                    Sounds.PlayHabitFinishedSound();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("extra personal dispatcherTimer_Tick - " + ex.Message);
                }
            }
        }

        private void btnStartACtivity_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("extra personal btnStartACtivity_Clicked " );

            if (appSettings.bMotivationalMessageEnabled)
                webView.Dispose();

            appSettings.nHabitIndex = 0;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                appSettings.CapturePostHog("start-morning-routine");
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                appSettings.CapturePostHog("start-evening-routine");
            }
            
            _parentWindow.ShowActivityStartScreen();
        }

        private void btnSkipThisHabit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("extra personal btnSkipThisHabit_Clicked ");

            if (appSettings.isPasswordSet)
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipMorning)
                    {
                        if (appSettings.bMotivationalMessageEnabled)
                            webView.Visibility = Visibility.Collapsed;
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                        if (appSettings.bMotivationalMessageEnabled)
                            webView.Visibility = Visibility.Visible;

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
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening)
                    {
                        if (appSettings.bMotivationalMessageEnabled)
                            webView.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                       _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;


                        if (appSettings.bMotivationalMessageEnabled)
                            webView.Visibility = Visibility.Visible;


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

            }

            appSettings.nHabitIndex = -1; //Starting with minus 1 as we will increment this on why skip nags screen
            
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                appSettings.CapturePostHog("start-morning-routine");
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                appSettings.CapturePostHog("start-evening-routine");
            }
            _parentWindow.ShowWhySkipHabitsWindow();
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
                LogWriter.LogWrite("HabitExtraPersonal SetWebView() EX - " + ex.Message);
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
 
                    string sSettingsURL = "http://dashboard.focusbear.io/webview/motivational-summary";

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
                string Settings = string.Format("{{\"access_token\":\"{0}\"}}", appSettings.userAccount.accessToken);
                string settingsFunc = string.Format("loadAccessTokenForInspirationPage({0});", Settings);
                var sReturn = webView.ExecuteScriptAsync(settingsFunc);
            }
        }
    }
}
