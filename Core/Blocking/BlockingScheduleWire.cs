using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Reading a <c>blocking_schedule</c> off the wire: the Mac's
    /// <c>customBlockingSchedule.init(from:)</c> plus the <c>FailableDecodable</c> wrapper its list
    /// decode is built on.
    /// </summary>
    public static class BlockingScheduleWire
    {
        public const string BlockLevelGentle = "gentle";
        public const string BlockLevelStrict = "strict";
        public const string BlockLevelSuperStrict = "super-strict";

        public const string DefaultName = "Untitled";
        public const string DefaultStartTime = "09:00";
        public const string DefaultEndTime = "18:00";

        private const string KeyId = "id";
        private const string KeyName = "name";
        private const string KeyStartTime = "start_time";
        private const string KeyEndTime = "end_time";
        private const string KeyDaysOfWeek = "days_of_week";
        private const string KeyFocusModeId = "focus_mode_id";
        private const string KeyBlockLevel = "block_level";
        private const string KeyIsAiBlockingEnabled = "is_ai_blocking_enabled";
        private const string KeyEmoji = "emoji";
        private const string KeyMetadata = "metadata";
        private const string KeyMicroBreaksEnabled = "is_micro_breaks_enabled";

        /// <summary>The time formats the backend has sent, in the order the Mac tries them.</summary>
        private static readonly string[] TimeFormats = { "HH:mm:ss", "HH:mm" };

        /// <summary>
        /// Decodes a LIST of schedules, keeping the ones that parse.
        ///
        /// This is the whole point of the Mac's <c>FailableDecodable</c>: a single malformed element
        /// used to throw out of the array decode and take every other schedule with it, so a user
        /// with one bad row had no blocking at all. One bad element now costs one schedule.
        /// </summary>
        public static IReadOnlyList<CustomBlockingSchedule> ParseList(string json, DateTime now)
        {
            var schedules = new List<CustomBlockingSchedule>();
            JToken root = JToken.Parse(json);
            var array = root as JArray;
            if (array == null) return schedules;

            foreach (JToken element in array)
            {
                CustomBlockingSchedule schedule;
                if (TryParse(element, now, out schedule)) schedules.Add(schedule);
            }
            return schedules;
        }

        /// <summary>
        /// Decodes one schedule. Returns false for anything Swift's <c>Decodable</c> would have
        /// thrown on: a non-object element, or a present field of the wrong JSON type — Swift's
        /// <c>decodeIfPresent</c> substitutes a default only for an ABSENT or null value, never for a
        /// value of the wrong type.
        /// </summary>
        public static bool TryParse(JToken element, DateTime now, out CustomBlockingSchedule schedule)
        {
            schedule = null;
            var json = element as JObject;
            if (json == null) return false;

            string id, name, focusModeId, blockLevelString, startTimeString, endTimeString, emoji;
            bool isAiBlockingEnabled;
            if (!TryReadString(json, KeyId, Guid.NewGuid().ToString(), out id)) return false;
            if (!TryReadString(json, KeyName, DefaultName, out name)) return false;
            if (!TryReadString(json, KeyFocusModeId, string.Empty, out focusModeId)) return false;
            if (!TryReadString(json, KeyBlockLevel, BlockLevelGentle, out blockLevelString)) return false;
            if (!TryReadString(json, KeyStartTime, DefaultStartTime, out startTimeString)) return false;
            if (!TryReadString(json, KeyEndTime, DefaultEndTime, out endTimeString)) return false;
            if (!TryReadString(json, KeyEmoji, null, out emoji)) return false;
            if (!TryReadBool(json, KeyIsAiBlockingEnabled, false, out isAiBlockingEnabled)) return false;

            bool isMicroBreaksEnabled;
            if (!TryReadMicroBreaks(json, out isMicroBreaksEnabled)) return false;

            DateTime startTime = ParseTime(startTimeString, now) ?? now;
            DateTime endTime = ParseTime(endTimeString, now) ?? now;
            // A window that ends before it starts runs into the next day.
            if (startTime > endTime) endTime = endTime.AddDays(1);

            schedule = new CustomBlockingSchedule
            {
                Id = id,
                Name = name,
                FocusModeId = focusModeId,
                BlockLevel = ParseBlockLevel(blockLevelString),
                IsAIBlockingEnabled = isAiBlockingEnabled,
                IsMicroBreaksEnabled = isMicroBreaksEnabled,
                Emoji = emoji,
                StartTimeString = startTimeString,
                EndTimeString = endTimeString,
                StartTime = startTime,
                EndTime = endTime,
                DaysOfWeek = BlockingScheduleDaysOfWeek.ToCalendarWeekdays(
                    BlockingScheduleDaysOfWeek.ParseWire(json[KeyDaysOfWeek])),
            };
            return true;
        }

        /// <summary>
        /// Anything that is not a known level is gentle — including an absent one. Strictness escalates
        /// what the user cannot do, so an unrecognised value from a newer backend must not be guessed
        /// upward.
        /// </summary>
        public static FocusModeStrictness ParseBlockLevel(string blockLevel)
        {
            switch (blockLevel)
            {
                case BlockLevelStrict: return FocusModeStrictness.Strict;
                case BlockLevelSuperStrict: return FocusModeStrictness.SuperStrict;
                default: return FocusModeStrictness.Normal;
            }
        }

        public static string BlockLevelWireValue(FocusModeStrictness blockLevel)
        {
            switch (blockLevel)
            {
                case FocusModeStrictness.Strict: return BlockLevelStrict;
                case FocusModeStrictness.SuperStrict: return BlockLevelSuperStrict;
                default: return BlockLevelGentle;
            }
        }

        /// <summary>
        /// The Mac's <c>parseTime</c>: a time of day, dropped onto <paramref name="now"/>'s date.
        /// The stored value is a clock time; the date part only exists so the comparisons have
        /// something to work with, and is re-based on every day change.
        /// </summary>
        public static DateTime? ParseTime(string value, DateTime now)
        {
            if (value == null) return null;
            foreach (string format in TimeFormats)
            {
                DateTime parsed;
                if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                                           DateTimeStyles.None, out parsed))
                {
                    return now.Date + parsed.TimeOfDay;
                }
            }
            return null;
        }

        private static bool TryReadString(JObject json, string key, string fallback, out string value)
        {
            value = fallback;
            JToken token = json[key];
            if (token == null || token.Type == JTokenType.Null) return true;
            if (token.Type != JTokenType.String) return false;
            value = token.Value<string>();
            return true;
        }

        private static bool TryReadBool(JObject json, string key, bool fallback, out bool value)
        {
            value = fallback;
            JToken token = json[key];
            if (token == null || token.Type == JTokenType.Null) return true;
            if (token.Type != JTokenType.Boolean) return false;
            value = token.Value<bool>();
            return true;
        }

        /// <summary>
        /// Micro-breaks ride inside <c>metadata</c>, which the Mac decodes as a dictionary of BOOLS —
        /// so a metadata blob carrying any non-boolean value fails the whole schedule, not just this
        /// flag.
        /// </summary>
        private static bool TryReadMicroBreaks(JObject json, out bool isMicroBreaksEnabled)
        {
            isMicroBreaksEnabled = false;
            JToken token = json[KeyMetadata];
            if (token == null || token.Type == JTokenType.Null) return true;

            var metadata = token as JObject;
            if (metadata == null) return false;
            foreach (KeyValuePair<string, JToken> pair in metadata)
            {
                if (pair.Value.Type != JTokenType.Boolean) return false;
            }

            JToken flag = metadata[KeyMicroBreaksEnabled];
            if (flag != null) isMicroBreaksEnabled = flag.Value<bool>();
            return true;
        }
    }
}
