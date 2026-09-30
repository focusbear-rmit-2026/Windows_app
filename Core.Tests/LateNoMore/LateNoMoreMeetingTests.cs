using System;
using System.Linq;
using FocusBear.Core.LateNoMore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.LateNoMore
{
    /// <summary>The Mac's LateNoMoreEventMatcherTests, ported: the cross-device same-meeting rule.</summary>
    [TestClass]
    public class LateNoMoreEventMatcherTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 26, 10, 0, 0);

        private static LateNoMoreMeeting M(string title, double offsetSeconds = 0, string id = null) =>
            new LateNoMoreMeeting(id, title, Start.AddSeconds(offsetSeconds));

        [TestMethod]
        public void MatchesOnExactIdEvenWhenTitleAndStartDiffer() =>
            Assert.IsTrue(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", 3600, "evt-123"), M("Completely different", 0, "evt-123")));

        [TestMethod]
        public void DifferentIdsStillFallBackToTitleAndTime() =>
            Assert.IsTrue(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", 0, "local"), M("Standup", 10, "google-xyz")));

        [TestMethod]
        public void EmptyIdsAreNotAnIdMatch() =>
            Assert.IsFalse(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", 0, ""), M("Retro", 0, "")));

        [TestMethod]
        public void TitlesCompareTrimmedAndCaseFolded() =>
            Assert.IsTrue(LateNoMoreEventMatcher.IsSameMeeting(M("  Daily STANDUP \n"), M("daily standup")));

        [TestMethod]
        public void TwoUntitledMeetingsAreNotCollapsed() =>
            Assert.IsFalse(LateNoMoreEventMatcher.IsSameMeeting(M("   "), M("")));

        [TestMethod]
        public void StartsWithinSixtySecondsMatch()
        {
            Assert.IsTrue(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", 59), M("Standup")));
            Assert.IsTrue(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", -60), M("Standup")));
            Assert.IsFalse(LateNoMoreEventMatcher.IsSameMeeting(M("Standup", 61), M("Standup")));
        }

        [TestMethod]
        public void DismissalsExpireFourHoursAfterTheMeeting()
        {
            var d = new LateNoMoreDismissals();
            d.Record(M("Standup"), Start);
            d.Record(M("standup", 5), Start);
            Assert.AreEqual(1, d.Count, "the same meeting is recorded once");
            Assert.IsTrue(d.IsDismissed(M("STANDUP", 30), Start.AddHours(1)));
            Assert.IsFalse(d.IsDismissed(M("Standup"), Start.AddHours(4).AddMinutes(1)));
            Assert.AreEqual(0, d.Count);
        }
    }

    [TestClass]
    public class MeetingLinkTests
    {
        [TestMethod]
        public void GoogleHangoutLinkComesFirst()
        {
            var e = JObject.Parse(@"{ ""summary"": ""Sync"", ""description"": ""https://zoom.us/j/123"", ""external_metadata"": { ""hangoutLink"": ""https://meet.google.com/abc-defg-hij"" } }");
            Assert.AreEqual("https://meet.google.com/abc-defg-hij", MeetingLink.FromBackendEvent(e));
        }

        [TestMethod]
        public void ConferenceDataVideoEntryPoint()
        {
            var e = JObject.Parse(@"{ ""external_metadata"": { ""conferenceData"": { ""entryPoints"": [
                { ""entryPointType"": ""phone"", ""uri"": ""tel:+1-555"" },
                { ""entryPointType"": ""video"", ""uri"": ""https://acme.zoom.us/j/99?pwd=x"" } ] } } }");
            Assert.AreEqual("https://acme.zoom.us/j/99?pwd=x", MeetingLink.FromBackendEvent(e));
        }

        [TestMethod]
        public void MicrosoftOnlineMeetingJoinUrl()
        {
            var e = JObject.Parse(@"{ ""external_metadata"": { ""onlineMeeting"": { ""joinUrl"": ""https://teams.microsoft.com/l/meetup-join/19%3a"" }, ""location"": { ""displayName"": ""Room 4"" } } }");
            Assert.AreEqual("https://teams.microsoft.com/l/meetup-join/19%3a", MeetingLink.FromBackendEvent(e));
        }

        [TestMethod]
        public void LocationThatIsALink()
        {
            var e = JObject.Parse(@"{ ""external_metadata"": { ""location"": ""https://example.com/room/7"" } }");
            Assert.AreEqual("https://example.com/room/7", MeetingLink.FromBackendEvent(e));
            Assert.IsNull(MeetingLink.FromBackendEvent(JObject.Parse(@"{ ""external_metadata"": { ""location"": ""Level 3, https://maps.example.com"" } }")),
                "a street address with a link in it is not a meeting link");
        }

        [TestMethod]
        public void DescriptionPatternsFromTheMacApp()
        {
            Assert.AreEqual("https://us02web.zoom.us/j/8812345?pwd=abc", MeetingLink.FromText("Join Zoom Meeting\nhttps://us02web.zoom.us/j/8812345?pwd=abc\nMeeting ID"));
            Assert.AreEqual("https://teams.live.com/meet/9387222203093?p=9IrX5g", MeetingLink.FromText("<a href=\"https://teams.live.com/meet/9387222203093?p=9IrX5g\">Join</a>"));
            Assert.AreEqual("https://acme.webex.com/meet/jo", MeetingLink.FromText("webex: https://acme.webex.com/meet/jo."));
            Assert.AreEqual("https://meet.jit.si/FocusBear", MeetingLink.FromText("room https://meet.jit.si/FocusBear"));
            Assert.AreEqual("https://zoom.us/j/1?a=1&b=2", MeetingLink.FromText("https://zoom.us/j/1?a=1&amp;b=2"), "HTML-escaped ampersands");
            Assert.IsNull(MeetingLink.FromText("Agenda: https://docs.google.com/doc/1"), "an ordinary link is not a meeting link");
            Assert.IsNull(MeetingLink.FromText(null));
        }

        [TestMethod]
        public void DeclinedInvitations()
        {
            Assert.IsTrue(MeetingLink.IsDeclined(JObject.Parse(@"{ ""external_metadata"": { ""attendees"": [ { ""email"": ""a"", ""responseStatus"": ""accepted"" }, { ""self"": true, ""responseStatus"": ""declined"" } ] } }")));
            Assert.IsFalse(MeetingLink.IsDeclined(JObject.Parse(@"{ ""external_metadata"": { ""attendees"": [ { ""self"": true, ""responseStatus"": ""accepted"" } ] } }")));
            Assert.IsTrue(MeetingLink.IsDeclined(JObject.Parse(@"{ ""external_metadata"": { ""responseStatus"": { ""response"": ""declined"" } } }")));
            Assert.IsFalse(MeetingLink.IsDeclined(JObject.Parse(@"{ ""summary"": ""x"" }")));
        }

        [TestMethod]
        public void ParsesCalendarDatas()
        {
            var root = JObject.Parse(@"{ ""calendars"": [
                { ""account"": ""me@x.io"", ""requires_reauth"": false, ""items"": [
                    { ""id"": ""u1"", ""calendar_id"": ""me@x.io"", ""displayName"": ""Work"", ""is_Selected"": true },
                    { ""id"": ""u2"", ""calendar_id"": ""holidays"", ""displayName"": ""Holidays"", ""is_Selected"": false } ] },
                { ""account"": ""old@x.io"", ""requires_reauth"": true, ""items"": [] } ], ""keywords"": [] }");
            var cals = LateNoMoreCalendar.ParseCalendarDatas(root, "google");
            Assert.AreEqual(2, cals.Count);
            Assert.AreEqual("me@x.io", cals[0].CalendarId, "the provider id, which events carry as calendar_id — not the row uuid");
            Assert.AreEqual("Work", cals[0].Name);
            Assert.IsTrue(cals[0].IsSynced);
            Assert.IsFalse(cals[1].IsSynced);
            Assert.AreEqual("me@x.io", cals[1].Account);
            Assert.AreEqual(0, LateNoMoreCalendar.ParseCalendarDatas(JArray.Parse("[]"), "google").Count);
        }
    }
}
