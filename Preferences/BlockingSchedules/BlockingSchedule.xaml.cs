using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Maps a card's ListView.AlternationIndex (0-7, cycling via AlternationCount="8") to a distinct
    // accent colour, matching mac's BlockingScheduleStore.schedulePalette exactly so the same
    // schedule reads with a similar accent on both platforms (colours are position-based, not
    // per-schedule state, on both apps).
    public class ScheduleAccentColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush[] Palette = new[]
        {
            new SolidColorBrush(Color.FromRgb(0xF2, 0x8C, 0x26)), // orange
            new SolidColorBrush(Color.FromRgb(0x33, 0xA3, 0x9E)), // teal
            new SolidColorBrush(Color.FromRgb(0x45, 0x85, 0xD1)), // blue
            new SolidColorBrush(Color.FromRgb(0x8F, 0x66, 0xC7)), // purple
            new SolidColorBrush(Color.FromRgb(0x5C, 0xAD, 0x66)), // green
            new SolidColorBrush(Color.FromRgb(0xDE, 0x66, 0x78)), // pink
            new SolidColorBrush(Color.FromRgb(0xD6, 0xA3, 0x33)), // amber
            new SolidColorBrush(Color.FromRgb(0x66, 0x78, 0xC2)), // indigo
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = value is int i ? i : 0;
            int n = Palette.Length;
            return Palette[((index % n) + n) % n];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

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

            // Bind the live collection then kick off a server refresh so schedules created or
            // deleted on other devices show up without restarting the app. #1114
            ListBlockingSchedules.ItemsSource = appSettings.arrCustomBlockingSchedules;
            _ = Focus_Bear.Services.BlockingScheduleManager.RefreshBlockingSchedulesFromServerAsync();
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
