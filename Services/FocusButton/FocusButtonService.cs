using Duende.IdentityModel;
using Focus_Bear.API;
using Focus_Bear.Utils;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private static volatile bool _rxRunning;

        private static readonly Regex _tsPfx =
            new Regex(@"^\d+\.\d+\|", RegexOptions.Compiled);

        private static readonly Regex _espLog =
            new Regex(@"^[EWIDV] \(\d+|^\[?\d+\]?\s*\[?[EWIDV]\]|^\[LOG\]",
                RegexOptions.Compiled);

        public enum DeviceState { Disconnected, Idle, Running, Paused }

        private DeviceState _state = DeviceState.Disconnected;

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
        public bool Start()
        {
            LogWriter.LogWrite("FocusButton: Start()");

            //_countdownTimer.Interval = TimeSpan.FromSeconds(1);
            //_countdownTimer.Tick += CountdownTick;

            _responseCheckTimer.Interval = TimeSpan.FromMilliseconds(100);
            _responseCheckTimer.Tick += ResponseCheckTick;

            string portName = AutoDetectPort();
            if (portName is null)
            {
                LogWriter.LogWrite("FocusButton: No COM port found. Available ports:");
                foreach (string p in SerialPort.GetPortNames().OrderBy(x => x))
                    Console.Error.WriteLine($"  {p}");
                LogWriter.LogWrite("FocusButton: Use --port <COMx> to specify one.");
                return false;
            }

            Connect(portName);

            return false;
        }

        // ── Serial connect ────────────────────────────────────────────────────
        private void Connect(string portName)
        {
            LogWriter.LogWrite("FocusButton Connect()");

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

                // Fetch the habit list from the device
                //SendCmd("GET|HABITCOUNT");

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

        private void Disconnect()
        {
            LogWriter.LogWrite("FocusButton: Disconnect()");

            _rxRunning = false;

           // SetListening(false);

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
            //BtnConnect.Content = "Connect";
        }
        // ── Serial TX ─────────────────────────────────────────────────────────

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
                        if (parts.Length >= 3 && long.TryParse(parts[2], out long rem))
                            ////SetRemaining(rem);
                        // mode + optional habit name
                        if (parts.Length >= 4 && parts[3].ToUpperInvariant() == "HABIT")
                        {
                            if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
                                _currentHabitName = parts[4].Trim();
                        }
                        else if (parts.Length >= 4 && parts[3].ToUpperInvariant() == "FOCUS")
                        {
                            _currentHabitName = "";
                        }
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
                                if (parts[1].ToUpperInvariant() == "STARTHB" && parts.Length >= 4)
                                {
                                    _currentHabitName = parts[4].Trim();
                                    _currentHabitID = parts[3].Trim();
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
                                /**else if(appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                                        appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && !appSettings.bHabitsPostponedState)
                                {
                                    //Habit is ongoing, user is trying to cancel from device, so fire the Postpone button
                                    ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageHabits.btnPleaseSpareMe);
                                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                    invokeProv.Invoke();
                                }**/

                                ApplyState(DeviceState.Idle);
                                break;
                            case "DONE":
                                _currentHabitName = "";
                                _currentHabitID = "";
                                //if(appSettings.isFocusModeRunning)
                                //{
                                //    ((App)Application.Current).StopFocusSessionFromEvents();
                                //}

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
                    // HABITCOUNT|<n>  — response to GET|HABITCOUNT
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int hCount) && hCount > 0)
                    {
                        // Clear the displayed list then request each habit by ID.
                        // We don't know the IDs yet, so ask for them by 1-based index alias
                        // — firmware stores them with IDs like "h1"…"h16".
                        // Instead, ask STATUS first; the IDs come from the loaded list.
                        // Simplest approach: GET|HABIT|h1 .. GET|HABIT|h<n>

                        /////_habits.Clear();
                        for (int i = 1; i <= hCount; i++)
                            SendCmd($"GET|HABIT|h{i}");
                    }
                    break;

                case "HABIT":
                    // HABIT|<id>|<name>|<duration_s>  — response to GET|HABIT|<id>
                    if (parts.Length >= 4
                        && long.TryParse(parts[3], out long durS))
                    {
                        string hId = parts[1].Trim();
                        string hName = parts[2].Trim();
                        int hMin = (int)Math.Round(durS / 60.0);
                        // Update existing entry or add new
                       /** var existing = _habits.FirstOrDefault(h => h.Id == hId);
                        if (existing != null)
                        {
                            existing.Name = hName;
                            existing.DurationMinutes = hMin;
                            // Refresh DataGrid view
                            int idx = _habits.IndexOf(existing);
                            _habits.RemoveAt(idx);
                            _habits.Insert(idx, existing);
                        }
                        else
                        {
                            _habits.Add(new HabitEntry { Id = hId, Name = hName, DurationMinutes = hMin });
                        }**/
                    }
                    break;
            }
        }

        // ── Listening indicator ────────────────────────────────────────────

        private async void SetListening(bool on)
        {
            ///ListeningBadge.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            ///
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
                    string apiKey = "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";
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
                                            string sTask = "\"<TASK>\" task created successfully.";//todo win texts //appSettings.ConfigFileStrings["common"]["taskCreated"].ToString();
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

            // Timer buttons (UniformGrid panel)
           /** foreach (Button btn in TimerButtonsPanel.Children.OfType<Button>())
                btn.IsEnabled = idle;

            BtnPause.IsEnabled = running || paused;
            BtnPause.Content = paused ? "▶  Resume" : "⏸  Pause";
            BtnPause.Style = paused
                ? (Style)FindResource("GreenBtn")
                : (Style)FindResource("OrangeBtn");
            BtnCancel.IsEnabled = running || paused;
            BtnStatus.IsEnabled = connected;
            BtnSendHabits.IsEnabled = connected;
            BtnStartHabit.IsEnabled = idle;

            // Status dot + label
            static SolidColorBrush Hex(byte r, byte g, byte b) =>
                new(Color.FromRgb(r, g, b));

            (Brush dot, Brush text, string label) = state switch
            {
                DeviceState.Idle => (Hex(0x4E, 0xC0, 0x7A), Hex(0x4E, 0xC0, 0x7A), "Idle"),
                DeviceState.Running => (Hex(0xFF, 0x94, 0x47), Hex(0xFF, 0x94, 0x47), "Running"),
                DeviceState.Paused => (Hex(0xFF, 0xD0, 0x47), Hex(0xFF, 0xD0, 0x47), "Paused"),
                _ => ((Brush)Hex(0x55, 0x55, 0x55), Hex(0x88, 0x88, 0x88), "Disconnected"),
            };
            TxtStatus.Text = label;
            TxtStatus.Foreground = text;
            StatusDot.Fill = dot;***/

            // Countdown
            //if (running)
            //    _countdownTimer.Start();
            //else
            //{
            //    _countdownTimer.Stop();
            //    if (idle || !connected)
            //    {
            //        _sessionTotalSeconds = 0;
            //        ///SetRemaining(0);
            //        ///ArcProgress.Data = Geometry.Empty;
            //        ///TxtFinishAt.Text = "";
            //    }
            //}

            ////if (!connected) TxtRemaining.Text = "--:--";

            // Habit name label — show only when a habit session is active
            bool showHabit = (running || paused) && !string.IsNullOrWhiteSpace(_currentHabitName);
            /////TxtHabitName.Text = showHabit ? _currentHabitName : "";
            ////TxtHabitName.Visibility = showHabit ? Visibility.Visible : Visibility.Collapsed;
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

        public void StartFocusSession(int nSeconds)
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: StartFocusSession()");

            SendCmd($"SET|START|{nSeconds}");

            ApplyState(DeviceState.Running);
        }
        public void PauseFocusSession()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: PauseFocusSession()");

            if (appSettings.isFocusModePaused)
            {
                SendCmd("SET|PAUSE");
                ApplyState(DeviceState.Paused);    // optimistic
            }
            else
            {
                SendCmd("SET|RESUME");
                ApplyState(DeviceState.Running);   // optimistic
            }
        }
        public void FinishFocusSession()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: FinishFocusSession()");

            SendCmd("SET|CANCEL");
            ApplyState(DeviceState.Idle);           // optimistic
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

            Task.Delay(2000);
            int nIndex = appSettings.nHabitIndex;
            if(bAfterFinishingHabit)
            {
                SendCmd("SET|IDLE");
                nIndex++;
            }
            
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if(appSettings.arrAllMorningHabits.Count > 0)
                {
                    //todo: need a check for non timer habits
                    //var entries = appSettings.arrAllMorningHabits.Select(h => $"{h.idFakeForDevice},{h.name},{h.durationSeconds}");
                    var entries = appSettings.arrAllMorningHabits.Skip(nIndex)
                                                                 .Select(h => $"{h.idFakeForDevice},{h.nameWithoutIcon},{h.durationSeconds}");
                    SendCmd("SET|HABITS|" + string.Join("|", entries));
                }
                else
                {
                    LogWriter.LogWrite("FocusButton: !! No morning habits to send."); 
                    return;
                }
            }
            else if(appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.arrAllEveningHabits.Count > 0)
                {
                    //todo: need a check for non timer habits
                    var entries = appSettings.arrAllEveningHabits.Skip(nIndex)
                                                                 .Select(h => $"{h.idFakeForDevice},{h.nameWithoutIcon},{h.durationSeconds}");

                    SendCmd("SET|HABITS|" + string.Join("|", entries));
                }
                else
                {
                    LogWriter.LogWrite("FocusButton: !! No evening habits to send.");
                    return;
                }
            }
            else
            {
                return;
            }
            
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
            
            ApplyState(DeviceState.Paused);   // optimistic
            
        }
        public void ResumeHabit()
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: ResumeHabit()");

            SendCmd("SET|RESUME");
            ApplyState(DeviceState.Running);   // optimistic
        }

        public void RemoveHabits() //case is like habits paused, remove them from device
        {
            if (_port == null || !_port.IsOpen)
                return;

            LogWriter.LogWrite("FocusButton: RemoveHabits()");

            //now we have a function to remove all in one go
            SendCmd("SET|CLEARHABITS");
            /*
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if (appSettings.arrAllMorningHabitsFiltered.Count > 0)
                {
                    //todo: need a check for non timer habits
                    var entries = appSettings.arrAllMorningHabitsFiltered.Select(h => $"{h.id},{h.name},{h.durationSeconds * 0}");
                    SendCmd("SET|HABITS|" + string.Join("|", entries));
                }
                else
                {
                    LogWriter.LogWrite("FocusButton: !! No morning habits to Remove.");
                    return;
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.arrAllEveningHabitsFiltered.Count > 0)
                {
                    //todo: need a check for non timer habits
                    var entries = appSettings.arrAllEveningHabitsFiltered.Select(h => $"{h.id},{h.name},{h.durationSeconds * 0}");
                    SendCmd("SET|HABITS|" + string.Join("|", entries));
                }
                else
                {
                    LogWriter.LogWrite("FocusButton: !! No evening habits to Remove.");
                    return;
                }
            }
            */
        }
        public void Stop()
        {
            Disconnect();
        }
    }
}
