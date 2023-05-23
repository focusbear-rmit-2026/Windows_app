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
        int nStep = 0;
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

                /* focusTipsVcInfo =  "tip1title": "Tip 1:",
                          "tip1Text":"You can try it out now by clicking the Play icon next to your new focus mode in the list that appears on clicking the top menu icon.",,*/
                TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1title"].ToString();
                TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1TextWIN"].ToString();

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
                     "optionFourthText":"Somewhere else"
                 optionRemarkableText": "Remarkable"    
                 */
              
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
                    //case 0:
                    //    Dot0.IsEnabled = true;
                    //    Dot1.IsEnabled = false;

                    //    btnBack.IsEnabled = false;
                    //    btnBack.Visibility = Visibility.Collapsed;

                    //    /* focusTipsVcInfo =  " "tip0title": "Create a Focus Mode",
                    //    "tip0Text": "To help you get more done, let's set up a Focus Mode. A Focus Mode is a set of apps and websites that are productive. When you start a Focus Block, non-productive apps/sites are blocked to help you avoid going down rabbitholes." */
                    //    TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip0title"].ToString();
                    //    TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip0Text"].ToString();

                    //    string imagePath0 = ("pack://application:,,,/Focus Bear;component/images/fb_big.png");
                    //    var image0 = new BitmapImage();
                    //    image0.BeginInit();
                    //    image0.UriSource = new Uri(imagePath0);
                    //    image0.EndInit();
                    //    TipImage.Source = image0;

                    //    break;
                    case 0:
                        Dot0.IsEnabled = true;
                        Dot1.IsEnabled = false;

                        btnBack.IsEnabled = false;
                        btnBack.Visibility = Visibility.Collapsed;

                        /* focusTipsVcInfo =  "tip1title": "Tip 1:",
                         "tip1Text":"You can try it out now by clicking the Play icon next to your new focus mode in the list that appears on clicking the top menu icon.",,*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1title"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1TextWIN"].ToString();

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_1.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();
                        TipImage.Source = image;

                        break;
                    case 1:
                        Dot1.IsEnabled = true;
                        Dot2.IsEnabled = false;

                        /* focusTipsVcInfo = "tip2Newtitle": "Tip 2: Teach Focus Bear what is productive",
                            "tip2NewText":"As you use this Focus Mode, Focus Bear will ask you whether each website/app is productive.",*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2Newtitle"].ToString(); 
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2NewText"].ToString(); 

                        string imagePath2 = ("pack://application:,,,/Focus Bear;component/images/focustips/tip_blockPopup.png");
                        var image2 = new BitmapImage();
                        image2.BeginInit();
                        image2.UriSource = new Uri(imagePath2);
                        image2.EndInit();
                        TipImage.Source = image2;

                        break;
                    case 2:
                        Dot2.IsEnabled = true;
                        Dot3.IsEnabled = false;

                        /* tip3Newtitle":"Tip 3: Permanently allow apps/websites",
        "tip3NewText":"At the end of the focus block, you can permanently allow those apps/websites (or choose not to).", */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3Newtitle"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3NewText"].ToString();

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/focustips/tip_allowApps.png");
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
                    //case 1:
                    //    Dot0.IsEnabled = false;
                    //    Dot1.IsEnabled = true;
                    //    /* focusTipsVcInfo =  "tip1title": "Tip 1:",
                    //    "tip1Text":"You can try it out now by clicking the Play icon next to your new focus mode in the list that appears on clicking the top menu icon.",,*/
                    //    TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1title"].ToString();
                    //    TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip1TextWIN"].ToString();

                    //    string imagePath0 = ("pack://application:,,,/Focus Bear;component/images/focustips/focusTip_1.png");
                    //    var image0 = new BitmapImage();
                    //    image0.BeginInit();
                    //    image0.UriSource = new Uri(imagePath0);
                    //    image0.EndInit();
                    //    TipImage.Source = image0;

                    //    break;
                    case 1:
                        Dot0.IsEnabled = false;
                        Dot1.IsEnabled = true;
                        /* focusTipsVcInfo = "tip2Newtitle": "Tip 2: Teach Focus Bear what is productive",
                            "tip2NewText":"As you use this Focus Mode, Focus Bear will ask you whether each website/app is productive.",*/
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2Newtitle"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip2NewText"].ToString();

                        string imagePath2 = ("pack://application:,,,/Focus Bear;component/images/focustips/tip_blockPopup.png");
                        var image2 = new BitmapImage();
                        image2.BeginInit();
                        image2.UriSource = new Uri(imagePath2);
                        image2.EndInit();
                        TipImage.Source = image2;

                        break;
                    case 2:
                        Dot1.IsEnabled = false;
                        Dot2.IsEnabled = true;

                        /* "tip3Newtitle":"Tip 3: Permanently allow apps/websites",
        "tip3NewText":"At the end of the focus block, you can permanently allow those apps/websites (or choose not to).", */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3Newtitle"].ToString();
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip3NewText"].ToString();

                        string imagePath1 = ("pack://application:,,,/Focus Bear;component/images/focustips/tip_allowApps.png");
                        var image1 = new BitmapImage();
                        image1.BeginInit();
                        image1.UriSource = new Uri(imagePath1);
                        image1.EndInit();
                        TipImage.Source = image1;

                        btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonNextText"].ToString();


                        break;
                    case 3:
                        Dot2.IsEnabled = false;
                        Dot3.IsEnabled = true;
                        btnNextTXT.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["buttonGotItText"].ToString();


                        /*   "tip4Newtitle":"Tip 4: Use keywords",
                        "tip4NewText":"You can even add keywords, e.g. \"React Native\" and any website with that keyword will be allowed.",  */
                        TipTitle.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip4Newtitle"].ToString(); 
                        TipDescription.Text = appSettings.ConfigFileStrings["focusTipsVcInfo"]["tip4NewText"].ToString(); 
                      
                        string imagePath3 = ("pack://application:,,,/Focus Bear;component/images/focustips/tip_allowKeywords.png");
                        var image3 = new BitmapImage();
                        image3.BeginInit();
                        image3.UriSource = new Uri(imagePath3);
                        image3.EndInit();
                        TipImage.Source = image3;
                        //TipImage.Height = 140;
                        break;
                    case 4:
                        //GridFocusTips.Visibility = Visibility.Collapsed;
                        ///GridHowDidYouHearAboutFocusBear.Visibility = Visibility.Visible;
                        _parentWindow.Close();
                        break;
                }
            }
            catch (Exception ex )
            {
                LogWriter.LogWrite("OnboardingSetupFocusTips btnNext_Click EX + " + ex.Message);
            }
        }

       
    }
}
