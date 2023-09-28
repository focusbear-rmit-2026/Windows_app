using Focus_Bear.API;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for AnalyzeSleepMoodWindow.xaml
    /// </summary>
    public partial class AnalyzeSleepMoodWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public AnalyzeSleepMoodWindow(HabitWindow parentWindow)
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Owner = parentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["description"].ToString();
                Data.Text = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["subTitleText"].ToString();
                Data1.Text = "• " + appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["pointFirstText"].ToString();
                Data2.Text = "• " + appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["pointSecondText"].ToString();
                Data3.Text = "• " + appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["pointThirdText"].ToString();
                btnNoThanksText.Text = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["buttonCancelText"].ToString();
                btnSureText.Text = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["buttonOkText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AnalyzeSleepMoodWindow() load text Exception - " + ex.Message);
            }
        }

        private async void btnSure_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("attempt to GetUserSettings with new habit pack");

            FocusBearAPIClient client = new FocusBearAPIClient();
            btnSure.IsEnabled = false;
            btnNoThanks.IsEnabled = false;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            string sHabitIdPack = "f522e5d0-6e40-4dd4-9010-338529a7223e";
            try
            {
                sHabitIdPack = appSettings.ConfigFileStrings["impactMeasurementVcInfo"]["packId"].ToString();
            }
            catch (Exception ex)
            {
                sHabitIdPack = "f522e5d0-6e40-4dd4-9010-338529a7223e";
            }

            try
            {
                var userSettingsResponse = await client.GetUserSettings(sHabitIdPack);
                int x = int.Parse(userSettingsResponse.ToString());
                if (x == 1)
                {
                    LogWriter.LogWrite("habit pack install Success, restart");

                    RegistryManager.SetAnalyzeSleepAndMoodStatus();
                    //Restart the app.

                    LogWriter.LogWrite("btnSure_Click - Restart app now for settings to take Effect!");

                    DialogResult = true;
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSure_Click ex = " + ex.Message);
            }
            Mouse.OverrideCursor = null;
            Close();
        }

        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            RegistryManager.SetAnalyzeSleepAndMoodStatus();
            Close();
        }
    }
}

