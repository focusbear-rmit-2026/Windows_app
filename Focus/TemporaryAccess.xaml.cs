using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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
    /// Interaction logic for TemporaryAccess.xaml
    /// </summary>
    public partial class TemporaryAccess : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        int nAutoBlockTimer = 30; //30 Seconds
        public DispatcherTimer dispatcherTimerAutoBlock = new DispatcherTimer(DispatcherPriority.Normal);
        private DateTime _lastKeyPressTime;
        private const int TypingDelay = 500; // Delay in milliseconds after user stops typing
        string sActiveURL = "";
        public TemporaryAccess(string sActiveURLVal)
        {
            InitializeComponent();

            try
            {
                sActiveURL = sActiveURLVal;

                Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["setIntentionText"].ToString();
                InfoIcon.ToolTip = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["infoTemporaryAllow"].ToString();
                AskNeed.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["titleAskIntentionText"].ToString();
                HowLong.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["titleSelectMinutesToUse"].ToString();
                btnTemporaryAccessText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonSetIntentionText"].ToString();
                btnBlockThisPageText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockUrlText"].ToString();

                TxtTimer.Text = "00:30";
                nAutoBlockTimer = 30;
                dispatcherTimerAutoBlock.Tick += new EventHandler(dispatcherTimer_AutoBlock);
                dispatcherTimerAutoBlock.Interval = new TimeSpan(0, 0, 1);
                dispatcherTimerAutoBlock.Start();
                CommandManager.InvalidateRequerySuggested();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TemporaryAccess texts ex - " + ex.Message);
            }
        }

        private void dispatcherTimer_AutoBlock(object sender, EventArgs e)
        {
            nAutoBlockTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nAutoBlockTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nAutoBlockTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                }
                if (nAutoBlockTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerAutoBlock.Stop();

                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlockThisPage);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();

                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimerAutoBlock.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerAutoBlock);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimerAutoBlock.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("TemporaryAccess dispatcherTimerAutoBlock remove event ex = " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TemporaryAccess dispatcherTimerAutoBlock ex-" + ex.Message);
            }

        }

        private void btnTemporaryAccess_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimerAutoBlock.Stop();

            double nMinutesToUseSite = 0; //treat this as 30 seconds or half a minute

            appSettings.GetTotalTimeForDistractingSitesToday();//we get this is to make sure if the app is contineously running, date changes.

            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                string nSelectedMinutes = typeItem.Content.ToString();
                if (nSelectedMinutes == "30s")
                {
                    nMinutesToUseSite = 0.5;
                }
                else
                {
                    nSelectedMinutes = nSelectedMinutes.Replace("m", ""); //remove m
                    nMinutesToUseSite = double.Parse(nSelectedMinutes);
                }

                LogWriter.LogWrite("btnTemporaryAccess_Click nMinutesTouse - " + nMinutesToUseSite.ToString());

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnTemporaryAccess_Click getting minutes from comboxbox ex - " + ex.Message);
            }

            //we may be coming from set intetion for the second time after double confirmation, we need to check if the same url exists in the set intention array, we will need to replace that with new intention and new end time
            var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
            if (allowedURLs.Count() != 0)
            {
                //found in allowed url, remove it
                appSettings.TemporaryAllowedURLsWithSetIntention.Remove(allowedURLs.First());
            }
            //Inserting at 0, as we checkfor the first element of the found itmes.
            appSettings.TemporaryAllowedURLsWithSetIntention.Insert(0, new DistractingURLAllowedWithIntention(sActiveURL.ToLower(), TxtIntention.Text, DateTime.Now.AddMinutes(nMinutesToUseSite), nMinutesToUseSite.ToString(), true));

            DialogResult = true;
            Close();
        }

        private void btnBlockThisPage_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtIntention.Text.Trim().Length != 0)
            {
                btnTemporaryAccess.IsEnabled = true;
            }
            else
            {
                btnTemporaryAccess.IsEnabled = false;
            }

            // Pause the timer when the user is typing
            dispatcherTimerAutoBlock.Stop();
            _lastKeyPressTime = DateTime.Now;

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
                    dispatcherTimerAutoBlock.Start();
                    restartTimer.Stop();
                }
            };
            restartTimer.Start();

        }
    }
}
