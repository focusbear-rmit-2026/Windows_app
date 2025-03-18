
using Focus_Bear.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
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
                }
                catch (NullReferenceException ex)
                {
                    LogWriter.LogWrite(ex.Message);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite(ex.Message);
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
                return false;
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
                    return rootKey.GetValue(value)?.ToString();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (NullReferenceException ex)
            {
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
                res = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                res = false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite(ex.Message);
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


        internal static bool GetGlobalUUID()
        {
            try
            {
                var regData = ReadStrGlobal("GLOBALUUID");

                if (regData == null)
                    return false;

                appSettings.sGlobalUUID = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetGlobalUUID - " + ex.Message);
                return false;
            }
        }

        internal static void SetGlobalUUID()
        {
            try
            {
                WriteStrGlobal("GLOBALUUID", appSettings.sGlobalUUID);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetGlobalUUID()" + ex.Message);
            }
        }
        internal static void RemoveGlobalUUID()
        {
            try
            {
                DeleteValueGlobal("GLOBALUUID");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveGlobalUUID - " + ex.Message);
            }
        }

        internal static void SetGlobalUUIDLinked()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("GLOBALUUID_LINKED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetGlobalUUIDLinked - " + ex.Message);
            }
        }
        internal static bool GetGlobalUUIDLinked()
        {
            byte[] regData = (byte[])Read("GLOBALUUID_LINKED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetMacIDLinked()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("MACID_LINKED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetMacIDLinked - " + ex.Message);
            }
        }
        internal static bool GetMacIDLinked()
        {
            byte[] regData = (byte[])Read("MACID_LINKED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

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

        internal static void SetOnboardingAfterLoginDoneStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("ONBOARDINGAFTERLOGINDONE", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetOnboardingAfterLoginDoneStatus - " + ex.Message);
            }
        }

        internal static bool GetOnboardingAfterLoginDoneStatus()
        {
            byte[] regData = (byte[])Read("ONBOARDINGAFTERLOGINDONE");

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
            }
        }

        internal static bool GetMorningHabitsShownTillDate()
        {
            try
            {
                var regData = ReadStr("MORNINGHABITSSHOWNDT");

                if (regData == null)
                    return false;

                appSettings.MorningHabitsShownTillDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetMorningHabitsShownTillDate - " + ex.Message);
                return false;
            }
        }

        internal static void SetMorningHabitsShownTillDate()
        {
            LogWriter.LogWrite("SetMorningHabitsShownTillDate()");

            appSettings.MorningHabitsShownTillDate = DateTime.Now.AddHours(6); //Default to three hours from now, in case we 

            try
            {
                DateTime now = DateTime.Now;
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString());

                if (now > myStartTime) //Same Day like befoee 12.00 am, so set for next day
                {
                    //Get the morning time and prepare a DT for Evening Habits Shown Till Date
                    myStartTime = myStartTime.AddDays(1);
                    appSettings.MorningHabitsShownTillDate = myStartTime;
                    LogWriter.LogWrite(myStartTime.ToString());
                }
                else //Set for Next Day like morning routine finished after 12.00 am but yet shutoff time not arrieved
                {
                    appSettings.MorningHabitsShownTillDate = myStartTime;//Today's Morning time
                    LogWriter.LogWrite(myStartTime.ToString());
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetMorningHabitsShownTillDate - " + ex.Message);
            }


            try
            {
                Write("MORNINGHABITSSHOWNDT", Global.ConvertToUnixTime(appSettings.MorningHabitsShownTillDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetMorningHabitsShownTillDate - " + ex.Message);
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
            }
        }


        internal static bool GetShutOffAbortedTillDate()
        {

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
                DeleteValue("CurrentActivityPostponedID");
                DeleteValue("CurrentActivityPostponedTimerVal");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemovePostponedHabitData - " + ex.Message);
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
            }
        }

        internal static void SetOfficeModeMessageStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("OFFICEMODEMSG_SHOWN", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetLoggedInStatus - " + ex.Message);
            }
        }

        internal static bool GetOfficeModeMessageStatus()
        {
            byte[] regData = (byte[])Read("OFFICEMODEMSG_SHOWN");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
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
                return false;
            }
        }
        internal static void SaveUserDetailsInfo()
        {
            LogWriter.LogWrite("RM SaveUserDetailsInfo()");
            try
            {
                Encoding unicode = Encoding.Unicode;
                // Convert unicode string into a byte array.  
                byte[] sUserDetailsData = unicode.GetBytes(appSettings.sUserDetailsInfo);
                Write("userdetails", sUserDetailsData);

                sUserDetailsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveUserDetailsInfo - " + ex.Message);
            }
        }

        internal static bool GetSavedUserDetailsInfo()
        {
            LogWriter.LogWrite("RM GetSavedUserDetailsInfo()");

            try
            {
                byte[] sUserDetailsData = (byte[])Read("userdetails");

                if (sUserDetailsData == null)
                    return false;

                appSettings.sUserDetailsInfo = Encoding.Unicode.GetString(sUserDetailsData);

                sUserDetailsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSavedUserDetailsInfo - " + ex.Message);
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

        internal static bool GetHasEditedBlockedURLs()
        {
            try
            {
                var regData = ReadStr("HASEDITEDBLOCKEDURLS");

                if (regData == null)
                    return false;

                appSettings.bHasEditedBlockedURLs = true;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetHasEditedBlockedURLs ex - " + ex.Message);
                return false;
            }
        }

        internal static void SetHasEditedBlockedURLs()
        {
            LogWriter.LogWrite("SetHasEditedBlockedURLs()");

            try
            {
                Write("HASEDITEDBLOCKEDURLS", "1");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetHasEditedBlockedURLs ex - " + ex.Message);
            }
        }

        internal static bool GetApplicationFont()
        {

            try
            {
                var regData = ReadStr("APPFONT");

                if (regData == null)
                    return false;

                appSettings.sSelectedAppFont = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetApplicationFont() - " + ex.Message);
                return false;
            }
        }

        internal static void SetApplicationFont()
        {
            try
            {
                Write("APPFONT", appSettings.sSelectedAppFont);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetApplicationFont - " + ex.Message);
            }
        }

        internal static bool GetSavedOccupation()
        {

            try
            {
                var regData = ReadStr("OCCUPATION");

                if (regData == null)
                    return false;

                appSettings.sSavedOccupationForFirstFocusSession = regData.ToString();

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSavedOccupation() - " + ex.Message);
                return false;
            }
        }

        internal static void SetSavedOccupation()
        {
            try
            {
                Write("OCCUPATION", appSettings.sSavedOccupationForFirstFocusSession);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetSavedOccupation - " + ex.Message);
            }
        }

        internal static void SetSurveyShownStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("SURVEYSHOWN", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
        }

        internal static bool GetSurveyShownStatus()
        {
            byte[] regData = (byte[])Read("SURVEYSHOWN");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetSimplePomodoroTourShownStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("SIMPLEPOMODOROTOURSHOWN", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
        }

        internal static bool GetSimplePomodoroTourShownStatus()
        {
            byte[] regData = (byte[])Read("SIMPLEPOMODOROTOURSHOWN");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void SetAnalyzeSleepAndMoodStatus()
        {
            try
            {
                bool status = true;
                byte[] regData = new byte[1] { Convert.ToByte(status) };
                Write("ANALYZESLEEPANDMOODASKED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
        }

        internal static bool GetAnalyzeSleepAndMoodStatus()
        {
            byte[] regData = (byte[])Read("ANALYZESLEEPANDMOODASKED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static bool IsSuperPomodoroIsRunning()
        {
            if(appSettings.bUseSuperPomodoroMode)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static bool GetDateFor_TotalTimeForDistractingSites()
        {
            try
            {
                var regData = ReadStr("TOTALTIMEFORDISTRACTINGSITES_DATE");

                if (regData == null)
                    return false;

                appSettings.Date_TotalTimeForDistractingSitesToday = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetDateFor_TotalTimeForDistractingSites() - " + ex.Message);
                return false;
            }
        }

        internal static void SetDateFor_TotalTimeForDistractingSites()
        {
            try
            {
                Write("TOTALTIMEFORDISTRACTINGSITES_DATE", Global.ConvertToUnixTime(appSettings.Date_TotalTimeForDistractingSitesToday).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetDateFor_TotalTimeForDistractingSites - " + ex.Message);
            }
        }

        internal static bool GetTotalTimeForDistractingSites()
        {
            try
            {
                var regData = ReadStr("TOTALTIMEFORDISTRACTINGSITES");

                if (regData == null)
                    return false;

                appSettings.nTotalTimeForDistractingSitesToday = int.Parse(regData.ToString());

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTotalTimeForDistractingSites() - " + ex.Message);
                return false;
            }
        }

        internal static void SetTotalTimeForDistractingSites()
        {
            try
            {
                Write("TOTALTIMEFORDISTRACTINGSITES",  appSettings.nTotalTimeForDistractingSitesToday.ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetTotalTimeForDistractingSites - " + ex.Message);
            }
        }

        internal static bool GetTotalTimeForBlockingDeactivated()
        {
            try
            {
                var regData = ReadStr("TOTALTIMEFORBLOCKINGDEACTIVATED");

                if (regData == null)
                    return false;

                appSettings.nTimeSpentBlockingDeactivated = int.Parse(regData.ToString());

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTotalTimeForBlockingDeactivated() - " + ex.Message);
                return false;
            }
        }

        internal static void SetTotalTimeForBlockingDeactivated()
        {
            try
            {
                Write("TOTALTIMEFORBLOCKINGDEACTIVATED", appSettings.nTimeSpentBlockingDeactivated.ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetTotalTimeForBlockingDeactivated - " + ex.Message);
            }
        }

        internal static bool GetTotalTimeForBlockingActivated()
        {
            try
            {
                var regData = ReadStr("TOTALTIMEFORBLOCKING_ACTIVATED");

                if (regData == null)
                    return false;

                appSettings.nTimeSpentBlockingActivated = int.Parse(regData.ToString());

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTotalTimeForBlockingActivated() - " + ex.Message);
                return false;
            }
        }

        internal static void SetTotalTimeForBlockingActivated()
        {
            try
            {
                Write("TOTALTIMEFORBLOCKING_ACTIVATED", appSettings.nTimeSpentBlockingActivated.ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetTotalTimeForBlockingActivated - " + ex.Message);
            }
        }

        internal static bool GetTotalDistractingSiteBlockedToday()
        {
            try
            {
                var regData = ReadStr("TOTALDISTRACTINGSITEBLOCKEDTODAY");

                if (regData == null)
                    return false;

                appSettings.nTotalDistractingSitesBlocked = int.Parse(regData.ToString());

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTotalDistractingSiteBlockedToday() - " + ex.Message);
                return false;
            }
        }

        internal static void SetTotalDistractingSiteBlockedToday()
        {
            try
            {
                Write("TOTALDISTRACTINGSITEBLOCKEDTODAY", appSettings.nTotalDistractingSitesBlocked.ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetTotalDistractingSiteBlockedToday - " + ex.Message);
            }
        }

        internal static bool GetSelectedMorningHabitsForToday()
        {
            LogWriter.LogWrite("RM GetSelectedMorningHabitsForToday()");

            try
            {
                byte[] SelectedMorningHabitsData = (byte[])Read("SelectedMorningHabitsData");

                if (SelectedMorningHabitsData == null)
                    return false;

                string sSelectedMorningHabitsData = Encoding.Unicode.GetString(SelectedMorningHabitsData);
                // Deserialize the JSON string back into an array of user details
                appSettings.arrAllMorningHabits = JsonConvert.DeserializeObject<habit[]>(sSelectedMorningHabitsData).ToList();

                sSelectedMorningHabitsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSavedUserSettings - " + ex.Message);
                return false;
            }


        }
        internal static void SaveSelectedMorningHabitsForToday()
        {
            LogWriter.LogWrite("RM SaveSelectedMorningHabitsForToday()");
            try
            {
                // Serialize the array of user details to a JSON string
                string jsonSelectedMorningHabits = JsonConvert.SerializeObject(appSettings.arrAllMorningHabits);

                Encoding unicode = Encoding.Unicode;
                // Convert the JSON string into a byte array.
                byte[] sSelectedMorningHabitsData = unicode.GetBytes(jsonSelectedMorningHabits);

                // Save the byte array (you can implement your Write method accordingly)
                Write("SelectedMorningHabitsData", sSelectedMorningHabitsData);

                SetSelectedMorningHabitsDate(); //Also set the date for which the selected morning habits will be applicable

                sSelectedMorningHabitsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveSelectedMorningHabitsForToday - " + ex.Message);
            }
        }

        internal static bool GetSelectedMorningHabitsDate()
        {
            try
            {
                var regData = ReadStr("SELECTEDMORNINGHABITSDATE");

                if (regData == null)
                    return false;

                appSettings.SelectedMorningHabitsDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSelectedMorningHabitsDate - " + ex.Message);
                return false;
            }
        }

        internal static void SetSelectedMorningHabitsDate()
        {
            LogWriter.LogWrite("SetSelectedMorningHabitsDate()");

            appSettings.SelectedMorningHabitsDate = DateTime.Now;
            try
            {
                Write("SELECTEDMORNINGHABITSDATE", Global.ConvertToUnixTime(appSettings.SelectedMorningHabitsDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetSelectedMorningHabitsDate - " + ex.Message);
            }
        }

        internal static bool GetSelectedEveningHabitsForToday()
        {
            LogWriter.LogWrite("RM GetSelectedEveningHabitsForToday()");

            try
            {
                byte[] SelectedEveningHabitsData = (byte[])Read("SelectedEveningHabitsData");

                if (SelectedEveningHabitsData == null)
                    return false;

                string sSelectedEveningHabitsData = Encoding.Unicode.GetString(SelectedEveningHabitsData);
                // Deserialize the JSON string back into an array of user details
                appSettings.arrAllEveningHabits = JsonConvert.DeserializeObject<habit[]>(sSelectedEveningHabitsData).ToList();

                sSelectedEveningHabitsData = null;

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSelectedEveningHabitsForToday - " + ex.Message);
                return false;
            }


        }
        internal static void SaveSelectedEveningHabitsForToday()
        {
            LogWriter.LogWrite("RM SaveSelectedEveningHabitsForToday()");
            try
            {
                // Serialize the array of user details to a JSON string
                string jsonSelectedEveningHabits = JsonConvert.SerializeObject(appSettings.arrAllEveningHabits);

                Encoding unicode = Encoding.Unicode;
                // Convert the JSON string into a byte array.
                byte[] sSelectedEveningHabitsData = unicode.GetBytes(jsonSelectedEveningHabits);

                // Save the byte array (you can implement your Write method accordingly)
                Write("SelectedEveningHabitsData", sSelectedEveningHabitsData);

                SetSelectedEveningHabitsDate(); //Also set the date for which the selected morning habits will be applicable

                sSelectedEveningHabitsData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SaveSelectedEveningHabitsForToday - " + ex.Message);
            }
        }

        internal static bool GetSelectedEveningHabitsDate()
        {
            try
            {
                var regData = ReadStr("SELECTEDEVENINGHABITSDATE");

                if (regData == null)
                    return false;

                appSettings.SelectedEveningHabitsDate = Global.ConvertFromUnixTimeToDT(Convert.ToInt64(regData));

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSelectedEveningHabitsDate - " + ex.Message);
                return false;
            }
        }

        internal static void SetSelectedEveningHabitsDate()
        {
            LogWriter.LogWrite("SetSelectedEveningHabitsDate()");

            appSettings.SelectedEveningHabitsDate = DateTime.Now;
            try
            {
                Write("SELECTEDEVENINGHABITSDATE", Global.ConvertToUnixTime(appSettings.SelectedEveningHabitsDate).ToString());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetSelectedEveningHabitsDate - " + ex.Message);
            }
        }


        /// //////////////////////////////////////////////////////////////////////////////////////
        //Focus bear Time Tracker Related
        public static string GetRegistryGlobalPathFBTT()
        {
            return "Software\\Focus Bear Time Tracker";
        }
        private static object ReadGlobalFBTT(string value)
        {
            try
            {
                string root = GetRegistryGlobalPathFBTT();
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
                LogWriter.LogWrite("ReadGlobalFBTT RM - " + ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite("ReadGlobalFBTT RM - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadGlobalFBTT RM - " + ex.Message);
            }
            return null;
        }

        private static void WriteGlobalFBTT(string value, byte[] data)
        {
            try
            {
                string root = GetRegistryGlobalPathFBTT();
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(root))
                {
                    rootKey.SetValue(value, data, RegistryValueKind.Binary);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite("WriteGlobalFBTT RM - " + ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite("WriteGlobalFBTT RM - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WriteGlobalFBTT RM - " + ex.Message);
            }
        }

        private static bool DeleteValueGlobalFBTT(string value)
        {
            try
            {
                string root = GetRegistryGlobalPathFBTT();
                using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey(root, true))
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
                LogWriter.LogWrite("DeleteValueGlobalFBTT RM - " + ex.Message);
            }
            catch (NullReferenceException ex)
            {
                LogWriter.LogWrite("DeleteValueGlobalFBTT RM - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteValueGlobalFBTT RM - " + ex.Message);

            }
            return false;
        }
        internal static void SetFocusBearTimeTrackerStatus()
        {
            try
            {
                byte[] regData = new byte[1] { Convert.ToByte(appSettings.bFocusBearTimeTrackerEnabled) };
                WriteGlobalFBTT("FBTTENABLED", regData);

                regData = null;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetFocusBearTimeTrackerStatus - " + ex.Message);
            }
        }

        internal static bool GetFocusBearTimeTrackerStatus()
        {
            byte[] regData = (byte[])ReadGlobalFBTT("FBTTENABLED");

            if (regData == null)
                return false;

            bool res = Convert.ToBoolean(regData[0]);

            regData = null;
            return res;
        }

        internal static void RemoveFocusBearTimeTrackerStatus()
        {
            try
            {
                DeleteValueGlobalFBTT("FBTTENABLED");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteValueGlobalFBTT - " + ex.Message);
            }
        }

        public static void AddToStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue("Focus Bear", "\"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\""); ;
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
