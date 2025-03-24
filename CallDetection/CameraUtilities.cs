using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace Focus_Bear.CallDetection
{
    public static class CameraUtilities
    {

        public static bool IsUserOnVideoCall()
        {
            LogWriter.LogWrite("IsUserOnVideoCall()");

            bool bWebcamInUse =  IsWebCamInUse();

            LogWriter.LogWrite("Webcam in use: " + bWebcamInUse.ToString());

            return bWebcamInUse;
        }
        private static bool IsWebCamInUse()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"))
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        // NonPackaged has multiple subkeys
                        if (subKeyName == "NonPackaged")
                        {
                            LogWriter.LogWrite("LM NonPackaged found");

                            using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                            {
                                foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                                {
                                    Console.WriteLine("LM nonpackagedSubKeyName " + nonpackagedSubKeyName);

                                    using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                    {
                                        if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                        {
                                            var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                            Console.WriteLine("lm nonpackagedSubKeyName endTime" + endTime.ToString());
                                            if (endTime <= 0)
                                            {
                                                return IsEXEAllowedInMeetingsFocusMode(subKey.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                Console.WriteLine("lm subKeyName" + subKeyName.ToString());
                                if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                {
                                    var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                    if (endTime <= 0)
                                    {
                                        return IsEXEAllowedInMeetingsFocusMode(subKey.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsWebCamInUse() localmachine Possibly key not found Exception" + ex.Message);
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"))
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        // NonPackaged has multiple subkeys
                        if (subKeyName == "NonPackaged")
                        {
                            LogWriter.LogWrite("cu NonPackaged found");
                            using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                            {
                                foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                                {
                                    Console.WriteLine("cu nonpackagedSubKeyName " + nonpackagedSubKeyName);
                                    using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                    {
                                        if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                        {
                                            var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                            Console.WriteLine("cu nonpackagedSubKeyName endTime" + endTime.ToString());
                                            if (endTime <= 0)
                                            {
                                                return IsEXEAllowedInMeetingsFocusMode(subKey.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                Console.WriteLine("cu subKeyName" + subKeyName.ToString());
                                if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                {
                                    var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                    if (endTime <= 0)
                                    {
                                        return IsEXEAllowedInMeetingsFocusMode(subKey.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsWebCamInUse() CurrentUser Exception" + ex.Message);
            }

            return false;
        }

        private static bool IsEXEAllowedInMeetingsFocusMode(string sExeName)
        {
            try
            {
                LogWriter.LogWrite("camera IsEXEAllowedInMeetingsFocusMode - " + sExeName);
                //we need to get the exe name after hte last #
                string inputString = sExeName; //example: @"C:#Users#jnbbe#AppData#Roaming#Movavi Screen Recorder 23#ScreenRecorder.exe";

                // Split the string by #
                string[] parts = inputString.Split('#');

                // Extract the string after the last #
                string extractedString = parts[parts.Length - 1];

                sExeName = extractedString; //assign back

                LogWriter.LogWrite("camera IsEXEAllowedInMeetingsFocusMode EXTRACTED - " + sExeName);
                //Let's check this in the Meetings focus mode
                //Check Meetings Focus Mode
                AppSettings appSettings = AppSettings.GetInstance;
                var listItemsM = appSettings.arrFocusModes.Where(m => m.isMeetings == true);
                if (listItemsM.Count() != 0)
                {
                    LogWriter.LogWrite("isMeetings foucs mode found - camera");

                    FocusMode focusType = listItemsM.First();
                    if (focusType != null)
                    {
                        LogWriter.LogWrite("focusType != null");

                        bool bAllowedAppFound = false;
                        foreach (AppDetails sApp in focusType.arrAllowedAppsWin)
                        {
                            foreach (string sAppExe in sApp.ExeNames)
                            {
                                string App = sAppExe.ToLower();
                                LogWriter.LogWrite("IsEXEAllowedInMeetingsFocusMode meeting mode exe - " + App);
                                if (App == sExeName.ToLower())
                                {
                                    bAllowedAppFound = true;
                                    break;
                                }
                            }

                            if (bAllowedAppFound) //we have found a matching exe name
                                break;
                        }
                        if (bAllowedAppFound)
                        {
                            LogWriter.LogWrite("bAllowedAppFound return true;");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("camera IsEXEAllowedInMeetingsFocusMode Exception" + ex.Message);
            }
            return false;
        }
    }
}
