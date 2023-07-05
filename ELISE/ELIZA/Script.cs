using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

using TagMap = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>;

namespace ELISE.ELIZA
{
    public class Script
    {
        internal Dictionary<string, Rule> Rules { get; set; } = new();

        internal Rule? MemoryRule { get; set; } = null;
        internal Rule? NoneRule { get; set; } = null;

        internal TagMap Tags { get; set; } = new();
        //internal SortedSet<SwapPair> Pairs { get; set; } = new();
        internal Dictionary<string, string> KeywordSubstitutions { get; set; } = new();

        internal IEnumerable<KeyValuePair<string, Rule>> GetKeywordMatches(string input)
        {
            return this.Rules.Where(x => Regex.IsMatch(input, $@"\b{x.Key}\b"));
        }

        public override string ToString()
        {
            return $"Script with {Rules.Count + 2} rules, {Tags.Count} tags. Memory rule {(MemoryRule != null ? "valid" : "invalid")}. None rule {(NoneRule != null ? "valid" : "invalid")}.";
            //, {Pairs.Count} pairs
        }
    }

    internal class Response
    {
        public readonly string RawInput;
        public readonly Script ParentScript;
        public readonly StringBuilder LogicLog;

        public string[] SplitWords = Array.Empty<string>();

        public string Text
        {
            get => String.Join(' ', this.SplitWords);
            set => this.SplitWords = value.Split(' ');
        }

        public string Keyword { get; set; } = String.Empty;

        //public string Output { get; set; } = String.Empty;
        //public string? NewKeyword { get; set; } = null;

        public Response(string input, Script parent, StringBuilder log)
        {
            this.RawInput = input;
            this.ParentScript = parent;
            this.LogicLog = log;

            this.Text = input;
        }

        public void UpdateKeyword(string? kw = null)
        {
            if (kw == null)
            {
                this.Keyword = "*";
            }
            else
            {
                this.Keyword = kw;
            }
        }

        public string[] ApplySubsToGroups(string[] groupValues)
        {
            string[][] words = groupValues.Select(x => x.Split(' ')).ToArray();

            for (int i = 0; i < words.Length; ++i)
            {
                for (int j = 0; j < words[i].Length; ++j)
                {
                    string word = words[i][j];

                    if (this.ParentScript.KeywordSubstitutions.TryGetValue(word, out string? sub) && sub != null)
                    {
                        words[i][j] = sub;
                    }
                }
            }

            return words.Select(x => String.Join(' ', x)).ToArray();
        }

        public override string ToString()
        {
            return $"<{this.Text}>";
        }
    }

    public struct Rank
    {
        public readonly int Priority;
        public readonly int ScriptIndex;
        internal readonly Rule Owner;

        internal Rank(int prio, int index, Rule rl)
        {
            Priority = prio;
            ScriptIndex = index;
            Owner = rl;
        }

        public static Rank MoreThan(Rank r)
        {
            return new Rank(r.Priority + 1, r.ScriptIndex, r.Owner);
        }

        public static Rank Max => new Rank(int.MaxValue, int.MinValue, new Rule());
    }

    internal class Rule
    {
        public List<string> Keywords { get; set; } = new();
        //public string? KeywordReplacement { get; set; } = null;

        //for sorting purposes
        public Rank Ranking { get; set; } = new();

        public List<Transformation> Transforms { get; set; } = new();

        public Transformation.Status TryApply(ref Response resp)
        {
            foreach (var trans in this.Transforms)
            {
                Transformation.Status result = trans.TryApply(ref resp);

                if (result != Transformation.Status.Fail)
                {
                    return result;
                }
            }

            return Transformation.Status.Fail;
        }

        public override string ToString()
        {
            return $"Rule on [{String.Join(", ", this.Keywords)}], prio {Ranking.Priority}, index {Ranking.ScriptIndex}, {Transforms.Count} transform{(Transforms.Count != 1 ? "s" : "ation")}";
        }
    }


    internal class Transformation
    {
        public string DecompositionRule { get; set; } = String.Empty;
        [JsonIgnore]
        private Regex? CachedDecomp = null;

        public List<string> ReassemblyRules { get; set; } = new();

