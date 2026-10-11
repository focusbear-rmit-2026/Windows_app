using System;
using System.Threading;
using System.Windows.Threading;
using Focus_Bear.Habits;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Ending an evening postpone at the cutoff only dropped the activity type to NONE. The postpone timer and
    /// the "resumes in 2 minutes" popup timer stayed armed, so once the user was back in the routine the old
    /// postpone fired, forced the routine to NONE mid-habit and flashed the resume popup; the persisted
    /// postpone also survived for a restart to restore.
    /// </summary>
    [TestClass]
    public class HabitsPostponeEndSeamTests
    {
        private static void OnSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new AssertFailedException(failure.ToString(), failure);
        }

        [TestMethod]
        public void EndingAPostponeDisarmsBothTimersAndForgetsIt()
        {
            OnSta(() =>
            {
                var s = AppSettings.GetInstance;
                bool savedPostponed = s.bHabitsPostponedState, savedAfter = s.bEveningHabitsPostponedAfterCutoffTime;
                try
                {
                    var postpone = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
                    var popup = new DispatcherTimer { Interval = TimeSpan.FromMinutes(28) };
                    postpone.Start();
                    popup.Start();
                    s.bHabitsPostponedState = true;
                    s.bEveningHabitsPostponedAfterCutoffTime = false;
                    bool hidden = false, forgotten = false;

                    HabitsPostponeEnd.Clear(s, postpone, popup, () => hidden = true, () => forgotten = true);

                    Assert.IsFalse(postpone.IsEnabled, "postpone timer still armed");
                    Assert.IsFalse(popup.IsEnabled, "resume-popup timer still armed");
                    Assert.IsTrue(hidden);
                    Assert.IsTrue(forgotten, "persisted postpone kept for a restart");
                    Assert.IsFalse(s.bHabitsPostponedState);
                }
                finally
                {
                    s.bHabitsPostponedState = savedPostponed;
                    s.bEveningHabitsPostponedAfterCutoffTime = savedAfter;
                }
            });
        }
    }
}
