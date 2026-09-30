using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// A note body as structure rather than markup: what the web editor's HTML means, with nothing
    /// about how any one platform draws it. The Mac keeps this in an NSAttributedString and the
    /// Windows editor in a FlowDocument; both convert through here, so the HTML rules exist once and
    /// can be tested without a UI framework.
    /// </summary>
    public sealed class NoteDocument
    {
        public List<NoteBlock> Blocks { get; } = new List<NoteBlock>();

        public NoteDocument() { }

        public NoteDocument(IEnumerable<NoteBlock> blocks)
        {
            Blocks.AddRange(blocks);
        }
    }

    public abstract class NoteBlock { }

    /// <summary>One paragraph, or one heading when <see cref="HeadingLevel"/> is 1–6.</summary>
    public sealed class NoteParagraph : NoteBlock
    {
        /// <summary>0 for body text; 1–6 for an <c>&lt;hN&gt;</c>, which exports back as the same level.</summary>
        public int HeadingLevel { get; set; }
        public List<NoteInline> Inlines { get; } = new List<NoteInline>();

        public NoteParagraph() { }

        public NoteParagraph(params NoteInline[] inlines)
        {
            Inlines.AddRange(inlines);
        }

        public bool IsHeading => HeadingLevel >= 1 && HeadingLevel <= 6;

        /// <summary>The paragraph's characters, line breaks as "\n".</summary>
        public string Text => string.Concat(Inlines.Select(i => i is NoteRun run ? run.Text : "\n"));

        public bool IsBlank => string.IsNullOrWhiteSpace(Text);
    }

    public sealed class NoteList : NoteBlock
    {
        public bool Ordered { get; set; }
        public List<NoteListItem> Items { get; } = new List<NoteListItem>();
    }

    /// <summary>A list item holds blocks, so a nested list is simply a <see cref="NoteList"/> inside one.</summary>
    public sealed class NoteListItem
    {
        public List<NoteBlock> Blocks { get; } = new List<NoteBlock>();
    }

    public abstract class NoteInline { }

    /// <summary>A run of text sharing one set of styles.</summary>
    public sealed class NoteRun : NoteInline
    {
        public string Text { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public bool Code { get; set; }
        /// <summary>
        /// The link target, already vetted by <see cref="NoteLinks.OpenableUrl"/>: an unsafe href never
        /// gets this far, so anything a renderer finds here is fine to make clickable.
        /// </summary>
        public string Link { get; set; }

        public NoteRun() { }

        public NoteRun(string text)
        {
            Text = text;
        }

        internal bool SameStyleAs(NoteRun other) =>
            Bold == other.Bold && Italic == other.Italic && Underline == other.Underline
            && Strikethrough == other.Strikethrough && Code == other.Code && Link == other.Link;
    }

    /// <summary>A <c>&lt;br&gt;</c>: a new line inside the same paragraph.</summary>
    public sealed class NoteLineBreak : NoteInline { }
}
