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
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnBoardingWindow.xaml
    /// </summary>
    ///
   
    public partial class OnBoardingWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public OnBoardingWindow()
        {
            InitializeComponent();

#if DEBUG
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
#endif
            // "stayFocusedText":"Now let's set up an account for you",
            Heading.Text = "";
            
            AppFrameOnboarding.Visibility = Visibility.Visible;

            AppFrameOnboarding.Navigate(new OnboardingWelcome(this));
        }

        public void ShowMindFullBreathing()
        {
            AppFrameOnboarding.Navigate(new OnboardingMindfulBreathing(this));
        }

        public void ShowMindFullBreathingFinished()
        {
            AppFrameOnboarding.Navigate(new OnboardingWorkType(this));
        }

        public async void HideFrameAndClose()
        {
            // "stayFocusedText":"Now let's set up an account for you",
            Heading.Text = appSettings.ConfigFileStrings["common"]["letsCreatAccountText"].ToString();
            AppFrameOnboarding.Visibility = Visibility.Collapsed;
            await Task.Delay(3000);
            ((App)(Application.Current)).ShowLoginWindow();
            Close();
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
