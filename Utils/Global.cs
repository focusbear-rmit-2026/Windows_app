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
using System.Windows.Media.Imaging;
using Focus_Bear.Models;
using Interop.UIAutomationClient;
using Microsoft.Win32;

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
            try
            {
                using (ManagementObjectSearcher query = new ManagementObjectSearcher(@"select name, macaddress from Win32_NetworkAdapter where netconnectionid != NULL and macaddress != NULL and Manufacturer != 'Microsoft' AND NOT PNPDeviceID LIKE 'ROOT\\%'"))
                {
                    ManagementObjectCollection queryCollection = query.Get();
                    foreach (ManagementObject mo in queryCollection)
                    {
                        mac = mo["MacAddress"].ToString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                mac = System.Guid.NewGuid().ToString();
                Debug.WriteLine(ex.Message);
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

        public static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
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
    public enum BLOCKINGSCHEDULE
    {
        UNTIL_I_FINISH_WORK = 0,
        WHEN_STARTING_FOCUS_SESSION = 1,
        CUSTOM_SCHEDULE = 2,
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
        public int durationSeconds { get; set; }
        public List<string> videoUrls;
        public bool logQuantity;
        public string logQuantityQuestion;
        public bool openSpecificApps;
        public List<string> allowedUrls;
        public List<string> allowedApps;
        public string allowedFocusMode;
        public string activity_sequence_id;
        public List<QuantityQuestion> logQuantityQuestions;
        public string completion_requirements;
        public bool useTimer;
        public bool show_saved_distracting_websites; //This means this is the relax activity, so nothing should be blocked
        public bool bHighPriorityActivity;
        public List<string> DaysOfWeek;
        public string sCutOffTime;
        public habit(string id, string name, int durationSeconds, List<string> videoUrls, bool logQuantity, string logQuantityQuestion, bool openSpecificApps, List<string> allowedUrls, List<string> allowedApps, string allowedFocusMode, string activity_sequence_id, List<QuantityQuestion> logQuantityQuestions, string completion_requirements, bool useTimer, bool show_saved_distracting_websites, bool bHighPriorityActivity, List<string> DaysOfWeek, string sCutOffTime)
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
            this.logQuantityQuestions = logQuantityQuestions;
            this.completion_requirements = completion_requirements;
            this.useTimer = useTimer;
            this.show_saved_distracting_websites = show_saved_distracting_websites;
            this.bHighPriorityActivity = bHighPriorityActivity;
            this.DaysOfWeek = DaysOfWeek;
            this.sCutOffTime = sCutOffTime;
        }
    }

    public class HabitListSelectable : INotifyPropertyChanged
    {
        private bool _bIsSelected;

        public bool bIsSelected
        {
            get { return _bIsSelected; }
            set
            {
                if (value != _bIsSelected)
                {
                    _bIsSelected = value;
                    OnChanged("bIsSelected");
                }
            }
        }
        public string id { get; set; }
        public string SNumber { get; set; }
        public string ActivityName { get; set; }

        public double nDuration { get; set; }
        public HabitListSelectable()
        {

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
        public List<QuantityQuestion> logQuantityQuestions;
        public string completion_requirements;
        public bool useTimer;
        public breakActivity(string id, string name, int durationSeconds, List<string> videoUrls, bool logQuantity, string logQuantityQuestion, bool openSpecificApps, List<string> allowedUrls, List<string> allowedApps, string allowedFocusMode, string activity_sequence_id, bool IncludeInEveryBreak, bool isRandomChoice, List<activityChoice> choices, bool isOfficeFriendly, string logQuantityType, List<QuantityQuestion> logQuantityQuestions, string completion_requirements, bool useTimer)
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
            this.logQuantityQuestions = logQuantityQuestions;
            this.completion_requirements = completion_requirements;
            this.useTimer = useTimer;
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
        public List<QuantityQuestion> logQuantityQuestions;
        public string completion_requirements;
        public bool useTimer;
        public activityChoice(string id, string name, List<string> videoUrls, string logQuantityQuestion, bool logQuantity, string logQuantityType, List<QuantityQuestion> logQuantityQuestions, string completion_requirements, bool useTimer)
        {
            this.id = id;
            this.name = name;
            this.videoUrls = videoUrls;
            this.logQuantityQuestion = logQuantityQuestion;
            this.logQuantity = logQuantity;
            this.logQuantityType = logQuantityType;
            this.logQuantityQuestions = logQuantityQuestions;
            this.completion_requirements = completion_requirements;
            this.useTimer = useTimer;
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

    public struct OfflineEntry
    {
        public string activity_id;
        public string duration_logged;
        public string activity_sequence_id;
        public string start_time;
        public string finish_time;
        public string skipped_did_complete;
        public string choice_id;
        public string quantity_logged;
        public string skipped_did_not_complete;
        public string note_logged;

        

        public OfflineEntry(string activity_id, string duration_logged, string activity_sequence_id, string start_time, string finish_time, string skipped_did_complete, string choice_id, string quantity_logged, string skipped_did_not_complete, string note_logged)
        {
            this.activity_id = activity_id;
            this.duration_logged = duration_logged;
            this.activity_sequence_id = activity_sequence_id;
            this.start_time = start_time;
            this.finish_time = finish_time;
            this.skipped_did_complete = skipped_did_complete;
            this.choice_id = choice_id;
            this.quantity_logged = quantity_logged;
            this.skipped_did_not_complete = skipped_did_not_complete;
            this.note_logged = note_logged;
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
        public bool isMeetings { get; set; }
        public bool isLockedFocus;
        public bool isRelaxFocus;
        public bool isDeepWorkFocus;

        public bool isShowAbleInList
        {
            get { return !isLockedFocus; }
        }

        private bool _isShowAbleInTrayMenuList = false;
        public bool isShowAbleInTrayMenuList
        {
            get 
            {
                if (RegistryManager.IsSuperPomodoroIsRunning())
                {
                    return _isShowAbleInTrayMenuList;
                }
                else
                {
                    if (RegistryManager.GetFocusModeSetupDone())
                    {
                        _isShowAbleInTrayMenuList = true;
                        return true;
                    }
                    else
                    {
                        if (isLockedFocus || isMeetings)
                        {
                            _isShowAbleInTrayMenuList = false;
                            return false;
                        }
                        else
                        {
                            _isShowAbleInTrayMenuList = true;
                            return true;
                        }
                    }
                }
            }
            set
            {
                if (value != _isShowAbleInTrayMenuList)
                {
                    _isShowAbleInTrayMenuList = value;
                    OnChanged("isShowAbleInTrayMenuList");
                }
            }
        }
       
        public string sToolTip { get; set; }

        public string lastTaskUsed { get; set; }
        public FocusMode()
        {

        }
        public FocusMode(string id, string name, ObservableCollection<string> allowedUrls, ObservableCollection<string> arrBlockedUrls, ObservableCollection<AppDetails> allowedAppsWin, string color, bool isOn, bool isAllowedAllApps, bool isAllowedListApproach, bool isDefault, bool isMeetings, bool isLockedFocus, bool isRelaxFocus, bool isDeepWorkFocus, string sToolTip = "", string lastTaskUsed = "")
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
            this.isDefault = isDefault;
            this.isMeetings = isMeetings;
            this.isLockedFocus = isLockedFocus;
            this.isRelaxFocus = isRelaxFocus;
            this.isDeepWorkFocus = isDeepWorkFocus;
            this.sToolTip = sToolTip;
            this.lastTaskUsed = lastTaskUsed;
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
        private bool bAllowDomain;
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
        public bool BAllowDomain
        {
            get { return bAllowDomain; }
            set
            {
                bAllowDomain = value;
                OnChanged("BAllowDomain");
            }
        }
        public DistractingURL()
        {
           
        }
        public DistractingURL(string sURL, bool bChecked, bool bAllowDomain = false)
        {
            this.sURL = sURL;
            this.IsItemChecked = bChecked;
            this.BAllowDomain = bAllowDomain;
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

    public class DistractingURLAllowedWithIntention
    {
        //private bool isItemChecked;

        public string sURL { get; set; }
        public string sIntention { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration{ get; set; }
        public bool bIsShowIntentionWindowClosedForThisURL { get; set; }
        //public bool IsItemChecked
        //{
        //    get { return isItemChecked; }
        //    set
        //    {
        //        isItemChecked = value;
        //        OnChanged("IsItemChecked");
        //    }
        //}

        public DistractingURLAllowedWithIntention()
        {

        }
        public DistractingURLAllowedWithIntention(string sURL, string sIntention, DateTime EndTime, string Duration, bool bIsShowIntentionWindowClosedForThisURL)
        {
            this.sURL = sURL;
            this.sIntention = sIntention;
            this.EndTime = EndTime;
            this.Duration = Duration;
            this.bIsShowIntentionWindowClosedForThisURL = bIsShowIntentionWindowClosedForThisURL;
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

    public class QuantityQuestion
    {
        public string id;
        public int minValue { get; set; }
        public int maxValue { get; set; }
        public string question { get; set; }
        public string logQuantityType;
        public double quantity { get; set; }
        public string minValueDescription { get; set; }
        public string maxValueDescription { get; set; }
        public bool bShowSlider { get; set; }
        public bool bShowTextBox { get; set; } 
        public QuantityQuestion()
        {

        }

        public QuantityQuestion(string id, int minValue, int maxValue, string question, string logQuantityType, double quantity, string minValueDescription, string maxValueDescription)
        {
            this.id = id;
            this.minValue = minValue;
            this.maxValue = maxValue; 
            this.question = question;
            this.logQuantityType = logQuantityType;
            this.quantity = (double)((minValue + maxValue) / 2);

            if(minValue == 0 && maxValue == 0)
            {
                bShowSlider = false;
                bShowTextBox = true;
            }
            else
            {
                bShowSlider = true;
                bShowTextBox = false;
            }
            this.minValueDescription = minValueDescription;
            this.maxValueDescription = maxValueDescription;
        }
    }
    public struct LogQuantityAnswer
    {
        public string question_id;
        public double logged_value;
        public LogQuantityAnswer(string question_id, double logged_value)
        {
            this.question_id = question_id;
            this.logged_value = logged_value;
        }
    }

    public class RelaxURL
    {
        private bool isItemChecked;

        public string sID { get; set; }
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

        public RelaxURL()
        {

        }
        public RelaxURL(string sID, string sURL, bool bChecked)
        {
            this.sID = sID;
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

    public struct FocusTask
    {
        public string id;
        public string focus_type;
        public string title;
        public string details;
        public string due_date;
        public string eisenhower_quadrant;
        public string status;
        public string tags;
        public string duration;
        
        public FocusTask(string id, string focus_type, string title, string details, string due_date, string eisenhower_quadrant, string status, string tags, string duration)
        {
            this.id = id;
            this.focus_type = focus_type;
            this.title = title;
            this.details = details;
            this.due_date = due_date;
            this.eisenhower_quadrant = eisenhower_quadrant;
            this.status = status;
            this.tags = tags;
            this.duration = duration;
        }
    }

    public class DroppedFile
    {
        public string FilePath { get; set; }
        public BitmapImage Thumbnail { get; set; }

        public DroppedFile(string path)
        {
            FilePath = path;
            Thumbnail = new BitmapImage(new Uri(path));
        }
    }
}

