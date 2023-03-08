using Focus_Bear.Onboarding.AfterLogin;
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
    /// Interaction logic for OnboardingDistractingSites.xaml
    /// </summary>
    public partial class OnboardingDistractingSites : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private OnboardingSetupAndTipsWindow _parentWindow;
        public OnboardingDistractingSites()
        {
        }

        public OnboardingDistractingSites(OnboardingSetupAndTipsWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            WhatSitesHeading.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["questionText"].ToString();
            BlockCertainSites.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["description"].ToString();
            EnterURL.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["enterUrlText"].ToString();
            btnAddUrlTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonAddUrlText"].ToString();
            btnContinueTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonContinueText"].ToString();
            PreferencesDesc.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["bottomText"].ToString();

            //Default Always Blocked URLS
            appSettings.arrDistractingURLs.Add(new DistractingURL("facebook.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("twitter.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("instagram.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("tiktok.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("bbc.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("cnn.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("reddit.com", true));

            //TxtURL
            ListBLockedURLs.ItemsSource = appSettings.arrDistractingURLs;

            //initially disable the button
            btnAddUrl.IsEnabled = false;
        }

        private void TxtURL_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TxtURL.Text;
            text = text.Trim();
            if (text.Length > 0)
            {
                btnAddUrl.IsEnabled = true;
            }
            else
            {
                btnAddUrl.IsEnabled = false;
            }
        }

        private void btnAddUrl_Click(object sender, RoutedEventArgs e)
        {
            var listItems = appSettings.arrDistractingURLs.Where(i => i.sURL.ToLower() == TxtURL.Text.ToLower());
            if (listItems.Count() == 0)
            {
                appSettings.arrDistractingURLs.Add(new DistractingURL(TxtURL.Text.ToLower(), true));
                ListBLockedURLs.ScrollIntoView(ListBLockedURLs.Items[ListBLockedURLs.Items.Count - 1]); //scroll to last
                TxtURL.Text = "";
                TxtURL.Focus();

            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            //we need to add the URLS to always blocked urls
            for(int i = 0; i < appSettings.arrDistractingURLs.Count(); i++)
            {
                if(appSettings.arrDistractingURLs[i].IsItemChecked)
                    appSettings.arrAlwaysBlockedURLs.Add(appSettings.arrDistractingURLs[i].sURL);
            }
            
            LocalSettingsManager.PutLocalSettings();
            _parentWindow.ShowTypeOfWorkScreen();
        }
    }
}
