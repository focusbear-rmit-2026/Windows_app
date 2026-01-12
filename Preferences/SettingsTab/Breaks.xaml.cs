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

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for Breaks.xaml
    /// </summary>
    public partial class Breaks : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public Breaks()
        {
        }
        public Breaks(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();
            _ParentWindow = ParentWindow;

            try
            {
                EnableBreaks.Text = appSettings.ConfigFileStrings["microBreaksSettingsVcInfo"]["enableBreaksText"].ToString();
                EnableBreaksDesc.Text = appSettings.ConfigFileStrings["tooltips"]["breaks_EnableBreaks"].ToString();
                EnableBreaksCheckbox.IsChecked = appSettings.bAreBreaksEnabled;

                ShowBreaksWhileInCall.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxShowBreaksInCallText"].ToString();
                ShowBreaksWhileInCallDesc.Text = appSettings.ConfigFileStrings["tooltips"]["advanced_MicroBreak_ShowBreaksInCall"].ToString();
                btnShowBreaksWhileInCall.IsChecked = appSettings.bShowBreaksWhileInCall;


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings BREAKS Page text setting ex -" + ex.Message);
            }

        }

        private void EnableBreaksCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bAreBreaksEnabled = true;

            //Only start the timer if the morning habits are shown
            bool isMorningHabitsAlreadyShown = false;
            if (RegistryManager.GetMorningHabitsShownTillDate())
            {
                if (DateTime.Now < appSettings.MorningHabitsShownTillDate)
                {
                    isMorningHabitsAlreadyShown = true;
                }
                else
                {
                    isMorningHabitsAlreadyShown = false;
                }
            }
            else
            {
                isMorningHabitsAlreadyShown = false;
            }

            if (isMorningHabitsAlreadyShown)
                ((App)(Application.Current)).StartBreakActivityTimer();
        }

        private void EnableBreaksCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bAreBreaksEnabled = false;
            ((App)(Application.Current)).StopBreakActivityTimer();
        }
        private void btnShowBreaksWhileInCall_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowBreaksWhileInCall = true;
            appSettings.CapturePostHog("settings-changed-enable-breaks-while-call");
        }
        private void btnShowBreaksWhileInCall_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowBreaksWhileInCall = false;
            appSettings.CapturePostHog("settings-changed-disable-breaks-while-call");
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                EnableBreaksCheckbox.IsChecked = appSettings.bAreBreaksEnabled;
                btnShowBreaksWhileInCall.IsChecked = appSettings.bShowBreaksWhileInCall;
            }
        }
    }
}
