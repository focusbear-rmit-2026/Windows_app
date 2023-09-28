 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Xml;
using TaskScheduler;
using System.Threading.Tasks;
using Focus_Bear.Utils;

namespace Focus_Bear.Models
{
    public class AppTaskScheduler
    {
        internal bool CreateTask(string appName)
        {
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.Settings.Enabled = true;
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;

                ITriggerCollection iTriggerCollection = taskDef.Triggers;
                ITrigger trigger = iTriggerCollection.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON);
                trigger.Enabled = true;

                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                string AppInstallationDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Focus Bear\\Focus Bear.exe";
                execAction.Path = AppInstallationDir; 
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                rootFolder.RegisterTaskDefinition(appName, taskDef, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        internal bool SetTaskStatus(string TaskName, bool status)
        {
            ITaskService taskService = new TaskScheduler.TaskScheduler();
            taskService.Connect();

            string rootFolder = @"\";

            try
            {
                ITaskFolder containingFolder = taskService.GetFolder(rootFolder);
                IRegisteredTask registeredTask = containingFolder.GetTask(TaskName);
                registeredTask.Enabled = status;
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        internal bool CheckTaskExists(string TaskToCheck)
        {
            ITaskService taskService = new TaskScheduler.TaskScheduler();
            taskService.Connect();

            string rootFolder = @"\";

            try
            {
                ITaskFolder containingFolder = taskService.GetFolder(rootFolder);
                IRegisteredTask registeredTask = containingFolder.GetTask(TaskToCheck);
                if(registeredTask.Name.Length > 0)
                {
                    return true;
                }
                return false;
            }
            catch (FileNotFoundException exception)
            {
                LogWriter.LogWrite(exception.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
            return false;
        }


        internal bool DeleteTask(string TaskToDelete)
        {
            ITaskService taskService = new TaskScheduler.TaskScheduler();
            taskService.Connect();

            string rootFolder = @"\";

            try
            {
                ITaskFolder containingFolder = taskService.GetFolder(rootFolder);   
                containingFolder.DeleteTask(TaskToDelete, 0);
                return true;
            }
            catch (FileNotFoundException exception)
            {
                LogWriter.LogWrite(exception.Message);
                Debug.WriteLine(exception.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return false;
        }

        
        internal int DeleteTaskStartup(string TaskToDelete)
        {
            ITaskService taskService = new TaskScheduler.TaskScheduler();
            taskService.Connect();

            string rootFolder = @"\";

            try
            {
                ITaskFolder containingFolder = taskService.GetFolder(rootFolder);
                containingFolder.DeleteTask(TaskToDelete, 0);
                return 1;
            }
            catch (FileNotFoundException exception)
            {
                LogWriter.LogWrite(exception.Message);
                return 0;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                return 0;
            }
        }

        private string GetAppPath(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            var taskNode = doc.LastChild;
            if (taskNode != null)
            {
               var ActionNode = taskNode["Actions"];
                if(ActionNode!=null)
                {
                    var execNode = ActionNode["Exec"];
                    if(execNode!=null)
                    {
                        var cmdNode = execNode["Command"];
                        if(cmdNode!=null)
                        {
                            var exePath = cmdNode.InnerText;
                            
                            if(exePath.Contains("\""))
                            {
                                exePath = exePath.Replace("\"", "");
                            }                            
                            
                            return exePath;
                        }
                    }
                }
            }
            return "";
        }

        internal bool CreateTask(string AppName, string args)
        {
            LogWriter.LogWrite("CreateTask(string AppName, string args) - Add to Startup");
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.Settings.Enabled = true;
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
                taskDef.Principal.RunLevel = _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;

                ITriggerCollection iTriggerCollection = taskDef.Triggers;
                ILogonTrigger trigger = (ILogonTrigger)iTriggerCollection.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON);
                trigger.Enabled = true;
                trigger.Delay = "PT4M";

                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                execAction.Arguments = args;

                string appName = AppName;
                string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;

                execAction.Path = appPath;
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                ITaskSettings set = taskDef.Settings;
                set.DisallowStartIfOnBatteries = false;
                set.StopIfGoingOnBatteries = false;
                rootFolder.RegisterTaskDefinition(appName, taskDef, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

       

        internal bool ScheduleRunOnceTask(string TriggerName, DateTime startTime, DateTime endTime)
        {
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.Settings.Enabled = true;
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
                taskDef.Principal.RunLevel = _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;

                ITriggerCollection iTriggerCollection = taskDef.Triggers;
                ITimeTrigger trigger = (ITimeTrigger)taskDef.Triggers.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_TIME);
                trigger.Id = TriggerName;
                trigger.StartBoundary = startTime.ToString("yyyy-MM-ddTHH:mm:ss");

                trigger.Repetition.StopAtDurationEnd = true;
      
                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                execAction.Path = System.Reflection.Assembly.GetEntryAssembly().Location;
                execAction.Arguments = "/quickscansch";
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                rootFolder.RegisterTaskDefinition("Focus Bear_schedule", taskDef, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
            return true;
        }


        internal bool ScheduleDailyTask(string TriggerName, DateTime dateTime)
        {
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.Settings.Enabled = true;
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
                taskDef.Principal.RunLevel = _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;

                ITriggerCollection iTriggerCollection = taskDef.Triggers;
                IDailyTrigger trigger = (IDailyTrigger)taskDef.Triggers.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_DAILY);
                trigger.Id = TriggerName;
                trigger.StartBoundary = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");

                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                execAction.Path = System.Reflection.Assembly.GetEntryAssembly().Location;
                execAction.Arguments = "/quickscansch";
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                rootFolder.RegisterTaskDefinition( "Focus Bear_schedule", taskDef, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                return false;
            }
            return true;
        }

        internal bool ScheduleEveryWeekTask(string TriggerName, DateTime dateTime, int days)
        {
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.Settings.Enabled = true;
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
                taskDef.Principal.RunLevel = _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;

                IWeeklyTrigger trigger = (IWeeklyTrigger)taskDef.Triggers.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_WEEKLY);
                trigger.Id = TriggerName;
                trigger.StartBoundary = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                trigger.DaysOfWeek = (short)days;

                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                execAction.Path = System.Reflection.Assembly.GetEntryAssembly().Location;
                execAction.Arguments = "/quickscansch";
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                rootFolder.RegisterTaskDefinition("Focus Bear_schedule", taskDef, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                return false;
            }
            return true;
        }
 
    }
}
