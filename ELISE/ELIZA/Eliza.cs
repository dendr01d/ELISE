using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELISE.ELIZA
{
    public class Eliza
    {
        public static Eliza FromScript(string path)
        {
            return new Eliza()
            {
                Script = ScriptReader.ReadFromFile(path)
            };
        }

        private Script Script = new();

        /// <summary>
        /// Check every word of the given text to see if it can be replaced according to the 
        /// </summary>
        private string Reflect(string text)
        {
            string[] words = text.ToLower().Split(' ').Select(x => x.Trim(",.:;\'\"!?".ToArray())).ToArray();

            for (int i = 0; i < words.Length; ++i)
            {
                if (this.Script.Reflexives.TryGetValue(words[i], out string? mirror) && mirror != null)
                {
                    words[i] = mirror;
                }
            }

            return String.Join(" ", words);
        }

        public string? Respond(string text)
        {
            foreach(var respPair in this.Script.Responses)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, respPair.Key);

                if (match != null && match.Success)
                {
                    string response = respPair.Value[Random.Shared.Next(respPair.Value.Count)];

                    while (response.Contains('%'))
                    {
                        int placeHolderIndex = response.IndexOf('%');
                        int placeHolderValue = int.Parse(response[placeHolderIndex + 1].ToString());

                        string replacement = this.Reflect(match.Groups[placeHolderValue].Value);

                        response = response.Replace($"%{placeHolderValue}", replacement);
                    }

                    if (response[^2..] == "?.") response = response[..^2] + ".";
                    if (response[^2..] == "??") response = response[..^2] + "?";

                    return response.ToUpper();
                }
            }

            return null;
        }
    }
}
