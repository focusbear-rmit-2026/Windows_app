
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shell32;

namespace Focus_Bear.Models
{
    
    public class AppDetails
    {
        private bool isSelected;

        public string AppName { get; set; } //NEED TO show the App Name to the user
        public string DisplayIcon
        {
            get => displayIcon;
            set { displayIcon = value; OnChanged("DisplayIcon"); }
        }
        private string displayIcon;
        public string AppInstallLocation { get; set; }//NEED TO get all executables
  
        public List<string> ExeNames= new List<string>(); //All exes list for a selected app and it install location folder
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                OnChanged("IsSelected");
            }
        }

        public AppDetails()
        {
            AppInstallLocation = "";
            DisplayIcon = "";
        }
        public AppDetails(string AppName, string ExeName)
        {
            this.AppName = AppName;
            this.ExeNames.Add(ExeName);
            AppInstallLocation = "";
            DisplayIcon = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
    class AppsEngine
    {


        AppSettings appSettings = AppSettings.GetInstance;
       
        public AppsEngine(/*CancellationToken token, ManualResetEventSlim resetEventSlim*/)
        {
        }

        public async void GetData()
        {
            // Off the UI thread (it resolves every Start menu shortcut, ~0.4 s, and ran inside the start-up chain), on an
            // STA thread of its own because Shell32's shortcut objects need one.
            List<AppDetails> StartMenuApps = await OnStaThread(GetStartMenuItems);

            LogWriter.LogWrite("GetStartMenuItems() found items - " + StartMenuApps.Count().ToString());
            
            appSettings.InstalledApps.Clear();
            appSettings.InstalledApps = await GetApps();

            LogWriter.LogWrite("InstalledApps found items - " + appSettings.InstalledApps.Count().ToString());

            //Now we need to combine the startup apps with the Installed apps to make a final list
            //WE will keep removing from Start menu apps array if we find the same app name
            foreach (AppDetails sApp in StartMenuApps)
            {
                var listItems = appSettings.InstalledApps.Where(i => i.AppName == sApp.AppName);
                if (listItems.Count() == 0)
                    appSettings.InstalledApps.Add(sApp);
            }

            appSettings.InstalledApps = appSettings.InstalledApps.OrderBy(i => i.AppName).ToList();

            await DetectIF_ChromeInstalled();

        }
        private static Task<T> OnStaThread<T>(Func<T> work)
        {
            var done = new TaskCompletionSource<T>();
            var thread = new System.Threading.Thread(() =>
            {
                try { done.SetResult(work()); }
                catch (Exception ex) { done.SetException(ex); }
            }) { IsBackground = true, Name = "Start menu apps" };
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            return done.Task;
        }

        private Task DetectIF_ChromeInstalled()
        {
            return Task.Run(() =>
            {
                try
                {
                    string appPath = @"\Google\Chrome\Application\chrome.exe";
                    string progFiles32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + appPath;
                    string progFiles64 = Environment.ExpandEnvironmentVariables(@"%systemdrive%\Program Files") + appPath;

                    if (File.Exists(progFiles32))
                    {
                        var app = new AppDetails();
                        app.AppName = "Google Chrome";
                        app.DisplayIcon = progFiles32;
                        app.ExeNames.Add(Path.GetFileName(progFiles32));
                        var listItems = appSettings.InstalledApps.Where(i => i.AppName == app.AppName);
                        if (listItems.Count() == 0)
                            appSettings.InstalledApps.Add(app);
                    }
                    else if (File.Exists(progFiles64))
                    {
                        var app = new AppDetails();
                        app.AppName = "Google Chrome";
                        app.DisplayIcon = progFiles64;
                        app.ExeNames.Add(Path.GetFileName(progFiles64));
                        var listItems = appSettings.InstalledApps.Where(i => i.AppName == app.AppName);
                        if (listItems.Count() == 0)
                            appSettings.InstalledApps.Add(app);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            });
        }
        public static string GetShortcutTargetFile(string shortcutFilename)
        {
            try
            {
                string pathOnly = System.IO.Path.GetDirectoryName(shortcutFilename);
                string filenameOnly = System.IO.Path.GetFileName(shortcutFilename);

                Shell shell = new Shell();
                Folder folder = shell.NameSpace(pathOnly);
                FolderItem folderItem = folder.ParseName(filenameOnly);
                if (folderItem != null)
                {
                    Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
                    return link.Path;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetShortcutTargetFile EX-" + ex.Message);
            }

            return string.Empty;
        }

        public List<AppDetails> GetStartMenuItems()
        {
            LogWriter.LogWrite("GetStartMenuItems()");

            List<AppDetails> StartMenuApps = new List<AppDetails>();
            //GEt from start menu first
   
            try
            {


                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);//% AppData %\Microsoft\Windows\Start Menu\Programs*/
                startMenu = startMenu + "\\Programs";

                //Scan the start menu folders for link files
                string[] files;
                files = Directory.GetFiles(startMenu, "*.lnk", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    foreach (string sFileName in files)
                    {
                        Console.WriteLine(sFileName);
                        if (sFileName.ToLower().Contains("uninstall"))
                        {
                            continue;
                        }

                        //get the app and check if its an exe
                        string sApp = GetShortcutTargetFile(sFileName);
                        if (sApp.Length > 0 && (".exe" == sApp.Substring(sApp.Length - 4)))
                        {
                            //If its an exe file, we need to add this file to the apps list

                            var app = new AppDetails();
                            app.AppName = Path.GetFileName(sFileName).Replace(".lnk", "");
                            app.DisplayIcon = sApp;
                            app.ExeNames.Add(Path.GetFileName(sApp));
                            StartMenuApps.Add(app);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetStartMenuItems() EX-" + ex.Message);
            }
            return StartMenuApps;
            ///////////
        }
       
 
        public async Task<List<AppDetails>> GetApps()
        {
            //Obtain app data
            return await Task.Run(() =>
            {
                LogWriter.LogWrite("AppDetails - GetApps() Task.Run");

                List<AppDetails> apps = new List<AppDetails>();
              
                try
                {
                    string root = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                    //Obtaining 64-bit Processes
                    LogWriter.LogWrite("AppDetails - fetch 64 bit apps");                    
                    if (Environment.Is64BitOperatingSystem)
                    {
                        ReadSubKeys(ref apps, RegistryHive.CurrentUser, root, true);

                        ReadSubKeys(ref apps, RegistryHive.LocalMachine, root, true);
                    }

                    //Obtaining 32-bit Processes
                    LogWriter.LogWrite("AppDetails - fetch 32 bit apps");
                    ReadSubKeys(ref apps, RegistryHive.CurrentUser, root, false);

                    ReadSubKeys(ref apps, RegistryHive.LocalMachine, root, false);

                    return apps;
                }
                catch (OperationCanceledException ex)
                {
                    LogWriter.LogWrite("AppDetails EX-" + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogWriter.LogWrite("AppDetails EX-" + ex.Message);
                }
                catch (NullReferenceException ex)
                {
                    LogWriter.LogWrite("AppDetails EX-" + ex.Message);
                }
                return apps;
            });
        }

       

        private void ReadSubKeys(ref List<AppDetails> apps, RegistryHive RegHive, string root, bool Is64Bit)
        {
            using (RegistryKey key = RegistryKey.OpenBaseKey(RegHive, Is64Bit ? RegistryView.Registry64 : RegistryView.Registry32))
            {
                RegistryKey keySoftware = key.OpenSubKey(root);
                var subkeys = keySoftware.GetSubKeyNames();
                foreach (string subKey in subkeys)
                {
                    try
                    {
                     
                        using (RegistryKey childSubKey = keySoftware.OpenSubKey(subKey))
                        {
                            var valDisplayName = FilterKeyData(childSubKey, "DisplayName");
                            var valInstallLocation = FilterKeyData(childSubKey, "InstallLocation");
                            var valDisplayIcon = FilterKeyData(childSubKey, "DisplayIcon");
          
                            if (!string.IsNullOrEmpty(valDisplayIcon))
                            {
                                //In case path contains ',' then only obtain path before ','
                                if (valDisplayIcon.Contains(','))
                                {
                                    valDisplayIcon = valDisplayIcon.Substring(0, valDisplayIcon.IndexOf(","));
                                }
                            }

                            if (!string.IsNullOrEmpty(valDisplayName) && !valDisplayName.Contains("Focus Bear") )
                            {
                                var IsSystemComponent = CheckIfComponent(childSubKey, "SystemComponent");
                                var IsWindowsComponent = CheckIfComponent(childSubKey, "WindowsComponent");
                                

                                if (!IsSystemComponent && !IsWindowsComponent)
                                {

                                    if (!string.IsNullOrEmpty(valInstallLocation) && Directory.Exists(valInstallLocation)) //Install location is must for us
                                    {
                                        var ifAlreadyExist = apps.FirstOrDefault(a => a.AppName.ToLower() == valDisplayName.Trim().ToLower());

                                        if (ifAlreadyExist == null)
                                        {
                                            var app = new AppDetails();
                                            app.AppName = valDisplayName.Trim();

                                            if (valDisplayIcon != null)
                                                app.DisplayIcon = valDisplayIcon.Trim();

                                            app.AppInstallLocation = valInstallLocation;

                                            string[] files;
                                            files = Directory.GetFiles(valInstallLocation, /*process +*/ "*.exe", SearchOption.AllDirectories);
                                            if (files.Length > 0)
                                            {
                                                foreach (string sFileName in files)
                                                {
                                                    app.ExeNames.Add(Path.GetFileName(sFileName));
                                                }
                                            }
                                            apps.Add(app);

                                        }
                                    }
                                    
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        LogWriter.LogWrite("AppDetails ReadSubKeys EX-" + ex.Message);
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("AppDetails ReadSubKeys EX-" + ex.Message);
                        continue;
                    }
                }
            }
        }

       
        private bool CheckIfComponent(RegistryKey childSubKey, string Key)
        {
            bool isComponent = false;
            var valKeyData = childSubKey.GetValue(Key);
            if (valKeyData != null)
            {
                isComponent = Convert.ToBoolean(childSubKey.GetValue(Key));
            }
            return isComponent;
        }

        private void FilterModifyPath(RegistryKey childSubKey, ref string valUninstallString, string Key)
        {
            var valKeyData = childSubKey.GetValue(Key);
            if (valUninstallString == null && valKeyData != null)
            {
                var tmpModifyPath = valKeyData.ToString();
                if (!string.IsNullOrEmpty(tmpModifyPath))
                {
                    valUninstallString = tmpModifyPath;
                }
            }
        }

        private string FilterKeyData(RegistryKey childSubKey, string Key)
        {
            string keyData = null;
            var valKeyData = childSubKey.GetValue(Key);
            if (valKeyData != null)
            {
                if (!string.IsNullOrEmpty(valKeyData.ToString()))
                {
                    keyData = valKeyData.ToString();
                }
            }
            return keyData;
        }

        

        private string filterPath(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                if (filePath.Contains("-") && filePath.Contains("\""))
                {
                    filePath = filePath.Substring(0, filePath.IndexOf("\"", 1));
                }

                if (filePath.Contains(","))
                    filePath = filePath.Replace(",", "");

                if (filePath.Contains("\""))
                    filePath = filePath.Replace("\"", "");                
            }
            return filePath;
        }

       
        private string GetFilePath(RegistryKey childSubKey, object valInstallLocation, string uninstallPath, bool Is64Bitref)
        {
            string filePath = null;

            //Checking if Display Icon Path exists
            var valIcon = childSubKey.GetValue("DisplayIcon");
            if (valIcon != null)
            {
                var tmpPath = childSubKey.GetValue("DisplayIcon").ToString();
                if (!string.IsNullOrEmpty(tmpPath))
                {
                    //In case path contains ',' then only obtain path before ','
                    if (tmpPath.Contains(','))
                    {
                        tmpPath = tmpPath.Substring(0, tmpPath.IndexOf(","));
                    }
                    filePath = filterPath(tmpPath);

                    //In case Dispay Icon contains .ico file extension then try obtain  Uninstall path 
                    if(tmpPath.ToLower().Contains(".ico"))
                    {
                        filePath = null;
                    }
                }
            }

            //if Uninstall string contains any parameters along with uninstall path
            if(filePath == null)
            {
                //If uninstall path contains any params
                filePath = uninstallPath;
                if (filePath.Contains("-") && filePath.Contains("\""))
                {
                    filePath = filePath.Substring(0, filePath.IndexOf("\"", 1));
                }

                if (filePath.Contains(","))
                    filePath = filePath.Replace(",", "");

                if (filePath.Contains("\""))
                    filePath = filePath.Replace("\"", "");
            }

            //If FilePath is empty then make use of Uninstall Path
            if (filePath != null)
            {
                if (!File.Exists(filePath))
                {
                    filePath = filterPath(uninstallPath);
                }
            }
                        
            if (filePath != null && filePath.Contains("\""))
            {
                filePath = filePath.Replace("\"", "");
            }            

            return filePath;
        }
         
    }
     
}
