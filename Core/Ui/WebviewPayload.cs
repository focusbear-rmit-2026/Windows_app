using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Ui
{
    /// <summary>
    /// The object handed to a dashboard webview (access token, theme, font, flags...) and the script that waits
    /// for the page's function and calls it. These were spliced with string.Format, so a custom goal with a
    /// quote or a newline broke the onboarding call, and the requestId the page echoes back went in unescaped.
    /// </summary>
    public sealed class WebviewPayload
    {
        private static readonly Regex Identifier = new Regex("^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant);
        private readonly JObject _payload = new JObject();

        /// <summary>Adds a value (string, number, bool, JToken...); a null string becomes "".</summary>
        public WebviewPayload Add(string key, object value)
        {
            _payload[key] = value == null ? (JToken)"" : value as JToken ?? JToken.FromObject(value);
            return this;
        }

        /// <summary>Adds JSON the app already holds as text (e.g. the raw task list); an empty array when it is not valid.</summary>
        public WebviewPayload AddJson(string key, string json)
        {
            JToken token = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { token = JToken.Parse(json); }
                catch (JsonException) { token = null; }
            }
            _payload[key] = token ?? new JArray();
            return this;
        }

        /// <summary>Echoes the page's requestId back on a reply (Path B); nothing on the initial load.</summary>
        public WebviewPayload WithRequestId(string requestId)
        {
            if (!string.IsNullOrEmpty(requestId))
                _payload["requestId"] = requestId;
            return this;
        }

        public string Json => _payload.ToString(Formatting.None);

        /// <summary>The script that waits for window.<paramref name="function"/> and calls it with the payload once.</summary>
        public string WaitAndCall(string function)
        {
            if (function == null || !Identifier.IsMatch(function))
                throw new ArgumentException("Not a function name: " + function, nameof(function));
            return "const waitForAppLoaded = setInterval(() => { if (window." + function + ") { " + function + "(" + Json
                   + "); clearInterval(waitForAppLoaded); } }, 100);";
        }
    }
}
