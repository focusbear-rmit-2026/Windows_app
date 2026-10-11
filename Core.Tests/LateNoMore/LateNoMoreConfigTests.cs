using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.LateNoMore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.LateNoMore
{
    [TestClass]
    public class LateNoMoreConfigTests
    {
        [TestMethod]
        public void DefaultsAreTheMacLateNoMoreDefaults()
        {
            var c = LateNoMoreConfig.Defaults();
            Assert.IsTrue(c.Enabled);
            Assert.IsNull(c.CalendarIds, "every synced calendar");
            CollectionAssert.AreEqual(new[] { 15, 2 }, c.AlertLeadMinutes, "the 15-minute heads-up and the 2-minute alert");
            Assert.AreEqual(2, c.MainAlertMinutes);
            Assert.AreEqual(15, c.HeadsUpMinutes);
            Assert.AreEqual("07:00", c.ActiveHoursStart);
            Assert.AreEqual("21:00", c.ActiveHoursEnd);
            CollectionAssert.AreEqual(new[] { "no-notify" }, c.IgnoredKeywords);
            Assert.IsFalse(c.RequireMeetingUrl);
            Assert.IsTrue(c.VerbalAlertsEnabled);
        }

        [TestMethod]
        public void RoundTripsTheEndpointShape()
        {
            var c = LateNoMoreConfig.Defaults();
            c.Enabled = false;
            c.CalendarIds = new List<string> { "primary@x.io", "team" };
            c.SetLeads(5, 30);
            c.ActiveHoursStart = "08:30";
            c.IgnoredKeywords.Add("Focus time");
            c.RequireMeetingUrl = true;
            c.VerbalAlertsEnabled = false;

            var o = JObject.Parse(c.ToJson());
            CollectionAssert.AreEquivalent(new[] { "enabled", "calendar_ids", "alert_lead_minutes", "active_hours_start", "active_hours_end",
                "ignored_keywords", "require_meeting_url", "verbal_alerts_enabled", "created_at", "updated_at" },
                o.Properties().Select(p => p.Name).ToList());

            var back = LateNoMoreConfig.FromJson(c.ToJson());
            Assert.IsFalse(back.Enabled);
            CollectionAssert.AreEqual(new[] { "primary@x.io", "team" }, back.CalendarIds);
            CollectionAssert.AreEqual(new[] { 30, 5 }, back.AlertLeadMinutes);
            Assert.AreEqual("08:30", back.ActiveHoursStart);
            CollectionAssert.AreEqual(new[] { "no-notify", "Focus time" }, back.IgnoredKeywords);
            Assert.IsTrue(back.RequireMeetingUrl);
            Assert.IsFalse(back.VerbalAlertsEnabled);
        }

        [TestMethod]
        public void TheUpdateBodyLeavesOutTheServerTimestamps()
        {
            var o = JObject.Parse(LateNoMoreConfig.Defaults().ToUpdateJson());
            Assert.IsNull(o["created_at"]);
            Assert.IsNull(o["updated_at"]);
            Assert.AreEqual(JTokenType.Null, o["calendar_ids"].Type, "null means all calendars and must be sent as null");
        }

        [TestMethod]
        public void ReadsAServerResponseWithTimestampsAndMissingFields()
        {
            var c = LateNoMoreConfig.FromJson(@"{ ""enabled"": true, ""alert_lead_minutes"": [2], ""created_at"": ""2026-09-26T01:02:03.000Z"", ""updated_at"": null }");
            CollectionAssert.AreEqual(new[] { 2 }, c.AlertLeadMinutes);
            Assert.IsNull(c.HeadsUpMinutes, "one lead is the main alert only");
            Assert.AreEqual(new DateTime(2026, 9, 26, 1, 2, 3, DateTimeKind.Utc), c.CreatedAt);
            Assert.IsNull(c.UpdatedAt);
            Assert.AreEqual("07:00", c.ActiveHoursStart, "a missing field keeps its default");
            CollectionAssert.AreEqual(new[] { "no-notify" }, c.IgnoredKeywords);
        }

        [TestMethod]
        public void ReadsAWrappedResponseAndRejectsGarbage()
        {
            Assert.IsFalse(LateNoMoreConfig.FromJson(@"{ ""data"": { ""enabled"": false } }").Enabled);
            Assert.IsNull(LateNoMoreConfig.FromJson("not json"));
            Assert.IsNull(LateNoMoreConfig.FromJson(""));
            Assert.IsNull(LateNoMoreConfig.FromJson("[1,2]"));
        }

        [TestMethod]
        public void NormalizeClampsWhatTheBackendWouldReject()
        {
            var c = new LateNoMoreConfig
            {
                AlertLeadMinutes = new List<int> { 2, 2, 500, -1, 15, 10, 5, 1, 0 },
                IgnoredKeywords = new List<string> { "  standup ", "STANDUP", "", "   ", new string('x', 101) },
                ActiveHoursStart = "25:00",
                ActiveHoursEnd = "9:5",
                CalendarIds = new List<string> { "a", "a", " ", "b" },
            }.Normalize();
            CollectionAssert.AreEqual(new[] { 15, 10, 5, 2, 1 }, c.AlertLeadMinutes, "0..120, unique, five at most, largest first");
            CollectionAssert.AreEqual(new[] { "standup" }, c.IgnoredKeywords, "trimmed, de-duplicated ignoring case, blanks and over-long dropped");
            Assert.AreEqual("07:00", c.ActiveHoursStart);
            Assert.AreEqual("21:00", c.ActiveHoursEnd, "minutes need two digits");
            CollectionAssert.AreEqual(new[] { "a", "b" }, c.CalendarIds);

            Assert.IsTrue(new LateNoMoreConfig { AlertLeadMinutes = new List<int>() }.Normalize().AlertLeadMinutes.SequenceEqual(new[] { 15, 2 }),
                "no leads left falls back to the defaults");
        }

        [TestMethod]
        public void SetLeadsDropsAHeadsUpThatIsNotEarlier()
        {
            var c = LateNoMoreConfig.Defaults();
            c.SetLeads(5, 5);
            CollectionAssert.AreEqual(new[] { 5 }, c.AlertLeadMinutes);
            c.SetLeads(2, null);
            CollectionAssert.AreEqual(new[] { 2 }, c.AlertLeadMinutes);
            c.SetLeads(1, 10);
            CollectionAssert.AreEqual(new[] { 10, 1 }, c.AlertLeadMinutes);
        }

        [TestMethod]
        public void KeywordsAddAndRemoveAsChips()
        {
            var c = LateNoMoreConfig.Defaults();
            Assert.IsTrue(c.AddKeyword("  Lunch "));
            Assert.IsFalse(c.AddKeyword("lunch"), "duplicates ignoring case");
            Assert.IsFalse(c.AddKeyword("   "));
            Assert.IsFalse(c.AddKeyword(new string('y', 101)));
            CollectionAssert.AreEqual(new[] { "no-notify", "Lunch" }, c.IgnoredKeywords);
            Assert.IsTrue(c.RemoveKeyword("LUNCH"));
            Assert.IsFalse(c.RemoveKeyword("nothing"));
            CollectionAssert.AreEqual(new[] { "no-notify" }, c.IgnoredKeywords);
        }

        [TestMethod]
        public void CalendarTogglesStoreAllAsNull()
        {
            var all = new[] { "a", "b", "c" };
            var c = LateNoMoreConfig.Defaults();
            Assert.IsTrue(c.IsCalendarOn("anything"));
            c.SetCalendarOn("b", false, all);
            CollectionAssert.AreEqual(new[] { "a", "c" }, c.CalendarIds, "switching one off turns 'all' into the explicit rest");
            Assert.IsFalse(c.IsCalendarOn("b"));
            Assert.IsFalse(c.IsCalendarOn("d"), "a calendar connected later is off once a list is explicit");
            c.SetCalendarOn("b", true, all);
            Assert.IsNull(c.CalendarIds, "every calendar on again is 'all' again");
        }

        [TestMethod]
        public void ActiveHoursAreInclusiveAndWrapPastMidnight()
        {
            var c = LateNoMoreConfig.Defaults();
            var day = new DateTime(2026, 9, 26);
            Assert.IsTrue(c.IsWithinActiveHours(day.AddHours(7)));
            Assert.IsTrue(c.IsWithinActiveHours(day.AddHours(21)));
            Assert.IsFalse(c.IsWithinActiveHours(day.AddHours(21).AddMinutes(1)));
            Assert.IsFalse(c.IsWithinActiveHours(day.AddHours(6).AddMinutes(59)));

            c.ActiveHoursStart = "22:00";
            c.ActiveHoursEnd = "02:00";
            Assert.IsTrue(c.IsWithinActiveHours(day.AddHours(23)));
            Assert.IsTrue(c.IsWithinActiveHours(day.AddHours(1)));
            Assert.IsFalse(c.IsWithinActiveHours(day.AddHours(12)));

            c.ActiveHoursEnd = "22:00";
            Assert.IsTrue(c.IsWithinActiveHours(day.AddHours(4)), "equal ends mean all day");
        }

        [TestMethod]
        public void ClockStringsParseStrictly()
        {
            Assert.IsTrue(LateNoMoreConfig.TryParseClock("07:05", out int m));
            Assert.AreEqual(425, m);
            Assert.IsTrue(LateNoMoreConfig.TryParseClock("7:05", out _));
            Assert.IsFalse(LateNoMoreConfig.TryParseClock("07:5", out _));
            Assert.IsFalse(LateNoMoreConfig.TryParseClock("24:00", out _));
            Assert.IsFalse(LateNoMoreConfig.TryParseClock("-1:00", out _));
            Assert.AreEqual("00:00", LateNoMoreConfig.FormatClock(1440));
            Assert.AreEqual("23:59", LateNoMoreConfig.FormatClock(-1));
        }
    }
}
