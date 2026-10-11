using System;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// The registry's local "till" dates were read back as Kind=Utc: the wall clock was right, the label wrong
    /// (the install date went out with a "Z"). StoredWallClock reads the same stored numbers as Local.
    /// </summary>
    [TestClass]
    public class StoredWallClockTests
    {
        // What the old Global.ConvertToUnixTime wrote: (local wall clock - 1970-01-01 00:00 UTC).TotalSeconds.
        private static long OldStored(DateTime local) =>
            (long)(local - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        [TestMethod]
        public void AnExistingStoredValue_ReadsAsTheSameLocalWallClock()
        {
            var shutOffAbortedTill = new DateTime(2026, 9, 27, 23, 59, 0, DateTimeKind.Local);
            long stored = OldStored(shutOffAbortedTill);

            DateTime read = StoredWallClock.FromStored(stored);
            Assert.AreEqual(DateTimeKind.Local, read.Kind);
            Assert.AreEqual(shutOffAbortedTill.Ticks, read.Ticks, "same wall clock as before");
            Assert.AreEqual(stored, StoredWallClock.ToStored(read), "written back unchanged");
        }

        [TestMethod]
        public void AValueTheOldCodeReadAsUtc_IsWrittenBackUnchanged()
        {
            // Old read: Kind=Utc holding the local wall clock. Re-saving it must not convert it.
            long stored = OldStored(new DateTime(2026, 4, 5, 2, 30, 0));
            DateTime oldRead = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(stored);
            Assert.AreEqual(stored, StoredWallClock.ToStored(oldRead));
        }

        [TestMethod]
        public void WallClockTimesAcrossADstChangeRoundTripExactly()
        {
            // Sydney: 2026-04-05 02:00-03:00 happens twice (DST ends); 2026-10-04 02:00-03:00 never happens (DST
            // starts). A wall-clock store keeps each exactly as written, whatever the machine's zone.
            foreach (var wall in new[]
            {
                new DateTime(2026, 4, 5, 2, 30, 0),
                new DateTime(2026, 10, 4, 2, 30, 0),
                new DateTime(2026, 3, 8, 2, 30, 0),   // US spring forward
                new DateTime(2026, 11, 1, 1, 30, 0),  // US fall back
            })
            {
                DateTime back = StoredWallClock.FromStored(StoredWallClock.ToStored(wall));
                Assert.AreEqual(wall.Ticks, back.Ticks, wall.ToString("s"));
                Assert.AreEqual(DateTimeKind.Local, back.Kind);
            }
        }

        [TestMethod]
        public void AnHourAfterIsStillAnHourAfter_ByTheWallClock()
        {
            var before = new DateTime(2026, 10, 4, 1, 30, 0, DateTimeKind.Local);
            DateTime a = StoredWallClock.FromStored(StoredWallClock.ToStored(before));
            DateTime b = StoredWallClock.FromStored(StoredWallClock.ToStored(before.AddHours(1)));
            Assert.IsTrue(b > a);
            Assert.AreEqual(TimeSpan.FromHours(1), b - a);
        }

        [TestMethod]
        public void TheRegistryString()
        {
            long stored = OldStored(new DateTime(2026, 9, 27, 8, 0, 0));
            Assert.AreEqual(new DateTime(2026, 9, 27, 8, 0, 0), StoredWallClock.FromStored(stored.ToString()));
            Assert.AreEqual(new DateTime(2026, 9, 27, 8, 0, 0), StoredWallClock.FromStored(" " + stored + " "));
            Assert.IsNull(StoredWallClock.FromStored("abc"));
            Assert.IsNull(StoredWallClock.FromStored((string)null));
            Assert.IsNull(StoredWallClock.FromStored(long.MaxValue.ToString()), "out of range is not a date");
        }
    }
}
