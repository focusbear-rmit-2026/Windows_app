using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingGoals.xaml
    /// </summary>
    public partial class OnboardingGoals : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingGoals()
        {
        }

        public OnboardingGoals(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                WhatGoals.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["questionText"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                TXTCheckBoxHealthyHabits.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxFirstText"].ToString();
                TXTCheckBoxFocused.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxSecondText"].ToString();
                AnythingElse.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["anythingElseText"].ToString();

                InfoHealthyHabits.ToolTip = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["infoBuildHabitsText"].ToString();
                InfoStayFocused.ToolTip = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["infoStayFocusedText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingGoals() ex - " + ex.Message);
            }
            HealthyHabits.IsChecked = false;
            StayFocused.IsChecked = false;
            btnContinue.IsEnabled = false;

            AnythingElse.Visibility = Visibility.Collapsed;
            AnythingElseVALUE.Visibility = Visibility.Collapsed;
        }

        private void HealthyHabits_Checked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                AnythingElse.Visibility = Visibility.Visible;
                AnythingElseVALUE.Visibility = Visibility.Visible;
            }
            else
            {
                btnContinue.IsEnabled = false;

                AnythingElse.Visibility = Visibility.Collapsed;
                AnythingElseVALUE.Visibility = Visibility.Collapsed;
            }
        }

        private void StayFocused_Checked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                AnythingElse.Visibility = Visibility.Visible;
                AnythingElseVALUE.Visibility = Visibility.Visible;
            }
            else
            {
                btnContinue.IsEnabled = false;

                AnythingElse.Visibility = Visibility.Collapsed;
                AnythingElseVALUE.Visibility = Visibility.Collapsed;
            }
        }


        private void HealthyHabits_Unchecked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                AnythingElse.Visibility = Visibility.Visible;
                AnythingElseVALUE.Visibility = Visibility.Visible;
            }
            else
            {
                btnContinue.IsEnabled = false;

                AnythingElse.Visibility = Visibility.Collapsed;
                AnythingElseVALUE.Visibility = Visibility.Collapsed;
            }
        }

        private void StayFocused_Unchecked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                AnythingElse.Visibility = Visibility.Visible;
                AnythingElseVALUE.Visibility = Visibility.Visible;
            }
            else
            {
                btnContinue.IsEnabled = false;

                AnythingElse.Visibility = Visibility.Collapsed;
                AnythingElseVALUE.Visibility = Visibility.Collapsed;
            }
        }


        public async void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                
                if (HealthyHabits.IsChecked.Value && StayFocused.IsChecked.Value)
                {
                    appSettings.bGoalHealthyHabits = true;
                    appSettings.bGoalStayFocused = true;
                    appSettings.CapturePostHog("activate-all-features");
                }
                else if (HealthyHabits.IsChecked.Value)
                {
                    appSettings.bGoalHealthyHabits = true;
                    appSettings.CapturePostHog("activate-habits-only");
                }
                else if (StayFocused.IsChecked.Value)
                {
                    appSettings.bGoalStayFocused = true;
                    appSettings.CapturePostHog("activate-productivity-only");
                }

                if (AnythingElseVALUE.Text.Length > 0)
                {
                    string sData = string.Format("\"feedback\":\"{0}\"", AnythingElseVALUE.Text);
                    appSettings.CapturePostHog("anything-else-goals-feedback", sData);
                }

                //If the user hadclicked EXIT full screen we will take up the default values in case he had unchecked both
                if (appSettings.bExitFullScreenClickedWhileOnboarding)
                {   //taking the default values
                    appSettings.bGoalHealthyHabits = true;
                    appSettings.bGoalStayFocused = true;
                }

                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                //Here firat we need to call the API to get the packs list
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetHabitPacks();

               
                Mouse.OverrideCursor = null;
        
                //Proceed normally to the next screen
                _parentWindow.ShowOnboardingSelectHabitPacksScreen();
            
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnContinue_Click ex - " + ex.Message);
            }
        }

        private void AnythingElseVALUE_TextChanged(object sender, TextChangedEventArgs e)
        {
        }
    }
}
