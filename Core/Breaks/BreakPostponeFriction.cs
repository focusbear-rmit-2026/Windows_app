using System;

namespace FocusBear.Core.Breaks
{
    /// <summary>
    /// Friction on postponing a break from the break warning, as the Mac has it: the postpone is a press-and-hold
    /// that grows 5 s, 10 s, 15 s with each consecutive postpone (taking a break resets it, #1590), and every
    /// second postpone from the warning asks whether to turn breaks off (deactivateBreaksVc).
    /// </summary>
    public sealed class BreakPostponeFriction
    {
        public static BreakPostponeFriction Shared { get; } = new BreakPostponeFriction();

        private int _consecutivePostpones;
        private int _postponesSincePrompt;

        /// <summary>How long the postpone must be held for the next warning.</summary>
        public int HoldSeconds => Math.Min(15, 5 * (_consecutivePostpones + 1));

        /// <summary>Records a postpone from the warning. True when it is time to ask about turning breaks off.</summary>
        public bool PostponedFromWarning()
        {
            _consecutivePostpones++;
            _postponesSincePrompt++;
            if (_postponesSincePrompt >= 2)
            {
                _postponesSincePrompt = 0;
                return true;
            }
            return false;
        }

        /// <summary>A break was taken: the next postpone is back to the shortest hold.</summary>
        public void BreakTaken()
        {
            _consecutivePostpones = 0;
        }

        /// <summary>"Turn them off for today", set at <paramref name="setAt"/>, still holds at <paramref name="now"/>.</summary>
        public static bool BreaksOffToday(DateTime? setAt, DateTime now)
        {
            return setAt.HasValue && setAt.Value.Date == now.Date;
        }
    }
}