        private int NextReassemblyIndex = 0;

        private string GetNextReassembly()
        {
            int index = NextReassemblyIndex;

            this.NextReassemblyIndex = (NextReassemblyIndex + 1) % ReassemblyRules.Count;
            return this.ReassemblyRules[index];
        }

        public enum Status
        {
            Succ, Fail, Link, Next, Prep
        }

        public Status TryApply(ref Response resp, int manualAssembly = -1)
        {
            resp.LogicLog.Append($"'{resp.Keyword}' --> \"{this.DecompositionRule}\" --> ");

            if (this.CachedDecomp == null)
            {
                this.CachedDecomp = Decomposition.CreateRegexFromPattern(this.DecompositionRule, resp.ParentScript.Tags);
            }

            if (Decomposition.TryApply(resp.Text, this.CachedDecomp, out string[] groups) && groups != null)
            {
                groups = resp.ApplySubsToGroups(groups);

                string rsm = manualAssembly > 0
                    ? this.ReassemblyRules[manualAssembly]
                    : GetNextReassembly();

                if (Reassembly.IsNewKey(rsm))
                {
                    resp.LogicLog.AppendLine(Reassembly.NewKey);
                    return Status.Next;
                }
                else if (Reassembly.IsLink(rsm))
                {
                    resp.Keyword = Reassembly.GetLink(rsm);

                    resp.LogicLog.AppendLine($"'{resp.Keyword}'");
                    return Status.Link;
                }
                else if (Reassembly.IsPre(rsm, out string reRule, out string reLink))
                {
                    resp.Text = Reassembly.Reassemble(reRule, groups, resp.ParentScript);
                    resp.Keyword = reLink;

                    resp.LogicLog.AppendLine($"'{resp.Keyword}'");
                    return Status.Prep;
                }
                else
                {
                    resp.Text = Reassembly.Reassemble(rsm, groups, resp.ParentScript);

                    resp.LogicLog.AppendLine("Success!");
                    return Status.Succ;
                }
            }


            resp.LogicLog.AppendLine("No Match");
            return Status.Fail;
        }

        public override string ToString()
        {
            return $"Transformation {{{DecompositionRule}}} with {ReassemblyRules.Count} reassembl{(ReassemblyRules.Count == 1 ? "y rules" : "ies")}.";
        }
    }

    internal static class Decomposition
    {
        private static readonly Regex Splitter = new Regex(@"(\(|\*|\)|\d+|\\?\b[^\s]+\b)");
        private const string RGX_UNGREEGY_PHRASE = @"(.*?)";
        private const string RGX_GREEDY_SPACE = @"\s*";
        private const string RGX_SOLO_WORD = @"[^\s]+";

        public static Regex CreateRegexFromPattern(string pattern, TagMap tags)
        {
            string[] pieces = Splitter.Matches(pattern).Select(x => x.Value).ToArray();
            StringBuilder sb = new();

            sb.Append('^');

            for (int i = 0; i < pieces.Length; ++i)
            {
                sb.Append(RGX_GREEDY_SPACE);

                if (int.TryParse(pieces[i], out int num))
                {
                    if (num == 0)
                    {
                        sb.Append(RGX_UNGREEGY_PHRASE);
                    }
                    else
                    {
                        sb.Append('(');
                        sb.Append(RGX_SOLO_WORD);

                        for (int j = 1; j < num; ++j)
                        {
                            sb.Append(RGX_GREEDY_SPACE);
                            sb.Append(RGX_SOLO_WORD);
                        }

                        sb.Append(')');
                    }
                }
                else if (pieces[i] == "(" && (i + 1 < pieces.Length) && pieces[i + 1] == "*")
                {
                    //we've entered a subpattern. consume words until we exit
                    i += 2; //skip the *

                    if (pieces[i] == ")")
                    {
                        //empty subpattern? why???
                        continue;
                    }

                    sb.Append('(');
                    sb.Append(pieces[i]);
                    //++i;

                    //while (i < pieces.Length && pieces[i] != ")")
                    //{
                    //    sb.Append('|').Append(pieces[i]);
                    //    ++i;
                    //}

                    for (++i; pieces[i] != ")"; ++i)
                    {
                        sb.Append('|').Append(pieces[i]);
                    }

                    sb.Append(')');
                }
                else if (pieces[i][0] == '\\' && tags.TryGetValue(pieces[i][1..], out var subs))
                {
                    sb.Append('(').Append(String.Join('|', subs)).Append(')');
                }
                else
                {
                    sb.Append('(').Append(pieces[i].Trim('\\')).Append(')');
                }
            }

            sb.Append(RGX_GREEDY_SPACE).Append('$');

            return new Regex(sb.ToString());
        }

