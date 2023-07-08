using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for FocusModeEnd.xaml
    /// </summary>
    public partial class FocusModeEnd : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
 
        public FocusModeEnd()
        {
            InitializeComponent();
            LogWriter.LogWrite("FocusModeEnd()");
            appSettings.bStartingAnotherFocusMode = false;
           
            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
         }

        private void btnTakeBreak_Click(object sender, RoutedEventArgs e)
        {
            Take5MinsBreak();
        }

        private async void Take5MinsBreak()
        {
            LogWriter.LogWrite("Take5MinsBreak() Clicked");
   
            DateTime now = DateTime.Now;
            TimeSpan difference = now.Subtract(DateTime.Parse(appSettings.sRunningFocusModeStartTime)); // could also write `now - otherTime`
            var durationSeconds = difference.TotalSeconds;
            int nSeconds = Convert.ToInt32(durationSeconds);

            string sAchieve = TxtAchieve.Text.Replace(Environment.NewLine, " ");
            string sDistractions = TxtDistractions.Text.Replace(Environment.NewLine, " ");
            appSettings.AddEntryInCSV("focus block", appSettings.sRunningFocusModeName, nSeconds.ToString(), "", "", appSettings.sRunningFocusModeStartTime, sAchieve, sDistractions, "", "");

            //Call api top finish focus mode
            FocusBearAPIClient client = new FocusBearAPIClient();
            var xResponse = client.FinishFocusMode(appSettings.sRunningFocusModeID, sAchieve, sDistractions);

            Hide();

            //Show the Break Window
            ((App)(Application.Current)).ShowBreakWindow();

            //Show the brain dump window
            if (appSettings.sBrainDump.Length > 0) //Only show if there is text.
                ((App)(Application.Current)).ShowBrainDumpWindow();

                //Ask the user for adding temp apps / urls
            if (appSettings.TemporaryAllowedAppsGentleBlock.Count != 0 || appSettings.TemporaryAllowedURLsGentleBlock.Count != 0 || appSettings.TemporaryAllowedURLsTabTitles.Count != 0)
            {
                if(appSettings.TemporaryAllowedURLsTabTitles.Count > 0)
                {
                    LogWriter.LogWrite("Attempt to GetKeywords() -");
                    var m = await client.GetKeywords();
                    if(m == 1)
                    {
                        LogWriter.LogWrite("GetKeywords() Done-");
                        ///
                        appSettings.TemporaryAllowedKeywords.Clear();

                        //Check is we got some response from keywords api
                        if (appSettings.sRawKeywords != null || (appSettings.sRawKeywords.Length != 0))
                        {
                            //Parse the JSON string
                            try
                            {
                                List<string> array = new List<string>();
                                array = JsonConvert.DeserializeObject<List<string>>(appSettings.sRawKeywords);
                                if (array.Count > 0)
                                {
                                    foreach(string ss in array)
                                    {
                                        appSettings.TemporaryAllowedKeywords.Add(new DistractingURL(ss, false));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite("Parsing appSettings.sRawKeywords  EX - " + ex.Message);
                            }
                        }
                    }
                }

                AddTempAllowedToFocusModeWindow aa = new AddTempAllowedToFocusModeWindow();
                aa.Topmost = true;
                bool? bResult = aa.ShowDialog();
                if(bResult.Value)
                {

                    var listItems = appSettings.arrFocusModes.Where(i => i.id.ToLower() == appSettings.sRunningFocusModeID.ToLower());
                    if (listItems.Count() != 0)
                    {
                        FocusMode ff = listItems.First();

                        try
                        {
                            foreach (DistractingURL dd in appSettings.TemporaryAllowedAppsGentleBlock)
                            {
                                 if(dd.IsItemChecked)
                                ff.arrAllowedAppsWin.Add(new AppDetails(dd.sURL, dd.sURL));
                            }
                            foreach (DistractingURL dd in appSettings.TemporaryAllowedURLsGentleBlock)
                            {
                                if(dd.IsItemChecked)
                                    ff.arrAllowedUrls.Add(dd.sURL);
                            }
                            foreach (DistractingURL dd in appSettings.TemporaryAllowedKeywords)
                            {
                                if(dd.IsItemChecked && dd.sURL.Length > 0)
                                    ff.arrAllowedUrls.Add(dd.sURL);
                            }
                            //we will call the API to update all the focus modes.
                            LogWriter.LogWrite("attempt to UpdateAllFocusModes() API");
                            var response = client.UpdateAllFocusModes();

                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("btnAddDeleteAllowedURLs_Click EX = " + ex.Message);
                        }
                    }
                }
            }
             
        }
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(IsVisible)
            {

                try
                {
                    Heading.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["title"].ToString();

                    string sDescription = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["subTitle"].ToString();
                    Description.Text = sDescription.Replace("<FOCUSTYPE>", appSettings.sRunningFocusModeName);

                    YourIntention.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleIntention"].ToString();
                    Achieve.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["questionFirstText"].ToString();
                    Distractions.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["questionSecondText"].ToString();

                    btnTakeBreakText.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonTakeBreakText"].ToString();

                    TxtIntention.Text = appSettings.sRunningFocusModeIntention; //Place the text

                    btnStartAnotherFocusModeText.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonStartFocusText"].ToString();

                    //Empty the boxes
                    TxtAchieve.Text = "";
                    TxtDistractions.Text = "";

                    //Empty the boxes
                    TxtAchieve.Text = "";
                    TxtDistractions.Text = "";
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FocusModeEnd Window_IsVisibleChanged EX = " + ex.Message);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            if(!appSettings.bStartingAnotherFocusMode)
                Take5MinsBreak();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (null != e)
                e.Handled = true;


            var source = PresentationSource.FromVisual(this);

            if (null != source && null != source.CompositionTarget)
            {
                var matrix = source.CompositionTarget.TransformToDevice;

                var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();

                LayoutTransform = dpiTransform;
                this.Width = this.Width * dpiTransform.ScaleX;
                this.Height = this.Height * dpiTransform.ScaleY;
                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Width - this.Width) / 2;
                this.Top = (desktopWorkingArea.Height - this.Height) / 2;
            }
        }

        private void btnStartAnotherFocusMode_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bStartingAnotherFocusMode = true;

            Hide();

            var listItems = appSettings.arrFocusModes.Where(i => i.id.ToLower() == appSettings.sRunningFocusModeID.ToLower());
            if (listItems.Count() != 0)
            {
                ((App)(Application.Current)).wndTrayMenu.ListFocusModes.SelectedItem = listItems.First();
            }
           
        }
    }
}
