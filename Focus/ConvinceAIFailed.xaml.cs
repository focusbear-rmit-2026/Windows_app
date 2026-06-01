using Focus_Bear.API;
using Focus_Bear.Habits;
using Focus_Bear.Utils;
using Sentry;
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

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for ConvinceAIFailed.xaml
    /// </summary>
    public partial class ConvinceAIFailed : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        DispatcherTimer dispatcherTimerBlock = new DispatcherTimer(DispatcherPriority.Normal);
        public int nTimer = 30;
        public bool bShowConvincePopupAgain = false;
        public bool bShowAskNewGoalPopup = false;
        public bool bReportBadAIDecision = false;
        public bool bShowTemporaryAccessPopup = false;
        public ConvinceAIFailed()
        {
        }
        public ConvinceAIFailed( double nProbability, int nTimerVal = 30)
        {
            InitializeComponent();

            try
            {
                nTimer = nTimerVal;

                Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["stillDistractingTitle"].ToString();

                Reason.Text = appSettings.sChatGPTReasonForCurrentURL;

                string sScore = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["aiRelevanceScoreText"].ToString();
                Score.Text = sScore.Replace("<SCORE>", ((int)(nProbability * 100)).ToString());

                string sAskNeed = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["stillDistractingDescription"].ToString(); //"Focus Bear's AI still thinks <URL> isn't relevant for what you're trying to achieve. Are you sure you need it?",
                if(appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                {
                    sAskNeed = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["distractingForCurrentTaskDescription"].ToString(); //"Focus Bear's AI still thinks <URL> isn't relevant for your current task \"<TASK>\". Are you sure you need it?",
                    sAskNeed = sAskNeed.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                }
                sAskNeed = sAskNeed.Replace("<URL>", appSettings.sConvincingAIForURL);

                AskNeed.Text = sAskNeed;

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                dispatcherTimerBlock.Tick += new EventHandler(dispatcherTimer_Block);
                dispatcherTimerBlock.Interval = new TimeSpan(0, 0, 1);
                dispatcherTimerBlock.Start();

                btnYesText.Text = appSettings.ConfigFileStrings["common"]["yesText"].ToString();
                btnNoText.Text = appSettings.ConfigFileStrings["common"]["noText"].ToString();

                btnConvinceAIText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonConvinceAgain"].ToString();

                TextReportBadAIDecision.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["reportBadAiTooltip"].ToString();

                //"titleGoalAndTask": "Are you still working on \"<TASK>\" as part of your focus goal: \"<GOAL>\"?",
                string sTaskInfo = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoalAndTask"].ToString();
                if(appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                {
                    sTaskInfo = sTaskInfo.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                }
                else
                {
                    sTaskInfo = sTaskInfo.Replace("<TASK>", "");
                }
                if(appSettings.sRunningFocusModeIntention.Length > 0)
                {
                    sTaskInfo = sTaskInfo.Replace("<GOAL>", appSettings.sRunningFocusModeIntention);
                }
                else
                {
                    sTaskInfo = sTaskInfo.Replace("<GOAL>", "");
                }

                HeadingStillWorking.Text = sTaskInfo;
                btnYesThisSiteIsRelevantText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonSiteRelevantText"].ToString();
                btnWorkingOnSomethingElseText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonWorkingSomethingElseText"].ToString();

                //save task for later
                HeadingSave.Text = appSettings.ConfigFileStrings["popUpSaveThisTaskForLaterVcInfo"]["title"].ToString();
                TaskTitle.Text = appSettings.ConfigFileStrings["popUpSaveThisTaskForLaterVcInfo"]["titleTask"].ToString();
                btnYesPleaseSaveTaskForLaterText.Text = appSettings.ConfigFileStrings["popUpSaveThisTaskForLaterVcInfo"]["buttonYesText"].ToString();
                btnNoBlockItText.Text = appSettings.ConfigFileStrings["popUpSaveThisTaskForLaterVcInfo"]["buttonJustBlockItText"].ToString();


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ConvinceAIFailed Texts EX -  " + ex.Message);
            }

        }
        private void dispatcherTimer_Block(object sender, EventArgs e)
        {
            nTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                    DialogResult = false;
                    Close();
                }
                else if (nTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerBlock.Stop();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ConvinceAIFailed window dispatcherTimer_Unlock" + ex.Message);
            }

        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            //show temporary unlock screen
            //We need to show Convince AI again
            bShowTemporaryAccessPopup = true;
            DialogResult = true;
            Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            //Show do you want to save this task for later view
            dispatcherTimerBlock.Stop();
            GridShade.Visibility = Visibility.Visible;
            GridWantToSaveTaskForLater.Visibility = Visibility.Visible;

            string sWork = appSettings.ConfigFileStrings["popUpSaveThisTaskForLaterVcInfo"]["taskNameString"].ToString();
            sWork = sWork.Replace("<URL>", appSettings.sConvincingAIForURL);
            TxtTaskToSave.Text = sWork;
        }

        private void BtnReportBadAIDecision_Click(object sender, RoutedEventArgs e)
        {
            bReportBadAIDecision = true;
            DialogResult = true;
            Close();
        }

        private void btnConvinceAI_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimerBlock.Stop();
            GridShade.Visibility = Visibility.Visible;
            GridLetMeConvinceAgain.Visibility = Visibility.Visible;
        }

        //After user clicks convince AI again
        private void btnWorkingOnSomethingElse_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Collapsed;
            GridLetMeConvinceAgain.Visibility = Visibility.Collapsed;

            //Show add new goal and task popup
            bShowAskNewGoalPopup = true;
            DialogResult = true;
            Close();
        }

        private void btnYesThisSiteIsRelevant_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Collapsed;
            GridLetMeConvinceAgain.Visibility = Visibility.Collapsed;

            //We need to show Convince AI again
            bShowConvincePopupAgain = true;
            DialogResult = true;
            Close();
        }

        private async void btnYesPleaseSaveTaskForLater_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Visible;
            GridWantToSaveTaskForLater.Visibility = Visibility.Visible;

            if (TxtTaskToSave.Text.Length > 0)
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var m = await client.AddTask(TxtTaskToSave.Text);
            }
            
            DialogResult = false; //block the page
            Close();
        }

        private void btnNoBlockIt_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Visible;
            GridWantToSaveTaskForLater.Visibility = Visibility.Visible;
            DialogResult = false; //block the page
            Close();
        }
    }
}