        public static bool CanApply(string input, Regex decompRule, out Match? patternMatch)
        {
            if(decompRule.Match(input) is var m && m != null && m.Success)
            {
                patternMatch = m;
                return true;
            }

            patternMatch = null;
            return false;
        }

        public static bool TryApply(string input, Regex decompRule, out string[] groups)
        {
            if (Decomposition.CanApply(input, decompRule, out Match? patternMatch) && patternMatch != null)
            {
                groups = patternMatch.Groups.Values.Select(x => x.Value).ToArray();
                return true;
            }

            groups = Array.Empty<string>();
            return false;
        }
    }

    internal static class Reassembly
    {
        public const string NewKey = "@newkey";

        public static bool IsNewKey(string response) => response == NewKey;
        public static bool IsLink(string response) => response[0] == '=';
        public static string GetLink(string response) => response[1..];

        public static bool IsPre(string response, out string rule, out string link)
        {
            string final = response.Split(' ').Last();

            if (IsLink(final))
            {
                rule = Regex.Replace(response, @"\s+\=.*$", String.Empty);
                link = GetLink(final);
                return true;
            }

            rule = String.Empty;
            link = String.Empty;
            return false;
        }

        public static string Reassemble(string reassemblyRule, string[] groups, Script scr)
        {
            //first (0th) group is always the whole match, which is forbidden
            for (int i = 1; i < groups.Length; ++i)
            {
                string replacement = groups[i];
                //scr.Pairs.Any(x => x.TryApply(groups[i], ref replacement));
                //foreach(SwapPair pair in scr.Pairs)
                //{
                //    pair.TryApply(replacement, ref replacement);
                //}

                reassemblyRule = reassemblyRule.Replace($"%{i}", replacement);
            }

            //remove any errant spaces between words and sentence-terminating punctuation
            reassemblyRule = Regex.Replace(reassemblyRule, @"\s(\.|\?|\!|\,|\;|\:)", x => x.Groups[1].Value);

            //foreach(SwapPair pair in scr.Pairs)
            //{
            //    pair.TryApply(reassemblyRule, ref reassemblyRule);
            //}

            return reassemblyRule;
        }
    }

    internal class SwapPair : IComparable<SwapPair>
    {
        public string Start { get; init; } = String.Empty;
        public string End { get; init; } = String.Empty;

        //determines priority of application
        //they need to be defined within the script in a particular order to work properly
        public int LineIndex { get; set; } = 0;

        public int CompareTo(SwapPair? other)
        {
            if (other == null)
            {
                return -1;
            }
            else
            {
                //note that these are getting compared "backward" here
                //because I want to sort by most words to least
                int byLength = other.Start.Split(' ').Length.CompareTo(this.Start.Split(' ').Length);

                if (byLength != 0)
                {
                    return byLength;
                }
                else
                {
                    //index order can still work normally though
                    return this.LineIndex.CompareTo(other.LineIndex);
                }
            }
        }

        public bool TryApply(string text, ref string result)
        {
            result = text;

            if (Regex.IsMatch(text, @$"\b{Start}\b"))
            {
                result = Regex.Replace(text, @$"\b{Start}\b", End);
                return true;
            }
            //else if (this.Invertible && Regex.IsMatch(text, $"\b{End}\b"))
            //{
            //    result = Regex.Replace(text, $"\b{End}\b", Start);
            //    return true;
            //}

            return false;
        }

        public SwapPair Invert()
        {
            return new SwapPair()
            {
                Start = this.End,
                End = this.Start
            };
        }

        public override string ToString()
        {
            return $"{this.Start} --> {this.End}";
        }
    }
}
