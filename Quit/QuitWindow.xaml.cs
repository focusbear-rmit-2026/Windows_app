using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

        int nTimer = 2;  
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bEmergencyTimer = false;
        string[] stringarr;
       
        public QuitWindow()
        {
            InitializeComponent();

            this.Topmost = true;
           
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingTitle"].ToString();
                DoYouWantToQuitCompletely.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["subTitle"].ToString();
                
                btnTurnOfCompletelyTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonQuitText"].ToString();

                TurnOffMinsText.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["selectHoursText"].ToString();

                AreYouSureToQuit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["subTitle"].ToString();
                WhileYouWait.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["message"].ToString();
                PleaseEmail.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["pleaseEmailText"].ToString() + " " + appSettings.ConfigFileStrings["common"]["emailId"].ToString();
                btnLetMeDoMyHabitsTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["buttonQuitCancelText"].ToString();

                ShareWhyQuit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["subTitle"].ToString();

                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_0"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString());

                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["title"].ToString());

                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString());

                ComboBoxReason.SelectedIndex = 0;

                btnQuitTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
                btnQuit.IsEnabled = false;

                string sHour = appSettings.ConfigFileStrings["common"]["hourText"].ToString();
                string sHours = appSettings.ConfigFileStrings["common"]["hoursText"].ToString();
                string sMinutes = appSettings.ConfigFileStrings["common"]["minutesText"].ToString(); 

                // Step 2:Array Initialization
                stringarr = new string[8] { string.Format("5 {0}", sMinutes),
                    string.Format("15 {0}", sMinutes),
                     string.Format("30 {0}", sMinutes),
                     string.Format("1 {0}", sHour),
                     string.Format("2 {0}", sHours),
                     string.Format("4 {0}", sHours),
                     string.Format("12 {0}", sHours),
                     string.Format("24 {0}", sHours)
                };

                ComboBoxMinutes.ItemsSource = stringarr;
                ComboBoxMinutes.SelectedIndex = 3;

                //default is one hour
                btnGiveMeABreakTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonBreakOneHourText"].ToString();

                 //If app is deactivated, we diorectly need to go to the next screen, so we will click the next button
                if (appSettings.IsAppDeactivated())
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnTurnOfCompletely);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Quit Window set texts ex = " + ex.Message);
            }
        }

        private void btnGiveMeABreak_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_Click - App should deactivate for selected hours");

            switch (ComboBoxMinutes.SelectedIndex)
            {
                case 0:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddMinutes(5);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 5 minutes");
                    break;
                case 1:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddMinutes(15);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 15 minutes");
                    break;
                case 2:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddMinutes(30);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 30 minutes");
                    break;
                case 3:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(1);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 1 hours");
                    break;
                case 4:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(2);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 2 hours");
                    break;
                case 5:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(4);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 4 hours");
                    break;
                case 6:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(12);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 12 hours");
                    break;
                case 7:
                    appSettings.AppDeactivatedTillDate = DateTime.Now.AddHours(24);
                    LogWriter.LogWrite("QuitWindow - btnGiveMeABreak_ for 24 hours");
                    break;

            }

            LogWriter.LogWrite("Quit - btnGiveMeABreak_Click - app deactivated till: )" + appSettings.AppDeactivatedTillDate);

            RegistryManager.SetAppDeactivatedTillDate();

            appSettings.CapturePostHog("turn-it-off-for-x-duration");

            //We will restart the app here
            LogWriter.LogWrite("Restart app now for DEACTIVATE to take Effect!");

            appSettings.bRestartAppNeeded = true;

            Application.Current.Shutdown(0);

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
                        return;
                    }

                }
            }
            GridQuitCompletely.Visibility = Visibility.Collapsed;
            GridSureToQuit.Visibility = Visibility.Visible;

            TxtTimer.Text = "00:02";
            //start the timer for 10 seconds
            nTimer = 2;
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
            if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_0"]["title"].ToString())
            {
                ReasonTitle.Visibility = Visibility.Collapsed;
                TxtExplanation.Visibility = Visibility.Collapsed;
                TxtFinalTimer.Visibility = Visibility.Collapsed;
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;
                string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                dispatcherTimer.Start();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                dispatcherTimer.Start();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                dispatcherTimer.Start();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["title"].ToString()) //annoying
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                dispatcherTimer.Start();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString()) //Something else
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
            }
        }

        private async void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnQuit_Click");
            btnQuit.IsEnabled = false;

            string sMetadata = string.Format("\"quitReason\":\"{0}\",\"usageStartDate\":\"{1}\",\"feedback\":\"{2}\"", ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT.ToString(), TxtExplanation.Text);
            appSettings.CapturePostHog("app-quit", sMetadata);


            if ( ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString()) //app broken
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["finalDescription"].ToString();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString()) //something else
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["finalDescription"].ToString();
            }
            ReasonTitle.Visibility = Visibility.Visible;

            ComboBoxReason.IsEnabled = false;
            TxtExplanation.Visibility = Visibility.Collapsed;

            if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString()) //app broken, we will need to post the logs if the user wants
            {
                FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["common"]["alertUploadLogText"].ToString() , "", MessageBoxButton.YesNo);  
                messageBox.Owner = this;
                messageBox.ShowDialog();
                if (messageBox.btnClicked == MessageBoxResult.Yes)
                {
                    //we will hide the window here
                    Hide();

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var sResponse = await client.PostAppLogs(TxtExplanation.Text);
                }

                //25-May-2023 - WE will not be showing the timer but easy fix is having 1 second invisible timer.
                TxtFinalTimer.Visibility = Visibility.Collapsed;
                bEmergencyTimer = true;
                nTimer = 1;
                dispatcherTimer.Start();
            }
            else
            {
                //25-May-2023 - WE will not be showing the timer but easy fix is having 1 second invisible timer.
                TxtFinalTimer.Visibility = Visibility.Collapsed;
                bEmergencyTimer = true;
                nTimer = 1;
                dispatcherTimer.Start();
            }
              
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

        private void ComboBoxMinutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //default is one hour
            string ssH = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonBreakFewHourText"].ToString();
            string ssM = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonBreakFewMinutesText"].ToString();

            switch (ComboBoxMinutes.SelectedIndex)
            {
                case 0:
                    btnGiveMeABreakTXT.Text = ssM.Replace("<MINUTE>", "5");
                    break;
                case 1:
                    btnGiveMeABreakTXT.Text = ssM.Replace("<MINUTE>", "15");
                    break;
                case 2:
                    btnGiveMeABreakTXT.Text = ssM.Replace("<MINUTE>", "30");
                    break;
                case 3:
                    btnGiveMeABreakTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonBreakOneHourText"].ToString();
                    break;
                case 4:
                    btnGiveMeABreakTXT.Text = ssH.Replace("<HOUR>", "2");
                    break;
                case 5:
                    btnGiveMeABreakTXT.Text = ssH.Replace("<HOUR>", "4");
                    break;
                case 6:
                    btnGiveMeABreakTXT.Text = ssH.Replace("<HOUR>", "12");
                    break;
                case 7:
                    btnGiveMeABreakTXT.Text = ssH.Replace("<HOUR>", "24");
                    break;

            }
        }
    }
}
