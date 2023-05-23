/*
/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: RegistryManager.cs
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

//using IWshRuntimeLibrary;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear.Models
{
    public class RegistryManager
    {
        static AppSettings appSettings = AppSettings.GetInstance;
        public async Task<Dictionary<string, string>> VerifyBrowserInstallations()
        {
            return await Task.Run(() =>
            {
                Dictionary<string, string> installedApps = new Dictionary<string, string>();
                try
                {
                    string root = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                    //Obtaining 64-bit Processes
                    if (Environment.Is64BitOperatingSystem)
                    {
                        ReadSubKeys(ref installedApps, root, true);
                    }

                    //Obtaining 32-bit Processes
                    ReadSubKeys(ref installedApps, root, false);

                    if (Environment.Is64BitOperatingSystem)
                    {
                        CheckMsEdgeInstallation(ref installedApps, true);
                    }
                    else
                    {
                        CheckMsEdgeInstallation(ref installedApps, false);
                    }

                    return installedApps;
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogWriter.LogWrite(ex.Message);
                    Debug.WriteLine(ex.Message);
                }
                catch (NullReferenceException ex)
                {
                    LogWriter.LogWrite(ex.Message);
                    Debug.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite(ex.Message);
                    Debug.WriteLine(ex.Message);
                }

                return installedApps;
            });
        }

      

        private void CheckMsEdgeInstallation(ref Dictionary<string, string> installedApps, bool Is64Bit)
        {
            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, Is64Bit ? RegistryView.Registry64 : RegistryView.Registry32))
            {
                string root = @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Packages";

                using (RegistryKey keySoftware = key.OpenSubKey(root))
                {
                    var subkeys = keySoftware.GetSubKeyNames();
                    foreach (string subKey in subkeys)
                    {
                        if (subKey.StartsWith("Microsoft.MicrosoftEdge_"))
                        {
                            try
                            {
                                using (RegistryKey childSubKey = keySoftware.OpenSubKey(subKey))
                                {
                                    var valDisplayName = "Microsoft Edge";
                                    var valAppPath = childSubKey.GetValue("Path");

                                    if (valDisplayName != null && valAppPath != null)
                                    {
                                        var installLocation = GetMsEdgeExecutable(valAppPath.ToString());

                                        if (!string.IsNullOrEmpty(installLocation))
                                        {
                                            if (!installedApps.ContainsKey(valDisplayName))
                                            {
                                                installedApps.Add(valDisplayName, installLocation);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite(ex.Message);
                                Debug.WriteLine(ex.Message);
                                continue;
                            }
                        }
                    }
                }
            }
        }



        private string GetMsEdgeExecutable(string dirPath)
        {
            string executablePath = null;
            try
            {
                if (Directory.Exists(dirPath))
                {
                    executablePath = Directory.EnumerateFiles(dirPath).FirstOrDefault(f => f == dirPath + "\\MicrosoftEdge.exe");
                    Debug.WriteLine("");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return executablePath;
        }

        private void ReadSubKeys(ref Dictionary<string, string> installedApps, string root, bool Is64Bit)
        {
            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Is64Bit ? RegistryView.Registry64 : RegistryView.Registry32))
            {
                using (RegistryKey keySoftware = key.OpenSubKey(root))
                {
                    var subkeys = keySoftware.GetSubKeyNames();
                    foreach (string subKey in subkeys)
                    {
                        if (!subKey.ToLower().Contains("google chrome") && !subKey.ToLower().Contains("mozilla firefox"))
                        {
                            continue;
                        }

                        try
                        {
                            using (RegistryKey childSubKey = keySoftware.OpenSubKey(subKey))
                            {
                                var valDisplayName = childSubKey.GetValue("DisplayName");
                                var valDisplayAppIcon = childSubKey.GetValue("DisplayIcon");

                                if (valDisplayName != null && valDisplayAppIcon != null)
                                {
                                    var installLocation = string.Empty;
                                    var displayName = FilterBrowserDisplayName(childSubKey.GetValue("DisplayName").ToString());
                                    if (valDisplayAppIcon != null)
                                    {
                                        var tmpPath = childSubKey.GetValue("DisplayIcon").ToString();
                                        if (!string.IsNullOrEmpty(tmpPath))
                                        {
                                            //In case path contains ',' then only obtain path before ','
                                            if (tmpPath.Contains(','))
                                            {
                                                tmpPath = tmpPath.Substring(0, tmpPath.IndexOf(","));
                                            }
                                            installLocation = tmpPath;
                                        }
                                    }

                                    if (System.IO.File.Exists(installLocation))
                                    {
                                        if (!installedApps.ContainsKey(displayName))
                                        {
                                            installedApps.Add(displayName, installLocation);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite(ex.Message);
                            Debug.WriteLine(ex.Message);
                            continue;
                        }
                    }
                }
            }
        }
       
        private string FilterBrowserDisplayName(string keyName)
        {
            if (keyName.ToLower().Contains("mozilla firefox"))
            {
                keyName = "Mozilla Firefox";
            }
            else if (keyName.ToLower().Contains("Google Chrome"))
            {
                keyName = "Google Chrome";
            }
            return keyName;
        }

        public static string GetRegistryGlobalPath()
        {
            return "Software\\Focus Bear";
        }
        public static string GetRegistryPath()
        {
            return "Software\\Focus Bear\\" + appSettings.userAccount.userID ;
        }

        public static string GetRegistryPathBackup()
        {
            return "Software\\{922474DA-ED9F-4602-878A-7C599D6F5284}"; //mj created fb
        }

        internal static bool CheckIfRegExists()
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {
                    if (rootKey == null)
                    {
                        return false;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return true;
        }

        private static object ReadGlobal(string value)
        {
            try
            {
                string root = GetRegistryGlobalPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {
                    if (rootKey != null)
                    {
                        return rootKey.GetValue(value);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static object Read(string value)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {
                    if (rootKey != null)
                    {
                        return rootKey.GetValue(value);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static object ReadSubKey(string subKey, string name)
        {
            try
            {
                string root = GetRegistryPath() + "\\" + subKey;
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {
                    if (rootKey != null)
                    {
                        return rootKey.GetValue(name);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static object ReadSubKeyBackup(string subKey, string name)
        {
            try
            {
                string root = GetRegistryPathBackup() + "\\" + subKey;
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {
                    if (rootKey != null)
                    {
                        return rootKey.GetValue(name);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static string ReadStr(string value)
        {
            try
            {
                string root = GetRegistryPath();
                RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root);

                if (rootKey != null)
                {
                    return rootKey.GetValue(value).ToString();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                ///LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static string ReadStrGlobal(string value)
        {
            try
            {
                string root = GetRegistryGlobalPath();
                RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root);

                if (rootKey != null)
                {
                    return rootKey.GetValue(value).ToString();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                ///LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static void WriteStrGlobal(string name, string value)
        {
            try
            {
                string root = GetRegistryGlobalPath();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static object ReadLocalMachine(string value)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(root))
                {
                    if (rootKey != null)
                    {
                        return rootKey.GetValue(value);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static IList<string> ReadValues(string value)
        {
            try
            {
                IList<string> path = new List<string>();
                string root = GetRegistryPath() + "\\" + value;
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root))
                {

                    if (rootKey == null)
                        return null;

                    string[] values = rootKey.GetValueNames();
                    foreach (var subKey in values)
                    {
                        string keyData = rootKey.GetValue(subKey).ToString();
                        if (!string.IsNullOrEmpty(keyData))
                        {
                            path.Add(keyData);
                        }
                    }
                }
                return path;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static bool Delete(string key, string path, bool deleteAll = false)
        {
            bool res = false;
            try
            {
                string obtainedKeyName = "";
                string root = GetRegistryPath();
                if (deleteAll)
                {
                    using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root, true))
                    {
                        if (rootKey != null)
                        {
                            rootKey.DeleteSubKeyTree(key);
                        }
                    }
                }
                else
                {
                    //using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root + "\\" + key, true))
                    using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root, true))
                    {
                        string[] values = rootKey.GetValueNames();
                        foreach (var subKey in values)
                        {
                            string keyData = rootKey.GetValue(subKey).ToString();
                            if (!string.IsNullOrEmpty(keyData))
                            {
                                if (path == keyData)
                                {
                                    obtainedKeyName = subKey;

                                    if (!string.IsNullOrEmpty(obtainedKeyName))
                                    {
                                        rootKey.DeleteValue(obtainedKeyName);
                                        res = true;

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                res = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                res = false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                res = false;
            }
            return res;
        }

        private static string ReadStrLocalMachine(string value)
        {
            try
            {
                string root = GetRegistryPath();
                RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(root);

                if (rootKey != null)
                {
                    var regValue = rootKey.GetValue(value);

                    if (regValue != null)
                        return regValue.ToString();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        private static void Write(string name, string value)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void Write(string subPath, string name, string value)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void WriteSubKey(string subKey, string name, string value)
        {
            try
            {
                string root = GetRegistryPath() + "\\" + subKey;
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void WriteSubKeyBackup(string subKey, string name, string value)
        {
            try
            {
                string root = GetRegistryPathBackup() + "\\" + subKey;
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void WriteLocalMachine(string value, string path)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.LocalMachine.CreateSubKey(root))
                {
                    rootKey.SetValue(value, path, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void Write(string value, byte[] data)
        {
            try
            {
                string root = GetRegistryPath();
                //MessageBox.Show(root);
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(value, data, RegistryValueKind.Binary);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void WriteGlobal(string value, byte[] data)
        {
            try
            {
                string root = GetRegistryGlobalPath();
                //MessageBox.Show(root);
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(value, data, RegistryValueKind.Binary);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void Write(string value, int index, string path)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root + "\\" + value))
                {
                    rootKey.SetValue(value + index, path, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void Update(string value, int index, string path)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root + "\\" + value, true))
                {
                    rootKey.SetValue(value + index, path, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }


        private static void Write(string value, IList<string> data)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root + "\\" + value))
                {
                    for (int i = 0; i < data.Count; i++)
                    {
                        var path = data[i];
                        rootKey.SetValue(value + i, path, RegistryValueKind.String);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
        private static bool DeleteValueGlobal(string value)
        {
            try
            {
                string root = GetRegistryGlobalPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root,true))
                {
                    if (rootKey != null)
                    {
                       rootKey.DeleteValue(value);
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite("DeleteValueGlobal RM - " + ex.Message);
             }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite("DeleteValueGlobal RM - " + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteValueGlobal RM - " + ex.Message);
 
            }
            return false;
        }

        private static bool DeleteValue(string value)
        {
            try
            {
                string root = GetRegistryPath();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root,true))
                {
                    if (rootKey != null)
                    {
                       rootKey.DeleteValue(value);
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
 
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
 
            }
            return false;
        }

        //internal static void SetGeneralSettings(bool ShouldLaunchAtStartup = false, bool UAC = false)
        //{
        //    try
        //    {
        //        byte[] regData = null;

        //        if (UAC)
        //        {
        //            var launchAtStartup = ShouldLaunchAtStartup;
        //            GetGeneralSettings();

        //            var ScanAtStartup = config.ScanAtAppLaunch;
        //            var MinimizeToTray = config.MinimizeToTray;

        //            var NotifyOnCrash = config.NotifyOnCrash;
        //            var AddToRecycleBinContext = config.AddToRecycleBinContext;                    

        //            var AddToShredOptionsToContext = config.AddShredOptionToContext;
        //            var MoveToRecycleBin = config.MoveFilesToRecycleBin;

        //            regData = new byte[7] {
        //                Convert.ToByte(launchAtStartup),
        //                Convert.ToByte(ScanAtStartup),
        //                Convert.ToByte(MinimizeToTray),
        //                Convert.ToByte(NotifyOnCrash),
        //                Convert.ToByte(AddToRecycleBinContext),                        
        //                Convert.ToByte(AddToShredOptionsToContext),
        //                Convert.ToByte(MoveToRecycleBin)};

        //            AppTaskScheduler scheduler = new AppTaskScheduler();
        //            if (ScanAtStartup && launchAtStartup)
        //            {
        //                scheduler.CreateTask(AppInfo.AppName, "/quickscansch");
        //            }
        //            else if (launchAtStartup && ScanAtStartup == false)
        //            {

        //                scheduler.CreateTask(AppInfo.AppName, string.Empty);
        //            }
        //        }
        //        else
        //        {
        //            regData = new byte[7] {
        //                Convert.ToByte(config.LaunchAtWindowStart),
        //                Convert.ToByte(config.ScanAtAppLaunch),
        //                Convert.ToByte(config.MinimizeToTray),
        //                Convert.ToByte(config.NotifyOnCrash),
        //                Convert.ToByte(config.AddToRecycleBinContext),                        
        //                Convert.ToByte(config.AddShredOptionToContext),
        //                Convert.ToByte(config.MoveFilesToRecycleBin)};
        //        }

        //        Write("GEN", regData);

        //        regData = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //internal static bool GetGeneralSettings()
        //{
        //    try
        //    {
        //        byte[] regData = (byte[])Read("GEN");

        //        if (regData == null)
        //            return false;

        //        config.LaunchAtWindowStart = Convert.ToBoolean(regData[0]);
        //        config.ScanAtAppLaunch = Convert.ToBoolean(regData[1]);
        //        config.MinimizeToTray = Convert.ToBoolean(regData[2]);
        //        config.NotifyOnCrash = Convert.ToBoolean(regData[3]);
        //        config.AddToRecycleBinContext = Convert.ToBoolean(regData[4]);
        //        config.AddShredOptionToContext = Convert.ToBoolean(regData[5]);
        //        config.MoveFilesToRecycleBin = Convert.ToBoolean(regData[6]);

        //        regData = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}


        //internal static bool GetJunkCleanSettings()
        //{
        //    try
        //    {
        //        byte[] regData = (byte[])Read("JCS");

        //        if (regData == null)
        //            return false;

        //        config.JunkScanForRecycleBin = Convert.ToBoolean(regData[0]);
        //        config.JunkScanForTempFiles = Convert.ToBoolean(regData[1]);
        //        config.JunkScanForWinDumpFiles = Convert.ToBoolean(regData[2]);
        //        config.JunkScanForChkFileFrags = Convert.ToBoolean(regData[3]);
        //        config.JunkScanForWinLogs = Convert.ToBoolean(regData[4]);
        //        config.JunkScanForErrorReport = Convert.ToBoolean(regData[5]);
        //        config.JunkScanForFontCache = Convert.ToBoolean(regData[6]);
        //        config.JunkScanForClipboardData = Convert.ToBoolean(regData[7]);
        //        config.JunkScanForWinInsts = Convert.ToBoolean(regData[8]);

        //        regData = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

        //internal static void SetInvalidShortcutsSettings()
        //{
        //    try
        //    {
        //        byte[] regData = new byte[3] {
        //                Convert.ToByte(config.InShortScanForStartMenu),
        //                Convert.ToByte(config.InShortScanForDesktop),
        //                Convert.ToByte(config.InShortScanForOther)};

        //        Write("INS", regData);
        //        regData = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //internal static bool GetInvalidShortcutsSettings()
        //{
        //    try
        //    {
        //        byte[] regData = (byte[])Read("INS");

        //        if (regData == null)
        //            return false;

        //        config.InShortScanForStartMenu = Convert.ToBoolean(regData[0]);
        //        config.InShortScanForDesktop = Convert.ToBoolean(regData[1]);
        //        config.InShortScanForOther = Convert.ToBoolean(regData[2]);

        //        regData = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //} 


        //internal static void SetAlertSettings()
        //{
        //    byte[] regData = new byte[3] {Convert.ToByte(config.ConfirmBeforeDeletePhotos),
        //                                Convert.ToByte(config.ConfirmShowAutoMarkAlert),
        //                                Convert.ToByte(config.ConfirmMoveFromScanResults)};
        //    Write("ALT", regData);

        //    regData = null;
        //}

        //internal static void SetActivationStatus(bool status)
        //{
        //    byte[] regData = new byte[1] { Convert.ToByte(status) };
        //    Write("Status", regData);

        //    regData = null;
        //}

        //internal static bool GetActivationStatus()
        //{
        //    byte[] regData = (byte[])Read("Status");

        //    if (regData == null)
        //        return false;

        //    bool res = Convert.ToBoolean(regData[0]);

        //    regData = null;
        //    return res;
        //}

        //internal static bool GetAlertSettings()
        //{
        //    byte[] regData = (byte[])Read("ALT");

        //    if (regData == null)
        //        return false;

        //    config.ConfirmBeforeDeletePhotos = Convert.ToBoolean(regData[0]);
        //    config.ConfirmShowAutoMarkAlert = Convert.ToBoolean(regData[1]);
        //    config.ConfirmMoveFromScanResults = Convert.ToBoolean(regData[2]);

        //    regData = null;
        //    return true;
        //}



        //internal static void SetScanExcludedExtensions(bool eraseAll = false)
        //{
        //    //Saving extensions
        //    int count = config.ListExcludedExtensions.Count;
        //    if (count == 0 && !eraseAll)
        //    {
        //        var extensions = Utility.excludedExtensions;
        //        List<string> data = new List<string>();
        //        foreach (var item in extensions)
        //        {
        //            data.Add(Convert.ToInt16(1) + "_" + item);
        //        }
        //        Write("SCNExt", string.Join(";", data.ToArray()));
        //        data.Clear();
        //    }
        //    else
        //    {
        //        List<string> data = new List<string>();
        //        foreach (var item in config.ListExcludedExtensions)
        //        {
        //            data.Add(Convert.ToInt16(item.Status) + "_" + item.Extension);
        //        }
        //        Write("SCNExt", string.Join(";", data.ToArray()));
        //        data.Clear();
        //    }
        //}




        internal static string GetUIArgs()
        {
            string regData = ReadStr("APPARGS");

            if (regData == null)
                return null;

            return regData;
        }

        internal static string GetNagFrom()
        {
            string regData = ReadStr("NagFrom");

            if (regData == null)
                return null;

            return regData;
        }

        internal static void SetNagFrom(string value)
        {
            try
            {
                Write("NagFrom", value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        internal static void SetUIArgs(string value)
        {
            try
            {
                Write("APPARGS", value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 1 = Perform silent quick scan,
        /// 0 = Set default / Quick scan on UI
        /// </summary>
        internal static int GetBGQuickScan()
        {
            try
            {
                string regData = ReadStr("BGQC");

                if (regData == null)
                    return -1;

                return Convert.ToInt16(regData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// 1 = Perform silent quick scan,
        /// 0 = Set default / Quick scan on UI
        /// </summary>
        /// <param name="value"></param>
        internal static void SetBGQuickScan(string value)
        {
            try
            {
                Write("BGQC", value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        #region Obtain DefaultBrowser
        //public static DefaultBrowserEnum GetDefaultBrowser()
        //{
        //    string userChoiceRegistry = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
        //    string progId = null;

        //    DefaultBrowserEnum browser = DefaultBrowserEnum.None;
        //    try
        //    {
        //        using (RegistryKey userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoiceRegistry))
        //        {
        //            if (userChoiceKey == null)
        //            {
        //                browser = DefaultBrowserEnum.None;
        //            }

        //            object progIdValue = userChoiceKey.GetValue("Progid");
        //            if (progIdValue == null)
        //            {
        //                browser = DefaultBrowserEnum.None;
        //            }

        //            progId = progIdValue.ToString();
        //            if (progId.Contains("IE.HTTP"))
        //            {
        //                browser = DefaultBrowserEnum.IE;
        //            }
        //            else if (progId.Contains("FirefoxURL"))
        //            {
        //                browser = DefaultBrowserEnum.FireFox;
        //            }
        //            else if (progId.Contains("ChromeHTML"))
        //            {
        //                browser = DefaultBrowserEnum.Chrome;
        //            }
        //            else if (progId.Contains("OperaStable"))
        //            {
        //                browser = DefaultBrowserEnum.Opera;
        //            }
        //            else if (progId.Contains("SafariHTML"))
        //            {
        //                browser = DefaultBrowserEnum.Safari;
        //            }
        //            else if (progId.Contains("AppXq0fevzme2pys62n3e0fbqa7peapykr8v"))
        //            {
        //                browser = DefaultBrowserEnum.Edge;
        //            }
        //            else
        //            {
        //                browser = DefaultBrowserEnum.None;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        browser = DefaultBrowserEnum.None;
        //    }
        //    return browser;
        //}






        #endregion

        //internal static void SetUpdateSettings()
        //{
        //    try
        //    {
        //        byte[] regData = null;
        //        regData = new byte[2] {Convert.ToByte(config.AutoDBUpdate),
        //                                Convert.ToByte(config.AutoAppUpdate) };

        //        Write("UPDATE", regData);

        //        regData = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //internal static bool GetUpdateSettings()
        //{
        //    try
        //    {
        //        byte[] regData = (byte[])Read("UPDATE");

        //        if (regData == null)
        //            return false;

        //        config.AutoDBUpdate = Convert.ToBoolean(regData[0]);
        //        config.AutoAppUpdate = Convert.ToBoolean(regData[1]);

        //        regData = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

       

        internal static void SetCounts(string subKeyName, string name, string value)
        {
            try
            {
                WriteSubKey(subKeyName, name, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        internal static void SetCountsSSR(string subKeyName, string name, string value)
        {
            try
            {
                WriteSubKeyBackup(subKeyName, name, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static int GetCounts(string subKeyName, string name)
        {
            try
            {
                var tmpItem = ReadSubKey(subKeyName, name);
                if (tmpItem != null)
                {
                    var tmpStr = tmpItem.ToString();
                    if (!string.IsNullOrEmpty(tmpStr))
                    {
                        return Convert.ToInt32(tmpStr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        public static int GetCountsSSR(string subKeyName, string name)
        {
            try
            {
                var tmpItem = ReadSubKeyBackup(subKeyName, name);
                if (tmpItem != null)
                {
                    var tmpStr = tmpItem.ToString();
                    if (!string.IsNullOrEmpty(tmpStr))
                    {
                        return Convert.ToInt32(tmpStr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        //internal static bool GetLastQuickScanDate()
        //{
        //    try
        //    {
        //        var regData = ReadStr("LastQScan");

        //        if (regData == null)
        //            return false;

        //        config.LastQuickScanDone = DateTime.Parse(regData);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

        //internal static void SetLastQuickScanDate()
        //{
        //    try
        //    {
        //        Write("LastQScan", config.LastQuickScanDone.ToString());
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //internal static bool GetRestorePoint()
        //{
        //    try
        //    {
        //        string lastScan = ReadStr("LAST_RESTOREPOINT_ON");

        //        if (lastScan == null)
        //        {
        //            config.LastRestorePointCreated = null;
        //            return false;
        //        }

        //        config.LastRestorePointCreated = DateTime.Parse(lastScan);

        //        //regData = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

        //internal static void SetRestorePoint()
        //{
        //    try
        //    {
        //        Write("LAST_RESTOREPOINT_ON", config.LastRestorePointCreated.ToString());
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //internal static bool GetAppScheduleInfo()
        //{
        //    try
        //    {
        //        var regData = ReadStr("APP_SCHDT");

        //        if (regData == null || string.IsNullOrEmpty(regData))
        //        {
        //            config.AppSchedule = false;
        //            config.AppSchType = SchType.None;
        //            config.AppScheduleDT = null;

        //            return false;
        //        }

        //        var schDetails = regData.Split(new char[] { '|' });
        //        if(schDetails!=null)
        //        {
        //            //Set Type
        //            if (schDetails[0] == "1")
        //                config.AppSchType = SchType.RunOnce;
        //            else if (schDetails[0] == "2")
        //                config.AppSchType = SchType.EveryDay;
        //            else if (schDetails[0] == "3")
        //                config.AppSchType = SchType.EveryWeek;
        //            else
        //                config.AppSchType = SchType.None;

        //            //Set Date
        //            config.AppScheduleDT = DateTime.Parse(schDetails[1]);

        //            //Set Days
        //            config.AppSchDays = Convert.ToInt16(schDetails[2]);

        //            config.AppSchedule = true;
        //        }               

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

        //internal static void SetAppScheduleInfo(SchType SchType, DateTime? SchDt, int Days)
        //{
        //    try
        //    {
        //        string stringSchInfo = string.Empty;
        //        if (SchType != SchType.None)
        //        {
        //            config.AppSchedule = true;
        //            config.AppScheduleDT = SchDt;

        //            stringSchInfo = (int)SchType + "|" + SchDt.ToString() + "|" + Days;
        //        }
        //        Write("APP_SCHDT", stringSchInfo);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        internal static int GetAppState()
        {
            try
            {
                string lastScan = ReadStr("APPSTATE");

                if (lastScan == null)
                    return 0;

                if (lastScan == "1")
                {
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// 1 = Visible
        /// 0 = Minimized / Silent / Closed
        /// </summary>
        internal static void SetAppState(int State)
        {
            try
            {
                Write("APPSTATE", State.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static string GetCtxFiles()
        {
            try
            {
               var tmp =  Read("CTX_CMD");

                if (tmp == null)
                    return null;

                if (string.IsNullOrEmpty(tmp.ToString()))
                    return null;

                return tmp.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return null;
        }

        public static void SetContextSelectedFiles(string cmd)
        {
            try
            {
                Write("CTX_CMD", cmd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        //internal static void SetAppLanguage()
        //{
        //    try
        //    {
        //        Write("APP_LANG", config.AppLanguage);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}


        //internal static void GetAppLanguage()
        //{
        //    try
        //    {
        //        var regData = ReadStr("APP_LANG");
        //        if (regData == null)
        //        {
        //            return;
        //        }

        //        config.AppLanguage = regData;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //}

        //////////FOCUS BEAR
        /// <summary>
        /// 
        /// </summary>
        /// 

        internal static bool GetLangCode()
        {
            try
            {
                var regData = ReadStrGlobal("APP_LANG");

                if (regData == null)
                    return false;

                appSettings.sLangCode = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLangCode - " + ex.Message);
                return false;
            }
        }

        internal static void SetLangCode()
        {
            try
            {
                WriteStrGlobal("APP_LANG", appSettings.sLangCode);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetLangCode()" + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }
        internal static void SetWelcomeAboardingDoneStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("WELCOMEABOARDDONE", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetWelcomeAboardingDoneStatus - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetWelcomeAboardingDoneStatus()
        {
            byte[] regData = (byte[])Read("WELCOMEABOARDDONE");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetOnboardingVideoShownStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("ONBOARDVIDEO", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetOnboardingVideoShownStatus()
        {
            byte[] regData = (byte[])Read("ONBOARDVIDEO");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetisOfficeModeEnabled(bool status)
        {
            try
            {
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("OFFICEMODEENABLED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetisOfficeModeEnabled - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }
        internal static bool GetisOfficeModeEnabled()
        {
            byte[] regData = (byte[])Read("OFFICEMODEENABLED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetFocusModeSetupDone(bool status)
        {
            try
            {
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("FOCUSMODESETUPDONE", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetFocusModeSetupDone - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }
        internal static bool GetFocusModeSetupDone()
        {
            byte[] regData = (byte[])Read("FOCUSMODESETUPDONE");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SaveLoggedInUserInfo()
        {
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] LOGGEDUSERIDData = unicode.GetBytes(appSettings.userAccount.userID);
                WriteGlobal("LOGGEDUSERID", LOGGEDUSERIDData);

                LOGGEDUSERIDData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveLoggedInUserInfo - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetLoggedInUserInfo()
        {
            try
            {
                byte[] LOGGEDUSERIDData = (byte[])ReadGlobal("LOGGEDUSERID");

               if (LOGGEDUSERIDData != null)
                    appSettings.userAccount.userID = Encoding.Unicode.GetString(LOGGEDUSERIDData);

                LOGGEDUSERIDData = null;
 
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLoggedInUserInfo - " + ex.Message);
                //Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetLoggedInStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                WriteGlobal("LOGGED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetLoggedInStatus - " + ex.Message);
             }
        }

        internal static bool GetLoggedInStatus()
        {
            byte[] regData = (byte[])ReadGlobal("LOGGED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void RemoveLoggedInStatus()
        {
            try
            {
                DeleteValueGlobal("LOGGED");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveLoggedInStatus - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }
        internal static void SaveTokens()
        {
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] regAccessTokenData = unicode.GetBytes(appSettings.userAccount.accessToken);
                Write("userat", regAccessTokenData);

                byte[] regRefreshTokenData = unicode.GetBytes(appSettings.userAccount.refreshToken);
                Write("userrt", regRefreshTokenData);

                byte[] regEmail = unicode.GetBytes(appSettings.userAccount.email);
                Write("userem", regEmail);

                Write("userid", appSettings.userAccount.userID);//simple string

                regAccessTokenData = null;
                regRefreshTokenData = null;
                regEmail = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveTokens - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetTokens()
        {
            try
            {
                byte[] regAccessTokenData = (byte[])Read("userat");
                byte[] regRefreshTokenData = (byte[])Read("userrt");
                byte[] regEmail = (byte[])Read("userem");

                appSettings.userAccount.userID = ReadStr("userid");


                if (regAccessTokenData != null)
                    appSettings.userAccount.accessToken = Encoding.Unicode.GetString(regAccessTokenData);

                if (regRefreshTokenData != null)
                    appSettings.userAccount.refreshToken = Encoding.Unicode.GetString(regRefreshTokenData);
              
                if (regEmail != null)
                    appSettings.userAccount.email = Encoding.Unicode.GetString(regEmail);


                regAccessTokenData = null;
                regRefreshTokenData = null;
                regEmail = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTokens - " + ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static bool GetFirstInstallDate()
        {
            try
            {
                var regData = ReadStr("APPINSTALLDT");

                if (regData == null)
                    return false;

                appSettings.FirstAppInstallDT = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetFirstInstallDate - " + ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetFirstInstallDate()
        {
            try
            {
                Write("APPINSTALLDT", Global.ConvertToUnixTime(appSettings.FirstAppInstallDT).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetFirstInstallDate - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetMorningHabitsShownDate()
        {
            try
            {
                var regData = ReadStr("MORNINGHABITSSHOWNDT");

                if (regData == null)
                    return false;

                appSettings.MorningHabitsShownDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetMorningHabitsShownDate - " + ex.Message);
                //Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetMorningHabitsShownDate()
        {
            LogWriter.LogWrite("SetMorningHabitsShownDate()");

            try
            {
                Write("MORNINGHABITSSHOWNDT", Global.ConvertToUnixTime(appSettings.MorningHabitsShownDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetMorningHabitsShownDate - " + ex.Message);
            }
        }

        internal static bool GetFocusSummaryShownTillDate()
        {
            try
            {
                var regData = ReadStr("FOCUSSUMMARYSHOWNDT");

                if (regData == null)
                    return false;

                appSettings.FocusSummaryShownTillDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetFocusSummaryShownTillDate - " + ex.Message);
                return false;
            }
        }

        internal static void SetFocusSummaryShownTillDate()
        {
            try
            {
                Write("FOCUSSUMMARYSHOWNDT", Global.ConvertToUnixTime(appSettings.FocusSummaryShownTillDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetFocusSummaryShownTillDate + " + ex.Message);
            }
        }


        internal static bool GetEveningHabitsShownTillDate()
        {
            try
            {
                var regData = ReadStr("EVENINGHABITSSHOWNDT");

                if (regData == null)
                    return false;

                appSettings.EveningHabitsShownTillDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetEveningHabitsShownTillDate - " + ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetEveningHabitsShownTillDate()
        {
            LogWriter.LogWrite("SetEveningHabitsShownTillDate()");

            try
            {
                Write("EVENINGHABITSSHOWNDT", Global.ConvertToUnixTime(appSettings.EveningHabitsShownTillDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetEveningHabitsShownTillDate - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }


        internal static bool GetShutOffAbortedTillDate()
        {
            //LogWriter.LogWrite(" RM GetShutOffAbortedTillDate()");

            try
            {
                var regData = ReadStr("SHUTOFFABORTEDTILLDATE");

                if (regData == null)
                    return false;

                appSettings.ShutOFFAbortedTillDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetShutOffAbortedTillDate() - " + ex.Message);
               // Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetShutOffAbortedTillDate()
        {
            try
            {
                Write("SHUTOFFABORTEDTILLDATE", Global.ConvertToUnixTime(appSettings.ShutOFFAbortedTillDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetShutOffAbortedTillDate - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        internal static void RemoveShutOffAbortedTillDate()
        {
            try
            {
                DeleteValue("SHUTOFFABORTEDTILLDATE");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveShutOffAbortedTillDate - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static string GetCurrentActivityPostponedID()
        {
            string regData = ReadStr("CurrentActivityPostponedID");

            if (regData == null)
                return null;

            return regData;
        }
        internal static string GetCurrentActivityPostponedTimerVal()
        {
            string regData = ReadStr("CurrentActivityPostponedTimerVal");

            if (regData == null)
                return null;

            return regData;
        }

        internal static void SetPostponedHabitData(string CurrentActivityPostponedID, string CurrentActivityPostponedTimerVal)
        {
            try
            {
                Write("CurrentActivityPostponedID", CurrentActivityPostponedID);
                Write("CurrentActivityPostponedTimerVal", CurrentActivityPostponedTimerVal);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetPostponedHabitData - " + ex.Message);
            }
        }
        internal static void RemovePostponedHabitData()
        {
            LogWriter.LogWrite("RemovePostponedHabitData()");

            try
            {
                DeleteValue(  "CurrentActivityPostponedID");
                DeleteValue( "CurrentActivityPostponedTimerVal");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemovePostponedHabitData - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetDeviceID()
        {
            try
            {
                var regData = ReadStr("DEVICEID");

                if (regData == null)
                    return false;

                appSettings.sDeviceID = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetDeviceID - " + ex.Message);
                //Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SetDeviceID()
        {
            try
            {
                Write("DEVICEID", appSettings.sDeviceID);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetDeviceID()" + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetUserPassword()
        {
            try
            {
                var regData = ReadStr("UP");

                if (regData == null)
                    return false;

                appSettings.sUserPassword = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetUserPassword ex - " + ex.Message);
                return false;
            }
        }

        internal static void SetUserPassword()
        {
            try
            {
                Write("UP", appSettings.sUserPassword);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetUserPassword() ex - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static void SaveUserSettings()
        {
            LogWriter.LogWrite("RM SaveUserSettings()");
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] regSettingsData = unicode.GetBytes(appSettings.sRawUserSettings);
                Write("usersettings", regSettingsData);

                regSettingsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveUserSettings - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetSavedUserSettings()
        {
            LogWriter.LogWrite("RM GetSavedUserSettings()");

            try
            {
                byte[] regSettingsData = (byte[])Read("usersettings");

                if (regSettingsData == null)
                    return false;

                appSettings.sRawUserSettings = Encoding.Unicode.GetString(regSettingsData);

                regSettingsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSavedUserSettings - " + ex.Message);
                //Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static void SaveLocalDeviceSettings()
        {
            LogWriter.LogWrite("RM SaveLocalDeviceSettings()");
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] regSettingsData = unicode.GetBytes(appSettings.sUserLocalDeviceSettings);
                Write("localdevicesettings", regSettingsData);

                regSettingsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveLocalDeviceSettings - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static bool GetLocalDeviceSettings()
        {
            LogWriter.LogWrite("RM GetLocalDeviceSettings()");

            try
            {
                byte[] regSettingsData = (byte[])Read("localdevicesettings");

                if (regSettingsData == null)
                    return false;

                appSettings.sUserLocalDeviceSettings = Encoding.Unicode.GetString(regSettingsData);

                regSettingsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLocalDeviceSettings - " + ex.Message);
                //Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal static bool GetAppDeactivatedTillDate()
        {
            try
            {
                var regData = ReadStr("APPDEACTIVATEDTILLDT");

                if (regData == null)
                    return false;

                appSettings.AppDeactivatedTillDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAppDeactivatedTillDate - " + ex.Message);
                return false;
            }
        }

        internal static void SetAppDeactivatedTillDate()
        {
            LogWriter.LogWrite("SetAppDeactivatedTillDate()");

            try
            {
                Write("APPDEACTIVATEDTILLDT", Global.ConvertToUnixTime(appSettings.AppDeactivatedTillDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetAppDeactivatedTillDate - " + ex.Message);
            }
        }

        internal static void RemoveAppDeactivatedTillDate()
        {
            LogWriter.LogWrite("RemoveAppDeactivatedTillDate()");

            try
            {
                DeleteValue("APPDEACTIVATEDTILLDT");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveAppDeactivatedTillDate - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        internal static void SaveBrainDump()
        {
            LogWriter.LogWrite("RM SaveBrainDump()");
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] regSettingsData = unicode.GetBytes(appSettings.sBrainDump);
                Write("braindump", regSettingsData);

                regSettingsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveBrainDump - " + ex.Message);
            }
        }

        internal static bool GetBrainDump()
        {
            LogWriter.LogWrite("RM GetBrainDump()");

            try
            {
                byte[] regSettingsData = (byte[])Read("braindump");

                if (regSettingsData == null)
                    return false;

                appSettings.sBrainDump = Encoding.Unicode.GetString(regSettingsData);

                regSettingsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetBrainDump - " + ex.Message);
                return false;
            }
        }


    }


    public enum SchType
    {
        None,
        RunOnce,
        EveryDay,
        EveryWeek
    }
}
