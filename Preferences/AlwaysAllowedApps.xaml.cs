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
                Description.Text = appSettings.ConfigFileStrings["alwaysAllowedAppsViewInfo"]["subTitleText"].ToString();

                btnAddAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
                btnDeleteAppTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipRemove"].ToString();
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
                AddApp aa = new AddApp(_ParentWindow, true);
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

                // Files picked via "Select File" — each stored with all the exes from its install folder.
                foreach (var custom in aa.SelectedCustomApps)
                {
                    var existing = appSettings.arrAlwaysAllowedApps.FirstOrDefault(
                        i => i.AppName.ToLower() == custom.AppName.ToLower());
                    if (existing == null)
                    {
                        appSettings.arrAlwaysAllowedApps.Add(custom);
                        appSettings.CapturePostHog("add-always-allowed-app-manually");
                    }
                }

                // Typed names — added alongside any selected files.
                if (!string.IsNullOrEmpty(sApps))
                {
                    string[] apps = sApps.Split(',');
                    bool bAppAdded = false;
                    foreach (string app in apps)
                    {
                        // Trim surrounding quotes too — Explorer's "Copy as path" wraps the path in quotes,
                        // and a quote is an illegal path char that would otherwise throw in Path.GetFileName.
                        string trimmedApp = app.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(trimmedApp))
                        {
                            string appName = System.IO.Path.GetFileNameWithoutExtension(trimmedApp);
                            // Strip any directory the user pasted — blocking matches on the bare exe name, so a
                            // full path stored here would never match a running process (root cause of #1140).
                            string exeName = System.IO.Path.GetFileName(trimmedApp);
                            var listItems = appSettings.arrAlwaysAllowedApps.Where(i => i.AppName.ToLower() == appName.ToLower());
                            if (listItems.Count() == 0)
                            {
                                appSettings.arrAlwaysAllowedApps.Add(new AppDetails(appName, exeName));
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
                var selectedItem = ListAllowedApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysAllowedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-allowed-app");
                    LocalSettingsManager.PutLocalSettings();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps btnDeleteApp_Click ex - " + ex.Message);
            }
        }
    }
    
}
