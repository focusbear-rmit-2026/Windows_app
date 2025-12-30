using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitsPostponedView.xaml
    /// </summary>
    public partial class HabitsPostponedView : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public HabitsPostponedView()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

        }

        private void btnResumeHabits_Click(object sender, RoutedEventArgs e)
        {
            //Show the gridshade as it will take a while for the 5 second timer to get current state again
            ((App)Application.Current).wndFocusBearMainWindow.GridShade.Visibility = Visibility.Visible;

            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnResumeHabits);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("Habits postponed state View shown");

                string sHeading = "";
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    if (appSettings.IsShutOffTime())
                    {
                        sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["eveningRoutinePostponedText"].ToString();
                    }
                    else
                    {
                        sHeading = appSettings.ConfigFileStrings["habitsStateVcInfo"]["morningRoutinePostponedText"].ToString();
                    }
                }
      
                Heading.Text = sHeading;

                btnResumeHabitsText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Habits postponed state () ex = " + ex.Message);
            }
        }
    }
}
