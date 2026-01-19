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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for BlockingSchedule.xaml
    /// </summary>
    public partial class BlockingSchedule : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusBearMainWindow _ParentWindow = null;
        public BlockingSchedule(FocusBearMainWindow ParentWindow)
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

            _ParentWindow = ParentWindow;

            try
            {
                btnNewBlockingScheduleTXT.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonAddNewBlock"].ToString();
            }
            catch(Exception ex)
            {
                LogWriter.LogWrite("Blocking schedule text set ex- " + ex.Message);
            }

            ListBlockingSchedules.ItemsSource = appSettings.arrCustomBlockingSchedules;
        }

        private void ListBlockingSchedules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogWriter.LogWrite("Blocking schedule clicked - ListBlockingSchedules_SelectionChanged");

            try
            {
                var selectedItem = ListBlockingSchedules.SelectedItem as CustomBlockingSchedule;

                if (selectedItem != null)
                {
                    _ParentWindow.PageBlocks.ShowBlockingScheduleDetails(selectedItem);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ListBlockingSchedules_SelectionChanged ex - " + ex.Message);
            }

        }

        private void btnNewBlockingSchedule_Click(object sender, RoutedEventArgs e)
        {
            _ParentWindow.PageBlocks.ShowBlockingScheduleDetails(null);
        }
    }
}
