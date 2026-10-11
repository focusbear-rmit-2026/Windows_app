using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// How the tasks nested under a time-boxed block (a meeting or a deep-work sprint) affect the
    /// "🕒 Nh free" figure in the Day Plan's tasks panel (report 2026-09-10_07-53-47).
    /// </summary>
    public static class DayPlanFreeTime
    {
        /// <summary>
        /// Minutes a block's nested tasks add to the day *beyond* the block's own duration.
        ///
        /// Tasks dropped onto a block are meant to be worked inside it, so that time is already carved
        /// out of the day — counting them again on top makes the day look fuller than it is. Only the
        /// overflow costs anything: a 10-minute block carrying 20 minutes of tasks costs 10 extra
        /// minutes, not 30. Tasks that fit inside the block cost nothing, and an under-filled block
        /// never hands time back.
        /// </summary>
        public static int ExcessMinutes(int taskMinutes, int blockMinutes)
        {
            return Math.Max(0, taskMinutes - Math.Max(0, blockMinutes));
        }

        /// <summary>One time-boxed block's own length and the total length of the tasks nested under it.</summary>
        public struct BlockLoad : IEquatable<BlockLoad>
        {
            public BlockLoad(int blockMinutes, int taskMinutes)
            {
                BlockMinutes = blockMinutes;
                TaskMinutes = taskMinutes;
            }

            public int BlockMinutes { get; }
            public int TaskMinutes { get; }

            public bool Equals(BlockLoad other)
            {
                return BlockMinutes == other.BlockMinutes && TaskMinutes == other.TaskMinutes;
            }

            public override bool Equals(object obj)
            {
                return obj is BlockLoad && Equals((BlockLoad)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return (BlockMinutes * 397) ^ TaskMinutes; }
            }
        }

        /// <summary>
        /// Free minutes left once every time-boxed block's overflow is deducted. Clamped at zero — an
        /// over-committed day reports no free time rather than a negative one.
        /// </summary>
        public static int AdjustedFreeMinutes(int rawFreeMinutes, IEnumerable<BlockLoad> blocks)
        {
            int overflow = (blocks ?? Enumerable.Empty<BlockLoad>())
                .Sum(b => ExcessMinutes(b.TaskMinutes, b.BlockMinutes));
            return Math.Max(0, rawFreeMinutes - overflow);
        }
    }
}
