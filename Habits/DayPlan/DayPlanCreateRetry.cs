using Focus_Bear.Utils;
using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Keeps trying to give local-only Day Plan tasks their to-do (#1646). <see cref="DayPlanTaskStore.CreateTodoFor"/>
    /// runs when a task is named and <see cref="DayPlanTaskStore.SyncLocalOnlyTasks"/> when the Day Plan opens; a
    /// task typed while offline and then left alone used to wait for one of those. Now a sweep runs every minute
    /// (the store's own backoff decides which tasks are due), and the moment the network comes back the backoff is
    /// dropped and the sweep runs at once. Everything happens on the UI thread, like the store it drives.
    /// </summary>
    internal static class DayPlanCreateRetry
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
        private static DispatcherTimer _timer;

        /// <summary>Start the sweep timer and the network listener. Safe to call more than once.</summary>
        internal static void Start()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (_timer != null) return;
                _timer = new DispatcherTimer { Interval = SweepInterval };
                _timer.Tick += (s, e) => Sweep("timer");
                _timer.Start();
                NetworkChange.NetworkAvailabilityChanged += (s, e) =>
                {
                    if (!e.IsAvailable) return;
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        DayPlanTaskStore.Shared.ClearCreateBackoff();
                        Sweep("network back");
                    }));
                };
            }));
        }

        private static void Sweep(string why)
        {
            try
            {
                int started = DayPlanTaskStore.Shared.SyncLocalOnlyTasks(DateTime.Today);
                if (started > 0) LogWriter.LogWrite("DayPlanCreateRetry (" + why + "): retrying the to-do create for " + started + " task(s)");
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanCreateRetry - " + ex.Message); }
        }
    }
}
