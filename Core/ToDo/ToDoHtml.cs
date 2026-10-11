using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Task details can carry HTML from the web editor; a native screen shows plain text. This is a
    /// display/authoring conversion, not a sanitiser — it exists so an edit started on the web does
    /// not show the user their own notes as markup.
    /// </summary>
    public static class ToDoHtml
    {
        /// <summary>The one character that says the value might be markup rather than text a user typed.</summary>
        private const char TagOpener = '<';

        private static readonly Regex LineBreak = new Regex("<br\\s*/?>", RegexOptions.IgnoreCase);
        private static readonly Regex ParagraphEnd = new Regex("</p>", RegexOptions.IgnoreCase);
        private static readonly Regex AnyTag = new Regex("<[^>]+>");

        /// <summary>The entities the web editor emits, and nothing more — this is not a full decoder.</summary>
        private static readonly IList<KeyValuePair<string, string>> Entities = new[]
        {
            new KeyValuePair<string, string>("&nbsp;", " "),
            new KeyValuePair<string, string>("&amp;", "&"),
            new KeyValuePair<string, string>("&lt;", "<"),
            new KeyValuePair<string, string>("&gt;", ">"),
            new KeyValuePair<string, string>("&quot;", "\""),
            new KeyValuePair<string, string>("&#39;", "'"),
        };

        public static string PlainText(string html)
        {
            if (html == null) return string.Empty;
            if (html.IndexOf(TagOpener) < 0) return DecodeEntities(html);

            string stripped = LineBreak.Replace(html, "\n");
            stripped = ParagraphEnd.Replace(stripped, "\n");
            stripped = AnyTag.Replace(stripped, string.Empty);
            return DecodeEntities(stripped).Trim();
        }

        private static string DecodeEntities(string text)
        {
            string decoded = text;
            foreach (KeyValuePair<string, string> entity in Entities)
            {
                decoded = decoded.Replace(entity.Key, entity.Value);
            }
            return decoded;
        }
    }
}
