using Focus_Bear.API;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for StopFocusMode.xaml
    /// </summary>
    public partial class StopFocusMode : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        DispatcherTimer dispatcherTimerDeactivate = new DispatcherTimer(DispatcherPriority.Normal);
        int nTimer = 0;

        public StopFocusMode()
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

            try
            {
                this.Topmost = true;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                string sHeading = appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["title"].ToString();
                
                Heading.Text = sHeading.Replace("<FOCUSTYPE>", appSettings.sRunningFocusModeName); 
                WhyDeactivating.Text = appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["questionText"].ToString();

                //Force user to actively make a choice from dropdown : 26 June 2023
                //Adding an empty item on 0 index
                ComboBoxReason.Items.Add("");

                //We still need random items in the combox box everytime
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);
                if(nRandom == 1)
                {
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsFirstText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsSecondText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsThirdText"].ToString());
                }
                else if(nRandom == 2)
                {
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsThirdText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsFirstText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsSecondText"].ToString());
                }
                else
                {
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsSecondText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsThirdText"].ToString());
                    ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsFirstText"].ToString());
                }

                ComboBoxReason.SelectedIndex = 0;

                btnCancelText.Text = appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["buttonCancelText"].ToString();
                btnStopFocusText.Text = appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["buttonAbortFocusText"].ToString();

                btnStopFocus.IsEnabled = false;
                //Removed the 5 second timer from build v 108
                Timer.Text = "";

                LogWriter.LogWrite("Deactivate Focus Mode Window shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StopFocusMode()" + ex.Message);
            }

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnStopFocus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
<<<<<<< Updated upstream
                if(!appSettings.bIsGeekModeEnabled)//Simple mode
=======
                await ((App)(Application.Current)).SyncTodoTasksFromPlayer(); // sync todo tasks

                if (!appSettings.bIsGeekModeEnabled)//Simple mode
>>>>>>> Stashed changes
                {
                    appSettings.bAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro; //resetting the AI value
                }
                //If super pomodoro was running and user manually stopped the focus mode, we will reset the variable
                appSettings.bUseSuperPomodoroMode = false;

                string sOption = ComboBoxReason.SelectedValue.ToString();
<<<<<<< Updated upstream
                if (sOption == appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsFirstText"].ToString())
=======
                if (sOption == appSettings.ConfigFileStrings["focusInfo"]["abortingFocusModeInfo"]["reasonsFirstText"].ToString()) //"reasonsFirstText": "I achieved what I wanted to.",
>>>>>>> Stashed changes
                {
                    //Show the Focusmode end dialog
                    ((App)(Application.Current)).ShowFocusEndWindow();
                }
                else
                {
<<<<<<< Updated upstream
                    if(appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 0)
=======
                    if(appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 2) //Now the empty array [] is retuned
>>>>>>> Stashed changes
                    {
                        //Show the Focusmode end dialog
                        ((App)(Application.Current)).ShowFocusEndWindow();
                    }
                    else
                    {
                        LogWriter.LogWrite("Focuse mode Aborted()");

                        DateTime now = DateTime.Now;
                        TimeSpan difference = now.Subtract(DateTime.Parse(appSettings.sRunningFocusModeStartTime)); // could also write `now - otherTime`
                        var durationSeconds = difference.TotalSeconds;

                        int nSeconds = Convert.ToInt32(durationSeconds);
                        appSettings.AddEntryInCSV("focus mode abort", "focus block - " + appSettings.sRunningFocusModeName, nSeconds.ToString(), "", "", appSettings.sRunningFocusModeStartTime, "", "", sOption, "");

                        ((App)(Application.Current)).HideToDoPlayer();

                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                        FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                        appSettings.CapturePostHog("completed-focus-session", string.Format("\"focus-type\":\"manual_focus\",\"focusDurationMinutes\":{0}", (nSeconds / 60)));
                        
                        //Call api top finish focus mode
                        FocusBearAPIClient client = new FocusBearAPIClient();
                        var xResponse = client.FinishFocusMode(appSettings.sRunningFocusModeID, "", "");
                    }
                }

                DialogResult = true;
                Close();
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStopFocus_Click()" + ex.Message);
            }
        }

        private void dispatcherTimer_Deactivate(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Deactivate()");
            try
            {
                nTimer--;
                string msg = "00:" + nTimer.ToString("D2");
                Timer.Text = msg;

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    dispatcherTimerDeactivate.Stop(); //Tick Once only

                    Timer.Text = "";

                    if (ComboBoxReason.SelectedIndex != 0)
                        btnStopFocus.IsEnabled = true;

                    LogWriter.LogWrite("Enable the the Deactivate Focus Mode button");
                }

                CommandManager.InvalidateRequerySuggested();
              
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Deactivate" + ex.Message);
            }
        }

        private void ComboBoxReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxReason.SelectedIndex != 0 )
                btnStopFocus.IsEnabled = true;
            else
                btnStopFocus.IsEnabled = false;
        }
    }
}
