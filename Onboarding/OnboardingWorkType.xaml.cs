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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingWorkType.xaml
    /// </summary>
    public partial class OnboardingWorkType : Page
    {
        private OnBoardingWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingWorkType()
        {

        }
        public OnboardingWorkType(OnBoardingWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
            InitializeComponent();

            string sActivityName = appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["name"].ToString();
            string Title = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
            Title = Title.Replace("<ACTIVITY>", sActivityName);
            Heading.Text = Title.ToString();

            //       "letsCreatAccountText":"Now let's set up an account for you",
            TypeOfWork.Text = appSettings.ConfigFileStrings["common"]["letsCreatAccountText"].ToString();
 
                btnContinueTxt.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();
            //int nDurationSeconds = (int)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["duration_seconds"];
            //nTimer = nDurationSeconds;

            //JArray array = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["video_urls"];
            //int n = array.Count;
            //Random rnd = new Random();
            //int nRandom = rnd.Next(0, n - 1);
            //string videoUrl = array[nRandom].ToString();
            btnContinue.IsEnabled = true;

        }

        //private void ComboBoxWorkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    btnContinue.IsEnabled = false;

        //    if (ComboBoxWorkType.SelectedIndex == 4) //Something Else Selected
        //    {
        //        TxtSomethingElse.Visibility = Visibility.Visible;
        //        TxtSomethingElse.Focus();
        //    }
        //    else
        //    {
        //        TxtSomethingElse.Text = "";
        //        TxtSomethingElse.Visibility = Visibility.Collapsed;
        //        if(ComboBoxWorkType.SelectedIndex != 0)
        //        {
        //            btnContinue.IsEnabled = true;
        //        }
        //        else
        //        {
        //            btnContinue.IsEnabled = false;
        //        }
        //    }
        //}

        //private void TxtSomethingElse_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    if(TxtSomethingElse.Text.Length > 0)
        //    {
        //        btnContinue.IsEnabled = true;
        //    }
        //    else
        //    {
        //        btnContinue.IsEnabled = false;
        //    }
        //}

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parentWindow.HideFrameAndClose();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
