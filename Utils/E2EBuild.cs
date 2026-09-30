using System;
using FocusBear.Core;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The isolated E2E test build: the app compiled with <c>-p:FocusBearE2E=true</c>, which defines <c>E2E</c> and
    /// writes to bin\E2E\. It is what the pywinauto suite (Focus-Bear/windows-app-e2e-tests) launches, so the tests
    /// can run on a machine where someone's own Focus Bear is running and signed in, without touching it. The Mac
    /// equivalent is the <c>.uitests</c> bundle id (Mac-App#2655).
    ///
    /// Under E2E the app keeps to its own copy of everything the two could share:
    /// <list type="bullet">
    /// <item>%LOCALAPPDATA%\Focus Bear E2E (settings files, logs, WebView2 profiles, music cache) and
    /// %USERPROFILE%\FocusBear-Claude-Tasks-E2E — see <see cref="AppInstance"/>, which also keeps telemetry off;</item>
    /// <item>HKCU\Software\Focus Bear E2E for every registry value, including the per-account keys, the backup
    /// key and the watchdog's clean-exit marker;</item>
    /// <item>its own single-instance mutex and wake-up event;</item>
    /// <item>no logon task, Run key, watchdog task or scheduled tasks; no update check or installer; no Focus Button
    /// serial connection; no global keyboard shortcuts; never shuts down or sleeps the PC;</item>
    /// <item>no blocking enforcement (the system-wide focus and window-event hooks, the keyboard hook of full-screen
    /// routines) unless the test runner opts in with <see cref="EnableBlockingVariable"/>;</item>
    /// <item>" (E2E)" after every window title and the tray icon's name.</item>
    /// </list>
    /// Test seams, each only in the E2E build and only while its variable is set (the constants below say what each
    /// value does): FOCUSBEAR_E2E_ENABLE_BLOCKING, FOCUSBEAR_E2E_IGNORE_SHUTOFF, FOCUSBEAR_E2E_BREAKS,
    /// FOCUSBEAR_E2E_CALENDAR_FIXTURE (Day Plan meetings from a JSON file), FOCUSBEAR_E2E_FAKE_AI (the AI URL / app
    /// check), FOCUSBEAR_E2E_FAKE_BUDDY (the accountability buddy), FOCUSBEAR_E2E_LOCAL_REPORTS (local reports for any
    /// account), FOCUSBEAR_E2E_EXPOSE_FONTS (tooltip fonts through UI Automation) and FOCUSBEAR_E2E_START_ROUTINE
    /// (a routine now).
    /// A normal build compiles every check here to "not E2E", so shipping behaviour is unchanged.
    /// </summary>
    internal static class E2EBuild
    {
#if E2E
        internal static readonly bool IsActive = true;
#else
        internal static readonly bool IsActive = false;
#endif

        /// <summary>Set to 1 by a test that needs the blocking engine. Ignored by every non-E2E build.</summary>
        internal const string EnableBlockingVariable = "FOCUSBEAR_E2E_ENABLE_BLOCKING";

        /// <summary>
        /// Set to 1 by a test that needs breaks outside the account's day (shut-off time to day start), such as the
        /// break warning tests run overnight. Ignored by every non-E2E build.
        /// </summary>
        internal const string IgnoreShutOffVariable = "FOCUSBEAR_E2E_IGNORE_SHUTOFF";

        internal static readonly bool IgnoreShutOffTime =
            IsActive && AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(IgnoreShutOffVariable));

        /// <summary>
        /// Set to 1 by the break tests. Without it the E2E build shows no micro breaks, so a break habit on the
        /// test account can't put a break over the screen another test is checking. Ignored by every other build.
        /// </summary>
        internal const string BreaksVariable = "FOCUSBEAR_E2E_BREAKS";

        internal static readonly bool BreaksAllowed =
            !IsActive || AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(BreaksVariable));

        // ---- test seams: each only in the E2E build, and only when its variable is set ----------------------------

        /// <summary>The variable's value, trimmed, when this is the E2E build and it is set; otherwise null.</summary>
        private static string Seam(string variable)
        {
            if (!IsActive) return null;
            string v = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        /// <summary>
        /// Path to a JSON file of meetings the Day Plan shows instead of the backend calendar
        /// (Habits\DayPlan\DayPlanCalendarFixture.cs has the schema). Read again on every calendar refresh.
        /// </summary>
        internal const string CalendarFixtureVariable = "FOCUSBEAR_E2E_CALENDAR_FIXTURE";

        internal static string CalendarFixturePath => Seam(CalendarFixtureVariable);

        /// <summary>
        /// The AI "is this URL / app aligned with the session?" check (is-url-safe-to-use, is-app-safe-to-use)
        /// answers without calling the backend: <c>allow</c> (probability 1, allowed silently), <c>checkin</c> or
        /// <c>block</c> (probability 0.1 with a reason: the check-in, or in grizzly mode the block), or a number from
        /// 0 to 1. The reason given is "E2E fake AI: ...".
        /// </summary>
        internal const string FakeAiVariable = "FOCUSBEAR_E2E_FAKE_AI";

        internal static bool TryFakeAi(string item, out double probability, out string reason)
        {
            probability = 0;
            reason = null;
            string v = Seam(FakeAiVariable);
            if (v == null) return false;
            switch (v.ToLowerInvariant())
            {
                case "allow": probability = 1.0; break;
                case "checkin":
                case "block": probability = 0.1; break;
                default:
                    if (!double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out probability))
                        return false;
                    probability = Math.Max(0, Math.Min(1, probability));
                    break;
            }
            reason = probability >= 0.8
                ? "E2E fake AI: " + item + " fits the session."
                : "E2E fake AI: " + item + " does not look related to what you said you'd focus on.";
            return true;
        }

        /// <summary>
        /// The AI accountability buddy (POST /user/evaluate-focus-postponement) answers from a script instead of the
        /// backend: <c>approve</c>, <c>deny</c>, <c>deny-then-approve</c> (denies the first user message, approves
        /// the next), <c>unavailable</c> / <c>offline</c> (the recovery card), or a path to a JSON file holding an
        /// array of backend-shaped replies, e.g. <c>[{"verdict":"deny","reason":"..."},{"verdict":"approve",
        /// "reason":"...","intention":"...","allowedApps":["notepad"],"allowedUrls":["example.com"],
        /// "clampedDurationMinutes":15}]</c>. Reply N answers the Nth user message (the last one repeats); an entry
        /// <c>{"status":"offline"}</c> or <c>{"status":"unavailable"}</c> fails that turn as those modes do. The file is
        /// read on every message, so a test can rewrite it while the app runs.
        /// An approval unlocks notepad and example.com for 15 minutes unless the file says otherwise.
        /// </summary>
        internal const string FakeBuddyVariable = "FOCUSBEAR_E2E_FAKE_BUDDY";

        internal static bool TryFakeBuddy(Newtonsoft.Json.Linq.JObject request, out FocusBear.Core.Postpone.AccountabilityReply reply)
        {
            reply = null;
            string v = Seam(FakeBuddyVariable);
            if (v == null) return false;
            var messages = request?["messages"] as Newtonsoft.Json.Linq.JArray;
            var userMessages = new System.Collections.Generic.List<string>();
            if (messages != null)
                foreach (var m in messages)
                    if ((string)m["role"] == "user") userMessages.Add((string)m["content"] ?? "");
            int turn = Math.Max(1, userMessages.Count);
            string goal = userMessages.Count > 0 ? userMessages[0] : "E2E goal";

            string Approve() => new Newtonsoft.Json.Linq.JObject
            {
                ["verdict"] = "approve",
                ["reason"] = "E2E fake buddy: approved - go and do that, then back to your habits.",
                ["intention"] = goal,
                ["allowedApps"] = new Newtonsoft.Json.Linq.JArray("notepad"),
                ["allowedUrls"] = new Newtonsoft.Json.Linq.JArray("example.com"),
                ["clampedDurationMinutes"] = 15,
            }.ToString();
            string Deny() => new Newtonsoft.Json.Linq.JObject
            {
                ["verdict"] = "deny",
                ["reason"] = "E2E fake buddy: that can wait until after your habits (" + turn + ").",
            }.ToString();

            string body;
            switch (v.ToLowerInvariant())
            {
                case "approve": body = Approve(); break;
                case "deny": body = Deny(); break;
                case "deny-then-approve": body = turn <= 1 ? Deny() : Approve(); break;
                case "offline":
                    reply = FocusBear.Core.Postpone.AccountabilityReply.From(false, false, null);
                    LogFakeBuddy("offline");
                    return true;
                case "unavailable":
                    reply = FocusBear.Core.Postpone.AccountabilityReply.From(true, false, null);
                    LogFakeBuddy("unavailable");
                    return true;
                default:
                    try
                    {
                        var script = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(v));
                        var entry = script.Count == 0 ? null : script[Math.Min(turn, script.Count) - 1];
                        // {"status":"offline"} / {"status":"unavailable"}: that turn fails the way those modes do.
                        string status = (entry as Newtonsoft.Json.Linq.JObject)?.Value<string>("status");
                        if (status == "offline" || status == "unavailable")
                        {
                            reply = FocusBear.Core.Postpone.AccountabilityReply.From(status == "unavailable", false, null);
                            LogFakeBuddy(status);
                            return true;
                        }
                        body = entry == null ? Deny() : entry.ToString();
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("E2E fake buddy script ex - " + ex.Message);
                        reply = FocusBear.Core.Postpone.AccountabilityReply.From(true, false, null);
                        return true;
                    }
                    break;
            }
            LogFakeBuddy(body);
            reply = FocusBear.Core.Postpone.AccountabilityReply.From(true, true, body);
            return true;
        }

        private static void LogFakeBuddy(string what) => LogWriter.LogWrite("E2E fake buddy: " + what.Replace("\r", "").Replace("\n", " "));

        /// <summary>
        /// Set to 1: every tooltip, when it opens, reports the font its text is drawn in as its UI Automation
        /// ItemStatus ("font:Segoe UI"), so a test can check that no tooltip uses an icon font.
        /// </summary>
        internal const string ExposeFontsVariable = "FOCUSBEAR_E2E_EXPOSE_FONTS";

        internal static bool ExposeFonts => AutomatedRun.IsTruthy(Seam(ExposeFontsVariable));

        /// <summary>
        /// Set to 1: any signed-in account gets local reports (Help, Report Problem), stored in the E2E build's own
        /// %USERPROFILE%\FocusBear-Claude-Tasks-E2E. Read by FocusBear.Core.Reports.LocalReportCapability.
        /// </summary>
        internal const string LocalReportsVariable = FocusBear.Core.Reports.LocalReportCapability.E2EVariable;

        /// <summary>
        /// <c>morning</c> or <c>evening</c>: that routine starts at launch as if its time had come, even if today's is
        /// already done, and the time of day stays on its side for the run (<see cref="AppSettings.IsShutOffTime"/>:
        /// morning is never shut-off time, evening always is). The routine's habits are the test account's own, from
        /// its settings (Edit Habits); an account with no habits in that routine gets none. Without it the E2E build never
        /// starts the morning routine by the clock (its habits count as none for the day), so a routine can't cover another
        /// test's screen.
        /// </summary>
        internal const string StartRoutineVariable = "FOCUSBEAR_E2E_START_ROUTINE";

        internal static string StartRoutine
        {
            get
            {
                string v = Seam(StartRoutineVariable)?.ToLowerInvariant();
                return v == "morning" || v == "evening" ? v : null;
            }
        }

        /// <summary>FOCUSBEAR_E2E_TRACE_URLS=1: log each browser address-bar value the URL hook reads (blocking tests).</summary>
        internal static readonly bool TraceUrls =
            IsActive && AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable("FOCUSBEAR_E2E_TRACE_URLS"));

        /// <summary>
        /// The named event a test sets to end the running focus session at once (the same stop as End Session,
        /// without the confirmation box): tests end a session in their teardown this way instead of through the
        /// screens. Created only by the E2E build; its name is <see cref="InstanceName"/> of this.
        /// </summary>
        internal const string EndSessionEventName = "FocusBearE2E-EndFocusSession";

        /// <summary>Waits (on a background thread) for <see cref="EndSessionEventName"/> and runs <paramref name="end"/> each time.</summary>
        internal static void ListenForEndSession(Action end) => ListenFor(EndSessionEventName, end);

        /// <summary>
        /// The named event a test sets to start the morning routine again from its first habit, in this process: what
        /// a relaunch with <see cref="StartRoutineVariable"/>=morning does, without the ~8 s of a restart. Only with that
        /// variable set (the app is already a routine test's app). Its name is <see cref="InstanceName"/> of this.
        /// </summary>
        internal const string RestartRoutineEventName = "FocusBearE2E-RestartRoutine";

        /// <summary>Waits for <see cref="RestartRoutineEventName"/> and runs <paramref name="restart"/> each time.</summary>
        internal static void ListenForRestartRoutine(Action restart)
        {
            if (StartRoutine != "morning") return;
            ListenFor(RestartRoutineEventName, restart);
        }

        /// <summary>Waits (on a background thread) for the named event and runs <paramref name="action"/> each time it is set.</summary>
        private static void ListenFor(string eventName, Action action)
        {
            if (!IsActive || action == null) return;
            try
            {
                var signal = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, InstanceName(eventName));
                var thread = new System.Threading.Thread(() =>
                {
                    while (true)
                    {
                        signal.WaitOne();
                        try { action(); } catch (Exception ex) { LogWriter.LogWrite("E2E " + eventName + " ex - " + ex.Message); }
                    }
                }) { IsBackground = true, Name = "E2E " + eventName };
                thread.Start();
            }
            catch (Exception ex) { LogWriter.LogWrite("E2E " + eventName + " listener ex - " + ex.Message); }
        }

        /// <summary>
        /// FOCUSBEAR_E2E_BREAK_SECONDS=n (with FOCUSBEAR_FAST_TIMERS): the first micro-break warning and a postponed
        /// one come after n seconds instead of 10 and 15, so the break tests don't wait. Null when unset.
        /// </summary>
        internal static int? FastBreakSeconds =>
            int.TryParse(Seam("FOCUSBEAR_E2E_BREAK_SECONDS"), out int s) && s > 0 ? s : (int?)null;

        /// <summary>
        /// FOCUSBEAR_E2E_POSTPONE_SECONDS=n: a one-minute routine postpone ends after n seconds, so a test of "the
        /// routine comes back when the postpone ends" doesn't wait a minute plus. Longer postpones are unchanged.
        /// Null when unset.
        /// </summary>
        internal static int? OneMinutePostponeSeconds =>
            int.TryParse(Seam("FOCUSBEAR_E2E_POSTPONE_SECONDS"), out int s) && s > 0 ? s : (int?)null;

        /// <summary>The routine postpone timer's length for <paramref name="minutes"/> (see <see cref="OneMinutePostponeSeconds"/>).</summary>
        internal static TimeSpan PostponeInterval(int minutes) =>
            minutes == 1 && OneMinutePostponeSeconds.HasValue ? TimeSpan.FromSeconds(OneMinutePostponeSeconds.Value) : TimeSpan.FromMinutes(minutes);

        /// <summary>Set by the app: whether the blocking engine is subscribed to focus changes (the probe's <c>fs</c>).</summary>
        internal static Func<bool> FocusSubscribed;

        private static long _focusHandledCount;
        private static int _focusHandledPid;

        /// <summary>
        /// E2E build only: the blocking engine has finished acting on a focus change into <paramref name="element"/>'s
        /// process. The probe's <c>fg</c> answers the count and that process, so a blocking test waits for the engine
        /// to have seen its app switch instead of sleeping.
        /// </summary>
        internal static void NoteFocusHandled(Interop.UIAutomationClient.IUIAutomationElement element)
        {
            if (!IsActive) return;
            int pid = 0;
            try { pid = element?.CurrentProcessId ?? 0; } catch { }
            System.Threading.Volatile.Write(ref _focusHandledPid, pid);
            System.Threading.Interlocked.Increment(ref _focusHandledCount);
        }

        /// <summary>"count,pid" of the focus changes the blocking engine has handled (see <see cref="NoteFocusHandled"/>).</summary>
        internal static string FocusHandledState() =>
            System.Threading.Interlocked.Read(ref _focusHandledCount) + "," + System.Threading.Volatile.Read(ref _focusHandledPid);

        internal const string TitleSuffix = " (E2E)";

        private const string DefaultRegistryRoot = @"Software\Focus Bear";
        private const string E2ERegistryRoot = @"Software\Focus Bear E2E";

        private const string TestHostRegistryRoot = @"Software\Focus Bear Tests";

        /// <summary>
        /// HKCU root of all the app's own registry values ("Software\Focus Bear" normally). A unit-test host running
        /// the app's code gets its own (<see cref="FocusBear.Core.AppInstance.IsTestHost"/>), so no test can read or
        /// overwrite the person's settings, tasks or tokens.
        /// </summary>
        internal static string RegistryRoot => IsActive ? E2ERegistryRoot + FocusBear.Core.AppInstance.E2EInstanceSuffix : FocusBear.Core.AppInstance.IsTestHost ? TestHostRegistryRoot : DefaultRegistryRoot;

        /// <summary>A name that must differ between the two builds, such as a mutex or event name.</summary>
        internal static string InstanceName(string name) => IsActive ? name + "-E2E" + FocusBear.Core.AppInstance.E2EInstanceSuffix.Replace(' ', '-') : name;

        /// <summary>
        /// Whether this process may enforce blocking system-wide. Always true for the real app; for the E2E build only
        /// when the runner opted in, so a test run never hides, minimises or closes the person's own windows.
        /// </summary>
        internal static bool BlockingEnforcementAllowed =>
            !IsActive || AutomatedRun.IsTruthy(Environment.GetEnvironmentVariable(EnableBlockingVariable));

        /// <summary>
        /// Whether the app may change the machine outside its own data: startup and scheduled tasks, update installs,
        /// shutting down or sleeping the PC, global shortcuts, the Focus Button. Never for the E2E build.
        /// </summary>
        internal static bool MachineIntegrationAllowed => !IsActive;

        /// <summary>
        /// Run first, from the App's static constructor. The compile-time switch and the assembly marker that
        /// FocusBear.Core reads (Properties\AssemblyInfo.cs) come from the same define, so they always agree; if a
        /// hand-edited build ever made them disagree, half the app would use the real locations, so it stops.
        /// </summary>
        internal static void EnsureConsistent()
        {
            if (IsActive != AppInstance.IsE2E)
            {
                Environment.Exit(3);
            }
        }

        /// <summary>The window title the E2E build shows: "(E2E)" appended once. An untitled window stays untitled.</summary>
        internal static string DecorateTitle(string title)
        {
            if (!IsActive || string.IsNullOrEmpty(title)) return title;
            return title.EndsWith(TitleSuffix, StringComparison.Ordinal) ? title : title + TitleSuffix;
        }
    }
}
