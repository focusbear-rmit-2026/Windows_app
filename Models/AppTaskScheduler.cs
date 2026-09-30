 
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
    /// <summary>Every method is a no-op in the E2E test build (see E2EBuild): it must not create, repair, repoint or delete the real app's logon, watchdog or schedule tasks.</summary>
    public class AppTaskScheduler
    {
        internal bool CreateTask(string appName)
        {
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return 0; // the E2E test build never touches Task Scheduler
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

        /// <summary>
        /// Task Scheduler's defaults end a task after 72 hours and stop it when a laptop goes onto battery, and
        /// Focus Bear runs for as long as the task does: the app was being killed after three days, and every time
        /// someone unplugged. Tasks registered before these settings existed never get re-registered (startup only
        /// creates the task when it is missing), so they are repaired in place.
        /// </summary>
        internal static void ApplyLongRunningSettings(ITaskSettings settings)
        {
            settings.DisallowStartIfOnBatteries = false;
            settings.StopIfGoingOnBatteries = false;
            settings.ExecutionTimeLimit = "PT0S";
            settings.MultipleInstances = _TASK_INSTANCES_POLICY.TASK_INSTANCES_IGNORE_NEW;
        }

        internal static bool HasLongRunningSettings(ITaskSettings settings)
        {
            return !settings.DisallowStartIfOnBatteries && !settings.StopIfGoingOnBatteries
                && settings.ExecutionTimeLimit == "PT0S";
        }

        /// <summary>
        /// Take the start-up delay off every logon trigger of a task. Returns true if any trigger had one. Tasks
        /// registered by older builds wait four minutes after sign-in (report 2026-09-28_21-45-38).
        /// </summary>
        internal static bool RemoveLogonDelays(ITaskDefinition definition)
        {
            bool changed = false;
            foreach (ITrigger trigger in definition.Triggers)
            {
                if (trigger is ILogonTrigger logon && FocusBear.Core.Startup.StartupTaskPolicy.DelaysStart(logon.Delay))
                {
                    logon.Delay = FocusBear.Core.Startup.StartupTaskPolicy.LogonDelay;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// Fix an existing startup task's settings, keeping its action and account as they are: the long-running
        /// settings above, and no delay after sign-in.
        /// </summary>
        internal bool RepairLongRunningSettings(string taskName)
        {
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();
                ITaskFolder rootFolder = taskService.GetFolder(@"\");
                IRegisteredTask registered = rootFolder.GetTask(taskName);
                ITaskDefinition definition = registered.Definition;
                bool settingsOk = HasLongRunningSettings(definition.Settings);
                bool delayRemoved = RemoveLogonDelays(definition);
                if (settingsOk && !delayRemoved) return true;

                ApplyLongRunningSettings(definition.Settings);
                rootFolder.RegisterTaskDefinition(taskName, definition, (int)_TASK_CREATION.TASK_UPDATE, null, null, definition.Principal.LogonType, null);
                LogWriter.LogWrite("RepairLongRunningSettings - repaired task " + taskName
                    + (settingsOk ? "" : " (battery / time-limit settings)") + (delayRemoved ? " (removed the delay after sign-in)" : ""));
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RepairLongRunningSettings(" + taskName + ") ex - " + ex.Message);
                return false;
            }
        }

        private static bool IsElevatedTask(string taskName)
        {
            try
            {
                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();
                IRegisteredTask registered = taskService.GetFolder(@"\").GetTask(taskName);
                return registered.Definition.Principal.RunLevel == _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Create or refresh the watchdog task (see <see cref="AppWatchdog"/>): every
        /// <see cref="AppWatchdog.ProbeInterval"/>, launch this exe with <see cref="AppWatchdog.LaunchArgument"/>.
        /// A relaunched app runs as that task's instance, so the task needs the same long-running settings as the
        /// startup task, and a probe while it runs is simply skipped (IgnoreNew).
        /// </summary>
        /// <param name="elevated">
        /// Whether this process runs as an administrator. An elevated app registers an elevated watchdog, as the
        /// startup task is; otherwise the task runs as the user, which needs no rights — and a non-elevated run
        /// never replaces an elevated watchdog that is already there, which would downgrade it.
        /// </param>
        internal bool EnsureWatchdogTask(string exePath, bool elevated)
        {
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
#if DEBUG
            // A Debug build never registers the watchdog: it would point the task (one per machine, shared with an
            // installed Focus Bear) at bin\Debug, and relaunch the app each time the debugger is stopped.
            return false;
#else
            try
            {
                if (!elevated && IsElevatedTask(AppWatchdog.TaskName)) return true;

                ITaskService taskService = new TaskScheduler.TaskScheduler();
                taskService.Connect();
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                ITaskDefinition taskDef = taskService.NewTask(0);
                taskDef.RegistrationInfo.Author = Environment.UserDomainName + "\\" + Environment.UserName;
                taskDef.RegistrationInfo.Description = "Restarts Focus Bear if it was closed without quitting (for example ended from Task Manager). A deliberate quit is left alone.";
                taskDef.Settings.Enabled = true;
                taskDef.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
                taskDef.Principal.RunLevel = elevated ? _TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST : _TASK_RUNLEVEL.TASK_RUNLEVEL_LUA;
                taskDef.Principal.LogonType = _TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN;
                ApplyLongRunningSettings(taskDef.Settings);

                ITimeTrigger trigger = (ITimeTrigger)taskDef.Triggers.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_TIME);
                trigger.Enabled = true;
                trigger.StartBoundary = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss");
                trigger.Repetition.Interval = FocusBear.Core.Startup.StartupTaskPolicy.WatchdogRepetitionInterval;
                trigger.Repetition.Duration = "";
                trigger.Repetition.StopAtDurationEnd = false;

                IExecAction execAction = (IExecAction)taskDef.Actions.Create(_TASK_ACTION_TYPE.TASK_ACTION_EXEC);
                execAction.Path = exePath;
                execAction.Arguments = AppWatchdog.LaunchArgument;

                rootFolder.RegisterTaskDefinition(AppWatchdog.TaskName, taskDef, (int)_TASK_CREATION.TASK_CREATE_OR_UPDATE, null, null, _TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN, null);
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("EnsureWatchdogTask ex - " + ex.Message);
                return false;
            }
#endif
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
                // Start at sign-in, not four minutes later: until it runs, nothing blocks and nothing stops the app
                // being uninstalled (report 2026-09-28_21-45-38).
                trigger.Delay = FocusBear.Core.Startup.StartupTaskPolicy.LogonDelay;

                IActionCollection actions = taskDef.Actions;
                _TASK_ACTION_TYPE actionType = _TASK_ACTION_TYPE.TASK_ACTION_EXEC;

                IAction action = actions.Create(actionType);
                IExecAction execAction = action as IExecAction;
                execAction.Arguments = args;

                string appName = AppName;
                string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;

                execAction.Path = appPath;
                ITaskFolder rootFolder = taskService.GetFolder(@"\");

                ApplyLongRunningSettings(taskDef.Settings);
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
            if (!E2EBuild.MachineIntegrationAllowed) return false; // the E2E test build never touches Task Scheduler
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
