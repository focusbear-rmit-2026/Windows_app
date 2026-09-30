using System.Linq;

namespace FocusBear.Core.Postpone
{
    public enum PostponeMinutesInput
    {
        Ok,
        /// <summary>More than <see cref="PostponeMinutes.Max"/>; the minutes are set to the maximum.</summary>
        Capped,
        Invalid,
    }

    /// <summary>
    /// How long a routine or break postpone may run. The box takes typed minutes (#1036), and past about 35,791
    /// minutes the DispatcherTimer refuses the interval - the postpone silently did nothing. A day is the cap:
    /// past that the routine's own window is over anyway (the Mac offers at most 90 minutes).
    /// </summary>
    public static class PostponeMinutes
    {
        public const int Max = 1440;

        /// <summary>1 to <see cref="Max"/>.</summary>
        public static int Clamp(int minutes)
        {
            if (minutes < 1)
                return 1;
            return minutes > Max ? Max : minutes;
        }

        /// <summary>Reads the typed minutes: at least 1, capped at <see cref="Max"/> (reported as Capped).</summary>
        public static PostponeMinutesInput Parse(string text, out int minutes)
        {
            minutes = 0;
            string t = text?.Trim() ?? "";
            if (t.Length == 0 || !t.All(c => c >= '0' && c <= '9'))
                return PostponeMinutesInput.Invalid;

            string digits = t.TrimStart('0');
            if (digits.Length > 9 || (digits.Length > 0 && int.Parse(digits) > Max))
            {
                minutes = Max;
                return PostponeMinutesInput.Capped;
            }
            minutes = Clamp(digits.Length == 0 ? 0 : int.Parse(digits));
            return PostponeMinutesInput.Ok;
        }
    }
}
