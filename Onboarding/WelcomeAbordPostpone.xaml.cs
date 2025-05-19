using Focus_Bear.Onboarding.New;
using Focus_Bear.Utils;
using Sentry.Protocol;
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
using System.Windows.Interop;
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
        App app = (App)Application.Current;
        private NewOnboardingStart parentWin = null;
        public WelcomeAbordPostpone()
        {
        }
        public WelcomeAbordPostpone(NewOnboardingStart _parentWin)
        {
            parentWin = _parentWin;
            InitializeComponent();
            InitializeSettings();
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;

        }
        void InitializeSettings()
        {
           
            this.Owner = parentWin;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            parentWin.GridShade.Visibility = Visibility.Visible;
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
            appSettings.CapturePostHog("postpone-initial-onboarding");
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
            }

        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }

        }
    }
}
