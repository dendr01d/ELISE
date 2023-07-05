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
        public static List<string> Errors = new();

        private const string MemoryRuleName = "@memory";
        private const string NoneRuleName = "@none";

        private static readonly Regex RGX_TAGLINE = new Regex(@"^\s*([^\s\:\=\#]+)\s*\:\=((?:\s*\w+\,?)+)$");

        private static readonly Regex RGX_Pair_OneWay = new Regex(@"^([^\<\>\#]+)>([^\<\>]+)$");
        private static readonly Regex RGX_Pair_TwoWay = new Regex(@"^([^\<\>\#]+)<>([^\<\>]+)$");

        private static readonly Regex RGX_PreKeyword = new Regex(@"^\s*([^\#].*)\s*\&\s*$");
        private static readonly Regex RGX_Keyword = new Regex(@"^\s*([^\s\:\#]+)\s*\:\:\s*(\d*)\s*$");
        private static readonly Regex RGX_Pattern = new Regex(@"^\s*""([^""\:]+)""\:$");
        private static readonly Regex RGX_Response = new Regex(@"^\s*([^\s\#][^\=\<\>]*(\=\w+)?)\s*$");

        public static Script ReadFromFile(string path)
        {
            Script output = new Script();
            IEnumerable<string> lines = File.ReadLines(path).Append(String.Empty);

            //first do a quick pass to gather all of the tag info
            //as well as all of the keyword names
            List<string> validKeywords = new();
            //might as well grab the swappairs here too

            int lineIndex = 1;

            foreach (string line in lines)
            {
                if (TryMatch(RGX_TAGLINE, line, out var tagMatch))
                {
                    string tag = tagMatch[1].Value;
                    if (!output.Tags.TryGetValue(tag, out List<string>? subs) || subs == null)
                    {
                        output.Tags[tag] = new();
                    }

                    output.Tags[tag].AddRange(tagMatch[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
                else if (TryMatch(RGX_Pair_OneWay, line, out var pairOneMatch))
                {
                    string word1 = pairOneMatch[1].Value.ToLower().Trim();
                    string word2 = pairOneMatch[2].Value.ToLower().Trim();

                    if (output.KeywordSubstitutions.ContainsKey(word1))
                    {
                        Errors.Add($"Keyword substitution for '{word1}' is already defined");
                    }
                    else
                    {
                        output.KeywordSubstitutions.Add(word1, word2);
                    }

                    //SwapPair newPair = new()
                    //{
                    //    Start = pairOneMatch[1].Value.ToLower().Trim(),
                    //    End = pairOneMatch[2].Value.ToLower().Trim(),
                    //    LineIndex = lineIndex
                    //};

                    //output.Pairs.Add(newPair);
                }
                else if (TryMatch(RGX_Pair_TwoWay, line, out var pairTwoMatch))
                {
                    string word1 = pairTwoMatch[1].Value.ToLower().Trim();
                    string word2 = pairTwoMatch[2].Value.ToLower().Trim();

                    if (output.KeywordSubstitutions.ContainsKey(word1))
                    {
                        Errors.Add($"Keyword substitution for '{word1}' is already defined");
                    }
                    else
                    {
                        output.KeywordSubstitutions.Add(word1, word2);
                    }

                    if (output.KeywordSubstitutions.ContainsKey(word2))
                    {
                        Errors.Add($"Keyword substitution for '{word2}' is already defined");
                    }
                    else
                    {
                        output.KeywordSubstitutions.Add(word2, word1);
                    }

                    //SwapPair newPair = new()
                    //{
                    //    Start = pairTwoMatch[1].Value.ToLower().Trim(),
                    //    End = pairTwoMatch[2].Value.ToLower().Trim(),
                    //    LineIndex = lineIndex
                    //};

                    //output.Pairs.Add(newPair);
                    //output.Pairs.Add(newPair.Invert());
                }
                else if (TryMatch(RGX_PreKeyword, line, out var precursorMatch))
                {
                    validKeywords.Add(precursorMatch[1].Value.ToLower().Trim());
                }
                else if (TryMatch(RGX_Keyword, line, out var keywordMatch))
                {
                    validKeywords.Add(keywordMatch[1].Value.ToLower().Trim());
                }
            }

            List<string> precursorKeywords = new();
            Rule? activeRule = null;
            Transformation? activeTransform = null;

            lineIndex = 1;
            bool panicState = false;

            foreach(string line in lines.Select(x => x.ToLower()))
            {
                if (TryMatch(RGX_TAGLINE, line, out var _)
                    || TryMatch(RGX_Pair_OneWay, line, out var _)
                    || TryMatch(RGX_Pair_TwoWay, line, out var _))
                {
                    ResolvePanic(lineIndex, ref panicState);
                    ValidateNewRule(ref output, ref activeRule, ref activeTransform);
                    // everything else handled in the previous pass
                }
                else if (TryMatch(RGX_PreKeyword, line, out var precursorMatch))
                {
                    ResolvePanic(lineIndex, ref panicState);
                    ValidateNewRule(ref output, ref activeRule, ref activeTransform);
                    precursorKeywords.Add(precursorMatch[1].Value.ToLower().Trim());
                }
                else if (TryMatch(RGX_Keyword, line, out var keywordMatch))
                {
                    ResolvePanic(lineIndex, ref panicState);
                    ValidateNewRule(ref output, ref activeRule, ref activeTransform);

                    int.TryParse(keywordMatch[2].Value, out int prio);

                    activeRule = new Rule();
                    activeRule.Keywords = new List<string>(precursorKeywords.ToArray());
                    activeRule.Ranking = new Rank(prio, lineIndex, activeRule);

                    activeRule.Keywords.Add(keywordMatch[1].Value);
                    precursorKeywords = new();
                }
                else if (activeRule != null && TryMatch(RGX_Pattern, line, out var patternMatch))
                {
                    ValidateNewTransformation(ref activeRule, ref activeTransform);

                    activeTransform = new Transformation()
                    {
                        DecompositionRule = patternMatch[1].Value.ToLower()
                    };
                }
                else if (activeRule != null && activeTransform != null && TryMatch(RGX_Response, line, out _))
                {
                    string response = line.ToLower().Trim();

                    if (Reassembly.IsPre(response, out string _, out string link) && !validKeywords.Contains(link))
                    {
                        panicState = true;
                        Errors.Add($"Link to non-existent keyword in response on line {lineIndex}");
                    }
                    else
                    {
                        activeTransform.ReassemblyRules.Add(response);
                    }
                }
                else if (!String.IsNullOrWhiteSpace(line) && line[0] != '#') //check for comments last
                {
                    panicState = true;
                    Errors.Add($"Parsing error on line {lineIndex}");
                }

                lineIndex++;
            }

            ValidateNewRule(ref output, ref activeRule, ref activeTransform);

            if (output.MemoryRule == null || output.NoneRule == null)
            {
                throw new Exception("Script doesn't contain definitions for @Memory or @None keywords");
            }

            return output;
        }

        private static void ResolvePanic(int lineIndex, ref bool panicking)
        {
            if (panicking)
            {
                panicking = false;
                Errors.Add($"Resuming read on line {lineIndex + 1}\n");
            }
        }

        private static void ValidateNewRule(ref Script sc, ref Rule? newRule, ref Transformation? newTrans)
        {
            if (newTrans != null)
            {
                ValidateNewTransformation(ref newRule, ref newTrans);
            }

            if (newRule != null)
            {
                foreach(var key in newRule.Keywords)
                {
                    if (key == MemoryRuleName)
                    {
                        sc.MemoryRule = newRule;
                    }
                    else if (key == NoneRuleName)
                    {
                        sc.NoneRule = newRule;
                    }
                    else
                    {
                        sc.Rules.Add(key, newRule);
                    }
                }
            }

            newRule = null;
        }

        private static void ValidateNewTransformation(ref Rule? r, ref Transformation? t)
        {
            if (r != null && t != null)
            {
                r.Transforms.Add(t);
            }

            t = null;
        }

        private static bool TryMatch(Regex rgx, string input, out GroupCollection groups)
        {
            groups = default;

            if (rgx.Match(input) is var match && match != null && match.Success)
            {
                groups = match.Groups;
                return true;
            }

            return false;
        }
    }
}
