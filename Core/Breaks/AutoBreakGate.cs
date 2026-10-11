namespace FocusBear.Core.Breaks
{
    /// <summary>What was true when the break warning's countdown ended.</summary>
    public sealed class AutoBreakConditions
    {
        public bool SkipBreaksDuringFocus { get; set; }
        public bool FocusSessionRunning { get; set; }
        public bool SuperFocusRunning { get; set; }
        public bool AppDeactivated { get; set; }
        /// <summary>An active, unpaused blocking schedule has micro-breaks turned off.</summary>
        public bool ScheduleDisablesBreaks { get; set; }
        public bool OnCall { get; set; }
        public bool ShowBreaksInCall { get; set; }
        /// <summary>A break is already on screen (the user started one during the warning).</summary>
        public bool BreakAlreadyRunning { get; set; }
        /// <summary>The day's shut-off time has come; breaks stop until the morning.</summary>
        public bool ShutOffTime { get; set; }
        /// <summary>The user chose "turn them off for today" from the deactivate-breaks prompt.</summary>
        public bool BreaksOffToday { get; set; }
    }

    public enum AutoBreakDecision
    {
        /// <summary>Start the break.</summary>
        Start,
        /// <summary>Don't start it; put the next break back on the timer.</summary>
        Reschedule,
        /// <summary>Don't start it and leave the timer alone (a break is already running, or it is shut-off time).</summary>
        Nothing,
    }

    /// <summary>
    /// The break warning counts down 30 seconds and then starts the break. The reasons the break timer holds a
    /// break back can become true during those 30 seconds - a focus session started with "skip micro-breaks
    /// during focus", the app deactivated, a call - so they are asked again when the countdown ends, as the
    /// Mac does (it starts the break only while its state is still "micro break"). Routine states are already
    /// refused by ShowBreakActivities.
    /// </summary>
    public static class AutoBreakGate
    {
        public static AutoBreakDecision WhenWarningEnds(AutoBreakConditions c)
        {
            if (c.BreakAlreadyRunning || c.ShutOffTime)
                return AutoBreakDecision.Nothing;
            if (c.SkipBreaksDuringFocus && (c.FocusSessionRunning || c.SuperFocusRunning))
                return AutoBreakDecision.Reschedule;
            if (c.ScheduleDisablesBreaks || c.AppDeactivated || c.BreaksOffToday)
                return AutoBreakDecision.Reschedule;
            if (c.OnCall && !c.ShowBreaksInCall)
                return AutoBreakDecision.Reschedule;
            return AutoBreakDecision.Start;
        }
    }
}
