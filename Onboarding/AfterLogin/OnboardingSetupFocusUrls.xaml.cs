using Focus_Bear.Preferences;
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

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingSetupFocusUrls.xaml
    /// </summary>
    public partial class OnboardingSetupFocusUrls : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        OnboardingSetupAndTipsWindow _parentWindow;
        public OnboardingSetupFocusUrls()
        {
        }
        public OnboardingSetupFocusUrls(OnboardingSetupAndTipsWindow parentWindow)
        {
            InitializeComponent();

            LogWriter.LogWrite("OnboardingSetupFocusUrls screen");

            _parentWindow = parentWindow;

            /* "preloadFocusModeAppsVcInfo": {
            "buttonDoneText": "Done",
            "titleForApps": "What apps do you normally use while you're doing <WORK_TYPE>?",
            "descriptionApps": "Don't worry you can still use other apps if you need to but these are the apps that are most likely to be productive for you while you're doing <WORK_TYPE>. You can change these settings later on.",
            "subTitleForApps": "Add the apps you plan to use:",
            "titleForUrls": "How about websites?",
            "descriptionUrls": "Don't worry you can still use other websites if you need to but these are the sites that are most likely to be productive for you while you're doing <WORK_TYPE>. You can change these settings later on.",
            "subTitleForUrls": "Add the websites you plan to use:"
             },*/

            string ss = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["titleForUrls"].ToString();
            ss = ss.Replace("<WORK_TYPE>", appSettings.FocusModeNameToCreateWhileOnBoarding.name);
            WhatSitesHeading.Text = ss;

            btnAddURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipAdd"].ToString();
            btnDeleteURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipRemove"].ToString();

            string ss1 = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["descriptionUrls"].ToString();
            ss1 = ss1.Replace("<WORK_TYPE>", appSettings.FocusModeNameToCreateWhileOnBoarding.name);
            DontWorry.Text = ss1;

            AddSites.Text = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["subTitleForUrls"].ToString();

            btnContinueTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonContinueText"].ToString();

            ListAllowedURLs.ItemsSource = appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedUrls;
        }

        private void btnAddURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                AddWebUrl aa = new AddWebUrl(_parentWindow); ;

                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                string url = "";


                switch (DialogResult)
                {
                    case true:
                        // Url added
                        url = aa.sURLToAdd;
                        break;
                    case false:
                        // User did not postponed the breaks..normal flow follows
                        break;
                    default:
                        // Indeterminate
                        break;
                }
                if (url.Length > 0)
                {
                    //add to focus mode urls list

                    //First check if it is already added
                    var listItems = appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedUrls.Where(i => i == url);
                    if (listItems.Count() == 0)
                    {
                        appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedUrls.Add(url);
                        appSettings.CapturePostHog("add-allowed-url-in-focus-mode");
                    }
                    else
                    {
                        //"urlAlreadyExistsText":"<APPNAME> already exists in the list.",
                        string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                        sErr = sErr.Replace("<APPNAME>", url);
                        //// MessageBox.Show(sErr, "");
                        FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                        ff.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnAddURL_Click ex - " + ex.Message);
            }

        }

        private void btnDeleteURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListAllowedURLs.SelectedItem as string;
                if (selectedItem != null)
                {
                    appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedUrls.Remove(selectedItem);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnDeleteURL_Click  ex -" + ex.Message);
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowFocusTipsScreen();

        }
    }
}
