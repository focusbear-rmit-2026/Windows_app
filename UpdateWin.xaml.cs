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

using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Reflection;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Focus_Bear.Preferences.SettingsTab;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for UpdateWin.xaml
    /// </summary>
    public partial class UpdateWin : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        CancellationTokenSource _tokenSource = null;
        CancellationToken _cancelToken;
        AppUpdater _appUpdater = null;
        FocusBearMainWindow mainWin = null;

        public UpdateWin()
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

            InitializeSettings();
            InitializeUIText();
        }

        private void InitializeSettings()
        {
            try
            {
                mainWin = Application.Current.MainWindow as FocusBearMainWindow;
                Owner = mainWin;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                //this.Width = mainWin.Width;
                this.DataContext = this;

                _tokenSource = new CancellationTokenSource();
                _cancelToken = _tokenSource.Token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public UpdateWin(ref AppUpdater appUpdater) : this()
        {
            try
            {
                _appUpdater = appUpdater;

                if (!_appUpdater.ForceUpdate)
                {
                    _appUpdater.DownloadProgressHandler += _appUpdater_DownloadProgressHandler;
                }

                if(_appUpdater.bCheckingForFocusButton)
                    _appUpdater.AsyncCompletedEventArgsHandler += _appUpdater_AsyncCompletedEventArgsHandlerForFocusButton;
                else
                    _appUpdater.AsyncCompletedEventArgsHandler += _appUpdater_AsyncCompletedEventArgsHandler;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tokenSource.Cancel();
                //_tokenSource.Dispose();
                _tokenSource = new CancellationTokenSource();

                CloseWin();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_appUpdater.ForceUpdate)
                {
                    this.Visibility = Visibility.Collapsed;
                }

                await _appUpdater.DownloadUpdate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void _appUpdater_DownloadProgressHandler(object sender, DownloadProgressChangedEventArgs e)
        {
            try
            {
                DownloadProgressBar.Value = e.ProgressPercentage;  
                //"Downloading Update: {0} of {1}"
                TxtStatus.Text = string.Format(appSettings.ConfigFileStrings["WindowsSpecific"]["downloadProgress"].ToString(), Global.ToFileSizeString(e.BytesReceived), Global.ToFileSizeString(e.TotalBytesToReceive));  
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void _appUpdater_AsyncCompletedEventArgsHandler(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            // Never in the E2E test build: the installer would install over the real app.
            if (!E2EBuild.MachineIntegrationAllowed) return;
            try
            {
                //Check if Download is correct
                string updateFilePath = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FocusBearSetup.exe");
                {
                    Application.Current.Shutdown(0);
                    Process.Start(updateFilePath, "/SILENT /VERYSILENT");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void _appUpdater_AsyncCompletedEventArgsHandlerForFocusButton(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                FocusButton.RaiseFirmwareDownloadFinished();
                CloseWin();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tokenSource.Cancel();
                //_tokenSource.Dispose();
                _tokenSource = new CancellationTokenSource();

                CloseWin();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                CloseWin();
            }
        }


        private void CloseWin()
        {
            try
            {
                //_tokenSource.Dispose();
                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
         
        private void InitializeUIText()
        {
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["WindowsSpecific"]["downloadingUpdate"].ToString();//"Downloading Update"; 
                BtnCancel.Content = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
