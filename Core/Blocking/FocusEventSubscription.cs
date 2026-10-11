using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Whether the app needs UI Automation's system-wide focus-changed events. The handler acts only in these
    /// states (FocusChangeHandler.HandleFocusChangedEvent); registered all the time, UIA delivered - and marshalled
    /// into the app - every focus change in every app, all day, with nothing to block.
    /// </summary>
    public static class FocusEventSubscription
    {
        public static bool IsNeeded(bool isShutOff, bool habitWithAllowedAppsRunning, bool habitWithFocusModeRunning,
            bool isFocusModeRunning, bool isFocusModePaused, bool blockingScheduleActiveAndNotPaused) =>
            isShutOff
            || (habitWithAllowedAppsRunning && !habitWithFocusModeRunning)
            || (isFocusModeRunning && !isFocusModePaused)
            || habitWithFocusModeRunning
            || blockingScheduleActiveAndNotPaused;
    }

    /// <summary>
    /// The add / remove bookkeeping for a subscription whose calls can be slow or hang (UIA's remove can take a
    /// minute): only a change of what's wanted produces a call, and only one call is ever outstanding. A call that
    /// runs past its timeout is NOT assumed to have failed: it is "in doubt" until it really returns
    /// (<see cref="Completed"/>), because an add abandoned as timed out that lands later, followed by a retried
    /// add, would register the handler twice and process every focus change twice. A call that failed is tried
    /// again after <see cref="RetryAfter"/>.
    /// </summary>
    public sealed class SubscriptionState
    {
        public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

        private readonly object _lock = new object();
        private bool _wanted, _registered, _busy, _inDoubt;
        private DateTime _retryAt = DateTime.MinValue;

        public enum Step { None, Add, Remove }

        /// <summary>Records what is wanted; returns the call to make now (None while one is outstanding or nothing changed).</summary>
        public Step Want(bool wanted, DateTime nowUtc)
        {
            lock (_lock)
            {
                _wanted = wanted;
                return NextLocked(nowUtc);
            }
        }

        /// <summary>The call has run past its timeout and is still going: nothing else starts until it returns.</summary>
        public void TimedOut(Step step)
        {
            lock (_lock) { if (_busy) _inDoubt = true; }
        }

        /// <summary>The call returned (however late), <paramref name="succeeded"/> or not; returns the next call to make, if any.</summary>
        public Step Completed(Step step, bool succeeded, DateTime nowUtc)
        {
            lock (_lock)
            {
                _busy = false;
                _inDoubt = false;
                if (succeeded) _registered = step == Step.Add;
                else _retryAt = nowUtc + RetryAfter;
                return NextLocked(nowUtc);
            }
        }

        public bool IsRegistered { get { lock (_lock) return _registered; } }

        /// <summary>A call timed out and hasn't returned yet.</summary>
        public bool IsInDoubt { get { lock (_lock) return _inDoubt; } }

        private Step NextLocked(DateTime nowUtc)
        {
            if (_busy || _wanted == _registered || nowUtc < _retryAt) return Step.None;
            _busy = true;
            return _wanted ? Step.Add : Step.Remove;
        }
    }
}
