using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Interaction logic for ConvinceAI.xaml
    /// </summary>
    public partial class ConvinceAI : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public DispatcherTimer dispatcherTimerBlock = new DispatcherTimer(DispatcherPriority.Normal);
        public int nTimer = 30;

        private const int TypingDelay = 500; // Delay in milliseconds after user stops typing
        private DateTime _lastKeyPressTime;
        public string sIntentionToConvinceAI = "";
        public ConvinceAI()
        {
        }
        public ConvinceAI(int nTimerVal = 30, bool bIsApp = false)
        {
            InitializeComponent();

            try
            {
                nTimer = nTimerVal;
                Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["convinceAiTitle"].ToString();
                //App vs URL wording - this popup is shared by the URL and app AI checks.
                Description.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"][bIsApp ? "appConvinceAiDescription" : "convinceAiDescription"].ToString();
                btnConvinceAIText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonConvinceText"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();

                btnConvinceAI.IsEnabled = false; //Disabled initially till user writes anything in intention

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                dispatcherTimerBlock.Tick += new EventHandler(dispatcherTimer_Block);
                dispatcherTimerBlock.Interval = new TimeSpan(0, 0, 1);
                dispatcherTimerBlock.Start();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ConvinceAI () text ex = " + ex.Message);
            }

        }

        private void dispatcherTimer_Block(object sender, EventArgs e)
        {
            nTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                    DialogResult = false;
                    Close();
                }
                else if (nTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerBlock.Stop();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ConvinceAI window dispatcherTimer_Unlock" + ex.Message);
            }

        }


        private void btnConvinceAI_Click(object sender, RoutedEventArgs e)
        {
            sIntentionToConvinceAI = TxtIntention.Text;

            appSettings.AddJustification(sIntentionToConvinceAI); //Add for last 5 justifications
            
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Pause the timer when the user is typing
            dispatcherTimerBlock.Stop();
            _lastKeyPressTime = DateTime.Now;

            if(TxtIntention.Text.Length > 0)
            {
                btnConvinceAI.IsEnabled = true;
            }
            else
            {
                btnConvinceAI.IsEnabled = false;
            }
            // Restart the timer after a delay if no typing happens
            DispatcherTimer restartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TypingDelay)
            };
            restartTimer.Tick += (s, args) =>
            {
                if ((DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= TypingDelay)
                {
                    // Resume the timer after typing delay
                    dispatcherTimerBlock.Start();
                    restartTimer.Stop();
                }
            };
            restartTimer.Start();
        }
    }
}
