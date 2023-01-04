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
    /// <summary>
    /// Interaction logic for AddAppsManually.xaml
    /// </summary>
    public partial class AddAppsManually : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public string sAppsToAdd;
        public AddAppsManually()
        {

        }
        public AddAppsManually(PreferencesWindow _ParentWindow)
        {
            InitializeComponent();

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Heading.Text = "Enter apps manually:"; //todo win text
            Instructions.Text = "If you know the name of the EXEs that you want to allow, you can add them here. You can enter multiple exe names seperated with comma. (Example: chrome.exe, firefox.exe, notepad.exe)";

            btnCancel.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonCancelText"].ToString();
            btnAdd.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddText"].ToString();

            btnAdd.IsEnabled = false;
        }

        private void TxtApps_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TxtApps.Text;
            text = text.Trim();
            if (text.Length > 0)
            {
                btnAdd.IsEnabled = true;
            }
            else
            {
                btnAdd.IsEnabled = false;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            sAppsToAdd = TxtApps.Text;

            DialogResult = true;
            Close();
        }
    }
}
