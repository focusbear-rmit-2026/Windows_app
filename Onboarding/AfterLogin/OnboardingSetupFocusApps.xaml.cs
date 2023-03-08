using Focus_Bear.Models;
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
    /// Interaction logic for OnboardingSetupFocusApps.xaml
    /// </summary>
    public partial class OnboardingSetupFocusApps : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        OnboardingSetupAndTipsWindow _parentWindow;
        public OnboardingSetupFocusApps()
        {
        }
        public OnboardingSetupFocusApps(OnboardingSetupAndTipsWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            LogWriter.LogWrite("OnboardingSetupFocusApps screen");

            /* "preloadFocusModeAppsVcInfo": {
            "buttonDoneText": "Done",
            "titleForApps": "What apps do you normally use while you're doing <WORK_TYPE>?",
            "descriptionApps": "Don't worry you can still use other apps if you need to but these are the apps that are most likely to be productive for you while you're doing <WORK_TYPE>. You can change these settings later on.",
            "subTitleForApps": "Add the apps you plan to use:",
            "titleForUrls": "How about websites?",
            "descriptionUrls": "Don't worry you can still use other websites if you need to but these are the sites that are most likely to be productive for you while you're doing <WORK_TYPE>. You can change these settings later on.",
            "subTitleForUrls": "Add the websites you plan to use:"
             },*/

            string ss = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["titleForApps"].ToString();
            ss = ss.Replace("<WORK_TYPE>", appSettings.FocusModeNameToCreateWhileOnBoarding.name);
            WhatAppsHeading.Text = ss;

            btnAddApp.ToolTip = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipAdd"].ToString();  
            btnDeleteApp.ToolTip = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipRemove"].ToString();
          
            string ss1 = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["descriptionApps"].ToString();
            ss1 = ss1.Replace("<WORK_TYPE>", appSettings.FocusModeNameToCreateWhileOnBoarding.name);
            DontWorry.Text = ss1;

            AddApps.Text = appSettings.ConfigFileStrings["preloadFocusModeAppsVcInfo"]["subTitleForApps"].ToString();

            btnContinueTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonContinueText"].ToString();

            ListAllowedApps.ItemsSource = appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedAppsWin;
        }

        private void btnAddApp_Click(object sender, RoutedEventArgs e)
        {
             _parentWindow.GridShade.Visibility = Visibility.Visible;
            AddApp aa = new AddApp(_parentWindow, appSettings.FocusModeNameToCreateWhileOnBoarding);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();

            _parentWindow.GridShade.Visibility = Visibility.Collapsed;

            if (DialogResult.Value == false) //means the Add apps manually button is clicked
            {
                ShowAddAppManuallyWindow();
            }
        }

        private void btnDeleteApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListAllowedApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedAppsWin.Remove(selectedItem);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetupFocusApps - btnDeleteApp_Click ex - " + ex.Message);
            }
        }

        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("ShowAddAppManuallyWindow()");


           // _parentWindow.RepositionWindow();
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            AddAppsManually aa = new AddAppsManually(_parentWindow);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();

            _parentWindow.GridShade.Visibility = Visibility.Collapsed;

            string sApps = "";

            switch (DialogResult)
            {
                case true:
                    // Url added
                    sApps = aa.sAppsToAdd;
                    break;
                case false:
                    // User did not postponed the breaks..normal flow follows
                    break;
                default:
                    // Indeterminate
                    break;
            }

            try
            {
                if (sApps.Length > 0)
                {
                    //add to focus mode urls list

                    //First check if it is already added
                   // var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                   // if (selectedFocusMode != null)
                    {
                        //Seperate the text with ,
                        string[] data = sApps.Split(',');
                        if (data != null && data.Count() > 0)
                        {
                            bool bAppAdded = false;
                            foreach (string ss in data)
                            {
                                string app = ss.Trim().ToLower();

                                if (app.Length > 0 && app.Contains(".exe"))
                                {
                                    //First check if this is not already present in the apps
                                    var listItems = appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedAppsWin.Where(i => i.AppName == app);
                                    if (listItems.Count() == 0)
                                    {
                                        appSettings.FocusModeNameToCreateWhileOnBoarding.arrAllowedAppsWin.Add(new AppDetails(app, app));
                                        bAppAdded = true;
                                    }
                                }
                            }
                            if (bAppAdded)
                                appSettings.CapturePostHog("add-allowed-app-manually-in-focus-mode");
                        }


                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowSetupFocusURLsScreen();

        }
    }
}
