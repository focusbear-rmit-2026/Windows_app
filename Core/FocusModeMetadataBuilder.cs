using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core
{
    /// <summary>
    /// Everything the metadata blob needs, with nothing WPF-shaped in it. The app fills this from
    /// its own <c>FocusMode</c>; keeping the contract here is what lets the merge rule be tested
    /// without a Windows machine.
    /// </summary>
    public sealed class FocusModeMetadataInput
    {
        /// <summary>The blob the server last sent, or null for a mode this client just created.</summary>
        public JObject ServerMetadata { get; set; }

        public IEnumerable<string> BlockedUrls { get; set; }

        /// <summary>
        /// Serialized by the caller, because the app's <c>AppDetails</c> lives in a file that pulls
        /// in COM interop and so cannot come along into a portable project.
        /// </summary>
        public JArray AllowedAppsWin { get; set; }

        public string Color { get; set; }
        public string LastTaskUsed { get; set; }
        public int AlignmentScoreThreshold { get; set; }
        public bool IsAllowedAllApps { get; set; }
        public bool IsAllowedListApproach { get; set; }
        public bool IsDefault { get; set; }
        public bool IsMeetings { get; set; }
        public bool IsLockedFocus { get; set; }
        public bool IsRelaxFocus { get; set; }
        public bool IsDeepWorkFocus { get; set; }
        public bool IsWorkFocus { get; set; }
        public bool AutoSuggestFocusModeOnAllowedUrl { get; set; }
    }

    /// <summary>
    /// Builds the <c>metadata</c> blob sent with a focus mode.
    ///
    /// The API replaces this column wholesale on every save — the update is a shallow spread of the
    /// request over the stored row — so any key a client omits is deleted, not merged. Assembling
    /// the blob from a fixed list of keys therefore deletes whatever another client stored there.
    /// Most visibly that was the Mac's <c>is_ai_enabled</c>: the Mac treats an absent value as
    /// "AI on", so editing a block list on Windows turned a user's per-block-list AI toggle back on.
    ///
    /// So: start from what the server last sent and overwrite only the keys this client owns.
    /// </summary>
    public static class FocusModeMetadataBuilder
    {
        public static JObject Build(FocusModeMetadataInput input)
        {
            JObject metadata = input.ServerMetadata != null
                ? (JObject)input.ServerMetadata.DeepClone()
                : new JObject();

            metadata["blockUrlsArray"] = JArray.FromObject(input.BlockedUrls ?? new string[0]);
            metadata["allowed_apps_win"] = input.AllowedAppsWin ?? new JArray();
            // Stored without the leading '#', the way the read path expects to find it.
            metadata["color"] = (input.Color ?? string.Empty).Replace("#", string.Empty);
            metadata["lastTaskUsed"] = input.LastTaskUsed;
            metadata["focusAlignmentScoreThreshold"] = input.AlignmentScoreThreshold;
            metadata["isAllowedAllApps"] = input.IsAllowedAllApps;
            metadata["isAllowedListApproach"] = input.IsAllowedListApproach;
            metadata["isDefault"] = input.IsDefault;
            metadata["isMeetings"] = input.IsMeetings;
            metadata["isLockedFocus"] = input.IsLockedFocus;
            metadata["isRelaxFocus"] = input.IsRelaxFocus;
            metadata["isDeepWorkFocus"] = input.IsDeepWorkFocus;
            metadata["isWorkFocus"] = input.IsWorkFocus;
            metadata["autoSuggestFocusModeOnAllowedURL"] = input.AutoSuggestFocusModeOnAllowedUrl;

            return metadata;
        }

        /// <summary>The blob as compact JSON, ready to embed in a request body.</summary>
        public static string Serialize(FocusModeMetadataInput input)
        {
            return Build(input).ToString(Formatting.None);
        }
    }
}
