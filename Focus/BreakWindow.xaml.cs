using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 300; //Five minutes
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        public BreakWindow()
        {
            InitializeComponent();

            try
            {
                /*     common "focusBreakNotificationTitle":"Your break ends in",*/
                Description.Text = appSettings.ConfigFileStrings["common"]["focusBreakNotificationTitle"].ToString();
                btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowBreakWindowAndStartTimer - " + ex.Message);
            }
            ShowInTaskbar = false;
            this.Topmost = true;
        }
        public void ShowBreakWindowAndStartTimer(int nSeconds, string sTitle = "")
        {
            nTimer = nSeconds;
            if (nTimer == 86400) //Means its an activity with no timer
            {
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
                Description.Text = sTitle;
 
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
        public void HideBreakWindowAndStopTimer()
        {
            dispatcherTimer.Stop();
            Hide();
            TxtTimer.Text = "05:00";
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
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Break Window = " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideBreakWindowAndStopTimer();
        }

        private void btnIAmDone_Click(object sender, RoutedEventArgs e)
        {
            HideBreakWindowAndStopTimer();
            if (((App)(Application.Current)).wndHabits != null /*&& !(((App)(Application.Current)).wndHabits.IsVisible)*/)
            {
                //show the habit window
                ((App)(Application.Current)).wndHabits.Show();
                //CLick the I am done button on the main window
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndHabits.PageHabitActivityOnGoing.btnIAmDone);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

            }
            Close(); //Best to close here and open new when we have the activity with completion requirement.
        }
    }
}
