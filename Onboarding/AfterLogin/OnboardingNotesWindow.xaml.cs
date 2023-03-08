using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingNotesWindow.xaml
    /// </summary>
    public partial class OnboardingNotesWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

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

        int nStep = 1;
        public OnboardingNotesWindow()
        {
            LogWriter.LogWrite("OnboardingNotesWindow()");

            InitializeComponent();
            try
            {
                GridSignupImportantNotes.Visibility = Visibility.Collapsed;
                GridSignupSuccess.Visibility = Visibility.Visible;


                Heading.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["titleSignUpSuccessful"].ToString();
                Description.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["signupSuccessText"].ToString();
                Description1.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["exploreTipsText"].ToString();
                btnNextTXT.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buttonNextText"].ToString();
                btnBackTXT.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buttonBackText"].ToString();
                HeadingImpNote.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["title"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow() EX + " + ex.Message);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nStep--;

                btnBack.IsEnabled = true;
                btnBack.Visibility = Visibility.Visible;

                PrivacyPolicy.Visibility = Visibility.Collapsed;
                NoteImage.Height = double.NaN; //sets the height to auto

                switch (nStep)
                {
                    case 1:
                        GridSignupSuccess.Visibility = Visibility.Visible;
                        GridSignupImportantNotes.Visibility = Visibility.Collapsed;

                        Dot1.IsEnabled = true;
                        Dot2.IsEnabled = false;

                        btnBack.IsEnabled = false;
                        btnBack.Visibility = Visibility.Collapsed;

                        break;
                    case 2:
                        Dot2.IsEnabled = true;
                        Dot3.IsEnabled = false;

                        /*onboardingCardsInfo - "buildingHabitsText":"Building Better Habits",
                           "buildingHabitsDescription":"Focus Bear will help you develop stronger habits in the morning and evening by blocking distractions. But don't worry - if you need to access your computer in a hurry, just click the \"Please spare me\" button in the top right.",
                          */
                        NoteTitle.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buildingHabitsText"].ToString(); //"Building Better Habits";
                        NoteDescription.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buildingHabitsDescription"].ToString(); //"xFocus Bear will help you develop stronger habits in the morning and evening by blocking distractions. But don't worry - if you need to access your computer in a hurry, just click the \"Please spare me\" button in the top right.";

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/cards/betterHabits_card.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();
                        NoteImage.Source = image;

                        break;
                    case 3:
                        Dot3.IsEnabled = true;
                        Dot4.IsEnabled = false;

                        /*onboardingCardsInfo -  "brainBreaksText":"Brain Breaks",
                                "brainBreaksDescription":"To avoid screen headaches and neck strain, Focus Bear will encourage you to take breaks at regular intervals. We give you a 30 second warning so you can postpone the break if it's a bad time. We also detect if you're in a meeting and don't show breaks as that might lead to awkward screensharing moments otherwise!",
                               */
                        NoteTitle.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["brainBreaksText"].ToString(); //"Brain Breaks";
                        NoteDescription.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["brainBreaksDescription"].ToString(); //"xTo avoid screen headaches and neck strain, Focus Bear will encourage you to take breaks at regular intervals. We give you a 30 second warning so you can postpone the break if it's a bad time. We also detect if you're in a meeting and don't show breaks as that might lead to awkward screensharing moments otherwise!";

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/cards/brainBreaks_card.png");
                        var image1 = new BitmapImage();
                        image1.BeginInit();
                        image1.UriSource = new Uri(imagePath1);
                        image1.EndInit();
                        NoteImage.Source = image1;

                        btnNextTXT.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buttonNextText"].ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow btnBack_Click EX + " + ex.Message);
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nStep++;
                btnBack.IsEnabled = true;
                btnBack.Visibility = Visibility.Visible;

                PrivacyPolicy.Visibility = Visibility.Collapsed;
                NoteImage.Height = double.NaN;//sets the height to auto

                switch (nStep)
                {
                    case 2:
                        GridSignupSuccess.Visibility = Visibility.Collapsed;
                        GridSignupImportantNotes.Visibility = Visibility.Visible;
                        /*onboardingCardsInfo - "buildingHabitsText":"Building Better Habits",
                            "buildingHabitsDescription":"Focus Bear will help you develop stronger habits in the morning and evening by blocking distractions. But don't worry - if you need to access your computer in a hurry, just click the \"Please spare me\" button in the top right.",
                           */
                        NoteTitle.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buildingHabitsText"].ToString(); //"Building Better Habits";
                        NoteDescription.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buildingHabitsDescription"].ToString(); //"xFocus Bear will help you develop stronger habits in the morning and evening by blocking distractions. But don't worry - if you need to access your computer in a hurry, just click the \"Please spare me\" button in the top right.";

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/cards/betterHabits_card.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();
                        NoteImage.Source = image;

                        Dot1.IsEnabled = false;
                        Dot2.IsEnabled = true;
                        break;
                    case 3:
                        /*onboardingCardsInfo -  "brainBreaksText":"Brain Breaks",
                                "brainBreaksDescription":"To avoid screen headaches and neck strain, Focus Bear will encourage you to take breaks at regular intervals. We give you a 30 second warning so you can postpone the break if it's a bad time. We also detect if you're in a meeting and don't show breaks as that might lead to awkward screensharing moments otherwise!",
                               */
                        NoteTitle.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["brainBreaksText"].ToString(); //"Brain Breaks";
                        NoteDescription.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["brainBreaksDescription"].ToString(); //"xTo avoid screen headaches and neck strain, Focus Bear will encourage you to take breaks at regular intervals. We give you a 30 second warning so you can postpone the break if it's a bad time. We also detect if you're in a meeting and don't show breaks as that might lead to awkward screensharing moments otherwise!";

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/cards/brainBreaks_card.png");
                        var image1 = new BitmapImage();
                        image1.BeginInit();
                        image1.UriSource = new Uri(imagePath1);
                        image1.EndInit();
                        NoteImage.Source = image1;

                        Dot2.IsEnabled = false;
                        Dot3.IsEnabled = true;
                        break;
                    case 4:
                        Dot3.IsEnabled = false;
                        Dot4.IsEnabled = true;
                        btnNextTXT.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buttonGotItText"].ToString();

                        string imagePath2 = ("pack://application:,,,/Focus Bear;component/images/FBicon-256.png");
                        var image2 = new BitmapImage();
                        image2.BeginInit();
                        image2.UriSource = new Uri(imagePath2);
                        image2.EndInit();
                        NoteImage.Source = image2;
                        NoteImage.Height = 140;

                        /* "privacyText": "Privacy and Security",
                         "privacyDescription":"We take your privacy and security very seriously. We do not log which websites or apps you use. We do sync your habit data to our server but this is only to support cross device distraction blocking. Your data is encrypted and we don't let anyone else see it. for more details or feel free to email team@focusbear.io. Refer to our privacy policy:",
                         "buttonPrivacyText":"Click here to view Privacy Policy"*/
                        NoteTitle.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["privacyText"].ToString(); //"Privacy and Security";
                        NoteDescription.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["privacyDescription"].ToString(); //"xWe take your privacy and security very seriously. We do not log which websites or apps you use. We do sync your habit data to our server but this is only to support cross device distraction blocking. Your data is encrypted and we don't let anyone else see it. for more details or feel free to email team@focusbear.io. Refer to our privacy policy:";
                        btnPrivacyPolicyTXT.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["buttonPrivacyText"].ToString(); //"Click here to view Privacy Policy";
                        PrivacyPolicy.Visibility = Visibility.Visible;

                        
                        break;
                    case 5:
                        Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow btnNext_Click EX + " + ex.Message);
            }
        }

        private void btnPrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnPrivacyPolicy_Click");
            try
            {
                Process.Start("https://www.focusbear.io/en/privacy-policy");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnUpgradeNow_Click + " + ex.Message);
            }
        }
    }
}
