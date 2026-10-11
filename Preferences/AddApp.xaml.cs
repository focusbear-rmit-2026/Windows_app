using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AddApp.xaml
    /// </summary>
    public partial class AddApp : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusMode selectedFocusMode = null;
        bool isForAlwaysAllowedApps = false;
        bool isForAlwaysBlockedApps = false;

        List<AppDetails> FilteredList = new List<AppDetails>();
        public AddApp()
        {

        }

        public AddApp(FocusBearMainWindow ParentWindow, FocusMode sFocusMode)
        {
            InitializeComponent();

            selectedFocusMode = sFocusMode;

            this.Owner = ParentWindow;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Never taller than the screen's work area (a 569-px laptop screen at 175%): the footer with Done / Cancel stays on it.
            Focus_Bear.ToDoScreens.DialogFit.Apply(this);

            LoadTexts();
        }

        public AddApp(FocusBearMainWindow ParentWindow, bool forAlwaysAllowedApps)
        {
            InitializeComponent();

            if(forAlwaysAllowedApps) //if true, its forAlwaysAllowedApps, if false its ForAlwaysBlockedApps
            {
                isForAlwaysAllowedApps = true;
            }
            else
            {
                isForAlwaysBlockedApps = true;
            }

            this.Owner = ParentWindow;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Never taller than the screen's work area (a 569-px laptop screen at 175%): the footer with Done / Cancel stays on it.
            Focus_Bear.ToDoScreens.DialogFit.Apply(this);

            LoadTexts();
        }

        void LoadTexts()
        {
            foreach (AppDetails sApp in appSettings.InstalledApps)
            {
                sApp.IsSelected = false;
            }

            FilteredList = appSettings.InstalledApps;
            ListInstalledApps.ItemsSource = FilteredList;

            try
            {
                btnAddTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString(); //"Add";
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString(); //"Cancel";
                Heading.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["selectApps"].ToString(); //"Select Apps to use while focusing:";  
                btnEnterManuallyTXT.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["clickToEnterManually"].ToString(); //"Can't find your app in the above list? Click here to enter manually.";  
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddApp load text ex + " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Add Apps btnAdd_Click()");
            try
            {
                List<AppDetails> ll = appSettings.InstalledApps.Where(item => item.IsSelected).ToList();
                if (ll.Count() > 0)
                {
                    if (isForAlwaysAllowedApps)
                    {
                        foreach (AppDetails sApp in ll)
                        {
                            // Check if app is already in the list by comparing AppName
                            if (!appSettings.arrAlwaysAllowedApps.Any(i => i.AppName == sApp.AppName))
                            {
                                appSettings.arrAlwaysAllowedApps.Add(sApp);
                                appSettings.CapturePostHog("add-always-allowed-app");
                            }
                        }
                        DialogResult = true;
                        Close();
                    }
                    else if (isForAlwaysBlockedApps)
                    {
                        foreach (AppDetails sApp in ll)
                        {
                            // Check if app is already in the list by comparing AppName
                            if (!appSettings.arrAlwaysBlockedApps.Any(i => i.AppName == sApp.AppName))
                            {
                                appSettings.arrAlwaysBlockedApps.Add(sApp);
                                appSettings.CapturePostHog("add-always-blocked-app");
                            }
                        }
                        DialogResult = true;
                        Close();
                    }
                    else if (selectedFocusMode != null)
                    {
                        foreach (AppDetails sApp in ll)
                        {
                            var listItems = selectedFocusMode.arrAllowedAppsWin.Where(i => i.AppName == sApp.AppName);
                            if (listItems.Count() == 0)
                            {
                                selectedFocusMode.arrAllowedAppsWin.Add(sApp);
                                appSettings.CapturePostHog("add-allowed-app-in-focus-mode");
                            }
                        }
                        DialogResult = true;
                        Close();
                    }
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox("No items selected to Add.", "Focus Bear");
                    ff.Owner = this;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnAdd_Click Add App Exception" + ex.Message);
            }
        }

        private void btnEnterManually_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilteredList = appSettings.InstalledApps.Where(i => i.AppName.ToLower().Contains(SearchBox.Text.ToLower())).ToList();
            ListInstalledApps.ItemsSource = FilteredList;
        }
    }
}
