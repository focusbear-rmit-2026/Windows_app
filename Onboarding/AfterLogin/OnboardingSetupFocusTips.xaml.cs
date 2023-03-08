using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingSetupFocusTips.xaml
    /// </summary>
    public partial class OnboardingSetupFocusTips : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        OnboardingSetupAndTipsWindow _parentWindow;
        int nStep = 1;
        public OnboardingSetupFocusTips()
        {
        }
        public OnboardingSetupFocusTips(OnboardingSetupAndTipsWindow parentWindow)
        {
            InitializeComponent();

            LogWriter.LogWrite("OnboardingSetupFocusTips screen");
            _parentWindow = parentWindow;

            try
            {
                HeadingAllSetup.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["title"].ToString();

                //initial
                /* focusTipsVcInfo =  "tip1title": "Tip 1:",
                            "tip1Text":"You can try it out now by clicking the Play icon next to your new focus mode in the list that appears on clicking the top menu icon.",,*/
                TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1title"].ToString();
                TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1Text"].ToString();

                string imagePath = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_1.png");
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                TipImage.Source = image;

                //    "buttonBackText":"Back",
                //"buttonNextText":"Next",
                btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonNextText"].ToString();
                btnBackTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonBackText"].ToString();

                /* "howHeardVcInfo": {
                     "title": "How did you hear about Focus Bear?",
             "buttonContinueText":"Continue",
             "optionFirstText":"Friend recommended it",
             "optionSecondText":"Google",
             "optionThirdText":"Ad on Reddit or Twitter",
             "optionFourthText":"Somewhere else"*/

                HeadingHear.Text = appSettings.ConfigFileStrings["howHeardVcInfo"]["title"].ToString();
                btnContinueTxt.Text = appSettings.ConfigFileStrings["howHeardVcInfo"]["buttonContinueText"].ToString();
                btnContinue.IsEnabled = false;
                                    
        
                var arrOptions = new string[] { appSettings.ConfigFileStrings["howHeardVcInfo"]["optionFirstText"].ToString(), appSettings.ConfigFileStrings["howHeardVcInfo"]["optionSecondText"].ToString(), appSettings.ConfigFileStrings["howHeardVcInfo"]["optionThirdText"].ToString() };
                Random rnd = new Random();
                string[] MyRandomArray = arrOptions.OrderBy(x => rnd.Next()).ToArray();

                Radio1.Content = MyRandomArray[0];
                Radio2.Content = MyRandomArray[1];
                Radio3.Content = MyRandomArray[2];

                SomewhereElse.Content = appSettings.ConfigFileStrings["howHeardVcInfo"]["optionFourthText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetupFocusTips Init EX + " + ex.Message);
            }
        }
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nStep--;

                btnBack.IsEnabled = true;
                btnBack.Visibility = Visibility.Visible;

                switch (nStep)
                {
                    case 1:
                        Dot1.IsEnabled = true;
                        Dot2.IsEnabled = false;

                        btnBack.IsEnabled = false;
                        btnBack.Visibility = Visibility.Collapsed;

                        /* focusTipsVcInfo =  "tip1title": "Tip 1:",
                         "tip1Text":"You can try it out now by clicking the Play icon next to your new focus mode in the list that appears on clicking the top menu icon.",,*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1title"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1Text"].ToString();

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_1.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();
                        TipImage.Source = image;

                        break;
                    case 2:
                        Dot2.IsEnabled = true;
                        Dot3.IsEnabled = false;

                        /* focusTipsVcInfo = "tip2title": "Tip 2:",
                            "tip2Text":"To modify this Focus Mode, first click on Preferences in the Focus Bear popup menu.",*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2title"].ToString(); 
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2Text"].ToString(); 

                        string imagePath2 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_2.png");
                        var image2 = new BitmapImage();
                        image2.BeginInit();
                        image2.UriSource = new Uri(imagePath2);
                        image2.EndInit();
                        TipImage.Source = image2;

                        break;
                    case 3:
                        Dot3.IsEnabled = true;
                        Dot4.IsEnabled = false;

                        /* "tip3title":"Tip 3:",
                        "tip3Text":"Then click on the Focus tab in the Preferences window that opens up.", */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3title"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3Text"].ToString();

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_3.png");
                        var image1 = new BitmapImage();
                        image1.BeginInit();
                        image1.UriSource = new Uri(imagePath1);
                        image1.EndInit();
                        TipImage.Source = image1;

                        btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonNextText"].ToString();

                        break;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetupFocusTips btnBack_Click EX + " + ex.Message);
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nStep++;
                btnBack.IsEnabled = true;
                btnBack.Visibility = Visibility.Visible;

                switch (nStep)
                {
                    case 2:
                        Dot1.IsEnabled = false;
                        Dot2.IsEnabled = true;
                        /* focusTipsVcInfo = "tip2title": "Tip 2:",
                             "tip2Text":"To modify this Focus Mode, first click on Preferences in the Focus Bear popup menu.",*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2title"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2Text"].ToString();

                        string imagePath2 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_2.png");
                        var image2 = new BitmapImage();
                        image2.BeginInit();
                        image2.UriSource = new Uri(imagePath2);
                        image2.EndInit();
                        TipImage.Source = image2;

                        break;
                    case 3:
                        Dot2.IsEnabled = false;
                        Dot3.IsEnabled = true;

                        /* "tip3title":"Tip 3:",
                        "tip3Text":"Then click on the Focus tab in the Preferences window that opens up.", */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3title"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3Text"].ToString();

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_3.png");
                        var image1 = new BitmapImage();
                        image1.BeginInit();
                        image1.UriSource = new Uri(imagePath1);
                        image1.EndInit();
                        TipImage.Source = image1;

                        btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonNextText"].ToString();


                        break;
                    case 4:
                        Dot3.IsEnabled = false;
                        Dot4.IsEnabled = true;
                        btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonGotItText"].ToString();


                        /*  "tip4title":"Tip 4:",
                         "tip4Text":"Then choose your focus mode from the left list of Focus Types. Edit the settings in the right side.",   */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip4title"].ToString(); 
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip4Text"].ToString(); 
                      
                        string imagePath3 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_4.png");
                        var image3 = new BitmapImage();
                        image3.BeginInit();
                        image3.UriSource = new Uri(imagePath3);
                        image3.EndInit();
                        TipImage.Source = image3;
                        //TipImage.Height = 140;
                        break;
                    case 5:
                        GridFocusTips.Visibility = Visibility.Collapsed;
                        GridHowDidYouHearAboutFocusBear.Visibility = Visibility.Visible;
                        break;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetupFocusTips btnNext_Click EX + " + ex.Message);
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            if(Radio1.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio1.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);

            }
            else if(Radio2.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio2.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            else if (Radio3.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio3.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            else if (SomewhereElse.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", TxtSomewhereElse.Text);
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            _parentWindow.Close();
        }

        private void TxtSomewhereElse_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(TxtSomewhereElse.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void SomewhereElse_Checked(object sender, RoutedEventArgs e)
        {
            TxtSomewhereElse.Visibility = Visibility.Visible;
            if (TxtSomewhereElse.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void Radio3_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }

        private void Radio2_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }

        private void Radio1_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }
    }
}
