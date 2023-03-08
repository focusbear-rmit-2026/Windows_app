using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Focus_Bear.Models;
using Interop.UIAutomationClient;

namespace Focus_Bear.Utils
{
    public static class Global
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }
        public static List<string> BROWSERS = new List<string> { "chrome", "firefox", "opera", "msedge" };

        public static void EnableSystemSleep()
        {
            // allow sleep
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
        public static void DisableSystemSleep()
        {
            // types of sleep

            // disable monitor sleep
            // SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS;

            // disable monitor sleep and keep system awake
            // SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED);

            // disable monitor sleep and keep system awake and prevent idle to sleep
            SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }
        public static string ToFileSizeString(long byteCount)
        {
            string[] suf = { " B", " KB", " MB", " GB", " TB", " PB", " EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static int Round(this int i, int nearest)
        {
            if (nearest <= 0 || nearest % 10 != 0)
                throw new ArgumentOutOfRangeException("nearest", "Must round to a positive multiple of 10");

            return (i + 5 * nearest / 10) / nearest * nearest;
        }

        public static bool IsValidEmailAddress(this string s)
        {
           
            try
            {
                MailAddress m = new MailAddress(s);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            //Regex regex = new Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
            //Regex regex = new Regex(@"[A-Z0-9a-z._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}"); //Mac apps

            //return regex.IsMatch(s);
        }

        //"[A-Z0-9a-z._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}"
        public static long ConvertToUnixTime(DateTime datetime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(datetime - sTime).TotalSeconds;
        }

        public static DateTime ConvertFromUnixTimeToDT(long unixtime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return sTime.AddSeconds(unixtime);
        }

        private static NativeFileDelete.FILEOP_FLAGS flags = new NativeFileDelete.FILEOP_FLAGS();
        private static NativeFileDelete obj = new NativeFileDelete();
        public static bool DeleteFile(string filePath, bool shouldMoveToRecycleBin)
        {
            bool res = false;
            try
            {
                //Using VB - Managed
                //if (File.Exists(filePath))
                //{
                //    deleteOptions = shouldMoveToRecycleBin ? VB.RecycleOption.SendToRecycleBin : VB.RecycleOption.DeletePermanently;
                //    VB.FileSystem.DeleteFile(filePath, VB.UIOption.OnlyErrorDialogs, deleteOptions, VB.UICancelOption.ThrowException);
                //    res = true;
                //}

                //Using Native SSH file operation

                //Set Flags

                flags.FOF_SILENT = true;
                flags.FOF_NOERRORUI = true;
                flags.FOF_NOCONFIRMATION = true;

                if (shouldMoveToRecycleBin)
                    flags.FOF_ALLOWUNDO = true;

                //Delete File                
                obj.pFrom = filePath;
                obj.fFlags = flags;
                if (obj.Execute())
                    return true;

                //if (!File.Exists(filePath))
                //    res = true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteFile - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return res;
        }
        public static string GetMacAddress()
        {
            string mac = null;
            using (ManagementObjectSearcher query = new ManagementObjectSearcher(@"select name, macaddress from Win32_NetworkAdapter where netconnectionid != NULL and macaddress != NULL and Manufacturer != 'Microsoft' AND NOT PNPDeviceID LIKE 'ROOT\\%'"))
            {
                ManagementObjectCollection queryCollection = query.Get();
                foreach (ManagementObject mo in queryCollection)
                {
                    mac = mo["MacAddress"].ToString();
                    break;
                }
            }
            return mac;
        }

        public static string Base64Encode(string text)
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            return System.Convert.ToBase64String(textBytes);
        }
        public static string Base64Decode(string base64)
        {
            var base64Bytes = System.Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(base64Bytes);
        }

    }

    public struct SKIPPED_ACTIVITY
    {
        public string id;
        public bool SKIP_CANNOT_DO;
        public bool SKIP_ALREADY_DONE;

        public SKIPPED_ACTIVITY(string id, bool SKIP_CANNOT_DO,   bool SKIP_ALREADY_DONE)
        {
            this.id = id;
            this.SKIP_CANNOT_DO = SKIP_CANNOT_DO;
            this.SKIP_ALREADY_DONE = SKIP_ALREADY_DONE;
        }
    }

    public enum CurrentActivityType
    {
        NONE = 0,
        MORNING_HABITS = 1,
        BREAK_ACTIVITY = 2,
        EVENING_HABITS = 3,
        SHUT_OFF = 4,
    }

    public struct habit
    {
        public string id;
        public string name;
        public int durationSeconds;
        public List<string> videoUrls;
        public bool logQuantity;
        public string logQuantityQuestion;
        public bool openSpecificApps;
        public List<string> allowedUrls;
        public List<string> allowedApps;
        public string allowedFocusMode;
        public string activity_sequence_id;


        public habit(string id, string name, int durationSeconds, List<string> videoUrls, bool logQuantity, string logQuantityQuestion, bool openSpecificApps, List<string> allowedUrls, List<string> allowedApps, string allowedFocusMode, string activity_sequence_id)
        {
            this.id = id;
            this.name = name;
            this.durationSeconds = durationSeconds;
            this.videoUrls = videoUrls;
            this.logQuantity = logQuantity;
            this.logQuantityQuestion = logQuantityQuestion;
            this.openSpecificApps = openSpecificApps;
            this.allowedUrls = allowedUrls;
            this.allowedApps = allowedApps;
            this.allowedFocusMode = allowedFocusMode;
            this.activity_sequence_id = activity_sequence_id;
        }
    }

    public struct breakActivity
    {
        public string id;
        public string name;
        public int durationSeconds;
        public List<string> videoUrls;
        public bool logQuantity;
        public string logQuantityQuestion;
        public bool openSpecificApps;
        public List<string> allowedUrls;
        public List<string> allowedApps;
        public string allowedFocusMode;
        public string activity_sequence_id;

        public bool IncludeInEveryBreak;
        public bool isRandomChoice;
        public List<activityChoice> choices;
        public bool isOfficeFriendly;
        public string logQuantityType;


        public breakActivity(string id, string name, int durationSeconds, List<string> videoUrls, bool logQuantity, string logQuantityQuestion, bool openSpecificApps, List<string> allowedUrls, List<string> allowedApps, string allowedFocusMode, string activity_sequence_id, bool IncludeInEveryBreak, bool isRandomChoice, List<activityChoice> choices, bool isOfficeFriendly, string logQuantityType)
        {
            this.id = id;
            this.name = name;
            this.durationSeconds = durationSeconds;
            this.videoUrls = videoUrls;
            this.logQuantity = logQuantity;
            this.logQuantityQuestion = logQuantityQuestion;
            this.openSpecificApps = openSpecificApps;
            this.allowedUrls = allowedUrls;
            this.allowedApps = allowedApps;
            this.allowedFocusMode = allowedFocusMode;
            this.activity_sequence_id = activity_sequence_id;
            this.IncludeInEveryBreak = IncludeInEveryBreak;
            this.isRandomChoice = isRandomChoice;
            this.choices = choices;
            this.isOfficeFriendly = isOfficeFriendly;
            this.logQuantityType = logQuantityType;

        }
    }

    public struct activityChoice
    {
        public string id;
        public string name;
        public List<string> videoUrls;
        public string logQuantityQuestion;
        public bool logQuantity;
        public string logQuantityType;

        public activityChoice(string id, string name, List<string> videoUrls, string logQuantityQuestion, bool logQuantity, string logQuantityType)
        {
            this.id = id;
            this.name = name;
            this.videoUrls = videoUrls;
            this.logQuantityQuestion = logQuantityQuestion;
            this.logQuantity = logQuantity;
            this.logQuantityType = logQuantityType;
        }
    }

    public struct LastShownActivityChoice
    {
        public string ActivityId;
        public string ChoiceId;

        public LastShownActivityChoice(string ActivityId, string ChoiceId)
        {
            this.ActivityId = ActivityId;
            this.ChoiceId = ChoiceId;
        }
    }

    public struct LogEntry
    {
        public string sLogType;
        public string sTimeStamp;
        public string sActivityName;
        public string sDurationInSeconds;
        public string sLogQuantityQuestion;
        public string sQuantityLogged;
        public string sStartTime;
        public string sWorkAchieved;
        public string sDistractionsEncountered;
        public string sAbortReason;
        public string sFeedback;

        public LogEntry(string sLogType, string sTimeStamp, string sActivityName, string sDurationInSeconds, string sLogQuantityQuestion, string sQuantityLogged, string sStartTime, string sWorkAchieved, string sDistractionsEncountered, string sAbortReason, string sFeedback)
        {
            this.sLogType = sLogType;
            this.sTimeStamp = sTimeStamp;
            this.sActivityName = sActivityName;
            this.sDurationInSeconds = sDurationInSeconds;
            this.sLogQuantityQuestion = sLogQuantityQuestion;
            this.sQuantityLogged = sQuantityLogged;
            this.sStartTime = sStartTime;
            this.sWorkAchieved = sWorkAchieved;
            this.sDistractionsEncountered = sDistractionsEncountered;
            this.sAbortReason = sAbortReason;
            this.sFeedback = sFeedback;
        }
    }

    public class FocusMode : INotifyPropertyChanged
    {
        public string id;
        public string _name;
        public string name
        {
            get { return _name; }
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnChanged("name");
                }
            }
        }


        //public List<string> arrAllowedApps; //wont use this as this is for mac apps
        public ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();


        //Metadata components:

        public ObservableCollection<string> arrBlockedUrls = new ObservableCollection<string>();

        //Metadata part
        public ObservableCollection<AppDetails> arrAllowedAppsWin = new ObservableCollection<AppDetails>();

        public string color { get; set; }

        private bool _isOn = false;
        public bool isOn
        {
            get { return _isOn; }
            set
            {
                if (value != _isOn)
                {
                    _isOn = value;
                    OnChanged("isOn");
                }
            }
        }

        public bool isAllowedAllApps;
        public bool isAllowedListApproach;
        ////public bool isAllowedAllUrls;
        ////public bool isBlockedAllUrls;
        public bool isDefault;
        public bool isMeetings;
        public bool isLockedFocus;

        public FocusMode()
        {

        }
        public FocusMode(string id, string name, ObservableCollection<string> allowedUrls, ObservableCollection<string> arrBlockedUrls, ObservableCollection<AppDetails> allowedAppsWin, string color, bool isOn, bool isAllowedAllApps, bool isAllowedListApproach, bool isDefault, bool isMeetings, bool isLockedFocus)
        {
            this.id = id;
            this.name = name;
            this.arrAllowedUrls = allowedUrls;
            this.arrBlockedUrls = arrBlockedUrls;
            this.arrAllowedAppsWin = allowedAppsWin;
            this.color = color;
            this.isOn = isOn;
            this.isAllowedAllApps = isAllowedAllApps;
            this.isAllowedListApproach = isAllowedListApproach;
            ////this.isAllowedAllUrls = isAllowedAllUrls;
            ////this.isBlockedAllUrls = isBlockedAllUrls;
            this.isDefault = isDefault;
            this.isMeetings = isMeetings;
            this.isLockedFocus = isLockedFocus;

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

    public class DistractingURL
    {
        private bool isItemChecked;

        public string sURL { get; set; }  
        public bool IsItemChecked
        {
            get { return isItemChecked; }
            set
            {
                isItemChecked = value;
                OnChanged("IsItemChecked");
            }
        }

        public DistractingURL()
        {
           
        }
        public DistractingURL(string sURL, bool bChecked)
        {
            this.sURL = sURL;
            this.IsItemChecked = bChecked;
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

}
