using Focus_Bear.Models;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for BreakActivityBrainDump.xaml
    /// </summary>
    public partial class BreakActivityBrainDump : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitWindow _parentWindow;
        public BreakActivityBrainDump()
        {

        }
        public BreakActivityBrainDump(HabitWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;
            /*
            "breaksBrainDumpVcInfo": {
                "title":"Capture your thoughts before starting your break",
                    "description":"Taking regular breaks (every 20 minutes is recommended) is crucial for avoiding eye strain and postural issues. You might be worried that it will take you out of your flow and disrupt your productivity. Let's capture what you were just working on so that you won't lose your train of thought after the break. BTW you'll often find that when you take a break and give your brain some time in the \"default mode network\", you'll come up with novel solutions to a problem you'd been struggling with. Not necessarily a productivity killer at all 😉",
                    "buttonStartBreakText":"Start Break"
                },
            */ 
            TxtDumpDown.Text = appSettings.sBrainDump;
            
            try
            {

                Heading.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["description"].ToString();
                btnStartBreakTXT.Text = appSettings.ConfigFileStrings["breaksBrainDumpVcInfo"]["buttonStartBreakText"].ToString();

              
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BreakActivityBrainDump text setting ex -" + ex.Message);
            }
        }

        private void btnStartBreak_Click(object sender, RoutedEventArgs e)
        {
            appSettings.sBrainDump = TxtDumpDown.Text;
            RegistryManager.SaveBrainDump();

            _parentWindow.ShowStartBreakActivityScreen();

        }
    }
}
