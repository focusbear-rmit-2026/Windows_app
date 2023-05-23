/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: TrayBalloon.xaml.cs
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Forms;
using Focus_Bear.Utils;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for TrayBalloon.xaml
    /// </summary>
    public partial class TrayBalloon : Window
    {
        private bool isClosed = true;

        AppSettings appSettings = AppSettings.GetInstance;
        public TrayBalloon()
        {
        }
        //pos 0 = bottom right, 1 = top right
        public TrayBalloon(int pos, string message)
        {
            InitializeComponent();
 
            AppTitle.Text = "Focus Bear";
            TrayBaloonTipMsg.Text = message;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width) - 15;
            if(pos == 1) //Top right
                this.Top = desktopWorkingArea.Top + 25;
            else
                this.Top = (desktopWorkingArea.Bottom - this.Height) - 15;

            this.Topmost = true;
        }

        Timer TooltipTimer = null;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TooltipTimer = new Timer();
                if (appSettings.IsFirstInstall)
                {
                    TooltipTimer.Interval = 60000; //1 minute
                }
                else
                {
                    TooltipTimer.Interval = 5000;
                }
                TooltipTimer.Tick += TooltipTimer_Tick;
                TooltipTimer.Enabled = true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void TooltipTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                isClosed = true;
                this.Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void window_Deactivated(object sender, EventArgs e)
        {
            try
            {
                if (!isClosed)
                    Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

      
    }
}
