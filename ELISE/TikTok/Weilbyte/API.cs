using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;

namespace LIZa.TikTok.Weilbyte
{
    internal static class API
    {

        private const string Session_ID = "d2fdb9937812f581f1541cbac39b37bb";
        private const string Speaker = "en_us_002";

        private const string Endpoint = "https://tiktok-tts.weilnet.workers.dev";
        private const string RelativeAddress = "/api/generation";
        private const int Text_Size_Limit = 300;

        private static readonly HttpClient Client;

        static API()
        {
            Client = new();
            Client.BaseAddress = new Uri(Endpoint);
        }

        public static void Initialize() { }

        public static bool TextWithinLimit(string text)
        {
            return new UTF8Encoding().GetByteCount(text) <= Text_Size_Limit;
        }

        private static HttpResponseMessage SendRequest(string speaker, string text)
        {
            Dictionary<string, string> reqValues = new()
            {
                { "text", text },
                { "voice", speaker }
            };

            var request = System.Text.Json.JsonSerializer.Serialize(reqValues, typeof(Dictionary<string, string>));

            var req = new HttpRequestMessage(HttpMethod.Post, RelativeAddress);
            req.Content = new StringContent(request, Encoding.UTF8);
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            return Client.SendAsync(req).Result;
        }

        private static byte[] ParseResponse(HttpResponseMessage? response)
        {
            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception("The response could not be interpreted");
            }

            var parsed = Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().Result);

            if (parsed.TryGetValue("data", out var data) && data != null)
            {
                return Convert.FromBase64String(data.ToString());
            }
            else
            {
                throw new Exception("The response could not be parsed");
            }

        }

        public static Task<byte[]> GetSpeech(string text)
        {
            return Task.Run(() =>
            {
                var response = SendRequest(Speaker, text);
                return ParseResponse(response);
            });

        }

        public static void SpeakText(string text, out int duration)
        {
            duration = 0;

            if (!String.IsNullOrWhiteSpace(text))
            {
                var request = GetSpeech(text);
                byte[] sound = request.Result;

                using (MemoryStream ms = new(sound))
                {
                    using (var audio = new NAudio.Wave.StreamMediaFoundationReader(ms))
                    using (var outputDevice = new NAudio.Wave.WaveOutEvent())
                    {
                        duration = audio.TotalTime.Seconds / 2 * 500;

                        outputDevice.Init(audio);
                        outputDevice.Play();
                        while (outputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
            }

        }

    }
}
