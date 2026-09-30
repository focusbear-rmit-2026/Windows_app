using System;
using System.Threading.Tasks;

namespace FocusBear.Core.FocusMusic
{
    /// <summary>
    /// A start-up that is shared while it runs and after it succeeds, but tried again after it fails. The YouTube
    /// player kept its first WebView2 start for good: if that one failed (the runtime updating, a locked profile
    /// folder), every later Play awaited the same failed task and the player never worked until the app restarted.
    /// </summary>
    public sealed class RetryableStart
    {
        private readonly Func<Task> _start;
        private Task _current;

        public RetryableStart(Func<Task> start)
        {
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public Task EnsureStarted()
        {
            Task current = _current;
            if (current == null || current.IsFaulted || current.IsCanceled)
            {
                try { current = _start() ?? Task.CompletedTask; }
                catch (Exception ex) { current = Task.FromException(ex); }
                _current = current;
            }
            return current;
        }
    }
}
