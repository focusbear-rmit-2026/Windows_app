using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Quit
{
    /// <summary>
    /// Interaction logic for QuitWindow.xaml
    /// </summary>
    public partial class QuitWindow : Window
    {
        /// <summary>The app-quit event's fields, escaped: typed feedback with a quote or line break used to break the fragment.</summary>
        internal static string QuitMetadata(string reason, DateTime installed, string feedback)
        {
            return FocusBear.Core.Analytics.EventPayload.Members(
                new System.Collections.Generic.KeyValuePair<string, object>("quitReason", reason ?? ""),
                new System.Collections.Generic.KeyValuePair<string, object>("usageStartDate", installed.ToString("o")),
                new System.Collections.Generic.KeyValuePair<string, object>("feedback", feedback ?? ""));
        }

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        int nTimer = 2;  
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bEmergencyTimer = false;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public QuitWindow()
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
            App.ThemeChanged += TitleBarThemeChange;

            // Attach the countdown Tick handler here, synchronously, instead of inside the delayed
            // TurnOffCompletely() (reached via InitializeAsync's 1-second await). btnQuit_Click starts
            // this same dispatcherTimer independently via StartShutdownCountdown() - if the user clicks
            // "Nope, just quit" before that delayed continuation has run, the timer was starting with no
            // Tick handler attached at all, so it ticked forever doing nothing: nTimer never decremented,
            // Application.Current.Shutdown() was never reached, and the popup looked completely frozen
            // (#1190 - confirmed from a real user's logs: TurnOffCompletely() never logged for that
            // click). Subscribing before the window can even be shown/clicked removes the race entirely.
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            this.Topmost = true;
           
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingTitle"].ToString();
                
                btnTurnOfCompletelyTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["askQuitVcInfo"]["buttonQuitText"].ToString();
                btnLetMeDoMyHabitsTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingCountDownInfo"]["buttonQuitCancelText"].ToString();

                ShareWhyQuit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["subTitle"].ToString();
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_0"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString());

                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["title"].ToString());
                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["title"].ToString());

                ComboBoxReason.Items.Add(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString());
                ComboBoxReason.SelectedIndex = 0;

                btnQuitTXT.Text = appSettings?.ConfigFileStrings?["quitInfo"]?["quittingConfirmInfo"]?["buttonQuitInitialText"]?.ToString() ?? "Nope - just quit";
                btnQuit.IsEnabled = true; //Can we make feedback non mandatory please? https://github.com/Focus-Bear/windows-app-v2/issues/1043

                //this will ask the password if set,
                _ = InitializeAsync();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Quit Window set texts ex = " + ex.Message);
            }
        }
        private async Task InitializeAsync()
        {
            await Task.Delay(1000);
            TurnOffCompletely();
        }
        private void TurnOffCompletely()
        {
            LogWriter.LogWrite("TurnOffCompletely()");

            //If a password is set, ask fopr a password
            if (appSettings.isPasswordSet)
            {
                if (appSettings.bPasswordRequireInQuitApp)
                { 
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = this;
                    GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    GridShade.Visibility = Visibility.Collapsed;

                    if (bResult.Value)
                    {
                        //Password is correct
                    }
                    else
                    {
                        Close();
                        return;
                    }

                }
            }
            TxtFinalTimer.Text = "00:02";
            //start the timer for 10 seconds
            nTimer = 2;
            CommandManager.InvalidateRequerySuggested();
        }

        // Teacher Mode: during an active class focus session, only the "emergency" reason may quit the
        // app. Every other reason is blocked with the locked alert. A determined student can still
        // force-quit via Task Manager (Ctrl+Alt+Del), so the emergency path is kept as a deliberate
        // escape hatch rather than pretending to be an unbreakable lock.
        private bool IsQuitBlockedByTeacher()
        {
            if (!TeacherSessionManager.Instance.IsTeacherSessionActive()
                || TeacherSessionManager.Instance.CanTeacherSessionFinishNow())
            {
                return false;
            }

            try
            {
                string sEmergency = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString();
                bool bIsEmergency = ComboBoxReason.SelectedItem != null
                    && ComboBoxReason.SelectedItem.ToString() == sEmergency;
                return !bIsEmergency; // emergency is always allowed; everything else is blocked
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("QuitWindow IsQuitBlockedByTeacher ex - " + ex.Message);
                return false; // never trap the user because of a config lookup failure
            }
        }

        // Starts the shutdown countdown, unless a teacher class session blocks this (non-emergency) quit.
        private void StartShutdownCountdown()
        {
            if (IsQuitBlockedByTeacher())
            {
                LogWriter.LogWrite("QuitWindow - non-emergency quit blocked while a teacher class session is active");
                TeacherSessionManager.Instance.ShowTeacherSessionLockedAlert();
                // Return the window to its initial state so the student can still pick "emergency".
                dispatcherTimer.Stop();
                bEmergencyTimer = false;
                ComboBoxReason.IsEnabled = true;
                btnQuit.IsEnabled = true;
                TxtFinalTimer.Visibility = Visibility.Collapsed;
                ComboBoxReason.SelectedIndex = 0;
                return;
            }
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {
                TxtFinalTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    TxtFinalTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    Application.Current.Shutdown(0);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Quit Window Timer= " + ex.Message);
            }

        }

        private void btnLetMeDoMyHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnLetMeDoMyHabits_Click");
            dispatcherTimer.Stop();
            Close();
        }

        private void ComboBoxReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_0"]["title"].ToString())
            {
                ReasonTitle.Visibility = Visibility.Collapsed;
                TxtExplanation.Visibility = Visibility.Collapsed;
                TxtFinalTimer.Visibility = Visibility.Collapsed;
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_1"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;
                string sMetadata = QuitMetadata(ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT, TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                StartShutdownCountdown();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
                btnQuitTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownMeeting"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = QuitMetadata(ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT, TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                StartShutdownCountdown();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownSomeOneElse"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = QuitMetadata(ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT, TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                StartShutdownCountdown();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["title"].ToString()) //annoying
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownAnnoying"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
                btnQuitTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["title"].ToString())
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDownRelax"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;

                ComboBoxReason.IsEnabled = false;
                TxtExplanation.Visibility = Visibility.Collapsed;

                bEmergencyTimer = true;
                nTimer = 2;

                string sMetadata = QuitMetadata(ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT, TxtExplanation.Text);
                appSettings.CapturePostHog("app-quit", sMetadata);

                StartShutdownCountdown();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString()) //Something else
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["description"].ToString();
                ReasonTitle.Visibility = Visibility.Visible;
                TxtExplanation.Visibility = Visibility.Visible;
                btnQuitTXT.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
            }
        }

        private async void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("QuitWindow - btnQuit_Click");
            btnQuit.IsEnabled = false;

            string sMetadata = QuitMetadata(ComboBoxReason.SelectedValue.ToString(), appSettings.FirstAppInstallDT, TxtExplanation.Text);
            appSettings.CapturePostHog("app-quit", sMetadata);

            ReasonTitle.Text = "";

            if ( ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString()) //app broken
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["finalDescription"].ToString();
            }
            else if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["title"].ToString()) //something else
            {
                ReasonTitle.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_3"]["finalDescription"].ToString();
            }
            ReasonTitle.Visibility = Visibility.Visible;

            ComboBoxReason.IsEnabled = false;
            TxtExplanation.Visibility = Visibility.Collapsed;

            if (ComboBoxReason.SelectedItem.ToString() == appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["dropDown_2"]["title"].ToString()) //app broken, we will need to post the logs if the user wants
            {
                FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["common"]["alertUploadLogText"].ToString() , "", MessageBoxButton.YesNo);  
                messageBox.Owner = this;
                messageBox.ShowDialog();
                if (messageBox.btnClicked == MessageBoxResult.Yes)
                {
                    this.IsEnabled = false;
                    bool xResponseUnused = await UploadLogs.UploadLogsToServer(TxtExplanation.Text);
                    this.IsEnabled = true;

                    //we will hide the window here
                    Hide();
                }

                //25-May-2023 - WE will not be showing the timer but easy fix is having 1 second invisible timer.
                TxtFinalTimer.Visibility = Visibility.Collapsed;
                bEmergencyTimer = true;
                nTimer = 1;
                StartShutdownCountdown();
            }
            else
            {
                //25-May-2023 - WE will not be showing the timer but easy fix is having 1 second invisible timer.
                TxtFinalTimer.Visibility = Visibility.Collapsed;
                bEmergencyTimer = true;
                nTimer = 1;
                StartShutdownCountdown();
            }
              
        }

        
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                //Compute how many logical units the screen can show
                double screenLogicalWidth = SystemParameters.WorkArea.Width ;
                double screenLogicalHeight = SystemParameters.WorkArea.Height;

                // “ideal” logical minimum
                const double idealMinW = 1165, idealMinH = 650;

                //Cap the minimum to what actually fits
                this.MinWidth = Math.Min(idealMinW, screenLogicalWidth);
                this.MinHeight = Math.Min(idealMinH, screenLogicalHeight);

                // If you want the window to start at that minimum size:
                this.Width = this.MinWidth;
                this.Height = this.MinHeight;


                //Center it
                this.Left = Math.Max(0, (screenLogicalWidth - this.Width) / 2);
                this.Top = Math.Max(0, (screenLogicalHeight - this.Height) / 2);
            }
        }
    }
}
