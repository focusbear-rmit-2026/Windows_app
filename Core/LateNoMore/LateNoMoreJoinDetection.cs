using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.LateNoMore
{
    /// <summary>What told Late No More that the user joined the meeting.</summary>
    public enum LateNoMoreJoinSignal
    {
        None,
        /// <summary>A meeting app is using the microphone or camera (the Mac's meeting detection).</summary>
        OnCall,
        /// <summary>The meeting's own link is open in the browser (Google Meet, Zoom's web client, ...).</summary>
        MeetingPage,
    }

    /// <summary>
    /// Works out that the user has joined the meeting an alert is showing for, so there is no "I'm attending" button to
    /// press (report 2026-09-28_16-01-47). The Mac treats "a meeting app has the microphone or camera" as being on a call
    /// (<c>AppDelegate.checkMeetingsInternal</c>); Windows has the same check (the Meetings focus mode's apps using the
    /// mic or camera) and adds the meeting's own page in the foreground browser, which also covers a browser that isn't
    /// in the Meetings mode's app list.
    /// </summary>
    public static class LateNoMoreJoinDetection
    {
        /// <summary>A call this long before the start counts as joining early; earlier than that it is another call.</summary>
        public static readonly TimeSpan EarlyJoin = TimeSpan.FromMinutes(10);

        public static LateNoMoreJoinSignal Detect(LateNoMoreEvent meeting, DateTime now, bool onCall, string browserUrl)
        {
            if (meeting == null) return LateNoMoreJoinSignal.None;
            if (now < meeting.Start - EarlyJoin || now >= meeting.End) return LateNoMoreJoinSignal.None;
            if (IsMeetingPage(meeting.MeetingUrl, browserUrl)) return LateNoMoreJoinSignal.MeetingPage;
            if (onCall) return LateNoMoreJoinSignal.OnCall;
            return LateNoMoreJoinSignal.None;
        }

        /// <summary>
        /// Whether <paramref name="browserUrl"/> is the meeting at <paramref name="meetingUrl"/>: the same host and path
        /// (scheme, "www.", case, query, fragment and a trailing slash ignored), or for Zoom the same meeting number, since
        /// its web client moves from <c>/j/123</c> to <c>/wc/123/join</c> on another host.
        /// </summary>
        public static bool IsMeetingPage(string meetingUrl, string browserUrl)
        {
            Tuple<string, string> meeting = Split(meetingUrl), seen = Split(browserUrl);
            if (meeting == null || seen == null) return false;
            if (meeting.Item2.Length == 0) return false;   // a bare host names a service, not a meeting

            if (IsZoom(meeting.Item1) && IsZoom(seen.Item1))
            {
                string number = ZoomNumber(meeting.Item2);
                return number != null && number == ZoomNumber(seen.Item2);
            }
            return meeting.Item1 == seen.Item1 && meeting.Item2 == seen.Item2;
        }

        private static bool IsZoom(string host) => host == "zoom.us" || host.EndsWith(".zoom.us", StringComparison.Ordinal);

        private static readonly Regex ZoomMeeting = new Regex(@"/(?:j|wc|s|w)/(\d{9,12})(?:/|$)", RegexOptions.CultureInvariant);

        private static string ZoomNumber(string path)
        {
            Match m = ZoomMeeting.Match(path);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>(host, path) lower-cased, without "www.", the query, the fragment or a trailing slash; null if it isn't a web address.</summary>
        private static Tuple<string, string> Split(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string s = url.Trim();
            int cut = s.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) s = s.Substring(0, cut);
            int scheme = s.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                string name = s.Substring(0, scheme).ToLowerInvariant();
                if (name != "http" && name != "https") return null;
                s = s.Substring(scheme + 3);
            }
            int slash = s.IndexOf('/');
            string host = (slash >= 0 ? s.Substring(0, slash) : s).ToLowerInvariant();
            string path = slash >= 0 ? s.Substring(slash).ToLowerInvariant().TrimEnd('/') : "";
            int port = host.IndexOf(':');
            if (port >= 0) host = host.Substring(0, port);
            if (host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
            if (host.Length == 0 || !host.Contains(".") || host.Any(char.IsWhiteSpace)) return null;
            return Tuple.Create(host, path);
        }
    }
}
