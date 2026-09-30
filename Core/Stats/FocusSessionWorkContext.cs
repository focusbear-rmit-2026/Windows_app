using System;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The two things a relevance check needs, kept apart.
    ///
    /// They are separate values and the endpoint takes separate fields for them (<c>intention</c> and
    /// <c>currentTaskInToDoPlayer</c>). Collapsing them is what made the AI judge the user against a
    /// goal they had already moved on from.
    /// </summary>
    public sealed class FocusSessionWorkContext : IEquatable<FocusSessionWorkContext>
    {
        private FocusSessionWorkContext(string intention, string currentTask)
        {
            Intention = intention;
            CurrentTask = currentTask;
        }

        /// <summary>The session's stated goal. Locked in a super-strict session.</summary>
        public string Intention { get; }

        /// <summary>What the user is working on right now, or null when the queue has no active task.</summary>
        public string CurrentTask { get; }

        public static FocusSessionWorkContext Resolve(string activeTaskTitle, string intention)
        {
            string task = (activeTaskTitle ?? string.Empty).Trim();
            // Both values are sent onward to the classifier, so they are trimmed here rather than at
            // each call site; a blank title is no task at all, not an empty one.
            return new FocusSessionWorkContext(
                (intention ?? string.Empty).Trim(),
                task.Length == 0 ? null : task);
        }

        public bool Equals(FocusSessionWorkContext other)
        {
            return other != null && Intention == other.Intention && CurrentTask == other.CurrentTask;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FocusSessionWorkContext);
        }

        public override int GetHashCode()
        {
            return (Intention ?? string.Empty).GetHashCode() ^ (CurrentTask ?? string.Empty).GetHashCode();
        }
    }

    /// <summary>
    /// Whether a caller outside the session screen may change each value.
    ///
    /// Changing the current task is always allowed — it is simply what the user is doing. The
    /// intention is the session's stated goal, and a super-strict session forbids changing it, so
    /// nothing outside the goal editor may write it. The regression this pins: a caller that "set the
    /// current task" by writing the intention edited the goal, bypassing the super-strict lock.
    /// </summary>
    public static class FocusSessionIntentionLock
    {
        public static bool MayChangeIntention(bool isSuperStrict)
        {
            return !isSuperStrict;
        }

        /// <summary>The buddy and the block popup set the task, never the goal, at every strictness.</summary>
        public static bool MayChangeCurrentTask(bool isSuperStrict)
        {
            return true;
        }
    }
}
