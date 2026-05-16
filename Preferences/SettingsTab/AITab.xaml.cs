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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AITab.xaml
    /// </summary>
    public partial class AITab : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public AITab()
        {
            InitializeComponent();

            try
            {
                
                PersonalisedMotivationCheckBoxTXT.Text = appSettings.ConfigFileStrings["aISettingVcInfo"]["checkBoxMorningMotivationText"].ToString();
                PersonalisedMotivationCheckBoxDesc.Text = appSettings.ConfigFileStrings["aISettingVcInfo"]["descriptionMorningMotivationText"].ToString();
                
                UseAIToDecideTXT.Text = appSettings.ConfigFileStrings["aISettingVcInfo"]["checkBoxWebsiteAllowText"].ToString();
                UseAIToDecideDesc.Text = appSettings.ConfigFileStrings["aISettingVcInfo"]["descriptionWebsiteAllowText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AITab load strings EX = " + ex.Message);
            }
        }

        private void PersonalisedMotivationCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bMotivationalMessageEnabled = true;
        }

        private void UseAIToDecide_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bAiCheckUrlEnabled = true;
        }

        private void PersonalisedMotivationCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bMotivationalMessageEnabled = false;
        }

        private void UseAIToDecide_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAiCheckUrlEnabled = false;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                PersonalisedMotivationCheckBox.IsChecked = appSettings.bMotivationalMessageEnabled;
                UseAIToDecideCheckBox.IsChecked = appSettings.bAiCheckUrlEnabled;
            }
        }
    }
}
