using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for BreakWarning.xaml
    /// </summary>
    public partial class BreakWarning : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        bool bSecondTime15SecTimerRunning;
        int nTimer = 30;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);


        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        int nMinutesToPostpone = 5;
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

        public BreakWarning()
        {
            InitializeComponent();
            bSecondTime15SecTimerRunning = false;

            nTimer = 30;
           
            ShowInTaskbar = false;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Top + 25);

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

            try
            {
                btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"].ToString();
                Description.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["titleWIN"].ToString();
                PostponeMinsText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["titleSelectMinutes"].ToString();

                FadeInBreakWindow ww = new FadeInBreakWindow();
                ww.Show();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning text setting ex - " + ex.Message);
            }

}

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer-=1;
#else
            int val = nTimer--;
#endif

            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                Progress.Value = nTimer;

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    if (bSecondTime15SecTimerRunning)
                    {
                        dispatcherTimer.Stop();
                        ((App)(Application.Current)).StartBreakActivityTimer(true, 5);
                        Close();
                    }
                    else
                    {
                        TxtTimer.Text = "00:00";
                        dispatcherTimer.Stop();
                        ((App)(Application.Current)).ShowBreakActivities();
                        Close();
                    }
                  
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning = " + ex.Message);
            }

        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (appSettings.nCountBreaksPostponedTimes > 0)
                {
                    if (bSecondTime15SecTimerRunning)
                    {
                        dispatcherTimer.Stop();
                        //User clicked means he wants to do the habits
                        ((App)(Application.Current)).ShowBreakActivities();
                        Close();
                        return;
                    }

                    //Set the break timer fro five minutes from now
                    dispatcherTimer.Stop(); //Stop old timer
                    TxtTimer.Text = "00:15";

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
                        LogWriter.LogWrite("BreakWarning = " + ex.Message);
                    }


                    Description.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["postponeConfirmText"].ToString();
                    btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonTakeBreakText"].ToString();
                    //hide progress
                    Progress.Visibility = Visibility.Collapsed;

                    Grid1.RowDefinitions[0].Height = new GridLength(50);

                    nTimer = 15;

                    bSecondTime15SecTimerRunning = true;

                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();

                    return;
                }
                else
                {
                    //Set the break timer fro five minutes from now
                    dispatcherTimer.Stop();

                    //Now we do not need to show 15 second timer...so easy fix is do not incrase this count
                    
                    ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);

                    Close();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakWarning = " + ex.Message);
            }

        }

        private void ComboBoxMinutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                nMinutesToPostpone = int.Parse(typeItem.Content.ToString());

                btnActionTXT.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonPostponeText"].ToString().Replace("5", nMinutesToPostpone.ToString());

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ComboBoxMinutes_SelectionChanged parse= " + ex.Message);
            }
        }
    }
}
