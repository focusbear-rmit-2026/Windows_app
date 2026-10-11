using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Focus_Bear.LateNoMore
{
    /// <summary>
    /// Late No More on Windows: the Mac's standalone Late No More app and Focus Bear's side of it in one. The Mac app reads
    /// Apple Calendar every 10 s; Windows has no local calendar, so this reads the meetings the Day Plan already caches
    /// from the backend (<see cref="DayPlanCalendarManager"/>) and keeps the next hours fresh on a light schedule of its
    /// own (the Day Plan refreshes only while it is open): every <see cref="CalendarRefreshMinutes"/> minutes it asks the
    /// manager for today and tomorrow, which is skipped when the Day Plan refreshed recently. Alerts are evaluated from
    /// that cache every <see cref="TickSeconds"/> seconds without any request.
    /// </summary>
    internal sealed class LateNoMoreService
    {
        public static LateNoMoreService Shared { get; } = new LateNoMoreService();

        public const int TickSeconds = 15;
        public const int CalendarRefreshMinutes = 3;
        /// <summary>A refresh by anyone (the Day Plan included) within this many seconds is fresh enough.</summary>
        public const int CalendarFreshSeconds = 150;
        public const int ConfigPullMinutes = 30;
        public const int SnoozeSeconds = 60;
        /// <summary>How often an open alert checks whether the user has joined its meeting.</summary>
        public const int JoinCheckSeconds = 3;
        /// <summary>How long the "You've joined" confirmation stays before the alert closes itself.</summary>
        public static readonly TimeSpan JoinedCloseAfter = TimeSpan.FromSeconds(6);

        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly Dictionary<string, LateNoMoreAlertWindow> _open = new Dictionary<string, LateNoMoreAlertWindow>();
        private readonly Dictionary<string, DateTime> _snoozedUntil = new Dictionary<string, DateTime>();
        private DispatcherTimer _timer, _joinTimer;
        private DateTime _lastCalendarRefresh = DateTime.MinValue, _lastConfigPull = DateTime.MinValue;
        private bool _refreshing;
        private LateNoMoreConfigSync _sync;
        private string _syncUser;

        public LateNoMoreDismissals Dismissals { get; } = new LateNoMoreDismissals();

        /// <summary>Raised on the UI thread when the settings in effect change (a pull or a save).</summary>
        public event Action ConfigChanged;

        private LateNoMoreService() { }

        // ------------------------------------------------------------------ settings

        private string UserId => _settings.userAccount?.userID;

        private LateNoMoreConfigSync Sync
        {
            get
            {
                string user = UserId ?? "";
                if (_sync == null || _syncUser != user)
                {
                    _syncUser = user;
                    _sync = new LateNoMoreConfigSync(Remote ?? new LateNoMoreApiRemote(), LoadLocal, SaveLocal);
                }
                return _sync;
            }
        }

        /// <summary>Replaces the backend (the harness and tests pass a fake). Takes effect on the next read.</summary>
        internal ILateNoMoreConfigRemote Remote { get; set; }

        internal void ResetForTests() { _sync = null; _fired.Clear(); }

        public LateNoMoreConfig Config => Sync.Current;

        public LateNoMoreRemoteState RemoteState => Sync.State;

        public bool HasPendingUpload => Sync.HasPendingUpload;

        public async Task<LateNoMoreConfig> PullConfigAsync()
        {
            _lastConfigPull = DateTime.Now;
            var c = await Sync.PullAsync();
            // TODO(late-no-more): until the backend's /late-no-more/config (PR feat/late-no-more-config) is deployed it
            // answers 404, and the settings live only in the local copy below. Remove this note once it is live.
            if (Sync.State == LateNoMoreRemoteState.NotDeployed)
                LogWriter.LogWrite("LateNoMore: /late-no-more/config not deployed yet - using the settings stored on this device");
            RaiseConfigChanged();
            return c;
        }

        public async Task<LateNoMoreConfig> SaveConfigAsync(LateNoMoreConfig config)
        {
            var c = await Sync.SaveAsync(config);
            LogWriter.LogWrite("[user-action] LateNoMore settings saved (" + Sync.State + (Sync.HasPendingUpload ? ", pending upload" : "") + ")");
            RaiseConfigChanged();
            Evaluate();
            return c;
        }

        private void RaiseConfigChanged()
        {
            try { ConfigChanged?.Invoke(); } catch (Exception ex) { LogWriter.LogWrite("LateNoMore ConfigChanged - " + ex.Message); }
        }

        /// <summary>Replaces where the local copy is kept (the harness points it at a scratch file).</summary>
        internal string LocalPathOverride { get; set; }

        private string LocalPath => LocalPathOverride ?? Path.Combine(AppSettings.AppDataDirPath, "LateNoMore",   // AppDataDirPath is the E2E build's own folder there
            "config-" + SafeName(UserId ?? "signed-out") + ".json");

        private static string SafeName(string s) => new string(s.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());

        private LateNoMoreLocalCopy LoadLocal()
        {
            try { return File.Exists(LocalPath) ? LateNoMoreLocalCopy.FromJson(File.ReadAllText(LocalPath)) : null; }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore LoadLocal - " + ex.Message); return null; }
        }

        private void SaveLocal(LateNoMoreLocalCopy copy)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));
                string temp = LocalPath + ".tmp";
                File.WriteAllText(temp, copy.ToJson());
                if (File.Exists(LocalPath)) File.Replace(temp, LocalPath, null); else File.Move(temp, LocalPath);
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore SaveLocal - " + ex.Message); }
        }

        // ------------------------------------------------------------------ the schedule

        /// <summary>Starts the schedule (once). Called when the signed-in workflow starts.</summary>
        public void Start()
        {
            if (_timer != null) return;
            LogWriter.LogWrite("LateNoMore: started (tick " + TickSeconds + " s, calendar every " + CalendarRefreshMinutes + " min)");
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(TickSeconds) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
            _ = PullConfigAsync();
            Tick();
        }

        private async void Tick()
        {
            try
            {
                if (string.IsNullOrEmpty(UserId)) return;
                if ((DateTime.Now - _lastConfigPull).TotalMinutes >= ConfigPullMinutes) _ = PullConfigAsync();
                if (!Config.Enabled) return;
                if (!_refreshing && (DateTime.Now - _lastCalendarRefresh).TotalMinutes >= CalendarRefreshMinutes)
                {
                    _refreshing = true;
                    _lastCalendarRefresh = DateTime.Now;
                    try
                    {
                        // Two days, so a meeting just after midnight is seen before it; the manager keeps a wider window it
                        // already holds and skips the read entirely when the Day Plan refreshed within CalendarFreshSeconds.
                        await DayPlanCalendarManager.Shared.RefreshAsync(false, CalendarFreshSeconds, null, 2);
                    }
                    finally { _refreshing = false; }
                }
                Evaluate();
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore Tick - " + ex.Message); }
        }

        /// <summary>Meetings Late No More looks at: from a quarter hour ago to the largest lead from now.</summary>
        public List<LateNoMoreEvent> UpcomingMeetings(LateNoMoreConfig config)
        {
            DateTime now = DateTime.Now;
            int lead = config?.AlertLeadMinutes?.DefaultIfEmpty(15).Max() ?? 15;
            return DayPlanCalendarManager.Shared.LateNoMoreEvents(now - LateNoMoreScheduler.StaleAfterStart, now.AddMinutes(Math.Max(lead, 60) + 5));
        }

        /// <summary>Upcoming meetings for the rest of today and tomorrow (the settings screen's status line).</summary>
        public List<LateNoMoreEvent> MeetingsAhead() =>
            DayPlanCalendarManager.Shared.LateNoMoreEvents(DateTime.Now - LateNoMoreScheduler.StaleAfterStart, DateTime.Today.AddDays(2));

        private void Evaluate()
        {
            var config = Config;
            if (!config.Enabled) return;
            DateTime now = DateTime.Now;
            var events = UpcomingMeetings(config);
            foreach (LateNoMoreAlert alert in LateNoMoreScheduler.DueAlerts(config, events, now, _fired, Dismissals))
            {
                foreach (string k in LateNoMoreScheduler.StagesToMarkFired(config, alert)) _fired.Add(k);
                LogWriter.LogWrite("LateNoMore: " + alert.Kind + " alert (" + alert.LeadMinutes + " min lead) for a meeting at " + alert.Meeting.Start.ToString("HH:mm"));
                Show(alert, config, test: false);
            }
            // Snoozed alerts come back when their minute is up (and stop once the meeting is stale).
            foreach (var kv in _snoozedUntil.ToList())
            {
                if (now < kv.Value) continue;
                _snoozedUntil.Remove(kv.Key);
                var meeting = events.FirstOrDefault(e => e.Key == kv.Key);
                if (meeting != null && LateNoMoreScheduler.WhySkipped(config, meeting, now, Dismissals) == LateNoMoreScheduler.Skip.None)
                    Show(new LateNoMoreAlert(meeting, config.MainAlertMinutes ?? 2, LateNoMoreAlertKind.Main), config, test: false);
            }
            if (_fired.Count > 500) _fired.Clear();   // a day of meetings is far fewer; the stale guard stops re-alerts anyway
        }

        // ------------------------------------------------------------------ alerts

        /// <summary>The settings screen's "Test alert": a made-up meeting two minutes out. Nothing is reported or started.</summary>
        public void ShowTestAlert()
        {
            LogWriter.LogWrite("[user-action] LateNoMore test alert");
            DateTime start = DateTime.Now.AddMinutes(2);
            var meeting = new LateNoMoreEvent("lnm-test-" + Guid.NewGuid().ToString("N"), "Late No More test meeting", start, start.AddMinutes(30),
                null, null, "https://meet.google.com/", false, "Test");
            Show(new LateNoMoreAlert(meeting, 2, LateNoMoreAlertKind.Main), Config, test: true);
        }

        private void Show(LateNoMoreAlert alert, LateNoMoreConfig config, bool test)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    string key = alert.Meeting.Key;
                    if (_open.TryGetValue(key, out LateNoMoreAlertWindow existing))
                    {
                        if (alert.Kind == LateNoMoreAlertKind.HeadsUp || existing.Kind == LateNoMoreAlertKind.Main) { existing.BringForward(); return; }
                        existing.CloseQuietly();   // the heads-up gives way to the full alert
                    }
                    var window = new LateNoMoreAlertWindow(alert.Meeting, alert.Kind, config.VerbalAlertsEnabled, test);
                    window.Choice += choice => OnChoice(window, alert.Meeting, choice, test);
                    window.Closed += (s, e) => { if (_open.TryGetValue(key, out var w) && w == window) _open.Remove(key); };
                    _open[key] = window;
                    window.ShowAlert();
                    if (!test && alert.Kind == LateNoMoreAlertKind.Main) StartJoinWatch();
                    if (!test && alert.Kind == LateNoMoreAlertKind.Main)
                        _settings.CapturePostHog("late-no-more-alert-shown", "\"lead_minutes\":\"" + alert.LeadMinutes + "\"");
                    // The Mac starts the meeting session as soon as the meeting is announced when the accountability
                    // buddy is on (so the session supersedes an all-day block schedule for the whole meeting).
                    if (!test && alert.Kind == LateNoMoreAlertKind.Main && _settings.bAccountabilityBuddyEnabled)
                        ((App)app).StartLateNoMoreMeetingSession(alert.Meeting, postponeRoutine: false);
                }
                catch (Exception ex) { LogWriter.LogWrite("LateNoMore Show - " + ex.Message); }
            }));
        }

        private void OnChoice(LateNoMoreAlertWindow window, LateNoMoreEvent meeting, LateNoMoreAlertChoice choice, bool test)
        {
            LogWriter.LogWrite("[user-action] LateNoMore " + choice + (test ? " (test alert)" : ""));
            switch (choice)
            {
                case LateNoMoreAlertChoice.Join:
                    OpenLink(meeting.MeetingUrl, test);
                    if (!test) Attend(meeting);
                    break;
                case LateNoMoreAlertChoice.NotAttending:
                    if (!test)
                    {
                        Dismiss(meeting, LateNoMoreSyncContract.ReasonStopReminders);
                        // The alert may already have started the meeting's session (accountability buddy on): as on the
                        // Mac, not attending cancels it, or it would hold the user to the meeting's allow-list anyway.
                        try { ((App)Application.Current).EndLateNoMoreMeetingSession(meeting); }
                        catch (Exception ex) { LogWriter.LogWrite("LateNoMore NotAttending - " + ex.Message); }
                    }
                    window.Close();
                    break;
                case LateNoMoreAlertChoice.Snooze:
                    if (!test) _snoozedUntil[meeting.Key] = DateTime.Now.AddSeconds(SnoozeSeconds);
                    window.Close();
                    break;
                case LateNoMoreAlertChoice.GotIt:
                    window.Close();
                    break;
            }
        }

        // ------------------------------------------------------------------ join detection

        /// <summary>
        /// Watches the open alerts for the user joining their meeting (report 2026-09-28_16-01-47: no "I'm attending"
        /// button - "it's meant to auto detect if you join the meeting"). As on the Mac, a meeting app using the
        /// microphone or camera means the user is on the call; the meeting's own page in the browser counts too. Runs
        /// only while an alert is waiting, every <see cref="JoinCheckSeconds"/> seconds.
        /// </summary>
        private void StartJoinWatch()
        {
            if (_joinTimer == null)
            {
                _joinTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(JoinCheckSeconds) };
                _joinTimer.Tick += (s, e) => CheckJoined();
            }
            if (!_joinTimer.IsEnabled) _joinTimer.Start();
        }

        private void CheckJoined()
        {
            try
            {
                var waiting = _open.Values.Where(w => w.Kind == LateNoMoreAlertKind.Main && !w.IsTest && !w.Joined && w.IsLoaded).ToList();
                if (waiting.Count == 0) { _joinTimer?.Stop(); return; }
                DateTime now = DateTime.Now;
                string url = _settings.sLastActiveURLInFirefox;   // the foreground browser's address bar, for every browser
                bool? onCall = null;
                foreach (var window in waiting)
                {
                    LateNoMoreJoinSignal signal = LateNoMoreJoinDetection.Detect(window.Meeting, now, false, url);
                    if (signal == LateNoMoreJoinSignal.None)
                    {
                        if (onCall == null) onCall = IsOnCall();
                        signal = LateNoMoreJoinDetection.Detect(window.Meeting, now, onCall.Value, null);
                    }
                    if (signal == LateNoMoreJoinSignal.None) continue;
                    LogWriter.LogWrite("LateNoMore: joined the meeting at " + window.Meeting.Start.ToString("HH:mm") + " (detected: " + signal + ")");
                    Attend(window.Meeting);
                    window.ShowJoined(JoinedCloseAfter);
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore CheckJoined - " + ex.Message); }
        }

        /// <summary>
        /// A Meetings-mode app on the microphone or camera. The background timer keeps <c>bIsUserOnCall</c> while
        /// meeting detection is on; with it off, ask directly (only while an alert is waiting, so it costs nothing otherwise).
        /// </summary>
        private bool IsOnCall()
        {
            if (_settings.bIsUserOnCall) return true;
            if (_settings.bEnableMeetingsDetection) return false;
            try { return Focus_Bear.CallDetection.AudioUtilities.IsUserOnaAudioCall() || Focus_Bear.CallDetection.CameraUtilities.IsUserOnVideoCall(); }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore IsOnCall - " + ex.Message); return false; }
        }

        /// <summary>The user is going: remember it, tell the other devices, and cover the meeting with a focus session.</summary>
        private void Attend(LateNoMoreEvent meeting)
        {
            Dismiss(meeting, LateNoMoreSyncContract.ReasonJoinedOnWindows);
            try { ((App)Application.Current).StartLateNoMoreMeetingSession(meeting, postponeRoutine: true); }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore Attend - " + ex.Message); }
        }

        private void Dismiss(LateNoMoreEvent meeting, string reason)
        {
            Dismissals.Record(meeting.AsMeeting(), DateTime.Now);
            _snoozedUntil.Remove(meeting.Key);
            _ = new FocusBearAPIClient().ReportLateNoMoreDismissal(meeting.AsMeeting(), reason);
        }

        private static void OpenLink(string url, bool test)
        {
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore OpenLink - " + ex.Message); }
        }

        /// <summary>
        /// Pusher <c>late-no-more-event-dismissed</c>: the meeting was dealt with on another device (the phone's "stop
        /// reminders", or a Mac that joined it). Remember it and close its alert here.
        /// </summary>
        public void OnDismissedElsewhere(string json)
        {
            try
            {
                var o = JObject.Parse(json);
                string title = (string)o["event_title"];
                if (string.IsNullOrWhiteSpace(title)) return;
                DateTime start = DateTime.TryParse((string)o["event_begins"], System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime utc)
                    ? utc.ToLocalTime() : DateTime.Now;
                var meeting = new LateNoMoreMeeting((string)o["event_id"], title, start);
                Dismissals.Record(meeting, DateTime.Now);
                LogWriter.LogWrite("LateNoMore: a meeting was dismissed on another device");
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var kv in _open.ToList())
                        if (LateNoMoreEventMatcher.IsSameMeeting(kv.Value.Meeting.AsMeeting(), meeting)) kv.Value.CloseQuietly();
                }));
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore OnDismissedElsewhere - " + ex.Message); }
        }
    }
}
