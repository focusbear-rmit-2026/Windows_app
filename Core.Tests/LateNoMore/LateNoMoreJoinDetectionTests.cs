using System;
using FocusBear.Core.LateNoMore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.LateNoMore
{
    /// <summary>
    /// Late No More works out for itself that the user has joined the meeting (report 2026-09-28_16-01-47: "Remove
    /// 'I'm attending' - it's meant to auto detect"). The Mac treats the microphone or camera being used by a meeting
    /// app as "on a call"; Windows adds the meeting's own page open in the browser.
    /// </summary>
    [TestClass]
    public class LateNoMoreJoinDetectionTests
    {
        private static readonly DateTime Ten = new DateTime(2026, 9, 28, 10, 0, 0);

        private static LateNoMoreEvent Meeting(string url = "https://meet.google.com/ubg-yquj-xjv", int minutes = 30) =>
            new LateNoMoreEvent("e1", "Weekly OKR", Ten, Ten.AddMinutes(minutes), "primary", "google", url, false);

        private static LateNoMoreJoinSignal Detect(LateNoMoreEvent m, DateTime now, bool onCall = false, string url = null) =>
            LateNoMoreJoinDetection.Detect(m, now, onCall, url);

        [TestMethod]
        public void BeingOnACallDuringTheMeetingCountsAsJoined()
        {
            Assert.AreEqual(LateNoMoreJoinSignal.OnCall, Detect(Meeting(), Ten.AddMinutes(1), onCall: true));
            Assert.AreEqual(LateNoMoreJoinSignal.OnCall, Detect(Meeting(), Ten.AddMinutes(-2), onCall: true), "joining a couple of minutes early");
        }

        [TestMethod]
        public void NothingIsDetectedWithoutASignal()
        {
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten.AddMinutes(1)));
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten.AddMinutes(1), url: "https://mail.google.com/mail/u/0/#inbox"));
        }

        [TestMethod]
        public void ACallLongBeforeOrAfterTheMeetingIsSomethingElse()
        {
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten.AddMinutes(-30), onCall: true), "an earlier call");
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten.AddMinutes(31), onCall: true), "after it ended");
        }

        [TestMethod]
        public void TheMeetingsOwnPageInTheBrowserCountsAsJoined()
        {
            var m = Meeting();
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "https://meet.google.com/ubg-yquj-xjv"));
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "meet.google.com/ubg-yquj-xjv"), "Chrome hides the scheme");
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "https://meet.google.com/ubg-yquj-xjv?authuser=1"));
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "HTTPS://Meet.Google.com/UBG-YQUJ-XJV/"));
        }

        [TestMethod]
        public void ADifferentMeetingOnTheSameServiceIsNotThisOne()
        {
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten, url: "https://meet.google.com/aaa-bbbb-ccc"));
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(Meeting(), Ten, url: "https://meet.google.com/"), "Meet's home page");
        }

        [TestMethod]
        public void ZoomsWebClientIsMatchedByTheMeetingNumber()
        {
            var m = Meeting("https://us02web.zoom.us/j/85123456789?pwd=abc");
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "https://app.zoom.us/wc/85123456789/join?fromPWA=1"));
            Assert.AreEqual(LateNoMoreJoinSignal.MeetingPage, Detect(m, Ten, url: "us02web.zoom.us/j/85123456789#success"));
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(m, Ten, url: "https://app.zoom.us/wc/99999999999/join"));
        }

        [TestMethod]
        public void AMeetingWithoutALinkIsDetectedOnlyByTheCall()
        {
            var m = Meeting(url: null);
            Assert.AreEqual(LateNoMoreJoinSignal.None, Detect(m, Ten, url: "https://meet.google.com/ubg-yquj-xjv"));
            Assert.AreEqual(LateNoMoreJoinSignal.OnCall, Detect(m, Ten, onCall: true));
        }

        [TestMethod]
        public void UrlMatchingToleratesJunk()
        {
            Assert.IsFalse(LateNoMoreJoinDetection.IsMeetingPage("https://meet.google.com/ubg-yquj-xjv", null));
            Assert.IsFalse(LateNoMoreJoinDetection.IsMeetingPage("https://meet.google.com/ubg-yquj-xjv", ""));
            Assert.IsFalse(LateNoMoreJoinDetection.IsMeetingPage("not a url", "also not"));
            Assert.IsFalse(LateNoMoreJoinDetection.IsMeetingPage("https://example.com", "https://example.com"), "a bare host is not a meeting");
        }
    }
}
