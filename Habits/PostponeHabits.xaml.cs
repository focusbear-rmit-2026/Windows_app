using Focus_Bear.Focus;
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
    /// Interaction logic for PostponeHabits.xaml
    /// </summary>
    public partial class PostponeHabits : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusActivitySummary _parentWindow = null;
        public PostponeHabits()
        {
        }
            public PostponeHabits(FocusActivitySummary parentWindow)
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

            _parentWindow = parentWindow;

            Heading.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["postponeHabitsText"].ToString();
            PostponeMinsText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["selectMinutesText"].ToString();
            btnPostponeText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["buttonPostponeHabitText"].ToString();
            btnCompletelySkipText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["buttonCompletelySkipText"].ToString();

        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
        private void btnCompletelySkip_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("Work In progress!", "Focus Bear");
            ff.ShowDialog();
        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnPostpone_Click habit postpone");
            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                int nMinutesToPostpone = int.Parse(typeItem.Content.ToString());
                _parentWindow.PostponeFocusSummary(nMinutesToPostpone);
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnPostpone_Click habit postpone" + ex.Message);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
