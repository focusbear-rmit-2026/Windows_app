using Focus_Bear.Models;
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
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    public partial class AddAppForAlwaysAllowed : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        List<AppDetails> FilteredList = new List<AppDetails>();

        public AddAppForAlwaysAllowed()
        {

        }

        public AddAppForAlwaysAllowed(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();

            this.Owner = ParentWindow;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

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
                btnAddTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["selectApps"].ToString();
                btnEnterManuallyTXT.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["clickToEnterManually"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddAppForAlwaysAllowed LoadTexts ex - " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AddAppForAlwaysAllowed btnAdd_Click()");
            try
            {
                List<AppDetails> ll = appSettings.InstalledApps.Where(item => item.IsSelected).ToList();
                if (ll.Count() > 0)
                {
                    foreach (AppDetails sApp in ll)
                    {
                        var listItems = appSettings.arrAlwaysAllowedApps.Where(i => i.ToLower() == sApp.AppName.ToLower() || i.ToLower() == sApp.ExeName.ToLower());
                        if (listItems.Count() == 0)
                        {
                            appSettings.arrAlwaysAllowedApps.Add(sApp.ExeName);
                            appSettings.CapturePostHog("add-always-allowed-app");
                        }
                    }
                    DialogResult = true;
                    Close();
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
                LogWriter.LogWrite("AddAppForAlwaysAllowed btnAdd_Click ex - " + ex.Message);
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
