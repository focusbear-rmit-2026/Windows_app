using Duende.IdentityModel;
using Focus_Bear.API;
using Focus_Bear.Utils;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Services.FocusButton
{
    public class FocusButtonService
    {
        public class TzEntry
        {
            public string Display { get; set; } = "";
            public int OffsetMinutes { get; set; }
            public override string ToString() => Display;
        }
        AppSettings appSettings = AppSettings.GetInstance;

        // ── Serial plumbing ───────────────────────────────────────────────────
        private static SerialPort _port;

        public string sPortName = "";

        private static volatile bool _rxRunning;

        private static readonly Regex _tsPfx =
            new Regex(@"^\d+\.\d+\|", RegexOptions.Compiled);

        private static readonly Regex _espLog =
            new Regex(@"^[EWIDV] \(\d+|^\[?\d+\]?\s*\[?[EWIDV]\]|^\[LOG\]",
                RegexOptions.Compiled);

        public enum DeviceState { Disconnected, Idle, Running, Paused }

        public DeviceState _state = DeviceState.Disconnected;

        private long _remainingSeconds;
        private long _sessionTotalSeconds;
        private string _currentHabitName = "";
        private string _currentHabitID = "";

        private Thread _rxThread;
        
        // Response-check timer: after sending a command, if no response in 100ms, query STATUS
        private readonly DispatcherTimer _responseCheckTimer = new DispatcherTimer(DispatcherPriority.Normal);
        private volatile bool _pendingResponse;
        private string _lastSentCmd = "";

        private readonly List<TzEntry> _tzList = new List<TzEntry>();

        private AudioRecorder _audioRecorder = new AudioRecorder();

        // ── SET|HABITS CRC verification (fw >= 1.7.0) ─────────────────────────────────
        private string _pendingHabitsLine;
        private uint _pendingHabitsCrc;
        private int _pendingHabitsCount;
        private int _habitsSendAttemptsLeft;
        private readonly DispatcherTimer _habitsCrcTimer = new DispatcherTimer(DispatcherPriority.Normal);

        private string _lastSentFocusGoal = "";

        private static readonly string[] FAULT_RESET_CODES = { "PANIC", "TASK_WDT", "INT_WDT", "WDT", "BROWNOUT" };

        private bool FirmwareSupports(Version minimumVersion)
        {
            //A device reporting "1.6" is at least 1.6.0 (System.Version alone says it is older).
            return FocusBear.Core.Updates.VersionCompare.AtLeast(appSettings.FirmwareVersion, minimumVersion);
        }
        public bool Start()
        {
            LogWriter.LogWrite("FocusButton: Start()");

            // The E2E test build never opens the serial port: it would take the Focus Button from the real app.
            if (!E2EBuild.MachineIntegrationAllowed) return false;

            //_countdownTimer.Interval = TimeSpan.FromSeconds(1);
            //_countdownTimer.Tick += CountdownTick;

            _responseCheckTimer.Interval = TimeSpan.FromMilliseconds(100);
            _responseCheckTimer.Tick += ResponseCheckTick;

            _habitsCrcTimer.Interval = TimeSpan.FromMilliseconds(FocusButtonProtocol.HABITS_CRC_SETTLE_MS);
            _habitsCrcTimer.Tick += HabitsCrcSettleTick;

            Task.Run(() =>
            {
                string portName = AutoDetectPort();

                // Validate that the detected COM port actually exists in the system
                bool portExists = portName != null && SerialPort.GetPortNames().Contains(portName, StringComparer.OrdinalIgnoreCase);

                if (!portExists)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogWriter.LogWrite("FocusButton: No valid COM port found. Available ports:");
                        foreach (string p in SerialPort.GetPortNames().OrderBy(x => x))
                            LogWriter.LogWrite($"  {p}");
                        LogWriter.LogWrite("FocusButton: Use --port <COMx> to specify one.");
                    });
                    return;
                }

                sPortName = portName;

                Application.Current.Dispatcher.InvokeAsync(() => Connect(portName));
            });

            return false;
        }

        // ── Serial connect ────────────────────────────────────────────────────
        public void Connect(string portName)
        {
            LogWriter.LogWrite("FocusButton Connect()");
            if (!E2EBuild.MachineIntegrationAllowed) return;

            try
            {

                Disconnect();
                _port = new SerialPort(portName, 2_000_000)
                {
                    NewLine = "\n",
                    ReadTimeout = 500,
                    WriteTimeout = 2000,
                };

                _port.Open();

                _rxRunning = true;
                _rxThread = new Thread(RxLoop) { IsBackground = true, Name = "RxLoop" };
                _rxThread.Start();

                LogWriter.LogWrite($"FocusButton: -- Connected to {portName}");

                // Send current UTC time immediately on connect
                long epoch1 = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds(); ;

                DateTime localDate = DateTime.Now;

                long timezoneOffset = (long)TimeZoneInfo.Local.GetUtcOffset(localDate).TotalSeconds;

                long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + timezoneOffset;

                SendCmd($"SET|TIME|{epoch}");

                // Request current device state
                SendCmd("STATUS");

                // Clear any cached firmware version from a previously-connected device, so a new or
                // blank (un-flashed) device doesn't keep showing the old device's version. It is
                // repopulated below only if THIS device actually replies to GET|FIRMWAREVER; a blank
                // device won't reply, leaving it unset so the updater offers to install firmware.
                appSettings.FirmwareVersion = null;
                appSettings.sFirmwareVersion = "";
                appSettings.sFirmwareHardwareCode = "";

                // Fetch the firmware version from the device
                SendCmd("GET|FIRMWAREVER");

                ApplyState(DeviceState.Idle);
                //BtnConnect.Content = "Disconnect";
            }
            catch (UnauthorizedAccessException ex)
            {
                LogWriter.LogWrite("Port is already in use: " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"Cannot open {portName}:\n{ex.Message} - Connection Error");
                _port?.Dispose();
                _port = null;
            }
        }

        public void Disconnect()
        {
            LogWriter.LogWrite("FocusButton: Disconnect()");

            _habitsCrcTimer.Stop();
            _pendingHabitsLine = null;

            _rxRunning = false;

            // Grab and null out _port *before* closing so RxLoop's IsOpen check
            // causes the loop to exit immediately without waiting on ReadLine().
            SerialPort port = _port;
            _port = null;

            if (port != null)
            {
                // Shrink ReadTimeout to 1 ms so any blocked ReadLine() throws
                // TimeoutException immediately instead of blocking for 500 ms.
                try { port.ReadTimeout = 1; } catch { }

                // Wait for the RxLoop background thread to exit cleanly (max 600 ms).
                _rxThread?.Join(600);

                try { port.Close(); } catch { /* ignore */ }
                port.Dispose();
            }

            LogWriter.LogWrite("FocusButton: -- Disconnected");
            ApplyState(DeviceState.Disconnected);

        }
        // ── Serial TX ─────────────────────────────────────────────────────────

        // Tells the device to go idle. Public so the shared voice dispatcher can request it
        // when a habit is started while habits are postponed (matches the hardware-button path).
        public void SendIdle() => SendCmd("SET|IDLE");

        private void SendCmd(string cmd)
        {
            if (_port == null || !_port.IsOpen)
                return;

            // Arm response-check timer before firing off the write so there is
            // no race window where the response could arrive before we arm it.
            if (cmd != "STATUS")
            {
                _lastSentCmd = cmd;
                _pendingResponse = true;
                _responseCheckTimer.Stop();
                _responseCheckTimer.Start();
            }
            LogWriter.LogWrite("FocusButton: SendCmd >>" + cmd);
            
            // WriteLine can block for WriteTimeout ms if the TX buffer is full.
            // Run it on a thread-pool thread so the UI stays responsive.
            SerialPort port = _port;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    port?.WriteLine(cmd);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogWriter.LogWrite($"FocusButton: !! TX error: {ex.Message}");

                        // Disarm the timer — no response will arrive for a failed send.
                        _pendingResponse = false;
                        _responseCheckTimer.Stop();
                    });
                }
            });
        }

        /// <summary>
        /// Called after 100ms if no response was received for a sent command.
        /// Queries STATUS to get the current device state.
        /// </summary>
        private void ResponseCheckTick(object sender, EventArgs e)
        {
            _responseCheckTimer.Stop();
            if (!_pendingResponse) return;
            _pendingResponse = false;

            LogWriter.LogWrite($"FocusButton: ?? No response to '{_lastSentCmd}' — querying STATUS");

            if (_port != null || _port.IsOpen)
            {
                try
                {
                    _port.WriteLine("STATUS");
                    LogWriter.LogWrite("FocusButton: >> STATUS");
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite($"FocusButton: !! STATUS query TX error: {ex.Message}");
                }
            }
        }

        // ── Serial RX loop (background thread) ───────────────────────────────

        private void RxLoop()
        {
            while (_rxRunning)
            {
                try
                {
                    if (_port == null || !_port.IsOpen)
                        break;
                    string raw = _port.ReadLine().Trim();
                    Debug.WriteLine($"RX: {raw}");
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (_espLog.IsMatch(raw)) continue;   // skip ESP log noise
                    Application.Current.Dispatcher.InvokeAsync(() => HandleLine(raw));
                }
                catch (TimeoutException) { /* normal — keep looping */ }
                catch (Exception)
                {
                    if (_rxRunning)
                        Application.Current.Dispatcher.InvokeAsync(Disconnect);
                    break;
                }
            }
        }

        // ── Incoming line parser ──────────────────────────────────────────────

        private static string StripTimestamp(string line)
        {
            // Firmware prefixes every response with  "<epoch>.<ms>|"
            var m = _tsPfx.Match(line);
            return m.Success ? line.Substring(m.Length) : line;
        }

        private void HandleLine(string raw)
        {

            // We received a valid response — disarm the response-check timer
            _pendingResponse = false;
            _responseCheckTimer.Stop();

            string line = StripTimestamp(raw);
            LogWriter.LogWrite($"FocusButton: HandleLine << " + line);

            var parts = line.Split('|');
            if (parts.Length == 0) return;

            switch (parts[0].ToUpperInvariant())
            {
                case "INFO":
                    // INFO|<state>|<remaining_s>|<mode>[|<habit_name>]
                    if (parts.Length >= 2)
                    {
                        DeviceState ns;

                        switch (parts[1].ToUpperInvariant())
                        {
                            case "RUNNING":
                                ns = DeviceState.Running;
                                break;

                            case "PAUSED":
                                ns = DeviceState.Paused;
                                break;

                            case "IDLE":
                                ns = DeviceState.Idle;
                                break;

                            default:
                                ns = _state;
                                break;
                        }
                        //(A commented-out SetRemaining(rem) used to leave the remaining-time "if" without a body, so it
                        //swallowed the mode check below: the habit name was read only when the remaining time parsed.)
                        // mode + optional habit name
                        if (parts.Length >= 4 && parts[3].ToUpperInvariant() == "HABIT")
                        {
                            if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
                                _currentHabitName = parts[4].Trim();
                        }
                        else if (parts.Length >= 4 && parts[3].ToUpperInvariant() == "FOCUS")
                        {
                            _currentHabitName = "";
                            // fw >= 1.8.0 may append the focus goal as an extra field here;
                            // the app is the source of truth for the goal so it is ignored.
                        }
                        ResyncRunningFocusSessionIfDeviceIdle(ns);
                        ApplyState(ns);
                    }
                    break;

                case "EVENT":
                    // EVENT|START|<total_s>
                    // EVENT|STARTHB|<total_s>|<HabitName>
                    // EVENT|PAUSE|<timeleft_s>
                    // EVENT|RESUME|<timeleft_s>
                    // EVENT|DONE
                    // EVENT|CANCEL|<timeleft_s>
                    if (parts.Length >= 2)
                    {
                        switch (parts[1].ToUpperInvariant())
                        {
                            case "START":
                            case "STARTHB":
                                if (parts.Length >= 3 && long.TryParse(parts[2], out long tot))
                                {
                                    _sessionTotalSeconds = tot;
                                    ///SetRemaining(tot);
                                }
                                if (parts[1].ToUpperInvariant() == "STARTHB"
                                    && FocusButtonProtocol.TryParseStartHabit(parts, out string startedId, out string startedName))
                                {
                                    _currentHabitName = startedName;
                                    _currentHabitID = startedId;
                                    if (_currentHabitID.Length > 0)
                                    {   
                                        if (((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == true)
                                        {
                                            //Habit is already ongoing, ignore it
                                            return;
                                        }
                                        if ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                                            appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) && appSettings.bHabitsPostponedState)
                                        {
                                            //Means habits are postponed. We will have to resume habits
                                            //But we need to set device as IDLE for now, then start habit after resume
                                            SendCmd("SET|IDLE");
                                            ((App)Application.Current).wndFocusBearMainWindow.PageHabits.ResumeHabitsBeforeStartActivityTrigger(_currentHabitID);

                                            return;
                                        }
                                        appSettings.bActivityStartedFromDevice = true; //Set it - Important
                                        ((App)Application.Current).wndFocusBearMainWindow.PageHabits.StartActivityTriggeredFromDevice(_currentHabitID);
                                    }
                                }
                                else if (parts[1].ToUpperInvariant() == "START")
                                {
                                    _currentHabitName = "";
                                    _currentHabitID = "";
                                    if (appSettings.isFocusModeRunning)
                                    {
                                        return;
                                    }
                                    //todo Start the most recent focus session here
                                    //use appSettings.sMostRecentFocusmodeDoneID;
                                    ((App)Application.Current).StartWorkFocusMode(appSettings.sMostRecentFocusmodeDoneID, (int)_sessionTotalSeconds, true);

                                }
                                ApplyState(DeviceState.Running);
                                break;
                            case "PAUSE":

                                if(appSettings.isFocusModeRunning)
                                {
                                    if (appSettings.isFocusModePaused)
                                    {
                                        return; //Already paused, may be coming from device
                                    }
                                    ((App)Application.Current).wndTrayMenu.ToggleFocusPauseFromUI(true);
                                }
                                else if (((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == true)
                                {
                                    //Habit is ongoing, Pause it
                                    ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnPausePlay);
                                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                    invokeProv.Invoke();
                                }

                                ApplyState(DeviceState.Paused);
                                
                                if (parts.Length >= 3 && long.TryParse(parts[2], out long remP))
                                    ;
                                    ///SetRemaining(remP);
                                break;
                            case "RESUME":
                                if (appSettings.isFocusModeRunning)
                                {
                                    if (!appSettings.isFocusModePaused)
                                    {
                                        return; //Already not paused and running 
                                    }
                                    ((App)Application.Current).wndTrayMenu.ToggleFocusPauseFromUI(true);
                                }
                                else if ( ((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == false)
                                {
                                    //Habit is ongoing, Pause it
                                    ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnPausePlay);
                                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                    invokeProv.Invoke();
                                }

                                ApplyState(DeviceState.Running);

                                if (parts.Length >= 3 && long.TryParse(parts[2], out long remR))
                                    ;//// SetRemaining(remR);
                                break;
                            case "CANCEL":
                                _currentHabitName = "";
                                _currentHabitID = "";

                                if (appSettings.isFocusModeRunning)
                                {
                                    ((App)Application.Current).StopFocusSessionFromEvents();
                                }

                                ApplyState(DeviceState.Idle);
                                break;
                            case "DONE":
                                _currentHabitName = "";
                                _currentHabitID = "";
                                //nothing to do actually as the app handles the done events

                                ApplyState(DeviceState.Idle);
                                break;
                            case "LISTEN":
                                // EVENT|LISTEN|START  or  EVENT|LISTEN|END
                                if (parts.Length >= 3)
                                {
                                    bool on = parts[2].ToUpperInvariant() == "START";
                                    SetListening(on);
                                }
                                break;
                        }
                    }
                    break;

                // ACK and ERROR are shown in the log — no extra action needed

                case "HABITCOUNT":
                    // HABITCOUNT|<n> — response to GET|HABITCOUNT. Habit IDs are not
                    // guaranteed contiguous (removals and SET|ADDHABIT leave gaps), so
                    // enumerating them as GET|HABIT|h1..h<n> silently misses entries;
                    // GET|HABITLIST (fw >= 1.6.0) walks the device's actual list instead.
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int hCount) && hCount > 0
                        && FirmwareSupports(FocusButtonProtocol.FW_HABITLIST))
                    {
                        SendCmd("GET|HABITLIST");
                    }
                    break;

                case "HABITLIST":
                    // HABITLIST|<n> — header for GET|HABITLIST; the n HABIT|<id>|<name>|<secs>
                    // lines that follow arrive in device array order and hit the HABIT case.
                    break;

                case "HABITSCRC":
                    HandleHabitsCrcResponse(parts);
                    break;

                case "RESETREASON":
                    // RESETREASON|<code> — response to GET|RESETREASON
                    if (parts.Length >= 2)
                        LogDeviceResetReason(parts[1].Trim(), false);
                    break;

                case "CRASH":
                    // CRASH|<code> — emitted once at boot when the previous boot ended in a
                    // fault; best-effort (often precedes the connection), so the reliable
                    // path is the GET|RESETREASON query sent once the firmware version is known.
                    if (parts.Length >= 2)
                        LogDeviceResetReason(parts[1].Trim(), true);
                    break;

                case "HABIT":
                    // HABIT|<id>|<name>|<duration_s>  — response to GET|HABIT|<id>
                    if (parts.Length >= 4
                        && long.TryParse(parts[3], out long durS))
                    {
                        string hId = parts[1].Trim();
                        string hName = parts[2].Trim();
                        int hMin = (int)Math.Round(durS / 60.0);
                    }
                    break;
                    case "FIRMWAREVER":
                    // FIRMWAREVER|<hw>-<version>  e.g. M50-1.4.2 (M50 = M5Dial, C30 = VIEWE C3)
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        string ver = parts[1].Trim();
                        //An unreadable version (no "-", not a number) leaves it unknown instead of throwing.
                        FocusButtonProtocol.TryParseFirmwareVersion(ver, out string hwCode, out Version parsedVersion);
                        if (parsedVersion != null)
                            appSettings.FirmwareVersion = parsedVersion;
                        //The board decides which firmware image may be offered (FirmwareBoard.ReleaseFor).
                        appSettings.sFirmwareHardwareCode = hwCode ?? "";
              
                        string hwName;

                        switch (hwCode)
                        {
                            case "M50":
                                hwName = "M5Dial";
                                break;

                            case "C30":
                                hwName = "VIEWE C3";
                                break;

                            default:
                                hwName = "unknown hardware";
                                break;
                        }
                        appSettings.sFirmwareVersion = $"{ver}  ({hwName})";

                        // Now that the version is known, ask why the device last rebooted so
                        // faults land in the app logs (the boot CRASH line is best-effort).
                        if (FirmwareSupports(FocusButtonProtocol.FW_HABITLIST))
                            SendCmd("GET|RESETREASON");
                    }
                    break;
            }
        }

        // ── Listening indicator ────────────────────────────────────────────

        private async void SetListening(bool on)
        {
            if(on)
            {
                LogWriter.LogWrite("FocusButton StartRecording()");
                _audioRecorder.StartRecording();
            }
            else
            {
                LogWriter.LogWrite("FocusButton StopRecording()");
                await _audioRecorder.StopRecording();

                try
                {
                    LogWriter.LogWrite("FocusButton ProcessVoiceCommand() started");
                    string apiKey = OpenAIService.OpenAIApiKey;
                    IntentResponse result = await OpenAIService.ProcessVoiceCommand(apiKey, appSettings.RecordingPath);
                    if (result.confidence == "low")
                    {
                        // Ask user for clarification.
                    }
                    else
                    {
                        switch (result.action)
                        {
                            case "create_task":
                                {
                                    string task = result.parameters?.task;
                                    string time = result.parameters?.time;
                                    string repeat = result.parameters?.repeat;

                                    // Create the task.
                                    // Parse time if needed.
                                    if (task.Length > 0)
                                    {
                                        FocusBearAPIClient client = new FocusBearAPIClient();
                                        var m = await client.AddTask(task);
                                        if (m == 1)
                                        {
                                            string sTask = appSettings.ConfigFileStrings["common"]["taskCreated"].ToString();
                                            sTask = sTask.Replace("<TASK>", task);
                                            FBMessageBox msg = new FBMessageBox("", sTask, MessageBoxButton.OK, "", "", "", false, "", true);
                                            msg.ShowDialog();
                                        }
                                    }

                                    break;
                                }

                            case "start_focus":
                                {
                                    int duration = result.parameters?.duration_minutes ?? 25;
                                    string goal = result.parameters?.goal;

                                    if (appSettings.isFocusModeRunning)
                                    {
                                        return;
                                    }
                                    //todo Start the most recent focus session here
                                    //use appSettings.sMostRecentFocusmodeDoneID;
                                    if(goal.Length > 0)
                                    {
                                        appSettings.sRunningFocusModeIntention = goal;
                                    }

                                    ((App)Application.Current).StartWorkFocusMode(appSettings.sMostRecentFocusmodeDoneID, duration*60, true);

                                    ApplyState(DeviceState.Running);
                                    break;
                                }

                            case "pause_focus":
                                {
                                    // Pause current focus session.
                                    if (appSettings.isFocusModeRunning)
                                    {
                                        if (appSettings.isFocusModePaused)
                                        {
                                            return; //Already paused, may be coming from device
                                        }
                                        ((App)Application.Current).wndTrayMenu.ToggleFocusPauseFromUI(true);
                                    }
                                    ApplyState(DeviceState.Paused);
                                    break;
                                }

                            case "stop_focus":
                                {
                                    // Stop current focus session.
                                    if (appSettings.isFocusModeRunning)
                                    {
                                        ((App)Application.Current).StopFocusSessionFromEvents();
                                    }
                                    break;
                                }

                            case "start_habit":
                                {
                                    string habit = result.parameters?.habit_name;
                                    int duration = result.parameters?.duration_minutes ?? 0;
                                    string repeat = result.parameters?.repeat;
                                    // Start the habit.
                                    if (habit.Length > 0)
                                    {
                                        if (((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == true)
                                        {
                                            //Habit is already ongoing, ignore it
                                            return;
                                        }
                                        if ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                                            appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) && appSettings.bHabitsPostponedState)
                                        {
                                            //Means habits are postponed. We will have to resume habits
                                            //But we need to set device as IDLE for now, then start habit after resume
                                            SendCmd("SET|IDLE");
                                            ((App)Application.Current).wndFocusBearMainWindow.PageHabits.ResumeHabitsBeforeStartActivityTrigger(habit);

                                            return;
                                        }
                                        appSettings.bActivityStartedFromDevice = false; //Set it - Important
                                        ((App)Application.Current).wndFocusBearMainWindow.PageHabits.StartActivityTriggeredFromAudioCommand(habit);
                                    }

                                    break;
                                }
                            case "pause_habit":
                                {
                                    if (((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == true)
                                    {
                                        //Habit is ongoing, Pause it
                                        ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnPausePlay);
                                        IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                        invokeProv.Invoke();
                                    }
                                    break;
                                }
                            case "resume_habit":
                                {
                                    if (((App)Application.Current).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing?.bTimerRunning == false)
                                    {
                                        //Habit is ongoing, Pause it
                                        ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.btnPausePlay);
                                        IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                        invokeProv.Invoke();
                                    }
                                    break;
                                }
                            default:
                                {
                                    // Unknown action.
                                    LogWriter.LogWrite("FocusButton ProcessVoiceCommand() - Action unknown");
                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FocusButton ProcessVoiceCommand() FAILED ex - " + ex.Message);
                }
            }
        }

        // ── UI state machine ──────────────────────────────────────────────────

        private void ApplyState(DeviceState state)
        {
            _state = state;
            bool connected = state != DeviceState.Disconnected;
            bool idle = state == DeviceState.Idle;
            bool running = state == DeviceState.Running;
            bool paused = state == DeviceState.Paused;

        }

        // ── Port auto-detection (mirrors WPF app logic) ───────────────────────
        private static string AutoDetectPort()
        {

            LogWriter.LogWrite("FocusButton: AutoDetectPort()");
            try
            {
                // Priority:
                // 1. FocusButton exact PID
                // 2. Espressif VID
                // 3. M5Stack VID
                // 4. Keywords
                // 5. First detected port

                const string focusVid = "VID_303A";
                const string focusPid = "PID_816A";

                const string espVid = "VID_303A";
                const string m5Vid = "VID_0416";

                string[] keywords =
                {
                    "CP210",
                    "CH34",
                    "CH91",
                    "FTDI",
                    "SILICON",
                    "PROLIFIC",
                    "USB SERIAL",
                    "FOCUSBUTTON"
                };

                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    string fbPort = null;
                    string espPort = null;
                    string m5Port = null;
                    string kwPort = null;
                    string firstPort = null;

                    foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    {
                        string name =
                            obj["Name"] != null
                                ? obj["Name"].ToString()
                                : "";

                        string deviceId =
                            obj["DeviceID"] != null
                                ? obj["DeviceID"].ToString()
                                : "";

                        Match m = Regex.Match(name, @"\(COM\d+\)");

                        if (!m.Success)
                            continue;

                        string port = m.Value.Substring(1, m.Value.Length - 2);

                        if (firstPort == null)
                            firstPort = port;

                        if (fbPort == null &&
                            deviceId.IndexOf(focusVid, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            deviceId.IndexOf(focusPid, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fbPort = port;
                        }
                        else if (espPort == null &&
                                 deviceId.IndexOf(espVid, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            espPort = port;
                        }
                        else if (m5Port == null &&
                                 deviceId.IndexOf(m5Vid, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            m5Port = port;
                        }
                        else if (kwPort == null)
                        {
                            string upper = name.ToUpperInvariant();

                            if (keywords.Any(k => upper.Contains(k)))
                            {
                                kwPort = port;
                            }
                        }
                    }

                    return fbPort ?? espPort ?? m5Port ?? kwPort ?? firstPort;
                }
            }
            catch
            {
                return null;
            }
        }

        public void StartFocusSession(int nSeconds, string sGoal = "")
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: StartFocusSession()");

            string sSanitizedGoal = FocusButtonProtocol.SanitizeDeviceText(sGoal);
            if (sSanitizedGoal.Length > 0 && FirmwareSupports(FocusButtonProtocol.FW_FOCUS_GOAL))
            {
                _lastSentFocusGoal = sSanitizedGoal;
                SendCmd($"SET|START|{nSeconds}|{sSanitizedGoal}");
            }
            else
            {
                _lastSentFocusGoal = "";
                SendCmd($"SET|START|{nSeconds}");
            }

            ApplyState(DeviceState.Running);
        }

        /// <summary>
        /// Shows the user's intention/current task under the device's progress ring
        /// (fw >= 1.8.0). Empty text clears it. Only meaningful while a focus session is
        /// running — the device ignores SET|GOAL in habit mode.
        /// </summary>
        public void SendFocusGoal(string sGoal)
        {
            if (_port == null || !_port.IsOpen)
                return;
            if (!appSettings.isFocusModeRunning || !FirmwareSupports(FocusButtonProtocol.FW_FOCUS_GOAL))
                return;

            string sSanitizedGoal = FocusButtonProtocol.SanitizeDeviceText(sGoal);
            if (sSanitizedGoal == _lastSentFocusGoal)
                return;

            _lastSentFocusGoal = sSanitizedGoal;
            LogWriter.LogWrite("FocusButton: SendFocusGoal()");
            SendCmd("SET|GOAL|" + sSanitizedGoal);
        }

        /// <summary>
        /// If the device reports IDLE (e.g. it rebooted or was reconnected mid-session)
        /// while the app still has a focus session running, re-push the session — and
        /// its goal — so the device display catches back up.
        /// </summary>
        private void ResyncRunningFocusSessionIfDeviceIdle(DeviceState reportedState)
        {
            if (reportedState != DeviceState.Idle)
                return;
            if (!appSettings.isFocusModeRunning || appSettings.isFocusModePaused)
                return;

            if (!DateTime.TryParse(appSettings.sRunningFocusModeEndTime, out DateTime endTime))
                return;

            int nRemainingSeconds = (int)(endTime - DateTime.Now).TotalSeconds;
            if (nRemainingSeconds <= 0)
                return;

            LogWriter.LogWrite("FocusButton: device idle but a focus session is running — resyncing " + nRemainingSeconds + "s");
            StartFocusSession(nRemainingSeconds, appSettings.sRunningFocusModeIntention);
        }

        /// <summary>
        /// Shows a "Late No More" countdown on the device: the title plus MM:SS ramping
        /// green→red, beeping every 3s past zero until DismissNotifyOnDevice() (fw >= 1.5.0).
        /// </summary>
        public void NotifyOnDevice(string sText, int nSeconds = 0)
        {
            if (_port == null || !_port.IsOpen || !FirmwareSupports(FocusButtonProtocol.FW_NOTIFY))
                return;

            string sSanitizedText = FocusButtonProtocol.SanitizeDeviceText(sText);
            if (sSanitizedText.Length == 0)
                return; // an empty title is rejected by the device (ERROR|invalid_value)

            LogWriter.LogWrite("FocusButton: NotifyOnDevice()");
            SendCmd(nSeconds > 0 ? $"SET|NOTIFY|{sSanitizedText}|{nSeconds}" : $"SET|NOTIFY|{sSanitizedText}");
        }

        public void DismissNotifyOnDevice()
        {
            if (_port == null || !_port.IsOpen || !FirmwareSupports(FocusButtonProtocol.FW_HABITLIST))
                return;

            LogWriter.LogWrite("FocusButton: DismissNotifyOnDevice()");
            SendCmd("SET|NOTIFYDISMISS");
        }

        /// <summary>Plays the attention melody (audible on M5Dial only; silent on VIEWE C3).</summary>
        public void BeepOnDevice()
        {
            if (_port == null || !_port.IsOpen || !FirmwareSupports(FocusButtonProtocol.FW_HABITLIST))
                return;

            LogWriter.LogWrite("FocusButton: BeepOnDevice()");
            SendCmd("SET|BEEP");
        }
        public void PauseFocusSession()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: PauseFocusSession()");

            if (appSettings.isFocusModePaused)
            {
                SendCmd("SET|PAUSE");
                ApplyState(DeviceState.Paused);    
            }
            else
            {
                SendCmd("SET|RESUME");
                ApplyState(DeviceState.Running);   
            }
        }
        public void FinishFocusSession()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: FinishFocusSession()");

            SendCmd("SET|CANCEL");
            ApplyState(DeviceState.Idle);          
        }

        public void ExtendFocusSession(int nSeconds)
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: ExtendFocusSession()");

            SendCmd($"SET|UPDATE|{nSeconds}");

            ApplyState(DeviceState.Running);
        }

        public void SendHabits(bool bAfterFinishingHabit = false)
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: SendHabits()");

            RemoveHabits(); //Remove if there is an old list of habits

            int nIndex = appSettings.nHabitIndex;
            if(bAfterFinishingHabit)
            {
                SendCmd("SET|IDLE");
                nIndex++;
            }

            List<habit> arrHabits;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                arrHabits = appSettings.arrAllMorningHabits;
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                arrHabits = appSettings.arrAllEveningHabits;
            else
                return;

            if (arrHabits.Count == 0)
            {
                LogWriter.LogWrite("FocusButton: !! No habits to send.");
                return;
            }

            bool bAllIdsNumeric;
            List<FocusButtonProtocol.HabitWireEntry> arrEntries =
                BuildHabitWireEntries(arrHabits.Skip(nIndex), out bAllIdsNumeric);
            if (arrEntries.Count == 0)
            {
                LogWriter.LogWrite("FocusButton: !! No habits left to send.");
                return;
            }

            string sHabitsLine = "SET|HABITS|" + string.Join("|",
                arrEntries.Select(entry => $"{entry.WireId},{entry.Name},{entry.Secs}"));

            SendCmd(sHabitsLine);
            ArmHabitsCrcVerify(sHabitsLine, arrEntries, bAllIdsNumeric);
        }

        /// <summary>
        /// Converts habits to the exact triples sent on the wire: sanitised names (pipes
        /// or newlines would corrupt the line framing) and the no-timer sentinel for
        /// completion-based habits, so the device counts up instead of running a bogus timer.
        /// </summary>
        private List<FocusButtonProtocol.HabitWireEntry> BuildHabitWireEntries(IEnumerable<habit> arrHabits, out bool bAllIdsNumeric)
        {
            var arrEntries = new List<FocusButtonProtocol.HabitWireEntry>();
            bool bNoTimerSupported = FirmwareSupports(FocusButtonProtocol.FW_HABITLIST);
            bAllIdsNumeric = true;

            foreach (habit h in arrHabits)
            {
                // The device stores decimal ids (GET|HABITSCRC hashes them as decimal),
                // so the "h" prefix is stripped for the CRC while the wire id stays as-is.
                string sWireId = h.idFakeForDevice ?? "";
                string sIdDigits = sWireId.StartsWith("h") || sWireId.StartsWith("H")
                    ? sWireId.Substring(1)
                    : sWireId;
                long nNumericId;
                if (!long.TryParse(sIdDigits, out nNumericId))
                {
                    bAllIdsNumeric = false;
                    nNumericId = 0;
                }

                string sName = FocusButtonProtocol.SanitizeDeviceText(h.nameWithoutIcon);
                long nSecs = !h.useTimer && bNoTimerSupported
                    ? FocusButtonProtocol.HABIT_DURATION_NO_TIMER
                    : h.durationSeconds;

                arrEntries.Add(new FocusButtonProtocol.HabitWireEntry(sWireId, nNumericId, sName, nSecs));
            }
            return arrEntries;
        }

        private void ArmHabitsCrcVerify(string sHabitsLine, List<FocusButtonProtocol.HabitWireEntry> arrEntries, bool bAllIdsNumeric)
        {
            if (!FirmwareSupports(FocusButtonProtocol.FW_HABITSCRC))
                return;
            if (!bAllIdsNumeric)
            {
                LogWriter.LogWrite("FocusButton: !! CRC verify skipped — a habit device id is not numeric");
                return;
            }

            _pendingHabitsLine = sHabitsLine;
            _pendingHabitsCount = arrEntries.Count;
            _pendingHabitsCrc = FocusButtonProtocol.HabitsCrc(arrEntries);
            _habitsSendAttemptsLeft = FocusButtonProtocol.HABITS_SEND_MAX_ATTEMPTS;

            _habitsCrcTimer.Stop();
            _habitsCrcTimer.Start();
        }

        /// <summary>Fires once the firmware's deferred NVS flush is safely done.</summary>
        private void HabitsCrcSettleTick(object sender, EventArgs e)
        {
            _habitsCrcTimer.Stop();
            if (_pendingHabitsLine != null)
                SendCmd("GET|HABITSCRC");
        }

        private void HandleHabitsCrcResponse(string[] parts)
        {
            // HABITSCRC|<n>|<crc32-hex> — response to GET|HABITSCRC. A damaged multi-packet
            // SET|HABITS line can still parse and ACK with fewer/corrupt entries, so count
            // and CRC are verified against what was sent and the list resent on mismatch.
            if (_pendingHabitsLine == null || parts.Length < 3)
                return;

            bool bMatches = int.TryParse(parts[1], out int nDeviceCount)
                && uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint nDeviceCrc)
                && nDeviceCount == _pendingHabitsCount
                && nDeviceCrc == _pendingHabitsCrc;

            if (bMatches)
            {
                LogWriter.LogWrite($"FocusButton: habits verified — {parts[1]} entries, CRC {parts[2]} matches");
                _pendingHabitsLine = null;
            }
            else if (--_habitsSendAttemptsLeft > 0)
            {
                LogWriter.LogWrite($"FocusButton: !! habits CRC mismatch (device {parts[1]}/{parts[2]}, expected {_pendingHabitsCount}/{_pendingHabitsCrc:x8}) — resending");
                // A dropped line tail may have eaten the '\n' — re-sync framing first.
                try { _port?.WriteLine(""); } catch { /* ignore */ }
                SendCmd(_pendingHabitsLine);
                _habitsCrcTimer.Stop();
                _habitsCrcTimer.Start();
            }
            else
            {
                LogWriter.LogWrite($"FocusButton: !! habits CRC still mismatched after retries — device list may be damaged (device {parts[1]}/{parts[2]}, expected {_pendingHabitsCount}/{_pendingHabitsCrc:x8})");
                _pendingHabitsLine = null;
            }
        }

        private void LogDeviceResetReason(string sCode, bool bFromCrashLine)
        {
            bool bFault = FAULT_RESET_CODES.Contains(sCode.ToUpperInvariant());
            string sSource = bFromCrashLine ? "boot CRASH line" : "GET|RESETREASON";
            if (bFault)
                LogWriter.LogWrite("FocusButton: !! device fault detected (" + sSource + "): " + sCode);
            else
                LogWriter.LogWrite("FocusButton: device reset reason (" + sSource + "): " + sCode);
        }
        public void StartHabit(string sFakeHabitId)
        {
            if (_port == null || !_port.IsOpen)
                return;
            if(appSettings.bActivityStartedFromDevice)
            {
                //This is just reinvoked, so return
                appSettings.bActivityStartedFromDevice = false;
                return;
            }

            LogWriter.LogWrite("FocusButton: StartHabit() - " + sFakeHabitId);

            SendCmd($"SET|STARTHBID|{sFakeHabitId}");

            //todo what is the habit duration?
            ApplyState(DeviceState.Running);

        }

        public void PauseHabit()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: PauseHabit()");

            SendCmd("SET|PAUSE");
            
            ApplyState(DeviceState.Paused);   
            
        }
        public void ResumeHabit()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: ResumeHabit()");

            SendCmd("SET|RESUME");
            ApplyState(DeviceState.Running);    
        }

        public void RemoveHabits() //case is like habits paused, remove them from device
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: RemoveHabits()");

            //now we have a function to remove all in one go
            SendCmd("SET|CLEARHABITS");
        }
        public void Stop()
        {
            Disconnect();
        }
    }
}
