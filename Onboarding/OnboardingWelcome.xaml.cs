using Focus_Bear.Utils;
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

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingWelcome.xaml
    /// </summary>
    public partial class OnboardingWelcome : Page
    {

        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingWelcome()
        {

        }

        public OnboardingWelcome(OnBoardingWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
            InitializeComponent();

            try
            {

                Heading.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageHeading"].ToString();

                /* "welcomeMessageOnlyBreakIntro": "Throughout the day, I'll remind you to take regular breaks to keep your mind sharp and your body flexible. Here's an example of a break activity: 30 seconds of deep breathing."
                */
                Suggest.Text = appSettings.ConfigFileStrings["welcomeInfo"]["takingBreaksText"].ToString();

                Description.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageIntro"].ToString();

                if (!appSettings.bGoalHealthyHabits)
                    Description.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageOnlyBreakIntro"].ToString();

                WhatActivity.Text = appSettings.ConfigFileStrings["welcomeInfo"]["titleSelectActivity"].ToString();
                ActivityName.Text = appSettings.ConfigFileStrings["welcomeInfo"]["titleEnterActivityName"].ToString();

                JArray arrActivities = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"];
                foreach (JObject item in arrActivities)
                {
                    if (item.ContainsKey("name"))
                    {
                        ComboBoxActivityType.Items.Add(item.GetValue("name").ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingWelcome texts ex - " + ex.Message);
            }

            //ComboBoxActivityType.SelectedIndex = 0;

        }
        private void btnStartMindfulBreathing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parentWindow.ShowMindFullBreathing();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStartMindfulBreathing_Click ex - " + ex.Message);
            }
        }

        private void TxtSomethingElse_TextChanged(object sender, TextChangedEventArgs e)
        {
            //if (TxtSomethingElse.Text.Length > 0)
            //{
            //    btnStartMindfulBreathing.IsEnabled = true;

            //    int nDurationSeconds = 0;
            //    JArray arrActivities = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"];
            //    int i = 0;
            //    foreach (JObject item in arrActivities)
            //    {
            //        if (i == ComboBoxActivityType.SelectedIndex)
            //        {
            //            if (item.ContainsKey("duration_seconds"))
            //            {
            //                nDurationSeconds = (int)item.GetValue("duration_seconds");
            //            }

            //            if (item.ContainsKey("video_urls"))
            //            {
            //                JArray array = (JArray)item.GetValue("video_urls");
            //                int n = array.Count;
            //                Random rnd = new Random();
            //                int nRandom = rnd.Next(0, n - 1);
            //                appSettings.OnboardingVideoUrl = array[nRandom].ToString();
            //            }
            //            break;
            //        }
            //        i++;
            //    }

            //    //    "startActivityButtonText":"Start <TIME> <NAME>",
            //    string Title = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();

            //    appSettings.OnboardingActivityName = TxtSomethingElse.Text;
            //    appSettings.OnboardingActivityDurationSeconds = nDurationSeconds;

            //    Title = Title.Replace("<NAME>", TxtSomethingElse.Text);
            //    Title = Title.Replace("<TIME>", appSettings.SecondsToText(nDurationSeconds));

            //    btnStartMindfulBreathingTXT.Text = Title;
            //}
            //else
            //{
            //    btnStartMindfulBreathing.IsEnabled = false;
            //}
        }
        //Yahan se dekhana hai
        private void ComboBoxActivityType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnStartMindfulBreathing.IsEnabled = false;

            string sName = "";
            int nDurationSeconds = 0;
            //if (ComboBoxActivityType.SelectedIndex == ComboBoxActivityType.Items.Count - 1) //Own adventure Selected
            //{
            //    TxtSomethingElse.Visibility = Visibility.Visible;
            //    TxtSomethingElse.Focus();

            //    ActivityName.Visibility = Visibility.Visible;
            //}
            //else
            {
                ///TxtSomethingElse.Text = "";
                ///TxtSomethingElse.Visibility = Visibility.Collapsed;

                ActivityName.Visibility = Visibility.Collapsed;


                sName = ComboBoxActivityType.SelectedItem.ToString();
                btnStartMindfulBreathing.IsEnabled = true;
            }

            JArray arrActivities = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"];
            int i = 0;
            foreach (JObject item in arrActivities)
            {
                if (i == ComboBoxActivityType.SelectedIndex)
                {
                    if (item.ContainsKey("duration_seconds"))
                    {
                        nDurationSeconds = (int)item.GetValue("duration_seconds");
                    }

                    if (item.ContainsKey("video_urls"))
                    {
                        JArray array = (JArray)item.GetValue("video_urls");
                        int n = array.Count;
                        Random rnd = new Random();
                        int nRandom = rnd.Next(0, n - 1);
                        appSettings.OnboardingVideoUrl = array[nRandom].ToString();
                    }

                    break;
                }
                i++;
            }

            //    "startActivityButtonText":"Start <TIME> <NAME>",
            string Title = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();

            appSettings.OnboardingActivityName = sName;
            appSettings.OnboardingActivityDurationSeconds = nDurationSeconds;

            Title = Title.Replace("<NAME>", sName);
            Title = Title.Replace("<TIME>", appSettings.SecondsToText(nDurationSeconds));

            btnStartMindfulBreathingTXT.Text = Title;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBoxActivityType.SelectedIndex = 0;
        }

        private void btnInfo_Click(object sender, RoutedEventArgs e)
        {
            Description.Visibility = Visibility.Visible;

            Suggest.Visibility = Visibility.Collapsed;
            btnInfo.Visibility = Visibility.Collapsed;
        }
    }
}
