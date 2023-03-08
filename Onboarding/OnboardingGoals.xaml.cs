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

            /* "askAppWorkVcInfo": {
            "title": "Welcome to Focus Bear",
            "questionText": "What are your goals?",
            "checkBoxFirstText": "Help me develop healthy habits in the morning (e.g. meditate before starting work) and evening (e.g. have dinner with loved ones)",
            "checkBoxSecondText": "Help me stay focused during the work day",
            "buttonContinueText": "Continue"
        },*/
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                WhatGoals.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["questionText"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                TXTCheckBoxHealthyHabits.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxFirstText"].ToString();
                TXTCheckBoxFocused.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxSecondText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingGoals() ex - " + ex.Message);
            }
            HealthyHabits.IsChecked = true;
            StayFocused.IsChecked = true;

        }

        private void HealthyHabits_Checked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void StayFocused_Checked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }


        private void HealthyHabits_Unchecked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void StayFocused_Unchecked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value || StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }


        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /*
                 * if checkBoxFirst.state == .on && checkBoxSecond.state == .on{
                allApiObject.capturePosthog(event: "activate-all-features", metadata: [:])
                }else if checkBoxFirst.state == .on{
                    allApiObject.capturePosthog(event: "activate-habits-only", metadata: [:])
                }else if checkBoxSecond.state == .on{
                    allApiObject.capturePosthog(event: "activate-productivity-only", metadata: [:])
                }
                 */
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

                _parentWindow.ShowOnboardingAskWorkTimeScreen();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnContinue_Click ex - " + ex.Message);
            }
        }
    }
}
