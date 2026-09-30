using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Analytics
{
    /// <summary>
    /// Event properties as the app's many callers hand them over: a fragment of JSON members
    /// (<c>"type":"simple-focus","focusDuration":"25"</c>) without the braces. The payloads around them used to be
    /// spliced together as text, so one caller that pasted a typed answer in with a quote, or a user name or
    /// occupation with one, made the whole event unreadable and it was dropped.
    /// </summary>
    public static class EventPayload
    {
        /// <summary>
        /// The fragment as an object; an empty one is an empty object, and one that does not parse is null — the
        /// caller then sends the event without its extra properties rather than not at all.
        /// </summary>
        public static JObject Properties(string fragment)
        {
            if (string.IsNullOrWhiteSpace(fragment)) return new JObject();
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader("{" + fragment + "}")) { DateParseHandling = DateParseHandling.None })
                {
                    var parsed = JToken.ReadFrom(reader) as JObject;
                    if (reader.Read()) return null;   // trailing text after the object
                    return parsed;
                }
            }
            catch (JsonException) { return null; }
        }

        /// <summary><c>"key":value</c> with both sides escaped — for callers that build a fragment from typed text.</summary>
        public static string Member(string key, object value) =>
            JsonConvert.SerializeObject(key ?? "") + ":" + JsonConvert.SerializeObject(value);

        /// <summary>Several <see cref="Member"/>s, comma-separated.</summary>
        public static string Members(params KeyValuePair<string, object>[] members)
        {
            var parts = new List<string>();
            foreach (var member in members ?? new KeyValuePair<string, object>[0]) parts.Add(Member(member.Key, member.Value));
            return string.Join(",", parts);
        }

        /// <summary>Copies <paramref name="extra"/> into <paramref name="target"/>; the target's own keys win.</summary>
        public static JObject Merge(JObject target, JObject extra)
        {
            if (extra == null) return target;
            foreach (var property in extra.Properties())
                if (target[property.Name] == null) target[property.Name] = property.Value.DeepClone();
            return target;
        }
    }
}
