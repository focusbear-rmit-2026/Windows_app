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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingAdditionalGoals.xaml
    /// </summary>
    public partial class OnboardingAdditionalGoals : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public OnboardingAdditionalGoals()
        {
        }
          
        public OnboardingAdditionalGoals(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();

                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
                
                CheckMeditateConsistently.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxMeditateConsistentlyText"].ToString(); //"Meditate consistently";
                CheckSleepBetter.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxSleepBetterText"].ToString(); //"Sleep better";
                CheckExerciseMore.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxExerciseMoreText"].ToString(); //"Exercise more";
                CheckImproveWorkLifeBalance.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxImproveBalanceText"].ToString(); //"Improve work life balance";

                AnythingElse.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["anythingElseText"].ToString();

                SetGoals.Text = "Set your Goals and Preferences"; //todo win text
                GladDesc.Text = "Glad you're as passionate about building routines as we are!";
                WhatGoals.Text = "Are there other goals you want to achieve with Focus Bear?";
                SelectAll.Text = "Select all that apply";


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAdditionalGoals() ex - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.sAdditionalGoals = "";

            if (CheckMeditateConsistently.IsChecked.Value)
            {
                appSettings.sAdditionalGoals = appSettings.sAdditionalGoals + "\"" + CheckMeditateConsistently.Content + "\"" + ",";
            }
            if (CheckSleepBetter.IsChecked.Value)
            {
                appSettings.sAdditionalGoals = appSettings.sAdditionalGoals + "\"" + CheckSleepBetter.Content + "\"" + ",";
            }
            if (CheckExerciseMore.IsChecked.Value)
            {
                appSettings.sAdditionalGoals = appSettings.sAdditionalGoals + "\"" + CheckExerciseMore.Content + "\"" + ",";
            }
            if (CheckImproveWorkLifeBalance.IsChecked.Value)
            {
                appSettings.sAdditionalGoals = appSettings.sAdditionalGoals + "\"" + CheckImproveWorkLifeBalance.Content + "\"" + ",";
            }

            appSettings.sAdditionalGoals = appSettings.sAdditionalGoals.Trim();
            if(appSettings.sAdditionalGoals.Length > 0)
            {
                //Remove last comma
                appSettings.sAdditionalGoals = appSettings.sAdditionalGoals.Remove(appSettings.sAdditionalGoals.Length - 1, 1);
            }
                
            appSettings.sAdditionalGoals = "[" + appSettings.sAdditionalGoals + "]";

            if (AnythingElseVALUE.Text.Length > 0)
            {
                string sData = string.Format("\"feedback\":\"{0}\"", AnythingElseVALUE.Text);
                appSettings.CapturePostHog("anything-else-goals-feedback", sData);
            }

            //Proceed normally to the next screen
            _parentWindow.ShowOnboardingSelectHabitPacksScreen();

        }
        private void AnythingElseVALUE_TextChanged(object sender, TextChangedEventArgs e)
        {
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this, BackgroundImage);
        }
    }
}
