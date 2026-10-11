using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Models;
using FocusBear.Core;
using FocusBear.Core.Postpone;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Adapter between the WPF <see cref="FocusMode"/> and the portable merge rule in
    /// <see cref="FocusModeMetadataBuilder"/>. The rule itself lives in FocusBear.Core so it can be
    /// built and tested on any machine; this file exists only because <c>FocusMode</c> and
    /// <c>AppDetails</c> cannot follow it there.
    /// </summary>
    public static class FocusModeMetadata
    {
        public static string Serialize(FocusMode focusMode)
        {
            return FocusModeMetadataBuilder.Serialize(ToInput(focusMode));
        }

        public static JObject Build(FocusMode focusMode)
        {
            return FocusModeMetadataBuilder.Build(ToInput(focusMode));
        }

        /// <summary>
        /// One block list as <c>POST /focus-mode</c> and <c>PUT /focus-mode</c> take it: <c>{ id, name,
        /// allowed_urls, metadata }</c>, built as JSON rather than spliced into a string. The name used to be
        /// pasted between quotes as typed, so a block list called <c>Mum's "work"</c> (or one with a backslash)
        /// made the whole PUT unreadable, and every block list stopped syncing.
        /// </summary>
        public static JObject RequestBody(FocusMode focusMode)
        {
            return new JObject
            {
                ["id"] = focusMode.id,
                ["name"] = focusMode.name ?? "",
                ["allowed_urls"] = new JArray((focusMode.arrAllowedUrls ?? new System.Collections.ObjectModel.ObservableCollection<string>()).Where(u => u != null).Cast<object>().ToArray()),
                ["metadata"] = Build(focusMode),
            };
        }

        /// <summary>
        /// <c>{ focusNames: [{ id, name }] }</c> for the settings web views' <c>LoadFocusNames(...)</c>. Spliced
        /// as text, a block-list name carrying a quote broke the call — and, being script, a name such as
        /// <c>"}]});alert(1);//</c> (block lists can be shared by other people) ran in the signed-in web view.
        /// </summary>
        public static string FocusNamesScriptArgument(System.Collections.Generic.IEnumerable<FocusMode> focusModes)
        {
            var names = new JArray((focusModes ?? Enumerable.Empty<FocusMode>()).Where(m => m != null)
                .Select(m => new JObject { ["id"] = m.id, ["name"] = m.name ?? "" }).Cast<object>().ToArray());
            return new JObject { ["focusNames"] = names }.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>The <c>PUT /focus-mode</c> array; an empty list is <c>[]</c>, not the malformed <c>]</c> it used to be.</summary>
        public static JArray RequestBody(System.Collections.Generic.IEnumerable<FocusMode> focusModes)
        {
            return new JArray((focusModes ?? Enumerable.Empty<FocusMode>()).Where(m => m != null).Select(RequestBody).Cast<object>().ToArray());
        }

        /// <summary>
        /// The body of the bulk PUT /focus-mode: every mode the server knows about. The accountability buddy's
        /// temporary mode exists only on this computer and its id is not a UUID, so it is left out; sending it
        /// made the server reject the whole request. Null when there is nothing to send.
        /// </summary>
        public static string BulkUpdateBody(IEnumerable<FocusMode> focusModes)
        {
            var modes = (focusModes ?? Enumerable.Empty<FocusMode>()).Where(m => m != null && !PostponeFocusSession.IsPostponeMode(m.id)).ToList();
            return modes.Count == 0 ? null : RequestBody(modes).ToString(Newtonsoft.Json.Formatting.None);
        }

        internal static FocusModeMetadataInput ToInput(FocusMode focusMode)
        {
            return new FocusModeMetadataInput
            {
                ServerMetadata = focusMode.serverMetadata,
                BlockedUrls = focusMode.arrBlockedUrls.ToArray(),
                AllowedAppsWin = JArray.FromObject(focusMode.arrAllowedAppsWin.ToArray<AppDetails>()),
                Color = focusMode.color,
                LastTaskUsed = focusMode.lastTaskUsed,
                AlignmentScoreThreshold = focusMode.AlignmentScoreThreshold,
                IsAllowedAllApps = focusMode.isAllowedAllApps,
                IsAllowedListApproach = focusMode.isAllowedListApproach,
                IsDefault = focusMode.isDefault,
                IsMeetings = focusMode.isMeetings,
                IsLockedFocus = focusMode.isLockedFocus,
                IsRelaxFocus = focusMode.isRelaxFocus,
                IsDeepWorkFocus = focusMode.isDeepWorkFocus,
                IsWorkFocus = focusMode.isWorkFocus,
                AutoSuggestFocusModeOnAllowedUrl = focusMode.AutoSuggestFocusModeOnAllowedURL,
            };
        }
    }
}
