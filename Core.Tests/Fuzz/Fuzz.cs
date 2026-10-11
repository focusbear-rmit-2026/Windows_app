using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FocusBear.Core.Tests.Fuzz
{
    /// <summary>
    /// Deterministic random inputs for the fuzz tests: each test names its seed, so a failure replays exactly.
    /// The alphabet leans on the characters parsers trip over (digits, separators, quotes, backslashes, control
    /// characters, surrogate halves, RTL marks) rather than uniform noise.
    /// </summary>
    internal sealed class Fuzz
    {
        private readonly Random _random;

        public Fuzz(int seed) { _random = new Random(seed); }

        private static readonly string[] Pieces =
        {
            "0", "1", "9", "12", "59", "60", "99", "100", "999", "2147483647", "2147483648", "-1", "-0", "00", "1e9",
            "h", "m", "min", "mins", "hr", "hrs", "d", "w", "am", "pm", "AM", ":", ".", ",", "-", "+", "/", "\\", " ", "  ",
            "\t", "\n", "\r\n", "\0", "#", "!", "*", "@", "=", "\"", "'", "`", "?", "&", "%", "%20", "%zz", "=true",
            "today", "tomorrow", "mon", "Monday", "next", "week", "in", "at", "jan", "31", "29", "feb", "2026", "9999",
            "1970", "0001", "T", "Z", "+10:00", "é", "ü", "日本", "‏", "😀", "\uD83D", "￿", " ",
            "{", "}", "[", "]", "null", "true", "false", "\"\"", "{}", "[]", "http://", "https://", "file:///", "unblock_it",
            "old_url", "v", "V", "1.2.3.4.5", "..", "M50", "C30", "|", "||", "STARTHB", "EVENT", "_", "__",
        };

        public int Next(int max) => _random.Next(max);
        public int Next(int min, int max) => _random.Next(min, max);
        public bool Coin() => _random.Next(2) == 0;
        public double NextDouble() => _random.NextDouble();

        /// <summary>A string of up to <paramref name="maxPieces"/> pieces, now and then a random char.</summary>
        public string Text(int maxPieces = 12)
        {
            int n = _random.Next(maxPieces + 1);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (_random.Next(8) == 0) sb.Append((char)_random.Next(0, 0x3000));
                else sb.Append(Pieces[_random.Next(Pieces.Length)]);
            }
            return sb.ToString();
        }

        /// <summary>Text, or null, or empty, or something very long.</summary>
        public string Any()
        {
            string s;
            switch (_random.Next(20))
            {
                case 0: s = null; break;
                case 1: s = ""; break;
                case 2: s = new string('9', _random.Next(1, 400)); break;
                case 3: s = new string(' ', _random.Next(1, 50)); break;
                default: s = Text(); break;
            }
            Generated.Add(s);
            return s;
        }

        /// <summary>What <see cref="Any"/> handed out in the current case, for the failure message.</summary>
        public readonly List<string> Generated = new List<string>();

        public DateTime Date()
        {
            switch (_random.Next(10))
            {
                case 0: return DateTime.MinValue;
                case 1: return DateTime.MaxValue;
                case 2: return new DateTime(2026, 4, 5, 2, 30, 0);   // Sydney: the repeated hour
                case 3: return new DateTime(2026, 10, 4, 2, 30, 0);  // Sydney: the skipped hour
                default:
                    long ticks = (long)(_random.NextDouble() * (new DateTime(2100, 1, 1).Ticks - new DateTime(1900, 1, 1).Ticks));
                    return new DateTime(new DateTime(1900, 1, 1).Ticks + ticks);
            }
        }

        /// <summary>A random JSON-ish document: valid JSON of random shape, or a damaged one.</summary>
        public string Json(IList<string> keys, int depth = 0)
        {
            if (_random.Next(10) == 0) return Text();      // not JSON at all
            string doc = Value(keys, depth);
            if (_random.Next(8) == 0 && doc.Length > 1) doc = doc.Substring(0, _random.Next(doc.Length));  // truncated
            return doc;
        }

        private string Value(IList<string> keys, int depth)
        {
            switch (depth > 3 ? _random.Next(5) : _random.Next(8))
            {
                case 0: return "null";
                case 1: return Coin() ? "true" : "false";
                case 2: return _random.Next(3) == 0 ? "1e308" : (_random.Next(-100000, 100000) / (Coin() ? 1.0 : 7.0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                case 3:
                case 4: return Newtonsoft.Json.JsonConvert.ToString(Text());
                case 5:
                case 6:
                {
                    var sb = new StringBuilder("{");
                    int n = _random.Next(6);
                    for (int i = 0; i < n; i++)
                    {
                        if (i > 0) sb.Append(',');
                        string key = Coin() && keys.Count > 0 ? keys[_random.Next(keys.Count)] : Text(2);
                        sb.Append(Newtonsoft.Json.JsonConvert.ToString(key)).Append(':').Append(Value(keys, depth + 1));
                    }
                    return sb.Append('}').ToString();
                }
                default:
                {
                    var sb = new StringBuilder("[");
                    int n = _random.Next(5);
                    for (int i = 0; i < n; i++) { if (i > 0) sb.Append(','); sb.Append(Value(keys, depth + 1)); }
                    return sb.Append(']').ToString();
                }
            }
        }

        /// <summary>Runs <paramref name="check"/> for each input, naming the seed and the input on failure.</summary>
        public static void Run(int seed, int count, Action<Fuzz, int> check)
        {
            // FB_FUZZ_SCALE / FB_FUZZ_SEED_OFFSET: a longer or different run by hand; CI uses the fixed seeds.
            if (int.TryParse(Environment.GetEnvironmentVariable("FB_FUZZ_SCALE"), out int scale) && scale > 1) count *= scale;
            if (int.TryParse(Environment.GetEnvironmentVariable("FB_FUZZ_SEED_OFFSET"), out int offset)) seed += offset;
            var fuzz = new Fuzz(seed);
            for (int i = 0; i < count; i++)
            {
                fuzz.Generated.Clear();
                try { check(fuzz, i); }
                catch (Exception ex)
                {
                    string inputs = string.Join(" | ", fuzz.Generated.Select(g => g == null ? "<null>" : Newtonsoft.Json.JsonConvert.ToString(g)));
                    throw new Exception("seed " + seed + ", case " + i + ": " + ex.GetType().Name + ": " + ex.Message + " INPUTS: " + inputs, ex);
                }
            }
        }

        /// <summary>RFC 4180 reader (quoted fields may hold commas, quotes and line breaks).</summary>
        public static List<List<string>> ReadCsv(string text, string lineBreak)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, any = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                    continue;
                }
                if (c == '"') { quoted = true; any = true; }
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (string.CompareOrdinal(text, i, lineBreak, 0, lineBreak.Length) == 0)
                {
                    row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>();
                    i += lineBreak.Length - 1;
                }
                else { cell.Append(c); any = true; }
            }
            if (any || row.Count > 0 || cell.Length > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
