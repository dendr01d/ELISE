using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace ELISE.ELIZA
{
    public static class ScriptReader
    {
        public static Script ReadFromFile(string path)
        {
            Script output = new Script();

            string? currentKey = null;

            foreach(string line in System.IO.File.ReadLines(path))
            {
                if (String.IsNullOrWhiteSpace(line))
                {
                    currentKey = null;
                }

                string trimmedLine = line.Trim();

                if (Regex.Match(trimmedLine, @"^([^:]*):([^:]*)$") is var match && match != null && match.Success)
                {
                    output.Reflexives.Add(match.Groups[1].Value.ToLower().Trim(), match.Groups[2].Value.ToLower().Trim());
                }
                else if (currentKey != null)
                {
                    output.Responses[currentKey].Add(trimmedLine.ToLower());
                }
                else if (Regex.Match(trimmedLine, @"^([^:]*)\:\:$") is var keyMatch && keyMatch != null && keyMatch.Success)
                {
                    currentKey = keyMatch.Groups[1].Value.ToLower();
                    output.Responses.Add(currentKey, new List<string>());
                }
            }

            return output;
        }
    }
}
