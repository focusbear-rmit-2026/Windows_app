using Focus_Bear.Models;
using Focus_Bear.Utils;
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
using Microsoft.Win32;

namespace Focus_Bear.Preferences
{
    public partial class AlwaysBlockedApps : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        public AlwaysBlockedApps()
        {
            
        }
        public AlwaysBlockedApps(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;

            LoadTexts();

            ListAllowedApps.ItemsSource = appSettings.arrAlwaysBlockedApps;
            
        }

        void LoadTexts()
        {
            try
            {
                Description.Text = appSettings.ConfigFileStrings["blockedAppsViewInfo"]["subTitleTextNew"].ToString();

                btnAddAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
                btnDeleteAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipRemove"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysBlockedApps LoadTexts ex - " + ex.Message);
            }
        }

        private void btnAddApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ParentWindow.RepositionWindow();
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                AddApp aa = new AddApp(_ParentWindow, false);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (DialogResult.Value == false)
                {
                    ShowAddAppManuallyWindow();
                }
                LocalSettingsManager.PutLocalSettings();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysBlockedApps btnAddApp_Click ex - " + ex.Message);
            }
        }

        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("AlwaysBlockedApps ShowAddAppManuallyWindow()");

            try
            {
                _ParentWindow.RepositionWindow();
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                AddAppsManually aa = new AddAppsManually(_ParentWindow);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                string sApps = "";

                switch (DialogResult)
                {
                    case true:
                        sApps = aa.sAppsToAdd;
                        break;
                    case false:
                        return;
                    default:
                        return;
                }

                if (!string.IsNullOrEmpty(sApps))
                {
                    string[] apps = sApps.Split(',');
                    bool bAppAdded = false;
                    foreach (string app in apps)
                    {
                        string trimmedApp = app.Trim();
                        if (!string.IsNullOrEmpty(trimmedApp))
                        {
                            string appName = System.IO.Path.GetFileNameWithoutExtension(trimmedApp);
                            var listItems = appSettings.arrAlwaysBlockedApps.Where(i => i.AppName.ToLower() == appName.ToLower());
                            if (listItems.Count() == 0)
                            {
                                appSettings.arrAlwaysBlockedApps.Add(new AppDetails(appName, trimmedApp));
                                bAppAdded = true;
                            }
                        }
                    }
                    if (bAppAdded)
                        appSettings.CapturePostHog("add-always-blocked-app-manually");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysBlockedApps ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }

        private void btnDeleteApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListAllowedApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysBlockedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-allowed-app");
                    LocalSettingsManager.PutLocalSettings();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysBlockedApps btnDeleteApp_Click ex - " + ex.Message);
            }
        }
    }
    
}
