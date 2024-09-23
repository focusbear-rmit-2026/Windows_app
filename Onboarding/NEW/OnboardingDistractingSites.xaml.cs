using Focus_Bear.Onboarding.AfterLogin;
using Focus_Bear.Onboarding.New;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using System.Windows.Threading;

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingDistractingSites.xaml
    /// </summary>
    public partial class OnboardingDistractingSites : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart _parentWindow;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        public OnboardingDistractingSites()
        {
        }

        public OnboardingDistractingSites(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                WhatSitesHeading.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["questionText"].ToString();
                BlockCertainSites.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["description"].ToString();
                EnterURL.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["enterUrlText"].ToString();
                btnAddUrlTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonAddUrlText"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonContinueText"].ToString();
                PreferencesDesc.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["bottomText"].ToString();
                Privacy.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["tipText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingDistractingSites()k EX -" + ex.Message);
            }
            //Default Always Blocked URLS
            appSettings.arrDistractingURLs.Add(new DistractingURL("facebook.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("x.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("instagram.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("tiktok.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("bbc.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("cnn.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("reddit.com", true));
            appSettings.arrDistractingURLs.Add(new DistractingURL("youtube.com", true));

            string sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["blockSitesCountText"].ToString();
            sCount = sCount.Replace("<COUNT>", appSettings.arrDistractingURLs.Count.ToString());
            CountBlockedSites.Text = sCount;

            appSettings.arrDistractingURLs.CollectionChanged += DistractingURLs_CollectionChanged;

            ListBLockedURLs.ItemsSource = appSettings.arrDistractingURLs;

            //initially disable the button
            btnAddUrl.IsEnabled = false;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            SetBlockedText();
        }
        void DistractingURLs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SetBlockedText();
        }

        private void SetBlockedText()
        {
            string sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["blockSitesCountText"].ToString();

            var listItems = appSettings.arrDistractingURLs.Where(i => i.IsItemChecked == true);
            if (listItems.Count() == 1)
            {
                sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["oneSiteBlockText"].ToString();
            }
            else
            {
                sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["blockSitesCountText"].ToString();
                sCount = sCount.Replace("<COUNT>", listItems.Count().ToString());
            }

            CountBlockedSites.Text = sCount;
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
                appSettings.bHasEditedBlockedURLs = true;
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();

            //we need to add the URLS to always blocked urls
            for (int i = 0; i < appSettings.arrDistractingURLs.Count(); i++)
            {
                if(appSettings.arrDistractingURLs[i].IsItemChecked)
                    appSettings.arrAlwaysBlockedURLs.Add(appSettings.arrDistractingURLs[i].sURL);
                else
                    appSettings.bHasEditedBlockedURLs = true;
            }
            
            _parentWindow.ShowPrivacyScreen();
        }
    }
}
