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

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingAskProcrastinatingWindow.xaml
    /// </summary>
    public partial class OnboardingAskProcrastinatingWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

         AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingAskProcrastinatingWindow()
        {
            InitializeComponent();

            try
            {
                //todo win texts
                Heading.Text = "Time to do something difficult";
                Description.Text = "What's something you've been procrastinating on? Focus Bear will help you get it done right after this.";

                btnLetsDoIt.IsEnabled = false;

                appSettings.CapturePostHog("show_procrastinating_screen");

                RegistryManager.GetSavedOccupation();

                switch(appSettings.sSavedOccupationForFirstFocusSession)
                {
                    case "University student":
                        {
                            btnFirstTxt.Text = "Start that essay (finally).";
                            btnSecondTxt.Text = "Review lecture notes.";
                            btnThirdTxt.Text = "Take lead in group project.";
                            break;
                        }
                    case "Business Owner":
                        {
                            btnFirstTxt.Text = "Chase overdue invoices.";
                            btnSecondTxt.Text = "Review financials.";
                            btnThirdTxt.Text = "Respond to client emails.";
                            break;
                        }
                    case "Programmer":
                        {
                            btnFirstTxt.Text = "Fix the sneaky bug.";
                            btnSecondTxt.Text = "Refactor that mess.";
                            btnThirdTxt.Text = "Write unit tests (ugh).";
                            break;
                        }
                    case "Graphic designer":
                        {
                            btnFirstTxt.Text = "Finish design.";
                            btnSecondTxt.Text = "Update portfolio.";
                            btnThirdTxt.Text = "Start new concept.";
                            break;
                        }
                    case "Other work from office":
                        {
                            btnFirstTxt.Text = "Clear out inbox.";
                            btnSecondTxt.Text = "Finish the report.";
                            btnThirdTxt.Text = "Declutter your desk.";
                            break;
                        }
                    case "Non desk based work":
                        {
                            btnFirstTxt.Text = "Finish design.";
                            btnSecondTxt.Text = "Update portfolio.";
                            btnThirdTxt.Text = "Start new concept.";
                            break;
                        }
                    case "Employee":
                        {
                            btnFirstTxt.Text = "Update portfolio.";
                            btnSecondTxt.Text = "Clear out inbox.";
                            btnThirdTxt.Text = "Start new concept.";
                            break;
                        }
                    case "Freelancer":
                        {
                            btnFirstTxt.Text = "Finish client project.";
                            btnSecondTxt.Text = "Update portfolio.";
                            btnThirdTxt.Text = "Send proposal.";
                            break;
                        }
                    case "Other":
                        {
                            btnFirstTxt.Text = "Start your tax return.";
                            btnSecondTxt.Text = "Sort through receipts.";
                            btnThirdTxt.Text = "Do a dreaded chore.";
                            break;
                        }
                    default:
                        {
                            btnFirstTxt.Text = "Start that essay (finally).";
                            btnSecondTxt.Text = "Review lecture notes.";
                            btnThirdTxt.Text = "Take lead in group project.";
                            break;
                        }

                }
                btnLetsDoItTxt.Text = "Let's do this!";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskProcrastinatingWindow() EX + " + ex.Message);
            }
            this.Loaded += MainWindow_Loaded;
            App.ThemeChanged += TitleBarThemeChange;
            TitleBarThemeChange();
        }

        private void SetIntentionText(int nNumButtonClicked)
        {
            switch (appSettings.sSavedOccupationForFirstFocusSession)
            {
                case "University student":
                    {
                        if (nNumButtonClicked == 1)
                        {
                            //"studentLongTextFirst": "Finally start that essay that’s been staring at you with judgmental eyes.",
                            TxtIntention.Text = "Finally start that essay that’s been staring at you with judgmental eyes.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            //"studentLongTextSecond": "Wrestle with your lecture notes to see if you can remember anything from last month.",
                            TxtIntention.Text = "Wrestle with your lecture notes to see if you can remember anything from last month.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            //"studentLongTextThird": "Pretend you know what’s going on in your group project and get cracking on research.",
                            TxtIntention.Text = "Pretend you know what’s going on in your group project and get cracking on research.";
                        }

                        break;
                    }
                case "Business Owner":
                    {
                        //"businessOwnerLongTextFirst":  "Tackle those overdue invoices—it’s time to get paid for all your hard work!",
                        //"businessOwnerLongTextSecond":  "Review your financials before the numbers scare you in your sleep.",
                        //"businessOwnerLongTextThird":  "Finally respond to those client emails that have been sitting in your inbox for way too long.",
      
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Tackle those overdue invoices—it’s time to get paid for all your hard work!";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Review your financials before the numbers scare you in your sleep.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Finally respond to those client emails that have been sitting in your inbox for way too long.";
                        }
                        break;
                    }
                case "Programmer":
                    {
                        //"programmerLongTextFirst": "Hunt down that bug that’s been hiding from you like it’s in witness protection.",
                        //"programmerLongTextSecond ": "Face the spaghetti code you created and somehow didn’t regret until now.",
                        //"programmerLongTextThird": "Write those unit tests your future self will thank you for (but probably never run).",
        
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Hunt down that bug that’s been hiding from you like it’s in witness protection.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Face the spaghetti code you created and somehow didn’t regret until now.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Write those unit tests your future self will thank you for (but probably never run).";
                        }
                        break;
                    }
                case "Graphic designer":
                    {
                        //"graphicDesignerLongTextFirst": "Finish the design project that’s been lurking on your desktop like a bad habit.",
                        //"graphicDesignerLongTextSecond": "Update your portfolio before you forget how awesome your old work is.",
                        //"graphicDesignerLongTextThird": "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.",
                        if (nNumButtonClicked == 1)
                        {

                            TxtIntention.Text = "Finish the design project that’s been lurking on your desktop like a bad habit.";
                        }
                        else if (nNumButtonClicked == 2)
                        {

                            TxtIntention.Text = "Update your portfolio before you forget how awesome your old work is.";
                        }
                        else if (nNumButtonClicked == 3)
                        {

                            TxtIntention.Text = "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.";
                        }
                        break;
                    }
                case "Other work from office":
                    {
                        //"officeWorkLongTextFirst": "Attack your inbox like a boss—your future unread emails are watching.",
                        //"officeWorkLongTextSecond": "Write that report before your boss reminds you (again... and again).",
                        //"officeWorkLongTextThird": "Declutter your desk like you’re about to star in an episode of Office Hoarders.",
        
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Attack your inbox like a boss—your future unread emails are watching.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Write that report before your boss reminds you (again... and again).";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Declutter your desk like you’re about to star in an episode of Office Hoarders.";
                        }
                        break;
                    }
                case "Non desk based work":
                    {
                        //"fieldWorkLongTextFirst": "Finish the design project that’s been lurking on your desktop like a bad habit.",
                        //"fieldWorkLongTextSecond": "Update your portfolio before you forget how awesome your old work is.",
                        //"fieldWorkLongTextThird": "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.",
        
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Finish the design project that’s been lurking on your desktop like a bad habit.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Update your portfolio before you forget how awesome your old work is.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.";
                        }
                        break;
                    }
                case "Employee":
                    {
                        //"employeeLongTextFirst": "Update your portfolio before you forget how awesome your old work is.",
                        //"employeeLongTextSecond": "Attack your inbox like a boss—your future unread emails are watching.",
                        //"employeeLongTextThird": "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.",
        
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Update your portfolio before you forget how awesome your old work is.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Attack your inbox like a boss—your future unread emails are watching.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Brainstorm that \"brilliant\" new concept that’ll either be genius or a creative dumpster fire.";
                        }
                        break;
                    }
                case "Freelancer":
                    {
                        //"freelancerLongTextFirst": "Finish that client project you’ve been “almost done with” for days.",
                        //"freelancerLongTextSecond": "Update your portfolio before you forget what awesome work you’ve done.",
                        //"freelancerLongTextThird": "Get around to sending that proposal you’ve been ‘fine-tuning’ forever.",
                        if (nNumButtonClicked == 1)
                        {

                            TxtIntention.Text = "Finish that client project you’ve been “almost done with” for days.";
                        }
                        else if (nNumButtonClicked == 2)
                        {

                            TxtIntention.Text = "Update your portfolio before you forget what awesome work you’ve done.";
                        }
                        else if (nNumButtonClicked == 3)
                        {

                            TxtIntention.Text = "Get around to sending that proposal you’ve been ‘fine-tuning’ forever.";
                        }
                        break;
                    }
                case "Other":
                    {
                        //"genericSuggestionsLongTextFirst": "Start that tax return you’ve been avoiding like it’s a monster under the bed.",
                        //"genericSuggestionsLongTextSecond": "Finally sort through those old receipts you’ve been pretending don’t exist.",
                        //"genericSuggestionsLongTextThird": "Knock out that dreaded 15-minute chore that’s been sitting on your list for days.",
        
                        if (nNumButtonClicked == 1)
                        {
                            TxtIntention.Text = "Start that tax return you’ve been avoiding like it’s a monster under the bed.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            TxtIntention.Text = "Finally sort through those old receipts you’ve been pretending don’t exist.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            TxtIntention.Text = "Knock out that dreaded 15-minute chore that’s been sitting on your list for days.";
                        }
                        break;
                    }
                default:
                    {
                        if (nNumButtonClicked == 1)
                        {
                            //"studentLongTextFirst": "Finally start that essay that’s been staring at you with judgmental eyes.",
                            TxtIntention.Text = "Finally start that essay that’s been staring at you with judgmental eyes.";
                        }
                        else if (nNumButtonClicked == 2)
                        {
                            //"studentLongTextSecond": "Wrestle with your lecture notes to see if you can remember anything from last month.",
                            TxtIntention.Text = "Wrestle with your lecture notes to see if you can remember anything from last month.";
                        }
                        else if (nNumButtonClicked == 3)
                        {
                            //"studentLongTextThird": "Pretend you know what’s going on in your group project and get cracking on research.",
                            TxtIntention.Text = "Pretend you know what’s going on in your group project and get cracking on research.";
                        }

                        break;
                    }

            }
        }

        private void btnFirst_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button_choice1_clicked");
            SetIntentionText(1);
        }

        private void btnSecond_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button_choice2_clicked");
            SetIntentionText(2);
        }

        private void btnThird_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button_choice3_clicked");
            SetIntentionText(3);
        }

        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button_lets_do_this_clicked");
            appSettings.sSavedIntentionFirstFocusSession = TxtIntention.Text;
            //Later after the tray menu is created, the start pomodoro window will be shown from start workflow()    

            Close();
        }

        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(TxtIntention.Text.Length > 0)
            {
                btnLetsDoIt.IsEnabled = true;
            }
            else
            {
                btnLetsDoIt.IsEnabled = false;
            }
        }

        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
        private IntPtr WndProctheme(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            int WM_SETTINGCHANGE = 0x1A;
            if (msg == WM_SETTINGCHANGE)
            {
                // Apply theme when system settings change
                ChangeThemeByWindows();
            }
            return IntPtr.Zero;
        }
        private void ChangeThemeByWindows()
        {
            bool isDarkMode = IsLightTheme();
            string theme = isDarkMode ? "Light" : "Dark";
            LogWriter.LogWrite("NewOnboarding detect theme changed WndProctheme" + theme);
            string selectedTheme = appSettings.sTheme;
            LogWriter.LogWrite("NewOnboarding report autotheme status" + appSettings.bAutoTheme);
            if (isDarkMode == true && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Light";
            }
            else if (isDarkMode == false && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Dark";
            }
            TitleBarThemeChange();
            ((App)Application.Current).ChangeTheme(selectedTheme);
            RegistryManager.SetThemeMode();

        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hook into Windows messages
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProctheme);
            }

            // Apply the correct theme at startup
            ChangeThemeByWindows();
            var isLightTheme = IsLightTheme();
            UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
        }
        private static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }
        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            Version osVersion = Environment.OSVersion.Version;
            bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

            if (isWindows10OrGreater)
            {
                var attribute = 20;
                if (osVersion.Build >= 18985)
                {
                    attribute = 19;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }
    }
}
