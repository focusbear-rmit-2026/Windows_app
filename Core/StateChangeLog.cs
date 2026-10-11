namespace FocusBear.Core
{
    /// <summary>
    /// A log line that repeats on every tick of a poll is written once per stay in a state. The 5 s background
    /// tick wrote "EVENING_HABITS STATE - Return from timer" every 5 s all evening (about 720 lines an hour);
    /// now it is written when the state is entered, and again only after the state has changed and come back.
    /// </summary>
    public sealed class StateChangeLog
    {
        private string _state;
        private string _logged;

        /// <summary>Called on every tick with the current state; a new state lets its lines be logged again.</summary>
        public void Observe(string state)
        {
            if (state == _state) return;
            _state = state;
            _logged = null;
        }

        /// <summary>True the first time <paramref name="line"/> comes up in the current state.</summary>
        public bool ShouldLog(string line)
        {
            if (line == _logged) return false;
            _logged = line;
            return true;
        }
    }
}
