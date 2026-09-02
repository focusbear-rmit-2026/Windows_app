using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Teacher Mode PACT invite - a two-screen card mirroring the Mac app's TeacherPactInviteView
    /// (Mac-App#2377). Screen A invites the student to activate the PACT; Screen B adds friction before
    /// declining: a required free-text reason plus a hold-for-15-seconds button, so declining is a
    /// deliberate act. The reason is reported to the teacher via DeclineClassSession(reason).
    /// </summary>
    public partial class TeacherPactInviteWindow : Window
    {
        //greys out the native close (X) button - same approach as SetIntention/GentleBlock/BreakWarning/
        //EasySkipPostpone/BlockingScheduleStrictTimer: this popup must be answered via its own buttons.
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;

        private const int HoldSecondsToDecline = 15;
        private const double HoldTickMilliseconds = 50;

        private readonly Action _onActivate;
        private readonly Action<string> _onDecline;

        private readonly string _sKeepHoldingTemplate;

        private readonly DispatcherTimer _holdTimer;
        private DateTime _holdStartTime;
        private bool _bHolding = false;
        private bool _bDeclined = false;

        public TeacherPactInviteWindow(string sSubtitle, string sEndsLine, string sInviteBody,
            List<string> allowedSites, Action onActivate, Action<string> onDecline)
        {
            InitializeComponent();

            _onActivate = onActivate;
            _onDecline = onDecline;

            AppSettings appSettings = AppSettings.GetInstance;
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            TeacherSessionManager manager = TeacherSessionManager.Instance;
            _sKeepHoldingTemplate = manager.GetLocalizedString("pactKeepHolding", "Keep holding, <N>s");

            try
            {
                // Screen A
                InviteTitle.Text = manager.GetLocalizedString("pactInviteTitle", "Your teacher invited you to join a PACT session");
                YouCanUseHeader.Text = manager.GetLocalizedString("pactYouCanUseHeader", "YOU CAN USE");
                InviteBody.Text = sInviteBody;
                btnActivateTxt.Text = manager.GetLocalizedString("pactActivateButton", "Activate the PACT");
                lnkDeclineTxt.Text = manager.GetLocalizedString("pactDeclineLink", "Decline");

                SetOptionalText(SubtitleText, sSubtitle);
                SetOptionalText(EndsText, sEndsLine);

                if (allowedSites != null && allowedSites.Count > 0)
                {
                    AllowedSitesList.ItemsSource = allowedSites;
                }
                else
                {
                    YouCanUseHeader.Visibility = Visibility.Collapsed;
                    AllowedSitesList.Visibility = Visibility.Collapsed;
                }

                // Screen B
                BeforeDeclineTitle.Text = manager.GetLocalizedString("pactBeforeDeclineTitle", "Before you decline");
                BeforeDeclineBody.Text = manager.GetLocalizedString("pactBeforeDeclineBody", "Declining is allowed but it will be reported to your teacher. Once in a while is genuinely fine, nobody is going to chase you over one lesson.");
                ReportNoteText.Text = manager.GetLocalizedString("pactDeclineReportNote", "If you decline most lessons, it shows up in your teacher's report and they will want a conversation about how the laptop is going for you.");
                WhyDecliningLabel.Text = manager.GetLocalizedString("pactWhyDecliningLabel", "WHY ARE YOU DECLINING?");
                ReasonHelper.Text = manager.GetLocalizedString("pactReasonHelper", "Your teacher will see this alongside your name.");
                TextReason.ToolTip = manager.GetLocalizedString("pactReasonPlaceholder", "Type your reason here...");
                MindfulNoteText.Text = manager.GetLocalizedString("pactMindfulNote", "Please be mindful with how you use your laptop. Research shows that students who play games, watch videos and so on distract their classmates as well.");
                HoldButtonTxt.Text = manager.GetLocalizedString("pactHoldToDecline", "Hold for 15 seconds to decline");
                lnkBackToJoinTxt.Text = manager.GetLocalizedString("pactBackToJoinLink", "Actually, I'll join");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherPactInviteWindow text ex - " + ex.Message);
            }

            // The hold button only arms once a reason has been typed.
            SetHoldEnabled(false);

            _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldTickMilliseconds) };
            _holdTimer.Tick += HoldTimer_Tick;

            //if the window is closed from outside mid-hold (e.g. the teacher ends the session), the timer
            //must not keep ticking - otherwise it can still fire CompleteDecline() after Close() and report
            //a decline for a session that's already gone
            Closed += (s, e) => _holdTimer.Stop();

            //a Border's CornerRadius only rounds its own fill - a child Grid clips to a plain rectangle, so
            //the progress bar's square corners were poking past the button's rounded edge. Give the clipping
            //Grid an actual rounded Clip geometry that tracks the button's real size.
            HoldButtonClip.SizeChanged += (s, e) =>
            {
                HoldButtonClip.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 6, 6);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var source = (HwndSource)PresentationSource.FromVisual(this);
                ((App)Application.Current).UseImmersiveDarkMode(source.Handle, AppSettings.GetInstance.sTheme != "Light");

                var hWnd = new WindowInteropHelper(this);
                var sysMenu = GetSystemMenu(hWnd.Handle, false);
                EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TeacherPactInviteWindow OnSourceInitialized ex - " + ex.Message);
            }
        }

        private static void SetOptionalText(System.Windows.Controls.TextBlock target, string sText)
        {
            if (string.IsNullOrWhiteSpace(sText))
            {
                target.Visibility = Visibility.Collapsed;
            }
            else
            {
                target.Text = sText;
            }
        }

        private void btnActivate_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("TeacherPactInviteWindow - activate clicked");
            _onActivate?.Invoke();
            Close();
        }

        private void lnkDecline_Click(object sender, RoutedEventArgs e)
        {
            InvitePanel.Visibility = Visibility.Collapsed;
            DeclinePanel.Visibility = Visibility.Visible;
        }

        private void lnkBackToJoin_Click(object sender, RoutedEventArgs e)
        {
            StopHold();
            DeclinePanel.Visibility = Visibility.Collapsed;
            InvitePanel.Visibility = Visibility.Visible;
        }

        private void TextReason_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SetHoldEnabled(TextReason.Text.Trim().Length > 0);
        }

        //The button stays orange (its Background/BorderBrush set once in XAML) regardless of enabled
        //state - only IsEnabled and the text's own opacity change. Do NOT dim the whole Border via
        //Opacity: that blends with whatever is BEHIND the window, and over the dark theme's near-black
        //background a transparent light-orange button turns muddy near-black.
        private void SetHoldEnabled(bool bEnabled)
        {
            HoldButton.IsEnabled = bEnabled;
            HoldButtonTxt.Opacity = bEnabled ? 1.0 : 0.6;
        }

        //mouse capture makes the hold robust to small hand tremor over 15s - without it, the pointer
        //drifting a pixel off the button mid-hold fired MouseLeave and reset all progress, which made
        //the button feel broken. With capture, only an actual release (anywhere) cancels the hold.
        private void HoldButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!HoldButton.IsEnabled || TextReason.Text.Trim().Length == 0 || _bDeclined)
            {
                return;
            }
            _bHolding = true;
            _holdStartTime = DateTime.Now;
            HoldButton.CaptureMouse();
            MindfulNoteBox.Visibility = Visibility.Visible; //reveal the mindfulness note while holding
            HoldCountdownText.Visibility = Visibility.Visible;
            _holdTimer.Start();
        }

        private void HoldButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StopHold();
        }

        //capture can be lost involuntarily (alt-tab, a system dialog stealing it) without a MouseUp ever
        //firing - without this, _bHolding/the timer would keep running in the background and could still
        //auto-decline later even though the user let go
        private void HoldButton_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StopHold();
        }

        //releasing before the 15s completes resets the progress
        private void StopHold()
        {
            if (!_bHolding)
            {
                return;
            }
            _bHolding = false;
            _holdTimer.Stop();
            HoldButton.ReleaseMouseCapture();
            HoldProgress.Width = 0;
            HoldCountdownText.Visibility = Visibility.Collapsed;
            MindfulNoteBox.Visibility = Visibility.Collapsed;
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            double elapsedSeconds = (DateTime.Now - _holdStartTime).TotalSeconds;
            double fraction = Math.Max(0, Math.Min(1, elapsedSeconds / HoldSecondsToDecline));

            HoldProgress.Width = fraction * HoldButton.ActualWidth;

            int nRemaining = (int)Math.Ceiling(HoldSecondsToDecline - elapsedSeconds);
            if (nRemaining < 0)
            {
                nRemaining = 0;
            }
            HoldCountdownText.Text = _sKeepHoldingTemplate.Replace("<N>", nRemaining.ToString());

            if (fraction >= 1)
            {
                CompleteDecline();
            }
        }

        private void CompleteDecline()
        {
            if (_bDeclined)
            {
                return;
            }
            _bDeclined = true;
            _bHolding = false;
            _holdTimer.Stop();
            HoldButton.ReleaseMouseCapture();

            string sReason = TextReason.Text.Trim();
            LogWriter.LogWrite("TeacherPactInviteWindow - decline confirmed after hold");
            _onDecline?.Invoke(sReason);
            Close();
        }
    }
}
