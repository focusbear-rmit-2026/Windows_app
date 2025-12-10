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

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for FocusModeSetting.xaml
    /// </summary>
    public partial class FocusModeSetting : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public FocusModeSetting()
        {
            InitializeComponent();
            try
            {

                FocusBearMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleAppMode"].ToString();
                CuddlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxCuddlyBearText"].ToString();
                GrizzlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxGrizzlyBearText"].ToString();

                CuddlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionCuddlyBearText"].ToString();
                GrizzlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionGrizzlyBearText"].ToString();

                CuddlyMode.IsChecked = (appSettings.nFocusBearMode == 1) ? true : false;//1 is cuddly bear mode
                GrizzlyMode.IsChecked = (appSettings.nFocusBearMode == 2) ? true : false;//2 is grizzly bear mode

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusModeSetting text setting ex - " + ex.Message);
            }

        }

        private void GrizzlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocusBearMode = 0;//0 is Grizzly bear mode
        }

        private void CuddlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocusBearMode = 1;//1 is cuddly bear mode
        }
    }
}
