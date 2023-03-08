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
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for DiscourageDefault.xaml
    /// </summary>
    public partial class DiscourageDefault : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        int nTimer = 5;
        DispatcherTimer dispatcherTimerUnlock = new DispatcherTimer(DispatcherPriority.Normal);
        public DiscourageDefault()
        {
        }
        public DiscourageDefault(StartFocusMode _ParentWindow)
        {
            InitializeComponent();

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;


            /*  "focusInfo":{
             *  "discourageDefaultFocusInfo":{
            "title":"Are you sure? It's pretty easy to get distracted without using a more restrictive focus mode",
            "buttonCancelText":"Bah I guess you're right - CANCEL!",
            "buttonShutUpText":"Shutup I know what I'm doing",
            "titleGoodLuck":"Ok good luck! (You'll need it 😅)"
        },
             */
            Heading.Text = appSettings.ConfigFileStrings["focusInfo"]["discourageDefaultFocusInfo"]["title"].ToString(); ;
            btnCancelTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["discourageDefaultFocusInfo"]["buttonCancelText"].ToString(); ;
            btnShutupTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["discourageDefaultFocusInfo"]["buttonShutUpText"].ToString(); ;

        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnShutup_Click(object sender, RoutedEventArgs e)
        {
            Heading.Text = appSettings.ConfigFileStrings["focusInfo"]["discourageDefaultFocusInfo"]["titleGoodLuck"].ToString();
            buttons.Visibility = Visibility.Collapsed;
            TxtTimer.Visibility = Visibility.Visible;
            TxtTimer.Text = "00:05";

            nTimer = 5;
            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
            dispatcherTimerUnlock.Tick += new EventHandler(dispatcherTimer_Unlock);
            dispatcherTimerUnlock.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerUnlock.Start();
            CommandManager.InvalidateRequerySuggested();

        }

        private void dispatcherTimer_Unlock(object sender, EventArgs e)
        {
            int val = nTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                }
                else if (nTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerUnlock.Stop();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DiscourageDefault dispatcherTimer_Unlock - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

    }       
}
