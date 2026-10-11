using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for EasySkipPostpone.xaml
    /// </summary>
    public partial class EasySkipPostpone : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            
            if (appSettings.sTheme == "Light")
            {
                ChangeTheme("Light");
            }
            else
            {
                ChangeTheme("Dark");
            }
        }

        public EasySkipPostpone()
        {
        }
        public EasySkipPostpone(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            LogWriter.LogWrite("EasySkipPostpone() Window Shown");

            this.Owner = _ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["title"].ToString();
                PostponeMinsText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["titleSelectMinutes"].ToString();
                btnPostponeText.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["buttonPostponeText"].ToString();
                FrictionLevel.Text = appSettings.ConfigFileStrings["easyPostponeVcInfo"]["infoText"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();
                ComboBoxMinutes.ToolTip = appSettings.GetConfigStringSafe("Pick a preset or type your own - minimum 1 minute", "easyPostponeVcInfo", "toolTipMinimumMinutes");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }

            //Editable combo box - reject a pasted value the same way typed keystrokes are filtered (#1036).
            DataObject.AddPastingHandler(ComboBoxMinutes, ComboBoxMinutes_Pasting);
        }

        // Digits only - the combo box is editable so the user can either pick a preset or type their
        // own minutes (#1036: simpler than a separate "Custom" item + hidden textbox).
        private void ComboBoxMinutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void ComboBoxMinutes_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)) && ((string)e.DataObject.GetData(typeof(string))).All(char.IsDigit))
                return;

            e.CancelCommand();
        }

        // Correct "0" the moment the user leaves the field (tabs away, clicks Postpone, ...) rather than
        // only at submit time - otherwise the box still visibly reads "0" right when they click Postpone,
        // which reads as "I'm about to postpone for 0 minutes" even though the click handler would have
        // silently corrected it.
        private void ComboBoxMinutes_LostFocus(object sender, RoutedEventArgs e)
        {
            ClampMinutesToMinimum();
        }

        // If the box parses to less than 1 minute, bump it up to 1 and reflect that in the displayed
        // text. Leaves genuinely unparseable text (empty, etc.) alone - that's still caught as an error
        // at submit time in btnPostpone_Click.
        private void ClampMinutesToMinimum()
        {
            if (int.TryParse(ComboBoxMinutes.Text, out int nMinutes) && nMinutes < 1)
            {
                ComboBoxMinutes.Text = "1";
            }
        }

        private void btnPostpone_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("EasyPostpone - btnPostpone_Click");

            try
            {
                //ComboBoxMinutes is editable (#1036) - Text reflects either the selected preset or
                //whatever the user typed, so it's the one source of truth to parse either way. Also
                //belt-and-suspenders clamp here in case LostFocus didn't fire before this click.
                ClampMinutesToMinimum();
                var input = FocusBear.Core.Postpone.PostponeMinutes.Parse(ComboBoxMinutes.Text, out int nMinutesToPostpone);
                if (input == FocusBear.Core.Postpone.PostponeMinutesInput.Invalid)
                {
                    string sMessage = appSettings.GetConfigStringSafe("Enter a valid positive number for minutes", "easyPostponeVcInfo", "customTimeError");
                    FBMessageBox ff = new FBMessageBox(sMessage);
                    ff.Owner = this;
                    ff.ShowDialog();
                    return;
                }
                if (input == FocusBear.Core.Postpone.PostponeMinutesInput.Capped)
                {
                    //Past about 35,791 minutes the timer refused the interval and the postpone silently did
                    //nothing. Show the cap in the box and say so; the user confirms with a second click.
                    ComboBoxMinutes.Text = nMinutesToPostpone.ToString();
                    string sMessage = appSettings.GetConfigStringSafe("You can postpone for up to 24 hours (1440 minutes).", "easyPostponeVcInfo", "customTimeMaxInfo");
                    FBMessageBox ff = new FBMessageBox(sMessage);
                    ff.Owner = this;
                    ff.ShowDialog();
                    return;
                }

                LogWriter.LogWrite("nMinutesToPostpone - " + nMinutesToPostpone.ToString());

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) // FOr Breaks we will simply cancel the current routine.
                {
                    // A break taken during a postponed routine hands the state back to that routine, as finishing
                    // the break does; NONE here let the background tick start the postponed routine at once.
                    ((App)(Application.Current)).RestoreActivityTypeAfterBreak();

                    //simply set the timer for thenext break activity.
                    ((App)(Application.Current)).StartBreakActivityTimer(true, nMinutesToPostpone);

                    ((App)(Application.Current)).ShowBreaksSectionInMainWindow();

                    //enable the left buttons
                    ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();
                }
                else
                {
                    ((App)(Application.Current)).PostponeHabitWork(nMinutesToPostpone);

                    //Now we also need to reset the break timer if habits are postponed with actual break after minutes
                    //((App)(Application.Current)).StartBreakActivityTimer(true, appSettings.breakAfterMinutes);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnPostpone_Click = " + ex.Message);
            }

          Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).EasyPostponeCancelled();
            Close();
        }

        private void ChangeTheme(string theme)
        {

            var source = (HwndSource)PresentationSource.FromVisual(this);

            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }        
    }
}
