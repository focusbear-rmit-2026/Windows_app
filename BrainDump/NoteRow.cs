using FocusBear.Core.Notes;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// One row of the Notes list, shaped for binding: a personal note or a shared (project) note, with the
    /// text each line of the card shows already worked out. The list is rebuilt from
    /// <see cref="NotesFeed.Items"/> whenever its inputs change, so a row never needs to notify.
    /// </summary>
    public sealed class NoteRow
    {
        private NoteRow(NotesListItem item)
        {
            Item = item;
        }

        public NotesListItem Item { get; }

        public bool IsProject { get { return Item.IsProject; } }
        public bool IsPersonal { get { return !Item.IsProject; } }

        public string Title { get; private set; }
        /// <summary>A note saved without a title shows the "Untitled note" label, dimmed, instead of an empty line.</summary>
        public bool IsUntitled { get; private set; }
        /// <summary>"Linked to habit: X" for a personal note, the project's name for a shared one.</summary>
        public string Subtitle { get; private set; }
        public bool HasSubtitle { get { return !string.IsNullOrEmpty(Subtitle); } }
        public string Preview { get; private set; }
        public bool HasPreview { get { return !string.IsNullOrWhiteSpace(Preview); } }
        public IList<NoteTag> Tags { get; private set; }
        public bool HasTags { get { return Tags != null && Tags.Count > 0; } }
        public string DateText { get; private set; }

        /// <summary>Only personal notes are edited and deleted from the list; a shared note opens.</summary>
        public bool CanEditInPlace { get { return IsPersonal; } }

        public static NoteRow For(NotesListItem item, string linkedToHabitLabel, string untitledLabel)
        {
            var row = new NoteRow(item);
            if (item.IsProject)
            {
                var entry = item.Project;
                row.Title = entry.Title;
                row.Subtitle = entry.ProjectName;
                row.Preview = PreviewOf(entry.Note, () => entry.Note.PlainText);
                row.Tags = new List<NoteTag>();
            }
            else
            {
                var note = item.Personal;
                row.Title = string.IsNullOrWhiteSpace(note.Title) || string.IsNullOrEmpty(note.ActivityEmoji) ? note.Title : note.ActivityEmoji + " " + note.Title;
                row.Subtitle = string.IsNullOrEmpty(note.ActivityName) ? null : linkedToHabitLabel + ": " + note.ActivityName;
                row.Preview = PreviewOf(note, () => NoteHtml.PlainText(note.Body ?? ""));
                row.Tags = note.TagList;
            }
            if (string.IsNullOrWhiteSpace(row.Title))
            {
                row.Title = untitledLabel;
                row.IsUntitled = true;
            }
            row.DateText = FormatDate(item.SortDate);
            return row;
        }

        /// <summary>More than the card's three preview lines can show at its widest.</summary>
        internal const int PreviewLength = 400;

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, string> Previews =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, string>();

        /// <summary>
        /// The card's preview: the note's plain text, cut to what three lines can show, and worked out once per
        /// note object (a changed note arrives as a new object). The list is rebuilt on every keystroke in the
        /// search box, and the list does not virtualize: every row re-parsed its whole body (up to 50,000
        /// characters) and handed all of it to a TextBlock to measure, so typing lagged with long notes.
        /// </summary>
        internal static string PreviewOf(object note, Func<string> plainText)
        {
            return Previews.GetValue(note, _ => Clip(plainText() ?? ""));
        }

        internal static string Clip(string text)
        {
            if (text.Length <= PreviewLength) return text;
            int cut = PreviewLength;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut) + "…";
        }

        /// <summary>The web list's "MMM d, yyyy hh:mm tt", in the user's culture and local time.</summary>
        internal static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("MMM d, yyyy hh:mm tt", CultureInfo.CurrentCulture) : "";
        }
    }
}
