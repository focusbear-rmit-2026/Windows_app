using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    /// <summary>
    /// Late No More's endpoints. <c>/late-no-more/config</c> is new (backend PR feat/late-no-more-config): until it is
    /// deployed it answers 404 and <see cref="LateNoMoreConfigSync"/> keeps the settings on this device.
    /// The dismissal report is the Mac's <c>reportLateNoMoreDismissal</c> (backend#2395), already live.
    /// </summary>
    partial class FocusBearAPIClient
    {
        internal const string LateNoMoreConfigUrl = ApiBase + "/late-no-more/config";

        internal async Task<LateNoMoreRemoteResult> GetLateNoMoreConfig()
        {
            ApiResponse r = await SendJsonRequest("GET", LateNoMoreConfigUrl, null, "GetLateNoMoreConfig");
            return new LateNoMoreRemoteResult(r.Status, r.Body);
        }

        internal async Task<LateNoMoreRemoteResult> PutLateNoMoreConfig(string json)
        {
            ApiResponse r = await SendJsonRequest("PUT", LateNoMoreConfigUrl, JToken.Parse(json), "PutLateNoMoreConfig");
            return new LateNoMoreRemoteResult(r.Status, r.Body);
        }

        /// <summary>
        /// <c>POST /notifications/calendar-events/dismiss</c> so the user's other devices stop alerting for this meeting.
        /// Best effort: the local dismissal already happened, and a failure is only logged.
        /// </summary>
        internal async Task<bool> ReportLateNoMoreDismissal(LateNoMoreMeeting meeting, string reason)
        {
            string summary = (meeting?.Title ?? "");
            if (summary.Length > LateNoMoreSyncContract.MaxSummaryLength) summary = summary.Substring(0, LateNoMoreSyncContract.MaxSummaryLength);
            if (summary.Trim().Length == 0) { LogWriter.LogWrite("late-no-more dismiss: skipped, meeting has no title"); return false; }
            string why = reason ?? LateNoMoreSyncContract.ReasonStopReminders;
            if (why.Length > LateNoMoreSyncContract.MaxDismissReasonLength) why = why.Substring(0, LateNoMoreSyncContract.MaxDismissReasonLength);
            var body = new JObject
            {
                ["summary"] = summary,
                ["event_begins"] = meeting.Start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["dismiss_reason"] = why,
            };
            // Lets the Pusher echo of our own dismissal be dropped (PusherHandler skips events carrying this device's id).
            if (Guid.TryParse(appSettings.sDeviceID, out _)) body["device_id"] = appSettings.sDeviceID;
            ApiResponse r = await SendJsonRequest("POST", ApiBase + "/" + LateNoMoreSyncContract.DismissEndpointPath, body, "ReportLateNoMoreDismissal");
            LogWriter.LogWrite("late-no-more dismiss (" + why + "): " + (r.IsSuccess ? "ok" : "status " + r.Status));
            return r.IsSuccess;
        }
    }

    /// <summary>The config endpoint over the app's API client.</summary>
    internal sealed class LateNoMoreApiRemote : ILateNoMoreConfigRemote
    {
        public Task<LateNoMoreRemoteResult> GetAsync() => new FocusBearAPIClient().GetLateNoMoreConfig();
        public Task<LateNoMoreRemoteResult> PutAsync(string json) => new FocusBearAPIClient().PutLateNoMoreConfig(json);
    }
}
