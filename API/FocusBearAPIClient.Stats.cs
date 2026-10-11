using Focus_Bear.Utils;
using FocusBear.Core.Stats;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    /// <summary>
    /// The endpoints the native Stats screen uses — what the web dashboard's /webview/stats page requested
    /// (<c>store/reducer/stats/extra.ts</c>) and the Mac's <c>StatsApi.swift</c> calls, so the three show the
    /// same numbers. Bodies are returned raw for <c>FocusBear.Core.Stats.StatsWire</c> to read.
    /// </summary>
    partial class FocusBearAPIClient
    {
        /// <summary>Request sizes shared with the web (<c>HABIT_CONSISTENCY</c>, <c>DAYS_TO_FETCH_HEATMAP</c>, <c>LEADER_BOARD.STAT_LIMIT</c>).</summary>
        internal const int StatsConsistencyDays = 90;
        internal const int StatsMaxConsistencyActivityIds = 100;
        internal const int StatsLeaderboardLimit = 20;

        private static string Q(string value) => Uri.EscapeDataString(value ?? "");

        private async Task<string> StatsGet(string path, string caller)
        {
            var response = await SendJsonRequest("GET", ApiBase + path, null, caller);
            return response.IsSuccess ? response.Body : null;
        }

        private async Task<bool> StatsWrite(string method, string path, JObject body, string caller)
        {
            return (await SendJsonRequest(method, ApiBase + path, body, caller)).IsSuccess;
        }

        internal Task<string> GetOnboardingStats() => StatsGet("/user/stats/onboarding", "GetOnboardingStats");

        internal Task<string> GetLongTermGoals() => StatsGet("/user/long-term-goals", "GetLongTermGoals");

        internal Task<bool> UpdateLongTermGoals(IEnumerable<string> goals) =>
            StatsWrite("PATCH", "/user/long-term-goals", StatsWire.GoalsBody(goals), "UpdateLongTermGoals");

        /// <summary>The routine activities (and account creation date) the Habit Stats tab ranks.</summary>
        internal Task<string> GetStatsUserSettings() => StatsGet("/user-settings", "GetStatsUserSettings");

        /// <summary><c>POST /completed-activity/consistency-stats</c> — read-only despite the verb (bulk ids in the body).</summary>
        internal async Task<string> GetConsistencyStats(IEnumerable<string> activityIds, string timezone)
        {
            var body = StatsWire.ConsistencyBody(activityIds.Take(StatsMaxConsistencyActivityIds), StatsConsistencyDays, timezone);
            var response = await SendJsonRequest("POST", ApiBase + "/completed-activity/consistency-stats", body, "GetConsistencyStats");
            return response.IsSuccess ? response.Body : null;
        }

        internal Task<string> GetActivityStats(string activityId, int days, string timezone) =>
            StatsGet("/completed-activity/" + Q(activityId) + "/stats?days_number=" + days + "&timezone=" + Q(timezone), "GetActivityStats");

        internal Task<string> GetLogQuestionStats(string questionId) =>
            StatsGet("/completed-activity/log-question/" + Q(questionId) + "/stats", "GetLogQuestionStats");

        internal Task<string> GetCompletedFocusBlocks(DateTime from, DateTime to) =>
            StatsGet("/completed-focus-blocks?from_time=" + Q(StatsWire.Iso(from)) + "&to_time=" + Q(StatsWire.Iso(to)), "GetCompletedFocusBlocks");

        internal Task<string> GetFocusLog(int limit, int offset) =>
            StatsGet("/user/weekly-focus-block-summary?limit=" + limit + "&offset=" + offset, "GetFocusLog");

        internal Task<bool> CreateFocusBlock(JObject body) => StatsWrite("POST", "/user/weekly-focus-block-summary", body, "CreateFocusBlock");

        internal Task<bool> UpdateFocusBlock(string id, JObject body) => StatsWrite("PATCH", "/user/weekly-focus-block-summary/" + Q(id), body, "UpdateFocusBlock");

        internal Task<bool> DeleteFocusBlock(string id) => StatsWrite("DELETE", "/user/weekly-focus-block-summary/" + Q(id), null, "DeleteFocusBlock");

        internal Task<string> GetFocusModeSummaries() => StatsGet("/user/focus-modes", "GetFocusModeSummaries");

        /// <summary>
        /// <c>streak_type</c> picks which streak the server ranks by — and therefore which users make the top
        /// <paramref name="limit"/> among those active in the last 30 days.
        /// </summary>
        internal Task<string> GetLeaderboard(int limit, string streakType) =>
            StatsGet("/user-stats/leaderboard?limit=" + limit + "&streak_type=" + Q(streakType), "GetLeaderboard");

        internal Task<bool> UpdateUsername(string username) => StatsWrite("PUT", "/user/username", StatsWire.UsernameBody(username), "UpdateUsername");

        private static readonly TimeSpan PepTalkFirstByteTimeout = TimeSpan.FromSeconds(45);

        private static async Task<HttpResponseMessage> SendWithin(HttpClient client, HttpRequestMessage request, CancellationTokenSource cancel, TimeSpan limit)
        {
            cancel.CancelAfter(limit);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            cancel.CancelAfter(Timeout.InfiniteTimeSpan);
            return response;
        }

        /// <summary>
        /// Streams <c>GET /user/motivational-summary</c> (text/event-stream). <paramref name="onChunk"/> runs on
        /// the calling context for each piece of text; the result says whether any text arrived, and why not.
        /// Never logs the body: after [DONE] the server streams its own prompt.
        /// </summary>
        internal async Task<(bool Succeeded, PepTalkFailure Failure)> StreamPepTalk(string tone, string language, Action<string> onChunk, CancellationToken cancel)
        {
            try
            {
                if (!appSettings.ValidateTokenValidBeforeAPICall() && await RenewToken() == 0)
                    return (false, PepTalkFailure.Other);
                string url = ApiBase + "/user/motivational-summary?tone=" + Q(tone) + "&device_type=" + PepTalkTones.DesktopDeviceType + "&language=" + Q(string.IsNullOrEmpty(language) ? "en" : language);
                using (HttpClient client = GetClientWithHeaders())
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                    request.Headers.Accept.ParseAdd("text/event-stream");
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    // The client has no timeout (the stream runs as long as the talk), so the wait for the first
                    // byte gets its own: a server that never answers used to leave the loading bar up for good.
                    using (var firstByte = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                    using (var response = await SendWithin(client, request, firstByte, PepTalkFirstByteTimeout))
                    {
                        int status = (int)response.StatusCode;
                        string mediaType = response.Content?.Headers?.ContentType?.MediaType ?? "";
                        LogWriter.LogWrite("pep talk stream: status=" + status + " eventStream=" + (mediaType == "text/event-stream"));
                        if (status < 200 || status >= 300 || mediaType != "text/event-stream")
                        {
                            string retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(CultureInfo.InvariantCulture)
                                                ?? (response.Headers.TryGetValues("Retry-After", out var values) ? values.FirstOrDefault() : null);
                            return (false, PepTalkFailure.From(status, retryAfter));
                        }
                        var decoder = new PepTalkStreamDecoder();
                        bool receivedText = false;
                        var buffer = new byte[4096];
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            while (!cancel.IsCancellationRequested)
                            {
                                int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancel);
                                if (read <= 0) break;
                                foreach (var e in decoder.Consume(buffer, 0, read))
                                {
                                    switch (e.Kind)
                                    {
                                        case PepTalkEventKind.Text: receivedText = true; onChunk(e.Text); break;
                                        case PepTalkEventKind.Done:
                                            LogWriter.LogWrite("pep talk stream: finished reason=done receivedText=" + receivedText);
                                            return (receivedText, receivedText ? null : PepTalkFailure.Other);
                                        case PepTalkEventKind.Fatal:
                                            LogWriter.LogWrite("pep talk stream: finished reason=server_fatal");
                                            return (false, PepTalkFailure.ServerError);
                                        case PepTalkEventKind.Invalid:
                                            LogWriter.LogWrite("pep talk stream: finished reason=invalid_stream");
                                            return (false, PepTalkFailure.Other);
                                    }
                                }
                            }
                        }
                        LogWriter.LogWrite("pep talk stream: finished reason=eof receivedText=" + receivedText);
                        return (receivedText, receivedText ? null : PepTalkFailure.Other);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (!cancel.IsCancellationRequested)
                {
                    LogWriter.LogWrite("pep talk stream: finished reason=no_response");
                    return (false, PepTalkFailure.Connection);
                }
                return (false, PepTalkFailure.Other);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("pep talk stream: transport - " + ex.GetType().Name);
                return (false, PepTalkFailure.Connection);
            }
        }
    }
}
