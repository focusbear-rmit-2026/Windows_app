/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: WinSecurity.cs
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
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Focus_Bear.Models
{
    public class WinSecurity
    {
        [DllImport("user32")]
        public static extern UInt32 SendMessage(IntPtr hWnd, UInt32 msg, UInt32 wParam, UInt32 lParam);

        private const int BCM_FIRST = 0x1600; //Normal button
        private const int BCM_SETSHIELD = (BCM_FIRST + 0x000C); //Elevated button

        internal static void RestartElevated(bool AddToStartup)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Verb = "runas";
            //startInfo.Arguments = "/opensettings";

            if (AddToStartup)
            {
                startInfo.Arguments = "/startupYes";
            }
            else
            {
                startInfo.Arguments = "/startupNo";
            }

            //System.Windows.Application.Current.Shutdown(0);
            Process p = null;
            try
            {
                p = Process.Start(startInfo);
                p.WaitForExit(10000);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }
        }

        internal static bool IsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
