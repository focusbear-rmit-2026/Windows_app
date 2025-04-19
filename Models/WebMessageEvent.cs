

using Newtonsoft.Json;

namespace Focus_Bear.Models
{
   public class WebMessageEvent
    {
        [JsonProperty("event")]
        public string Event { get; set; }
    }
}
