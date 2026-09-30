using System;
using System.Threading.Tasks;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan's settings edits PUT the whole settings document. They must start from the server's
    /// latest copy, or they undo whatever another device changed since this one launched.
    /// </summary>
    [TestClass]
    public class DayPlanSettingsEditsSeamTests
    {
        private string _savedRaw, _savedStart;
        private Func<Task<string>> _fetch;
        private Func<Task<bool>> _put;
        private Action _local;

        [TestInitialize]
        public void Seams()
        {
            _savedRaw = AppSettings.GetInstance.sRawUserSettings;
            _savedStart = AppSettings.GetInstance.sDayStartTime;
            _fetch = DayPlanSettingsEdits.FetchLatestSettings;
            _put = DayPlanSettingsEdits.PutSettings;
            _local = DayPlanSettingsEdits.SaveLocalCopy;
            DayPlanSettingsEdits.SaveLocalCopy = () => { };   // never the real registry
            DayPlanSettingsEdits.ClearUnsynced();
        }

        [TestCleanup]
        public void Restore()
        {
            DayPlanSettingsEdits.FetchLatestSettings = _fetch;
            DayPlanSettingsEdits.PutSettings = _put;
            DayPlanSettingsEdits.SaveLocalCopy = _local;
            AppSettings.GetInstance.sRawUserSettings = _savedRaw;
            AppSettings.GetInstance.sDayStartTime = _savedStart;
            DayPlanSettingsEdits.ClearUnsynced();
        }

        [TestMethod]
        public void AHangingFetch_FallsBackToTheLocalCopyQuickly()
        {
            // The GET went through an HttpClient with the default 100 s timeout, and the Day Plan sat busy that long
            // on a captive or dying connection before the edit was even tried.
            AppSettings.GetInstance.sRawUserSettings = Settings("Stretch", "07:00");
            var never = new TaskCompletionSource<string>();
            DayPlanSettingsEdits.FetchLatestSettings = () => never.Task;
            TimeSpan saved = DayPlanSettingsEdits.FetchTimeout;
            DayPlanSettingsEdits.FetchTimeout = TimeSpan.FromMilliseconds(200);
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                JObject root = DayPlanSettingsEdits.LatestSettingsAsync().GetAwaiter().GetResult();
                Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
                Assert.AreEqual("Stretch", (string)root[RoutineSettingsKey.MorningActivities][0]["name"], "the local copy");
            }
            finally { DayPlanSettingsEdits.FetchTimeout = saved; }
        }

        [TestMethod]
        public void AnOfflineStartNow_IsNotUndoneByTheNextEditOnceOnline()
        {
            // "Start morning routine now" offline keeps its start time locally (its PUT fails). The next edit fetches
            // the server's copy - which never got it - and PUT that back, undoing it on this device and the server.
            AppSettings.GetInstance.sRawUserSettings = Settings("Stretch", "07:00");
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult<string>(null);
            DayPlanSettingsEdits.PutSettings = () => Task.FromResult(false);
            AppSettings.GetInstance.ChangeStartupTimeToCurrentTimeAndSaveSettingsAsync(new DateTime(2026, 9, 27, 5, 42, 0)).GetAwaiter().GetResult();

            // Back online: a habit edit on another device, then an evening timing edit here.
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult(Settings("Yoga", "07:00"));
            string put = null;
            DayPlanSettingsEdits.PutSettings = () => { put = AppSettings.GetInstance.sRawUserSettings; return Task.FromResult(true); };
            Assert.IsTrue(DayPlanSettingsEdits.SaveRoutineTimingAsync(RoutineSettingsSection.Evening, 22 * 60, null).GetAwaiter().GetResult());

            var body = JObject.Parse(put);
            Assert.AreEqual("05:42", (string)body[RoutineSettingsKey.StartupTime], "the unsynced start time rides along");
            Assert.AreEqual("Yoga", (string)body[RoutineSettingsKey.MorningActivities][0]["name"], "and the other device's change stays");
            Assert.AreEqual("22:00", (string)body[RoutineSettingsKey.ShutdownTime]);

            // Once it has reached the server it is no longer re-applied.
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult(Settings("Yoga", "06:15"));
            Assert.IsTrue(DayPlanSettingsEdits.SaveRoutineTimingAsync(RoutineSettingsSection.Evening, 22 * 60, null).GetAwaiter().GetResult());
            Assert.AreEqual("06:15", (string)JObject.Parse(put)[RoutineSettingsKey.StartupTime], "a later change elsewhere wins");
        }

        private static string Settings(string habitName, string startup) => new JObject
        {
            [RoutineSettingsKey.StartupTime] = startup,
            [RoutineSettingsKey.ShutdownTime] = "21:00",
            [RoutineSettingsKey.MorningActivities] = new JArray(new JObject { ["id"] = "h1", ["name"] = habitName, ["duration_seconds"] = 60 }),
            [RoutineSettingsKey.EveningActivities] = new JArray(),
        }.ToString();

        [TestMethod]
        public void ARoutineTimeEdit_KeepsAChangeMadeOnAnotherDeviceSinceLaunch()
        {
            AppSettings.GetInstance.sRawUserSettings = Settings("Stretch", "07:00");                   // fetched at launch
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult(Settings("Yoga", "07:00")); // renamed on the web since
            string put = null;
            DayPlanSettingsEdits.PutSettings = () => { put = AppSettings.GetInstance.sRawUserSettings; return Task.FromResult(true); };

            bool saved = DayPlanSettingsEdits.SaveRoutineTimingAsync(RoutineSettingsSection.Morning, 8 * 60, null).GetAwaiter().GetResult();

            Assert.IsTrue(saved);
            var body = JObject.Parse(put);
            Assert.AreEqual("08:00", (string)body[RoutineSettingsKey.StartupTime]);
            Assert.AreEqual("Yoga", (string)body[RoutineSettingsKey.MorningActivities][0]["name"], "the other device's rename survives this edit");
        }

        [TestMethod]
        public void Offline_TheEditStartsFromTheLocalCopy()
        {
            AppSettings.GetInstance.sRawUserSettings = Settings("Stretch", "07:00");
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult<string>(null);
            DayPlanSettingsEdits.PutSettings = () => Task.FromResult(false);

            bool saved = DayPlanSettingsEdits.SaveRoutineTimingAsync(RoutineSettingsSection.Morning, 8 * 60, null).GetAwaiter().GetResult();

            Assert.IsFalse(saved);
            Assert.AreEqual("07:00", (string)JObject.Parse(AppSettings.GetInstance.sRawUserSettings)[RoutineSettingsKey.StartupTime], "a refused save is rolled back");
        }

        [TestMethod]
        public void StartingTheMorningNow_KeepsAChangeMadeOnAnotherDevice()
        {
            AppSettings.GetInstance.sRawUserSettings = Settings("Stretch", "07:00");
            DayPlanSettingsEdits.FetchLatestSettings = () => Task.FromResult(Settings("Yoga", "07:00"));
            string put = null;
            DayPlanSettingsEdits.PutSettings = () => { put = AppSettings.GetInstance.sRawUserSettings; return Task.FromResult(true); };

            AppSettings.GetInstance.ChangeStartupTimeToCurrentTimeAndSaveSettingsAsync(new DateTime(2026, 9, 27, 5, 42, 0)).GetAwaiter().GetResult();

            var body = JObject.Parse(put);
            Assert.AreEqual("05:42", (string)body[RoutineSettingsKey.StartupTime]);
            Assert.AreEqual("Yoga", (string)body[RoutineSettingsKey.MorningActivities][0]["name"]);
            Assert.AreEqual("05:42", AppSettings.GetInstance.sDayStartTime);
        }
    }
}
