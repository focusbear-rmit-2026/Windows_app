using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Asked after every second postpone from the break warning, as on the Mac (deactivateBreaksVc): keep breaks,
    /// turn them off for today, or turn them off completely.
    /// </summary>
    public partial class DeactivateBreaksWindow : Window
    {
        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private readonly App app = (App)Application.Current;

        public DeactivateBreaksWindow()
        {
            InitializeComponent();

            TitleText.Text = appSettings.GetConfigStringSafe(TitleText.Text, "deactivateBreaksVcInfo", "title");
            btnTurnOffForTodayTXT.Text = appSettings.GetConfigStringSafe(btnTurnOffForTodayTXT.Text, "deactivateBreaksVcInfo", "buttonTurnOffForTodayText");
            btnKeepThemOnTXT.Text = appSettings.GetConfigStringSafe(btnKeepThemOnTXT.Text, "deactivateBreaksVcInfo", "buttonKeepThemOnText");
            lnkTurnBreaksOffTXT.Text = appSettings.GetConfigStringSafe(lnkTurnBreaksOffTXT.Text, "deactivateBreaksVcInfo", "buttonTurnBreaksOffText");

            bool light = appSettings.sTheme == "Light";
            Background = light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(40, 40, 40));
            TitleText.Foreground = light ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.LightGray);

            Loaded += (s, e) =>
            {
                var area = SystemParameters.WorkArea;
                Left = area.Right - ActualWidth;
                Top = area.Top + 25;
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }

        private void btnKeepThemOn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnTurnOffForToday_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("[user-action] DeactivateBreaks - turn off for today");
            appSettings.CapturePostHog("disable-breaks-for-today");
            RegistryManager.SaveBreaksOffToday(DateTime.Now);
            Close();
        }

        private void lnkTurnBreaksOff_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("[user-action] DeactivateBreaks - turn breaks off completely");
            appSettings.CapturePostHog("settings-changed-disable-breaks");
            appSettings.bAreBreaksEnabled = false;
            app.StopBreakActivityTimer();
            LocalSettingsManager.PutLocalSettings();
            Close();
        }
    }
}
