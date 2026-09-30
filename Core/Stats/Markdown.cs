using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FocusBear.Core.Stats
{
    public sealed class MarkdownRun : IEquatable<MarkdownRun>
    {
        public MarkdownRun(string text, bool bold = false, bool italic = false, bool code = false, string link = null)
        {
            Text = text;
            IsBold = bold;
            IsItalic = italic;
            IsCode = code;
            LinkUrl = link;
        }

        public string Text { get; internal set; }
        public bool IsBold { get; }
        public bool IsItalic { get; }
        public bool IsCode { get; }
        public string LinkUrl { get; }

        internal MarkdownRun With(string text = null, bool? bold = null, bool? italic = null, bool? code = null, string link = null, bool setLink = false) =>
            new MarkdownRun(text ?? Text, bold ?? IsBold, italic ?? IsItalic, code ?? IsCode, setLink ? link : LinkUrl);

        internal bool SameStyle(MarkdownRun o) => IsBold == o.IsBold && IsItalic == o.IsItalic && IsCode == o.IsCode && LinkUrl == o.LinkUrl;

        public bool Equals(MarkdownRun o) => o != null && Text == o.Text && SameStyle(o);
        public override bool Equals(object obj) => Equals(obj as MarkdownRun);
        public override int GetHashCode() => (Text ?? "").GetHashCode();
        public override string ToString() => "\"" + Text + "\"" + (IsBold ? " b" : "") + (IsItalic ? " i" : "") + (IsCode ? " code" : "") + (LinkUrl != null ? " ->" + LinkUrl : "");
    }

    public enum MarkdownBlockKind { Paragraph, Heading, Bullet, Numbered }

    public sealed class MarkdownBlock
    {
        public MarkdownBlock(MarkdownBlockKind kind, IList<MarkdownRun> runs, int headingLevel = 0, string label = null)
        {
            Kind = kind;
            Runs = runs;
            HeadingLevel = headingLevel;
            Label = label;
        }

        public MarkdownBlockKind Kind { get; }
        public IList<MarkdownRun> Runs { get; }
        public int HeadingLevel { get; }
        /// <summary>"1." or "2)" for a numbered line.</summary>
        public string Label { get; }

        public string PlainText => string.Concat(Runs.Select(r => r.Text));
    }

    /// <summary>
    /// The pragmatic markdown subset the motivational-summary stream emits: paragraphs, bold, italic, code,
    /// headings, bullet and numbered lines, links. Port of the Mac's <c>MarkdownParser</c>. It re-runs on every
    /// streamed chunk, so it is one linear scan and leaves any marker it cannot close as literal text — a
    /// half-arrived <c>**bol</c> reads as typed until its closing marker lands.
    /// </summary>
    public static class MarkdownParser
    {
        public const int MaxHeadingLevel = 3;
        private static readonly char[] BulletMarkers = { '-', '*', '•' };
        private static readonly string[] BoldMarkers = { "**", "__" };
        private static readonly char[] ItalicMarkers = { '*', '_' };

        public static IList<MarkdownBlock> Parse(string markdown)
        {
            var blocks = new List<MarkdownBlock>();
            var paragraph = new List<string>();

            void Flush()
            {
                if (paragraph.Count == 0) return;
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, ParseInline(string.Join("\n", paragraph))));
                paragraph.Clear();
            }

            foreach (string raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n', '\r'))
            {
                string line = raw.Trim(' ', '\t');
                if (line.Length == 0) { Flush(); continue; }
                var block = LineBlock(line);
                if (block != null) { Flush(); blocks.Add(block); }
                else paragraph.Add(line);
            }
            Flush();
            return blocks;
        }

        private static MarkdownBlock LineBlock(string line) => Heading(line) ?? Bullet(line) ?? Numbered(line);

        private static MarkdownBlock Heading(string line)
        {
            int level = 0;
            while (level < line.Length && line[level] == '#') level++;
            if (level < 1 || level > MaxHeadingLevel || level >= line.Length || line[level] != ' ') return null;
            return new MarkdownBlock(MarkdownBlockKind.Heading, ParseInline(line.Substring(level).Trim()), headingLevel: level);
        }

        private static MarkdownBlock Bullet(string line)
        {
            if (line.Length < 2 || Array.IndexOf(BulletMarkers, line[0]) < 0 || line[1] != ' ') return null;
            return new MarkdownBlock(MarkdownBlockKind.Bullet, ParseInline(line.Substring(1).Trim()));
        }

        private static MarkdownBlock Numbered(string line)
        {
            int digits = 0;
            while (digits < line.Length && char.IsDigit(line[digits])) digits++;
            if (digits == 0 || digits + 1 >= line.Length) return null;
            char separator = line[digits];
            if ((separator != '.' && separator != ')') || line[digits + 1] != ' ') return null;
            return new MarkdownBlock(MarkdownBlockKind.Numbered, ParseInline(line.Substring(digits + 1).Trim()), label: line.Substring(0, digits + 1));
        }

        public static IList<MarkdownRun> ParseInline(string text) => ParseInline(text ?? "", new MarkdownRun(""));

        /// <summary>A recognised span is parsed recursively with its style applied, so <c>**bold *and italic***</c> nests.</summary>
        private static List<MarkdownRun> ParseInline(string chars, MarkdownRun style)
        {
            var runs = new List<MarkdownRun>();
            var literal = new StringBuilder();
            int index = 0;

            void FlushLiteral()
            {
                if (literal.Length == 0) return;
                Append(style.With(text: literal.ToString()), runs);
                literal.Clear();
            }

            while (index < chars.Length)
            {
                var span = Span(chars, index, style);
                if (span != null)
                {
                    FlushLiteral();
                    foreach (var run in span.Value.Runs) Append(run, runs);
                    index = span.Value.End;
                }
                else
                {
                    literal.Append(chars[index]);
                    index++;
                }
            }
            FlushLiteral();
            return runs;
        }

        private static (List<MarkdownRun> Runs, int End)? Span(string chars, int index, MarkdownRun style)
        {
            if (style.IsCode) return null;
            var code = CodeSpan(chars, index, style);
            if (code != null) return code;
            var link = LinkSpan(chars, index, style);
            if (link != null) return link;
            if (!style.IsBold)
                foreach (var marker in BoldMarkers)
                {
                    var bold = EmphasisSpan(chars, index, marker, style, s => s.With(bold: true));
                    if (bold != null) return bold;
                }
            if (!style.IsItalic)
                foreach (var marker in ItalicMarkers)
                {
                    var italic = EmphasisSpan(chars, index, marker.ToString(), style, s => s.With(italic: true));
                    if (italic != null) return italic;
                }
            return null;
        }

        private static (List<MarkdownRun> Runs, int End)? CodeSpan(string chars, int index, MarkdownRun style)
        {
            if (chars[index] != '`') return null;
            int close = chars.IndexOf('`', index + 1);
            if (close <= index + 1) return null;
            return (new List<MarkdownRun> { style.With(text: chars.Substring(index + 1, close - index - 1), code: true) }, close + 1);
        }

        private static (List<MarkdownRun> Runs, int End)? LinkSpan(string chars, int index, MarkdownRun style)
        {
            if (chars[index] != '[') return null;
            int closeBracket = chars.IndexOf(']', index + 1);
            if (closeBracket < 0) return null;
            int urlStart = closeBracket + 1;
            if (urlStart >= chars.Length || chars[urlStart] != '(') return null;
            int closeParen = chars.IndexOf(')', urlStart + 1);
            if (closeParen < 0) return null;
            string url = chars.Substring(urlStart + 1, closeParen - urlStart - 1);
            if (url.Length == 0 || url.Contains(" ")) return null;
            var inner = chars.Substring(index + 1, closeBracket - index - 1);
            return (ParseInline(inner, style.With(link: url, setLink: true)), closeParen + 1);
        }

        /// <summary>
        /// A marker opens a span only when it hugs non-space text on the inside, and closes only when the text
        /// before it is not a space — so "2 * 3 * 4" and snake_case_words stay literal.
        /// </summary>
        private static (List<MarkdownRun> Runs, int End)? EmphasisSpan(string chars, int index, string marker, MarkdownRun style, Func<MarkdownRun, MarkdownRun> apply)
        {
            int contentStart = index + marker.Length;
            if (!Matches(marker, chars, index) || contentStart >= chars.Length || char.IsWhiteSpace(chars[contentStart])) return null;
            // Either half of a ** that has not closed yet (mid-stream) is a pending bold opener, not an italic
            // opener wrapping a stray star.
            if (marker.Length == 1 && chars[contentStart] == marker[0]) return null;
            if (marker.Length == 1 && index > 0 && chars[index - 1] == marker[0]) return null;
            if (marker == "_" && index > 0 && char.IsLetterOrDigit(chars[index - 1])) return null;
            int? close = ClosingIndex(marker, chars, contentStart);
            if (close == null) return null;
            var inner = chars.Substring(contentStart, close.Value - contentStart);
            return (ParseInline(inner, apply(style)), close.Value + marker.Length);
        }

        private static int? ClosingIndex(string marker, string chars, int start)
        {
            int index = start;
            while (index + marker.Length <= chars.Length)
            {
                if (Matches(marker, chars, index))
                {
                    bool isDoubled = marker.Length == 1 && index + 1 < chars.Length && chars[index + 1] == marker[0];
                    if (isDoubled)
                    {
                        // A ** inside a *…* span is a nested bold marker, not the italic closer.
                        index += 2;
                        continue;
                    }
                    if (index > start && !char.IsWhiteSpace(chars[index - 1]))
                    {
                        if (marker == "_" && index + 1 < chars.Length && char.IsLetterOrDigit(chars[index + 1]))
                        {
                            index++;
                            continue;
                        }
                        return index;
                    }
                }
                index++;
            }
            return null;
        }

        private static bool Matches(string marker, string chars, int index) =>
            index + marker.Length <= chars.Length && string.CompareOrdinal(chars, index, marker, 0, marker.Length) == 0;

        /// <summary>Adjacent runs with identical styling merge so the rendered text stays small.</summary>
        private static void Append(MarkdownRun run, List<MarkdownRun> runs)
        {
            if (string.IsNullOrEmpty(run.Text)) return;
            if (runs.Count > 0 && runs[runs.Count - 1].SameStyle(run))
            {
                runs[runs.Count - 1] = runs[runs.Count - 1].With(text: runs[runs.Count - 1].Text + run.Text);
                return;
            }
            runs.Add(run);
        }
    }
}
