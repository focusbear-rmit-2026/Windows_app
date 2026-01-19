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
    public partial class AlwaysAllowedApps : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        public AlwaysAllowedApps()
        {
            
        }
        public AlwaysAllowedApps(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;

            LoadTexts();

            ListAllowedApps.ItemsSource = appSettings.arrAlwaysAllowedApps;
            
        }

        void LoadTexts()
        {
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["alwaysAllowedAppsViewInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["alwaysAllowedAppsViewInfo"]["subTitleText"].ToString();

                btnAddAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
                btnDeleteAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonRemoveText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps LoadTexts ex - " + ex.Message);
            }
        }

        private void btnAddApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ParentWindow.RepositionWindow();
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                AddAppForAlwaysAllowed aa = new AddAppForAlwaysAllowed(_ParentWindow);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (DialogResult.Value == false)
                {
                    ShowAddAppManuallyWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps btnAddApp_Click ex - " + ex.Message);
            }
        }

        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("AlwaysAllowedApps ShowAddAppManuallyWindow()");

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
                            var listItems = appSettings.arrAlwaysAllowedApps.Where(i => i.ToLower() == trimmedApp.ToLower());
                            if (listItems.Count() == 0)
                            {
                                appSettings.arrAlwaysAllowedApps.Add(trimmedApp);
                                bAppAdded = true;
                            }
                        }
                    }
                    if (bAppAdded)
                        appSettings.CapturePostHog("add-always-allowed-app-manually");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }

        private void btnDeleteApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListAllowedApps.SelectedItem as string;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysAllowedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-allowed-app");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps btnDeleteApp_Click ex - " + ex.Message);
            }
        }
    }
    
}
