using Focus_Bear.Preferences;
using Focus_Bear.Utils;
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
            /*
             "activityInfo":{
             "visualizeDescriptionText": "Before you begin your routine, take 30 seconds to engage in \"extrapersonal focus\": try your best to keep your eyes fixed on the target. Research from Balcetis and colleagues in 2020 indicates that concentrating on a visual point will sharpen your focus and increase the likelihood of completing your routine.",
            "titleGetFocused": "Get Focused"
                "startActivityButtonText":"Start <TIME> <NAME>",
                        "buttonSkipHabitText":"Skip this habit for today",
             */
            Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["titleGetFocused"].ToString();
            Description.Text = appSettings.ConfigFileStrings["activityInfo"]["visualizeDescriptionText"].ToString();
            btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSkipHabitText"].ToString();
            //Get the First activity name and duration to set on the Button
            try
            {
               
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    appSettings.CapturePostHog("visualize-morning-routine");

                    //Simple take the first element
                    if (appSettings.arrAllMorningHabits.Count > 0)
                    {
                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();

                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[0].name);
                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllMorningHabits[0].durationSeconds));
                        btnStartACtivityTXT.Text = sButtonTitle;
                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    appSettings.CapturePostHog("visualize-evening-routine");

                    //Simple take the first element
                    if (appSettings.arrAllEveningHabits.Count > 0)
                    {
                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();

                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[0].name);
                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllEveningHabits[0].durationSeconds));
                        btnStartACtivityTXT.Text = sButtonTitle;
                    }

                }


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitExtraPersonal - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }

           
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
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
                    //Debug.WriteLine(ex.Message);
                }
            }
        }

        private void btnStartACtivity_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("extra personal btnStartACtivity_Clicked " );
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
                            //Hide();
                            return;
                        }

                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening)
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
                            //Hide();
                            return;
                        }
                    }
                }

            }

            appSettings.nHabitIndex = -1; //Starting with minus 1 as we will inclement this on why skip nags screen
            
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
    }
}
