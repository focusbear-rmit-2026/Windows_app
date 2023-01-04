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

            this.Topmost = true;
            this.ShowInTaskbar = false;

            this.Owner = parentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _parentWindow = parentWindow;

            /* "activityPostponeInfo": {
                "postponeHabitsText": "If you have something urgent, you can postpone Habits for a few minutes.",
                "postponeBreakText": "If you have something urgent, you can postpone the Break Activity for 5 Minutes.",
                "buttonTakeBreakText": "Argh ok I'll take a break!",
                "selectMinutesText": "Select minutes to postpone:",
                "buttonCompletelySkipText": "Completely Skip",
                "buttonPostponeHabitText": "Postpone Habit",
                "buttonPostponeBreakText": "Postpone Break",
                "buttonAbortBreakText": "Abort Break Activity",
                "postponedLastBreakText": "You postponed the last break - are you sure you can't make time for the break now?",
                "buttonEnableOfficeModeText": "Switch to office mode",
                "officeModeEnableText": "Ok I'll give you some break activities that are appropriate to do in an office environment.",
                "postponedLastHabitText": "You've postponed your habits a few times. Are you sure you want to postpone again?",
                "buttonDoHabitText": "I changed my mind - I'll do my habits"
            },*/

            Heading.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["postponeHabitsText"].ToString();
            PostponeMinsText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["selectMinutesText"].ToString();
            btnPostponeText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["buttonPostponeHabitText"].ToString();
            btnCompletelySkipText.Text = appSettings.ConfigFileStrings["activityPostponeInfo"]["buttonCompletelySkipText"].ToString();

        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
               // parentWin.GridShade.Visibility = Visibility.Collapsed;
                Close();
            }
        }
        private void btnCompletelySkip_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Work In progress!", "Focus Bear");
        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnPostpone_Click habit postpone");
            /*
            *  <ComboBoxItem Content="5"></ComboBoxItem>
               <ComboBoxItem Content="10"></ComboBoxItem>
               <ComboBoxItem Content="15"></ComboBoxItem>
               <ComboBoxItem Content="20"></ComboBoxItem>
               <ComboBoxItem Content="25"></ComboBoxItem>
               <ComboBoxItem Content="30"></ComboBoxItem>
               <ComboBoxItem Content="45"></ComboBoxItem>
               <ComboBoxItem Content="60"></ComboBoxItem>
               <ComboBoxItem Content="90"></ComboBoxItem>
            */
            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                int nMinutesToPostpone = int.Parse(typeItem.Content.ToString());
                _parentWindow.PostponeOnboarding(nMinutesToPostpone);
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
