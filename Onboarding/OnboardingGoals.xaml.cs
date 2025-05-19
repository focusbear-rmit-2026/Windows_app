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
using System.Windows.Interop;
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
        App app = (App)Application.Current;
        public OnboardingGoals()
        {
        }

        public OnboardingGoals(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = "Focus Bear";//appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                //todo win texts
                WhatGoals.Text = "We'll do our best to help you achieve your goals 🤩 Which Focus Bear features do you want to activate?"; //appSettings.ConfigFileStrings["askAppWorkVcInfo"]["questionText"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                TXTCheckBoxHealthyHabits.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxFirstText"].ToString();
                TXTCheckBoxFocused.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["checkBoxSecondText"].ToString();
                

                InfoHealthyHabits.ToolTip = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["infoBuildHabitsText"].ToString();
                InfoStayFocused.ToolTip = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["infoStayFocusedText"].ToString();

                SetGoals.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleActivateFeatures"].ToString(); //"Set your Goals and Preferences";  
                CareAboutHabits.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleHowMuchCare"].ToString(); // "How much do you care about building habits?";
                SelectOne.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["selectOneText"].ToString(); //"Select one that apply";
                DoNotCare.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonDontCareText"].ToString(); //"Meh - don't really care";
                NotCommitted.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonNotCommittedText"].ToString(); //"I probably should do habits but am not that committed";
                Committed.Content = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonCommittedText"].ToString(); //"Super committed -this is the main reason I want to use Focus Bear";

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingGoals() ex - " + ex.Message);
            }
            HealthyHabits.IsChecked = false;
            StayFocused.IsChecked = false;
            btnContinue.IsEnabled = false;
            CareDesc.Text = "";
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private void HealthyHabits_Checked(object sender, RoutedEventArgs e)
        {
            HabitsCare.Visibility = Visibility.Visible;
            
            //One of the three must be selected to proceed.
            if(DoNotCare.IsChecked.Value || NotCommitted.IsChecked.Value || Committed.IsChecked.Value)
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
            if (HealthyHabits.IsChecked.Value )
            {
                if (DoNotCare.IsChecked.Value || NotCommitted.IsChecked.Value || Committed.IsChecked.Value)
                {
                    btnContinue.IsEnabled = true;
                }
                else
                {
                    btnContinue.IsEnabled = false;
                }
            }
            else
            {
                btnContinue.IsEnabled = true;
            }
        }


        private void HealthyHabits_Unchecked(object sender, RoutedEventArgs e)
        {
            HabitsCare.Visibility = Visibility.Collapsed;

            if (StayFocused.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }

            //refresh selection
            DoNotCare.IsChecked = false;
            NotCommitted.IsChecked = false;
            Committed.IsChecked = false;
            CareDesc.Text = "";
        }

        private void StayFocused_Unchecked(object sender, RoutedEventArgs e)
        {
            if (HealthyHabits.IsChecked.Value)
            {
                if (DoNotCare.IsChecked.Value || NotCommitted.IsChecked.Value || Committed.IsChecked.Value)
                {
                    btnContinue.IsEnabled = true;
                }
                else
                {
                    btnContinue.IsEnabled = false;
                }
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }


        public void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Initialize with false
                appSettings.bGoalHealthyHabits = false;
                appSettings.bGoalStayFocused = false;

                if (HealthyHabits.IsChecked.Value && StayFocused.IsChecked.Value)
                {
                    if (DoNotCare.IsChecked.Value) //still no habits and just focus
                    {
                        appSettings.bGoalStayFocused = true;
                        appSettings.bGoalStayFocused = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"do_not_care\"");
                        appSettings.sCareFactorString = "do_not_care";
                    }
                    else if (NotCommitted.IsChecked.Value) //still no habits and just focus
                    {
                        appSettings.bGoalStayFocused = true;
                        appSettings.bGoalStayFocused = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"not_committed\"");
                        appSettings.sCareFactorString = "not_committed";
                    }
                    else if (Committed.IsChecked.Value) // ALL habits
                    {
                        appSettings.bGoalHealthyHabits = true;
                        appSettings.bGoalStayFocused = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"committed\"");
                        appSettings.CapturePostHog("activate-all-features");
                        appSettings.sCareFactorString = "committed";
                    }
                }
                else if (HealthyHabits.IsChecked.Value)
                {
                    if (DoNotCare.IsChecked.Value) //still no habits and just focus
                    {
                        appSettings.bGoalStayFocused = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"do_not_care\"");
                        appSettings.sCareFactorString = "do_not_care";
                    }
                    else if (NotCommitted.IsChecked.Value) //still no habits and just focus
                    {
                        appSettings.bGoalStayFocused = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"not_committed\"");
                        appSettings.sCareFactorString = "not_committed";
                    }
                    else if (Committed.IsChecked.Value) // ALL habits
                    {
                        appSettings.bGoalHealthyHabits = true;
                        appSettings.CapturePostHog("cares-about-habits", "\"care-factor\":\"committed\"");
                        appSettings.CapturePostHog("activate-habits-only");
                        appSettings.sCareFactorString = "committed";
                    }

                }
                else if (StayFocused.IsChecked.Value)
                {
                    appSettings.bGoalStayFocused = true;
                    appSettings.CapturePostHog("activate-productivity-only");
                }

                
                //If the user hadclicked EXIT full screen we will take up the default values in case he had unchecked both
                if (appSettings.bExitFullScreenClickedWhileOnboarding)
                {   //taking the default values
                    appSettings.bGoalStayFocused = true; //Jeremy mentioned that we shold default to FOcus Only
                }

                //will get habit packs only if the user is doing habits
                if (appSettings.bGoalHealthyHabits)
                {
                    /**
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    //Here first we need to call the API to get the packs list
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var xResponse = await client.GetHabitPacks();
                    Mouse.OverrideCursor = null;
                    **/
                    //Proceed normally to the next screen to select habit packs.
                    _parentWindow.ShowOnboardingSelectHabitPacksScreen();
                    return;
                }
                if (appSettings.bGoalStayFocused)
                {
                    _parentWindow.ShowOnboardingSetHabitsLaterInfo();
                }


            
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnContinue_Click ex - " + ex.Message);
            }
        }
        
        private void DoNotCare_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = "Ok no worries. We'll let you focus on focusing!"; //todo win texts
            btnContinue.IsEnabled = true;
        }
        private void NotCommitted_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = "Got it. We won't activate the habit features for now but you can activate them later."; //todo win texts
            btnContinue.IsEnabled = true;
        }

        private void Committed_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = "Cool. We'll let you set up your habits right away"; //todo win texts
            btnContinue.IsEnabled = true;
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
