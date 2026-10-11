using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FocusBear.Core.Stats
{
    public enum PepTalkEventKind { Text, Done, Fatal, Invalid }

    public sealed class PepTalkEvent : IEquatable<PepTalkEvent>
    {
        private PepTalkEvent(PepTalkEventKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public PepTalkEventKind Kind { get; }
        public string Text { get; }

        public static PepTalkEvent Chunk(string text) => new PepTalkEvent(PepTalkEventKind.Text, text);
        public static readonly PepTalkEvent Done = new PepTalkEvent(PepTalkEventKind.Done, null);
        public static readonly PepTalkEvent Fatal = new PepTalkEvent(PepTalkEventKind.Fatal, null);
        public static readonly PepTalkEvent Invalid = new PepTalkEvent(PepTalkEventKind.Invalid, null);

        public bool Equals(PepTalkEvent other) => other != null && Kind == other.Kind && Text == other.Text;
        public override bool Equals(object obj) => Equals(obj as PepTalkEvent);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Text?.GetHashCode() ?? 0);
        public override string ToString() => Kind == PepTalkEventKind.Text ? "text(" + Text + ")" : Kind.ToString();
    }

    /// <summary>
    /// Decodes <c>GET /user/motivational-summary</c> (text/event-stream), like the web's <c>fetchEventSource</c>:
    /// each <c>data:</c> line is a text chunk, <c>[DONE]</c> ends the message, <c>event: fatal_error</c> aborts.
    /// Port of the Mac's <c>MotivationalSummaryDecoder</c>.
    ///
    /// Bytes are buffered until a whole line exists, because a packet can split an emoji or an accent in
    /// two; decoding per packet would corrupt it. Everything after <c>[DONE]</c> is ignored: the server
    /// keeps streaming its prompt, which must never reach the UI or the logs.
    /// </summary>
    public sealed class PepTalkStreamDecoder
    {
        /// <summary>A line longer than this is not a pep talk.</summary>
        public const int MaxBufferedBytes = 65536;

        private readonly List<byte> _buffer = new List<byte>();
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public bool Finished { get; private set; }

        public IList<PepTalkEvent> Consume(byte[] data, int offset, int count)
        {
            var events = new List<PepTalkEvent>();
            if (Finished) return events;
            for (int i = 0; i < count; i++) _buffer.Add(data[offset + i]);
            while (true)
            {
                int newline = _buffer.IndexOf((byte)'\n');
                if (newline < 0) break;
                byte[] bytes = _buffer.GetRange(0, newline).ToArray();
                _buffer.RemoveRange(0, newline + 1);
                string line;
                try { line = StrictUtf8.GetString(bytes); }
                catch (DecoderFallbackException)
                {
                    Finished = true;
                    events.Add(PepTalkEvent.Invalid);
                    return events;
                }
                if (line.EndsWith("\r", StringComparison.Ordinal)) line = line.Substring(0, line.Length - 1);
                if (line == "event: fatal_error")
                {
                    Finished = true;
                    events.Add(PepTalkEvent.Fatal);
                    return events;
                }
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                string value = line.Substring(5);
                if (value.StartsWith(" ", StringComparison.Ordinal)) value = value.Substring(1);
                if (value == "[DONE]")
                {
                    Finished = true;
                    events.Add(PepTalkEvent.Done);
                    return events;
                }
                if (value.Length > 0) events.Add(PepTalkEvent.Chunk(value));
            }
            if (_buffer.Count > MaxBufferedBytes)
            {
                Finished = true;
                events.Add(PepTalkEvent.Invalid);
            }
            return events;
        }

        public IList<PepTalkEvent> Consume(byte[] data) => Consume(data, 0, data.Length);
    }

    public enum PepTalkFailureKind { RateLimited, ServerError, Connection, Other }

    /// <summary>
    /// Why a pep talk did not arrive. A bare "it failed" made every failure read "try again later", so a user
    /// being rate limited pressed Retry five times in two minutes (report 2026-09-22_07-45-26). Port of the
    /// Mac's <c>PepTalkFailure</c>.
    /// </summary>
    public sealed class PepTalkFailure : IEquatable<PepTalkFailure>
    {
        /// <summary>Used when a 429 carries no Retry-After: long enough to clear a burst, short enough not to feel broken.</summary>
        public static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromSeconds(60);
        /// <summary>A misconfigured header must not disable the feature for the rest of the day.</summary>
        public static readonly TimeSpan MaximumHonouredWait = TimeSpan.FromMinutes(15);

        private PepTalkFailure(PepTalkFailureKind kind, TimeSpan? retryAfter)
        {
            Kind = kind;
            RetryAfter = retryAfter;
        }

        public PepTalkFailureKind Kind { get; }
        /// <summary>The server's own advice, for a rate limit that sent any.</summary>
        public TimeSpan? RetryAfter { get; }

        public static PepTalkFailure RateLimited(TimeSpan? retryAfter) => new PepTalkFailure(PepTalkFailureKind.RateLimited, retryAfter);
        public static readonly PepTalkFailure ServerError = new PepTalkFailure(PepTalkFailureKind.ServerError, null);
        public static readonly PepTalkFailure Connection = new PepTalkFailure(PepTalkFailureKind.Connection, null);
        public static readonly PepTalkFailure Other = new PepTalkFailure(PepTalkFailureKind.Other, null);

        public bool IsRateLimited => Kind == PepTalkFailureKind.RateLimited;

        /// <summary>How long to hold Retry. Only a rate limit has a knowable answer; guessing one for the rest would be inventing a number.</summary>
        public TimeSpan? RetryDelay => IsRateLimited ? RetryAfter ?? DefaultRateLimitWait : (TimeSpan?)null;

        /// <summary>
        /// Classify a response. Retry-After may be seconds or an HTTP date; only the seconds form is honoured,
        /// because a date needs a clock both ends agree on and a wrong one would hold the button for hours.
        /// </summary>
        public static PepTalkFailure From(int status, string retryAfterHeader)
        {
            if (status == 429)
            {
                if (double.TryParse((retryAfterHeader ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0)
                {
                    var wait = TimeSpan.FromSeconds(seconds);
                    return RateLimited(wait > MaximumHonouredWait ? MaximumHonouredWait : wait);
                }
                return RateLimited(null);
            }
            if (status == 0) return Connection;
            if (status >= 500 && status < 600) return ServerError;
            return Other;
        }

        public bool Equals(PepTalkFailure other) => other != null && Kind == other.Kind && RetryAfter == other.RetryAfter;
        public override bool Equals(object obj) => Equals(obj as PepTalkFailure);
        public override int GetHashCode() => ((int)Kind * 397) ^ RetryAfter.GetHashCode();
        public override string ToString() => Kind + (RetryAfter.HasValue ? "(" + RetryAfter + ")" : "");
    }

    /// <summary>
    /// The last pep talk that arrived, so a screen appearance shows it again instead of generating another.
    /// Every pep talk is an LLM call against a per-user budget the backend caps at 60 a day; a Mac that
    /// generated one per appearance spent 57, 60, 49 and 48 on consecutive days and then met a 429 while
    /// doing nothing unusual. Port of the Mac's <c>PepTalkCache</c>. Static because the screen that uses it
    /// is rebuilt each time it appears.
    /// </summary>
    public static class PepTalkCache
    {
        /// <summary>A product judgement: long enough that a routine costs one call, short enough to feel fresh.</summary>
        public static readonly TimeSpan ReuseWindow = TimeSpan.FromMinutes(30);

        private static readonly object Lock = new object();
        private static string _text = "", _tone = "", _language = "";
        private static DateTime? _cachedAt;

        /// <summary>A stamp in the future means the clock moved; it is stale, not trusted for the next half hour.</summary>
        public static bool IsFresh(DateTime? cachedAt, DateTime now, TimeSpan? window = null)
        {
            if (cachedAt == null) return false;
            TimeSpan age = now - cachedAt.Value;
            return age >= TimeSpan.Zero && age < (window ?? ReuseWindow);
        }

        /// <summary>The message to reuse for <paramref name="language"/>, or null when there is nothing worth reusing.</summary>
        public static (string Text, string Tone)? Reusable(string language, DateTime now)
        {
            lock (Lock)
            {
                if (!IsFresh(_cachedAt, now) || _text.Length == 0 || _language != language) return null;
                return (_text, _tone);
            }
        }

        /// <summary>An empty message is never stored: a failed stream must not overwrite — and re-stamp — a good one.</summary>
        public static void Store(string text, string tone, string language, DateTime now)
        {
            if (string.IsNullOrEmpty(text)) return;
            lock (Lock)
            {
                _text = text;
                _tone = tone ?? "";
                _language = language ?? "";
                _cachedAt = now;
            }
        }

        /// <summary>Used by tests, and on sign-out: one account's pep talk is not another's.</summary>
        public static void Clear()
        {
            lock (Lock)
            {
                _text = _tone = _language = "";
                _cachedAt = null;
            }
        }
    }

    public static class PepTalkTones
    {
        /// <summary>Web <c>AiToneOptions</c>, in the order the tone buttons appear. The wire values; names are localised.</summary>
        public static readonly string[] All =
        {
            "humorous", "sassy", "court jester", "pirate", "future self in twenty years",
            "scientist", "cheerleader", "upbeat", "factual",
        };

        public const string DesktopDeviceType = "desktop";
    }
}
