using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The getData(...) call that feeds the routine finished-screen graphs (graph/GraphDays*.html and
    /// graph/GraphTime*.html). Built as JSON so a habit or question name is one string, whatever it contains;
    /// it was spliced in with string.Format, so a quote or backslash broke the script and the graph stayed blank.
    /// </summary>
    public static class HabitGraphScript
    {
        /// <summary>The per-day graph: label, axis labels, the day values (a JSON array) and the y maximum.</summary>
        public static string Days(string label, string labelX, string labelY, string dataArrayJson, int max)
        {
            return Script(Payload(label, labelX, labelY, dataArrayJson, max));
        }

        /// <summary>The through-the-day graph, which also carries the time of each point.</summary>
        public static string Time(string label, string labelX, string labelY, string dataArrayJson, int max, string timeValuesJson)
        {
            JObject payload = Payload(label, labelX, labelY, dataArrayJson, max);
            payload["TimeValues"] = ArrayOrEmpty(timeValuesJson);
            return Script(payload);
        }

        private static JObject Payload(string label, string labelX, string labelY, string dataArrayJson, int max)
        {
            return new JObject
            {
                ["label"] = label ?? "",
                ["labelX"] = labelX ?? "",
                ["labelY"] = labelY ?? "",
                ["data"] = ArrayOrEmpty(dataArrayJson),
                ["max"] = max,
            };
        }

        private static string Script(JObject payload)
        {
            return "getData(" + payload.ToString(Formatting.None) + ");";
        }

        /// <summary>The array the caller assembled, or an empty one when it is not valid JSON (an empty graph beats a script error).</summary>
        private static JArray ArrayOrEmpty(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new JArray();
            try
            {
                return JToken.Parse(json) as JArray ?? new JArray();
            }
            catch (JsonException)
            {
                return new JArray();
            }
        }
    }
}
