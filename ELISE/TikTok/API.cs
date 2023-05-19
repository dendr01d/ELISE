using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace LIZa.TikTok
{
    internal class API
    {
        //adapted from https://github.com/oscie57/tiktok-voice/blob/main/main.py

        private static readonly HttpClient Client = new();

        private const string Session_ID = "d2fdb9937812f581f1541cbac39b37bb";
        private const string Speaker = "en_us_002";

        private static string Agent => @"com.zhiliaoapp.musically/2022600030 (Linux; U; Android 7.1.2; es_ES; SM-G988N; Build/NRD90M;tt-ok/3.12.13.1)";
        private static string GetCookie(string sessionID) => $"sessionid={sessionID}";
        private static string GetURL(string speaker, string text) => $"https://api16-normal-c-useast1a.tiktokv.com/media/api/text/speech/invoke/?text_speaker={speaker}&req_text={text}&speaker_map_type=0&aid=1233";


        private static string SanitizeText(string input)
        {
            return input.Replace("+", "plus").Replace(" ", "+").Replace("&", "and");
        }

        private static async Task<string?> SendRequest(string sessionID, string speaker, string text)
        {
            text = SanitizeText(text);

            Dictionary<string, string> headers = new()
            {
                { "User-Agent", Agent },
                { "Cookie", GetCookie(sessionID) }
            };
            string url = GetURL(speaker, text);

            var content = new FormUrlEncodedContent(headers);
            var response = await Client.PostAsync(url, content);
            var output = await response.Content.ReadAsStringAsync();

            return output;
        }

        private static TikTokResponse? ParseResponse(string? response)
        {
            if (response == null)
            {
                return null;
            }
            else
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<TikTokResponse>(response);

                if (parsed == null)
                {
                    throw new Exception("Couldn't parse response.");
                }
                else if (parsed.status_code != 0)
                {
                    throw new Exception($"Request failed, status code {parsed.status_code}: " + parsed.status_code switch
                    {
                        1 => "Incorrect 'aid' value in url",
                        2 => "Submitted text is too long",
                        4 => "Passed invalid 'speaker' value",
                        _ => "Unknown error"
                    });
                }
                else
                {
                    return parsed;
                }
            }
        }

        public static Task<byte[]> GetSpeech(string text)
        {
            return Task.Run(() =>
            {
                var response = SendRequest(Session_ID, Speaker, text);
                var parsed = ParseResponse(response.Result);
                return Convert.FromBase64String(parsed.data.v_str);
            });
        }
    }
}
