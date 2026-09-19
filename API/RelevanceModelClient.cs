using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.API
{
    internal class RelevanceModelClient
    {
        private const string PortFileName = "relevance-model.json";

        private int GetModelPort()
        {
            string portFilePath = Path.Combine(
                AppSettings.AppDataDirPath,
                PortFileName
            );

            if (!File.Exists(portFilePath))
            {
                throw new FileNotFoundException(
                    "Relevance model port file was not found.",
                    portFilePath
                );
            }

            string json = File.ReadAllText(portFilePath);
            JObject portInfo = JObject.Parse(json);

            JToken portToken = portInfo["port"];

            if (portToken == null)
            {
                throw new InvalidDataException(
                    "Relevance model port file does not contain a port."
                );
            }

            return portToken.Value<int>();
        }

        internal async Task<JObject> Predict(
            string focusIntention,
            string tabTitle,
            string url)
        {
            int port = GetModelPort();

            string endpoint =
                $"http://127.0.0.1:{port}/predict";

            JObject payload = new JObject
            {
                ["focus_intention"] = focusIntention ?? "",
                ["tab_title"] = tabTitle ?? "",
                ["url"] = url ?? ""
            };

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                StringContent content = new StringContent(
                    payload.ToString(),
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response =
                    await client.PostAsync(endpoint, content);

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Relevance model returned {(int)response.StatusCode}: {responseBody}"
                    );
                }

                return JObject.Parse(responseBody);
            }
        }
    }
}