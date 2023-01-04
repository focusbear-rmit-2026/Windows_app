using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// <summary>
    /// Interaction logic for AddApp.xaml
    /// </summary>
    public partial class AddApp : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusMode selectedFocusMode = null;

        public AddApp()
        {

        }
        public AddApp(PreferencesWindow ParentWindow, FocusMode sFocusMode)
        {
            InitializeComponent();

            selectedFocusMode = sFocusMode;

            this.Owner = ParentWindow;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            ListInstalledApps.ItemsSource = appSettings.InstalledApps;


            /* "focusPreferencesInfo":{
             * "buttonAddText":"Add",
             */

            btnAddTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString(); //"Add";
            btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString(); //"Cancel";
            Heading.Text = "Select Apps to use while focusing:"; //todo win text
            btnEnterManuallyTXT.Text = "Can't find your app in the above list? Click here to enter manually."; //todo win text
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
                if (selectedFocusMode != null && ll.Count() > 0)
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
                else
                {
                    MessageBox.Show("No items selected to Add.", "Focus Bear"); //todo win text
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
    }
}
