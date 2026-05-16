using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for StartSetUpHabitsPage.xaml
    /// </summary>
    public partial class StartSetUpHabitsPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;

        public StartSetUpHabitsPage()
        {
        }
        public StartSetUpHabitsPage(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Bubble1.Text = appSettings.ConfigFileStrings["startSetUpHabitsVcInfo"]?["bubbleText"]?.ToString() ?? "Want me to bring aboard some new habits?";  
                Heading.Text = appSettings.ConfigFileStrings["startSetUpHabitsVcInfo"]?["titleText"]?.ToString() ?? "Need some fresh ideas for habits?";
                Description.Text = appSettings.ConfigFileStrings["startSetUpHabitsVcInfo"]?["subTitleText"]?.ToString() ?? "If you have your own habit list, you can import them too.";

                btnAlreadyHaveHabitsText.Text = appSettings.ConfigFileStrings["startSetUpHabitsVcInfo"]?["buttonNo"]?.ToString() ?? "Nay - I already have some habits";
                btnGiveMeSomeIdeasText.Text = appSettings.ConfigFileStrings["startSetUpHabitsVcInfo"]?["buttonGiveMeHabits"]?.ToString() ?? "Aye - Give me some ideas, Cap’n";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartSetUpHabitsPage() EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnGiveMeSomeIdeas_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-give-me-some-ideas-clicked");
            appSettings.sHabitsParam = "&add-habits-mode=recommend";
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._INSTALL_HABIT_PACKS_);
        }

        private void btnAlreadyHaveHabits_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-already-have-habits-clicked");
            appSettings.sHabitsParam = "&add-habits-mode=import";
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._INSTALL_HABIT_PACKS_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("setting-up-habits-screen-shown");
            }
        }
    }
}
