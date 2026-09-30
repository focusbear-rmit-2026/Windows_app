using System;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// One "Woah, 'X' completed!" banner that ABSORBS rapid completions instead of stacking them.
    ///
    /// Ticking four tasks off in a row used to raise four banners, each one covering the list the user
    /// was ticking. The batch keeps a single banner alive instead: the first completion starts it with
    /// a randomly chosen phrase, every further completion inside the lifetime bumps the count and
    /// restarts the clock, and un-ticking takes one back.
    ///
    /// Immutable, because the Mac's is a struct and every rule here is written as "the batch you get
    /// back", never as a mutation of the one you had.
    /// </summary>
    public sealed class ToDoCompletionBatch : IEquatable<ToDoCompletionBatch>
    {
        /// <summary>How long a banner survives its last completion.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(6);

        public ToDoCompletionBatch(string phrase, string lastTitle, int count, DateTimeOffset expiresAt)
        {
            Phrase = phrase;
            LastTitle = lastTitle;
            Count = count;
            ExpiresAt = expiresAt;
        }

        /// <summary>The celebratory word the batch opened with, kept for the batch's whole life.</summary>
        public string Phrase { get; }

        /// <summary>The most recently completed task's title — what a single-task banner names.</summary>
        public string LastTitle { get; }

        public int Count { get; }

        public DateTimeOffset ExpiresAt { get; }

        /// <summary>Expiry is inclusive: a completion landing exactly on it starts a new batch.</summary>
        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiresAt;
        }

        /// <summary>
        /// Adds a completion to <paramref name="current"/>, starting a fresh batch when there is none
        /// or the one there has expired.
        ///
        /// A continuing batch keeps its ORIGINAL phrase and ignores the one offered: the banner is
        /// already on screen, and swapping its opening word mid-sentence reads as a different banner
        /// rather than the same one counting up.
        /// </summary>
        public static ToDoCompletionBatch Recording(string title, ToDoCompletionBatch current,
                                                    string phrase, DateTimeOffset now)
        {
            DateTimeOffset expiresAt = now + Lifetime;
            if (current == null || current.IsExpired(now))
            {
                return new ToDoCompletionBatch(phrase, title, 1, expiresAt);
            }
            return new ToDoCompletionBatch(current.Phrase, title, current.Count + 1, expiresAt);
        }

        /// <summary>
        /// Takes one completion back; null when the batch is down to its last one or already gone —
        /// both mean "hide the banner".
        ///
        /// The clock is deliberately NOT restarted. Un-ticking is a correction, not an achievement,
        /// and keeping the banner alive for it would let a user who is fixing a mis-click hold their
        /// own congratulations on screen.
        /// </summary>
        public ToDoCompletionBatch Uncompleting(DateTimeOffset now)
        {
            if (IsExpired(now) || Count <= 1) return null;
            return new ToDoCompletionBatch(Phrase, LastTitle, Count - 1, ExpiresAt);
        }

        public bool Equals(ToDoCompletionBatch other)
        {
            return other != null && Phrase == other.Phrase && LastTitle == other.LastTitle
                && Count == other.Count && ExpiresAt == other.ExpiresAt;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ToDoCompletionBatch);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Phrase?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (LastTitle?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Count;
                return (hash * 397) ^ ExpiresAt.GetHashCode();
            }
        }
    }
}
