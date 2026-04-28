using Focus_Bear.Models;
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
    /// Interaction logic for AllSetPage.xaml
    /// </summary>
    public partial class AllSetPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public AllSetPage()
        {
        }
        public AllSetPage(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["allSetVcInfo"]["title"].ToString(); //"Fantastic! We are all set.";
                Description.Text = appSettings.ConfigFileStrings["allSetVcInfo"]["subTitle"].ToString(); //"You are now ready to become a creature of habit.";
                btnStartUsingFocusBearText.Text = appSettings.ConfigFileStrings["allSetVcInfo"]["buttonContinue"].ToString(); //"Start using Focus Bear";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AllSetPage() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }


        private void btnStartUsingFocusBear_Click(object sender, RoutedEventArgs e)
        {
            appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Set state to none now.
            ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();

            RegistryManager.SetOnboardingAfterLoginDoneStatus();
            

            ((App)(Application.Current)).ExecutePostLoginTasks(); 
            _parentWindow.Hide();//will be handled by main window
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("all-set-screen-shown");
            }
        }
    }
}
