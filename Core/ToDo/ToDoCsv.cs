using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Web <c>downloadToDoCSVFile</c>: title, description, objective, status, quadrant, projects, subtasks,
    /// due date, created — every cell quoted. The labels are the caller's, since this project carries no
    /// translations.
    /// </summary>
    public static class ToDoCsv
    {
        /// <summary>What a spreadsheet reads as the start of a formula (OWASP "CSV injection").</summary>
        private static readonly char[] FormulaLeads = { '=', '+', '-', '@', '\t', '\r' };

        public static string Build(IEnumerable<ToDoTask> tasks, IList<string> headers,
                                   Func<ToDoTask, string> statusLabel, Func<ToDoTask, string> quadrantLabel)
        {
            var lines = new List<string> { string.Join(",", headers.Select(Escape)) };
            foreach (ToDoTask task in tasks)
            {
                var row = new[]
                {
                    task.Title ?? string.Empty,
                    ToDoHtml.PlainText(task.Details ?? string.Empty),
                    ToDoHtml.PlainText(task.Objective ?? string.Empty),
                    statusLabel(task),
                    quadrantLabel(task),
                    string.Join(",", task.TagList.Select(tag => tag.Text)),
                    string.Join(",", task.SubtaskList.Select(subtask => subtask.Name)),
                    Day(task.DueDate),
                    Day(task.CreatedAt),
                };
                lines.Add(string.Join(",", row.Select(Escape)));
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The local calendar day a wire date means, as <c>yyyy-MM-dd</c> — the day the list shows, as the web
        /// exports it. The raw instant is UTC, which is the previous day for everyone east of Greenwich
        /// between midnight and their offset. A value that does not read as a date goes out as sent.
        /// </summary>
        private static string Day(string wire)
        {
            if (string.IsNullOrWhiteSpace(wire)) return string.Empty;
            return ToDoDates.YmdFromIso(wire) ?? wire;
        }

        /// <summary>Quoted, with a leading quote mark on anything a spreadsheet would evaluate as a formula.</summary>
        private static string Escape(string value)
        {
            string text = value ?? string.Empty;
            if (text.Length > 0 && Array.IndexOf(FormulaLeads, text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
