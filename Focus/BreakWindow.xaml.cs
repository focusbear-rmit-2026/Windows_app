using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for BreakWindow.xaml
    /// </summary>
    public partial class BreakWindow : Window
    {   
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        App app = (App)Application.Current;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            //Set the window style to noactivate.
            var helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            
        }


        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        
        public bool bShowingForNonTimerActivities = false;
        bool bIamDoneTypeActivity = false;
        public BreakWindow()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    this.Height = 151;
                }
            }
            else
            {
                this.Height = 145;
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                Description.Text = appSettings.ConfigFileStrings["common"]["focusBreakNotificationTitle"].ToString();
                btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();

                btnSkipTXT.Text = appSettings.ConfigFileStrings["customRoutineTimeVcInfo"]["buttonSkipText"].ToString();
                btnPostponeTXT.Text = appSettings.ConfigFileStrings["customRoutineTimeVcInfo"]["buttonPostponeText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowBreakWindowAndStartTimer - " + ex.Message);
            }
            ShowInTaskbar = false;
            this.Topmost = true;
            App.ThemeChanged += TitleBarThemeChange;
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }
        public void ShowBreakWindowAndStartTimer(int nSeconds, string sTitle = "")
        {
            nTimer = nSeconds;
            TxtTimer.Visibility = Visibility.Visible;
            bIamDoneTypeActivity = false;
            if (appSettings.bHabitwithUserActionNonTimerRunning) //Means its an activity with no timer
            {
                bIamDoneTypeActivity = true;

                TxtTimer.Visibility = Visibility.Collapsed;
                
                UseTimerRelated.Visibility = Visibility.Collapsed;
                DoNotUseTimerRelated.Visibility = Visibility.Visible;

                if (sTitle.Length > 0) //Set the activity title if thelength is > 0
                    DescriptionNoTimer.Text = sTitle;
            }
            else
            {
                UseTimerRelated.Visibility = Visibility.Visible;
                DoNotUseTimerRelated.Visibility = Visibility.Collapsed;
            }

            if (sTitle.Length > 0) //Set the activity title if thelength is > 0
            {
                Description.Text = sTitle;
                SkipPostponeSection.Visibility = Visibility.Visible;//Show skip postpone buttons
            }
            else //break timing
            {
                Description.Text = appSettings.ConfigFileStrings["common"]["focusBreakNotificationTitle"].ToString();// default your breaks ends in
                SkipPostponeSection.Visibility = Visibility.Collapsed; //Hide skip postpone buttons
            }

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 25);

            //Remove the previous event handler so that new can be set
            try
            {
                var eventField = dispatcherTimer.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimer);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimer.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowBreakWindowAndStartTimer - " + ex.Message);
            }

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
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

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
  
                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    Hide();

                    //if (appSettings.bUseSuperPomodoroMode)
                    //We will need to start the focus mode again after the focus session and X min break is over
                    if(!appSettings.bHabitwithAllowedAppsRunning && !bIamDoneTypeActivity ) //make sure break window is not shown for habits with allowed apps
                    {

                        if (appSettings.bUseSuperPomodoroMode) //If super pomodoro is still running we keep the title as Stop Super focus session
                            ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopSuperFocusSession"].ToString());
                        else
                            ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartFocusSession"].ToString());
             
                        //Removing the super pomodoro check, we need to show start focus mode window after the break ends.
                        //(The tray Start button now opens an inline picker, so open the focus window directly.)
                        {
                            if (appSettings.bIsGeekModeEnabled)
                                ((App)(Application.Current)).ShowPomodoroWindow();
                            else
                                ((App)(Application.Current)).ShowSimplePomodoroWindow();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Break Window = " + ex.Message);
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (bShowingForNonTimerActivities)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            dispatcherTimer.Stop();
            ((App)(Application.Current)).wndTakeABreak = null;
        }

        private void btnIAmDone_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            if (((App)(Application.Current)).wndFocusBearMainWindow != null )
            {
                //Select the habits tab
                ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);

                //CLick the I am done button on the main window
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnIAmDone);//V2
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

            }
            bShowingForNonTimerActivities = false;
            Close(); //Best to close here and open new when we have the activity with completion requirement.
            ((App)(Application.Current)).wndTakeABreak = null;
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            
           
            ((App)(Application.Current)).ShowHabitsSectionInMainWindow();

            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnPausePlay);//V2
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

            Hyperlink link = ((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnSkipThisHabit;   //  Hyperlink control
            var peerH = new HyperlinkAutomationPeer(link);
            var invokeProvH = peerH.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProvH?.Invoke();

            Close(); //Best to close here and open new when we have the activity with completion requirement.
            ((App)(Application.Current)).wndTakeABreak = null;
        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).ShowHabitsSectionInMainWindow();

            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.btnPleaseSpareMe); 
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

            Close(); //Best to close here and open new when we have the activity with completion requirement.
            ((App)(Application.Current)).wndTakeABreak = null;

        }
    }
}
