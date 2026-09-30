using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The AI check's <c>allowed_probability</c>. It was read with <c>double.Parse(token.ToString())</c>: the text of
    /// a JSON number is invariant ("0.8"), so under a culture with a decimal comma it threw or read 8, and an
    /// exception there made the check answer 0.70 ("probably a timeout"). Read invariantly, finite only.
    /// </summary>
    public static class AiProbability
    {
        /// <summary>The probability, or null when the field is missing, null, or not a finite number.</summary>
        public static double? Read(JToken token)
        {
            if (token == null) return null;
            double value;
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    value = token.Value<double>();
                    break;
                case JTokenType.String:
                    if (!double.TryParse((string)token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
                    break;
                default:
                    return null;
            }
            return double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
        }
    }
}
