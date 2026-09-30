using Focus_Bear.Utils;
using FocusBear.Core.Postpone;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    /// <summary>The AI accountability buddy's endpoint — the Mac's <c>evaluateAccountabilityConversation</c>.</summary>
    partial class FocusBearAPIClient
    {
        /// <summary>
        /// <c>POST /user/evaluate-focus-postponement</c> with the whole conversation so far (answers 201).
        /// A failure comes back as offline (the connectivity check failed first) or unavailable (anything that
        /// went wrong after sending), which the chat turns into its recovery card — never into a postpone.
        /// </summary>
        internal async Task<AccountabilityReply> EvaluateFocusPostponement(JObject body)
        {
            // E2E test seam (E2EBuild.FakeBuddyVariable): a scripted answer instead of the AI backend.
            if (E2EBuild.TryFakeBuddy(body, out AccountabilityReply fake))
            {
                await Task.Delay(300);   // the "Checking…" line still shows, as with the real call
                return fake;
            }
            if (!await IsInternetOn())
            {
                LogWriter.LogWrite("EvaluateFocusPostponement: no internet");
                return AccountabilityReply.From(false, false, null);
            }
            var response = await SendJsonRequest("POST", ApiBase + "/user/evaluate-focus-postponement", body, "EvaluateFocusPostponement", checkInternet: false);
            return AccountabilityReply.From(true, response.IsSuccess, response.Body);
        }
    }
}
