using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Focus_Bear.Services.VoiceCommand
{
    // Snake_case members mirror the backend's VoiceIntentResponseDto key-for-key, so the intent JSON
    // deserializes without mapping attributes.
    public class IntentResponse
    {
        // The verbatim transcript, filled in client-side from /voice/transcribe (not part of the intent).
        public string transcription { get; set; }
        public string action { get; set; }
        public IntentParameters parameters { get; set; }
        public string confidence { get; set; }
        public string clarification_needed { get; set; }
        public List<IntentAdditionalAction> additional_actions { get; set; }
    }

    public class IntentParameters
    {
        public string task { get; set; }
        public string time { get; set; }
        public string repeat { get; set; }
        // Nullable: the backend omits it (or sends null) for actions without a duration, and a plain
        // int would throw on null.
        public int? duration_minutes { get; set; }
        public string goal { get; set; }
        public string habit_name { get; set; }
    }

    public class IntentAdditionalAction
    {
        public string action { get; set; }
        // Free-form per action (the backend sends every value as a string), so kept as raw JSON.
        public JObject parameters { get; set; }
    }

    // Response of POST /voice/transcribe. Text is empty when nothing intelligible was said.
    public class VoiceTranscription
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    // Body of POST /voice/intent.
    public class VoiceIntentRequest
    {
        [JsonProperty("transcript")]
        public string Transcript { get; set; }

        [JsonProperty("habits", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Habits { get; set; }

        // Local "yyyy-MM-dd HH:mm:ss" so relative times ("tomorrow 9am") resolve in the user's zone.
        // No IANA timezone is sent: .NET Framework only knows Windows zone ids.
        [JsonProperty("clientDatetime", NullValueHandling = NullValueHandling.Ignore)]
        public string ClientDatetime { get; set; }
    }
}
