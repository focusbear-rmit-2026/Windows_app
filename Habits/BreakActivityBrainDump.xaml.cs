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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for BreakActivityBrainDump.xaml
    /// </summary>
    public partial class BreakActivityBrainDump : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitWindow _parentWindow;
        public BreakActivityBrainDump()
        {

        }
        public BreakActivityBrainDump(HabitWindow parentWindow) : this()
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


            _parentWindow = parentWindow;
            if (RegistryManager.GetBrainDump())
            {
                TxtDumpDown.Text = appSettings.sBrainDump;
            }

            
            try
            {

                Heading.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["description"].ToString();
                btnStartBreakTXT.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["buttonStartBreakText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakActivityBrainDump text setting ex -" + ex.Message);
            }
        }

        private void btnStartBreak_Click(object sender, RoutedEventArgs e)
        {
            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            if (appSettings.isExpired)
            {
                _parentWindow.GridShade.Visibility = Visibility.Visible;

                appSettings.CapturePostHog("alert-limited-usage-shown");

                string sTitle = appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["title"].ToString().Replace("<COUNT>", "3");
                string sSubTitle = appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["subTitle"].ToString().Replace("<COUNT>", "3");
                FBMessageBox ff = new FBMessageBox(sSubTitle, sTitle, MessageBoxButton.YesNo, appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["buttonUpgradeNowText"].ToString(), appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["buttonContinueText"].ToString());
                ff.Owner = _parentWindow;
                ff.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                //if user has cancelled the update process
                if (ff.btnClicked == MessageBoxResult.Yes)
                {
                    appSettings.CapturePostHog("alert-limited-usage-button-upgrade-clicked");
                    ((App)(Application.Current)).ShowUpgradeToProNag("");
                }
                else
                {
                    appSettings.CapturePostHog("alert-limited-usage-button-continue-clicked");
                }
            }

            _parentWindow.ShowStartBreakActivityScreen();

        }
    }
}
