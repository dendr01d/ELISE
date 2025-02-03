using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;

namespace ELISE.Weilbyte
{
    // Credit to Welbyte for the use of his API: https://github.com/Weilbyte/tiktok-tts

    internal static class API
    {
        private const string _SPEAKER = "en_us_002";
        private const string _ENDPOINT = "https://tiktok-tts.weilnet.workers.dev";
        private const string _DIR = "/api/generation";
        private const int _TEXT_LIMIT = 300;

        private static readonly HttpClient Client = new HttpClient()
        {
            BaseAddress = new Uri(_ENDPOINT)
        };

        public static bool TextWithinLimit(string text)
        {
            return new UTF8Encoding().GetByteCount(text) <= _TEXT_LIMIT;
        }

        private static HttpResponseMessage SendRequest(string speaker, string text)
        {
            Dictionary<string, string> reqValues = new()
            {
                { "text", text },
                { "voice", speaker }
            };

            var request = System.Text.Json.JsonSerializer.Serialize(reqValues, typeof(Dictionary<string, string>));

            var req = new HttpRequestMessage(HttpMethod.Post, _DIR);
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
                var response = SendRequest(_SPEAKER, text);
                return ParseResponse(response);
            });

        }

        public static void Speak(byte[] speech)
        {
            using (MemoryStream ms = new(speech))
            {
                using (var audio = new NAudio.Wave.StreamMediaFoundationReader(ms))
                using (var outputDevice = new NAudio.Wave.WaveOutEvent())
                {
                    outputDevice.Init(audio);
                    outputDevice.Play();

                    while (outputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        public static void GetSpeechAndSpeak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var request = GetSpeech(text);
                byte[] sound = request.Result;

                Speak(sound);
            }

        }

        public static void WriteToFile(byte[] speech, string outFilePath)
        {
            File.WriteAllBytes(outFilePath, speech);
        }
    }
}
