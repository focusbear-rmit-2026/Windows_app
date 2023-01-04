/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: UpdateWin.xaml.cs
 * Project: SMART System Repair
 * User: Gaurav
 *
 * This document is the property of Wise Tech Labs Private Limited
 * It is considered confidential and proprietary.
 *
 * This document may not be reproduced or transmitted in any form, 
 * in whole or in part, without the express written permission of 
 * Wise Tech Labs Private Limited
 *
 */

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
        MainWindow mainWin = null;

        public UpdateWin()
        {
            InitializeComponent();
            InitializeSettings();
            InitializeUIText();
        }

        private void InitializeSettings()
        {
            try
            {
                mainWin = Application.Current.MainWindow as MainWindow;
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
                DownloadProgressBar.Value = e.ProgressPercentage; //todo Win Text
                TxtStatus.Text = string.Format("Downloading Update: {0} of {1}", Global.ToFileSizeString(e.BytesReceived), Global.ToFileSizeString(e.TotalBytesToReceive));  //$"Downloading Update:  {Utility.ToFileSizeString(e.BytesReceived)} of {Utility.ToFileSizeString(e.TotalBytesToReceive)}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void _appUpdater_AsyncCompletedEventArgsHandler(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                //Check if Download is correct  
                string updateFilePath = AppSettings.AppDataDirPath + "\\FocusBearSetup.exe";
                //var currentFileHash = Utility.GetMD5Hash(updateFilePath);
               // if (currentFileHash.ToLower() == _appUpdater._UpdateHash.ToLower())
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
                Heading.Text = "Downloading Update"; //todo Win Text
                BtnCancel.Content = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
