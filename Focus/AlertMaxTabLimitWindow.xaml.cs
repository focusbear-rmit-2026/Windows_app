using Focus_Bear.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Alert window displayed when the user's browser tab count exceeds the configured limit.
    /// Counts down 15 seconds and auto-closes excess tabs if the user does not act.
    /// Also auto-dismisses if the user closes tabs manually before the countdown ends.
    /// User cannot dismiss the alert; the window only closes when tabs are closed.
    /// </summary>
    public partial class AlertMaxTabLimitWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private readonly int _nMaxTabLimit;
        private readonly Func<int> _fnGetTabCount;
        private readonly Action<int> _fnCloseExcess;

        private int _nCountdown = 15;
        private int _nCheckTick = 0;
        private DispatcherTimer _countdownTimer;
        private bool _allowClose = false;

        public AlertMaxTabLimitWindow(int nCurrentTabCount, int nMaxTabLimit,
                                      Func<int> fnGetTabCount, Action<int> fnCloseExcess)
        {
            InitializeComponent();

            _nMaxTabLimit  = nMaxTabLimit;
            _fnGetTabCount = fnGetTabCount;
            _fnCloseExcess = fnCloseExcess;

            try
            {
                string sHeading = appSettings.ConfigFileStrings["alertMaxTabLimitVcInfo"]["title"].ToString();
                sHeading = sHeading.Replace("<COUNT>", nMaxTabLimit.ToString());
                Heading.Text = sHeading;

                TabsCountLabel.Text = appSettings.ConfigFileStrings["alertMaxTabLimitVcInfo"]["titleTabsCount"].ToString();
                TabsCount.Text = nCurrentTabCount.ToString();

                if (appSettings.ConfigFileStrings["alertMaxTabLimitVcInfo"]["buttonCloseText"] != null)
                    btnOpenBrowserText.Text = appSettings.ConfigFileStrings["alertMaxTabLimitVcInfo"]["buttonCloseText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlertMaxTabLimitWindow() ex - " + ex.Message);

                Heading.Text = $"Oops, you have a limit of {nMaxTabLimit} tabs";
                TabsCountLabel.Text = "Number of open tabs:";
                TabsCount.Text = nCurrentTabCount.ToString();
                btnOpenBrowserText.Text = "OK";
            }

            UpdateCountdownLabel();

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();

            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Prevent user from closing the window manually by clicking the X
            // The window will only close when tabs are closed or countdown completes
            if (!_allowClose)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

        private async void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _nCountdown--;
            _nCheckTick++;

            // Every 3 seconds check if user already closed enough tabs
            if (_nCheckTick >= 3)
            {
                _nCheckTick = 0;
                try
                {
                    int nCurrent = await Task.Run(() => _fnGetTabCount());
                    TabsCount.Text = nCurrent.ToString();
                    if (nCurrent <= _nMaxTabLimit)
                    {
                        LogWriter.LogWrite("AlertMaxTabLimitWindow - tab count dropped below limit, auto-dismissing");
                        _countdownTimer.Stop();
                        _allowClose = true;
                        Close();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("AlertMaxTabLimitWindow CountdownTimer_Tick getCount ex - " + ex.Message);
                }
            }

            if (_nCountdown <= 0)
            {
                _countdownTimer.Stop();
                LogWriter.LogWrite("AlertMaxTabLimitWindow - countdown reached 0, closing excess tabs");
                try
                {
                    await Task.Run(() => _fnCloseExcess(_nMaxTabLimit));
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("AlertMaxTabLimitWindow - CloseExcess ex: " + ex.Message);
                }
                _allowClose = true;
                Close();
                return;
            }

            UpdateCountdownLabel();
        }

        private void UpdateCountdownLabel()
        {
            CountdownLabel.Text = $"Closing in {_nCountdown} seconds (or close an existing tab)";
        }

        private void Page_Theme()
        {
            try
            {
                bool isDark = appSettings.sTheme == "Dark";
                if (isDark && Application.Current.Resources["DarkBackground"] != null)
                    MainGrid.Background = Application.Current.Resources["DarkBackground"] as System.Windows.Media.Brush;
                else if (!isDark)
                    MainGrid.Background = System.Windows.Media.Brushes.White;
            }
            catch { }
        }

        private async void btnOpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AlertMaxTabLimitWindow - OK button clicked: closing excess tabs");
            _countdownTimer?.Stop();
            try
            {
                await Task.Run(() => _fnCloseExcess(_nMaxTabLimit));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlertMaxTabLimitWindow btnOpenBrowser_Click ex - " + ex.Message);
            }
            _allowClose = true;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            App.ThemeChanged -= Page_Theme;
            base.OnClosed(e);
        }
    }
}
