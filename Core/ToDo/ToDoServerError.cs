using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// The message inside a rejected write's body, so a failure can say <i>why</i> instead of showing
    /// the generic toast.
    ///
    /// NestJS spells `message` three ways: a string, an array of strings, or — from the global
    /// `ValidationPipe` — an array of `ValidationError` objects whose `constraints` hold the readable
    /// reasons. Not every rejection is JSON at all: the API's auth layer answers `text/plain` ("Token
    /// missing or corrupted!"), and that body used to be discarded, so a save that failed 401 showed
    /// "Could not update task" with no reason and left no trace of why (Mac report 2026-09-16_15-14-36).
    ///
    /// The Mac takes the raw <c>Data</c>; this takes the decoded body text, because the Windows HTTP
    /// layer already has the string and this project has no business decoding bytes.
    /// </summary>
    public static class ToDoServerError
    {
        private const string MessageKey = "message";
        private const string ConstraintsKey = "constraints";
        private const string Separator = "; ";

        /// <summary>A plain-text body longer than this is a proxy's error page, not a reason worth showing.</summary>
        private const int MaxPlainTextLength = 200;

        /// <summary>The character an HTML error page starts with once trimmed.</summary>
        private const char MarkupOpener = '<';

        /// <summary>Null when the body carries no reason a user should be shown.</summary>
        public static string Message(string body)
        {
            if (body == null) return null;

            JObject json = AsJsonObject(body);
            if (json == null) return PlainText(body);

            JToken message = json[MessageKey];
            if (message == null) return null;

            if (message.Type == JTokenType.String) return NonEmpty(message.Value<string>());
            if (message.Type != JTokenType.Array) return null;

            IEnumerable<string> parts = ((JArray)message).SelectMany(Reasons);
            return NonEmpty(string.Join(Separator, parts));
        }

        /// <summary>One array entry's readable reasons: the string itself, or its validation constraints.</summary>
        private static IEnumerable<string> Reasons(JToken item)
        {
            if (item.Type == JTokenType.String) return new[] { item.Value<string>() };

            var error = item as JObject;
            if (error == null) return Enumerable.Empty<string>();

            var constraints = error[ConstraintsKey] as JObject;
            if (constraints == null) return Enumerable.Empty<string>();

            // Sorted so the same rejection always reads the same way, whatever order the server used.
            return constraints.Properties()
                .OrderBy(property => property.Name, System.StringComparer.Ordinal)
                .Where(property => property.Value.Type == JTokenType.String)
                .Select(property => property.Value.Value<string>());
        }

        private static JObject AsJsonObject(string body)
        {
            try
            {
                return JToken.Parse(body) as JObject;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        private static string PlainText(string body)
        {
            string trimmed = body.Trim();
            if (trimmed.Length > 0 && trimmed[0] == MarkupOpener) return null;
            if (trimmed.Length > MaxPlainTextLength) return null;
            return NonEmpty(trimmed);
        }

        private static string NonEmpty(string text)
        {
            if (text == null) return null;
            string trimmed = text.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
