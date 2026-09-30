using System;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// One row of focusAIDecisions.CSV:
    /// Timestamp,Url,Domain,TabTitle,AIRelevanceScore,AdditionalUserJustification,currentTaskName,focusIntention,AIReason.
    /// The rows were built by string interpolation, so a tab title, intention or AI reason with a quote or a line
    /// break in it (the AI's reasons often quote the page) split the row and shifted every later column.
    /// </summary>
    public static class AiDecisionCsv
    {
        public static string Row(DateTime utc, string url, string domain, string tabTitle, double probability,
            string justification, string currentTask, string intention, string reason)
        {
            return string.Join(",", new[]
            {
                utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                Field(url), Field(domain), Field(tabTitle),
                probability.ToString("R", CultureInfo.InvariantCulture),
                Field(justification), Field(currentTask), Field(intention), Field(reason),
            });
        }

        /// <summary>RFC 4180: quoted, inner quotes doubled; line breaks become spaces so a row stays one line.</summary>
        public static string Field(string value)
        {
            string text = (value ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Splits a row written by <see cref="Row"/> back into its fields (for tests and readers).</summary>
        public static string[] Parse(string row)
        {
            var fields = new System.Collections.Generic.List<string>();
            var current = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (row ?? "").Length; i++)
            {
                char c = row[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < row.Length && row[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else current.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }
}
