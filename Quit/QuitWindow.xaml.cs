using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Quit
{
    /// <summary>
    /// Interaction logic for QuitWindow.xaml
    /// </summary>
    public partial class QuitWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 5; //10 seconds
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bEmergencyTimer = false;

        public QuitWindow()
        {
            InitializeComponent();

            this.Topmost = true;
            /* "quitInfo":{
            "quittingTitle":"Quitting..",
            "quittingCountDownInfo":{
                "subTitle":"Are you sure you want to Quit? Please take 10 seconds to reflect on whether you are serious about quitting.",
                "message":"While you wait, reflect on why you’re doing this. If it was because your morning routine was too ambitious, perhaps scale it back to only be a few minutes long so that it’s easier to do it than not do it. If you’re deactivating because the app is broken, really sorry!!",
                "pleaseEmailText":"Please email",
                "buttonQuitCancelText":"I didn’t mean it. Let me do my habits!"
            },
            "quittingConfirmInfo":{
                "subTitle":"Can you share why you're quitting?",
                "buttonQuitText":"Quit",
                "emailTitle":"Your Email (if you want us to reply):",
                "dropDown_0":{
                    "title":"Select a reason"
                },
                "dropDown_1":{
                    "title":"I've got an emergency",
                    "description":"Uh oh! Hope everything is ok. Restoring access immediately and quitting app."
                },
                "dropDown_2":{
                    "title":"The app is broken",
                    "description":"Oh no! We're really sorry. Can you please let us know what happened?﹡",
                    "finalDescription":"Thanks for letting us know. We'll fix it ASAP."
                },
                "dropDown_3":{
                    "title":"Something else",
                    "description":"Can you tell us why you want to stop getting habit + break notifications?﹡",
                    "finalDescription":"Thanks for letting us know. All the best."
                }
            },
            "askQuitVcInfo":{
                "subTitle":"Do you want to quit the app completely or just take a break for a few hours?",
                "buttonDiableAppText":"Give me a break for 4hrs",
                "buttonQuitText":"Turn it off completely - I'll turn it back on when I'm ready"
            }*/

            Heading.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingTitle"].ToString();
            DoYouWantToQuitCompletely.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["subTitle"].ToString();
            btnGiveMeABreakTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonDiableAppText"].ToString();
            btnTurnOfCompletelyTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonQuitText"].ToString();


            AreYouSureToQuit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["subTitle"].ToString();
            WhileYouWait.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["message"].ToString();
            PleaseEmail.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["pleaseEmailText"].ToString() + " " + appSettings.ConfigFileStrings["common"]["emailId"].ToString();
            btnLetMeDoMyHabitsTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["buttonQuitCancelText"].ToString();

            ShareWhyQuit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["subTitle"].ToString();

            ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_0"]["title"].ToString());
            ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString());
            ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString());
            ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString());
            ComboBoxReason.SelectedIndex = 0;
            //ReasonTitle
            //TxtExplanation
            btnQuitTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
            btnQuit.IsEnabled = false;
        }

        private void btnGiveMeABreak_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_Click - App should deactivate for 4 hours");

            appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(4);

            RegistryManager.SetAppDeactivatedTillDate();

            appSettings.CapturePostHog("give-me-4hr-break");

            Close();
        }

        private void btnTurnOfCompletely_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnTurnOfCompletely_Click");

            //If a password is set, ask fopr a password
            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInQuitApp)
                { 
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = this;
                    GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    GridShade.Visibility = Visibility.Collapsed;

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
            GridQuitCompletely.Visibility = Visibility.Collapsed;
            GridSureToQuit.Visibility = Visibility.Visible;

            TxtTimer.Text = "00:05";
            //start the timer for 10 seconds
            nTimer = 5;
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {
                if(bEmergencyTimer)
                    TxtFinalTimer.Text = appSettings.SecondsToTimerText(nTimer);
                else
                    TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    if (!bEmergencyTimer)
                    {
                        GridSureToQuit.Visibility = Visibility.Collapsed;
                        GridWhyQuitting.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Application.Current.Shutdown(0);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Quit Window Timer= " + ex.Message);
            }

        }

        private void btnLetMeDoMyHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnLetMeDoMyHabits_Click");
            dispatcherTimer.Stop();
            Close();
        }

        private void ComboBoxReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /* "dropDown_1":{
                "title":"I've got an emergency",
                "description":"Uh oh! Hope everything is ok. Restoring access immediately and quitting app."
            },
            "dropDown_2":{
                "title":"The app is broken",
                "description":"Oh no! We're really sorry. Can you please let us know what happened?﹡",
                "finalDescription":"Thanks for letting us know. We'll fix it ASAP."
            },
            "dropDown_3":{
                "title":"Something else",
                "description":"Can you tell us why you want to stop getting habit + break notifications?﹡",
                "finalDescription":"Thanks for letting us know. All the best."
            }*/
            if (ComboBoxReason.SelectedIndex == 0)
            {
                ReasonTitle.Visibility = Visibility.Collapsed;
                TxtExplanation.Visibility = Visibility.Collapsed;
                TxtFinalTimer.Visibility = Visibility.Collapsed;
            }
            else if (ComboBoxReason.SelectedIndex == 1)
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;
                TxtFinalTimer.Text = "00:02";
                TxtFinalTimer.Visibility = Visibility.Visible;

                string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                dispatcherTimer.Start();
            }
            else if (ComboBoxReason.SelectedIndex == 2)
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
            }
            else if (ComboBoxReason.SelectedIndex == 3)
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
            }
        }

        private void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnQuit_Click");
            btnQuit.IsEnabled = false;

            string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
            appSettings.CapturePostHog("app-quit", sMetadata);


            if ( ComboBoxReason.SelectedIndex == 2 )
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["finalDescription"].ToString();
            }
            else if (ComboBoxReason.SelectedIndex == 3)
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["finalDescription"].ToString();
            }
            ReasonTitle.Visibility = Visibility.Visible;

            ComboBoxReason.IsEnabled = false;
            TxtExplanation.Visibility = Visibility.Collapsed;

            TxtFinalTimer.Text = "00:05";
            TxtFinalTimer.Visibility = Visibility.Visible;
            bEmergencyTimer = true;
            nTimer = 5;
            dispatcherTimer.Start();
        }

        private void TxtExplanation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(TxtExplanation.Text.Length > 0 && ComboBoxReason.SelectedIndex > 1)
            {
                btnQuit.IsEnabled = true;
            }
            else
            {
                btnQuit.IsEnabled = false;
            }
        }
    }
}
