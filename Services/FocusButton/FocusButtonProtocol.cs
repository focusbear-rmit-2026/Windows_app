using System;
using System.Collections.Generic;
using System.Text;

namespace Focus_Bear.Services.FocusButton
{
    /// <summary>
    /// Focus Button serial-protocol constants and helpers. The authoritative spec is
    /// protocol.md (v1.8.1) in the Focus-Bear/Focus-Button repo.
    /// </summary>
    public static class FocusButtonProtocol
    {
        /// <summary>
        /// secs value marking a completion-based habit: the device counts up, never
        /// auto-completes, and the habit ends only via cancel. 0 already means
        /// "remove", so the firmware uses UINT32_MAX as the sentinel (fw 1.6.0).
        /// </summary>
        public const long HABIT_DURATION_NO_TIMER = 4294967295;

        /// <summary>Device text fields (habit name, focus goal, notify title) are capped at 63 chars.</summary>
        public const int MAX_DEVICE_TEXT_LENGTH = 63;

        /// <summary>
        /// After SET|HABITS the firmware defers its NVS flush until 500ms of serial
        /// silence, so the CRC readback must wait at least 1s or it races the flush.
        /// </summary>
        public const int HABITS_CRC_SETTLE_MS = 1500;

        /// <summary>SET|HABITS attempts per sync (initial send + CRC-mismatch resends).</summary>
        public const int HABITS_SEND_MAX_ATTEMPTS = 3;

        // Firmware versions that introduced each adopted feature.
        public static readonly Version FW_NOTIFY = new Version(1, 5, 0);          // SET|NOTIFY
        public static readonly Version FW_HABITLIST = new Version(1, 6, 0);       // + SET|BEEP, SET|NOTIFYDISMISS, no-timer sentinel, GET|RESETREASON
        public static readonly Version FW_HABITSCRC = new Version(1, 7, 0);       // GET|HABITSCRC
        public static readonly Version FW_FOCUS_GOAL = new Version(1, 8, 0);      // SET|START|secs|goal, SET|GOAL

        /// <summary>One habit entry as sent to the device in a SET|HABITS triple.</summary>
        public struct HabitWireEntry
        {
            /// <summary>The id exactly as sent in the triple (may keep its "h" prefix).</summary>
            public string WireId;
            /// <summary>The decimal id as the device stores it — what GET|HABITSCRC hashes.</summary>
            public long NumericId;
            public string Name;
            public long Secs;

            public HabitWireEntry(string wireId, long numericId, string name, long secs)
            {
                WireId = wireId;
                NumericId = numericId;
                Name = name;
                Secs = secs;
            }
        }

        /// <summary>
        /// CRC32, zlib polynomial (reflected 0xEDB88320). Pass a previous result as
        /// <paramref name="seed"/> to continue, like zlib.crc32(data, crc).
        /// </summary>
        public static uint Crc32(byte[] data, uint seed = 0)
        {
            uint crc = ~seed;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }
            return ~crc;
        }

        /// <summary>
        /// CRC32 over "id,name,secs\n" (UTF-8, decimal fields) per habit in send order —
        /// must produce the same value the device reports via GET|HABITSCRC.
        /// </summary>
        public static uint HabitsCrc(IEnumerable<HabitWireEntry> entries)
        {
            uint crc = 0;
            foreach (HabitWireEntry entry in entries)
                crc = Crc32(Encoding.UTF8.GetBytes(entry.NumericId + "," + entry.Name + "," + entry.Secs + "\n"), crc);
            return crc;
        }

        /// <summary>
        /// EVENT|STARTHB|&lt;total_s&gt;|&lt;habit_id&gt;|&lt;HabitName&gt; (protocol.md). The name is everything after the id, so
        /// one containing '|' stays whole; an older 4-field line carries a name and no id. The caller used to read
        /// the name from parts[4] after checking only for 4 parts, which threw on a 4-field line.
        /// </summary>
        public static bool TryParseStartHabit(string[] parts, out string habitId, out string habitName)
        {
            habitId = "";
            habitName = "";
            if (parts == null || parts.Length < 4)
                return false;
            if (parts.Length == 4)
            {
                habitName = parts[3].Trim();
                return true;
            }
            habitId = parts[3].Trim();
            habitName = string.Join("|", parts, 4, parts.Length - 4).Trim();
            return true;
        }

        /// <summary>
        /// FIRMWAREVER|&lt;hw&gt;-&lt;version&gt;[-suffix], e.g. M50-1.4.2 or C30-1.7.0-rc2. False (version null) when there is no
        /// readable version; the hardware code is still returned when present.
        /// </summary>
        public static bool TryParseFirmwareVersion(string field, out string hardwareCode, out Version version)
        {
            hardwareCode = "";
            version = null;
            if (string.IsNullOrWhiteSpace(field))
                return false;
            string[] pieces = field.Trim().Split('-');
            hardwareCode = pieces[0];
            if (pieces.Length < 2)
                return false;
            version = FocusBear.Core.Updates.VersionCompare.Parse(pieces[1]);
            return version != null;
        }

        /// <summary>
        /// Makes text safe for a pipe-delimited device line: strips pipes, newlines,
        /// control chars and emoji (the device font has no glyphs for them), and clamps
        /// to <see cref="MAX_DEVICE_TEXT_LENGTH"/>.
        /// </summary>
        public static string SanitizeDeviceText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '|' || char.IsControl(c))
                    continue;
                if (char.IsSurrogate(c))
                    continue; // emoji and other non-BMP glyphs
                if (c >= '\u2190' && c <= '\u2BFF')
                    continue; // arrows, misc symbols, dingbats (BMP emoji)
                if (c == '\u200D' || c == '\uFE0E' || c == '\uFE0F' || c == '\u20E3')
                    continue; // emoji joiners / variation selectors / keycap
                sb.Append(c);
            }

            string sanitized = sb.ToString().Trim();
            if (sanitized.Length > MAX_DEVICE_TEXT_LENGTH)
                sanitized = sanitized.Substring(0, MAX_DEVICE_TEXT_LENGTH).TrimEnd();
            return sanitized;
        }
    }
}
