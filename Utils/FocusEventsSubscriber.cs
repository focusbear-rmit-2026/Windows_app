using System;
using System.Threading;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;
using Interop.UIAutomationClient;

namespace FocusBear.Utils
{
    /// <summary>
    /// Registers the blocking code's UI Automation focus-changed handler only while something can be blocked
    /// (<see cref="FocusEventSubscription.IsNeeded"/>), instead of for the app's whole life. Registered, UIA
    /// delivers every focus change in every app to this process.
    ///
    /// Add and remove run on their own MTA thread against a UIA object created there (UIA recommends not calling
    /// it from a UI thread; a remove has been seen to take a minute), one at a time, and the caller never waits:
    /// <see cref="Update"/> only records what's wanted. The worker reports its own outcome whenever it returns; a
    /// call still going after <see cref="CallTimeout"/> is logged and holds back every other call until then
    /// (<see cref="SubscriptionState"/>), so a late add can't be followed by a second one.
    /// </summary>
    internal sealed class FocusEventsSubscriber
    {
        public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

        private readonly IUIAutomationFocusChangedEventHandler _handler;
        private readonly SubscriptionState _state = new SubscriptionState();
        private CUIAutomation _automation;

        public FocusEventsSubscriber(IUIAutomationFocusChangedEventHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public bool IsRegistered => _state.IsRegistered;

        /// <summary>Cheap: call it as often as the state may have changed (the 5 s tick, a session start).</summary>
        public void Update(bool needed)
        {
            Run(_state.Want(needed, DateTime.UtcNow));
        }

        private void Run(SubscriptionState.Step step)
        {
            if (step == SubscriptionState.Step.None) return;
            string what = step == SubscriptionState.Step.Add ? "started" : "stopped";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            // A task, not a ManualResetEventSlim + RegisterWaitForSingleObject: those were never disposed or
            // unregistered, one of each per add or remove.
            var done = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var worker = new Thread(() =>
            {
                bool ok = false;
                try
                {
                    if (_automation == null) _automation = new CUIAutomation();
                    if (step == SubscriptionState.Step.Add) _automation.AddFocusChangedEventHandler(null, _handler);
                    else _automation.RemoveFocusChangedEventHandler(_handler);
                    ok = true;
                }
                catch (Exception ex) { LogWriter.LogWrite("FocusEventsSubscriber " + step + " ex - " + ex.Message); }
                done.TrySetResult(ok);
                bool late = clock.Elapsed > CallTimeout;
                LogWriter.LogWrite("Blocking focus handler " + what + (ok ? "" : " - FAILED") + (late ? " (late)" : "") + " (" + clock.ElapsedMilliseconds + " ms)");
                // The only place a call is finished: the next one (if the wish changed meanwhile) starts from here.
                Run(_state.Completed(step, ok, DateTime.UtcNow));
            }) { IsBackground = true, Name = "UIA focus subscription" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
            // Only to say so in the log: a hung call is never abandoned, it holds the next one back.
            System.Threading.Tasks.Task.WhenAny(done.Task, System.Threading.Tasks.Task.Delay(CallTimeout)).ContinueWith(first =>
            {
                if (first.Result == done.Task) return;
                _state.TimedOut(step);
                LogWriter.LogWrite("Blocking focus handler " + what + " - still running after " + (int)CallTimeout.TotalSeconds + " s, waiting for it");
            }, System.Threading.Tasks.TaskScheduler.Default);
        }
    }
}
