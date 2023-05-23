using Focus_Bear.Onboarding.New;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for WelcomeAbordPostpone.xaml
    /// </summary>
    public partial class WelcomeAbordPostpone : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart parentWin = null;
        public WelcomeAbordPostpone()
        {
        }
            public WelcomeAbordPostpone(NewOnboardingStart _parentWin)
        {
            parentWin = _parentWin;
            InitializeComponent();
            InitializeSettings();

          
        }
        void InitializeSettings()
        {
           
            this.Owner = parentWin;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
           // parentWin.RepositionWindow();
            parentWin.GridShade.Visibility = Visibility.Visible;
            /*
               "onboardingPostponeVcInfo": {
                "title": "If you have something urgent, you can postpone the Onboarding process for few Minutes.",
                "selectMinutesText": "Select minutes to postpone:",
                "buttonPostponeText": "Postpone Onboarding"
            },
             */

            Heading.Text = appSettings.ConfigFileStrings["onboardingPostponeVcInfo"]["title"].ToString();
            PostponeMinsText.Text = appSettings.ConfigFileStrings["onboardingPostponeVcInfo"]["selectMinutesText"].ToString();
            btnPostponeText.Text = appSettings.ConfigFileStrings["onboardingPostponeVcInfo"]["buttonPostponeText"].ToString();

        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                parentWin.GridShade.Visibility = Visibility.Collapsed;
                Close();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            parentWin.GridShade.Visibility = Visibility.Collapsed;
            Close();
        }

        private void btnPostponeOnboarding_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnPostponeOnboarding_Click");
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
                parentWin.pageBuildHabits.PostponeOnboarding(nMinutesToPostpone);
                parentWin.GridShade.Visibility = Visibility.Collapsed;
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnPostponeOnboarding_Click - " + ex.Message);
               // Debug.WriteLine(ex.Message);
            }

        }
    }
}
