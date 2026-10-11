using Focus_Bear.Utils;
using System;
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Ends a routine postpone for good: both timers disarmed (the postpone itself and the "resumes in 2
    /// minutes" popup), the popup hidden, the postponed flags cleared and the persisted postpone forgotten.
    /// Any path that ends a postpone early must do all of it, or the old timer fires later and forces the
    /// routine to NONE mid-habit.
    /// </summary>
    internal static class HabitsPostponeEnd
    {
        internal static void Clear(AppSettings settings, DispatcherTimer postponeTimer, DispatcherTimer resumePopupTimer,
                                   Action hidePopup, Action forgetPersisted)
        {
            postponeTimer?.Stop();
            resumePopupTimer?.Stop();
            try { hidePopup?.Invoke(); } catch (Exception ex) { LogWriter.LogWrite("HabitsPostponeEnd hide popup - " + ex.Message); }
            settings.bHabitsPostponedState = false;
            settings.bEveningHabitsPostponedAfterCutoffTime = false;
            forgetPersisted?.Invoke();
        }
    }
}
