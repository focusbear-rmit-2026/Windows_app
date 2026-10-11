using System;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// How long the user has been on the session's current task. Port of the Mac's
    /// <c>FocusSessionTaskTimer</c>.
    ///
    /// A class rather than the Mac's struct: C# copies a struct on every assignment, so a mutable
    /// one silently loses the mutations a caller makes through a copy. The behaviour is otherwise
    /// identical.
    /// </summary>
    public sealed class FocusSessionTaskTimer
    {
        public string TaskId { get; private set; }

        /// <summary>Seconds banked by earlier runs, i.e. everything before the current pause/resume.</summary>
        public int AccumulatedSeconds { get; private set; }

        public DateTime? RunningSince { get; private set; }

        public bool IsRunning
        {
            get { return RunningSince.HasValue; }
        }

        public int ElapsedSeconds(DateTime now)
        {
            if (!RunningSince.HasValue)
            {
                return AccumulatedSeconds;
            }

            return AccumulatedSeconds + Math.Max(0, (int)(now - RunningSince.Value).TotalSeconds);
        }

        /// <summary>
        /// Points the timer at a task. Switching to a different task starts its count from zero;
        /// being told the SAME task again is a no-op, so a refresh does not reset the clock.
        /// </summary>
        public void Activate(string newTaskId, DateTime now)
        {
            if (newTaskId == TaskId)
            {
                if (newTaskId != null && !RunningSince.HasValue)
                {
                    RunningSince = now;
                }

                return;
            }

            TaskId = newTaskId;
            AccumulatedSeconds = 0;
            RunningSince = newTaskId == null ? (DateTime?)null : now;
        }

        public void Pause(DateTime now)
        {
            if (!RunningSince.HasValue)
            {
                return;
            }

            AccumulatedSeconds += Math.Max(0, (int)(now - RunningSince.Value).TotalSeconds);
            RunningSince = null;
        }

        public void Resume(DateTime now)
        {
            if (TaskId == null || RunningSince.HasValue)
            {
                return;
            }

            RunningSince = now;
        }
    }
}
