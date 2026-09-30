using Focus_Bear.Preferences;
using Focus_Bear.Services;
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
        private FocusBearMainWindow _parentWindow;
        private int nTimer = 30;
        private DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        AppSettings appSettings = AppSettings.GetInstance;
        private string sButtonTitle;

        private readonly bool _showPepTalk;

        public HabitExtraPersonal()
        {
        }
        public HabitExtraPersonal(FocusBearMainWindow parentWindow) : this()
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

            // Once per routine per day (Mac #2617): this screen comes round after every habit, and each fresh
            // pep talk is a /user/motivational-summary request against the rate limit.
            string routine = appSettings.nCurrentActivityType.ToString();
            _showPepTalk = appSettings.bMotivationalMessageEnabled
                && FocusBear.Core.Stats.PepTalkRoutineDisplay.Shared.ShouldShow(routine, DateTime.Now);
            if (_showPepTalk) FocusBear.Core.Stats.PepTalkRoutineDisplay.Shared.Record(routine, DateTime.Now);
 
           //Get the First activity name and duration to set on the Button
           if(_showPepTalk)
           {
                // The Mac's routine start opens its Stats screen on the Pep Talk tab, which streams
                // /user/motivational-summary natively; this used to be the dashboard's motivational-summary
                // webview. A fresh screen per routine start, so each start is one fresh stream.
                pepTalkHost.Content = new StatsScreens.StatsScreen(FocusBear.Core.Stats.StatsTab.PepTalk);

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
                    //The button starts the routine's first habit, so its wording follows that habit - not the
                    //one at nHabitIndex, which may be stale (or -1 after the Focus Summary).
                    sButtonTitle = StartButtonTitle(appSettings.arrAllMorningHabits,
                        appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString(),
                        appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString(),
                        appSettings.SecondsToText);
                    if (sButtonTitle != null)
                    {
                        btnStartACtivityTXT.Text = sButtonTitle;
                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    appSettings.CapturePostHog("visualize-evening-routine");

                    //Simple take the first element
                    //The button starts the routine's first habit, so its wording follows that habit - not the
                    //one at nHabitIndex, which may be stale (or -1 after the Focus Summary).
                    sButtonTitle = StartButtonTitle(appSettings.arrAllEveningHabits,
                        appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString(),
                        appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString(),
                        appSettings.SecondsToText);
                    if (sButtonTitle != null)
                    {
                        btnStartACtivityTXT.Text = sButtonTitle;
                    }

                }

                if (_showPepTalk)
                {
                    Random rnd = new Random();
                    string inspiration = PickInspiration(appSettings.ConfigFileStrings["inspirations"] as JArray, rnd.Next);
                    if (inspiration != null)
                        btnStartACtivityTXT.Text = inspiration;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitExtraPersonal - " + ex.Message);
            }

            //Start timer only when we are not showing the webview
            if (!_showPepTalk)
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

        /// <summary>
        /// A random inspiration, or null when there are none. <paramref name="nextBelow"/>(n) returns an index in
        /// [0, n) - Random.Next's upper bound is exclusive, so asking for n - 1 never picked the last message.
        /// </summary>
        internal static string PickInspiration(JArray messages, Func<int, int> nextBelow)
        {
            if (messages == null || messages.Count == 0)
                return null;
            return messages[nextBelow(messages.Count)].ToString();
        }

        /// <summary>The start button's text for <paramref name="routine"/>'s first habit; null when there is none.</summary>
        internal static string StartButtonTitle(IList<habit> routine, string timerTemplate, string nonTimerTemplate, Func<int, string> secondsToText)
        {
            if (routine == null || routine.Count == 0)
                return null;
            habit first = routine[0];
            if (first.useTimer)
                return timerTemplate.Replace("<NAME>", first.name).Replace("<TIME>", secondsToText(first.durationSeconds));
            return nonTimerTemplate.Replace("<NAME>", first.name);
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

            if (_showPepTalk)
                pepTalkHost.Content = null;

            appSettings.nHabitIndex = 0;
            LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex);

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                appSettings.CapturePostHog("start-morning-routine");
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                appSettings.CapturePostHog("start-evening-routine");
            }
            
            _parentWindow.PageHabits.ShowActivityStartScreen();
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
                        if (_showPepTalk)
                            pepTalkHost.Visibility = Visibility.Collapsed;
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                        if (_showPepTalk)
                            pepTalkHost.Visibility = Visibility.Visible;

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
                    if (appSettings.bPasswordRequireInSkipEvening || (appSettings.DidCutOffTimePassed() && appSettings.bPasswordRequireToSkipEveningAfterCutOff))
                    {
                        if (_showPepTalk)
                            pepTalkHost.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                       _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;


                        if (_showPepTalk)
                            pepTalkHost.Visibility = Visibility.Visible;


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
            LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex);

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                appSettings.CapturePostHog("start-morning-routine");
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                appSettings.CapturePostHog("start-evening-routine");
            }
            _parentWindow.PageHabits.ShowWhySkipHabitsWindow();
        }

    }
}
